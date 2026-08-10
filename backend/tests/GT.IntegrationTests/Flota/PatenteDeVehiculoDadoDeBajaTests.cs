using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-008f: registrar una patente que pertenece a una unidad <b>dada de baja</b> devuelve
/// <c>patente_de_vehiculo_dado_de_baja</c>, no <c>patente_duplicada</c>.
///
/// La distinción no es cosmética (research §6): un vehículo dado de baja no aparece en el listado por
/// defecto, así que quien recibe "ya está registrada" no lo encuentra en ningún lado. El mensaje que
/// corresponde le dice que lo reactive en vez de crear una unidad nueva.
/// </summary>
public class PatenteDeVehiculoDadoDeBajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Registrar_LaPatenteDeUnaUnidadDadaDeBaja_PideReactivarla()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la unidad que volvió");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la unidad que volvió");

        var dadoDeBaja = await app.CrearVehiculoAsync(
            tipo.Id,
            transportista.Id,
            patente: "BA321DC",
            activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta("BA321DC", tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("patente_de_vehiculo_dado_de_baja", error!.Codigo);
        Assert.Equal(
            "Esa patente pertenece a una unidad dada de baja. Reactivala desde su ficha en vez de " +
            "registrarla de nuevo.",
            error.Mensaje);
        Assert.Equal("patente", error.Campo);

        // Y la unidad existente sigue ahí, intacta: nada se creó ni se pisó.
        var enLaBase = await app.RecargarVehiculoAsync(dadoDeBaja.Id);
        Assert.False(enLaBase!.Activo);
    }

    /// <summary>
    /// El otro lado de la misma moneda: con la unidad activa, el mensaje es el de siempre (FR-002).
    /// </summary>
    [Fact]
    public async Task Con_LaUnidadActiva_ElMensajeEsElDeDuplicada()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la unidad activa");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la unidad activa");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, patente: "CB432ED");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            VehiculosPatenteTests.Alta("CB432ED", tipo.Id, transportista.Id));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("patente_duplicada", error!.Codigo);
    }

    /// <summary>
    /// La patente de una unidad dada de baja <b>sigue ocupada</b>: el índice único no lleva filtro
    /// por activo, y es lo que hace posible reactivarla sin conflictos (FR-002, FR-008f).
    /// </summary>
    [Fact]
    public async Task La_PatenteSigueOcupada_YReactivarLaUnidadFunciona()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la reactivación por patente");
        var transportista = await app.CrearTransportistaAsync(
            nombre: "Dueño de la reactivación por patente");

        var vehiculo = await app.CrearVehiculoAsync(
            tipo.Id,
            transportista.Id,
            patente: "DC543FE",
            activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var reactivacion = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.NoContent, reactivacion.StatusCode);

        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.True(enLaBase!.Activo);
        Assert.Equal("DC543FE", enLaBase.Patente);
    }
}
