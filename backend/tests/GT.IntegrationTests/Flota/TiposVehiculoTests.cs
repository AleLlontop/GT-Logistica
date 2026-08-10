using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Catálogo de tipos de vehículo (FR-009, US1 esc. 3 y 4).
///
/// El catálogo arranca vacío y la baja es <b>lógica</b>: el registro no se borra nunca (FR-028).
/// </summary>
public class TiposVehiculoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(string nombre) => new { nombre };

    [Fact]
    public async Task Crea_UnTipo_QueArrancaActivoYSinVehiculos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            Alta("Semirremolque de prueba"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<TipoVehiculoLeido>();
        Assert.Equal("Semirremolque de prueba", creado!.Nombre);
        Assert.True(creado.Activo);
        Assert.Equal(0, creado.CantidadVehiculos);
    }

    /// <summary>FR-009: el nombre es único en el catálogo.</summary>
    [Fact]
    public async Task Rechaza_NombreDuplicado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PostAsJsonAsync("/api/flota/tipos-vehiculo", Alta("Chasis duplicado"));

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            Alta("Chasis duplicado"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("nombre_duplicado", error!.Codigo);
        Assert.Equal("Ya existe un tipo con ese nombre.", error.Mensaje);
        Assert.Equal("nombre", error.Campo);
    }

    /// <summary>Conservar el propio nombre al modificar no es un duplicado (FR-009).</summary>
    [Fact]
    public async Task Modifica_ConservandoSuPropioNombre()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var creacion = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            Alta("Utilitario que se renombra"));
        var tipo = await creacion.Content.ReadFromJsonAsync<TipoVehiculoLeido>();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/tipos-vehiculo/{tipo!.Id}",
            Alta("Utilitario que se renombra"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// US1 esc. 4: la baja es lógica. El tipo desaparece de los activos —deja de ofrecerse al
    /// registrar unidades— pero <b>el registro sigue existiendo</b> (FR-009, FR-028).
    /// </summary>
    [Fact]
    public async Task La_Baja_EsLogica_YElRegistroNoSeBorra()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var creacion = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            Alta("Tipo que se da de baja"));
        var tipo = await creacion.Content.ReadFromJsonAsync<TipoVehiculoLeido>();

        var baja = await cliente.DeleteAsync($"/api/flota/tipos-vehiculo/{tipo!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var todos = await cliente.GetFromJsonAsync<List<TipoVehiculoLeido>>(
            "/api/flota/tipos-vehiculo");
        var activos = await cliente.GetFromJsonAsync<List<TipoVehiculoLeido>>(
            "/api/flota/tipos-vehiculo?soloActivos=true");

        var enElCatalogo = Assert.Single(todos!, t => t.Id == tipo.Id);
        Assert.False(enElCatalogo.Activo);
        Assert.DoesNotContain(activos!, t => t.Id == tipo.Id);
    }

    /// <summary>US1 esc. 1: el catálogo vacío es una respuesta legítima, no un error.</summary>
    [Fact]
    public async Task Responde_UnaListaVacia_SinRomper()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/flota/tipos-vehiculo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<TipoVehiculoLeido>>());
    }

    [Fact]
    public async Task Rechaza_UnNombreVacio()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            new { nombre = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("nombre", error.Campo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlModificarUnoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/flota/tipos-vehiculo/999999",
            Alta("Cualquiera"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
