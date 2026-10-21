using System.Text.Json;
using GT.Api.Reportes;
using GT.Application.Reportes.Analisis;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GT.UnitTests.Reportes;

public class ReadinessRespuestaAnalisisTests
{
    [Fact]
    public async Task ReadinessTraduce503SinFiltrarErrorInternoNiDatosParciales()
    {
        var respuesta=await SerializadorAnalisisReportes.EjecutarAsync(()=>throw new AnalisisNoDisponibleException(new InvalidOperationException("detalle interno")));
        using var servicios=new ServiceCollection().AddLogging().BuildServiceProvider();
        var contexto=new DefaultHttpContext { RequestServices=servicios };
        contexto.Response.Body=new MemoryStream();
        await respuesta.ExecuteAsync(contexto);
        contexto.Response.Body.Position=0;
        using var json=await JsonDocument.ParseAsync(contexto.Response.Body);
        Assert.Equal(503,contexto.Response.StatusCode);
        Assert.Equal("analisis_no_disponible",json.RootElement.GetProperty("codigo").GetString());
        Assert.DoesNotContain("detalle interno",json.RootElement.GetRawText());
        Assert.False(json.RootElement.TryGetProperty("datos",out _));
    }
}
