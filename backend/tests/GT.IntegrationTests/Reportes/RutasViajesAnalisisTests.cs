using System.Net;
using System.Text.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class RutasViajesAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task TablaGruposYReferencias_ContratoExactoYErroresDeCampo()
    {
        var cliente = await app.CrearClienteAsync(activo: false);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Rendido, importe: 0.01m);
        using var http = await app.CrearClienteAutenticadoAsync();
        var filtro = $"?desde=2026-10-01&hasta=2026-10-31&clienteId={cliente.Id}";
        using var respuesta = await http.GetAsync("/api/reportes/viajes" + filtro);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True(respuesta.Headers.CacheControl!.NoStore);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("0.01", json.RootElement.GetProperty("resumen").GetProperty("importeTotal").GetString());
        Assert.Equal("fechaViaje", json.RootElement.GetProperty("contexto").GetProperty("criterioTemporal").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
        foreach (var dimension in new[] { "estado", "cliente", "transportista" })
        {
            using var grupo = await http.GetAsync("/api/reportes/viajes/grupos" + filtro + "&dimension=" + dimension);
            Assert.Equal(HttpStatusCode.OK, grupo.StatusCode);
        }
        using var referencia = await http.GetAsync("/api/reportes/viajes/referencias/clientes?busqueda=" + Uri.EscapeDataString(cliente.RazonSocial));
        Assert.Equal(HttpStatusCode.OK, referencia.StatusCode);
        using var referencias = JsonDocument.Parse(await referencia.Content.ReadAsStringAsync());
        var fila = referencias.RootElement.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == cliente.Id);
        Assert.False(fila.GetProperty("activo").GetBoolean());
        Assert.Equal(new[] { "activo", "id", "nombre" }, fila.EnumerateObject().Select(p => p.Name).Order().ToArray());
        foreach (var query in new[] { "", "?desde=incorrecto&hasta=2026-10-31", filtro + "&estado=desconocido", filtro + "&pagina=0", filtro + "&tamanioPagina=201" })
        {
            using var error = await http.GetAsync("/api/reportes/viajes" + query);
            Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
            using var detalle = JsonDocument.Parse(await error.Content.ReadAsStringAsync());
            Assert.NotEqual(JsonValueKind.Null, detalle.RootElement.GetProperty("errores").ValueKind);
        }
    }
}
