using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisCobros> CobrosAsync(FiltroCobrosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerCobrosAsync(filtro, contexto, token), ct);
    }

    private IQueryable<FacturaCliente> CobrosFiltrados(FiltroCobrosAnalisis filtro)
    {
        var consulta = db.Facturas.AsNoTracking().Where(f => f.Estado == EstadoFactura.Pagada && f.FechaCobro >= filtro.Periodo.Desde && f.FechaCobro <= filtro.Periodo.Hasta);
        if (filtro.ClienteId is { } cliente) consulta = consulta.Where(f => f.ClienteId == cliente);
        if (!string.IsNullOrWhiteSpace(filtro.Numero)) consulta = consulta.Where(f => f.NumeroComprobante.Contains(filtro.Numero.Trim()));
        return consulta;
    }

    private async Task<AnalisisCobros> LeerCobrosAsync(FiltroCobrosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosCobrosAsync(filtro, contexto, ct);
        var consulta = CobrosFiltrados(filtro);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var total = checked((int)resumen.CantidadCobros);
        var importe = resumen.ImporteCobrado;
        var filas = await consulta.OrderByDescending(f => f.FechaCobro).ThenByDescending(f => f.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(f => new FilaCobroAnalisis(f.Id, f.NumeroComprobante, f.ClienteId, f.ClienteRazonSocial, f.Fecha, f.FechaCobro!.Value, f.Total)).ToArrayAsync(ct);
        return new(contexto, new(total, importe), series, distribuciones, new PaginaDe<FilaCobroAnalisis>(filas, total, filtro.Pagina, filtro.TamanioPagina));
        }

    private async Task<(ResumenCobros Resumen, IReadOnlyList<MesCobros> Series, DistribucionesCobros Distribuciones)> LeerAgregadosCobrosAsync(FiltroCobrosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = CobrosFiltrados(filtro);
        var total = await consulta.CountAsync(ct);
        var importe = await consulta.SumAsync(f => f.Total, ct);
        var meses = await consulta.GroupBy(f => new { f.FechaCobro!.Value.Year, f.FechaCobro.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Importe = g.Sum(f => f.Total) }).ToArrayAsync(ct);
        var series = new List<MesCobros>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), dato?.Importe ?? 0m));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var clientes = consulta.GroupBy(f => new { f.ClienteId, f.Cliente!.RazonSocial })
            .Select(g => new GrupoImporteSql { Id = g.Key.ClienteId, Nombre = g.Key.RazonSocial, Cantidad = g.LongCount(), Importe = g.Sum(f => f.Total) });
        var distribuciones = new DistribucionesCobros(await DistribuirImportesAsync(clientes, ct));
        return (new ResumenCobros(total, importe), series, distribuciones);
    }
}
