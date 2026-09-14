using System.Net;
using System.Net.Http.Json;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>Editar los viajes de una liquidación (User Story 5, FR-045 a FR-052).</summary>
public class EdicionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Quitar_YAgregar_RecalculaSubeLaVersion_YRegistraExactamenteLosViajes()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var a = await app.ViajeDeAsync(clienteId, fletero.Id, 120_000m);
        var b = await app.ViajeDeAsync(clienteId, fletero.Id, 95_000m);
        var c = await app.ViajeDeAsync(clienteId, fletero.Id, 140_000m);
        var j = await app.ViajeDeAsync(clienteId, fletero.Id, 80_000m);

        var factura = await app.CrearFacturaAsync(clienteId);
        await app.AsociarViajesAsync(factura.Id, c.Id);

        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [a, b, c]);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var antes = await LeerAsync(cliente, liquidacion.Id);

        var respuesta = await EditarAsync(cliente, liquidacion.Id, [a.Id, c.Id, j.Id], antes.Version);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;

        Assert.Equal(340_000m, despues.ImporteTotal);
        Assert.Equal(antes.Version + 1, despues.Version);
        Assert.Equal([a.Id, c.Id, j.Id], despues.Viajes.Select(viaje => viaje.Id).Order());

        // FR-052: número, fecha de generación, estado, transportista y período no cambian.
        Assert.Equal(antes.Numero, despues.Numero);
        Assert.Equal(antes.FechaGeneracion, despues.FechaGeneracion);
        Assert.Equal(antes.Estado, despues.Estado);
        Assert.Equal(antes.Transportista.Id, despues.Transportista.Id);
        Assert.Equal((antes.Mes, antes.Anio), (despues.Mes, despues.Anio));

        var edicion = despues.Historial.Single(entrada => entrada.Operacion == "edicion");
        Assert.Equal([b.Numero], edicion.ViajesQuitados);
        Assert.Equal([j.Numero], edicion.ViajesAgregados);

        // Sólo las ediciones tienen filas de viajes; el vínculo quitado se borró.
        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.All(
            releida.Cambios.Where(cambio => cambio.Operacion != OperacionDeLiquidacion.Edicion),
            cambio => Assert.Empty(cambio.Viajes));
        Assert.DoesNotContain(releida.Viajes, vinculo => vinculo.ViajeId == b.Id);

        // FR-050: el quitado vuelve a ofrecerse.
        var disponibles = await cliente.GetFromJsonAsync<List<ViajeDisponibleLeido>>(
            DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id));
        Assert.Contains(disponibles!, viaje => viaje.Id == b.Id);

        // FR-020: los viajes quitados y agregados conservan su estado y su factura.
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(b.Id))!.Estado);
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(j.Id))!.Estado);
        Assert.Equal(factura.Id, (await app.RecargarViajeAsync(c.Id))!.FacturaId);
    }

    [Fact]
    public async Task Un_ConjuntoIgualAlActual_NoEscribeNada()
    {
        var (liquidacion, viajes, cliente) = await LiquidacionDeDosViajesAsync();

        var respuesta = await EditarAsync(cliente, liquidacion.Id, [.. viajes.Select(viaje => viaje.Id)], 0);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.Equal(0, releida.Version);
        Assert.Single(releida.Cambios);
    }

    [Fact]
    public async Task Una_VersionDistinta_Responde409Modificada()
    {
        var (liquidacion, viajes, cliente) = await LiquidacionDeDosViajesAsync();

        var respuesta = await EditarAsync(cliente, liquidacion.Id, [viajes[0].Id], versionAbierta: 7);

        await Assert409Async(respuesta, "liquidacion_modificada");
    }

    [Theory]
    [InlineData(EstadoLiquidacion.Pagada, 0, "pagada")]
    [InlineData(EstadoLiquidacion.Anulada, 0, "anulada")]
    [InlineData(EstadoLiquidacion.Pendiente, 50_000, "conOrdenesDePago")]
    public async Task No_SeEditaUnaPagadaAnuladaOConPagos(EstadoLiquidacion estado, int pagado, string motivo)
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var otro = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje], estado, pagado);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await EditarAsync(cliente, liquidacion.Id, [viaje.Id, otro.Id], 0);

        var error = await Assert409Async(respuesta, "liquidacion_no_editable");
        Assert.Equal(motivo, error.Motivo);
    }

    /// <summary>FR-045: dado de baja, o con el CUIT de la empresa emisora, ya no se liquida.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task No_SeEditaSiElTransportistaYaNoSePuedeLiquidar(bool porBaja)
    {
        var (liquidacion, viajes, cliente) = await LiquidacionDeDosViajesAsync();
        var transportistaId = liquidacion.TransportistaId;

        await app.EnLaBaseAsync(contexto => porBaja
            ? contexto.Transportistas
                .Where(transportista => transportista.Id == transportistaId)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(transportista => transportista.Activo, false))
            : contexto.Transportistas
                .Where(transportista => transportista.Id == transportistaId)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(
                    transportista => transportista.Cuit,
                    DatosDePruebaLiquidaciones.CuitEmpresaEmisora)));

        try
        {
            var respuesta = await EditarAsync(cliente, liquidacion.Id, [viajes[0].Id], 0);

            var error = await Assert409Async(respuesta, "liquidacion_no_editable");
            Assert.Equal("transportistaNoLiquidable", error.Motivo);
        }
        finally
        {
            // El CUIT es único en el padrón: se devuelve para no chocar con otro test de la clase.
            if (!porBaja)
            {
                await app.EnLaBaseAsync(contexto => contexto.Transportistas
                    .Where(transportista => transportista.Id == transportistaId)
                    .ExecuteUpdateAsync(cambio => cambio.SetProperty(transportista => transportista.Cuit, "30000000019")));
            }
        }
    }

    [Fact]
    public async Task Sin_Viajes_OConTotalEnCero_SeRechaza()
    {
        var (liquidacion, _, cliente) = await LiquidacionDeDosViajesAsync();
        var enCero = await app.ViajeDeAsync((await app.CrearClienteAsync()).Id, liquidacion.TransportistaId, 0m);

        var sinViajes = await EditarAsync(cliente, liquidacion.Id, [], 0);
        Assert.Equal(HttpStatusCode.BadRequest, sinViajes.StatusCode);
        Assert.Equal("sin_viajes", (await sinViajes.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);

        var enCeroRespuesta = await EditarAsync(cliente, liquidacion.Id, [enCero.Id], 0);
        Assert.Equal(HttpStatusCode.BadRequest, enCeroRespuesta.StatusCode);
        Assert.Equal("total_en_cero", (await enCeroRespuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
    }

    [Fact]
    public async Task Un_AgregadoQueYaEstaEnOtraLiquidacion_Responde409()
    {
        var (liquidacion, viajes, cliente) = await LiquidacionDeDosViajesAsync();
        var ajeno = await app.ViajeDeAsync((await app.CrearClienteAsync()).Id, liquidacion.TransportistaId);
        var otra = await app.CrearLiquidacionAsync(liquidacion.TransportistaId, [ajeno]);

        var respuesta = await EditarAsync(cliente, liquidacion.Id, [viajes[0].Id, ajeno.Id], 0);

        var error = await Assert409Async(respuesta, "viaje_ya_liquidado");
        Assert.Equal($"LQ-{otra.Numero}", Assert.Single(error.Viajes!).Liquidacion);
    }

    private async Task<(Liquidacion Liquidacion, Viaje[] Viajes, HttpClient Cliente)> LiquidacionDeDosViajesAsync()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        Viaje[] viajes =
        [
            await app.ViajeDeAsync(clienteId, fletero.Id, 60_000m),
            await app.ViajeDeAsync(clienteId, fletero.Id, 40_000m),
        ];

        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, viajes);

        return (liquidacion, viajes, await app.CrearClienteAutenticadoAsync());
    }

    private static async Task<LiquidacionDetalleLeida> LeerAsync(HttpClient cliente, int id) =>
        (await cliente.GetFromJsonAsync<LiquidacionDetalleLeida>($"/api/liquidaciones/{id}"))!;

    private static Task<HttpResponseMessage> EditarAsync(HttpClient cliente, int id, int[] viajeIds, int versionAbierta) =>
        cliente.PutAsJsonAsync($"/api/liquidaciones/{id}", new { viajeIds, version = versionAbierta });

    private static async Task<ErrorLiquidacionLeido> Assert409Async(HttpResponseMessage respuesta, string codigo)
    {
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal(codigo, error.Codigo);

        return error;
    }
}
