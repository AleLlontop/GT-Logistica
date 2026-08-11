using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La anulación de un viaje que no se hizo (FR-036, FR-037, SC-007; US6 esc. 1, 4 y 6).
/// </summary>
public class AnulacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_Motivo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        foreach (var cuerpo in new object[] { new { motivo = "" }, new { motivo = "   " } })
        {
            var respuesta = await cliente.PostAsJsonAsync(
                $"/api/viajes/{viaje.Id}/anulacion",
                cuerpo);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
            Assert.Equal("motivo_requerido", error!.Codigo);
        }

        var sinCambios = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.Pendiente, sinCambios!.Estado);
    }

    [Fact]
    public async Task Con_Motivo_ElViajeQuedaAnuladoYElHistorialLoRegistra()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/anulacion",
            new { motivo = "El cliente canceló la carga." });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(EstadoViaje.Anulado, despues!.Estado);
        Assert.Equal("El cliente canceló la carga.", despues.MotivoAnulacion);

        var historial = await app.HistorialDeAsync(viaje.Id);
        var ultima = historial[^1];

        Assert.Equal(EstadoViaje.Pendiente, ultima.EstadoAnterior);
        Assert.Equal(EstadoViaje.Anulado, ultima.EstadoNuevo);
    }

    /// <summary>
    /// FR-037: las dos unidades quedan libres <b>conservando la asignación</b>. Liberar es dejar de
    /// ocupar, nunca borrar el dato: la ficha tiene que seguir diciendo a quién se le había encargado.
    /// </summary>
    [Fact]
    public async Task Al_Anular_LasUnidadesQuedanLibresConservandoLaAsignacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var primero = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{primero.Id}/en-curso", null);

        await cliente.PostAsJsonAsync(
            $"/api/viajes/{primero.Id}/anulacion",
            new { motivo = "Se rompió el acoplado en el camino." });

        // La unidad quedó libre: otro viaje puede arrancar con ella.
        var segundo = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        var arranca = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.OK, arranca.StatusCode);

        // Y la asignación del anulado se conserva.
        var anulado = await app.RecargarViajeAsync(primero.Id);

        Assert.Equal(escenario.ChoferId, anulado!.ChoferId);
        Assert.Equal(escenario.VehiculoId, anulado.VehiculoId);
    }

    /// <summary>US6 esc. 1 y 4: procede desde `pendiente` y desde `en curso`.</summary>
    [Fact]
    public async Task Procede_DesdePendienteYDesdeEnCurso()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var pendiente = await app.CrearViajeAsync(padron.Id);

        var desdePendiente = await cliente.PostAsJsonAsync(
            $"/api/viajes/{pendiente.Id}/anulacion",
            new { motivo = "El cliente canceló la carga." });

        Assert.Equal(HttpStatusCode.OK, desdePendiente.StatusCode);

        var enCurso = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{enCurso.Id}/en-curso", null);

        var desdeEnCurso = await cliente.PostAsJsonAsync(
            $"/api/viajes/{enCurso.Id}/anulacion",
            new { motivo = "Se suspendió el servicio." });

        Assert.Equal(HttpStatusCode.OK, desdeEnCurso.StatusCode);
    }

    /// <summary>US6 esc. 6: no procede desde `rendido`, que es terminal e inmutable (FR-018).</summary>
    [Fact]
    public async Task No_ProcedeDesdeRendido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Rendido);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/anulacion",
            new { motivo = "Un motivo cualquiera." });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("viaje_rendido_inmutable", error!.Codigo);
    }

    [Fact]
    public async Task Un_MotivoDeMasDe500Caracteres_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/anulacion",
            new { motivo = new string('a', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("motivo_requerido", error!.Codigo);
    }
}
