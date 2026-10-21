using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearLiquidaciones(WebApplication app)
    {
        var grupo = app.MapGroup("/api/reportes/liquidaciones").WithMetadata(MensajeSinPermisoAnalisis)
            .RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, CodigosPermiso.LiquidacionesConsultar));
        foreach (var esCobro in new[] { false, true })
        {
            var pagos = esCobro;
            var prefijo = pagos ? "/pagos" : "";
            grupo.MapGet(prefijo, (HttpContext http, IConsultaLiquidacionesAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var filtro = FiltroLiquidaciones(http.Request.Query, pagos);
                var contexto = ContextoAnalisis.Capturar(reloj, filtro.Periodo, pagos ? CriterioTemporalAnalisis.FechaPago : CriterioTemporalAnalisis.PeriodoLiquidado);
                return pagos ? SerializadorAnalisisReportes.Json(await repo.PagosAsync((FiltroPagosAnalisis)filtro, contexto, ct))
                    : SerializadorAnalisisReportes.Json(await repo.LiquidacionesAsync((FiltroLiquidacionesAnalisis)filtro, contexto, ct));
            }));
            grupo.MapGet(prefijo + "/grupos", (HttpContext http, IConsultaLiquidacionesAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var q = http.Request.Query;
                var filtro = FiltroLiquidaciones(q, pagos);
                var agrupacion = new FiltroGrupoAnalisis(filtro, ParametrosAnalisis.Texto(q, "dimension") ?? "", filtro.Pagina, filtro.TamanioPagina);
                var contexto = ContextoAnalisis.Capturar(reloj, filtro.Periodo, pagos ? CriterioTemporalAnalisis.FechaPago : CriterioTemporalAnalisis.PeriodoLiquidado);
                return agrupacion.Dimension == "estado"
                    ? SerializadorAnalisisReportes.Json(await repo.EstadosLiquidacionesAsync(agrupacion, contexto, ct))
                    : SerializadorAnalisisReportes.Json(await repo.TransportistasLiquidacionesAsync(agrupacion, contexto, ct));
            }));
        }
        grupo.MapGet("/referencias/transportistas", (HttpContext http, IConsultaLiquidacionesAnalisis repo, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var q = http.Request.Query;
            return SerializadorAnalisisReportes.Json(await repo.ReferenciasTransportistasLiquidacionesAsync(ParametrosAnalisis.Texto(q, "busqueda"),
                new(ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20), ct));
        }));
    }
    private static FiltroTemporalAnalisis FiltroLiquidaciones(IQueryCollection q, bool pagos)
    {
        var periodo = ParametrosAnalisis.Periodo(q);
        var transportista = ParametrosAnalisis.Entero(q, "transportistaId");
        var pagina = ParametrosAnalisis.Entero(q, "pagina") ?? 1;
        var tamanio = ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20;
        return pagos ? new FiltroPagosAnalisis(periodo, transportista, ParametrosAnalisis.Texto(q, "vigencia") ?? "todos", pagina, tamanio)
            : new FiltroLiquidacionesAnalisis(periodo, transportista, ParametrosAnalisis.Texto(q, "estado"), pagina, tamanio);
    }
}
