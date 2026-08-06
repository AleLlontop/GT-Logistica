using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Baja de un transportista (FR-010).
///
/// Se rechaza mientras tenga choferes <b>activos</b>, e informa cuántos: dejarlo pasar dejaría
/// choferes activos colgando de un transportista inactivo, que es lo mismo que FR-008 no admite al
/// darlos de alta.
/// </summary>
public class BajaTransportistaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_LaBaja_ConChoferesActivos_YDiceCuantos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Con choferes activos");

        await app.CrearChoferCompletoAsync(19111222, transportistaId: transportista.Id);
        await app.CrearChoferCompletoAsync(19211222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("transportista_con_choferes", error!.Codigo);
        Assert.Equal(
            "No se puede dar de baja: tiene 2 chofer(es) activo(s). Reasignalos o dalos de baja primero.",
            error.Mensaje);
    }

    /// <summary>La baja procede cuando todos sus choferes están inactivos.</summary>
    [Fact]
    public async Task Procede_CuandoTodosSusChoferesEstanInactivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Con choferes inactivos");

        await app.CrearChoferCompletoAsync(19311222, activo: false, transportistaId: transportista.Id);
        await app.CrearChoferCompletoAsync(19411222, activo: false, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        // Baja lógica: sigue existiendo, inactivo.
        var leido = await cliente.GetFromJsonAsync<TransportistaLeido>(
            $"/api/transportistas/{transportista.Id}");
        Assert.False(leido!.Activo);
    }

    [Fact]
    public async Task Procede_CuandoNoTieneNingunChofer()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Sin choferes");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>Un transportista inactivo deja de ofrecerse al registrar choferes (FR-008).</summary>
    [Fact]
    public async Task Un_TransportistaDadoDeBaja_NoApareceEntreLosActivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Deja de ofrecerse");
        var cliente = await app.CrearClienteAutenticadoAsync();

        await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        var activos = await cliente.GetFromJsonAsync<List<TransportistaLeido>>(
            "/api/transportistas?soloActivos=true");

        Assert.DoesNotContain(activos!, t => t.Id == transportista.Id);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeBajaUnoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/transportistas/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record TransportistaLeido(
        int Id,
        string Nombre,
        string Cuit,
        string Tipo,
        string Telefono,
        string Email,
        bool Activo,
        int ChoferesActivos);
}
