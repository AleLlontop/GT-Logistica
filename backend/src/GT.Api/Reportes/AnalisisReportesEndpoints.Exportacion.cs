using GT.Api.Autorizacion;
using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearExportaciones(WebApplication app)
    {
        (string Ruta, string Fuente, Func<IQueryCollection, FiltroTemporalAnalisis> Filtro)[] temporales =
        [
            ("viajes", CodigosPermiso.ViajesConsultar, q => ParametrosAnalisis.Viajes(q)),
            ("facturacion", CodigosPermiso.FacturacionConsultar, q => FiltroFacturacion(q, false)),
            ("facturacion/cobros", CodigosPermiso.FacturacionConsultar, q => FiltroFacturacion(q, true)),
            ("liquidaciones", CodigosPermiso.LiquidacionesConsultar, q => FiltroLiquidaciones(q, false)),
            ("liquidaciones/pagos", CodigosPermiso.LiquidacionesConsultar, q => FiltroLiquidaciones(q, true)),
            ("caja", CodigosPermiso.CajaConsultar, q => FiltroCaja(q)),
            ("flota", CodigosPermiso.ReportesFlotaConsultar, q => FiltroFlota(q)),
        ];
        foreach (var opcion in temporales)
        {
            var recurso = opcion;
            app.MapGet("/api/reportes/" + recurso.Ruta + "/exportacion", (HttpContext http, EmitirAnalisisReporte caso, CancellationToken ct) =>
                Ejecutar(http, async () =>
                {
                    if (FormatosDeReporte.Leer(ParametrosAnalisis.Texto(http.Request.Query, "formato")) is not { } formato)
                        return RespuestasDeReporte.FormatoInvalido();
                    var resultado = await caso.EjecutarAsync(recurso.Filtro(http.Request.Query), formato, http.User.Identity?.Name ?? "", ct);
                    return RespuestasDeReporte.Traducir(http.Response, resultado);
                }))
                .RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, recurso.Fuente, CodigosPermiso.ReportesEmitir))
                .WithMetadata(MensajeSinPermisoAnalisis);
        }
        foreach (var opcion in new[]
        {
            (Ruta: "choferes", Fuente: CodigosPermiso.ChoferesVencimientosConsultar, Panel: SeccionReporte.VencimientosChoferes),
            (Ruta: "flota", Fuente: CodigosPermiso.FlotaVencimientosConsultar, Panel: SeccionReporte.VencimientosFlota),
            (Ruta: "facturas", Fuente: CodigosPermiso.FacturacionConsultar, Panel: SeccionReporte.VencimientosFacturas),
        })
        {
            var panel = opcion;
            app.MapGet("/api/reportes/vencimientos/" + panel.Ruta + "/exportacion", (HttpContext http, EmitirAnalisisReporte caso, CancellationToken ct) =>
                Ejecutar(http, async () =>
                {
                    if (FormatosDeReporte.Leer(ParametrosAnalisis.Texto(http.Request.Query, "formato")) is not { } formato)
                        return RespuestasDeReporte.FormatoInvalido();
                    if (http.Request.Query.ContainsKey("desde") || http.Request.Query.ContainsKey("hasta"))
                        throw new ReglaAnalisisException("filtro_invalido", "Los vencimientos muestran la situación actual y no admiten un período histórico.", "desde");
                    var resultado = await caso.VencimientosAsync(panel.Panel, formato, http.User.Identity?.Name ?? "", ct);
                    return RespuestasDeReporte.Traducir(http.Response, resultado);
                }))
                .RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, panel.Fuente, CodigosPermiso.ReportesEmitir))
                .WithMetadata(MensajeSinPermisoAnalisis);
        }
    }
}
