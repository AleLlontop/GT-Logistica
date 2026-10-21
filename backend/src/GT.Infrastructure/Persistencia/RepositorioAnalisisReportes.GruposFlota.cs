using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<GruposCantidadAnalisis> GruposFlotaAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Filtro is not FiltroFlotaAnalisis flota) throw new ReglaAnalisisException("filtro_invalido", "La agrupación no corresponde a Flota.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = ConteosFlota(flota);
            var claves = filtro.Dimension == "tipo"
                ? consulta.Select(v => new { Id = v.TipoVehiculoId, Nombre = v.TipoNombre, v.CantidadViajes })
                : consulta.Select(v => new { Id = v.TransportistaId, Nombre = v.TransportistaNombre, v.CantidadViajes });
            var grupos = claves.GroupBy(v => new { v.Id, v.Nombre });
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key.Id).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { g.Key.Id, g.Key.Nombre, Cantidad = g.Sum(v => v.CantidadViajes) }).ToArrayAsync(token);
            return new GruposCantidadAnalisis(contexto, new(filas.Select(g => new GrupoCantidad(g.Id.ToString(CultureInfo.InvariantCulture), g.Nombre, g.Cantidad)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<PaginaDe<ReferenciaAnalisis>> ReferenciasFlotaAsync(ReferenciaDeAnalisis recurso, string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct)
    {
        ValidacionAnalisis.Pagina(pagina.Pagina, pagina.TamanioPagina);
        if (recurso == ReferenciaDeAnalisis.TransportistasFlota) return ReferenciasViajesAsync(true, busqueda, pagina, ct);
        if (recurso is not (ReferenciaDeAnalisis.TiposVehiculo or ReferenciaDeAnalisis.Vehiculos)) throw new ReglaAnalisisException("filtro_invalido", "La referencia no corresponde a Flota.");
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = recurso == ReferenciaDeAnalisis.TiposVehiculo
                ? db.TiposVehiculo.AsNoTracking().Select(t => new { t.Id, t.Nombre, t.Activo })
                : db.Vehiculos.AsNoTracking().Select(v => new { v.Id, Nombre = v.Patente, v.Activo });
            if (!string.IsNullOrWhiteSpace(busqueda)) consulta = consulta.Where(v => v.Nombre.Contains(busqueda.Trim()));
            var total = await consulta.CountAsync(token);
            var filas = await consulta.OrderBy(v => v.Nombre).ThenBy(v => v.Id).Skip((pagina.Pagina - 1) * pagina.TamanioPagina).Take(pagina.TamanioPagina)
                .Select(v => new ReferenciaAnalisis(v.Id, v.Nombre, v.Activo)).ToArrayAsync(token);
            return new PaginaDe<ReferenciaAnalisis>(filas, total, pagina.Pagina, pagina.TamanioPagina);
        }, ct);
    }
}
