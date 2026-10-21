using System.Text.Json;
using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ExportacionFlotaVencimientosTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task FlotaCompletaConservaRanking_CerosYBajas_ConSinVehiculoSeparado()
    {
        var dueno = await app.TransportistaExternoAsync(); var tipo = await app.CrearTipoVehiculoAsync();
        var unidades = new List<GT.Domain.Flota.Vehiculo>();
        for (var i = 0; i < 21; i++) unidades.Add(await app.CrearVehiculoAsync(tipo.Id, dueno.Id, activo: i != 0));
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido, vehiculoId: unidades[0].Id);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Facturado, vehiculoId: unidades[1].Id);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido);
        using var scope = app.Services.CreateScope();
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var instante = TimeProvider.System.GetUtcNow();
        var fuente = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>();
        var reporte = await fuente.ExportarFlotaAsync(new(periodo, TransportistaId: dueno.Id, Pagina: 2), instante, "gerencia");
        Assert.Equal(21, reporte.Filas.Count);
        var analisis = await scope.ServiceProvider.GetRequiredService<IConsultaFlotaAnalisis>().FlotaAsync(new(periodo, TransportistaId: dueno.Id, TamanioPagina: 200), ContextoAnalisis.DesdeInstante(instante, periodo, CriterioTemporalAnalisis.FechaViaje));
        Assert.Equal(analisis.Detalle.Items.Select(v => v.Patente), reporte.Filas.Select(f => f[0].ValorTexto));
        Assert.Equal(19, reporte.Filas.Count(f => f[6].ValorEntero == 0));
        Assert.Single(reporte.Filas, f => f[3].ValorTexto == "Dado de baja" && f[6].ValorEntero == 1);
        Assert.Equal(1m, reporte.Totales.Single(t => t.Etiqueta.Contains("sin vehículo")).Importe);
        Assert.All(reporte.Totales, t => Assert.True(t.EsCantidad));
    }

    [Fact]
    public async Task TresAdaptadoresConservanExactamenteFilasColumnasYUrgenciaDe012()
    {
        var tipoDocumento = await app.CrearTipoDocumentacionAsync(); var chofer = await app.CrearChoferCompletoAsync(90159901);
        await app.CrearDocumentoAsync(chofer.Id, tipoDocumento.Id, -5);
        var dueno = await app.TransportistaExternoAsync(); var tipo = await app.CrearTipoVehiculoAsync();
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, dueno.Id); var docVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync();
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, docVehiculo.Id, -3);
        var cliente = await app.CrearClienteAsync(); var instante = TimeProvider.System.GetUtcNow();
        var hoy = GT.Domain.Choferes.FechaHoyArgentina.Desde(instante);
        await app.CrearFacturaAsync(cliente.Id, fecha: hoy.AddDays(-10), vencimientoPago: hoy.AddDays(-2));
        using var scope = app.Services.CreateScope();
        var fuente = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>(); var capturador = new Capturador();
        var armado = new ArmadoDeReporte(capturador, capturador, OpcionesDeReporte.PorDefecto, TimeProvider.System);
        foreach (var panel in new[] { SeccionReporte.VencimientosChoferes, SeccionReporte.VencimientosFlota, SeccionReporte.VencimientosFacturas })
        {
            if (panel == SeccionReporte.VencimientosChoferes)
                await new FuenteReporteVencimientosChoferes(scope.ServiceProvider.GetRequiredService<GT.Application.Choferes.Documentacion.ConsultarVencimientos>(), armado).EjecutarAsync(FormatoDeReporte.Pdf, "gerencia");
            else if (panel == SeccionReporte.VencimientosFlota)
                await new FuenteReporteVencimientosFlota(scope.ServiceProvider.GetRequiredService<GT.Application.Flota.Documentacion.ConsultarVencimientosFlota>(), armado).EjecutarAsync(FormatoDeReporte.Pdf, "gerencia");
            else await new FuenteReporteVencimientosFacturas(scope.ServiceProvider.GetRequiredService<GT.Application.Facturacion.ConsultarVencimientos>(), armado).EjecutarAsync(FormatoDeReporte.Pdf, "gerencia");
            var nuevo = await fuente.ExportarVencimientosAsync(panel, instante, "gerencia");
            var previo = Assert.IsType<ReporteTabular>(capturador.Reporte);
            Assert.NotEmpty(nuevo.Filas);
            Assert.Equal(JsonSerializer.Serialize(previo.Columnas), JsonSerializer.Serialize(nuevo.Columnas));
            Assert.Equal(JsonSerializer.Serialize(previo.Filas), JsonSerializer.Serialize(nuevo.Filas));
            Assert.Equal(JsonSerializer.Serialize(previo.Totales), JsonSerializer.Serialize(nuevo.Totales));
            Assert.Contains("Situación actual", nuevo.Encabezado.Filtros);
            Assert.Equal(instante.UtcDateTime, nuevo.Encabezado.GeneradoEn);
        }
    }

    private sealed class Capturador : IArmadorReportePdf, IArmadorReporteExcel
    {
        public ReporteTabular? Reporte { get; private set; }
        public byte[] Armar(ReporteTabular reporte) { Reporte = reporte; return [1]; }
    }
}
