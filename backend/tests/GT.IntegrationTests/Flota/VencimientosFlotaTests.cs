using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Panel de vencimientos de la flota (FR-035, US5 esc. 1 a 4).
///
/// Nadie ejecuta nada: el estado se calcula al consultar, así que un documento entra al panel solo el
/// día que le toca (FR-022, SC-005).
/// </summary>
public class VencimientosFlotaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// US5 esc. 1 y 3: entran los vencidos y los que están dentro de la ventana de aviso de su tipo;
    /// los que vencen más lejos no.
    /// </summary>
    [Fact]
    public async Task Entran_LosVencidosYLosProximos_YNoLosQueVencenLejos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del panel de flota");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del panel de flota");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del panel de flota",
            diasAvisoVencimiento: 30);

        var vencido = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(vencido.Id, tipoDocumento.Id, -5);

        var porVencer = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(porVencer.Id, tipoDocumento.Id, 10);

        var alDia = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(alDia.Id, tipoDocumento.Id, 300);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        Assert.Contains(panel!, a => a.VehiculoId == vencido.Id);
        Assert.Contains(panel!, a => a.VehiculoId == porVencer.Id);
        Assert.DoesNotContain(panel!, a => a.VehiculoId == alDia.Id);
    }

    /// <summary>
    /// US5 esc. 4: <b>los vehículos dados de baja no aparecen</b>, cualquiera sea el estado de sus
    /// papeles. Ya no forman parte de la flota operativa y nadie va a renovarlos (FR-035).
    /// </summary>
    [Fact]
    public async Task No_ApareceUnVehiculoDadoDeBaja()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del panel sin bajas");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del panel sin bajas");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del panel sin bajas");

        var dadoDeBaja = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            activo: false);

        await app.CrearDocumentoVehiculoAsync(dadoDeBaja.Id, tipoDocumento.Id, -100);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        Assert.DoesNotContain(panel!, a => a.VehiculoId == dadoDeBaja.Id);

        // Y al reactivarla vuelve a alertar sola, sin recargar nada (FR-008e, research §10).
        await cliente.PostAsJsonAsync($"/api/flota/vehiculos/{dadoDeBaja.Id}/reactivacion", new { });

        var despues = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");
        Assert.Contains(despues!, a => a.VehiculoId == dadoDeBaja.Id);
    }

    /// <summary>
    /// US5 esc. 2 y FR-024: un documento ya reemplazado por una renovación <b>no alerta</b>. Sólo se
    /// evalúa el vigente de cada tipo.
    /// </summary>
    [Fact]
    public async Task No_AlertaUnDocumentoYaReemplazado()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del panel renovado");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del panel renovado");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del panel renovado");

        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        var viejo = await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -200);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 400);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        Assert.DoesNotContain(panel!, a => a.Documento.Id == viejo.Id);
        Assert.DoesNotContain(panel!, a => a.VehiculoId == vehiculo.Id);
    }

    /// <summary>
    /// Ordenado por urgencia: primero lo vencido hace más tiempo. El <c>Id</c> desempata para que dos
    /// documentos con la misma fecha no cambien de lugar entre dos consultas iguales.
    /// </summary>
    [Fact]
    public async Task Ordena_PorUrgencia()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del orden por urgencia");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del orden por urgencia");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del orden por urgencia",
            diasAvisoVencimiento: 30);

        // Se cargan desordenados a propósito.
        var porVencer = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(porVencer.Id, tipoDocumento.Id, 20);

        var muyVencido = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(muyVencido.Id, tipoDocumento.Id, -90);

        var pocoVencido = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(pocoVencido.Id, tipoDocumento.Id, -2);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        var deEsteTransportista = panel!
            .Where(a => a.Transportista.Id == transportista.Id)
            .Select(a => a.VehiculoId)
            .ToList();

        Assert.Equal([muyVencido.Id, pocoVencido.Id, porVencer.Id], deEsteTransportista);
    }

    /// <summary>
    /// Cada fila trae lo que la pantalla necesita: patente, transportista y el documento con sus días
    /// hasta el vencimiento, que es lo que se muestra como "vence en" o "venció hace" (FR-035).
    /// </summary>
    [Fact]
    public async Task Cada_FilaTraeLaPatente_ElTransportistaYLosDiasHastaElVencimiento()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la fila completa");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la fila completa");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de la fila completa",
            diasAvisoVencimiento: 30);

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            patente: "FA111ZA");

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -7, numero: "POL-777");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        var alerta = Assert.Single(panel!, a => a.VehiculoId == vehiculo.Id);

        Assert.Equal("FA111ZA", alerta.Patente);
        Assert.Equal(transportista.Id, alerta.Transportista.Id);
        Assert.Equal("POL-777", alerta.Documento.Numero);
        Assert.Equal("vencida", alerta.Documento.Estado);
        Assert.Equal(-7, alerta.Documento.DiasHastaVencimiento);
        Assert.True(alerta.Documento.EsVigenteDelTipo);
    }
}
