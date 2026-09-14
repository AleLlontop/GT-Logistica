using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>Qué viajes se ofrecen para liquidar (FR-004, FR-005, SC-003).</summary>
public class DisponiblesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Trae_RendidosYFacturados_YNingunoDeLosOtrosEstados()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var rendido = await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.Rendido);
        var facturado = await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.Facturado);
        await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.EnCurso);
        await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.Pendiente);
        await app.ViajeDeAsync(clienteId, fletero.Id, estado: EstadoViaje.Anulado);

        var disponibles = await LeerAsync(fletero.Id);

        Assert.Equal([rendido.Id, facturado.Id], disponibles.Select(viaje => viaje.Id).Order());
        Assert.Equal("rendido", disponibles.Single(viaje => viaje.Id == rendido.Id).Estado);
        Assert.Equal("facturado", disponibles.Single(viaje => viaje.Id == facturado.Id).Estado);
    }

    [Fact]
    public async Task Filtra_PorElMesYElAnioDeLaFechaDelViaje()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var deJulio = await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 7, 31));
        await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 6, 15));
        await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2025, 7, 15));

        var disponibles = await LeerAsync(fletero.Id);

        Assert.Equal(deJulio.Id, Assert.Single(disponibles).Id);
    }

    /// <summary>
    /// FR-005: el transportista del viaje es el que quedó registrado al asignarlo, aunque el chofer
    /// pertenezca hoy a otro transportista.
    /// </summary>
    [Fact]
    public async Task Usa_ElTransportistaRegistradoEnElViaje_YNoElDelChofer()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var otro = await app.TransportistaExternoAsync("Fletes del Sur S.R.L.");

        var chofer = await app.CrearChoferCompletoAsync(DatosDePruebaViajes.SemillaUnica(), transportistaId: otro.Id);

        var viaje = await app.CrearViajeAsync(
            clienteId,
            DatosDePruebaLiquidaciones.FechaDelPeriodo,
            EstadoViaje.Rendido,
            importe: 50_000m,
            choferId: chofer.Id,
            transportistaId: fletero.Id);

        Assert.Contains(await LeerAsync(fletero.Id), disponible => disponible.Id == viaje.Id);
        Assert.DoesNotContain(await LeerAsync(otro.Id), disponible => disponible.Id == viaje.Id);
    }

    [Fact]
    public async Task Excluye_LosDeUnaLiquidacionVigente_EIncluyeLosDeUnaAnulada()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var liquidado = await app.ViajeDeAsync(clienteId, fletero.Id);
        var liberado = await app.ViajeDeAsync(clienteId, fletero.Id);
        var libre = await app.ViajeDeAsync(clienteId, fletero.Id);

        await app.CrearLiquidacionAsync(fletero.Id, [liquidado]);
        await app.CrearLiquidacionAsync(fletero.Id, [liberado], EstadoLiquidacion.Anulada);

        var disponibles = await LeerAsync(fletero.Id);

        Assert.Equal([liberado.Id, libre.Id], disponibles.Select(viaje => viaje.Id).Order());
    }

    [Fact]
    public async Task Ordena_PorFechaYNumero()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var del20 = await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 7, 20));
        var del5 = await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 7, 5));
        var otroDel5 = await app.ViajeDeAsync(clienteId, fletero.Id, fecha: new DateOnly(2026, 7, 5));

        var disponibles = await LeerAsync(fletero.Id);

        Assert.Equal([del5.Id, otroDel5.Id, del20.Id], disponibles.Select(viaje => viaje.Id));
    }

    [Theory]
    [InlineData(13, 2026)]
    [InlineData(7, 2024)]
    public async Task Rechaza_UnPeriodoFueraDeLasOpciones(int mes, int anio)
    {
        var (_, fletero) = await app.EscenarioBaseAsync();

        var error = await RechazoAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id, mes, anio));

        Assert.Equal("periodo_invalido", error.Codigo);
    }

    /// <summary>El tope es el año en curso, leído del reloj del servidor: el siguiente todavía no se ofrece.</summary>
    [Fact]
    public async Task Rechaza_ElAnioSiguienteAlEnCurso()
    {
        var (_, fletero) = await app.EscenarioBaseAsync();
        var siguiente = FechaHoyArgentina.Hoy().Year + 1;

        var error = await RechazoAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id, 7, siguiente));

        Assert.Equal("periodo_invalido", error.Codigo);
    }

    [Fact]
    public async Task Rechaza_AlTransportistaPropio_YAlDadoDeBaja()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var propio = await app.TransportistaPropioAsync();
        var dadoDeBaja = await app.TransportistaExternoAsync("Retirado S.A.", activo: false);

        Assert.Equal(
            "transportista_no_liquidable",
            (await RechazoAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(propio.Id))).Codigo);

        Assert.Equal(
            "transportista_no_liquidable",
            (await RechazoAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(dadoDeBaja.Id))).Codigo);
    }

    [Fact]
    public async Task Rechaza_SinEmpresaEmisoraConfigurada()
    {
        var fletero = await app.TransportistaExternoAsync();
        await app.SinEmpresaEmisoraAsync();

        var error = await RechazoAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id));

        Assert.Equal("empresa_emisora_no_configurada", error.Codigo);
    }

    private async Task<List<ViajeDisponibleLeido>> LeerAsync(int transportistaId)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        return (await cliente.GetFromJsonAsync<List<ViajeDisponibleLeido>>(
            DatosDePruebaLiquidaciones.RutaDeDisponibles(transportistaId)))!;
    }

    private async Task<ErrorLiquidacionLeido> RechazoAsync(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        return (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
    }
}
