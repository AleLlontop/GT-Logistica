using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-008e y US6 esc. 11: reactivar una unidad cuyo transportista o cuyo tipo se dieron de baja
/// mientras estuvo afuera.
///
/// La reactivación tiene que dejar la unidad en un estado que el alta también aceptaría; si no,
/// quedaría un vehículo activo apuntando a un transportista inactivo, que es lo que FR-008a prohíbe.
/// Por eso el cuerpo es <b>opcional</b>: sólo hace falta cuando algo se cayó (research §11).
/// </summary>
public class ReactivacionConDependenciasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_LaReactivacion_ConElTransportistaDadoDeBaja()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del transportista caído");
        var caido = await app.CrearTransportistaAsync(
            nombre: "Transportista caído al reactivar",
            activo: false);

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, caido.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("transportista_inactivo_al_reactivar", error!.Codigo);
        Assert.Equal(
            "El transportista de esta unidad está dado de baja. Elegí uno activo para reactivarla.",
            error.Mensaje);
        Assert.Equal("transportistaId", error.Campo);

        // Y la unidad sigue dada de baja: el rechazo no dejó nada a medias.
        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.False(enLaBase!.Activo);
    }

    /// <summary>Con el reemplazo activo en el cuerpo, la reactivación procede (US6 esc. 11).</summary>
    [Fact]
    public async Task Procede_AlEnviarUnTransportistaActivo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del reemplazo de transportista");
        var caido = await app.CrearTransportistaAsync(nombre: "Transportista reemplazado", activo: false);
        var nuevo = await app.CrearTransportistaAsync(nombre: "Transportista de reemplazo");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, caido.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { transportistaId = nuevo.Id });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.True(enLaBase!.Activo);
        Assert.Equal(nuevo.Id, enLaBase.TransportistaId);
    }

    [Fact]
    public async Task Rechaza_LaReactivacion_ConElTipoDadoDeBaja()
    {
        var caido = await app.CrearTipoVehiculoAsync(nombre: "Tipo caído al reactivar", activo: false);
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del tipo caído");

        var vehiculo = await app.CrearVehiculoAsync(caido.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("tipo_inactivo_al_reactivar", error!.Codigo);
        Assert.Equal(
            "El tipo de esta unidad está dado de baja. Elegí uno activo para reactivarla.",
            error.Mensaje);
        Assert.Equal("tipoVehiculoId", error.Campo);
    }

    [Fact]
    public async Task Procede_AlEnviarUnTipoActivo()
    {
        var caido = await app.CrearTipoVehiculoAsync(nombre: "Tipo reemplazado", activo: false);
        var nuevo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de reemplazo");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del reemplazo de tipo");

        var vehiculo = await app.CrearVehiculoAsync(caido.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { tipoVehiculoId = nuevo.Id });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.True(enLaBase!.Activo);
        Assert.Equal(nuevo.Id, enLaBase.TipoVehiculoId);
    }

    /// <summary>
    /// El caso normal —la unidad que vuelve con todo en orden— no necesita cuerpo: pedirlo siempre
    /// sería molesto para lo que más pasa (research §11).
    /// </summary>
    [Fact]
    public async Task Sin_Cuerpo_ReactivaCuandoTodoSigueActivo()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la vuelta sin cuerpo");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la vuelta sin cuerpo");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlReactivarUnaUnidadInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/flota/vehiculos/999999/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
