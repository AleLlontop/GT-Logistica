using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>Generar la liquidación de un fletero para un período (User Story 1, FR-011 a FR-020).</summary>
public class GeneracionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Genera_PendienteConElTotalIgualALaSuma_YLosViajesDejanDeOfrecerse()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var a = await app.ViajeDeAsync(clienteId, fletero.Id, 120_000m);
        var b = await app.ViajeDeAsync(clienteId, fletero.Id, 95_000m);
        var c = await app.ViajeDeAsync(clienteId, fletero.Id, 140_000m);

        // C ya facturado al cliente: se liquida igual y su factura no se entera (FR-004, FR-020).
        var factura = await app.CrearFacturaAsync(clienteId);
        await app.AsociarViajesAsync(factura.Id, c.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(fletero.Id, [a.Id, b.Id, c.Id]));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<LiquidacionDetalleLeida>())!;

        Assert.Equal($"/api/liquidaciones/{detalle.Id}", respuesta.Headers.Location!.ToString());
        Assert.Equal(355_000m, detalle.ImporteTotal);
        Assert.Equal(0m, detalle.ImportePagado);
        Assert.Equal(355_000m, detalle.RestaPagar);
        Assert.Equal("pendiente", detalle.Estado);
        Assert.StartsWith("LQ-", detalle.Numero);
        Assert.Empty(detalle.OrdenesDePago);
        Assert.Equal(3, detalle.Viajes.Count);
        Assert.Equal("generacion", Assert.Single(detalle.Historial).Operacion);

        var disponibles = await cliente.GetFromJsonAsync<List<ViajeDisponibleLeido>>(
            DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id));

        Assert.Empty(disponibles!);

        // FR-020: los viajes siguen exactamente como estaban.
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(a.Id))!.Estado);

        var cDespues = (await app.RecargarViajeAsync(c.Id))!;
        Assert.Equal(EstadoViaje.Facturado, cDespues.Estado);
        Assert.Equal(factura.Id, cDespues.FacturaId);
        Assert.Equal(140_000m, cDespues.Importe);
    }

    /// <summary>FR-008: la lista enviada manda; los que quedan afuera siguen disponibles.</summary>
    [Fact]
    public async Task Una_ListaParcial_SeAcepta_YLosDemasSiguenDisponibles()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var incluido = await app.ViajeDeAsync(clienteId, fletero.Id);
        var afuera = await app.ViajeDeAsync(clienteId, fletero.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(fletero.Id, [incluido.Id]));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var disponibles = await cliente.GetFromJsonAsync<List<ViajeDisponibleLeido>>(
            DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id));

        Assert.Equal(afuera.Id, Assert.Single(disponibles!).Id);
    }

    [Fact]
    public async Task Sin_Viajes_SeRechaza()
    {
        var (_, fletero) = await app.EscenarioBaseAsync();

        var error = await RechazoAsync(fletero.Id, [], HttpStatusCode.BadRequest);

        Assert.Equal("sin_viajes", error.Codigo);
    }

    [Fact]
    public async Task Con_TodosLosViajesEnCero_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var enCero = await app.ViajeDeAsync(clienteId, fletero.Id, 0m);

        var error = await RechazoAsync(fletero.Id, [enCero.Id], HttpStatusCode.BadRequest);

        Assert.Equal("total_en_cero", error.Codigo);
    }

    [Fact]
    public async Task Un_ViajeQueNoEstaRendido_SeRechazaNombrandolo()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var bueno = await app.ViajeDeAsync(clienteId, fletero.Id);
        var enCurso = await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.EnCurso);

        var error = await RechazoAsync(fletero.Id, [bueno.Id, enCurso.Id], HttpStatusCode.BadRequest);

        Assert.Equal("viaje_no_liquidable", error.Codigo);
        var viaje = Assert.Single(error.Viajes!);
        Assert.Equal(enCurso.Id, viaje.Id);
        Assert.Equal("noRendidoNiFacturado", viaje.Motivo);
        Assert.Contains($"#{enCurso.Numero}", error.Mensaje, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_ViajeDeOtroTransportista_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var otro = await app.TransportistaExternoAsync("Fletes del Sur S.R.L.");
        var ajeno = await app.ViajeDeAsync(clienteId, otro.Id);

        var error = await RechazoAsync(fletero.Id, [ajeno.Id], HttpStatusCode.BadRequest);

        Assert.Equal("deOtroTransportista", Assert.Single(error.Viajes!).Motivo);
    }

    [Fact]
    public async Task Un_ViajeDeOtroPeriodo_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var deJunio = await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 6, 15));

        var error = await RechazoAsync(fletero.Id, [deJunio.Id], HttpStatusCode.BadRequest);

        Assert.Equal("deOtroPeriodo", Assert.Single(error.Viajes!).Motivo);
    }

    [Fact]
    public async Task El_TransportistaPropio_SeRechaza()
    {
        var (clienteId, _) = await app.EscenarioBaseAsync();
        var propio = await app.TransportistaPropioAsync();
        var viaje = await app.ViajeDeAsync(clienteId, propio.Id);

        var error = await RechazoAsync(propio.Id, [viaje.Id], HttpStatusCode.BadRequest);

        Assert.Equal("transportista_no_liquidable", error.Codigo);
    }

    [Fact]
    public async Task Un_PeriodoInvalido_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);

        var error = await RechazoAsync(fletero.Id, [viaje.Id], HttpStatusCode.BadRequest, mes: 13);

        Assert.Equal("periodo_invalido", error.Codigo);
    }

    [Fact]
    public async Task Sin_EmpresaEmisora_SeRechaza()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);
        await app.SinEmpresaEmisoraAsync();

        var error = await RechazoAsync(fletero.Id, [viaje.Id], HttpStatusCode.BadRequest);

        Assert.Equal("empresa_emisora_no_configurada", error.Codigo);
    }

    [Fact]
    public async Task Un_ViajeYaLiquidado_Responde409NombrandoLaLiquidacionQueLoTiene()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);
        var otro = await app.ViajeDeAsync(clienteId, fletero.Id);

        var existente = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);

        var error = await RechazoAsync(fletero.Id, [viaje.Id, otro.Id], HttpStatusCode.Conflict, esperadas: 1);

        Assert.Equal("viaje_ya_liquidado", error.Codigo);
        var enConflicto = Assert.Single(error.Viajes!);
        Assert.Equal(viaje.Id, enConflicto.Id);
        Assert.Equal($"LQ-{existente.Numero}", enConflicto.Liquidacion);
        Assert.Contains($"LQ-{existente.Numero}", error.Mensaje, StringComparison.Ordinal);
    }

    /// <summary>Todo rechazo deja la base exactamente como estaba: ninguna liquidación nueva.</summary>
    private async Task<ErrorLiquidacionLeido> RechazoAsync(
        int transportistaId,
        int[] viajeIds,
        HttpStatusCode esperado,
        int mes = DatosDePruebaLiquidaciones.Mes,
        int esperadas = 0)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(transportistaId, viajeIds, mes));

        Assert.Equal(esperado, respuesta.StatusCode);
        Assert.Equal(esperadas, await app.ContarLiquidacionesDeAsync(transportistaId));

        return (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
    }
}
