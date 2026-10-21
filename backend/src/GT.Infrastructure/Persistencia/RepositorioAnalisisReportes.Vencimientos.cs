using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisVencimientosChoferes> VencimientosChoferesAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidarSituacionActual(filtro, contexto);
        return foto.LeerAsync(async (_, token) => new AnalisisVencimientosChoferes(contexto,
            Paginar(await LeerVencimientosChoferesAsync(contexto, token), filtro)), ct);
    }

    public Task<AnalisisVencimientosFlota> VencimientosFlotaAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidarSituacionActual(filtro, contexto);
        return foto.LeerAsync(async (_, token) => new AnalisisVencimientosFlota(contexto,
            Paginar(await LeerVencimientosFlotaAsync(contexto, token), filtro)), ct);
    }

    public Task<AnalisisVencimientosFacturas> VencimientosFacturasAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidarSituacionActual(filtro, contexto);
        return foto.LeerAsync(async (_, token) =>
        {
            var filas = await LeerVencimientosFacturasAsync(contexto, token);
            return new AnalisisVencimientosFacturas(contexto, filas.Sum(f => f.Total), Paginar(filas, filtro));
        }, ct);
    }

    private Task<List<GT.Application.Choferes.Documentacion.AlertaVencimiento>> LeerVencimientosChoferesAsync(ContextoAnalisis contexto, CancellationToken ct) =>
        new GT.Application.Choferes.Documentacion.ConsultarVencimientos(new RepositorioDocumentacion(db)).EjecutarAsync(ct, contexto.HoyArgentina);

    private Task<List<GT.Application.Flota.AlertaVencimientoFlota>> LeerVencimientosFlotaAsync(ContextoAnalisis contexto, CancellationToken ct) =>
        new GT.Application.Flota.Documentacion.ConsultarVencimientosFlota(new RepositorioDocumentacionVehiculo(db)).EjecutarAsync(ct, contexto.HoyArgentina);

    // Misma consulta de006; pasar hoy capturado evita que el caso de uso legado lea otro instante.
    private Task<IReadOnlyList<GT.Application.Facturacion.FilaDeVencimiento>> LeerVencimientosFacturasAsync(ContextoAnalisis contexto, CancellationToken ct) =>
        RepositorioFacturas.ConsultarVencimientosDeAsync(db, contexto.HoyArgentina, ct);

    private static PaginaDe<T> Paginar<T>(IReadOnlyList<T> filas, FiltroPaginaAnalisis filtro) =>
        new(filas.Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina).ToArray(), filas.Count, filtro.Pagina, filtro.TamanioPagina);

    private static void ValidarSituacionActual(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto)
    {
        ValidacionAnalisis.Pagina(filtro.Pagina, filtro.TamanioPagina);
        if (contexto.CriterioTemporal != CriterioTemporalAnalisis.SituacionActual || contexto.Desde is not null || contexto.Hasta is not null)
            throw new ReglaAnalisisException("filtro_invalido", "Los vencimientos muestran la situación actual, sin período histórico.", "desde");
    }
}
