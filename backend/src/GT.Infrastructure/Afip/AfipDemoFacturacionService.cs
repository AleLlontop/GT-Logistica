using GT.Application.Facturacion;
using GT.Application.Facturacion.Afip;

namespace GT.Infrastructure.Afip;

/// <summary>
/// Proveedor de simulación (Modo Demo) para autorización de comprobantes ante AFIP / ARCA (WSFEv1).
///
/// Permite probar todo el circuito de facturación sin depender de certificados digitales reales
/// ni de conexión con los servidores de AFIP. Simula el comportamiento exacto de los métodos:
/// <list type="bullet">
///   <item><c>FECompUltimoAutorizado</c>: asigna correlatividad estricta por punto de venta.</item>
///   <item><c>FECAESolicitar</c>: genera un CAE válido de 14 dígitos y calcula su vencimiento a 10 días.</item>
/// </list>
/// </summary>
public class AfipDemoFacturacionService(
    IRepositorioFacturas repositorioFacturas) : IAfipFacturacionService
{
    public async Task<ResultadoAutorizacionAfip> AutorizarComprobanteAsync(
        SolicitudAutorizacionAfip solicitud,
        CancellationToken cancelacion = default)
    {
        var pv = int.TryParse(solicitud.PuntoDeVenta, out var parsed) ? parsed : 14;
        var ptoVtaStr = pv.ToString("D4");

        // Obtener último número autorizado y proponer el siguiente
        var ultimo = await repositorioFacturas.ObtenerUltimoNumeroCorrelativoAsync(ptoVtaStr, cancelacion);
        var proximo = ultimo + 1;
        var proximoStr = proximo.ToString("D8");
        var numeroComprobante = $"{ptoVtaStr}-{proximoStr}";

        // Generar CAE simulado de 14 dígitos (74 + fecha YYYYMMDD + 2 dígitos correlativo + 2 dígitos de control)
        var fechaStr = solicitud.Fecha.ToString("yyyyMMdd");
        var baseCae = $"74{fechaStr}{(proximo % 100):D2}89";
        var cae = baseCae.Length > 14 ? baseCae[..14] : baseCae.PadRight(14, '0');

        // Vencimiento oficial AFIP: 10 días corridos posteriores a la fecha del comprobante
        var caeVencimiento = solicitud.Fecha.AddDays(10);

        return new ResultadoAutorizacionAfip(
            numeroComprobante,
            cae,
            caeVencimiento,
            "Demo",
            "Comprobante autorizado correctamente por AFIP WSFE (Simulación Modo Demo).");
    }
}
