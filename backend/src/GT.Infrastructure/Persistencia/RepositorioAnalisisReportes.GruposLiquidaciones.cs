using System.Globalization;
using GT.Application.Choferes;
using GT.Application.Liquidaciones;
using GT.Application.Reportes.Analisis;
using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<GruposCantidadAnalisis> EstadosLiquidacionesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Filtro is not FiltroLiquidacionesAnalisis documentos || filtro.Dimension != "estado")
            throw new ReglaAnalisisException("filtro_invalido", "Elegí la agrupación por estado documental.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var grupos = LiquidacionesFiltradas(documentos).GroupBy(l => l.Estado);
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { Estado = g.Key, Cantidad = g.LongCount() }).ToArrayAsync(token);
            return new GruposCantidadAnalisis(contexto, new(filas.Select(g => new GrupoCantidad(NombresDeEstadoLiquidacion.EnJson(g.Estado), g.Estado.ToString(), g.Cantidad)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<GruposImporteAnalisis> TransportistasLiquidacionesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Dimension != "transportista" || filtro.Filtro is not (FiltroLiquidacionesAnalisis or FiltroPagosAnalisis))
            throw new ReglaAnalisisException("filtro_invalido", "Elegí la agrupación por transportista.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = filtro.Filtro is FiltroLiquidacionesAnalisis documentos
                ? LiquidacionesFiltradas(documentos).Select(l => new { Id = l.TransportistaId, l.Transportista!.Nombre, Cuenta = 1L, Importe = l.Estado == EstadoLiquidacion.Anulada ? 0m : l.ImporteTotal })
                : PagosFiltrados((FiltroPagosAnalisis)filtro.Filtro).Select(o => new { Id = o.Liquidacion!.TransportistaId, o.Liquidacion.Transportista!.Nombre, Cuenta = o.Vigente ? 1L : 0L, Importe = o.Vigente ? o.Importe : 0m });
            var grupos = consulta.GroupBy(v => new { v.Id, v.Nombre });
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key.Id).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { g.Key.Id, g.Key.Nombre, Cantidad = g.Sum(v => v.Cuenta), Importe = g.Sum(v => v.Importe) }).ToArrayAsync(token);
            return new GruposImporteAnalisis(contexto, new(filas.Select(g => new GrupoImporte(g.Id.ToString(CultureInfo.InvariantCulture), g.Nombre, g.Cantidad, g.Importe)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<PaginaDe<ReferenciaAnalisis>> ReferenciasTransportistasLiquidacionesAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct)
        => ReferenciasViajesAsync(true, busqueda, pagina, ct);
}
