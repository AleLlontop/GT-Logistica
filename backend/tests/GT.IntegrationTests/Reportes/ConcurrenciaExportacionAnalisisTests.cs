using System.Net;
using ClosedXML.Excel;
using GT.Application.Facturacion;
using GT.Application.Reportes;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Reportes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ConcurrenciaExportacionAnalisisTests
{
    [Fact]
    public async Task CobroConfirmadoEntreCountYFilas_ArchivoEsFotoAnteriorCompleta()
    {
        await using var app = new AplicacionConBarreraFoto(); await app.InitializeAsync();
        var cliente = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 15), neto: 100m);
        var administrador = await app.ConAlcanceAsync(DatosDePruebaLiquidaciones.AdministradorAsync);
        using var http = await app.CrearClienteAutenticadoAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var url = RutasExportacionAnalisisTests.Url("facturacion/cobros", "excel") + $"&clienteId={cliente.Id}";
        app.Barrera.Armar("[Facturas]"); var pendiente = http.GetAsync(url, limite.Token);
        await app.Barrera.Primera.Task.WaitAsync(limite.Token);
        try
        {
            using var escritura = app.Services.CreateScope();
            var resultado = await escritura.ServiceProvider.GetRequiredService<RegistrarCobro>().EjecutarAsync(factura.Id, new(new DateOnly(2026, 10, 18)), administrador.Id, limite.Token);
            Assert.True(resultado.Exitoso);
        }
        finally { app.Barrera.Liberada.TrySetResult(); }
        using var respuesta = await pendiente; Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using (var memoria = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync()))
        using (var libro = new XLWorkbook(memoria)) Assert.Equal("Filas incluidas: 0", libro.Worksheets.First().Cell(3, 1).GetString());
        using var siguiente = await http.GetAsync(url, limite.Token); Assert.Equal(HttpStatusCode.OK, siguiente.StatusCode);
        using (var memoria = new MemoryStream(await siguiente.Content.ReadAsByteArrayAsync()))
        using (var libro = new XLWorkbook(memoria))
        {
            Assert.Equal("Filas incluidas: 1", libro.Worksheets.First().Cell(3, 1).GetString());
            Assert.Equal(121m, libro.Worksheets.First().Cell(7, 5).GetValue<decimal>());
        }
    }

    [Fact]
    public async Task EscrituraDespuesDelModelo_NoCambiaPdfYNingunaTransaccionSigueAbierta()
    {
        await using var app = new AplicacionConBarreraRender(); await app.InitializeAsync();
        var cliente = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), vencimientoPago: new(2026, 10, 31), neto: 100m);
        var administrador = await app.ConAlcanceAsync(DatosDePruebaLiquidaciones.AdministradorAsync);
        using var http = await app.CrearClienteAutenticadoAsync();
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var url = RutasExportacionAnalisisTests.Url("facturacion", "pdf") + $"&clienteId={cliente.Id}";
        app.Barrera.Armada = true; var pendiente = http.GetAsync(url, limite.Token);
        var modelo = await app.Barrera.Modelo.Task.WaitAsync(limite.Token);
        try
        {
            using var escritura = app.Services.CreateScope();
            var resultado = await escritura.ServiceProvider.GetRequiredService<RegistrarCobro>().EjecutarAsync(factura.Id, new(new DateOnly(2026, 10, 18)), administrador.Id, limite.Token);
            Assert.True(resultado.Exitoso);
            Assert.Equal("Pendiente", Assert.Single(modelo.Filas)[3].ValorTexto);
            Assert.Equal(121m, modelo.Totales.Single(t => t.Etiqueta == "Pendiente vigente de estos documentos").Importe);
        }
        finally { app.Barrera.Liberar.TrySetResult(); }
        using var respuesta = await pendiente; Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("%PDF"u8.ToArray(), (await respuesta.Content.ReadAsByteArrayAsync())[..4]);
        Assert.Equal("Pendiente", modelo.Filas[0][3].ValorTexto);
        using var siguiente = await http.GetAsync(url, limite.Token); Assert.Equal(HttpStatusCode.OK, siguiente.StatusCode);
        Assert.Equal("Pagada", app.Barrera.Ultimo!.Filas[0][3].ValorTexto);
        Assert.Equal(0m, app.Barrera.Ultimo.Totales.Single(t => t.Etiqueta == "Pendiente vigente de estos documentos").Importe);
    }
}

public sealed class BarreraRenderAnalisis
{
    public bool Armada;
    public TaskCompletionSource<ReporteTabular> Modelo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Liberar { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ReporteTabular? Ultimo;
}
public class AplicacionConBarreraRender : AplicacionAnalisisDePrueba
{
    public BarreraRenderAnalisis Barrera { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddScoped<IArmadorReportePdf>(sp => new ArmadorObservado(sp.GetRequiredService<GtDbContext>(), Barrera)));
    }
    private sealed class ArmadorObservado(GtDbContext db, BarreraRenderAnalisis barrera) : IArmadorReportePdf
    {
        public byte[] Armar(ReporteTabular reporte)
        {
            Assert.Null(db.Database.CurrentTransaction);
            barrera.Ultimo = reporte;
            if (barrera.Armada)
            {
                barrera.Armada = false; barrera.Modelo.TrySetResult(reporte);
                barrera.Liberar.Task.WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            }
            return new ArmadorReportePdfQuestPdf().Armar(reporte);
        }
    }
}
