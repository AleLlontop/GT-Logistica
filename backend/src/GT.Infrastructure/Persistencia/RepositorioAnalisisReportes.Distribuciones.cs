using System.Globalization;
using GT.Application.Reportes.Analisis;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    private sealed class GrupoImporteSql
    {
        public int? Id { get; init; }
        public string Nombre { get; init; } = "";
        public long Cantidad { get; init; }
        public decimal Importe { get; init; }
    }
    private sealed class GrupoCantidadSql
    {
        public int? Id { get; init; }
        public string Nombre { get; init; } = "";
        public long Cantidad { get; init; }
    }

    private static async Task<DistribucionImporte> DistribuirImportesAsync(IQueryable<GrupoImporteSql> consulta, CancellationToken ct)
    {
        var categorias = await consulta.CountAsync(ct);
        var ordenada = consulta.OrderByDescending(g => g.Importe).ThenBy(g => g.Id);
        var top = await ordenada.Take(10).ToArrayAsync(ct);
        GrupoImporte? otros = null;
        if (categorias > 10)
        {
            var resto = await ordenada.Skip(10).GroupBy(_ => 1).Select(g => new { Cantidad = g.Sum(v => v.Cantidad), Importe = g.Sum(v => v.Importe) }).SingleAsync(ct);
            otros = new("otros", "Otros", resto.Cantidad, resto.Importe);
        }
        return new(top.Select(g => new GrupoImporte(g.Id?.ToString(CultureInfo.InvariantCulture), g.Nombre, g.Cantidad, g.Importe)).ToArray(), otros, categorias);
    }

    private static async Task<DistribucionCantidad> DistribuirCantidadesAsync(IQueryable<GrupoCantidadSql> consulta, CancellationToken ct)
    {
        var categorias = await consulta.CountAsync(ct);
        var ordenada = consulta.OrderByDescending(g => g.Cantidad).ThenBy(g => g.Id);
        var top = await ordenada.Take(10).ToArrayAsync(ct);
        GrupoCantidad? otros = null;
        if (categorias > 10)
        {
            var resto = await ordenada.Skip(10).SumAsync(g => g.Cantidad, ct);
            otros = new("otros", "Otros", resto);
        }
        return new(top.Select(g => new GrupoCantidad(g.Id?.ToString(CultureInfo.InvariantCulture), g.Nombre, g.Cantidad)).ToArray(), otros, categorias);
    }
}
