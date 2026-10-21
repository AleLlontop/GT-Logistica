using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;
using GT.Application.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    private void VerificarTopeExportacion(int cantidad)
    {
        if (opciones.Supera(cantidad)) throw new TopeExportacionAnalisisException(cantidad, opciones.TopeDeFilas);
    }

    private static LineaDeFiltros PeriodoExportacion(PeriodoAnalisis periodo, string criterio) =>
        new LineaDeFiltros().Con("Criterio temporal", criterio).Con("Desde", periodo.Desde).Con("Hasta", periodo.Hasta);

    public Task<ReporteTabular> ExportarViajesAsync(FiltroViajesAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await ViajesFiltrados(filtro).CountAsync(token));
            var datos = await LeerViajesAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var cliente = filtro.ClienteId is { } clienteId
                ? await db.Clientes.AsNoTracking().Where(c => c.Id == clienteId).Select(c => c.RazonSocial).SingleOrDefaultAsync(token) : null;
            var transportista = filtro.TransportistaId is { } transportistaId
                ? await db.Transportistas.AsNoTracking().Where(t => t.Id == transportistaId).Select(t => t.Nombre).SingleOrDefaultAsync(token) : null;
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha del viaje")
                .Con("Cliente", cliente ?? (filtro.ClienteId is { } id ? $"ID {id} (sin coincidencias)" : null))
                .Con("Transportista", transportista ?? (filtro.TransportistaId is { } tid ? $"ID {tid} (sin coincidencias)" : null))
                .Con("Estado", filtro.Estado is { } estado ? NombresDeEstadoViaje.EnPantalla(NombresDeEstadoViaje.Leer(estado)!.Value) : "Todos excepto anulados")
                .Con("Búsqueda", filtro.Busqueda).ToString();
            return FuenteAnalisisViajes.Crear(datos, filtros, autor);
        }, ct);
    }

    public Task<ReporteTabular> ExportarCajaAsync(FiltroCajaAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaMovimientoArgentina);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await MovimientosFiltrados(filtro).CountAsync(token));
            var datos = await LeerCajaAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var caja = filtro.CajaId is { } cajaId
                ? await db.Cajas.AsNoTracking().Where(c => c.Id == cajaId)
                    .Select(c => new { Nombre = c.UsuarioResponsable!.Username, c.FechaApertura }).SingleOrDefaultAsync(token) : null;
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha del movimiento en Argentina")
                .Con("Caja", caja is not null ? NombreCajaAnalisis.Desde(caja.Nombre, caja.FechaApertura)
                    : filtro.CajaId is { } id ? $"ID {id} (sin coincidencias)" : "Todas")
                .Con("Neto", "Movimiento neto del período, no saldo disponible").ToString();
            return FuenteAnalisisCaja.Crear(datos, filtros, autor);
        }, ct);
    }
}
