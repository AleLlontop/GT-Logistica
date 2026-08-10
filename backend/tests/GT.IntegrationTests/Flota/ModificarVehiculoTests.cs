using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Modificación y reasignación de una unidad (FR-002, FR-008a, FR-008c, SC-003c, US6 esc. 2 a 4).
/// </summary>
public class ModificarVehiculoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// FR-002: la unicidad de la patente <b>excluye al propio vehículo</b>. Conservar la suya al
    /// corregir la marca no puede ser un conflicto.
    /// </summary>
    [Fact]
    public async Task Conservar_LaPropiaPatente_NoEsUnDuplicado()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la patente propia");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la patente propia");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, patente: "GA222ZB");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(
                "GA222ZB",
                tipo.Id,
                transportista.Id,
                marca: "Mercedes-Benz",
                modelo: "Actros"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal("Mercedes-Benz", modificado!.Marca);
        Assert.Equal("Actros", modificado.Modelo);
    }

    /// <summary>Pero la patente de <b>otra</b> unidad sí se rechaza (FR-002).</summary>
    [Fact]
    public async Task Rechaza_LaPatenteDeOtraUnidad()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la patente ajena");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la patente ajena");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, patente: "HA333ZC");
        var otro = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, patente: "IA444ZD");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{otro.Id}",
            VehiculosPatenteTests.Alta("HA333ZC", tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("patente_duplicada", error!.Codigo);
    }

    /// <summary>
    /// US6 esc. 3 y SC-003c: la reasignación a otro transportista <b>conserva la documentación
    /// íntegra</b>. No hace falta tocarla: los documentos cuelgan del vehículo, no del transportista
    /// (FR-008c).
    /// </summary>
    [Fact]
    public async Task La_Reasignacion_ConservaLaDocumentacion()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la reasignación");
        var original = await app.CrearTransportistaAsync(nombre: "Transportista original");
        var nuevo = await app.CrearTransportistaAsync(nombre: "Transportista nuevo");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de la reasignación");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, original.Id);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 300);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -100);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(vehiculo.Patente, tipo.Id, nuevo.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal(nuevo.Id, modificado!.Transportista.Id);

        // Los dos documentos siguen ahí, con el mismo estado que antes.
        Assert.Equal(2, modificado.Documentos.Count);
        Assert.Equal("enRegla", modificado.EstadoDocumentacion);
    }

    /// <summary>US6 esc. 4: no se puede dejar la unidad con un transportista inactivo (FR-008a).</summary>
    [Fact]
    public async Task Rechaza_UnTransportistaInactivo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del transportista caído");
        var activo = await app.CrearTransportistaAsync(nombre: "Transportista que sigue");
        var inactivo = await app.CrearTransportistaAsync(
            nombre: "Transportista que se fue",
            activo: false);

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, activo.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(vehiculo.Patente, tipo.Id, inactivo.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
        Assert.Equal("transportistaId", error.Campo);
    }

    /// <summary>Sin transportista tampoco: la unidad siempre pertenece a alguien (FR-008a).</summary>
    [Fact]
    public async Task Rechaza_DejarlaSinTransportista()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo sin transportista");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño que no se puede quitar");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            new
            {
                patente = vehiculo.Patente,
                marca = "Scania",
                modelo = "R450",
                tipoVehiculoId = tipo.Id,
                transportistaId = (int?)null,
                estadoOperativo = "fueraDeServicio",
            });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("transportistaId", error.Campo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlModificarUnaUnidadInexistente()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo inalcanzable");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño inalcanzable");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/flota/vehiculos/999999",
            VehiculosPatenteTests.Alta("ZZ999ZZ", tipo.Id, transportista.Id));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
