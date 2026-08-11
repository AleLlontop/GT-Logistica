using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El bloqueo por documentación vencida <b>a la fecha del viaje</b> (FR-022, FR-024, SC-004, SC-014;
/// US3 esc. 4, 6 y 13).
///
/// Es el control que justifica el módulo, y lo que lo hace distinto de una validación cualquiera es
/// que corre contra la fecha del viaje y no contra hoy: la carga retroactiva puede decir la verdad.
/// </summary>
public class BloqueoPorDocumentacionTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Con_UnDocumentoVencido_SeRechazaNombrandoTipoYNumero()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync(diasDelDocumentoDelChofer: -30);
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("documentacion_vencida", error!.Codigo);

        // Qué unidad y qué documento lo impiden, en el cuerpo además de en el texto (SC-004).
        Assert.Equal(escenario.NombreDelChofer, error.UnidadQueBloquea);
        Assert.Contains("N°", error.DocumentoQueBloquea);

        // Y no se guardó nada.
        var despues = await app.RecargarViajeAsync(viaje.Id);
        Assert.Null(despues!.ChoferId);
        Assert.Null(despues.VehiculoId);
        Assert.Null(despues.TransportistaId);
    }

    /// <summary>US3 esc. 6: un viaje del mes que viene con un documento que vence antes.</summary>
    [Fact]
    public async Task Un_ViajeFuturo_SeRechazaSiElDocumentoVenceAntesDeEsaFecha()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync(diasDelDocumentoDelChofer: 10);

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            fecha: FechaHoyArgentina.Hoy().AddDays(40));

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("documentacion_vencida", error!.Codigo);
    }

    /// <summary>
    /// US3 esc. 13 y SC-014: el viaje retroactivo <b>se acepta</b> si el documento estaba vigente ese
    /// día, aunque hoy esté vencido. Evaluar contra hoy rechazaría un viaje que realmente ocurrió.
    /// </summary>
    [Fact]
    public async Task Un_ViajeRetroactivo_SeAceptaSiElDocumentoEstabaVigenteEseDia()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        // Venció hace 10 días; el viaje es de hace 30, cuando todavía valía.
        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: -10,
            diasDelDocumentoDelVehiculo: -10);

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            fecha: FechaHoyArgentina.Hoy().AddDays(-30));

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(escenario.ChoferId, despues!.ChoferId);
    }

    /// <summary>
    /// FR-024: una unidad <b>sin ningún documento cargado</b> no bloquea. Contradice al Módulo 4
    /// —donde una unidad sin documentación no puede quedar disponible— y es deliberado: son dos
    /// preguntas distintas, y la lista de asignables ya filtró por el estado operativo guardado.
    /// </summary>
    [Fact]
    public async Task Una_UnidadSinNingunDocumento_NoBloquea()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: null,
            diasDelDocumentoDelVehiculo: null);

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();
        Assert.Empty(sobre!.Advertencias);
    }

    /// <summary>El documento del vehículo bloquea igual que el del chofer, y nombra la patente.</summary>
    [Fact]
    public async Task El_DocumentoDelVehiculo_TambienBloquea()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(diasDelDocumentoDelVehiculo: -5);
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("documentacion_vencida", error!.Codigo);
        Assert.Equal(escenario.Patente, error.UnidadQueBloquea);
    }

    /// <summary>El borde declarado: vence exactamente el día del viaje, y eso todavía vale.</summary>
    [Fact]
    public async Task El_DocumentoQueVenceElDiaDelViaje_NoBloquea()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: 0,
            diasDelDocumentoDelVehiculo: 0,
            diasAviso: 0);

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, fecha: FechaHoyArgentina.Hoy());

        var respuesta = await Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    internal static Task<HttpResponseMessage> Asignar(
        HttpClient cliente,
        int viajeId,
        EscenarioDeAsignacion escenario) =>
        cliente.PostAsJsonAsync(
            $"/api/viajes/{viajeId}/asignacion",
            new { choferId = escenario.ChoferId, vehiculoId = escenario.VehiculoId });
}
