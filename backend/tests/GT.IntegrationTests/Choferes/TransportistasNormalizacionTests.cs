using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// El CUIT se normaliza a sólo dígitos <b>antes</b> de validar la unicidad, así que
/// <c>30-73000000-2</c> y <c>30730000002</c> son el mismo transportista (FR-025).
/// </summary>
public class TransportistasNormalizacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(string cuit) => new
    {
        nombre = "Transporte Prueba",
        cuit,
        tipo = "juridica",
        telefono = "11-5555-5555",
        email = "info@prueba.com.ar",
    };

    [Fact]
    public async Task Rechaza_CuitDuplicado_AunqueEsteEscritoDistinto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30-73000000-2"));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        // El mismo CUIT, ahora sin guiones.
        var respuesta = await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30730000002"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("cuit_duplicado", error!.Codigo);
    }

    /// <summary>Y con puntos también: lo que se guarda son los once dígitos y nada más.</summary>
    [Fact]
    public async Task Guarda_ElCuitNormalizado_AunqueVengaConPuntos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30.74000000.0"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<TransportistaLeido>();
        Assert.Equal("30740000000", creado!.Cuit);
    }

    private record TransportistaLeido(int Id, string Cuit);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
