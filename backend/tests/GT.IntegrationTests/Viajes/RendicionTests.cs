using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La rendición y su confirmación previa (FR-037, FR-038, SC-007a; US4 esc. 5, 6 y 7).
///
/// <b>La confirmación vive en el backend</b>, a diferencia de todas las anteriores del sistema: las
/// bajas del Módulo 3 y del 4 las confirmaba la pantalla porque se deshacen. Rendir con importe en
/// cero no se deshace —FR-018 deja el viaje inmutable para siempre—, así que el primer intento
/// responde <c>409</c> sin cambiar nada. El criterio es la reversibilidad, no la gravedad.
/// </summary>
public class RendicionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Con_ImporteMayorACero_RindeDirecto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var viajeId = await ArmarViajeEnCursoAsync(cliente, importe: 240_000m);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viajeId);
        Assert.Equal(EstadoViaje.Rendido, despues!.Estado);
    }

    /// <summary>US4 esc. 6: el primer intento con importe en cero <b>no cambia nada</b>.</summary>
    [Fact]
    public async Task Con_ImporteEnCero_ElPrimerIntentoNoCambiaNada()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var viajeId = await ArmarViajeEnCursoAsync(cliente, importe: 0m);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("rendicion_requiere_confirmacion", error!.Codigo);

        // Lo que hace válida a la confirmación previa: el viaje sigue exactamente como estaba.
        var sinCambios = await app.RecargarViajeAsync(viajeId);
        Assert.Equal(EstadoViaje.EnCurso, sinCambios!.Estado);

        // Y no quedó ninguna línea de historial de una rendición que no ocurrió.
        var historial = await app.HistorialDeAsync(viajeId);
        Assert.DoesNotContain(historial, linea => linea.EstadoNuevo == EstadoViaje.Rendido);
    }

    [Fact]
    public async Task Con_ImporteEnCero_RindeAlConfirmar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var viajeId = await ArmarViajeEnCursoAsync(cliente, importe: 0m);

        await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viajeId}/rendicion",
            new { confirmado = true });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viajeId);
        Assert.Equal(EstadoViaje.Rendido, despues!.Estado);
    }

    /// <summary>
    /// US4 esc. 7: cancelar deja el viaje en curso con su importe en cero, y se puede completar antes
    /// de volver a rendirlo. El sistema no exige el importe: sólo avisa.
    /// </summary>
    [Fact]
    public async Task Cancelar_DejaElViajeEnCursoYSePuedeCompletarElImporte()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viajeId = await ArmarViajeEnCursoAsync(cliente, importe: 0m, clienteId: padron.Id);

        // Primer intento: pide confirmación. Quien opera cancela y completa el importe.
        await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null);

        var viaje = await app.RecargarViajeAsync(viajeId);

        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{viajeId}", new
        {
            clienteId = viaje!.ClienteId,
            fecha = viaje.Fecha.ToString("yyyy-MM-dd"),
            origen = viaje.Origen,
            destino = viaje.Destino,
            importe = 180_000m,
        });

        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);

        // Con importe, rinde directo: ya no hay nada que confirmar.
        var rendicion = await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null);

        Assert.Equal(HttpStatusCode.OK, rendicion.StatusCode);
    }

    /// <summary>
    /// FR-037: al rendir las dos unidades quedan libres <b>conservando la asignación</b>. La primera
    /// mitad se verifica arrancando otro viaje con la misma unidad; la segunda, mirando la ficha.
    /// </summary>
    [Fact]
    public async Task Al_Rendir_LasUnidadesQuedanLibresConservandoLaAsignacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var primero = await app.CrearViajeDelEscenarioAsync(
            escenario,
            asignado: true,
            importe: 240_000m);

        await cliente.PostAsync($"/api/viajes/{primero.Id}/en-curso", null);
        await cliente.PostAsync($"/api/viajes/{primero.Id}/rendicion", null);

        var segundo = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        var arranca = await cliente.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.OK, arranca.StatusCode);

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>($"/api/viajes/{primero.Id}");

        Assert.Equal(escenario.ChoferId, ficha!.Chofer!.Id);
        Assert.Equal(escenario.VehiculoId, ficha.Vehiculo!.Id);
    }

    private async Task<int> ArmarViajeEnCursoAsync(
        HttpClient cliente,
        decimal importe,
        int? clienteId = null)
    {
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeAsync(
            clienteId ?? escenario.ClienteId,
            importe: importe,
            choferId: escenario.ChoferId,
            vehiculoId: escenario.VehiculoId,
            transportistaId: escenario.TransportistaId);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        respuesta.EnsureSuccessStatusCode();

        return viaje.Id;
    }
}
