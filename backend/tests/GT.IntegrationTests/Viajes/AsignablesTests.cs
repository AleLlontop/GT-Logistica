using System.Net.Http.Json;
using GT.Domain.Flota;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La lista de asignables (FR-021, US3 esc. 2 y 3).
///
/// Lo que <b>no</b> hace es tan importante como lo que hace: no filtra por documentación. Eso se
/// evalúa al asignar, contra la fecha del viaje, y filtrarlo acá rompería la carga retroactiva
/// (SC-014).
/// </summary>
public class AsignablesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task No_ApareceNingunChoferDadoDeBaja()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync(choferActivo: false);

        var asignables = await cliente.GetFromJsonAsync<AsignablesLeidos>("/api/viajes/asignables");

        Assert.DoesNotContain(asignables!.Choferes, fila => fila.Id == escenario.ChoferId);
    }

    [Fact]
    public async Task No_ApareceNingunVehiculoDadoDeBaja()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync(vehiculoActivo: false);

        var asignables = await cliente.GetFromJsonAsync<AsignablesLeidos>("/api/viajes/asignables");

        Assert.DoesNotContain(asignables!.Vehiculos, fila => fila.Id == escenario.VehiculoId);
    }

    /// <summary>
    /// El filtro es el <b>estado operativo guardado</b>, no el derivado: es lo que el Módulo 4 dice
    /// sobre la unidad, sin recalcularlo contra el día en curso (FR-021, SC-014).
    /// </summary>
    [Fact]
    public async Task No_ApareceNingunVehiculoFueraDeServicio()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(
            estadoDelVehiculo: VehiculoEstado.FueraDeServicio);

        var asignables = await cliente.GetFromJsonAsync<AsignablesLeidos>("/api/viajes/asignables");

        Assert.DoesNotContain(asignables!.Vehiculos, fila => fila.Id == escenario.VehiculoId);
    }

    [Fact]
    public async Task Los_ActivosYDisponiblesAparecen()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var asignables = await cliente.GetFromJsonAsync<AsignablesLeidos>("/api/viajes/asignables");

        Assert.Contains(asignables!.Choferes, fila => fila.Id == escenario.ChoferId);
        Assert.Contains(asignables.Vehiculos, fila => fila.Id == escenario.VehiculoId);
    }

    /// <summary>
    /// FR-021: la habilitación por documentación <b>no</b> filtra esta lista. Un chofer con la
    /// licencia vencida se ofrece igual; lo que se rechaza es la asignación, y con un mensaje que
    /// explica qué documento la impide.
    /// </summary>
    [Fact]
    public async Task La_Lista_NoFiltraPorDocumentacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: -30,
            diasDelDocumentoDelVehiculo: -30);

        var asignables = await cliente.GetFromJsonAsync<AsignablesLeidos>("/api/viajes/asignables");

        Assert.Contains(asignables!.Choferes, fila => fila.Id == escenario.ChoferId);
        Assert.Contains(asignables.Vehiculos, fila => fila.Id == escenario.VehiculoId);
    }

    /// <summary>
    /// La ruta literal convive con <c>/api/viajes/{id:int}</c>. Sin la restricción de tipo, el
    /// enrutador trataría <c>asignables</c> como un identificador y esta ruta sería inalcanzable
    /// (tasks §trampa 1).
    /// </summary>
    [Fact]
    public async Task La_RutaLiteral_NoQuedaCapturadaPorLaDeIdentificador()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/viajes/asignables");

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<AsignablesLeidos>());
    }
}
