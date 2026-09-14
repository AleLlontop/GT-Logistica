using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Los tres datos guardados que repiten a otros, contra sus fuentes (research §1, §2, convención [003]):
/// <c>ImporteTotal</c> contra la suma de los viajes, <c>ImportePagado</c> contra la suma de las órdenes, y
/// <c>Vigente</c> contra el estado. Y la regla del estado tras un pago, escrita en C# y en el <c>CASE</c>
/// del <c>UPDATE</c>, sobre el mismo dato.
///
/// Todo pasa por la API: lo que se verifica es lo que dejaron las cuatro operaciones reales.
/// </summary>
public class CoherenciaDeLiquidacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly string Hoy = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd");

    [Fact]
    public async Task Despues_DeGenerarEditarPagarYAnular_LosAgregadosCoincidenConSusFuentes()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var v = new List<int>();
        foreach (var importe in new[] { 120_000m, 95_000m, 140_000m, 80_000m, 30_000m, 45_000m, 10_000m })
        {
            v.Add((await app.ViajeDeAsync(clienteId, fletero.Id, importe)).Id);
        }

        // Una generada, editada y pagada en parte.
        var editada = await GenerarAsync(cliente, fletero.Id, [v[0], v[1], v[2]]);
        var trasEditar = await LeerOkAsync(await cliente.PutAsJsonAsync(
            $"/api/liquidaciones/{editada.Id}",
            new { viajeIds = new[] { v[0], v[2], v[3] }, version = editada.Version }));

        await PagarYCompararAsync(cliente, trasEditar, 100_000m);

        // Una pagada entera en dos órdenes.
        var pagada = await GenerarAsync(cliente, fletero.Id, [v[4], v[5]]);
        var trasPrimerPago = await PagarYCompararAsync(cliente, pagada, 25_000m);
        await PagarYCompararAsync(cliente, trasPrimerPago, 50_000m);

        // Una anulada.
        var anulada = await GenerarAsync(cliente, fletero.Id, [v[6]]);
        Assert.Equal(
            HttpStatusCode.OK,
            (await cliente.PostAsJsonAsync(
                $"/api/liquidaciones/{anulada.Id}/anulacion",
                new { motivo = "Generada por error.", confirmado = true })).StatusCode);

        await app.EnLaBaseAsync(async contexto =>
        {
            var liquidaciones = await contexto.Liquidaciones
                .Where(liquidacion => liquidacion.TransportistaId == fletero.Id)
                .Include(liquidacion => liquidacion.Viajes).ThenInclude(vinculo => vinculo.Viaje)
                .Include(liquidacion => liquidacion.OrdenesDePago)
                .AsSplitQuery()
                .AsNoTracking()
                .ToListAsync();

            Assert.Equal(3, liquidaciones.Count);

            foreach (var liquidacion in liquidaciones)
            {
                var anuladaLa = liquidacion.Estado is EstadoLiquidacion.Anulada;

                Assert.Equal(
                    liquidacion.Viajes.Where(vinculo => anuladaLa || vinculo.Vigente).Sum(vinculo => vinculo.Viaje!.Importe),
                    liquidacion.ImporteTotal);

                Assert.Equal(liquidacion.OrdenesDePago.Sum(orden => orden.Importe), liquidacion.ImportePagado);

                Assert.All(liquidacion.Viajes, vinculo => Assert.Equal(!anuladaLa, vinculo.Vigente));
            }

            Assert.Contains(liquidaciones, liquidacion => liquidacion.Estado is EstadoLiquidacion.Pagada);
        });
    }

    private async Task<LiquidacionDetalleLeida> GenerarAsync(HttpClient cliente, int transportistaId, int[] viajeIds)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(transportistaId, viajeIds));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        return (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;
    }

    /// <summary>
    /// Paga y compara el estado que dejó el <c>CASE</c> del <c>UPDATE</c> con el que calcula
    /// <see cref="ReglasDeLiquidacion.EstadoTrasPago"/> sobre los mismos importes.
    /// </summary>
    private async Task<LiquidacionDetalleLeida> PagarYCompararAsync(
        HttpClient cliente,
        LiquidacionDetalleLeida antes,
        decimal importe)
    {
        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/liquidaciones/{antes.Id}/ordenes-de-pago",
            new { fechaPago = Hoy, importe, confirmado = true });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var despues = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;

        var esperado = ReglasDeLiquidacion.EstadoTrasPago(antes.ImporteTotal, antes.ImportePagado, importe);
        var guardado = await app.ConAlcanceAsync(contexto => contexto.Liquidaciones
            .Where(liquidacion => liquidacion.Id == antes.Id)
            .Select(liquidacion => liquidacion.Estado)
            .SingleAsync());

        Assert.Equal(esperado, guardado);

        return despues;
    }

    private static async Task<LiquidacionDetalleLeida> LeerOkAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        return (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;
    }
}
