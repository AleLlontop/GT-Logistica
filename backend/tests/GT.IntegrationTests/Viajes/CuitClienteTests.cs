using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El CUIT del padrón de clientes (FR-003, FR-004, FR-007; US1 esc. 3, 4, 5 y 10).
/// </summary>
public class CuitClienteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_CuitEsUnico()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var cuit = DatosDePruebaViajes.CuitUnico();

        var primera = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("Uno", cuit));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var segunda = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("Otro", cuit));
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);

        var error = await segunda.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("cuit_duplicado", error!.Codigo);
        Assert.Equal("cuit", error.Campo);
    }

    /// <summary>
    /// FR-003: el índice del CUIT <b>no</b> filtra por <c>Activo</c>, así que el de un cliente dado de
    /// baja sigue ocupado. Y el rechazo tiene código propio: sin eso, quien lo intenta recibe "ya
    /// pertenece a otro cliente" y sale a buscarlo a un listado donde no aparece (FR-007, US1 esc. 10).
    /// </summary>
    [Fact]
    public async Task El_CuitDeUnClienteDadoDeBaja_SigueOcupadoYLoDiceDistinto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var cuit = DatosDePruebaViajes.CuitUnico();

        var alta = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("El que se fue", cuit));
        var creado = await alta.Content.ReadFromJsonAsync<ClienteLeido>();

        var baja = await cliente.DeleteAsync($"/api/clientes/{creado!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var reintento = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("El mismo", cuit));

        Assert.Equal(HttpStatusCode.BadRequest, reintento.StatusCode);

        var error = await reintento.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("cuit_de_cliente_dado_de_baja", error!.Codigo);
        Assert.NotEqual("cuit_duplicado", error.Codigo);
    }

    /// <summary>US1 esc. 5: conservar el CUIT propio al editar no genera conflicto (FR-003).</summary>
    [Fact]
    public async Task La_Modificacion_ExcluyeAlPropioCliente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var cuit = DatosDePruebaViajes.CuitUnico();

        var alta = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("Razón vieja", cuit));
        var creado = await alta.Content.ReadFromJsonAsync<ClienteLeido>();

        var edicion = await cliente.PutAsJsonAsync(
            $"/api/clientes/{creado!.Id}",
            Cuerpo("Razón nueva", cuit));

        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);

        var actualizado = await edicion.Content.ReadFromJsonAsync<ClienteLeido>();
        Assert.Equal("Razón nueva", actualizado!.RazonSocial);
        Assert.Equal(cuit, actualizado.Cuit);
    }

    /// <summary>FR-004: el CUIT mal formado se rechaza con el campo marcado.</summary>
    [Theory]
    [InlineData("123")]
    [InlineData("30712345670")]
    [InlineData("no es un cuit")]
    public async Task Un_CuitMalFormado_SeRechazaConElCampoMarcado(string cuitInvalido)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/clientes",
            Cuerpo("Con CUIT roto", cuitInvalido));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("cuit_invalido", error!.Codigo);
        Assert.Equal("cuit", error.Campo);
    }

    /// <summary>
    /// FR-004: se normaliza <b>antes</b> de validar y de guardar. Escribir <c>30-71234567-8</c> es
    /// válido y se guarda como <c>30712345678</c>, con la misma regla del Módulo 3.
    /// </summary>
    [Fact]
    public async Task El_Cuit_SeNormalizaAntesDeValidarYDeGuardar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var cuit = DatosDePruebaViajes.CuitUnico();
        var conGuiones = $"{cuit[..2]}-{cuit[2..10]}-{cuit[10..]}";

        var alta = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("Con guiones", conGuiones));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var creado = await alta.Content.ReadFromJsonAsync<ClienteLeido>();
        Assert.Equal(cuit, creado!.Cuit);

        // Y el mismo número sin guiones ya no entra: no conviven como dos clientes distintos.
        var repetido = await cliente.PostAsJsonAsync("/api/clientes", Cuerpo("Sin guiones", cuit));
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
    }

    [Fact]
    public async Task Un_EmailSinFormato_SeRechazaConSuPropioCodigo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/clientes", new
        {
            razonSocial = "Sin arroba",
            cuit = DatosDePruebaViajes.CuitUnico(),
            telefono = "0341-555-5555",
            email = "esto-no-es-un-email",
        });

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("email_invalido", error!.Codigo);
        Assert.Equal("email", error.Campo);
    }

    private static object Cuerpo(string razonSocial, string cuit) => new
    {
        razonSocial,
        cuit,
        telefono = "0341-555-5555",
        email = "compras@litoral.com.ar",
        direccion = (string?)null,
    };
}
