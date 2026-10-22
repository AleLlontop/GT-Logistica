using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Flota;
using GT.Application.Reportes.Analisis;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisFlota> FlotaAsync(FiltroFlotaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerFlotaAsync(filtro, contexto, token), ct);
    }

    private async Task<AnalisisFlota> LeerFlotaAsync(FiltroFlotaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosFlotaAsync(filtro, contexto, ct);
        var consulta = ConteosFlota(filtro);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var total = checked((int)resumen.CantidadUnidades);
        var filas = await OrdenarFlota(consulta, filtro.Orden).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina).ToArrayAsync(ct);
        var ids = filas.Select(v => v.Id).ToArray();
        // Sólo vigentes de cada tipo, en lote y sin escaneos ni historial completo de documentos.
        var documentos = await db.DocumentacionesVehiculo.AsNoTracking().Where(d => ids.Contains(d.VehiculoId) &&
            !db.DocumentacionesVehiculo.Any(otro => otro.VehiculoId == d.VehiculoId && otro.DocumentacionTipoId == d.DocumentacionTipoId &&
                (otro.FechaVencimiento > d.FechaVencimiento || otro.FechaVencimiento == d.FechaVencimiento && otro.Id > d.Id)))
            .Select(d => new { d.VehiculoId, d.FechaVencimiento, d.Tipo!.DiasAvisoVencimiento }).ToArrayAsync(ct);
        var estados = documentos.ToLookup(d => d.VehiculoId, d => CalculadorEstadoDocumento.Calcular(d.FechaVencimiento, d.DiasAvisoVencimiento, contexto.HoyArgentina));
        var detalle = new PaginaDe<FilaFlotaAnalisis>(filas.Select(v =>
        {
            var docs = estados[v.Id].ToArray();
            var estado = RepositorioVehiculos.EstadoDesde(docs.Length, docs.Count(d => d == DocumentacionEstado.Vencida), docs.Count(d => d == DocumentacionEstado.ProximaAvencer));
            return new FilaFlotaAnalisis(v.Id, v.Patente, new(v.TipoVehiculoId, v.TipoNombre), new(v.TransportistaId, v.TransportistaNombre), v.Activo,
                NombresDeEstadoFlota.DelVehiculo(v.EstadoOperativo), NombresDeEstadoFlota.DelVehiculo(CalculadorEstadoOperativo.Derivar(v.EstadoOperativo, estado)), v.CantidadViajes);
        }).ToArray(), total, filtro.Pagina, filtro.TamanioPagina);
        return new(contexto, resumen, series, distribuciones, detalle);
        }

    private async Task<(ResumenFlota Resumen, IReadOnlyList<MesFlota> Series, DistribucionesFlota Distribuciones)> LeerAgregadosFlotaAsync(FiltroFlotaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = ConteosFlota(filtro);
        var total = await consulta.CountAsync(ct);
        var viajes = await consulta.SumAsync(v => v.CantidadViajes, ct);
        // Contexto explícito de la UI: sin vehículo del período, fuera de los filtros de unidades.
        var sinVehiculo = await ViajesRealizadosEn(filtro.Periodo).LongCountAsync(v => v.VehiculoId == null, ct);
        var resumen = new ResumenFlota(total, viajes, sinVehiculo);
        var unidades = VehiculosFiltrados(filtro).Select(v => v.Id);
        var meses = await ViajesRealizadosEn(filtro.Periodo).Where(v => v.VehiculoId != null && unidades.Contains(v.VehiculoId.Value))
            .GroupBy(v => new { v.Fecha.Year, v.Fecha.Month }).Select(g => new { g.Key.Year, g.Key.Month, Cantidad = g.LongCount() }).ToArrayAsync(ct);
        var series = new List<MesFlota>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture), dato?.Cantidad ?? 0L));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var tipos = consulta.GroupBy(v => new { v.TipoVehiculoId, v.TipoNombre })
            .Select(g => new GrupoCantidadSql { Id = g.Key.TipoVehiculoId, Nombre = g.Key.TipoNombre, Cantidad = g.Sum(v => v.CantidadViajes) });
        var transportistas = consulta.GroupBy(v => new { v.TransportistaId, v.TransportistaNombre })
            .Select(g => new GrupoCantidadSql { Id = g.Key.TransportistaId, Nombre = g.Key.TransportistaNombre, Cantidad = g.Sum(v => v.CantidadViajes) });
        var distribuciones = new DistribucionesFlota(await DistribuirCantidadesAsync(tipos, ct), await DistribuirCantidadesAsync(transportistas, ct));
        return (resumen, series, distribuciones);
    }

}
