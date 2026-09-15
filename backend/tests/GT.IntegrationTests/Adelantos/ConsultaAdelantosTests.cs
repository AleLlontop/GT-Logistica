using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Consultar y filtrar los adelantos, con su total adelantado (User Story 2, FR-013 a FR-018).
///
/// Los escenarios se cargan con el ayudante de inserción directa, porque la regla de la fecha no deja
/// registrar por la API un adelanto de octubre. Las aserciones que no filtran por persona recorren todas
/// las páginas y miran sólo las filas del test: la base la comparten los demás tests de la clase.
/// </summary>
public class ConsultaAdelantosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_Filtro_TraeTodos_IncluidosRechazadosYAnulados()
    {
        var (_, ids) = await UnoPorEstadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var filas = await cliente.TodasLasPaginasAsync("/api/adelantos");

        Assert.All(ids.Values, id => Assert.Contains(filas, fila => fila.Id == id));
    }

    [Fact]
    public async Task El_FiltroPorPersona_TraeSoloLosSuyos()
    {
        var (persona, ids) = await UnoPorEstadoAsync();
        await UnoPorEstadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await PaginaAsync(cliente, $"/api/adelantos?personaId={persona}");

        Assert.Equal(ids.Values.Order(), pagina.Items.Select(fila => fila.Id).Order());
        Assert.Equal(4, pagina.Total);
    }

    [Fact]
    public async Task El_Rango_IncluyeSusDosExtremos_YAdmiteUnoSolo()
    {
        var persona = await app.EmpleadoAsync();
        var agosto31 = (await app.CrearAdelantoAsync(persona.Id, fecha: new DateOnly(2026, 8, 31))).Id;
        var septiembre1 = (await app.CrearAdelantoAsync(persona.Id, fecha: new DateOnly(2026, 9, 1))).Id;
        var septiembre30 = (await app.CrearAdelantoAsync(persona.Id, fecha: new DateOnly(2026, 9, 30))).Id;
        var octubre1 = (await app.CrearAdelantoAsync(persona.Id, fecha: new DateOnly(2026, 10, 1))).Id;
        int[] propios = [agosto31, septiembre1, septiembre30, octubre1];
        var cliente = await app.CrearClienteAutenticadoAsync();

        async Task<int[]> PropiosDe(string consulta) =>
            [.. (await cliente.TodasLasPaginasAsync(consulta)).Select(fila => fila.Id).Where(propios.Contains).Order()];

        Assert.Equal(
            new[] { septiembre1, septiembre30 }.Order(),
            await PropiosDe("/api/adelantos?desde=2026-09-01&hasta=2026-09-30"));

        Assert.Equal(
            new[] { septiembre1, septiembre30, octubre1 }.Order(),
            await PropiosDe("/api/adelantos?desde=2026-09-01"));

        Assert.Equal(
            new[] { agosto31, septiembre1, septiembre30 }.Order(),
            await PropiosDe("/api/adelantos?hasta=2026-09-30"));
    }

    [Fact]
    public async Task Un_RangoInvertido_Responde400()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/adelantos?desde=2026-09-30&hasta=2026-09-01");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("rango_invalido", (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);
    }

    [Theory]
    [InlineData("pendiente", EstadoAdelanto.Pendiente)]
    [InlineData("aprobado", EstadoAdelanto.Aprobado)]
    [InlineData("rechazado", EstadoAdelanto.Rechazado)]
    [InlineData("anulado", EstadoAdelanto.Anulado)]
    public async Task Cada_Estado_TraeSoloLosSuyos(string valor, EstadoAdelanto estado)
    {
        var (_, ids) = await UnoPorEstadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var filas = await cliente.TodasLasPaginasAsync($"/api/adelantos?estado={valor}");

        Assert.All(filas, fila => Assert.Equal(valor, fila.Estado));
        Assert.Equal([ids[estado]], filas.Select(fila => fila.Id).Where(ids.Values.Contains));
    }

    [Fact]
    public async Task Un_EstadoDesconocido_SeIgnora()
    {
        var (_, ids) = await UnoPorEstadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var filas = await cliente.TodasLasPaginasAsync("/api/adelantos?estado=entregado");

        Assert.All(ids.Values, id => Assert.Contains(filas, fila => fila.Id == id));
    }

    [Fact]
    public async Task Los_TresFiltrosCombinados_TraenLosQueCumplenTodos()
    {
        var juan = await app.EmpleadoAsync("Pérez", "Juan");
        var ana = await app.EmpleadoAsync("Torres", "Ana");
        var buscado = (await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Aprobado, fecha: new DateOnly(2026, 9, 10))).Id;
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Pendiente, fecha: new DateOnly(2026, 9, 10));
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Aprobado, fecha: new DateOnly(2026, 8, 10));
        await app.CrearAdelantoAsync(ana.Id, EstadoAdelanto.Aprobado, fecha: new DateOnly(2026, 9, 10));
        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await PaginaAsync(
            cliente,
            $"/api/adelantos?personaId={juan.Id}&desde=2026-09-01&hasta=2026-09-30&estado=aprobado");

        Assert.Equal([buscado], pagina.Items.Select(fila => fila.Id));
    }

    /// <summary>US2 esc. 7: el total suma la selección entera y sólo los aprobados.</summary>
    [Fact]
    public async Task El_TotalAdelantado_SumaSoloLosAprobados()
    {
        var juan = await app.EmpleadoAsync("Pérez", "Juan");
        var septiembre = new DateOnly(2026, 9, 14);
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Aprobado, 150_000m, septiembre);
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Aprobado, 50_000m, septiembre);
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Pendiente, 30_000m, septiembre);
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Rechazado, 20_000m, septiembre);
        await app.CrearAdelantoAsync(juan.Id, EstadoAdelanto.Anulado, 40_000m, septiembre);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await PaginaAsync(cliente, $"/api/adelantos?personaId={juan.Id}&desde=2026-09-01&hasta=2026-09-30");

        Assert.Equal(5, pagina.Total);
        Assert.Equal(200_000m, pagina.TotalAdelantado);

        // Sin aprobados en la selección, el total es cero y no falla (research §9.3).
        var pendientes = await PaginaAsync(cliente, $"/api/adelantos?personaId={juan.Id}&estado=pendiente");

        Assert.Equal(1, pendientes.Total);
        Assert.Equal(0m, pendientes.TotalAdelantado);
    }

    /// <summary>FR-016 y FR-017: el total es de todas las páginas, y el orden no repite ni saltea.</summary>
    [Fact]
    public async Task Pagina_DeA20_ConElTotalDeTodasLasPaginas_YOrdenTotalPorFechaEId()
    {
        var persona = await app.EmpleadoAsync();

        for (var i = 0; i < 25; i++)
        {
            // Cinco fechas distintas: varias filas comparten fecha y el `Id` desempata.
            await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado, 1_000m, new DateOnly(2026, 9, 1 + i % 5));
        }

        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await PaginaAsync(cliente, $"/api/adelantos?personaId={persona.Id}");
        var segunda = await PaginaAsync(cliente, $"/api/adelantos?personaId={persona.Id}&pagina=2");

        Assert.Equal(20, primera.Items.Count);
        Assert.Equal(5, segunda.Items.Count);
        Assert.Equal((25, 20), (primera.Total, primera.TamanioPagina));
        Assert.Equal(25_000m, primera.TotalAdelantado);
        Assert.Equal(25_000m, segunda.TotalAdelantado);

        var todas = primera.Items.Concat(segunda.Items).ToList();

        Assert.Equal(25, todas.Select(fila => fila.Id).Distinct().Count());
        Assert.Equal(
            todas.OrderByDescending(fila => fila.Fecha, StringComparer.Ordinal).ThenByDescending(fila => fila.Id).Select(fila => fila.Id),
            todas.Select(fila => fila.Id));
    }

    [Fact]
    public async Task Cada_Fila_TraeLaPersonaVigente_YElTipoGuardado()
    {
        var juan = await app.ChoferPropioAsync("Pérez", "Juan");
        var adelanto = await app.CrearAdelantoAsync(juan.Id, tipo: TipoBeneficiario.Chofer, motivo: "Gastos médicos", importe: 150_000m, fecha: new DateOnly(2026, 9, 14));
        await app.CorregirApellidoAsync(juan.Id, "Pérez Gil");
        await app.DarDeBajaFichaAsync(juan.Id);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var fila = Assert.Single((await PaginaAsync(cliente, $"/api/adelantos?personaId={juan.Id}")).Items);

        Assert.Equal(
            new AdelantoListadoLeido(
                adelanto.Id,
                "2026-09-14",
                new PersonaResumenLeida(juan.Id, "Pérez Gil", "Juan", juan.Dni),
                "chofer",
                "Gastos médicos",
                150_000m,
                "pendiente"),
            fila);
    }

    private async Task<(int PersonaId, Dictionary<EstadoAdelanto, int> Ids)> UnoPorEstadoAsync()
    {
        var persona = await app.EmpleadoAsync();
        var ids = new Dictionary<EstadoAdelanto, int>();

        foreach (var estado in Enum.GetValues<EstadoAdelanto>())
        {
            ids[estado] = (await app.CrearAdelantoAsync(persona.Id, estado)).Id;
        }

        return (persona.Id, ids);
    }

    private static async Task<PaginaDeAdelantosLeida> PaginaAsync(HttpClient cliente, string consulta) =>
        (await cliente.GetFromJsonAsync<PaginaDeAdelantosLeida>(consulta))!;
}
