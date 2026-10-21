using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisPagos> PagosAsync(FiltroPagosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerPagosAsync(filtro, contexto, token), ct);
    }

    private IQueryable<OrdenDePago> PagosFiltrados(FiltroPagosAnalisis filtro)
    {
        var consulta = db.OrdenesDePago.AsNoTracking().Where(o => o.FechaPago >= filtro.Periodo.Desde && o.FechaPago <= filtro.Periodo.Hasta);
        if (filtro.TransportistaId is { } transportista) consulta = consulta.Where(o => o.Liquidacion!.TransportistaId == transportista);
        return filtro.Vigencia switch
        {
            "vigentes" => consulta.Where(o => o.Vigente),
            "revertidos" => consulta.Where(o => !o.Vigente),
            _ => consulta,
        };
    }

    private async Task<AnalisisPagos> LeerPagosAsync(FiltroPagosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosPagosAsync(filtro, contexto, ct);
        var consulta = PagosFiltrados(filtro);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var filas = await consulta.OrderByDescending(o => o.FechaPago).ThenByDescending(o => o.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(o => new { o.Id, o.Numero, o.LiquidacionId, LqNumero = o.Liquidacion!.Numero, o.Liquidacion.PeriodoAnio, o.Liquidacion.PeriodoMes,
                o.Liquidacion.TransportistaId, o.Liquidacion.Transportista!.Nombre, o.Liquidacion.Transportista.Activo, o.FechaPago, o.Importe, o.Vigente,
                o.OrdenPagoProveedorId, OppNumero = o.OrdenPagoProveedor == null ? (int?)null : o.OrdenPagoProveedor.Numero,
                o.RevertidaEn, o.RevertidaPorId, RevertidaPorNombre = o.RevertidaPor == null ? null : o.RevertidaPor.Username }).ToArrayAsync(ct);
        var detalle = new PaginaDe<FilaPagoAnalisis>(filas.Select(o => new FilaPagoAnalisis(o.Id, NumerosVisibles.OrdenDePago(o.Numero), o.LiquidacionId, NumerosVisibles.Liquidacion(o.LqNumero),
            new DateOnly(o.PeriodoAnio, o.PeriodoMes, 1).ToString("yyyy-MM", CultureInfo.InvariantCulture), new(o.TransportistaId, o.Nombre, o.Activo),
            o.FechaPago, o.Importe, o.Vigente, o.OrdenPagoProveedorId.HasValue ? "opp" : "manual", o.OrdenPagoProveedorId,
            o.OppNumero is { } numero ? NumeroVisiblePagoProveedor.Desde(numero) : null, o.RevertidaEn,
            o.RevertidaPorId is { } autor ? new ReferenciaAnalisis(autor, o.RevertidaPorNombre!) : null)).ToArray(), checked((int)(resumen.CantidadPagosVigentes + resumen.CantidadRevertidos)), filtro.Pagina, filtro.TamanioPagina);
        return new(contexto, resumen, series, distribuciones, detalle);
        }

    private async Task<(ResumenPagos Resumen, IReadOnlyList<MesPagos> Series, DistribucionesPagos Distribuciones)> LeerAgregadosPagosAsync(FiltroPagosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = PagosFiltrados(filtro);
        var vigencias = await consulta.GroupBy(o => o.Vigente).Select(g => new { Vigente = g.Key, Cantidad = g.LongCount(), Importe = g.Sum(o => o.Importe) }).ToArrayAsync(ct);
        var resumen = new ResumenPagos(vigencias.Where(g => g.Vigente).Sum(g => g.Cantidad), vigencias.Where(g => g.Vigente).Sum(g => g.Importe),
            vigencias.Where(g => !g.Vigente).Sum(g => g.Cantidad), vigencias.Where(g => !g.Vigente).Sum(g => g.Importe));
        var meses = await consulta.GroupBy(o => new { o.FechaPago.Year, o.FechaPago.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Importe = g.Sum(o => o.Vigente ? o.Importe : 0m) }).ToArrayAsync(ct);
        var series = new List<MesPagos>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture), dato?.Importe ?? 0m));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var transportistas = consulta.GroupBy(o => new { o.Liquidacion!.TransportistaId, o.Liquidacion.Transportista!.Nombre })
            .Select(g => new GrupoImporteSql { Id = g.Key.TransportistaId, Nombre = g.Key.Nombre, Cantidad = g.Sum(o => o.Vigente ? 1L : 0L), Importe = g.Sum(o => o.Vigente ? o.Importe : 0m) });
        var distribuciones = new DistribucionesPagos(await DistribuirImportesAsync(transportistas, ct));
        return (resumen, series, distribuciones);
    }
}
