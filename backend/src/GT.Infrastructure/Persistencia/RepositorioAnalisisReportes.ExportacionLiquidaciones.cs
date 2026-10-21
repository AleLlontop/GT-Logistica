using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    private async Task<string?> NombreTransportistaExportacionAsync(int? id, CancellationToken ct) => id is { } valor
        ? await db.Transportistas.AsNoTracking().Where(t => t.Id == valor).Select(t => t.Nombre).SingleOrDefaultAsync(ct)
            ?? $"ID {valor} (sin coincidencias)"
        : null;

    public Task<ReporteTabular> ExportarLiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.PeriodoLiquidado);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await LiquidacionesFiltradas(filtro).CountAsync(token));
            var datos = await LeerLiquidacionesAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var filtros = PeriodoExportacion(filtro.Periodo, "Período liquidado: meses completos, sin prorratear")
                .Con("Mes inicial", contexto.PeriodoLiquidadoDesde).Con("Mes final", contexto.PeriodoLiquidadoHasta)
                .Con("Transportista actual", await NombreTransportistaExportacionAsync(filtro.TransportistaId, token))
                .Con("Estado", filtro.Estado is { } estado ? FuenteAnalisisLiquidaciones.EstadoVisible(estado) : "Todos excepto anuladas")
                .Con("Deuda", "Vigente a la consulta; anuladas sin deuda").ToString();
            return FuenteAnalisisLiquidaciones.Crear(datos, filtros, autor);
        }, ct);
    }

    public Task<ReporteTabular> ExportarPagosAsync(FiltroPagosAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaPago);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await PagosFiltrados(filtro).CountAsync(token));
            var datos = await LeerPagosAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha de pago: días exactos; cualquier período liquidado")
                .Con("Transportista actual", await NombreTransportistaExportacionAsync(filtro.TransportistaId, token))
                .Con("Vigencia", filtro.Vigencia switch { "vigentes" => "Vigentes", "revertidos" => "Revertidos", _ => "Todos" })
                .Con("Reversión", "Situación vigente al consultar, incluso si la reversión es posterior al rango")
                .Con("Unidad del flujo", "Cada OP una vez; OPP no se suma adicionalmente").ToString();
            return FuenteAnalisisPagos.Crear(datos, filtros, autor);
        }, ct);
    }
}
