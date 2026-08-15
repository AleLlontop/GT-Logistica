using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Registra el cobro de una factura (FR-042).
///
/// <b>Es un recurso propio y nunca un campo del <c>PUT</c> de corrección</b> (FR-044): así corregir un
/// CAE no puede marcar una factura como cobrada en silencio.
///
/// <b><c>pagada</c> es terminal.</b> No existe ninguna acción que revierta un cobro, y no está oculta: no
/// existe (FR-043). La pantalla lo dice antes de confirmar, con esas palabras.
///
/// <b>No regenera el documento</b>, y es el único cambio de estado que no lo hace: la fecha de cobro no
/// sale impresa, porque es información interna de cobranzas y el comprobante que se le mandó al cliente
/// no cambia porque después haya pagado. Las operaciones que regeneran son exactamente tres: emitir,
/// corregir y anular (FR-031b, spec §Clarifications CHK027).
/// </summary>
public class RegistrarCobro(
    IRepositorioFacturas facturas,
    ConsultarFichaFactura fichas,
    TimeProvider reloj)
{
    public async Task<ResultadoFactura> EjecutarAsync(
        int id,
        CobroRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var factura = await facturas.ObtenerParaModificarAsync(id, cancelacion);

        if (factura is null)
        {
            return new ResultadoFactura(ErrorFactura.NoEncontrada);
        }

        if (peticion?.FechaCobro is not { } fechaCobro)
        {
            return new ResultadoFactura(ErrorFactura.DatosInvalidos, Campo: "fechaCobro");
        }

        // Sólo desde `pendiente` o `vencida`, que son el mismo estado guardado. Una ya pagada o anulada
        // se rechaza aunque se invoque el endpoint a mano (FR-043).
        if (!TransicionesDeFactura.EstaPermitida(factura.Estado, EstadoFactura.Pagada))
        {
            return new ResultadoFactura(
                ErrorFactura.TransicionNoPermitida,
                Mensaje: MensajesFacturas.TransicionNoPermitida(
                    factura.NumeroComprobante,
                    factura.Estado is EstadoFactura.Pagada ? "pagada" : "anulada"));
        }

        // No anterior a la fecha de facturación: cobrar antes de emitir no es un hecho posible (FR-042).
        if (fechaCobro < factura.Fecha)
        {
            return new ResultadoFactura(ErrorFactura.FechaCobroAnterior, Campo: "fechaCobro");
        }

        await facturas.RegistrarCobroAsync(
            factura,
            fechaCobro,
            usuarioId,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        // Se relee la ficha: la entidad con la que se escribió no trae el historial completo, sólo la
        // línea que este caso de uso agregó (ver el comentario de `CorregirFactura`).
        return ResultadoFactura.Exito((await fichas.EjecutarAsync(id, cancelacion))!);
    }
}
