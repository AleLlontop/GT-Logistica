using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-010 y SC-008: la baja de un tipo de vehículo se rechaza si tiene vehículos asociados, y el
/// mensaje <b>dice cuántos son</b>.
///
/// Cuentan <b>todos</b>, activos e inactivos, y eso es deliberado: un vehículo dado de baja sigue
/// mostrando su tipo (FR-011), así que el tipo tiene que seguir existiendo. Es la asimetría con la
/// baja de transportista, que sólo mira dependientes activos (research §8).
/// </summary>
public class TiposVehiculoBajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_LaBaja_ConVehiculosActivos_YDiceCuantos()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo con flota activa");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la flota activa");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/flota/tipos-vehiculo/{tipo.Id}");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_vehiculo_en_uso", error!.Codigo);
        Assert.Equal("No se puede dar de baja: 2 vehículo(s) usan este tipo.", error.Mensaje);
        Assert.Equal(2, error.CantidadVehiculos);
    }

    /// <summary>
    /// El caso que distingue esta regla de la del transportista: los vehículos <b>dados de baja</b>
    /// también impiden la baja del tipo, porque siguen mostrándolo en su ficha (FR-010, FR-011).
    /// </summary>
    [Fact]
    public async Task Rechaza_LaBaja_TambienConVehiculosInactivos()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo con flota inactiva");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la flota inactiva");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/flota/tipos-vehiculo/{tipo.Id}");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_vehiculo_en_uso", error!.Codigo);
        Assert.Equal(1, error.CantidadVehiculos);
    }

    [Fact]
    public async Task Procede_CuandoNingunVehiculoLoUsa()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo sin flota");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/flota/tipos-vehiculo/{tipo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>El listado muestra la cantidad: es lo que explica por qué algunos no se pueden bajar.</summary>
    [Fact]
    public async Task El_Listado_InformaCuantosVehiculosUsanCadaTipo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo que informa su uso");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño que informa");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var catalogo = await cliente.GetFromJsonAsync<List<TipoVehiculoLeido>>(
            "/api/flota/tipos-vehiculo");

        var fila = Assert.Single(catalogo!, t => t.Id == tipo.Id);
        Assert.Equal(1, fila.CantidadVehiculos);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeBajaUnoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/flota/tipos-vehiculo/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// El alta de un tipo dado de baja (FR-009): vuelve a estar activo y vuelve a ofrecerse al
    /// registrar unidades, que es lo que verifica <c>?soloActivos=true</c>.
    /// </summary>
    [Fact]
    public async Task El_Alta_DevuelveElTipoAlCatalogoQueSeOfrece()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo que vuelve", activo: false);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var ofrecidosAntes = await cliente.GetFromJsonAsync<List<TipoVehiculoLeido>>(
            "/api/flota/tipos-vehiculo?soloActivos=true");

        Assert.DoesNotContain(ofrecidosAntes!, t => t.Id == tipo.Id);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/tipos-vehiculo/{tipo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var reactivado = await respuesta.Content.ReadFromJsonAsync<TipoVehiculoLeido>();
        Assert.True(reactivado!.Activo);

        var ofrecidosDespues = await cliente.GetFromJsonAsync<List<TipoVehiculoLeido>>(
            "/api/flota/tipos-vehiculo?soloActivos=true");

        Assert.Contains(ofrecidosDespues!, t => t.Id == tipo.Id);
    }

    /// <summary>Reactivar uno que ya está activo lo deja como está: la acción es idempotente.</summary>
    [Fact]
    public async Task El_Alta_DeUnoYaActivo_NoFalla()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo ya activo");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/tipos-vehiculo/{tipo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var leido = await respuesta.Content.ReadFromJsonAsync<TipoVehiculoLeido>();
        Assert.True(leido!.Activo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeAltaUnoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo/999999/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
