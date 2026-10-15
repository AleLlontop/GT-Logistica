using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Revierte el cobro de una factura y la regresa al estado pendiente/vencida.
/// Exige motivo obligatorio y registra la operación en el historial para auditoría.
/// </summary>
public class RevertirCobro(
    IRepositorioFacturas facturas,
    ConsultarFichaFactura fichas,
    TimeProvider reloj)
{
    public const int LargoMaximoDelMotivo = 500;

    public async Task<ResultadoFactura> EjecutarAsync(
        int id,
        RevertirCobroRequest? peticion,
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

        if (factura.Estado is not EstadoFactura.Pagada)
        {
            return new ResultadoFactura(
                ErrorFactura.TransicionNoPermitida,
                Mensaje: $"La factura {factura.NumeroComprobante} no se encuentra cobrada.");
        }

        await facturas.RevertirCobroAsync(
            factura,
            motivo,
            usuarioId,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        return ResultadoFactura.Exito((await fichas.EjecutarAsync(id, cancelacion))!);
    }
}
