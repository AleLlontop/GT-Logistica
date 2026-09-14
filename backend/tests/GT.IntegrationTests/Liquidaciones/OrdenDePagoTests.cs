using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>Registrar órdenes de pago (User Story 4, FR-036 a FR-044).</summary>
public class OrdenDePagoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateOnly Hoy = FechaHoyArgentina.Hoy();

    /// <summary>FR-040: sin <c>confirmado</c> no registra nada y devuelve los importes del servidor.</summary>
    [Fact]
    public async Task Sin_Confirmado_Responde409ConLosImportes_YNoRegistraNada()
    {
        var liquidacion = await LiquidacionDe355MilAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 200_000m, confirmado: false);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var confirmacion = (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal("pago_requiere_confirmacion", confirmacion.Codigo);
        Assert.Equal(200_000m, confirmacion.Importe);
        Assert.Equal(355_000m, confirmacion.RestaPagarAntes);
        Assert.Equal(155_000m, confirmacion.RestaPagarDespues);
        Assert.False(confirmacion.QuedaPagada);

        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.Equal(0m, releida.ImportePagado);
        Assert.Empty(releida.OrdenesDePago);
    }

    [Fact]
    public async Task Con_Confirmado_RegistraLaOrden_YUnPagoParcialNoAgregaHistorial()
    {
        var liquidacion = await LiquidacionDe355MilAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 200_000m);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;
        var orden = Assert.Single(detalle.OrdenesDePago);

        Assert.StartsWith("OP-", orden.Numero);
        Assert.Equal("admin", orden.RegistradaPor);
        Assert.Equal(200_000m, orden.Importe);
        Assert.Equal("pendiente", detalle.Estado);
        Assert.Equal(155_000m, detalle.RestaPagar);
        Assert.Equal(["generacion"], detalle.Historial.Select(entrada => entrada.Operacion));
    }

    [Fact]
    public async Task El_PagoExacto_DejaPagada_ConUnaSolaEntradaPagada()
    {
        var liquidacion = await LiquidacionDe355MilAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        await PagarAsync(cliente, liquidacion.Id, Hoy, 200_000m);
        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 155_000m);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;

        Assert.Equal("pagada", detalle.Estado);
        Assert.Equal(0m, detalle.RestaPagar);
        Assert.Equal(["generacion", "pagada"], detalle.Historial.Select(entrada => entrada.Operacion));
        Assert.Equal("admin", detalle.Historial[1].Usuario);
    }

    /// <summary>FR-038: los dos límites se aceptan y un día más allá de cada uno se rechaza con el rango.</summary>
    [Fact]
    public async Task La_FechaDePago_AceptaLosDosLimites_YRechazaUnDiaAfuera()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje], generadaEn: DateTime.UtcNow.AddDays(-3));
        var generacion = Hoy.AddDays(-3);

        var cliente = await app.CrearClienteAutenticadoAsync();

        foreach (var fuera in new[] { generacion.AddDays(-1), Hoy.AddDays(1) })
        {
            var rechazo = await PagarAsync(cliente, liquidacion.Id, fuera, 10_000m);
            Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);

            var error = (await rechazo.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
            Assert.Equal("fecha_de_pago_fuera_de_rango", error.Codigo);
            Assert.Equal(generacion.ToString("yyyy-MM-dd"), error.Desde);
            Assert.Equal(Hoy.ToString("yyyy-MM-dd"), error.Hasta);
        }

        Assert.Equal(HttpStatusCode.Created, (await PagarAsync(cliente, liquidacion.Id, generacion, 10_000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PagarAsync(cliente, liquidacion.Id, Hoy, 10_000m)).StatusCode);
    }

    [Fact]
    public async Task Un_ImporteEnCero_SeRechaza()
    {
        var liquidacion = await LiquidacionDe355MilAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 0m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal("datos_invalidos", error.Codigo);
        Assert.Equal("importe", error.Campo);
    }

    [Fact]
    public async Task Un_ImporteMayorAlSaldo_SeRechazaDiciendoCuantoResta()
    {
        var liquidacion = await LiquidacionDe355MilAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 400_000m);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
        Assert.Equal("importe_supera_saldo", error.Codigo);
        Assert.Equal(355_000m, error.RestaPagar);
        Assert.Contains("$ 355.000,00", error.Mensaje, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EstadoLiquidacion.Pagada)]
    [InlineData(EstadoLiquidacion.Anulada)]
    public async Task Una_PagadaOAnulada_NoAdmiteOrdenes(EstadoLiquidacion estado)
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje], estado);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await PagarAsync(cliente, liquidacion.Id, Hoy, 1m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(
            "liquidacion_no_pagable",
            (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
    }

    /// <summary>FR-020: pagar no cambia el estado de los viajes ni su factura.</summary>
    [Fact]
    public async Task Pagar_NoTocaLosViajesNiLaFactura()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var factura = await app.CrearFacturaAsync(clienteId);
        await app.AsociarViajesAsync(factura.Id, viaje.Id);

        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);
        var cliente = await app.CrearClienteAutenticadoAsync();

        await PagarAsync(cliente, liquidacion.Id, Hoy, 100_000m);

        var despues = (await app.RecargarViajeAsync(viaje.Id))!;
        Assert.Equal(EstadoViaje.Facturado, despues.Estado);
        Assert.Equal(factura.Id, despues.FacturaId);
    }

    private async Task<Liquidacion> LiquidacionDe355MilAsync()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        return await app.CrearLiquidacionAsync(
            fletero.Id,
            [
                await app.ViajeDeAsync(clienteId, fletero.Id, 120_000m),
                await app.ViajeDeAsync(clienteId, fletero.Id, 95_000m),
                await app.ViajeDeAsync(clienteId, fletero.Id, 140_000m),
            ]);
    }

    private static Task<HttpResponseMessage> PagarAsync(
        HttpClient cliente,
        int id,
        DateOnly fecha,
        decimal importe,
        bool confirmado = true) =>
        cliente.PostAsJsonAsync(
            $"/api/liquidaciones/{id}/ordenes-de-pago",
            new { fechaPago = fecha.ToString("yyyy-MM-dd"), importe, confirmado });
}
