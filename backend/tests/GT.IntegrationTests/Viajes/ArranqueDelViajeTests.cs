using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Qué exige poner un viaje en curso (FR-025; US4 esc. 2, 11, 14 y 15).
///
/// <b>Lo que no revisa es tan importante como lo que revisa.</b> La documentación y el estado
/// operativo del vehículo se controlaron al asignar; volver a mirarlos acá dejaría en tierra un viaje
/// planificado con la unidad en regla el día en que se lo asignó.
/// </summary>
public class ArranqueDelViajeTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>US4 esc. 2: sin asignación no arranca.</summary>
    [Fact]
    public async Task Sin_Asignacion_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("falta_asignacion", error!.Codigo);
        Assert.Equal("Asigná chofer y vehículo antes de poner el viaje en curso.", error.Mensaje);
    }

    /// <summary>
    /// US4 esc. 14: el chofer se dio de baja después de asignarlo. El viaje no arranca hasta que se
    /// lo reasigne, y el rechazo dice cuál de los dos es (FR-025).
    /// </summary>
    [Fact]
    public async Task Con_ElChoferDadoDeBaja_SeRechazaIndicandoCual()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        await DarDeBajaChoferAsync(escenario.ChoferId);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("unidad_dada_de_baja", error!.Codigo);
        Assert.Equal(escenario.NombreDelChofer, error.UnidadQueBloquea);
    }

    [Fact]
    public async Task Con_ElVehiculoDadoDeBaja_SeRechazaIndicandoCual()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        await app.EnLaBaseAsync(async contexto =>
        {
            var vehiculo = await contexto.Vehiculos.FirstAsync(v => v.Id == escenario.VehiculoId);
            vehiculo.Activo = false;
            await contexto.SaveChangesAsync();
        });

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("unidad_dada_de_baja", error!.Codigo);
        Assert.Equal(escenario.Patente, error.UnidadQueBloquea);
    }

    /// <summary>Y arranca después de reasignar a una unidad activa (US4 esc. 14).</summary>
    [Fact]
    public async Task Arranca_DespuesDeReasignar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var original = await app.ArmarEscenarioAsync();
        var reemplazo = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(original, asignado: true);

        await DarDeBajaChoferAsync(original.ChoferId);

        var bloqueado = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);

        await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = reemplazo.ChoferId, vehiculoId = reemplazo.VehiculoId });

        var arranca = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.OK, arranca.StatusCode);
    }

    /// <summary>
    /// US4 esc. 11: con la documentación vencida <b>arranca igual</b>. Se controló al asignar, contra
    /// la fecha del viaje; revalidar acá contra hoy dejaría en tierra un viaje planificado con papeles
    /// que estaban en regla cuando se lo asignó (FR-025).
    /// </summary>
    [Fact]
    public async Task Con_DocumentacionVencida_ArrancaIgual()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        // El viaje se asignó cuando el papel valía; hoy está vencido.
        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: -30,
            diasDelDocumentoDelVehiculo: -30);

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.EnCurso, despues!.Estado);
    }

    /// <summary>US4 esc. 15: con el vehículo fuera de servicio, ídem. Se controló al asignar.</summary>
    [Fact]
    public async Task Con_ElVehiculoFueraDeServicio_ArrancaIgual()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        await app.EnLaBaseAsync(async contexto =>
        {
            var vehiculo = await contexto.Vehiculos.FirstAsync(v => v.Id == escenario.VehiculoId);
            vehiculo.EstadoOperativo = VehiculoEstado.FueraDeServicio;
            await contexto.SaveChangesAsync();
        });

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private Task DarDeBajaChoferAsync(int choferId) =>
        app.EnLaBaseAsync(async contexto =>
        {
            var chofer = await contexto.Choferes.FirstAsync(c => c.Id == choferId);
            chofer.Activo = false;
            await contexto.SaveChangesAsync();
        });
}
