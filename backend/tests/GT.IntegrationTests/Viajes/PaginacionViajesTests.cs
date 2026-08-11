using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La paginación y el orden del listado (FR-043, US5 esc. 7).
///
/// <b>Es el primer orden del sistema que no termina en <c>Id</c></b>: termina en <c>Numero</c>, que
/// tiene índice único propio y además es el que ve el usuario. La convención [003] pide un orden
/// <b>total</b>, no uno que termine en <c>Id</c>, y con el número se cumple (research §12).
/// </summary>
public class PaginacionViajesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Devuelve_VeinteFilasPorPaginaConElTotalCompleto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        for (var i = 0; i < 25; i++)
        {
            await app.CrearViajeAsync(padron.Id);
        }

        var primera = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&pagina=1");

        Assert.Equal(20, primera!.Items.Count);
        Assert.Equal(25, primera.Total);
        Assert.Equal(20, primera.TamanioPagina);

        var segunda = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&pagina=2");

        Assert.Equal(5, segunda!.Items.Count);
        Assert.Equal(25, segunda.Total);
    }

    /// <summary>
    /// US5 esc. 7: dos viajes del mismo día <b>no se intercambian entre páginas</b>. Sin un criterio
    /// total, uno aparecería dos veces y el otro nunca.
    /// </summary>
    [Fact]
    public async Task Dos_ViajesDelMismoDia_NoSeIntercambianEntrePaginas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var mismaFecha = FechaHoyArgentina.Hoy();

        for (var i = 0; i < 25; i++)
        {
            await app.CrearViajeAsync(padron.Id, fecha: mismaFecha);
        }

        var vistos = new List<int>();

        foreach (var numeroDePagina in new[] { 1, 2 })
        {
            var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
                $"/api/viajes?clienteId={padron.Id}&pagina={numeroDePagina}");

            vistos.AddRange(pagina!.Items.Select(fila => fila.Id));
        }

        Assert.Equal(25, vistos.Count);
        Assert.Equal(25, vistos.Distinct().Count());
    }

    /// <summary>Fecha descendente y, a igual fecha, número descendente. Lo más reciente primero.</summary>
    [Fact]
    public async Task Ordena_PorFechaYNumeroDescendente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var viejo = await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-5));
        var primeroDeHoy = await app.CrearViajeAsync(padron.Id, fecha: hoy);
        var segundoDeHoy = await app.CrearViajeAsync(padron.Id, fecha: hoy);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}");

        Assert.Equal(
            [segundoDeHoy.Id, primeroDeHoy.Id, viejo.Id],
            pagina!.Items.Select(fila => fila.Id));
    }

    /// <summary>
    /// Los filtros se aplican <b>antes</b> de paginar, sobre todos los viajes, y <c>total</c> cuenta
    /// las coincidencias completas y no las de esta página (FR-043).
    /// </summary>
    [Fact]
    public async Task El_Total_CuentaLasCoincidenciasCompletas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        for (var i = 0; i < 22; i++)
        {
            await app.CrearViajeAsync(padron.Id, origen: "Rosario");
        }

        await app.CrearViajeAsync(padron.Id, origen: "Mendoza");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=rosario&pagina=1");

        Assert.Equal(22, pagina!.Total);
        Assert.Equal(20, pagina.Items.Count);
    }

    /// <summary>
    /// Pedir el listado sin el parámetro de página tiene que tomar el valor por defecto en vez de
    /// fallar al enlazar (convención [003]).
    /// </summary>
    [Fact]
    public async Task Sin_ParametroDePagina_TomaLaPrimera()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/viajes");

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);

        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaDeViajesLeida>();
        Assert.Equal(1, pagina!.Pagina);
    }
}
