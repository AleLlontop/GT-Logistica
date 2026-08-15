using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Corrige una factura ya emitida (FR-035 a FR-038, FR-031b).
///
/// <b>Cuatro campos y ninguno más</b>: el detalle, el CAE, su vencimiento y el vencimiento de pago. El
/// cliente, los viajes y los importes no están en <see cref="CorreccionRequest"/>, así que no hay nada
/// que ignorar — no se pueden mandar (FR-036, SC-013). Si están mal, la factura se anula y se emite una
/// Refacturación.
///
/// <b>No toca el estado ni la fecha de cobro</b> (FR-044, research §15.5): el cobro y la anulación son
/// recursos propios. Corregir el CAE de una factura <c>pagada</c> está permitido y la deja cobrada, con
/// su fecha intacta (US4 esc. 8).
///
/// <b>Regenera el documento y reemplaza al anterior</b> (FR-031b): los cuatro campos corregibles son
/// exactamente los cuatro que salen impresos, así que dejar el archivo viejo haría que la ficha y el
/// documento dijeran cosas distintas. Si no se puede regenerar, la corrección no queda guardada.
///
/// Agrega al historial una entrada de <b>corrección</b>: quién y cuándo, y nada más. La ausencia de
/// estado nuevo es la marca (FR-037).
/// </summary>
public class CorregirFactura(
    IRepositorioFacturas facturas,
    IArmadorDocumentoFactura armador,
    GestionarLogo logo,
    IAlmacenDeArchivos almacen,
    ConsultarFichaFactura fichas,
    TimeProvider reloj)
{
    public async Task<ResultadoFactura> EjecutarAsync(
        int id,
        CorreccionRequest peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var factura = await facturas.ObtenerParaModificarAsync(id, cancelacion);

        if (factura is null)
        {
            return new ResultadoFactura(ErrorFactura.NoEncontrada);
        }

        // El único estado que cierra la corrección. `pendiente`, `vencida` y `pagada` la admiten
        // (FR-038).
        if (factura.Estado is EstadoFactura.Anulada)
        {
            return new ResultadoFactura(
                ErrorFactura.FacturaAnuladaInmutable,
                Mensaje: MensajesFacturas.FacturaAnuladaInmutable);
        }

        // Una factura emitida no puede quedarse sin CAE ni sin su vencimiento: vaciarlos la dejaría sin
        // lo que le da validez fiscal (US4 esc. 6).
        if (string.IsNullOrWhiteSpace(peticion.Cae) || peticion.Cae.Trim().Length > 20)
        {
            return new ResultadoFactura(
                ErrorFactura.CaeRequerido,
                Campo: "cae",
                Mensaje: MensajesFacturas.CaeRequerido);
        }

        if (peticion.CaeVencimiento is null)
        {
            return new ResultadoFactura(
                ErrorFactura.CaeRequerido,
                Campo: "caeVencimiento",
                Mensaje: MensajesFacturas.CaeVencimientoRequerido);
        }

        if (peticion.VencimientoPago is null)
        {
            return new ResultadoFactura(ErrorFactura.DatosInvalidos, Campo: "vencimientoPago");
        }

        if (peticion.Detalle is { } detalle && detalle.Trim().Length > 500)
        {
            return new ResultadoFactura(ErrorFactura.DatosInvalidos, Campo: "detalle");
        }

        // Las mismas validaciones del alta, contra la fecha de facturación que **no** cambia: corregir
        // no mueve la fecha de la factura (FR-029, FR-030).
        if (peticion.CaeVencimiento < factura.Fecha)
        {
            return new ResultadoFactura(
                ErrorFactura.CaeVencimientoAnterior,
                Campo: "caeVencimiento");
        }

        if (peticion.VencimientoPago < factura.Fecha)
        {
            return new ResultadoFactura(
                ErrorFactura.VencimientoPagoAnterior,
                Campo: "vencimientoPago");
        }

        // Los cuatro campos, y nada más. El estado y la fecha de cobro no se tocan ni por accidente: no
        // están escritos acá (FR-044).
        factura.Detalle = string.IsNullOrWhiteSpace(peticion.Detalle) ? null : peticion.Detalle.Trim();
        factura.Cae = peticion.Cae.Trim();
        factura.CaeVencimiento = peticion.CaeVencimiento.Value;
        factura.VencimientoPago = peticion.VencimientoPago.Value;

        var logoVigente = await logo.ParaElDocumentoAsync(cancelacion);

        try
        {
            await facturas.CorregirAsync(
                factura,
                usuarioId,
                reloj.GetUtcNow().UtcDateTime,
                (actual, token) => EscribirDocumentoAsync(actual, logoVigente, token),
                cancelacion);
        }
        catch (DocumentoNoGeneradoException)
        {
            // FR-031b: si el documento no se puede regenerar, la corrección no queda guardada. La
            // transacción ya se deshizo, así que la factura sigue diciendo lo que dice su archivo.
            return new ResultadoFactura(
                ErrorFactura.DatosInvalidos,
                Mensaje: "No pudimos regenerar el documento de la factura. No se guardó ningún cambio; " +
                    "volvé a intentar en unos minutos.");
        }

        // **Se relee la ficha en vez de mapear la entidad rastreada**, y hace falta: la que se usó para
        // escribir vino de `ObtenerParaModificarAsync`, que no trae el historial, así que su colección de
        // cambios sólo tiene la línea que este caso de uso acaba de agregar. Mapearla dejaría la respuesta
        // con un historial de una sola entrada —la corrección— y la pantalla, que reemplaza su estado con
        // lo que llega, mostraría el historial incompleto hasta que alguien recargara.
        //
        // Lo encontró el recorrido manual del quickstart. Es el mismo camino que ya usaba la emisión.
        return ResultadoFactura.Exito((await fichas.EjecutarAsync(id, cancelacion))!);
    }

    /// <summary>
    /// Arma el documento y lo escribe como <b>archivo nuevo</b>. Nunca sobreescribe el anterior en el
    /// lugar: una falla a mitad de escritura dejaría un PDF corrupto donde antes había uno bueno. El
    /// viejo lo borra el repositorio recién después de confirmar (research §6).
    /// </summary>
    private async Task<string> EscribirDocumentoAsync(
        FacturaCliente factura,
        LogoDelDocumento? logoVigente,
        CancellationToken cancelacion)
    {
        var contenido = armador.Armar(DatosDelDocumento.Desde(factura, logoVigente));

        await using var flujo = new MemoryStream(contenido);

        return await almacen.GuardarAsync(flujo, cancelacion);
    }
}
