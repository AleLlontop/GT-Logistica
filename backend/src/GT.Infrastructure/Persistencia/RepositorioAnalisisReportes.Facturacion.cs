using GT.Application.Choferes;
using GT.Application.Facturacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisFacturacion> FacturacionAsync(FiltroFacturacionAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerFacturacionAsync(filtro, contexto, token), ct);
    }

    private IQueryable<FacturaCliente> FacturasFiltradas(FiltroFacturacionAnalisis filtro, DateOnly hoy)
    {
        var consulta = db.Facturas.AsNoTracking().Where(f => f.Fecha >= filtro.Periodo.Desde && f.Fecha <= filtro.Periodo.Hasta);
        if (filtro.ClienteId is { } cliente) consulta = consulta.Where(f => f.ClienteId == cliente);
        if (!string.IsNullOrWhiteSpace(filtro.Numero)) consulta = consulta.Where(f => f.NumeroComprobante.Contains(filtro.Numero.Trim()));
        return filtro.Estado switch
        {
            "pendiente" => consulta.Where(f => f.Estado == EstadoFactura.Pendiente && f.VencimientoPago >= hoy),
            "vencida" => consulta.Where(f => f.Estado == EstadoFactura.Pendiente && f.VencimientoPago < hoy),
            "pagada" => consulta.Where(f => f.Estado == EstadoFactura.Pagada),
            "anulada" => consulta.Where(f => f.Estado == EstadoFactura.Anulada),
            _ => consulta.Where(f => f.Estado != EstadoFactura.Anulada),
        };
    }

    private async Task<AnalisisFacturacion> LeerFacturacionAsync(FiltroFacturacionAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosFacturacionAsync(filtro, contexto, ct);
        var consulta = FacturasFiltradas(filtro, contexto.HoyArgentina);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var filas = await consulta.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.NumeroComprobante).ThenByDescending(f => f.Id)
            .Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(f => new { f.Id, f.NumeroComprobante, f.Fecha, f.ClienteId, f.ClienteRazonSocial, f.Estado, f.VencimientoPago, f.Neto, f.Iva, f.Total, f.FechaCobro }).ToArrayAsync(ct);
        var detalle = new PaginaDe<FilaFacturaAnalisis>(filas.Select(f => new FilaFacturaAnalisis(f.Id, f.NumeroComprobante, f.Fecha, f.ClienteId, f.ClienteRazonSocial,
            NombresDeEstadoFactura.EnJson(DerivadorEstadoFactura.Derivar(f.Estado, f.VencimientoPago, contexto.HoyArgentina)), f.Neto, f.Iva, f.Total, f.FechaCobro)).ToArray(), checked((int)resumen.Cantidad), filtro.Pagina, filtro.TamanioPagina);
        return new(contexto, resumen, series, distribuciones, detalle);
        }

    private async Task<(ResumenFacturacion Resumen, IReadOnlyList<MesFacturacion> Series, DistribucionesFacturacion Distribuciones)> LeerAgregadosFacturacionAsync(FiltroFacturacionAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = FacturasFiltradas(filtro, contexto.HoyArgentina);
        var estados = await consulta.GroupBy(f => f.Estado == EstadoFactura.Pagada ? EstadoFacturaVisible.Pagada :
                f.Estado == EstadoFactura.Anulada ? EstadoFacturaVisible.Anulada :
                f.VencimientoPago < contexto.HoyArgentina ? EstadoFacturaVisible.Vencida : EstadoFacturaVisible.Pendiente)
            .Select(g => new { Estado = g.Key, Cantidad = g.LongCount(), Importe = g.Sum(f => f.Estado == EstadoFactura.Anulada ? 0m : f.Total) }).ToArrayAsync(ct);
        var resumen = new ResumenFacturacion(estados.Sum(g => g.Cantidad), estados.Sum(g => g.Importe),
            estados.Where(g => g.Estado == EstadoFacturaVisible.Pagada).Sum(g => g.Importe),
            estados.Where(g => g.Estado == EstadoFacturaVisible.Pendiente || g.Estado == EstadoFacturaVisible.Vencida).Sum(g => g.Importe),
            estados.Where(g => g.Estado == EstadoFacturaVisible.Anulada).Sum(g => g.Cantidad));
        var meses = await consulta.GroupBy(f => new { f.Fecha.Year, f.Fecha.Month }).Select(g => new
        {
            g.Key.Year, g.Key.Month,
            Facturado = g.Sum(f => f.Estado == EstadoFactura.Anulada ? 0m : f.Total),
            Cobrado = g.Sum(f => f.Estado == EstadoFactura.Pagada ? f.Total : 0m),
            Pendiente = g.Sum(f => f.Estado == EstadoFactura.Pendiente ? f.Total : 0m),
        }).ToArrayAsync(ct);
        var series = new List<MesFacturacion>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), dato?.Facturado ?? 0m, dato?.Cobrado ?? 0m, dato?.Pendiente ?? 0m));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var clientes = consulta.GroupBy(f => new { f.ClienteId, f.Cliente!.RazonSocial })
            .Select(g => new GrupoImporteSql { Id = g.Key.ClienteId, Nombre = g.Key.RazonSocial, Cantidad = g.LongCount(), Importe = g.Sum(f => f.Estado == EstadoFactura.Anulada ? 0m : f.Total) });
        var distribuciones = new DistribucionesFacturacion(
            new(estados.OrderByDescending(g => g.Cantidad).ThenBy(g => g.Estado).Select(g => new GrupoCantidad(NombresDeEstadoFactura.EnJson(g.Estado), EstadoFacturaAnalisis.EnPantalla(g.Estado), g.Cantidad)).ToArray(), null, estados.Length),
            await DistribuirImportesAsync(clientes, ct));
        return (resumen, series, distribuciones);
    }
}
