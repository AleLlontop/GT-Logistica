using System.Globalization;
using GT.Application.Reportes.Analisis;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<DashboardAnalisis> DashboardAsync(PeriodoAnalisis periodo, IReadOnlySet<SeccionReporte> autorizadas, DateTimeOffset instante, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(periodo);
        ArgumentNullException.ThrowIfNull(autorizadas);
        ValidacionAnalisis.Filtro(new FiltroCajaAnalisis(periodo));
        return foto.LeerAsync(async (_, token) =>
        {
            ContextoAnalisis Contexto(CriterioTemporalAnalisis criterio) => ContextoAnalisis.DesdeInstante(instante, periodo, criterio);
            var actual = ContextoAnalisis.DesdeInstante(instante, null, CriterioTemporalAnalisis.SituacionActual);
            var viajes = SeccionAnalisis<DashboardViajes>.SinPermiso();
            var facturacion = SeccionAnalisis<DashboardFacturacion>.SinPermiso();
            var liquidaciones = SeccionAnalisis<DashboardLiquidaciones>.SinPermiso();
            var caja = SeccionAnalisis<DashboardCaja>.SinPermiso();
            var flota = SeccionAnalisis<DashboardFlota>.SinPermiso();
            var choferes = SeccionAnalisis<DashboardVencimientos>.SinPermiso();
            var vencimientosFlota = SeccionAnalisis<DashboardVencimientos>.SinPermiso();
            var vencimientosFacturas = SeccionAnalisis<DashboardVencimientosFacturas>.SinPermiso();
            if (autorizadas.Contains(SeccionReporte.Viajes))
            {
                var datos = await LeerAgregadosViajesAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaViaje), token);
                viajes = SeccionAnalisis<DashboardViajes>.Disponible(new(datos.Resumen, datos.Series, datos.Distribuciones));
            }
            if (autorizadas.Contains(SeccionReporte.Facturacion))
            {
                var documentos = await LeerAgregadosFacturacionAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaFactura), token);
                var cobros = await LeerAgregadosCobrosAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaCobro), token);
                facturacion = SeccionAnalisis<DashboardFacturacion>.Disponible(new(documentos.Resumen, documentos.Series, cobros.Resumen, cobros.Series, documentos.Distribuciones, cobros.Distribuciones));
            }
            if (autorizadas.Contains(SeccionReporte.Liquidaciones))
            {
                var documentos = await LeerAgregadosLiquidacionesAsync(new(periodo), Contexto(CriterioTemporalAnalisis.PeriodoLiquidado), token);
                var pagos = await LeerAgregadosPagosAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaPago), token);
                liquidaciones = SeccionAnalisis<DashboardLiquidaciones>.Disponible(new(documentos.Resumen, documentos.Series, pagos.Resumen, pagos.Series, documentos.Distribuciones.Transportista));
            }
            if (autorizadas.Contains(SeccionReporte.Caja))
            {
                var datos = await LeerAgregadosCajaAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaMovimientoArgentina), token);
                caja = SeccionAnalisis<DashboardCaja>.Disponible(new(datos.Resumen, datos.Series));
            }
            if (autorizadas.Contains(SeccionReporte.Flota))
            {
                var datos = await LeerAgregadosFlotaAsync(new(periodo), Contexto(CriterioTemporalAnalisis.FechaViaje), token);
                var ranking = await DistribuirCantidadesAsync(ConteosFlota(new(periodo)).Select(v => new GrupoCantidadSql { Id = v.Id, Nombre = v.Patente, Cantidad = v.CantidadViajes }), token);
                flota = SeccionAnalisis<DashboardFlota>.Disponible(new(datos.Resumen, datos.Series, datos.Distribuciones.Tipo, ranking));
            }
            if (autorizadas.Contains(SeccionReporte.VencimientosChoferes))
                choferes = SeccionAnalisis<DashboardVencimientos>.Disponible(new(actual, new((await LeerVencimientosChoferesAsync(actual, token)).Count)));
            if (autorizadas.Contains(SeccionReporte.VencimientosFlota))
                vencimientosFlota = SeccionAnalisis<DashboardVencimientos>.Disponible(new(actual, new((await LeerVencimientosFlotaAsync(actual, token)).Count)));
            if (autorizadas.Contains(SeccionReporte.VencimientosFacturas))
            {
                var filas = await LeerVencimientosFacturasAsync(actual, token);
                vencimientosFacturas = SeccionAnalisis<DashboardVencimientosFacturas>.Disponible(new(actual, new(filas.Count, filas.Sum(f => f.Total))));
            }
            return new DashboardAnalisis(Contexto(CriterioTemporalAnalisis.PerspectivasSeparadas), new(viajes, facturacion, liquidaciones, caja, flota, choferes, vencimientosFlota, vencimientosFacturas));
        }, ct);
    }
}
