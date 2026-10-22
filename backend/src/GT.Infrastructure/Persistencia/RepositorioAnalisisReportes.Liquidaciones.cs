using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Liquidaciones;
using GT.Application.Reportes.Analisis;
using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisLiquidaciones> LiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerLiquidacionesAsync(filtro, contexto, token), ct);
    }

    private IQueryable<Liquidacion> LiquidacionesFiltradas(FiltroLiquidacionesAnalisis filtro)
    {
        var anioDesde = filtro.Periodo.Desde.Year;
        var mesDesde = filtro.Periodo.Desde.Month;
        var anioHasta = filtro.Periodo.Hasta.Year;
        var mesHasta = filtro.Periodo.Hasta.Month;
        var consulta = db.Liquidaciones.AsNoTracking().Where(l =>
            (l.PeriodoAnio > anioDesde || l.PeriodoAnio == anioDesde && l.PeriodoMes >= mesDesde) &&
            (l.PeriodoAnio < anioHasta || l.PeriodoAnio == anioHasta && l.PeriodoMes <= mesHasta));
        if (filtro.TransportistaId is { } transportista) consulta = consulta.Where(l => l.TransportistaId == transportista);
        var estado = NombresDeEstadoLiquidacion.LeerEstado(filtro.Estado);
        return estado is { } valor ? consulta.Where(l => l.Estado == valor) : consulta.Where(l => l.Estado != EstadoLiquidacion.Anulada);
    }

    private async Task<AnalisisLiquidaciones> LeerLiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosLiquidacionesAsync(filtro, contexto, ct);
        var consulta = LiquidacionesFiltradas(filtro);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var filas = await consulta.OrderByDescending(l => l.PeriodoAnio).ThenByDescending(l => l.PeriodoMes).ThenByDescending(l => l.Numero)
            .Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(l => new { l.Id, l.Numero, l.PeriodoAnio, l.PeriodoMes, l.TransportistaId, l.Transportista!.Nombre, l.Transportista.Activo, l.Estado, l.ImporteTotal, l.ImportePagado }).ToArrayAsync(ct);
        var detalle = new PaginaDe<FilaLiquidacionAnalisis>(filas.Select(l => new FilaLiquidacionAnalisis(l.Id, NumerosVisibles.Liquidacion(l.Numero), new DateOnly(l.PeriodoAnio, l.PeriodoMes, 1).ToString("yyyy-MM", CultureInfo.InvariantCulture),
            new(l.TransportistaId, l.Nombre, l.Activo), NombresDeEstadoLiquidacion.EnJson(l.Estado), l.ImporteTotal, l.ImportePagado, l.Estado == EstadoLiquidacion.Anulada ? null : l.ImporteTotal - l.ImportePagado)).ToArray(), checked((int)resumen.Cantidad), filtro.Pagina, filtro.TamanioPagina);
        return new(contexto, resumen, series, distribuciones, detalle);
        }

    private async Task<(ResumenLiquidaciones Resumen, IReadOnlyList<MesLiquidaciones> Series, DistribucionesLiquidaciones Distribuciones)> LeerAgregadosLiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = LiquidacionesFiltradas(filtro);
        var estados = await consulta.GroupBy(l => l.Estado).Select(g => new
        {
            Estado = g.Key, Cantidad = g.LongCount(),
            Importe = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal),
            Pagado = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImportePagado),
            Pendiente = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal - l.ImportePagado),
        }).ToArrayAsync(ct);
        var resumen = new ResumenLiquidaciones(estados.Sum(g => g.Cantidad), estados.Sum(g => g.Importe), estados.Sum(g => g.Pagado), estados.Sum(g => g.Pendiente), estados.Where(g => g.Estado == EstadoLiquidacion.Anulada).Sum(g => g.Cantidad));
        var meses = await consulta.GroupBy(l => new { l.PeriodoAnio, l.PeriodoMes }).Select(g => new
        {
            g.Key.PeriodoAnio, g.Key.PeriodoMes,
            Importe = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal),
            Pagado = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImportePagado),
            Pendiente = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal - l.ImportePagado),
        }).ToArrayAsync(ct);
        var series = new List<MesLiquidaciones>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.PeriodoAnio == mes.Year && g.PeriodoMes == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture), dato?.Importe ?? 0m, dato?.Pagado ?? 0m, dato?.Pendiente ?? 0m));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var transportistas = consulta.GroupBy(l => new { l.TransportistaId, l.Transportista!.Nombre })
            .Select(g => new GrupoImporteSql { Id = g.Key.TransportistaId, Nombre = g.Key.Nombre, Cantidad = g.LongCount(), Importe = g.Sum(l => l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal) });
        var distribuciones = new DistribucionesLiquidaciones(
            new(estados.OrderByDescending(g => g.Cantidad).ThenBy(g => g.Estado).Select(g => new GrupoCantidad(NombresDeEstadoLiquidacion.EnJson(g.Estado), g.Estado.ToString(), g.Cantidad)).ToArray(), null, estados.Length),
            await DistribuirImportesAsync(transportistas, ct));
        return (resumen, series, distribuciones);
    }
}
