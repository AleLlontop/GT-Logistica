using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Caja;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ExportacionActividadAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    private static readonly PeriodoAnalisis Periodo = new(new(2026, 10, 1), new(2026, 10, 31));
    private static readonly DateTimeOffset Instante = new(2026, 11, 1, 0, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ViajesExporta21EnOrden_DesdePagina2_ConResumenYColumnasOriginales()
    {
        var cliente = await app.CrearClienteAsync(activo: false);
        for (var i = 0; i < 21; i++) await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido, importe: 1.01m);
        await app.CrearViajeAsync(cliente.Id, new(2026, 9, 18), EstadoViaje.Rendido, importe: 100m);
        using var scope = app.Services.CreateScope();
        var fuente = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>();
        var reporte = await fuente.ExportarViajesAsync(new(Periodo, ClienteId: cliente.Id, Pagina: 2), Instante, "gerencia");
        Assert.Equal(21, reporte.Filas.Count);
        Assert.Equal(21, reporte.Encabezado.CantidadDeFilas);
        Assert.Equal(Instante.UtcDateTime, reporte.Encabezado.GeneradoEn);
        Assert.Contains(cliente.RazonSocial, reporte.Encabezado.Filtros);
        Assert.Equal(new[] { "Número", "Fecha", "Cliente", "Origen", "Destino", "Chofer", "Vehículo", "Transportista", "Estado", "Importe" }, reporte.Columnas.Select(c => c.Nombre));
        Assert.All(reporte.Filas, f => { Assert.Equal("Rendido", f[8].ValorTexto); Assert.Equal(1.01m, f[9].ValorImporte); });
        Assert.Equal(21.21m, reporte.Totales.Single(t => !t.EsCantidad).Importe);
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var numeros = await db.Viajes.Where(v => v.ClienteId == cliente.Id && v.Fecha.Month == 10).OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Numero).Select(v => v.Numero).ToArrayAsync();
        Assert.Equal(numeros, reporte.Filas.Select(f => f[0].ValorEntero!.Value));
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task CajaExportaTodosLosMovimientos_CorteArgentino_YNetoNegativo()
    {
        var usuario = await app.UsuarioAsync(); var caja = await app.CrearCajaAsync(usuario.Id);
        for (var i = 0; i < 21; i++) await app.CrearMovimientoAsync(caja.Id, usuario.Id, tipo: TipoMovimientoCaja.Egreso, importe: 1.01m,
            fecha: new(2026, 11, 1, 2, 30, 0, DateTimeKind.Utc));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 999m, fecha: new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
        using var scope = app.Services.CreateScope();
        var reporte = await scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>().ExportarCajaAsync(new(Periodo, caja.Id, Pagina: 2), Instante, "gerencia");
        Assert.Equal(21, reporte.Filas.Count);
        Assert.Equal(new[] { "Fecha", "Tipo", "Importe", "Concepto", "Responsable", "Referencia" }, reporte.Columnas.Select(c => c.Nombre));
        Assert.All(reporte.Filas, f => { Assert.Equal(new DateOnly(2026, 10, 31), f[0].ValorFecha); Assert.Equal("Egreso", f[1].ValorTexto); });
        Assert.Equal(-21.21m, reporte.Totales.Single(t => t.Etiqueta == "Movimiento neto").Importe);
        Assert.Contains("no saldo disponible", reporte.Encabezado.Filtros);
        Assert.Null(scope.ServiceProvider.GetRequiredService<GtDbContext>().Database.CurrentTransaction);
    }
}
