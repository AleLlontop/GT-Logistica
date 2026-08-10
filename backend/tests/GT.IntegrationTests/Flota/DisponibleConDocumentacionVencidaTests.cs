using System.Net;
using System.Net.Http.Json;
using GT.Domain.Flota;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-014a y US6 esc. 7: al editar, dejar <c>disponible</c> una unidad con un documento vencido se
/// rechaza <b>nombrando qué documentación lo impide</b>.
///
/// No es lo mismo que FR-014 y conviene no confundirlos: ésta es la validación del formulario, que
/// explica el motivo en el momento; aquélla es la derivación al consultar, que cubre el paso del
/// tiempo. Una sola de las dos deja un agujero (research §4).
/// </summary>
public class DisponibleConDocumentacionVencidaTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_DejarlaDisponible_YNombraElDocumentoVencido()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del rechazo por vencido");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del rechazo por vencido");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro contra terceros");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -5);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(
                vehiculo.Patente,
                tipo.Id,
                transportista.Id,
                estadoOperativo: "disponible"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("disponible_con_documentacion_vencida", error!.Codigo);

        // El mensaje nombra el documento: sin eso, quien opera sabe que no puede pero no qué
        // resolver (FR-014a).
        Assert.Equal(
            $"No podés dejar la unidad disponible: {tipoDocumento.Nombre} está vencido.",
            error.Mensaje);
        Assert.Equal("estadoOperativo", error.Campo);

        // Y el estado guardado no se movió.
        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.Equal(VehiculoEstado.FueraDeServicio, enLaBase!.EstadoOperativo);
    }

    /// <summary>Sin ningún documento, el rechazo es el otro y también lo explica (FR-013).</summary>
    [Fact]
    public async Task Rechaza_DejarlaDisponible_SinNingunDocumento()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del rechazo sin papeles");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del rechazo sin papeles");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(
                vehiculo.Patente,
                tipo.Id,
                transportista.Id,
                estadoOperativo: "disponible"));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("disponible_sin_documentacion", error!.Codigo);
    }

    /// <summary>
    /// Con la documentación en regla, la edición procede: la validación no traba lo que sí se puede
    /// (FR-014a).
    /// </summary>
    [Fact]
    public async Task Acepta_DejarlaDisponible_ConLaDocumentacionEnRegla()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo que sí puede quedar disponible");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del sí puede");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Seguro al día");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 400);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(
                vehiculo.Patente,
                tipo.Id,
                transportista.Id,
                estadoOperativo: "disponible"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var modificado = await respuesta.Content.ReadFromJsonAsync<VehiculoDetalleLeido>();
        Assert.Equal("disponible", modificado!.Estado);
        Assert.Equal("disponible", modificado.EstadoOperativoGuardado);
    }

    /// <summary>
    /// Con documentación <c>proximaAvencer</c> también procede: el papel todavía vale, y avisar no es
    /// inhabilitar (FR-014a).
    /// </summary>
    [Fact]
    public async Task Acepta_DejarlaDisponible_ConDocumentacionProximaAvencer()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del aviso que no traba");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del aviso que no traba");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro por vencer",
            diasAvisoVencimiento: 30);

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 10);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}",
            VehiculosPatenteTests.Alta(
                vehiculo.Patente,
                tipo.Id,
                transportista.Id,
                estadoOperativo: "disponible"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
