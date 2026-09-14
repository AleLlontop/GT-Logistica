using System.Net;
using System.Net.Http.Json;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>Anular una liquidación (User Story 6, FR-053 a FR-060).</summary>
public class AnulacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Valida_EnOrden_Existencia_Estado_Motivo_YConfirmacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await AnularAsync(cliente, 999_999, "Motivo.", confirmado: true)).StatusCode);

        // El estado antes que el motivo: una pagada sin motivo dice que está pagada.
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var pagada = await app.CrearLiquidacionAsync(
            fletero.Id,
            [await app.ViajeDeAsync(clienteId, fletero.Id)],
            EstadoLiquidacion.Pagada);

        var noAnulable = await AnularAsync(cliente, pagada.Id, "", confirmado: false);
        Assert.Equal(HttpStatusCode.Conflict, noAnulable.StatusCode);

        var errorEstado = (await noAnulable.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal("liquidacion_no_anulable", errorEstado.Codigo);
        Assert.Equal("pagada", errorEstado.Motivo);

        // El motivo antes que la confirmación.
        var pendiente = await app.CrearLiquidacionAsync(fletero.Id, [await app.ViajeDeAsync(clienteId, fletero.Id)]);

        var sinMotivo = await AnularAsync(cliente, pendiente.Id, "   ", confirmado: false);
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        Assert.Equal("motivo_requerido", (await sinMotivo.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
    }

    [Fact]
    public async Task Sin_Confirmado_Responde409_YNoCambiaNada()
    {
        var (liquidacion, _, cliente, _) = await EscenarioAsync();

        var respuesta = await AnularAsync(cliente, liquidacion.Id, "Período equivocado.", confirmado: false);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "anulacion_requiere_confirmacion",
            (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);

        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.Equal(EstadoLiquidacion.Pendiente, releida.Estado);
        Assert.All(releida.Viajes, vinculo => Assert.True(vinculo.Vigente));
        Assert.Single(releida.Cambios);
    }

    [Fact]
    public async Task Con_Confirmado_Anula_LiberaLosViajes_YSePuedenLiquidarDeNuevo()
    {
        var (liquidacion, viajes, cliente, factura) = await EscenarioAsync();

        var respuesta = await AnularAsync(cliente, liquidacion.Id, "  El período no correspondía.  ", confirmado: true);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;
        Assert.Equal("anulada", detalle.Estado);
        Assert.Equal("El período no correspondía.", detalle.MotivoAnulacion);
        Assert.Equal(3, detalle.Viajes.Count);
        Assert.Equal(["generacion", "anulacion"], detalle.Historial.Select(entrada => entrada.Operacion));

        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.All(releida.Viajes, vinculo => Assert.False(vinculo.Vigente));

        var disponibles = await cliente.GetFromJsonAsync<List<ViajeDisponibleLeido>>(
            DatosDePruebaLiquidaciones.RutaDeDisponibles(liquidacion.TransportistaId));
        Assert.Equal(viajes.Select(viaje => viaje.Id).Order(), disponibles!.Select(viaje => viaje.Id).Order());

        var nueva = await cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(liquidacion.TransportistaId, viajes.Select(viaje => viaje.Id)));

        Assert.Equal(HttpStatusCode.Created, nueva.StatusCode);
        Assert.NotEqual(detalle.Numero, (await nueva.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!.Numero);

        // FR-020: el viaje facturado sigue facturado con su factura.
        var facturado = (await app.RecargarViajeAsync(viajes[2].Id))!;
        Assert.Equal(EstadoViaje.Facturado, facturado.Estado);
        Assert.Equal(factura, facturado.FacturaId);
    }

    [Fact]
    public async Task Con_OrdenesDePago_SeRechazaDiciendoCuantasYPorCuanto()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var liquidacion = await app.CrearLiquidacionAsync(
            fletero.Id,
            [await app.ViajeDeAsync(clienteId, fletero.Id, 355_000m)],
            importePagado: 200_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await AnularAsync(cliente, liquidacion.Id, "Motivo.", confirmado: true);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal("conOrdenesDePago", error.Motivo);
        Assert.Equal(1, error.CantidadOrdenesDePago);
        Assert.Equal(200_000m, error.ImportePagado);
        Assert.Contains("1 orden de pago por $ 200.000,00", error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Una_YaAnulada_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var liquidacion = await app.CrearLiquidacionAsync(
            fletero.Id,
            [await app.ViajeDeAsync(clienteId, fletero.Id)],
            EstadoLiquidacion.Anulada);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await AnularAsync(cliente, liquidacion.Id, "Otra vez.", confirmado: true);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("anulada", (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Motivo);
    }

    private async Task<(Liquidacion Liquidacion, Viaje[] Viajes, HttpClient Cliente, int FacturaId)> EscenarioAsync()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        Viaje[] viajes =
        [
            await app.ViajeDeAsync(clienteId, fletero.Id, 120_000m),
            await app.ViajeDeAsync(clienteId, fletero.Id, 95_000m),
            await app.ViajeDeAsync(clienteId, fletero.Id, 140_000m),
        ];

        var factura = await app.CrearFacturaAsync(clienteId);
        await app.AsociarViajesAsync(factura.Id, viajes[2].Id);

        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, viajes);

        return (liquidacion, viajes, await app.CrearClienteAutenticadoAsync(), factura.Id);
    }

    private static Task<HttpResponseMessage> AnularAsync(HttpClient cliente, int id, string motivo, bool confirmado) =>
        cliente.PostAsJsonAsync($"/api/liquidaciones/{id}/anulacion", new { motivo, confirmado });
}
