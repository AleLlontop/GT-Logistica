using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Qué es un viaje anulado a partir de ahí (FR-011, FR-017, FR-047; US6 esc. 5 y 7).
///
/// <c>anulado</c> es <b>terminal</b>: no hay ninguna acción que lo devuelva a <c>pendiente</c> ni a
/// <c>en curso</c>. Deja de contar como trabajo realizado, pero no desaparece de la historia.
/// </summary>
public class ViajeAnuladoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task No_HayTransicionDeVuelta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            estado: EstadoViaje.Anulado,
            asignado: true);

        var aEnCurso = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        var aRendido = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/rendicion",
            new { confirmado = true });

        Assert.Equal(HttpStatusCode.Conflict, aEnCurso.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, aRendido.StatusCode);

        var sinCambios = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.Anulado, sinCambios!.Estado);
    }

    [Fact]
    public async Task Sus_DatosNoSeEditan()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var viaje = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Anulado,
            motivoAnulacion: "El cliente canceló la carga.");

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = padron.Id,
            fecha = viaje.Fecha.ToString("yyyy-MM-dd"),
            origen = "Otro origen",
            destino = "Otro destino",
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("viaje_anulado_inmutable", error!.Codigo);

        var sinCambios = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal("Rosario", sinCambios!.Origen);
    }

    /// <summary>US6 esc. 5: el viaje anulado sigue en la historia, con su motivo a la vista.</summary>
    [Fact]
    public async Task Sigue_EnLaHistoriaConSuMotivo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var viaje = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Anulado,
            motivoAnulacion: "El cliente canceló la carga.");

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>($"/api/viajes/{viaje.Id}");

        Assert.Equal("anulado", ficha!.Estado);
        Assert.Equal("El cliente canceló la carga.", ficha.MotivoAnulacion);
    }

    /// <summary>FR-047 y SC-008: su importe no figura en ningún total.</summary>
    [Fact]
    public async Task Su_ImporteNoFiguraEnNingunTotal()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearViajeAsync(padron.Id, fecha: hoy, estado: EstadoViaje.Rendido, importe: 100_000m);
        await app.CrearViajeAsync(
            padron.Id,
            fecha: hoy,
            estado: EstadoViaje.Anulado,
            importe: 999_999m,
            motivoAnulacion: "No se hizo.");

        var desde = hoy.AddDays(-1).ToString("yyyy-MM-dd");
        var hasta = hoy.AddDays(1).ToString("yyyy-MM-dd");

        var totales = await cliente.GetFromJsonAsync<TotalesLeidos>(
            $"/api/viajes/totales?desde={desde}&hasta={hasta}");

        var delCliente = totales!.PorCliente.Single(fila => fila.Id == padron.Id);

        Assert.Equal(1, delCliente.CantidadViajes);
        Assert.Equal(100_000m, delCliente.ImporteTotal);
    }

    /// <summary>FR-011: su número no se reutiliza. La secuencia sólo avanza.</summary>
    [Fact]
    public async Task Su_NumeroNoSeReutiliza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var anulado = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Anulado,
            motivoAnulacion: "No se hizo.");

        var siguiente = await app.CrearViajeAsync(padron.Id);

        Assert.True(siguiente.Numero > anulado.Numero);
    }
}
