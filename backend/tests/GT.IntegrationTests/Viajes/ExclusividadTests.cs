using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La exclusividad de las unidades (FR-026, FR-027; US4 esc. 3, 4 y 5, US3 esc. 12).
///
/// <b>Sólo <c>en curso</c> ocupa.</b> Dos viajes pendientes con el mismo chofer y la misma fecha se
/// aceptan: un pendiente todavía no compromete a nadie, y prohibirlo obligaría a planificar en un
/// orden que la operación real no tiene.
/// </summary>
public class ExclusividadTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_SegundoViajeConElMismoChofer_SeRechazaNombrandoElQueLoOcupa()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var otroEscenario = await app.ArmarEscenarioAsync();

        var primero = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{primero.Id}/en-curso", null);

        // El segundo comparte chofer y usa otro vehículo, para aislar el motivo del rechazo.
        var segundo = await app.CrearViajeAsync(
            escenario.ClienteId,
            choferId: escenario.ChoferId,
            vehiculoId: otroEscenario.VehiculoId,
            transportistaId: escenario.TransportistaId);

        var respuesta = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("chofer_ocupado", error!.Codigo);

        // El rechazo nombra el viaje que lo ocupa, en el texto **y** en el cuerpo (FR-026).
        var recargado = await app.RecargarViajeAsync(primero.Id);
        Assert.Equal(recargado!.Numero, error.ViajeQueOcupa);
        Assert.Contains($"viaje {recargado.Numero}", error.Mensaje);
    }

    [Fact]
    public async Task El_SegundoViajeConElMismoVehiculo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var otroEscenario = await app.ArmarEscenarioAsync();

        var primero = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{primero.Id}/en-curso", null);

        var segundo = await app.CrearViajeAsync(
            escenario.ClienteId,
            choferId: otroEscenario.ChoferId,
            vehiculoId: escenario.VehiculoId,
            transportistaId: otroEscenario.TransportistaId);

        var respuesta = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("vehiculo_ocupado", error!.Codigo);
    }

    /// <summary>
    /// US3 esc. 12: dos viajes <c>pendiente</c> con el mismo chofer y la misma fecha se aceptan. Un
    /// pendiente no ocupa a nadie (FR-027).
    /// </summary>
    [Fact]
    public async Task Dos_ViajesPendientesConElMismoChoferYLaMismaFecha_SeAceptan()
    {
        var escenario = await app.ArmarEscenarioAsync();
        var fecha = FechaHoyArgentina.Hoy();

        var primero = await app.CrearViajeDelEscenarioAsync(escenario, fecha: fecha, asignado: true);
        var segundo = await app.CrearViajeDelEscenarioAsync(escenario, fecha: fecha, asignado: true);

        Assert.Equal(EstadoViaje.Pendiente, primero.Estado);
        Assert.Equal(EstadoViaje.Pendiente, segundo.Estado);
        Assert.Equal(escenario.ChoferId, segundo.ChoferId);
    }

    /// <summary>US4 esc. 5: al rendir el primero, el segundo arranca.</summary>
    [Fact]
    public async Task Al_RendirElPrimero_ElSegundoArranca()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var primero = await app.CrearViajeDelEscenarioAsync(
            escenario,
            asignado: true,
            importe: 240_000m);

        var segundo = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        await cliente.PostAsync($"/api/viajes/{primero.Id}/en-curso", null);

        var bloqueado = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);
        Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);

        await cliente.PostAsync($"/api/viajes/{primero.Id}/rendicion", null);

        var arranca = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);
        Assert.Equal(HttpStatusCode.OK, arranca.StatusCode);
    }

    /// <summary>
    /// FR-037: al rendir, la unidad queda libre <b>conservando la asignación</b>. Liberar es dejar de
    /// ocupar, nunca borrar el dato: la ficha tiene que seguir diciendo quién hizo el viaje.
    /// </summary>
    [Fact]
    public async Task Al_Rendir_LaAsignacionSeConserva()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            asignado: true,
            importe: 240_000m);

        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null);

        var rendido = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(EstadoViaje.Rendido, rendido!.Estado);
        Assert.Equal(escenario.ChoferId, rendido.ChoferId);
        Assert.Equal(escenario.VehiculoId, rendido.VehiculoId);
        Assert.Equal(escenario.TransportistaId, rendido.TransportistaId);
    }
}
