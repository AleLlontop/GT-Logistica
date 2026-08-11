using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La advertencia por documento próximo a vencer (FR-023, FR-015a, US3 esc. 5).
///
/// <b>La asignación se guarda igual.</b> El criterio para advertir con el resultado en vez de exigir
/// confirmación previa no es la gravedad del aviso sino la <b>reversibilidad</b>: reasignar es
/// reversible mientras el viaje no esté rendido ni anulado (research §5).
/// </summary>
public class AdvertenciaAsignacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Un_DocumentoDentroDeLaVentanaDeAviso_AdvierteYGuarda()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        // Vence en 10 días, con ventana de aviso de 30: está próximo a vencer, no vencido.
        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: 10,
            diasDelDocumentoDelVehiculo: 400,
            diasAviso: 30);

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        var advertencia = Assert.Single(sobre!.Advertencias);

        Assert.Equal("documentacion_proxima_a_vencer", advertencia.Codigo);

        // Nombra el afectado: sin eso, quien opera sabe que algo vence pero no de quién (FR-023).
        Assert.Contains(escenario.NombreDelChofer, advertencia.Mensaje);

        // Y **se guardó**: la advertencia no es un error.
        var despues = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(escenario.ChoferId, despues!.ChoferId);
        Assert.Equal(escenario.VehiculoId, despues.VehiculoId);
    }

    /// <summary>
    /// Si a las dos unidades les falta poco, se informan las dos: nombrar sólo una dejaría la otra
    /// sin resolver.
    /// </summary>
    [Fact]
    public async Task Con_LasDosUnidadesPorVencer_SeInformanLasDos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync(
            diasDelDocumentoDelChofer: 5,
            diasDelDocumentoDelVehiculo: 7,
            diasAviso: 30);

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        Assert.Equal(2, sobre!.Advertencias.Count);
        Assert.Contains(sobre.Advertencias, a => a.Mensaje.Contains(escenario.NombreDelChofer));
        Assert.Contains(sobre.Advertencias, a => a.Mensaje.Contains(escenario.Patente));
    }

    [Fact]
    public async Task Con_TodoEnRegla_NoHayNingunaAdvertencia()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync(diasAviso: 30);
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        Assert.Empty(sobre!.Advertencias);
    }
}
