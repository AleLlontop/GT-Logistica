using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Padrón de transportistas: unicidad del CUIT y validación de su dígito verificador (FR-003).
/// </summary>
public class TransportistasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(
        string nombre = "G&T Logística S.A.",
        string cuit = "30-70000000-8",
        string tipo = "juridica",
        string telefono = "11-5555-5555",
        string email = "info@gt.com.ar") => new
        {
            nombre,
            cuit,
            tipo,
            telefono,
            email,
        };

    [Fact]
    public async Task Registra_UnTransportista_ConCuitValido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30-71000000-6"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<TransportistaLeido>();
        Assert.NotNull(creado);

        // Se guarda normalizado: sólo dígitos, sin guiones ni puntos (FR-025).
        Assert.Equal("30710000006", creado.Cuit);
        Assert.True(creado.Activo);

        // Recién creado no puede tener choferes: la columna del listado arranca en cero (FR-010).
        Assert.Equal(0, creado.ChoferesActivos);
    }

    /// <summary>
    /// La columna de choferes activos es la que explica por qué un transportista no se puede dar de
    /// baja (FR-010), así que tiene que traer el número real y no un cero fijo.
    /// </summary>
    [Fact]
    public async Task Informa_CuantosChoferesActivos_TieneCadaTransportista()
    {
        var transportista = await app.CrearTransportistaAsync(cuit: "30760000007");
        var activa = await app.CrearPersonaAsync(dni: "60111222");
        var inactiva = await app.CrearPersonaAsync(dni: "61111222");

        await app.CrearChoferAsync(activa.Id, transportista.Id, cuil: "20601112222");
        await app.CrearChoferAsync(inactiva.Id, transportista.Id, cuil: "20611112220", activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var listado = await cliente.GetFromJsonAsync<List<TransportistaLeido>>(
            $"/api/transportistas?texto={Uri.EscapeDataString(transportista.Nombre)}");

        var leido = Assert.Single(listado!, t => t.Id == transportista.Id);

        // Sólo cuenta el activo: el dado de baja ya no impide nada.
        Assert.Equal(1, leido.ChoferesActivos);
    }

    /// <summary>
    /// El listado sin ningún parámetro es lo primero que pide la pantalla al entrar, así que tiene
    /// que responder aunque no venga ni <c>texto</c> ni <c>soloActivos</c> (FR-023).
    /// </summary>
    [Fact]
    public async Task Lista_SinNingunParametro()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/transportistas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<TransportistaLeido>>());
    }

    /// <summary>La búsqueda por CUIT funciona con guiones, como se ve en pantalla (FR-025).</summary>
    [Fact]
    public async Task Busca_PorCuit_AunqueSeEscribaConGuiones()
    {
        await app.CrearTransportistaAsync(nombre: "Transporte del Norte", cuit: "30770000005");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var listado = await cliente.GetFromJsonAsync<List<TransportistaLeido>>(
            "/api/transportistas?texto=30-77");

        Assert.Contains(listado!, t => t.Cuit == "30770000005");
    }

    [Fact]
    public async Task Rechaza_UnCuitYaRegistrado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30-70000000-8"));

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/transportistas",
            Alta(nombre: "Otro", cuit: "30-70000000-8"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("cuit_duplicado", error!.Codigo);
        Assert.Equal("Ese CUIT ya está registrado para otro transportista.", error.Mensaje);
        Assert.Equal("cuit", error.Campo);
    }

    /// <summary>Se valida el dígito verificador, no sólo el largo: acá sobra un 7 donde va un 6.</summary>
    [Fact]
    public async Task Rechaza_UnCuitConDigitoVerificadorInvalido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/transportistas", Alta(cuit: "30-71000000-7"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("Revisá los campos marcados en rojo.", error.Mensaje);
        Assert.Equal("cuit", error.Campo);
    }

    /// <summary>Un id inexistente responde el cuerpo de error del contrato, no un 404 pelado.</summary>
    [Fact]
    public async Task Responde_NoEncontrado_ConSuCuerpoDeError()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/transportistas/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("no_encontrado", error!.Codigo);
        Assert.Equal(
            "Ese registro ya no existe. Puede que lo hayan eliminado desde otra sesión.",
            error.Mensaje);
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

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
