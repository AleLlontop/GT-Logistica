using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearVencimientos(WebApplication app)
    {
        foreach(var opcion in new[]{("choferes", CodigosPermiso.ChoferesVencimientosConsultar), ("flota", CodigosPermiso.FlotaVencimientosConsultar), ("facturas", CodigosPermiso.FacturacionConsultar)})
        {
            var panel = opcion.Item1;
            app.MapGet("/api/reportes/vencimientos/" + panel, (HttpContext http, IConsultaVencimientosAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var q = http.Request.Query;
                if(q.ContainsKey("desde") || q.ContainsKey("hasta"))
                    throw new ReglaAnalisisException("filtro_invalido", "Los vencimientos muestran la situación actual y no admiten un período histórico.", "desde");
                var filtro = new FiltroPaginaAnalisis(ParametrosAnalisis.Entero(q,"pagina") ?? 1, ParametrosAnalisis.Entero(q,"tamanioPagina") ?? 20);
                var contexto = ContextoAnalisis.Capturar(reloj,null,CriterioTemporalAnalisis.SituacionActual);
                return panel switch
                {
                    "choferes" => SerializadorAnalisisReportes.Json(await repo.VencimientosChoferesAsync(filtro,contexto,ct)),
                    "flota" => SerializadorAnalisisReportes.Json(await repo.VencimientosFlotaAsync(filtro,contexto,ct)),
                    _ => SerializadorAnalisisReportes.Json(await repo.VencimientosFacturasAsync(filtro,contexto,ct)),
                };
            })).RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar,opcion.Item2)).WithMetadata(MensajeSinPermisoAnalisis);
        }
    }
}
