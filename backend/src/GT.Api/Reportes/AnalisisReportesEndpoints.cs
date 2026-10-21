using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static readonly MensajeDeSinPermiso MensajeSinPermisoAnalisis = new(new GT.Application.Autenticacion.ErrorResponse(
        "sin_permiso", "No tenés permiso para consultar o emitir este reporte. Pedíselo a quien administra el sistema."));

    public static void MapearAnalisisReportes(this WebApplication app)
    {
        MapearFacturacion(app);
        MapearLiquidaciones(app);
        MapearCaja(app);
        MapearFlota(app);
        MapearVencimientos(app);
        MapearDashboard(app);
        MapearExportaciones(app);
        var viajes = app.MapGroup("/api/reportes/viajes").WithMetadata(MensajeSinPermisoAnalisis)
            .RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, CodigosPermiso.ViajesConsultar));
        viajes.MapGet("", (HttpContext http, IConsultaViajesAnalisis repo, TimeProvider reloj, CancellationToken ct) =>
            Ejecutar(http, async () =>
            {
                var filtro = ParametrosAnalisis.Viajes(http.Request.Query);
                return SerializadorAnalisisReportes.Json(await repo.ViajesAsync(filtro, ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje), ct));
            }));
        viajes.MapGet("/grupos", (HttpContext http, IConsultaViajesAnalisis repo, TimeProvider reloj, CancellationToken ct) =>
            Ejecutar(http, async () =>
            {
                var q = http.Request.Query;
                var filtro = ParametrosAnalisis.Viajes(q);
                var grupo = new FiltroGrupoAnalisis(filtro, ParametrosAnalisis.Texto(q, "dimension") ?? "", filtro.Pagina, filtro.TamanioPagina);
                var contexto = ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje);
                return grupo.Dimension == "estado"
                    ? SerializadorAnalisisReportes.Json(await repo.EstadosViajesAsync(grupo, contexto, ct))
                    : SerializadorAnalisisReportes.Json(await repo.ImportesViajesAsync(grupo, contexto, ct));
            }));
        foreach (var referencia in new[] { "clientes", "transportistas" })
        {
            var transportistas = referencia == "transportistas";
            viajes.MapGet("/referencias/" + referencia, (HttpContext http, IConsultaViajesAnalisis repo, CancellationToken ct) =>
                Ejecutar(http, async () =>
                {
                    var q = http.Request.Query;
                    return SerializadorAnalisisReportes.Json(await repo.ReferenciasViajesAsync(transportistas, ParametrosAnalisis.Texto(q, "busqueda"),
                        new(ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20), ct));
                }));
        }
    }
    private static Task<IResult> Ejecutar(HttpContext http, Func<Task<IResult>> operacion)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
        return SerializadorAnalisisReportes.EjecutarAsync(operacion);
    }
}
