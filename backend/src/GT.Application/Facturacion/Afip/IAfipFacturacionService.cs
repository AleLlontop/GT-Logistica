namespace GT.Application.Facturacion.Afip;

/// <summary>
/// Parámetros para solicitar la autorización de un comprobante ante AFIP / ARCA (WSFEv1).
/// </summary>
public record SolicitudAutorizacionAfip(
    string PuntoDeVenta,
    string TipoComprobante,
    DateOnly Fecha,
    decimal Total);

/// <summary>
/// Resultado devuelto por el servicio de AFIP / ARCA al autorizar el comprobante.
/// </summary>
public record ResultadoAutorizacionAfip(
    string NumeroComprobante,
    string Cae,
    DateOnly CaeVencimiento,
    string Modo,
    string Mensaje);

/// <summary>
/// Contrato para el servicio de emisión y autorización ante AFIP / ARCA (WSFEv1).
/// Permite operar en modo simulación (Demo) o en producción / homologación.
/// </summary>
public interface IAfipFacturacionService
{
    Task<ResultadoAutorizacionAfip> AutorizarComprobanteAsync(
        SolicitudAutorizacionAfip solicitud,
        CancellationToken cancelacion = default);
}
