using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;
using GT.Application.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    private async Task<string?> NombreClienteExportacionAsync(int? id, CancellationToken ct) => id is { } valor
        ? await db.Clientes.AsNoTracking().Where(c => c.Id == valor).Select(c => c.RazonSocial).SingleOrDefaultAsync(ct)
            ?? $"ID {valor} (sin coincidencias)"
        : null;

    public Task<ReporteTabular> ExportarFacturacionAsync(FiltroFacturacionAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaFactura);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await FacturasFiltradas(filtro, contexto.HoyArgentina).CountAsync(token));
            var datos = await LeerFacturacionAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha de factura; deuda vigente a la consulta")
                .Con("Cliente actual del filtro", await NombreClienteExportacionAsync(filtro.ClienteId, token))
                .Con("Estado", filtro.Estado is { } estado ? EstadoFacturaAnalisis.EnPantalla(NombresDeEstadoFactura.LeerEstado(estado)!.Value) : "Todos excepto anuladas")
                .Con("Número", filtro.Numero).Con("Anuladas", "Importes nominales visibles; no contribuyen a totales vigentes").ToString();
            return FuenteAnalisisFacturacion.Crear(datos, filtros, autor);
        }, ct);
    }

    public Task<ReporteTabular> ExportarCobrosAsync(FiltroCobrosAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct)
    {
        ValidacionAnalisis.Filtro(filtro);
        var contexto = ContextoAnalisis.DesdeInstante(instante, filtro.Periodo, CriterioTemporalAnalisis.FechaCobro);
        return foto.LeerAsync(async (_, token) =>
        {
            VerificarTopeExportacion(await CobrosFiltrados(filtro).CountAsync(token));
            var datos = await LeerCobrosAsync(filtro with { Pagina = 1, TamanioPagina = opciones.TopeDeFilas }, contexto, token);
            var filtros = PeriodoExportacion(filtro.Periodo, "Fecha efectiva de cobro; incluye facturas emitidas antes del período")
                .Con("Cliente actual del filtro", await NombreClienteExportacionAsync(filtro.ClienteId, token)).Con("Número", filtro.Numero).ToString();
            return FuenteAnalisisCobros.Crear(datos, filtros, autor);
        }, ct);
    }
}
