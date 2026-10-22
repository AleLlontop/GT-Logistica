using GT.Api.Autorizacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

public static partial class AnalisisReportesEndpoints
{
    private static void MapearFacturacion(WebApplication app)
    {
        var grupo = app.MapGroup("/api/reportes/facturacion").WithMetadata(MensajeSinPermisoAnalisis)
            .RequireAuthorization(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, CodigosPermiso.FacturacionConsultar));
        foreach (var esCobro in new[] { false, true })
        {
            var cobros = esCobro;
            var prefijo = cobros ? "/cobros" : "";
            grupo.MapGet(prefijo, (HttpContext http, IConsultaFacturacionAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var filtro = FiltroFacturacion(http.Request.Query, cobros);
                var contexto = ContextoAnalisis.Capturar(reloj, filtro.Periodo, cobros ? CriterioTemporalAnalisis.FechaCobro : CriterioTemporalAnalisis.FechaFactura);
                return cobros ? SerializadorAnalisisReportes.Json(await repo.CobrosAsync((FiltroCobrosAnalisis)filtro, contexto, ct))
                    : SerializadorAnalisisReportes.Json(await repo.FacturacionAsync((FiltroFacturacionAnalisis)filtro, contexto, ct));
            }));
            grupo.MapGet(prefijo + "/grupos", (HttpContext http, IConsultaFacturacionAnalisis repo, TimeProvider reloj, CancellationToken ct) => Ejecutar(http, async () =>
            {
                var q = http.Request.Query;
                var filtro = FiltroFacturacion(q, cobros);
                var agrupacion = new FiltroGrupoAnalisis(filtro, ParametrosAnalisis.Texto(q, "dimension") ?? "", filtro.Pagina, filtro.TamanioPagina);
                var contexto = ContextoAnalisis.Capturar(reloj, filtro.Periodo, cobros ? CriterioTemporalAnalisis.FechaCobro : CriterioTemporalAnalisis.FechaFactura);
                return agrupacion.Dimension == "estado"
                    ? SerializadorAnalisisReportes.Json(await repo.EstadosFacturacionAsync(agrupacion, contexto, ct))
                    : SerializadorAnalisisReportes.Json(await repo.ClientesFacturacionAsync(agrupacion, contexto, ct));
            }));
        }
        grupo.MapGet("/referencias/clientes", (HttpContext http, IConsultaFacturacionAnalisis repo, CancellationToken ct) => Ejecutar(http, async () =>
        {
            var q = http.Request.Query;
            return SerializadorAnalisisReportes.Json(await repo.ReferenciasClientesFacturacionAsync(ParametrosAnalisis.Texto(q, "busqueda"),
                new(ParametrosAnalisis.Entero(q, "pagina") ?? 1, ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20), ct));
        }));
    }
    private static FiltroTemporalAnalisis FiltroFacturacion(IQueryCollection q, bool cobros)
    {
        var periodo = ParametrosAnalisis.Periodo(q);
        var cliente = ParametrosAnalisis.Entero(q, "clienteId");
        var numero = ParametrosAnalisis.Texto(q, "numero");
        var pagina = ParametrosAnalisis.Entero(q, "pagina") ?? 1;
        var tamanio = ParametrosAnalisis.Entero(q, "tamanioPagina") ?? 20;
        return cobros ? new FiltroCobrosAnalisis(periodo, cliente, numero, pagina, tamanio)
            : new FiltroFacturacionAnalisis(periodo, cliente, ParametrosAnalisis.Texto(q, "estado"), numero, pagina, tamanio);
    }
}
