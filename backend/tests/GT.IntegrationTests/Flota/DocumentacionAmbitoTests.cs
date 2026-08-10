using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-017a y US3 esc. 12: la documentación de un vehículo sólo acepta tipos de <b>ámbito vehículo</b>.
///
/// El formulario ya filtra el selector, pero el servidor lo verifica igual: mandar el identificador de
/// un tipo de chofer a mano se rechaza como si el tipo no existiera.
/// </summary>
public class DocumentacionAmbitoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_UnTipoDeAmbitoChofer()
    {
        var vehiculo = await CrearUnidadAsync("del ámbito rechazado");
        var deChofer = await app.CrearTipoDocumentacionAsync(nombre: "Licencia que la flota no acepta");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo}/documentacion",
            AyudasDeDocumentacion.Formulario(deChofer.Id, diasHastaVencimiento: 200));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_inexistente", error!.Codigo);
        Assert.Equal("documentacionTipoId", error.Campo);
    }

    [Fact]
    public async Task Acepta_UnTipoDeAmbitoVehiculo()
    {
        var vehiculo = await CrearUnidadAsync("del ámbito aceptado");
        var deVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "VTV aceptada");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo}/documentacion",
            AyudasDeDocumentacion.Formulario(deVehiculo.Id, diasHastaVencimiento: 200));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    /// <summary>Un tipo de ámbito vehículo pero <b>inactivo</b> tampoco se acepta (FR-017a).</summary>
    [Fact]
    public async Task Rechaza_UnTipoDeVehiculoInactivo()
    {
        var vehiculo = await CrearUnidadAsync("del tipo inactivo");
        var inactivo = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro dado de baja",
            activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo}/documentacion",
            AyudasDeDocumentacion.Formulario(inactivo.Id, diasHastaVencimiento: 200));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_inexistente", error!.Codigo);
    }

    /// <summary>
    /// FR-026 y FR-017a: corregir el tipo de un documento tampoco puede llevarlo a otro ámbito. La
    /// corrección aplica las mismas validaciones que el alta.
    /// </summary>
    [Fact]
    public async Task Al_Corregir_TampocoAceptaUnTipoDeChofer()
    {
        var vehiculo = await CrearUnidadAsync("de la corrección de ámbito");
        var deVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "RUTA corregible");
        var deChofer = await app.CrearTipoDocumentacionAsync(nombre: "Psicofísico inalcanzable");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo}/documentacion",
            AyudasDeDocumentacion.Formulario(deVehiculo.Id, diasHastaVencimiento: 200));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();

        var correccion = await cliente.PutAsync(
            $"/api/flota/documentacion/{documento!.Id}",
            AyudasDeDocumentacion.Formulario(deChofer.Id, diasHastaVencimiento: 200));

        Assert.Equal(HttpStatusCode.BadRequest, correccion.StatusCode);

        var error = await correccion.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_inexistente", error!.Codigo);
    }

    /// <summary>FR-018: el vencimiento tiene que ser posterior a la emisión, no igual.</summary>
    [Fact]
    public async Task Rechaza_UnVencimientoIgualALaEmision()
    {
        var vehiculo = await CrearUnidadAsync("de las fechas");
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Tipo de las fechas");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var hoy = GT.Domain.Choferes.FechaHoyArgentina.Hoy();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                diasHastaVencimiento: 0,
                fechaEmision: hoy,
                fechaVencimiento: hoy));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("vencimiento_anterior_a_emision", error!.Codigo);
        Assert.Equal("fechaVencimiento", error.Campo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_SiElVehiculoNoExiste()
    {
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Tipo sin vehículo");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            "/api/flota/vehiculos/999999/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, diasHastaVencimiento: 200));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private async Task<int> CrearUnidadAsync(string etiqueta)
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: $"Tipo {etiqueta}");
        var transportista = await app.CrearTransportistaAsync(nombre: $"Dueño {etiqueta}");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        return vehiculo.Id;
    }
}
