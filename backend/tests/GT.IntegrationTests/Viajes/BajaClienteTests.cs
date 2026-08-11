using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La baja del cliente y su restricción por dependientes vivos (FR-006, SC-009; US1 esc. 6 y 8).
///
/// <b>El caso que la revisión de calidad de la spec destapó</b>: FR-006 rechazaba la baja por
/// cualquier viaje no anulado, incluidos los rendidos, con lo que el único cliente dado de baja
/// posible era el que nunca había operado — mientras US1 justifica la baja con "el que dejó de operar
/// con la empresa", que por definición tiene historial. Los dos primeros tests de acá son ese caso.
/// </summary>
public class BajaClienteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.EnCurso)]
    public async Task La_Baja_SeRechazaConViajesVivos(EstadoViaje estado)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, estado: estado);
        await app.CrearViajeAsync(padron.Id, estado: estado);

        var respuesta = await cliente.DeleteAsync($"/api/clientes/{padron.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("cliente_con_viajes", error!.Codigo);

        // La cantidad va en el cuerpo, no sólo en el texto (SC-009, precedente [004]).
        Assert.Equal(2, error.CantidadViajes);
        Assert.Contains("2 viaje(s)", error.Mensaje);

        // Y no se tocó nada.
        var sinCambios = await app.RecargarClienteAsync(padron.Id);
        Assert.True(sinCambios!.Activo);
    }

    /// <summary>
    /// US1 esc. 8: el cliente que dejó de operar. Todos sus viajes están cerrados —rendidos o
    /// anulados— y la baja procede: es el caso normal, no la excepción.
    /// </summary>
    [Fact]
    public async Task La_Baja_ProcedeConTodosLosViajesCerrados()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Rendido, importe: 120_000m);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Rendido, importe: 80_000m);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Anulado);

        var respuesta = await cliente.DeleteAsync($"/api/clientes/{padron.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var dadoDeBaja = await app.RecargarClienteAsync(padron.Id);
        Assert.False(dadoDeBaja!.Activo);
    }

    [Fact]
    public async Task La_Baja_ProcedeSinNingunViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.DeleteAsync($"/api/clientes/{padron.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>La baja es lógica: el registro no se borra nunca (FR-001).</summary>
    [Fact]
    public async Task La_Baja_NoBorraElRegistro()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await cliente.DeleteAsync($"/api/clientes/{padron.Id}");

        var ficha = await cliente.GetFromJsonAsync<ClienteLeido>($"/api/clientes/{padron.Id}");

        Assert.NotNull(ficha);
        Assert.False(ficha.Activo);
        Assert.Equal(padron.Cuit, ficha.Cuit);
    }

    /// <summary>El cliente inactivo deja de ofrecerse al registrar viajes (FR-008).</summary>
    [Fact]
    public async Task El_ClienteInactivo_NoApareceEntreLosActivos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await cliente.DeleteAsync($"/api/clientes/{padron.Id}");

        var activos = await cliente.GetFromJsonAsync<PaginaDeClientesLeida>(
            "/api/clientes?soloActivos=true&pagina=1");

        Assert.DoesNotContain(activos!.Items, fila => fila.Id == padron.Id);
    }
}
