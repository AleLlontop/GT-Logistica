using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    private sealed class ConteoFlotaSql
    {
        public int Id { get; init; }
        public string Patente { get; init; } = "";
        public int TipoVehiculoId { get; init; }
        public string TipoNombre { get; init; } = "";
        public int TransportistaId { get; init; }
        public string TransportistaNombre { get; init; } = "";
        public bool Activo { get; init; }
        public VehiculoEstado EstadoOperativo { get; init; }
        public long CantidadViajes { get; init; }
    }

    private IQueryable<Viaje> ViajesRealizadosEn(PeriodoAnalisis periodo) => db.Viajes.AsNoTracking().Where(v =>
        v.Fecha >= periodo.Desde && v.Fecha <= periodo.Hasta && (v.Estado == EstadoViaje.Rendido || v.Estado == EstadoViaje.Facturado));

    private IQueryable<Vehiculo> VehiculosFiltrados(FiltroFlotaAnalisis filtro)
    {
        var consulta = db.Vehiculos.AsNoTracking();
        if (filtro.VehiculoId is { } vehiculo) consulta = consulta.Where(v => v.Id == vehiculo);
        if (filtro.TipoVehiculoId is { } tipo) consulta = consulta.Where(v => v.TipoVehiculoId == tipo);
        if (filtro.TransportistaId is { } transportista) consulta = consulta.Where(v => v.TransportistaId == transportista);
        if (filtro.Activo is { } activo) consulta = consulta.Where(v => v.Activo == activo);
        return consulta;
    }

    private IQueryable<ConteoFlotaSql> ConteosFlota(FiltroFlotaAnalisis filtro)
    {
        var conteos = ViajesRealizadosEn(filtro.Periodo).Where(v => v.VehiculoId != null)
            .GroupBy(v => v.VehiculoId).Select(g => new { Id = g.Key, Cantidad = g.LongCount() });
        return from vehiculo in VehiculosFiltrados(filtro)
            join cantidad in conteos on (int?)vehiculo.Id equals cantidad.Id into cantidades
            from cantidad in cantidades.DefaultIfEmpty()
            select new ConteoFlotaSql
            {
                Id = vehiculo.Id, Patente = vehiculo.Patente, TipoVehiculoId = vehiculo.TipoVehiculoId, TipoNombre = vehiculo.Tipo!.Nombre,
                TransportistaId = vehiculo.TransportistaId, TransportistaNombre = vehiculo.Transportista!.Nombre,
                Activo = vehiculo.Activo, EstadoOperativo = vehiculo.EstadoOperativo, CantidadViajes = (long?)cantidad.Cantidad ?? 0L,
            };
    }

    private static IOrderedQueryable<ConteoFlotaSql> OrdenarFlota(IQueryable<ConteoFlotaSql> consulta, string orden) =>
        (orden == "cantidadAsc" ? consulta.OrderBy(v => v.CantidadViajes) : consulta.OrderByDescending(v => v.CantidadViajes)).ThenBy(v => v.Patente).ThenBy(v => v.Id);

    public Task<PaginaDe<GrupoCantidad>> ConteosFlotaAsync(FiltroFlotaAnalisis filtro, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = ConteosFlota(filtro);
            var total = await consulta.CountAsync(token);
            var filas = await OrdenarFlota(consulta, filtro.Orden).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(v => new { v.Id, v.Patente, v.CantidadViajes }).ToArrayAsync(token);
            return new PaginaDe<GrupoCantidad>(filas.Select(v => new GrupoCantidad(v.Id.ToString(CultureInfo.InvariantCulture), v.Patente, v.CantidadViajes)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina);
        }, ct);
    }
}
