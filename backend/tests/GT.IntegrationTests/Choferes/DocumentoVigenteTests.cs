using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// De cada tipo manda uno solo: el de vencimiento más lejano y, con la misma fecha, el de <c>Id</c>
/// mayor (FR-020a, research §8).
///
/// El desempate por <c>Id</c> no es decorativo: dos documentos del mismo tipo con la misma fecha son
/// un error de carga plausible, y sin criterio adicional la consulta devolvería una fila u otra
/// según el plan de ejecución, así que el listado cambiaría solo entre dos consultas idénticas.
/// </summary>
public class DocumentoVigenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Manda_ElDeVencimientoMasLejano_AunqueSeHayaCargadoAntes()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 10111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        // El más lejano se carga primero, para que "el último cargado" no coincida con "el vigente".
        var lejano = await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 500, numero: "LEJANO");
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 10, numero: "CERCANO");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        var vigente = Assert.Single(ficha!.Documentos, documento => documento.EsVigenteDelTipo);
        Assert.Equal(lejano.Id, vigente.Id);

        // Y el chofer figura en regla: el cercano ya no cuenta.
        Assert.Equal("enRegla", ficha.EstadoDocumentacion);
    }

    [Fact]
    public async Task Con_LaMismaFecha_MandaElDeIdMayor()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 10211222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 200, numero: "PRIMERO");
        var segundo = await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 200, numero: "SEGUNDO");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        var vigente = Assert.Single(ficha!.Documentos, documento => documento.EsVigenteDelTipo);
        Assert.Equal(segundo.Id, vigente.Id);
    }

    /// <summary>Cada tipo tiene su propio vigente: no hay un único vigente por chofer.</summary>
    [Fact]
    public async Task Cada_TipoTieneSuVigente()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 10311222);
        var licencia = await app.CrearTipoDocumentacionAsync(nombre: "Licencia");
        var psicofisico = await app.CrearTipoDocumentacionAsync(nombre: "Psicofísico");

        await app.CrearDocumentoAsync(chofer.Id, licencia.Id, -30, numero: "LIC VIEJA");
        var licenciaNueva = await app.CrearDocumentoAsync(chofer.Id, licencia.Id, 400, numero: "LIC NUEVA");
        var psicoVencido = await app.CrearDocumentoAsync(chofer.Id, psicofisico.Id, -5, numero: "PSICO");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        var vigentes = ficha!.Documentos.Where(documento => documento.EsVigenteDelTipo).ToList();

        Assert.Equal(2, vigentes.Count);
        Assert.Contains(vigentes, documento => documento.Id == licenciaNueva.Id);
        Assert.Contains(vigentes, documento => documento.Id == psicoVencido.Id);

        // El peor de los dos vigentes manda: el psicofísico vencido pone al chofer en vencida.
        Assert.Equal("vencida", ficha.EstadoDocumentacion);
    }

    /// <summary>
    /// La ficha trae todos los documentos, vigentes e históricos, agrupados por tipo y con el
    /// vigente primero (contracts/choferes-api.yaml).
    /// </summary>
    [Fact]
    public async Task La_FichaTraeElHistorial_ConElVigentePrimero()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 10411222);
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia con historial");

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -400, numero: "MAS VIEJA");
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -30, numero: "VIEJA");
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 400, numero: "ACTUAL");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        Assert.Equal(3, ficha!.Documentos.Count);
        Assert.Equal(["ACTUAL", "VIEJA", "MAS VIEJA"], ficha.Documentos.Select(d => d.Numero));
        Assert.True(ficha.Documentos[0].EsVigenteDelTipo);
        Assert.All(ficha.Documentos.Skip(1), documento => Assert.False(documento.EsVigenteDelTipo));
    }
}

/// <summary>La ficha del chofer, tal como la devuelve <c>GET /api/choferes/{id}</c>.</summary>
public record ChoferConDocumentos(
    int Id,
    string Apellido,
    string Nombre,
    string Dni,
    string Cuil,
    bool Activo,
    string EstadoDocumentacion,
    int PersonaId,
    IReadOnlyList<DocumentoLeido> Documentos);
