using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearFlota(WebApplication app)
    {
        var grupo = app.MapGroup("/api/reportes/flota").WithMetadata(MensajeSinPermisoAnalisis).RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesFlotaConsultar));
        grupo.MapGet("", (HttpContext http, IConsultaFlotaAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var filtro = FiltroFlota(http.Request.Query);
            return SerializadorAnalisisReportes.Json(await repo.FlotaAsync(filtro, ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje), ct));
        }));
        grupo.MapGet("/grupos", (HttpContext http, IConsultaFlotaAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var q = http.Request.Query;
            var filtro = FiltroFlota(q);
            return SerializadorAnalisisReportes.Json(await repo.GruposFlotaAsync(new(filtro, ParametrosAnalisis.Texto(q, "dimension") ?? "", filtro.Pagina, filtro.TamanioPagina),
                ContextoAnalisis.Capturar(reloj, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje), ct));
        }));
        foreach(var opcion in new[]{("vehiculos", ReferenciaDeAnalisis.Vehiculos), ("tipos", ReferenciaDeAnalisis.TiposVehiculo), ("transportistas", ReferenciaDeAnalisis.TransportistasFlota)})
        {
            var recurso = opcion.Item2;
            grupo.MapGet("/referencias/" + opcion.Item1, (HttpContext http, IConsultaFlotaAnalisis repo, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var q = http.Request.Query;
                return SerializadorAnalisisReportes.Json(await repo.ReferenciasFlotaAsync(recurso, ParametrosAnalisis.Texto(q, "busqueda"),
                    new(ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20), ct));
            }));
        }
    }
    private static FiltroFlotaAnalisis FiltroFlota(IQueryCollection q) => new(ParametrosAnalisis.Periodo(q), ParametrosAnalisis.Entero(q, "vehiculoId"),
        ParametrosAnalisis.Entero(q, "tipoVehiculoId"), ParametrosAnalisis.Entero(q, "transportistaId"), ParametrosAnalisis.Booleano(q, "activo"),
        ParametrosAnalisis.Texto(q, "orden") ?? "cantidadDesc", ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20);
}
