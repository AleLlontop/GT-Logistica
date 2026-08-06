using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Baja de un tipo del catálogo (FR-014).
///
/// Se rechaza si tiene documentos asociados, y el mensaje dice cuántos: es lo que explica por qué
/// no se puede, en vez de dejar a quien opera adivinando.
/// </summary>
public class BajaTipoDocumentacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_LaBaja_DeUnTipoConDocumentos_YDiceCuantos()
    {
        var transportista = await app.CrearTransportistaAsync();
        var persona = await app.CrearPersonaAsync(dni: "70111222");
        var chofer = await app.CrearChoferAsync(persona.Id, transportista.Id, cuil: "20701112229");
        var tipo = await app.CrearTipoDocumentacionAsync();

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: 100);
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: 400);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/tipos-documentacion/{tipo.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("tipo_con_documentos", error!.Codigo);
        Assert.Equal("No se puede dar de baja: hay 2 documento(s) de ese tipo cargados.", error.Mensaje);
    }

    [Fact]
    public async Task Da_DeBaja_UnTipoSinDocumentos()
    {
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo sin uso");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/tipos-documentacion/{tipo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        // Baja lógica: sigue existiendo, inactivo, para no romper nada que lo referencie.
        var catalogo = await cliente.GetFromJsonAsync<List<TipoLeido>>("/api/tipos-documentacion");
        var leido = Assert.Single(catalogo!, t => t.Id == tipo.Id);
        Assert.False(leido.Activo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeBajaUnTipoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/tipos-documentacion/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record TipoLeido(
        int Id,
        string Nombre,
        int DiasAvisoVencimiento,
        bool Activo,
        int DocumentosAsociados);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
