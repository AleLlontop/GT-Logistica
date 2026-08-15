using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Anula una factura y devuelve sus viajes a <c>rendido</c> (FR-046 a FR-048, FR-031b).
///
/// <b>Exige motivo escrito</b> (FR-046). La confirmación explícita la pide <b>la pantalla</b>, no el
/// backend, a diferencia de las dos de la emisión: la anulación es un cambio de estado con motivo, y su
/// irreversibilidad ya está cubierta por el motivo obligatorio — quien escribe por qué anula ya pensó lo
/// que está haciendo (research §11).
///
/// <b>O vuelven todos los viajes o no vuelve ninguno</b> (FR-048), y <b>la regeneración del documento va
/// dentro de la misma transacción</b> (FR-031b): si el documento no se puede armar con la leyenda y el
/// motivo, la anulación no queda aplicada a medias y los viajes no vuelven a <c>rendido</c>.
///
/// <b>Anular una factura <c>pagada</c> se rechaza</b> informando desde qué fecha está cobrada, <b>sin
/// ofrecer ni sugerir revertir el cobro</b>: no existe ninguna acción que lo haga (FR-043a).
/// </summary>
public class AnularFactura(
    IRepositorioFacturas facturas,
    IArmadorDocumentoFactura armador,
    GestionarLogo logo,
    IAlmacenDeArchivos almacen,
    ConsultarFichaFactura fichas,
    TimeProvider reloj)
{
    /// <summary>Largo máximo del motivo (FR-046, contracts/README §Anular una factura).</summary>
    public const int LargoMaximoDelMotivo = 500;

    public async Task<ResultadoFactura> EjecutarAsync(
        int id,
        AnulacionFacturaRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var factura = await facturas.ObtenerParaModificarAsync(id, cancelacion);

        if (factura is null)
        {
            return new ResultadoFactura(ErrorFactura.NoEncontrada);
        }

        var motivo = peticion?.Motivo?.Trim();

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > LargoMaximoDelMotivo)
        {
            return new ResultadoFactura(ErrorFactura.MotivoRequerido, Campo: "motivo");
        }

        // FR-043a: la cobrada se rechaza **nombrando la fecha desde la que lo está**, y nada más. Ofrecer
        // "revertir el cobro" sería inventar una acción que el sistema no tiene.
        if (factura.Estado is EstadoFactura.Pagada)
        {
            return new ResultadoFactura(
                ErrorFactura.FacturaCobrada,
                FacturaEnConflicto: new FacturaResumen(
                    factura.Id,
                    factura.NumeroComprobante,
                    factura.FechaCobro?.ToString("yyyy-MM-dd") ?? factura.Fecha.ToString("yyyy-MM-dd"),
                    NombresDeEstadoFactura.EnJson(EstadoFacturaVisible.Pagada)),
                Mensaje: MensajesFacturas.FacturaCobrada(
                    factura.NumeroComprobante,
                    FormatoDeDocumento.Fecha(factura.FechaCobro ?? factura.Fecha)));
        }

        if (!TransicionesDeFactura.EstaPermitida(factura.Estado, EstadoFactura.Anulada))
        {
            return new ResultadoFactura(
                ErrorFactura.TransicionNoPermitida,
                Mensaje: MensajesFacturas.TransicionNoPermitida(factura.NumeroComprobante, "anulada"));
        }

        var logoVigente = await logo.ParaElDocumentoAsync(cancelacion);

        try
        {
            await facturas.AnularAsync(
                factura,
                motivo,
                usuarioId,
                reloj.GetUtcNow().UtcDateTime,
                (actual, token) => EscribirDocumentoAsync(actual, logoVigente, token),
                cancelacion);
        }
        catch (DocumentoNoGeneradoException)
        {
            // FR-031b: **nada queda aplicado.** La transacción ya se deshizo, así que la factura sigue
            // vigente y sus viajes siguen facturados. Es lo que evita que queden a medias.
            return new ResultadoFactura(
                ErrorFactura.DatosInvalidos,
                Mensaje: "No pudimos regenerar el documento de la factura. La anulación no se aplicó; " +
                    "volvé a intentar en unos minutos.");
        }

        // Se relee la ficha: la entidad con la que se escribió no trae el historial completo, sólo la línea
        // que este caso de uso agregó (ver el comentario de `CorregirFactura`).
        //
        // **La ficha releída viene sin viajes, y es el dato correcto**: la anulación les puso `FacturaId`
        // en nulo y volvieron a `rendido`, así que la factura ya no los incluye — data-model §Anular lo
        // define así, y el detalle de qué viajes tenía queda en el documento regenerado. La pantalla dice
        // cuántos volvieron usando la cuenta que tenía **antes** de anular.
        return ResultadoFactura.Exito((await fichas.EjecutarAsync(id, cancelacion))!);
    }

    /// <summary>
    /// Arma el documento con la leyenda de anulada y el motivo, y lo escribe como <b>archivo nuevo</b>.
    ///
    /// La factura ya llega con el estado y el motivo puestos, así que la leyenda sale sola: el armador la
    /// imprime cuando ve el estado, y <b>no se estampa al servir el archivo</b> — el documento se arma en
    /// un solo lugar (FR-031d).
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
