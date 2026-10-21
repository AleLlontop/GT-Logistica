using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearCaja(WebApplication app)
    {
        var grupo = app.MapGroup("/api/reportes/caja").WithMetadata(MensajeSinPermisoAnalisis).RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, CodigosPermiso.CajaConsultar));
        grupo.MapGet("", (HttpContext http, IConsultaCajaAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var filtro = FiltroCaja(http.Request.Query);
            return SerializadorAnalisisReportes.Json(await repo.CajaAsync(filtro, ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaMovimientoArgentina), ct));
        }));
        grupo.MapGet("/grupos", (HttpContext http, IConsultaCajaAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var q = http.Request.Query;
            var filtro = FiltroCaja(q);
            return SerializadorAnalisisReportes.Json(await repo.TiposCajaAsync(new(filtro, ParametrosAnalisis.Texto(q, "dimension") ?? "", filtro.Pagina, filtro.TamanioPagina),
                ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaMovimientoArgentina), ct));
        }));
        grupo.MapGet("/referencias/cajas", (HttpContext http, IConsultaCajaAnalisis repo, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var q = http.Request.Query;
            return SerializadorAnalisisReportes.Json(await repo.ReferenciasCajasAsync(ParametrosAnalisis.Texto(q, "busqueda"),
                new(ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20), ct));
        }));
    }
    private static FiltroCajaAnalisis FiltroCaja(IQueryCollection q) => new(ParametrosAnalisis.Periodo(q), ParametrosAnalisis.Entero(q, "cajaId"),
        ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20);
}
