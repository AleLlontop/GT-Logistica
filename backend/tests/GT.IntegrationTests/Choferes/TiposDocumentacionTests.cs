using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>Alta y modificación del catálogo de tipos de documentación (FR-013).</summary>
public class TiposDocumentacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(string nombre, int diasAvisoVencimiento = 30) =>
        new { nombre, diasAvisoVencimiento };

    [Fact]
    public async Task Crea_UnTipo_ConSusDiasDeAviso()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/tipos-documentacion",
            Alta("Licencia de conducir"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<TipoLeido>();
        Assert.Equal("Licencia de conducir", creado!.Nombre);
        Assert.Equal(30, creado.DiasAvisoVencimiento);
        Assert.True(creado.Activo);
        Assert.Equal(0, creado.DocumentosAsociados);
    }

    /// <summary>Cero es válido: significa sin período de aviso intermedio (FR-013, caso límite).</summary>
    [Fact]
    public async Task Acepta_CeroDiasDeAviso()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/tipos-documentacion",
            Alta("Constancia sin aviso", diasAvisoVencimiento: 0));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<TipoLeido>();
        Assert.Equal(0, creado!.DiasAvisoVencimiento);
    }

    [Fact]
    public async Task Rechaza_NombreDuplicado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PostAsJsonAsync("/api/tipos-documentacion", Alta("Psicofísico"));

        var respuesta = await cliente.PostAsJsonAsync("/api/tipos-documentacion", Alta("Psicofísico"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("tipo_duplicado", error!.Codigo);
        Assert.Equal("Ya existe un tipo de documentación con ese nombre.", error.Mensaje);
        Assert.Equal("nombre", error.Campo);
    }

    [Fact]
    public async Task Rechaza_DiasDeAvisoNegativos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/tipos-documentacion",
            Alta("Seguro con días negativos", diasAvisoVencimiento: -1));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("diasAvisoVencimiento", error.Campo);
    }

    /// <summary>Conservar el propio nombre al modificar no es un duplicado.</summary>
    [Fact]
    public async Task Modifica_ConservandoSuPropioNombre()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var creacion = await cliente.PostAsJsonAsync("/api/tipos-documentacion", Alta("ART", 15));
        var tipo = await creacion.Content.ReadFromJsonAsync<TipoLeido>();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo!.Id}",
            Alta("ART", 45));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<TipoLeido>();
        Assert.Equal(45, modificado!.DiasAvisoVencimiento);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlModificarUnTipoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/tipos-documentacion/999999",
            Alta("Cualquiera"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("no_encontrado", error!.Codigo);
    }

    /// <summary>El catálogo arranca vacío y `soloActivos` deja afuera a los dados de baja.</summary>
    [Fact]
    public async Task Lista_SoloLosActivos_CuandoSePide()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var creacion = await cliente.PostAsJsonAsync(
            "/api/tipos-documentacion",
            Alta("Tipo que se da de baja"));
        var tipo = await creacion.Content.ReadFromJsonAsync<TipoLeido>();

        await cliente.DeleteAsync($"/api/tipos-documentacion/{tipo!.Id}");

        var todos = await cliente.GetFromJsonAsync<List<TipoLeido>>("/api/tipos-documentacion");
        var activos = await cliente.GetFromJsonAsync<List<TipoLeido>>(
            "/api/tipos-documentacion?soloActivos=true");

        Assert.Contains(todos!, t => t.Id == tipo.Id);
        Assert.DoesNotContain(activos!, t => t.Id == tipo.Id);
    }

    private record TipoLeido(
        int Id,
        string Nombre,
        int DiasAvisoVencimiento,
        bool Activo,
        int DocumentosAsociados);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
