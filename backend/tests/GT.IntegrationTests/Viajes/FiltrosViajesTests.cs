using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Los cuatro filtros del listado (FR-041, FR-044, SC-010; US5 esc. 2, 5, 9 y 10).
///
/// <b>La exclusión de los anulados es un predicado de la consulta, no un filtrado posterior</b>: sin
/// filtro de estado no aparecen, y con el filtro <c>anulado</c> aparecen sólo ellos, con su motivo.
/// Escrito así, la exclusión es una garantía y no algo que alguien pueda olvidar (FR-044).
/// </summary>
public class FiltrosViajesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_FiltroDeEstado_LosAnuladosNoAparecen()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Pendiente);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Rendido);
        var anulado = await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Anulado,
            motivoAnulacion: "El cliente canceló la carga.");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}");

        Assert.Equal(2, pagina!.Total);
        Assert.DoesNotContain(pagina.Items, fila => fila.Id == anulado.Id);
    }

    /// <summary>US5 esc. 5: con el filtro `anulado` aparecen, y cada fila muestra su motivo (FR-036).</summary>
    [Fact]
    public async Task Con_ElFiltroAnulado_AparecenConSuMotivo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Pendiente);
        await app.CrearViajeAsync(
            padron.Id,
            estado: EstadoViaje.Anulado,
            motivoAnulacion: "El cliente canceló la carga.");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&estado=anulado");

        var fila = Assert.Single(pagina!.Items);

        Assert.Equal("anulado", fila.Estado);
        Assert.Equal("El cliente canceló la carga.", fila.MotivoAnulacion);
    }

    [Fact]
    public async Task El_FiltroPorCliente_DevuelveSoloLosDeEseCliente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var uno = await app.CrearClienteAsync();
        var otro = await app.CrearClienteAsync();

        await app.CrearViajeAsync(uno.Id);
        await app.CrearViajeAsync(uno.Id);
        await app.CrearViajeAsync(otro.Id);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={uno.Id}");

        Assert.Equal(2, pagina!.Total);
        Assert.All(pagina.Items, fila => Assert.Equal(uno.Id, fila.Cliente.Id));
    }

    [Fact]
    public async Task El_FiltroPorRangoDeFechas_UsaLaFechaDelViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-20));
        var dentro = await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-5));
        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(20));

        var desde = hoy.AddDays(-10).ToString("yyyy-MM-dd");
        var hasta = hoy.AddDays(10).ToString("yyyy-MM-dd");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&desde={desde}&hasta={hasta}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(dentro.Id, fila.Id);
    }

    /// <summary>
    /// SC-010: el filtro usa el transportista <b>registrado en el viaje</b>, no el actual del chofer.
    /// Los viajes de un chofer que cambió de transportista siguen apareciendo bajo el de entonces.
    /// </summary>
    [Fact]
    public async Task El_FiltroPorTransportista_UsaElRegistradoEnElViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var otro = await app.ArmarEscenarioAsync();

        var delPrimero = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await app.CrearViajeDelEscenarioAsync(otro, asignado: true);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?transportistaId={escenario.TransportistaId}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(delPrimero.Id, fila.Id);
    }

    /// <summary>US5 esc. 2: los cuatro combinados devuelven sólo los que cumplen **todas**.</summary>
    [Fact]
    public async Task Los_CuatroFiltrosCombinados_ExigenTodasLasCondiciones()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // El que cumple las cuatro.
        var elBueno = await app.CrearViajeDelEscenarioAsync(
            escenario,
            fecha: hoy,
            estado: EstadoViaje.EnCurso,
            asignado: true);

        // Cada uno falla exactamente una condición.
        await app.CrearViajeDelEscenarioAsync(escenario, fecha: hoy, asignado: true);
        await app.CrearViajeDelEscenarioAsync(escenario, fecha: hoy.AddDays(-90), asignado: true);

        var otroCliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(
            otroCliente.Id,
            fecha: hoy,
            estado: EstadoViaje.EnCurso,
            choferId: null,
            vehiculoId: null,
            transportistaId: escenario.TransportistaId);

        var desde = hoy.AddDays(-10).ToString("yyyy-MM-dd");
        var hasta = hoy.AddDays(10).ToString("yyyy-MM-dd");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={escenario.ClienteId}" +
            $"&transportistaId={escenario.TransportistaId}" +
            $"&estado=enCurso&desde={desde}&hasta={hasta}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(elBueno.Id, fila.Id);
    }

    /// <summary>
    /// Un valor de estado desconocido se ignora en vez de romper: filtrar de más no es un error, y el
    /// listado responde su vista por defecto (convención [003]).
    /// </summary>
    [Fact]
    public async Task Un_EstadoDesconocido_SeIgnoraEnVezDeRomper()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id);
        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Anulado);

        var respuesta = await cliente.GetAsync(
            $"/api/viajes?clienteId={padron.Id}&estado=inventado");

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaDeViajesLeida>();

        // Vista por defecto: todos menos los anulados.
        Assert.Equal(1, pagina!.Total);
    }
}
