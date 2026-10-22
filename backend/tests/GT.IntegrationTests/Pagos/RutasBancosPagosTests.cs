using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GT.Application.Pagos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Pagos;

public class RutasBancosPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ListadoDetalleYErroresConAutorizacionIndependiente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await cliente.GetAsync("/api/pagos/bancos");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(20, json.RootElement.GetProperty("tamanioPagina").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("pagina").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/pagos/bancos/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/pagos/bancos?pagina=0")).StatusCode);
        using var anonimo = app.CrearCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/pagos/bancos")).StatusCode);
    }
    [Fact]
    public async Task CrearDevuelveRecursoCompleto_YErroresDeNombreSonDeCampo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var nombre = "Banco " + Guid.NewGuid().ToString("N");
        var respuesta = await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "  " + nombre + "  " });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(nombre, json.RootElement.GetProperty("nombre").GetString());
        Assert.True(json.RootElement.GetProperty("activo").GetBoolean());
        Assert.NotNull(respuesta.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(respuesta.Headers.Location)).StatusCode);
        var invalido = await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        using var error = JsonDocument.Parse(await invalido.Content.ReadAsStringAsync());
        Assert.Equal("nombre", error.RootElement.GetProperty("campo").GetString());
    }

    [Fact]
    public async Task EditarSoloNombreYVersion_ConflictoNoEsConfirmacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var alta = await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Editar " + Guid.NewGuid().ToString("N") });
        var banco = (await alta.Content.ReadFromJsonAsync<BancoDto>())!;
        var editar = await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}", new { nombre = "Editado " + banco.Id, version = banco.Version });
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        var actualizado = (await editar.Content.ReadFromJsonAsync<BancoDto>())!;
        Assert.True(actualizado.Activo);
        Assert.Equal(banco.Version + 1, actualizado.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}", new { nombre = "Otra edición", version = banco.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}", new { nombre = "Otro", version = actualizado.Version, activo = false })).StatusCode);
    }

    [Fact]
    public async Task ActivarDesactivarSonIdempotentes_EditarNoReactiva()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var alta = await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Estados " + Guid.NewGuid().ToString("N") });
        var banco = (await alta.Content.ReadFromJsonAsync<BancoDto>())!;
        for (var i = 0; i < 2; i++)
        {
            var baja = await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}/desactivacion", new { });
            Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
            Assert.False((await baja.Content.ReadFromJsonAsync<BancoDto>())!.Activo);
        }
        var editar = await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}", new { nombre = "Renombrado " + banco.Id, version = banco.Version });
        Assert.False((await editar.Content.ReadFromJsonAsync<BancoDto>())!.Activo);
        var reactivar = await cliente.PutAsJsonAsync($"/api/pagos/bancos/{banco.Id}/activacion", new { });
        var reactivado = (await reactivar.Content.ReadFromJsonAsync<BancoDto>())!;
        Assert.Equal(banco.Id, reactivado.Id);
        Assert.True(reactivado.Activo);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PutAsJsonAsync("/api/pagos/bancos/2147483647/activacion", new { })).StatusCode);
    }

}
