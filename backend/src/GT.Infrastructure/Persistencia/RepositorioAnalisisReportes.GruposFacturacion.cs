using GT.Application.Choferes;
using GT.Application.Facturacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<GruposCantidadAnalisis> EstadosFacturacionAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Filtro is not FiltroFacturacionAnalisis documentos || filtro.Dimension != "estado")
            throw new ReglaAnalisisException("filtro_invalido", "Elegí la agrupación por estado documental.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var grupos = FacturasFiltradas(documentos, contexto.HoyArgentina).GroupBy(f => f.Estado == EstadoFactura.Pagada ? EstadoFacturaVisible.Pagada :
                f.Estado == EstadoFactura.Anulada ? EstadoFacturaVisible.Anulada : f.VencimientoPago < contexto.HoyArgentina ? EstadoFacturaVisible.Vencida : EstadoFacturaVisible.Pendiente);
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { Estado = g.Key, Cantidad = g.LongCount() }).ToArrayAsync(token);
            return new GruposCantidadAnalisis(contexto, new(filas.Select(g => new GrupoCantidad(NombresDeEstadoFactura.EnJson(g.Estado), EstadoFacturaAnalisis.EnPantalla(g.Estado), g.Cantidad)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<GruposImporteAnalisis> ClientesFacturacionAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Dimension != "cliente" || filtro.Filtro is not (FiltroFacturacionAnalisis or FiltroCobrosAnalisis))
            throw new ReglaAnalisisException("filtro_invalido", "Elegí la agrupación por cliente.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = filtro.Filtro is FiltroFacturacionAnalisis documentos
                ? FacturasFiltradas(documentos, contexto.HoyArgentina) : CobrosFiltrados((FiltroCobrosAnalisis)filtro.Filtro);
            var grupos = consulta.GroupBy(f => new { f.ClienteId, f.Cliente!.RazonSocial });
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key.ClienteId).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { g.Key.ClienteId, g.Key.RazonSocial, Cantidad = g.LongCount(), Importe = g.Sum(f => f.Estado == EstadoFactura.Anulada ? 0m : f.Total) }).ToArrayAsync(token);
            return new GruposImporteAnalisis(contexto, new(filas.Select(g => new GrupoImporte(g.ClienteId.ToString(System.Globalization.CultureInfo.InvariantCulture), g.RazonSocial, g.Cantidad, g.Importe)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<PaginaDe<ReferenciaAnalisis>> ReferenciasClientesFacturacionAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct)
        => ReferenciasViajesAsync(false, busqueda, pagina, ct);
}
