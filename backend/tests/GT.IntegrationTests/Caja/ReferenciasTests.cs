using System.Net;
using System.Net.Http.Json;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// Los dos desplegables de referencia y lo que el guardado vuelve a evaluar (FR-011, research §4, §5).
/// </summary>
public class ReferenciasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Facturas_Pendientes_TraeSoloLasPendientes_ConElTextoArmado()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var pendiente = await app.FacturaAsync(EstadoFactura.Pendiente);
        var pagada = await app.FacturaAsync(EstadoFactura.Pagada);
        var anulada = await app.FacturaAsync(EstadoFactura.Anulada);

        var opciones = (await cliente.GetFromJsonAsync<List<OpcionLeida>>("/api/caja/facturas-pendientes"))!;

        var opcion = Assert.Single(opciones, o => o.Id == pendiente.Id);
        Assert.Equal($"Factura {pendiente.NumeroComprobante} · {pendiente.ClienteRazonSocial}", opcion.Texto);
        Assert.DoesNotContain(opciones, o => o.Id == pagada.Id);
        Assert.DoesNotContain(opciones, o => o.Id == anulada.Id);
    }

    [Fact]
    public async Task Ordenes_DePago_TraeLas50MasRecientes_YLaMasViejaSeAceptaIgualAlGuardar()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var ordenes = await app.OrdenesDePagoAsync(51);

        var opciones = (await cliente.GetFromJsonAsync<List<OpcionLeida>>("/api/caja/ordenes-de-pago"))!;

        Assert.Equal(50, opciones.Count);

        var masVieja = ordenes[0];
        Assert.DoesNotContain(opciones, o => o.Id == masVieja.Id);
        Assert.Contains(opciones, o => o.Id == ordenes[^1].Id);

        // Por número descendente: la primera es la más reciente.
        Assert.Equal(ordenes[^1].Id, opciones[0].Id);
        Assert.StartsWith(NumerosVisibles.OrdenDePago(ordenes[^1].Numero), opciones[0].Texto);

        // El tope es del desplegable, no del guardado (research §4).
        var cajaId = (await (await cliente.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;
        var respuesta = await cliente.RegistrarMovimientoAsync(cajaId, "egreso", ordenDePagoId: masVieja.Id);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Una_FacturaCobradaDespuesDeListada_SeRechazaAlGuardar()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var factura = await app.FacturaAsync();
        var cajaId = (await (await cliente.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;

        var antes = (await cliente.GetFromJsonAsync<List<OpcionLeida>>("/api/caja/facturas-pendientes"))!;
        Assert.Contains(antes, o => o.Id == factura.Id);

        await app.MarcarFacturaPagadaAsync(factura.Id);

        var respuesta = await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", facturaId: factura.Id);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("referencia_invalida", error.Codigo);
        Assert.Equal("La factura elegida ya no está pendiente de cobro.", error.Mensaje);
        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }
}
