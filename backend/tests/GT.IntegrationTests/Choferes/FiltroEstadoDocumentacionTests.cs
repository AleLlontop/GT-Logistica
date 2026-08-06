using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// El filtro por estado de documentación opera sobre un valor <b>calculado</b>, no almacenado, y se
/// resuelve en la base (FR-022, research §2).
///
/// Era el riesgo de haber elegido calcular el estado al leer: si el filtro no se pudiera traducir a
/// SQL habría que traer todo el padrón a memoria. Este test es el que lo verifica de punta a punta.
/// </summary>
public class FiltroEstadoDocumentacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// Cuatro choferes del mismo transportista, uno por cada valor de FR-029. Filtrar por cada
    /// estado tiene que devolver exactamente el suyo.
    /// </summary>
    [Fact]
    public async Task Filtra_PorCadaUnoDeLosCuatroEstados()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Filtros por estado");
        var tipo = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: 30);

        var enRegla = await app.CrearChoferCompletoAsync(11111222, transportistaId: transportista.Id);
        var porVencer = await app.CrearChoferCompletoAsync(11211222, transportistaId: transportista.Id);
        var vencida = await app.CrearChoferCompletoAsync(11311222, transportistaId: transportista.Id);
        var sinDocumentacion = await app.CrearChoferCompletoAsync(11411222, transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(enRegla.Id, tipo.Id, 300);
        await app.CrearDocumentoAsync(porVencer.Id, tipo.Id, 10);
        await app.CrearDocumentoAsync(vencida.Id, tipo.Id, -10);
        // El cuarto no lleva ninguno, a propósito.

        var cliente = await app.CrearClienteAutenticadoAsync();

        await AssertUnicoAsync(cliente, transportista.Id, "enRegla", enRegla.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "proximaAvencer", porVencer.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "vencida", vencida.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "sinDocumentacion", sinDocumentacion.Id);
    }

    /// <summary>
    /// Un chofer con un vencido y otro por vencer figura <c>vencida</c>: manda el peor
    /// (precedencia de FR-029).
    /// </summary>
    [Fact]
    public async Task El_PeorEstadoManda()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Precedencia");
        var licencia = await app.CrearTipoDocumentacionAsync(nombre: "Licencia precedencia");
        var psicofisico = await app.CrearTipoDocumentacionAsync(nombre: "Psico precedencia");

        var chofer = await app.CrearChoferCompletoAsync(11511222, transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(chofer.Id, licencia.Id, 10);
        await app.CrearDocumentoAsync(chofer.Id, psicofisico.Id, -10);

        var cliente = await app.CrearClienteAutenticadoAsync();

        await AssertUnicoAsync(cliente, transportista.Id, "vencida", chofer.Id);

        var comoPorVencer = await ConsultarAsync(cliente, transportista.Id, "proximaAvencer");
        Assert.Empty(comoPorVencer.Items);
    }

    /// <summary>
    /// Un documento histórico no cambia el estado: sólo se mira el vigente de cada tipo (FR-020a).
    /// </summary>
    [Fact]
    public async Task Los_DocumentosHistoricos_NoAfectanElFiltro()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Historial no cuenta");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia historial");

        var chofer = await app.CrearChoferCompletoAsync(11611222, transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -400, numero: "VIEJA VENCIDA");
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 400, numero: "RENOVADA");

        var cliente = await app.CrearClienteAutenticadoAsync();

        await AssertUnicoAsync(cliente, transportista.Id, "enRegla", chofer.Id);

        var comoVencida = await ConsultarAsync(cliente, transportista.Id, "vencida");
        Assert.Empty(comoVencida.Items);
    }

    private static async Task AssertUnicoAsync(
        HttpClient cliente,
        int transportistaId,
        string estado,
        int choferEsperado)
    {
        var pagina = await ConsultarAsync(cliente, transportistaId, estado);

        var fila = Assert.Single(pagina.Items);
        Assert.Equal(choferEsperado, fila.Id);
        Assert.Equal(estado, fila.EstadoDocumentacion);
    }

    private static async Task<PaginaLeida> ConsultarAsync(
        HttpClient cliente,
        int transportistaId,
        string estado)
    {
        var pagina = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}&estadoDocumentacion={estado}");

        return pagina!;
    }
}

public record ChoferEnListado(
    int Id,
    string Apellido,
    string Nombre,
    string Dni,
    TipoLeido Transportista,
    bool Activo,
    string EstadoDocumentacion);

public record PaginaLeida(
    IReadOnlyList<ChoferEnListado> Items,
    int Total,
    int Pagina,
    int TamanioPagina);
