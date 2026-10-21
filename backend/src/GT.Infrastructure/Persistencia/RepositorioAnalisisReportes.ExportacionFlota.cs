using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<ReporteTabular> ExportarFlotaAsync(FiltroFlotaAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaViaje);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await VehiculosFiltrados(filtro).CountAsync(token));
            var datos = await LeerFlotaAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var patente = filtro.VehiculoId is { } vehiculoId ? await db.Vehiculos.AsNoTracking().Where(v => v.Id == vehiculoId).Select(v => v.Patente).SingleOrDefaultAsync(token) ?? $"ID {vehiculoId} (sin coincidencias)" : null;
            var tipo = filtro.TipoVehiculoId is { } tipoId ? await db.TiposVehiculo.AsNoTracking().Where(t => t.Id == tipoId).Select(t => t.Nombre).SingleOrDefaultAsync(token) ?? $"ID {tipoId} (sin coincidencias)" : null;
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha del viaje; realizados = Rendido + Facturado")
                .Con("Vehículo", patente).Con("Tipo actual", tipo).Con("Transportista/propietario actual", await NombreTransportistaExportacionAsync(filtro.TransportistaId, token))
                .Con("Condición actual", filtro.Activo switch { true => "Activos", false => "Dados de baja", _ => "Todos" })
                .Con("Orden", filtro.Orden == "cantidadAsc" ? "Cantidad ascendente, patente e ID" : "Cantidad descendente, patente e ID")
                .Con("Atributos", "Tipo, propietario y disponibilidad actuales; incluye unidades con cero viajes").ToString();
            return FuenteAnalisisFlota.Crear(datos, filtros, autor);
        }, ct);
    }

    public Task<ReporteTabular> ExportarVencimientosAsync(SeccionReporte panel, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        if (panel is not (SeccionReporte.VencimientosChoferes or SeccionReporte.VencimientosFlota or SeccionReporte.VencimientosFacturas))
            throw new ReglaAnalisisException("filtro_invalido", "Elegí un panel de vencimientos válido.");
        var contexto = ContextoAnalisis.DesdeInstante(instante, null, CriterioTemporalAnalisis.SituacionActual);
        return foto.LeerAsync(async (_, token) =>
        {
            switch (panel)
            {
                case SeccionReporte.VencimientosChoferes:
                    VerificarTopeExportacion(await RepositorioDocumentacion.VigentesDeChoferesActivos(db)
                        .CountAsync(d => EF.Functions.DateDiffDay(contexto.HoyArgentina, d.FechaVencimiento) <= d.Tipo!.DiasAvisoVencimiento, token));
                    var choferes = await LeerVencimientosChoferesAsync(contexto, token);
                    VerificarTopeExportacion(choferes.Count);
                    return FuenteAnalisisVencimientos.Crear(new AnalisisVencimientosChoferes(contexto, new(choferes, choferes.Count, 1, opciones.TopeDeFilas)), autor);
                case SeccionReporte.VencimientosFlota:
                    VerificarTopeExportacion(await RepositorioDocumentacionVehiculo.VigentesDeVehiculosActivos(db)
                        .CountAsync(d => EF.Functions.DateDiffDay(contexto.HoyArgentina, d.FechaVencimiento) <= d.Tipo!.DiasAvisoVencimiento, token));
                    var flota = await LeerVencimientosFlotaAsync(contexto, token);
                    VerificarTopeExportacion(flota.Count);
                    return FuenteAnalisisVencimientos.Crear(new AnalisisVencimientosFlota(contexto, new(flota, flota.Count, 1, opciones.TopeDeFilas)), autor);
                default:
                    VerificarTopeExportacion(await RepositorioFacturas.FacturasQueVencen(db, contexto.HoyArgentina).CountAsync(token));
                    var facturas = await LeerVencimientosFacturasAsync(contexto, token);
                    VerificarTopeExportacion(facturas.Count);
                    return FuenteAnalisisVencimientos.Crear(new AnalisisVencimientosFacturas(contexto, facturas.Sum(f => f.Total), new(facturas, facturas.Count, 1, opciones.TopeDeFilas)), autor);
            }
        }, ct);
    }
}
