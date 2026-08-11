using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El alta de nuevo de un cliente dado de baja (FR-007, US1 esc. 9).
///
/// Recurso propio y no un campo del <c>PUT</c>, idempotente y sin confirmación aparte, y sin tocar
/// los viajes históricos.
/// </summary>
public class AltaClienteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_Alta_DevuelveAlPadronYVuelveAOfrecerse()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync(activo: false);

        var respuesta = await cliente.PostAsync($"/api/clientes/{padron.Id}/alta", null);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var activos = await cliente.GetFromJsonAsync<PaginaDeClientesLeida>(
            "/api/clientes?soloActivos=true&pagina=1");

        Assert.Contains(activos!.Items, fila => fila.Id == padron.Id);
    }

    /// <summary>Darle de alta a un cliente ya activo no cambia nada y no es un error (FR-007).</summary>
    [Fact]
    public async Task El_Alta_EsIdempotente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var primera = await cliente.PostAsync($"/api/clientes/{padron.Id}/alta", null);
        var segunda = await cliente.PostAsync($"/api/clientes/{padron.Id}/alta", null);

        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);

        var sinCambios = await app.RecargarClienteAsync(padron.Id);
        Assert.True(sinCambios!.Activo);
        Assert.Equal(padron.RazonSocial, sinCambios.RazonSocial);
    }

    /// <summary>US1 esc. 9: la baja y el alta nunca tocaron los viajes del cliente.</summary>
    [Fact]
    public async Task Los_ViajesHistoricos_QuedanIntactos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var viaje = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Rendido,
            importe: 240_000m);

        await cliente.DeleteAsync($"/api/clientes/{padron.Id}");
        await cliente.PostAsync($"/api/clientes/{padron.Id}/alta", null);

        var intacto = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(EstadoViaje.Rendido, intacto!.Estado);
        Assert.Equal(240_000m, intacto.Importe);
        Assert.Equal(padron.Id, intacto.ClienteId);
    }

    /// <summary>
    /// El <c>PUT</c> de edición no lleva <c>activo</c>: corregir una razón social no puede reactivar
    /// en silencio a alguien que estaba dado de baja (FR-007, precedente [004]).
    /// </summary>
    [Fact]
    public async Task La_Edicion_NoReactivaEnSilencio()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync(activo: false);

        var edicion = await cliente.PutAsJsonAsync($"/api/clientes/{padron.Id}", new
        {
            razonSocial = "Razón corregida",
            cuit = padron.Cuit,
            telefono = "0341-555-0000",
            email = "nuevo@litoral.com.ar",
            // Mandado a propósito: el contrato de entrada no lo tiene, así que no debe tener efecto.
            activo = true,
        });

        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);

        var despues = await app.RecargarClienteAsync(padron.Id);

        Assert.Equal("Razón corregida", despues!.RazonSocial);
        Assert.False(despues.Activo);
    }
}
