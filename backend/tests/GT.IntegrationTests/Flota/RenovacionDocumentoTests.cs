using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-023, FR-024 y SC-010: cargar una renovación saca la alerta <b>sin que nadie borre el papel
/// viejo</b>, y eliminar el vigente devuelve el mando al más reciente de los que quedan.
///
/// Nada de eso actualiza ninguna fila: el vigente de cada tipo se elige al leer.
/// </summary>
public class RenovacionDocumentoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Una_Renovacion_DejaAlAnteriorComoHistorial_YSacaLaAlerta()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("de la renovación");

        // El seguro venció hace 10 días: la unidad está vencida y alerta.
        var viejo = await app.CrearDocumentoVehiculoAsync(
            vehiculoId,
            tipoId,
            diasHastaVencimiento: -10);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var antes = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");
        Assert.Equal("vencida", antes!.EstadoDocumentacion);

        var enElPanelAntes = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>(
            "/api/flota/vencimientos");
        Assert.Contains(enElPanelAntes!, alerta => alerta.VehiculoId == vehiculoId);

        // Se carga la renovación, sin tocar el documento anterior.
        var renovacion = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(tipoId, diasHastaVencimiento: 365));

        Assert.Equal(HttpStatusCode.Created, renovacion.StatusCode);

        var despues = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");

        Assert.Equal("enRegla", despues!.EstadoDocumentacion);

        // Los dos siguen estando: el viejo es historial, se ve pero no manda (FR-024, FR-038).
        Assert.Equal(2, despues.Documentos.Count);
        Assert.False(Assert.Single(despues.Documentos, d => d.Id == viejo.Id).EsVigenteDelTipo);
        Assert.True(Assert.Single(despues.Documentos, d => d.Id != viejo.Id).EsVigenteDelTipo);

        // Y deja de alertar sin que nadie haya borrado nada (SC-010).
        var enElPanelDespues = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>(
            "/api/flota/vencimientos");
        Assert.DoesNotContain(enElPanelDespues!, alerta => alerta.VehiculoId == vehiculoId);
    }

    /// <summary>
    /// FR-024: al eliminar el vigente, el más reciente de los que quedan vuelve a mandar y el estado
    /// se recalcula solo.
    /// </summary>
    [Fact]
    public async Task Al_EliminarElVigente_ElAnteriorVuelveAMandar()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("de la vuelta atrás");

        await app.CrearDocumentoVehiculoAsync(vehiculoId, tipoId, diasHastaVencimiento: -10);
        var vigente = await app.CrearDocumentoVehiculoAsync(
            vehiculoId,
            tipoId,
            diasHastaVencimiento: 365);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var antes = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");
        Assert.Equal("enRegla", antes!.EstadoDocumentacion);

        var baja = await cliente.DeleteAsync($"/api/flota/documentacion/{vigente.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var despues = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");

        Assert.Equal("vencida", despues!.EstadoDocumentacion);
        Assert.True(Assert.Single(despues.Documentos).EsVigenteDelTipo);
    }

    /// <summary>
    /// La renovación conserva el número: una póliza no cambia de número al renovarse, y por eso la
    /// columna no lleva unicidad (FR-016).
    /// </summary>
    [Fact]
    public async Task Dos_DocumentosDelMismoTipo_PuedenRepetirElNumero()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("del número repetido");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(tipoId, 100, numero: "POL-9999"));

        var segunda = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(tipoId, 400, numero: "POL-9999"));

        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
    }

    /// <summary>
    /// El desempate por <c>Id</c> mayor sobre la misma fecha, verificado de punta a punta: sin él, la
    /// ficha podría cambiar de resultado entre dos consultas idénticas (research §12).
    /// </summary>
    [Fact]
    public async Task Con_LaMismaFechaDeVencimiento_MandaElUltimoCargado()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("del empate");

        await app.CrearDocumentoVehiculoAsync(vehiculoId, tipoId, diasHastaVencimiento: 120);
        var ultimo = await app.CrearDocumentoVehiculoAsync(
            vehiculoId,
            tipoId,
            diasHastaVencimiento: 120);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");

        var vigente = Assert.Single(ficha!.Documentos, d => d.EsVigenteDelTipo);
        Assert.Equal(ultimo.Id, vigente.Id);
    }

    private async Task<(int VehiculoId, int TipoDocumentacionId)> PrepararAsync(string etiqueta)
    {
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: $"Tipo {etiqueta}");
        var transportista = await app.CrearTransportistaAsync(nombre: $"Dueño {etiqueta}");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        var tipoDocumentacion = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: $"Seguro {etiqueta}");

        return (vehiculo.Id, tipoDocumentacion.Id);
    }
}
