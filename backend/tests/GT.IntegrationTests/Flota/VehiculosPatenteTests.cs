using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-002, FR-003 y SC-002: la unicidad de la patente se decide sobre el valor <b>normalizado</b>.
///
/// <c>AB123CD</c>, <c>ab 123 cd</c> y <c>AB-123-CD</c> son la misma patente, y sólo la primera crea
/// un vehículo. Sin normalizar antes de comparar, las tres convivirían como tres unidades distintas,
/// que es justo el caso límite que la spec declara.
/// </summary>
public class VehiculosPatenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Tres_FormasDeEscribirLaMismaPatente_CreanUnSoloVehiculo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la patente única");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la patente única");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            Alta("AB123CD", tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        // Y se guarda ya normalizada: quien escribe `ab 123 cd` termina con `AB123CD` en la base.
        var creado = await primera.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal("AB123CD", creado!.Patente);

        foreach (var repetida in new[] { "ab 123 cd", "AB-123-CD", "AB.123.CD" })
        {
            var respuesta = await cliente.PostAsJsonAsync(
                "/api/flota/vehiculos",
                Alta(repetida, tipo.Id, transportista.Id));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
            Assert.Equal("patente_duplicada", error!.Codigo);
            Assert.Equal("Esa patente ya está registrada en la flota.", error.Mensaje);
            Assert.Equal("patente", error.Campo);
        }
    }

    /// <summary>FR-004: los dos formatos argentinos vigentes se aceptan.</summary>
    [Theory]
    [InlineData("ABC123")]
    [InlineData("XY456ZW")]
    public async Task Acepta_LosDosFormatosArgentinos(string patente)
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: $"Tipo de {patente}");
        var transportista = await app.CrearTransportistaAsync(nombre: $"Dueño de {patente}");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            Alta(patente, tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-004: una patente con formato inválido se rechaza con su propio código, distinto del de
    /// duplicada. El formato se valida sobre el valor ya normalizado (research §6).
    /// </summary>
    [Theory]
    [InlineData("AB12CD")]
    [InlineData("123ABC")]
    [InlineData("ABCD12")]
    public async Task Rechaza_UnFormatoInvalido(string patente)
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: $"Tipo inválido {patente}");
        var transportista = await app.CrearTransportistaAsync(nombre: $"Dueño inválido {patente}");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos",
            Alta(patente, tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("patente_invalida", error!.Codigo);
        Assert.Equal("La patente tiene que tener el formato ABC123 o AB123CD.", error.Mensaje);
        Assert.Equal("patente", error.Campo);
    }

    internal static object Alta(
        string patente,
        int tipoVehiculoId,
        int transportistaId,
        string estadoOperativo = "fueraDeServicio",
        string marca = "Scania",
        string modelo = "R450") =>
        new { patente, marca, modelo, tipoVehiculoId, transportistaId, estadoOperativo };
}
