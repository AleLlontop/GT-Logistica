using System.Data;
using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class TopeExportacionAnalisisTests
{
    [Fact]
    public async Task CincoMilIncluidas_CincoMilUnaRechazadas_AntesDeTraerFilas_SnapshotCerrado()
    {
        await using var app = new AplicacionObservadaAnalisis(); await app.InitializeAsync();
        var cliente = await app.CrearClienteAsync();
        await app.EnLaBaseAsync(async db =>
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            db.Viajes.AddRange(Enumerable.Range(0, 5001).Select(i => new Viaje
            {
                ClienteId = cliente.Id, Fecha = new(2026, 10, 18), Estado = EstadoViaje.Pendiente,
                Origen = "Rosario", Destino = i < 5000 ? "Conjunto admitido" : "Extra", Importe = 0.01m,
            }));
            await db.SaveChangesAsync();
        });
        using var scope = app.Services.CreateScope();
        var fuente = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var instante = TimeProvider.System.GetUtcNow();
        var modelo = await fuente.ExportarViajesAsync(new(periodo, cliente.Id, Busqueda: "admitido", Pagina: 2, TamanioPagina: 1), instante, "gerencia");
        Assert.Equal(5000, modelo.Filas.Count);
        Assert.Equal(50m, modelo.Totales.Single(t => !t.EsCantidad).Importe);
        Assert.Null(db.Database.CurrentTransaction);
        app.Sonda.Iniciar();
        var error = await Assert.ThrowsAsync<TopeExportacionAnalisisException>(() => fuente.ExportarViajesAsync(new(periodo, cliente.Id), instante, "gerencia"));
        Assert.Equal(5001, error.Filas); Assert.Equal(5000, error.Tope);
        var sql = Assert.Single(app.Sonda.Comandos, c => c.Sql.Contains("[Viajes]"));
        Assert.Contains("COUNT(*)", sql.Sql); Assert.Equal(IsolationLevel.Snapshot, sql.Aislamiento);
        Assert.Null(db.Database.CurrentTransaction);
        app.Sonda.Activa = false;
        using var http = await app.CrearClienteAutenticadoAsync();
        using var rechazoHttp = await http.GetAsync(RutasExportacionAnalisisTests.Url("viajes", "excel") + $"&clienteId={cliente.Id}");
        Assert.Equal(System.Net.HttpStatusCode.Conflict, rechazoHttp.StatusCode);
        Assert.Null(rechazoHttp.Content.Headers.ContentDisposition);
        using (var json = System.Text.Json.JsonDocument.Parse(await rechazoHttp.Content.ReadAsStringAsync()))
        {
            Assert.Equal(5001, json.RootElement.GetProperty("filas").GetInt32());
            Assert.Equal(5000, json.RootElement.GetProperty("tope").GetInt32());
        }
        using var completoHttp = await http.GetAsync(RutasExportacionAnalisisTests.Url("viajes", "excel") + $"&clienteId={cliente.Id}&busqueda=admitido&pagina=2&tamanioPagina=1");
        Assert.Equal(System.Net.HttpStatusCode.OK, completoHttp.StatusCode);
        using (var memoria = new MemoryStream(await completoHttp.Content.ReadAsByteArrayAsync()))
        using (var libro = new ClosedXML.Excel.XLWorkbook(memoria))
        {
            var hoja = libro.Worksheets.First();
            var cantidadInformada = new string(hoja.Cell(3, 1).GetString().Where(char.IsDigit).ToArray());
            Assert.Equal("5000", cantidadInformada);
            Assert.All(Enumerable.Range(7, 5000), fila => Assert.Equal(ClosedXML.Excel.XLDataType.Number, hoja.Cell(fila, 1).DataType));
            Assert.True(hoja.Cell(5007, 1).IsEmpty());
        }
        var vacio = await fuente.ExportarViajesAsync(new(periodo, cliente.Id, Busqueda: "inexistente"), instante, "gerencia");
        Assert.Empty(vacio.Filas); Assert.Equal(0, vacio.Encabezado.CantidadDeFilas);
    }

    [Fact]
    public async Task ConteosSqlDeTresPanelesCoincidenConReglasOriginales_YRechazanAntesDeFilas()
    {
        await using var app = new AplicacionObservadaAnalisis(); await app.InitializeAsync();
        var tipoChofer = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: 7);
        var tipoVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync(diasAvisoVencimiento: 7);
        var dueno = await app.TransportistaExternoAsync(); var tipo = await app.CrearTipoVehiculoAsync();
        var cliente = await app.CrearClienteAsync(); var instante = TimeProvider.System.GetUtcNow();
        var hoy = GT.Domain.Choferes.FechaHoyArgentina.Desde(instante);
        var offsets = new[] { -10, 0, 7, 8, 30 };
        for (var i = 0; i < offsets.Length; i++)
        {
            var chofer = await app.CrearChoferCompletoAsync(90159960 + i);
            await app.CrearDocumentoAsync(chofer.Id, tipoChofer.Id, offsets[i]);
            var vehiculo = await app.CrearVehiculoAsync(tipo.Id, dueno.Id);
            await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoVehiculo.Id, offsets[i]);
            await app.CrearFacturaAsync(cliente.Id, fecha: hoy.AddDays(-15), vencimientoPago: hoy.AddDays(offsets[i]));
        }
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var foto = scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        var permitida = new RepositorioAnalisisReportes(db, foto);
        var acotada = new RepositorioAnalisisReportes(db, foto, new OpcionesDeReporte(2));
        foreach (var panel in new[] { SeccionReporte.VencimientosChoferes, SeccionReporte.VencimientosFlota, SeccionReporte.VencimientosFacturas })
        {
            var modelo = await permitida.ExportarVencimientosAsync(panel, instante, "gerencia", default);
            Assert.Equal(3, modelo.Filas.Count);
            app.Sonda.Iniciar();
            var error = await Assert.ThrowsAsync<TopeExportacionAnalisisException>(() => acotada.ExportarVencimientosAsync(panel, instante, "gerencia", default));
            Assert.Equal(3, error.Filas); Assert.Equal(2, error.Tope);
            var dominio = app.Sonda.Comandos.Where(c => c.Sql.Contains("[Documentaciones]") || c.Sql.Contains("[DocumentacionesVehiculo]") || c.Sql.Contains("[Facturas]")).ToArray();
            Assert.Contains("COUNT(*)", Assert.Single(dominio).Sql);
            Assert.All(dominio, c => Assert.Equal(IsolationLevel.Snapshot, c.Aislamiento));
            Assert.Null(db.Database.CurrentTransaction); app.Sonda.Activa = false;
        }
    }
}
