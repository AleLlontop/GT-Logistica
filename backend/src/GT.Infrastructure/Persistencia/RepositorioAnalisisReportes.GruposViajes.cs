using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Application.Viajes;
using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<GruposCantidadAnalisis> EstadosViajesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Dimension != "estado" || filtro.Filtro is not FiltroViajesAnalisis viajes)
            throw new ReglaAnalisisException("filtro_invalido", "Elegí la agrupación por estado.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var grupos = ViajesFiltrados(viajes).GroupBy(v => v.Estado);
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { Estado = g.Key, Cantidad = g.LongCount() }).ToArrayAsync(token);
            return new GruposCantidadAnalisis(contexto, new(filas.Select(g => new GrupoCantidad(NombresDeEstadoViaje.EnJson(g.Estado), NombresDeEstadoViaje.EnPantalla(g.Estado), g.Cantidad)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<GruposImporteAnalisis> ImportesViajesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Filtro is not FiltroViajesAnalisis viajes || filtro.Dimension == "estado")
            throw new ReglaAnalisisException("filtro_invalido", "Elegí cliente o transportista.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = ViajesFiltrados(viajes);
            var claves = filtro.Dimension == "cliente"
                ? consulta.Select(v => new { Id = (int?)v.ClienteId, Nombre = v.Cliente!.RazonSocial, v.Estado, v.Importe })
                : consulta.Select(v => new { Id = v.TransportistaId, Nombre = v.Transportista == null ? "Sin asignar" : v.Transportista.Nombre, v.Estado, v.Importe });
            var grupos = claves.GroupBy(v => new { v.Id, v.Nombre });
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key.Id).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { g.Key.Id, g.Key.Nombre, Cantidad = g.LongCount(), Importe = g.Sum(v => v.Estado == EstadoViaje.Anulado ? 0m : v.Importe) }).ToArrayAsync(token);
            return new GruposImporteAnalisis(contexto, new(filas.Select(g => new GrupoImporte(g.Id?.ToString(System.Globalization.CultureInfo.InvariantCulture), g.Nombre, g.Cantidad, g.Importe)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<PaginaDe<ReferenciaAnalisis>> ReferenciasViajesAsync(bool transportistas, string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct)
    {
        ValidacionAnalisis.Pagina(pagina.Pagina, pagina.TamanioPagina);
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = transportistas
                ? db.Transportistas.AsNoTracking().Select(t => new { t.Id, Nombre = t.Nombre, t.Activo })
                : db.Clientes.AsNoTracking().Select(c => new { c.Id, Nombre = c.RazonSocial, c.Activo });
            if (!string.IsNullOrWhiteSpace(busqueda)) consulta = consulta.Where(r => r.Nombre.Contains(busqueda.Trim()));
            var total = await consulta.CountAsync(token);
            var filas = await consulta.OrderBy(r => r.Nombre).ThenBy(r => r.Id).Skip((pagina.Pagina - 1) * pagina.TamanioPagina).Take(pagina.TamanioPagina)
                .Select(r => new ReferenciaAnalisis(r.Id, r.Nombre, r.Activo)).ToArrayAsync(token);
            return new PaginaDe<ReferenciaAnalisis>(filas, total, pagina.Pagina, pagina.TamanioPagina);
        }, ct);
    }
}
