using System.Net.Http.Json;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// El listado con sus filtros (User Story 2, FR-021 a FR-024, FR-029, SC-010). Cada test filtra por su
/// propio transportista, porque la clase comparte la base.
/// </summary>
public class ConsultaLiquidacionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_FiltroDeEstado_TraeTodasIncluidasLasAnuladas_YCadaEstadoSoloLasSuyas()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        await UnaAsync(clienteId, fletero.Id, EstadoLiquidacion.Pendiente);
        await UnaAsync(clienteId, fletero.Id, EstadoLiquidacion.Pagada);
        await UnaAsync(clienteId, fletero.Id, EstadoLiquidacion.Anulada);

        Assert.Equal(3, (await ListarAsync($"transportistaId={fletero.Id}")).Total);

        foreach (var estado in new[] { "pendiente", "pagada", "anulada" })
        {
            var pagina = await ListarAsync($"transportistaId={fletero.Id}&estado={estado}");
            Assert.Equal(estado, Assert.Single(pagina.Items).Estado);
        }

        // Un estado desconocido se ignora: responde la vista por defecto.
        Assert.Equal(3, (await ListarAsync($"transportistaId={fletero.Id}&estado=vencida")).Total);
    }

    [Fact]
    public async Task Transportista_MesYAnio_FiltranPorSeparadoYCombinados()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var otro = await app.TransportistaExternoAsync("Fletes del Sur S.R.L.");

        await UnaAsync(clienteId, fletero.Id, mes: 7, anio: 2026);
        await UnaAsync(clienteId, fletero.Id, mes: 6, anio: 2026);
        await UnaAsync(clienteId, fletero.Id, mes: 7, anio: 2025, estado: EstadoLiquidacion.Pagada);
        await UnaAsync(clienteId, otro.Id, mes: 7, anio: 2026);

        Assert.Equal(3, (await ListarAsync($"transportistaId={fletero.Id}")).Total);
        Assert.Equal(2, (await ListarAsync($"transportistaId={fletero.Id}&mes=7")).Total);
        Assert.Equal(2, (await ListarAsync($"transportistaId={fletero.Id}&anio=2026")).Total);
        Assert.Equal(1, (await ListarAsync($"transportistaId={fletero.Id}&mes=7&anio=2026&estado=pendiente")).Total);
        Assert.Equal(0, (await ListarAsync($"transportistaId={fletero.Id}&mes=7&anio=2026&estado=pagada")).Total);
    }

    [Fact]
    public async Task Pagina_DeA20_PorNumeroDescendente_SinRepetir()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        for (var i = 0; i < 25; i++)
        {
            await UnaAsync(clienteId, fletero.Id);
        }

        var primera = await ListarAsync($"transportistaId={fletero.Id}&pagina=1");
        var segunda = await ListarAsync($"transportistaId={fletero.Id}&pagina=2");

        Assert.Equal(25, primera.Total);
        Assert.Equal(20, primera.Items.Count);
        Assert.Equal(5, segunda.Items.Count);
        Assert.Equal(20, primera.TamanioPagina);

        var numeros = primera.Items.Concat(segunda.Items)
            .Select(item => int.Parse(item.Numero["LQ-".Length..]))
            .ToList();

        Assert.Equal(25, numeros.Distinct().Count());
        Assert.Equal(numeros.OrderDescending(), numeros);
    }

    [Fact]
    public async Task Resta_PagarEsNulaEnLaAnulada_CeroEnLaPagada_YElTransportistaDadoDeBajaDiceInactivo()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var pendiente = await UnaAsync(clienteId, fletero.Id, importePagado: 30_000m);
        var pagada = await UnaAsync(clienteId, fletero.Id, EstadoLiquidacion.Pagada);
        var anulada = await UnaAsync(clienteId, fletero.Id, EstadoLiquidacion.Anulada);

        await app.EnLaBaseAsync(contexto => contexto.Transportistas
            .Where(transportista => transportista.Id == fletero.Id)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(transportista => transportista.Activo, false)));

        var items = (await ListarAsync($"transportistaId={fletero.Id}")).Items;

        Assert.Equal(70_000m, items.Single(item => item.Id == pendiente.Id).RestaPagar);
        Assert.Equal(0m, items.Single(item => item.Id == pagada.Id).RestaPagar);

        var laAnulada = items.Single(item => item.Id == anulada.Id);
        Assert.Null(laAnulada.RestaPagar);
        Assert.NotNull(laAnulada.MotivoAnulacion);

        Assert.All(items, item => Assert.False(item.Transportista.Activo));
        Assert.All(items, item => Assert.Equal($"{item.Mes:D2}/{item.Anio}", "07/2026"));
    }

    private async Task<Liquidacion> UnaAsync(
        int clienteId,
        int transportistaId,
        EstadoLiquidacion estado = EstadoLiquidacion.Pendiente,
        int mes = DatosDePruebaLiquidaciones.Mes,
        int anio = DatosDePruebaLiquidaciones.Anio,
        decimal importePagado = 0m)
    {
        var viaje = await app.ViajeDeAsync(clienteId, transportistaId, 100_000m);

        return await app.CrearLiquidacionAsync(transportistaId, [viaje], estado, importePagado, mes: mes, anio: anio);
    }

    private async Task<PaginaDeLiquidacionesLeida> ListarAsync(string query)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        return (await cliente.GetFromJsonAsync<PaginaDeLiquidacionesLeida>($"/api/liquidaciones?{query}"))!;
    }
}
