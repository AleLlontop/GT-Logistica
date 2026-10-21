using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Caja;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class PdfAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task TodasLasPerspectivasGeneranPdfRealPorDi_DespuesDeCerrarFoto()
    {
        await EscenarioAnulacion.CrearAsync(app, "fletero");
        var cliente = await app.CrearClienteAsync(razonSocial: "Cliente actual sintético");
        await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 18), neto: 1234567.89m);
        var hoy = GT.Domain.Choferes.FechaHoyArgentina.Desde(TimeProvider.System.GetUtcNow());
        await app.CrearFacturaAsync(cliente.Id, fecha: hoy.AddDays(-10), vencimientoPago: hoy.AddDays(-2), neto: 100.01m);
        var dueno = await app.TransportistaExternoAsync(); var tipo = await app.CrearTipoVehiculoAsync();
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, dueno.Id); var tipoDocVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync();
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocVehiculo.Id, -2);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido, importe: 100.01m, vehiculoId: vehiculo.Id, transportistaId: dueno.Id);
        var chofer = await app.CrearChoferCompletoAsync(90159951); var tipoDoc = await app.CrearTipoDocumentacionAsync();
        await app.CrearDocumentoAsync(chofer.Id, tipoDoc.Id, -3);
        var usuario = await app.UsuarioAsync(); var caja = await app.CrearCajaAsync(usuario.Id);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, tipo: TipoMovimientoCaja.Egreso, importe: 123.45m, fecha: new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
        using var scope = app.Services.CreateScope();
        var caso = scope.ServiceProvider.GetRequiredService<EmitirAnalisisReporte>();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        FiltroTemporalAnalisis[] filtros = [new FiltroViajesAnalisis(periodo), new FiltroFacturacionAnalisis(periodo), new FiltroCobrosAnalisis(periodo),
            new FiltroLiquidacionesAnalisis(periodo), new FiltroPagosAnalisis(periodo), new FiltroCajaAnalisis(periodo), new FiltroFlotaAnalisis(periodo)];
        var carpeta = "/tmp/gt015-exportacion/pdf"; Directory.CreateDirectory(carpeta);
        foreach (var filtro in filtros)
        {
            var resultado = await caso.EjecutarAsync(filtro, FormatoDeReporte.Pdf, "gerencia-sintetica");
            Assert.Null(db.Database.CurrentTransaction);
            Assert.NotNull(resultado.Contenido);
            Assert.Equal("%PDF"u8.ToArray(), resultado.Contenido[..4]);
            await File.WriteAllBytesAsync(Path.Combine(carpeta, resultado.NombreDeArchivo!), resultado.Contenido);
        }
        foreach (var panel in new[] { SeccionReporte.VencimientosChoferes, SeccionReporte.VencimientosFlota, SeccionReporte.VencimientosFacturas })
        {
            var resultado = await caso.VencimientosAsync(panel, FormatoDeReporte.Pdf, "gerencia-sintetica");
            Assert.Null(db.Database.CurrentTransaction);
            Assert.NotNull(resultado.Contenido);
            Assert.Equal("%PDF"u8.ToArray(), resultado.Contenido[..4]);
            await File.WriteAllBytesAsync(Path.Combine(carpeta, resultado.NombreDeArchivo!), resultado.Contenido);
        }
    }
}
