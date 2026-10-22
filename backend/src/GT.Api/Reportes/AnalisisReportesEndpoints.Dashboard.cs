using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;
using Microsoft.AspNetCore.Authorization;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearDashboard(WebApplication app)
    {
        app.MapGet("/api/reportes/dashboard", (HttpContext http, ConsultarDashboard consulta, IAuthorizationService autorizacion, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var periodo = ParametrosAnalisis.Periodo(http.Request.Query);
            var autorizadas = new HashSet<SeccionReporte>();
            foreach(var seccion in new[]{
                (SeccionReporte.Viajes,CodigosPermiso.ViajesConsultar),
                (SeccionReporte.Facturacion,CodigosPermiso.FacturacionConsultar),
                (SeccionReporte.Liquidaciones,CodigosPermiso.LiquidacionesConsultar),
                (SeccionReporte.Caja,CodigosPermiso.CajaConsultar),
                (SeccionReporte.Flota,CodigosPermiso.ReportesFlotaConsultar),
                (SeccionReporte.VencimientosChoferes,CodigosPermiso.ChoferesVencimientosConsultar),
                (SeccionReporte.VencimientosFlota,CodigosPermiso.FlotaVencimientosConsultar),
                (SeccionReporte.VencimientosFacturas,CodigosPermiso.FacturacionConsultar)})
            {
                if ((await autorizacion.AuthorizeAsync(http.User, PoliticasAutorizacion.Para(seccion.Item2))).Succeeded)
                    autorizadas.Add(seccion.Item1);
            }
            return SerializadorAnalisisReportes.Json(await consulta.EjecutarAsync(periodo,autorizadas,ct));
        })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ReportesConsultar)).WithMetadata(MensajeSinPermisoAnalisis);
    }
}
