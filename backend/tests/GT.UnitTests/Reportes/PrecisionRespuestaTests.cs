using System.Text.Json;
using GT.Api.Reportes;
using GT.Application.Reportes;
using GT.Application.Reportes.Analisis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GT.UnitTests.Reportes;

public class PrecisionRespuestaTests
{
    [Fact]
    public async Task TopeCompletoDevuelve409ConFilasYTope_AntesDeCabeceraDeArchivo()
    {
        var resultado = await SerializadorAnalisisReportes.EjecutarAsync(() => throw new TopeExportacionAnalisisException(5001, 5000));
        using var servicios = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var contexto = new DefaultHttpContext { RequestServices = servicios, Response = { Body = new MemoryStream() } };
        await resultado.ExecuteAsync(contexto);
        Assert.Equal(409, contexto.Response.StatusCode);
        Assert.False(contexto.Response.Headers.ContainsKey("Content-Disposition"));
        contexto.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.Equal("tope_de_filas_superado", json.RootElement.GetProperty("codigo").GetString());
        Assert.Equal(5001, json.RootElement.GetProperty("filas").GetInt32());
        Assert.Equal(5000, json.RootElement.GetProperty("tope").GetInt32());
    }

    [Fact]
    public async Task ExcepcionTipadaPrecedeCatchGenerico_NuevoRecorridoDevuelve409SinArchivo()
    {
        var resultado = await SerializadorAnalisisReportes.EjecutarAsync(() => throw new PrecisionMonetariaXlsxException());
        using var servicios = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        var contexto = new DefaultHttpContext { RequestServices = servicios, Response = { Body = new MemoryStream() } };
        await resultado.ExecuteAsync(contexto);
        Assert.Equal(409, contexto.Response.StatusCode);
        Assert.False(contexto.Response.Headers.ContainsKey("Content-Disposition"));
        contexto.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.Equal("precision_monetaria_xlsx", json.RootElement.GetProperty("codigo").GetString());
        Assert.Equal(MensajesDeReporte.PrecisionMonetariaXlsx, json.RootElement.GetProperty("mensaje").GetString());
        Assert.Equal(2, json.RootElement.EnumerateObject().Count());
    }
}
