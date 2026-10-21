using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<GruposImporteAnalisis> TiposCajaAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        ValidacionAnalisis.Grupo(filtro);
        if (filtro.Filtro is not FiltroCajaAnalisis caja)
            throw new ReglaAnalisisException("filtro_invalido", "La agrupación no corresponde a Caja.", "dimension");
        return foto.LeerAsync(async (_, token) =>
        {
            var grupos = MovimientosFiltrados(caja).GroupBy(m => m.Tipo);
            var total = await grupos.CountAsync(token);
            var filas = await grupos.OrderBy(g => g.Key).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
                .Select(g => new { Tipo = g.Key, Cantidad = g.LongCount(), Importe = g.Sum(m => m.Importe) }).ToArrayAsync(token);
            return new GruposImporteAnalisis(contexto, new(filas.Select(g => new GrupoImporte(NombresDeEstadoCaja.EnJson(g.Tipo), NombresDeEstadoCaja.EnPantalla(g.Tipo), g.Cantidad, g.Importe)).ToArray(), total, filtro.Pagina, filtro.TamanioPagina));
        }, ct);
    }

    public Task<PaginaDe<ReferenciaAnalisis>> ReferenciasCajasAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct)
    {
        ValidacionAnalisis.Pagina(pagina.Pagina, pagina.TamanioPagina);
        return foto.LeerAsync(async (_, token) =>
        {
            var consulta = db.Cajas.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(busqueda)) consulta = consulta.Where(c => c.UsuarioResponsable!.Username.Contains(busqueda.Trim()));
            var total = await consulta.CountAsync(token);
            var filas = await consulta.OrderBy(c => c.UsuarioResponsable!.Username).ThenBy(c => c.FechaApertura).ThenBy(c => c.Id)
                .Skip((pagina.Pagina - 1) * pagina.TamanioPagina).Take(pagina.TamanioPagina)
                .Select(c => new { c.Id, Nombre = c.UsuarioResponsable!.Username, c.FechaApertura }).ToArrayAsync(token);
            return new PaginaDe<ReferenciaAnalisis>(filas.Select(c => new ReferenciaAnalisis(c.Id, NombreCajaAnalisis.Desde(c.Nombre, c.FechaApertura))).ToArray(), total, pagina.Pagina, pagina.TamanioPagina);
        }, ct);
    }
}
