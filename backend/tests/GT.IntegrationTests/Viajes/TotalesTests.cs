using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Los totales por período (FR-046, FR-046a, FR-047; US7 esc. 1, 2 y 3).
/// </summary>
public class TotalesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>US7 esc. 2: sin rango no se calcula nada, y el rechazo dice qué falta.</summary>
    [Theory]
    [InlineData("/api/viajes/totales")]
    [InlineData("/api/viajes/totales?desde=2026-08-01")]
    [InlineData("/api/viajes/totales?hasta=2026-08-31")]
    public async Task Sin_Rango_SeRechaza(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("rango_de_fechas_requerido", error!.Codigo);
        Assert.Equal("Elegí un rango de fechas para ver los totales.", error.Mensaje);
    }

    /// <summary>FR-046a: la fecha de corte es <b>la del viaje</b>, no la de carga ni la de rendición.</summary>
    [Fact]
    public async Task La_FechaDeCorte_EsLaDelViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Los tres se cargan ahora, pero sus fechas de viaje son distintas.
        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-40), importe: 500_000m);
        await app.CrearViajeAsync(padron.Id, fecha: hoy, importe: 100_000m);
        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(40), importe: 700_000m);

        var totales = await TotalesDelRangoAsync(cliente, hoy.AddDays(-5), hoy.AddDays(5));

        var delCliente = totales.PorCliente.Single(fila => fila.Id == padron.Id);

        Assert.Equal(1, delCliente.CantidadViajes);
        Assert.Equal(100_000m, delCliente.ImporteTotal);
    }

    /// <summary>
    /// US7 esc. 3: un cliente con 10 viajes de los cuales 2 están anulados figura con <b>8</b>. La
    /// exclusión es un predicado de la consulta, no un filtrado posterior (FR-047).
    /// </summary>
    [Fact]
    public async Task Los_Anulados_NoCuentanNiEnCantidadNiEnImporte()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        for (var i = 0; i < 8; i++)
        {
            await app.CrearViajeAsync(padron.Id, fecha: hoy, importe: 100_000m);
        }

        for (var i = 0; i < 2; i++)
        {
            await app.CrearViajeAsync(
                padron.Id,
                fecha: hoy,
                estado: EstadoViaje.Anulado,
                importe: 999_999m,
                motivoAnulacion: "No se hizo.");
        }

        var totales = await TotalesDelRangoAsync(cliente, hoy.AddDays(-1), hoy.AddDays(1));

        var delCliente = totales.PorCliente.Single(fila => fila.Id == padron.Id);

        Assert.Equal(8, delCliente.CantidadViajes);
        Assert.Equal(800_000m, delCliente.ImporteTotal);
    }

    /// <summary>
    /// Los viajes sin transportista no aparecen en el segundo cuadro: un viaje sin chofer asignado
    /// todavía no tiene transportista, y es el comportamiento esperado.
    /// </summary>
    [Fact]
    public async Task Los_ViajesSinTransportista_NoApareceEnEseCuadro()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearViajeDelEscenarioAsync(escenario, fecha: hoy, asignado: true);
        await app.CrearViajeAsync(escenario.ClienteId, fecha: hoy, importe: 300_000m);

        var totales = await TotalesDelRangoAsync(cliente, hoy.AddDays(-1), hoy.AddDays(1));

        // El cliente cuenta los dos; el transportista, sólo el asignado.
        Assert.Equal(2, totales.PorCliente.Single(f => f.Id == escenario.ClienteId).CantidadViajes);

        var delTransportista = totales.PorTransportista
            .Single(fila => fila.Id == escenario.TransportistaId);

        Assert.Equal(1, delTransportista.CantidadViajes);
    }

    [Fact]
    public async Task Agrupa_PorClienteYPorTransportista()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var primero = await app.ArmarEscenarioAsync();
        var segundo = await app.ArmarEscenarioAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearViajeDelEscenarioAsync(primero, fecha: hoy, asignado: true);
        await app.CrearViajeDelEscenarioAsync(primero, fecha: hoy, asignado: true);
        await app.CrearViajeDelEscenarioAsync(segundo, fecha: hoy, asignado: true);

        var totales = await TotalesDelRangoAsync(cliente, hoy.AddDays(-1), hoy.AddDays(1));

        Assert.Equal(2, totales.PorCliente.Single(f => f.Id == primero.ClienteId).CantidadViajes);
        Assert.Equal(1, totales.PorCliente.Single(f => f.Id == segundo.ClienteId).CantidadViajes);

        Assert.Equal(
            2,
            totales.PorTransportista.Single(f => f.Id == primero.TransportistaId).CantidadViajes);
    }

    [Fact]
    public async Task Un_PeriodoSinViajes_DevuelveLosDosCuadrosVacios()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await TotalesDelRangoAsync(
            cliente,
            new DateOnly(1990, 1, 1),
            new DateOnly(1990, 1, 31));

        Assert.Empty(totales.PorCliente);
        Assert.Empty(totales.PorTransportista);
    }

    /// <summary>La ruta literal no queda capturada por la de identificador (tasks §trampa 1).</summary>
    [Fact]
    public async Task La_RutaLiteral_NoQuedaCapturadaPorLaDeIdentificador()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/viajes/totales?desde=2026-08-01&hasta=2026-08-31");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    internal static async Task<TotalesLeidos> TotalesDelRangoAsync(
        HttpClient cliente,
        DateOnly desde,
        DateOnly hasta)
    {
        var totales = await cliente.GetFromJsonAsync<TotalesLeidos>(
            $"/api/viajes/totales?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");

        return totales!;
    }
}
