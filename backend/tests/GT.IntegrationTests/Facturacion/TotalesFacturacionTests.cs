using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// Los totales facturado, cobrado y pendiente por cliente (User Story 7, FR-061, FR-062).
/// </summary>
public class TotalesFacturacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// FR-061: <b>el rango es obligatorio.</b> Sin él no se calcula nada y se responde con su código
    /// propio, para que la pantalla diga que falta elegirlo en vez de mostrar un cuadro vacío — que se
    /// leería como "no hay facturas".
    /// </summary>
    [Theory]
    [InlineData("/api/facturas/totales")]
    [InlineData("/api/facturas/totales?desde=2026-01-01")]
    [InlineData("/api/facturas/totales?hasta=2026-12-31")]
    public async Task Sin_ElRangoCompleto_SeRechaza(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("rango_de_fechas_requerido", error!.Codigo);
        Assert.Equal("Elegí un rango de fechas para ver los totales.", error.Mensaje);
    }

    /// <summary>
    /// <b>La fecha de corte es la fecha de facturación</b>, no la de cobro: es la misma con la que el
    /// listado ordena y filtra (FR-061).
    /// </summary>
    [Fact]
    public async Task La_FechaDeCorte_EsLaDeFacturacion()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Facturada dentro del rango y cobrada **fuera**: cuenta, porque la que manda es la de
        // facturación.
        await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy,
            neto: 100_000m,
            estado: EstadoFactura.Pagada,
            fechaCobro: hoy.AddMonths(3));

        // Facturada fuera del rango: no cuenta.
        await app.CrearFacturaAsync(padron.Id, fecha: hoy.AddMonths(-6), neto: 999_999m);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            $"/api/facturas/totales?desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}");

        var fila = Assert.Single(totales!, total => total.ClienteId == padron.Id);

        Assert.Equal(1, fila.Cantidad);
        Assert.Equal(121_000m, fila.Facturado);
        Assert.Equal(121_000m, fila.Cobrado);
    }

    /// <summary>
    /// FR-062: <b>las anuladas no suman en ninguna columna</b>, ni en la cantidad. Es lo que sostiene
    /// SC-011.
    /// </summary>
    [Fact]
    public async Task Las_Anuladas_NoSumanEnNingunaColumna()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 100_000m);

        await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy,
            neto: 500_000m,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Cliente equivocado.");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            $"/api/facturas/totales?desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}");

        var fila = Assert.Single(totales!, total => total.ClienteId == padron.Id);

        // Sólo la vigente: ni la cantidad, ni el facturado, ni el cobrado cuentan la anulada.
        Assert.Equal(1, fila.Cantidad);
        Assert.Equal(121_000m, fila.Facturado);
        Assert.Equal(0m, fila.Cobrado);
        Assert.Equal(121_000m, fila.Pendiente);
    }

    /// <summary>
    /// Las tres columnas y su relación: <c>pendiente = facturado − cobrado</c>, con las pagadas sumando en
    /// las dos primeras (FR-061).
    /// </summary>
    [Fact]
    public async Task Las_TresColumnas_SeRelacionanComoFR061Pide()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Dos impagas y una cobrada.
        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 100_000m);
        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 200_000m);
        await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy,
            neto: 300_000m,
            estado: EstadoFactura.Pagada,
            fechaCobro: hoy);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            $"/api/facturas/totales?desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}");

        var fila = Assert.Single(totales!, total => total.ClienteId == padron.Id);

        Assert.Equal(3, fila.Cantidad);
        Assert.Equal(726_000m, fila.Facturado);
        Assert.Equal(363_000m, fila.Cobrado);
        Assert.Equal(363_000m, fila.Pendiente);
        Assert.Equal(fila.Facturado - fila.Cobrado, fila.Pendiente);
    }

    /// <summary>
    /// SC-011: <b>la suma de los importes del listado filtrado coincide con la columna <i>facturado</i></b>.
    /// Es lo que hace que los dos números se puedan comparar sin explicaciones, y sale de que las dos
    /// consultas excluyan las anuladas con el mismo predicado.
    /// </summary>
    [Fact]
    public async Task La_SumaDelListadoFiltrado_CoincideConLaColumnaFacturado()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 82_644.63m);
        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 30_000m);
        await app.CrearFacturaAsync(padron.Id, fecha: hoy, neto: 17_355.37m);
        await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy,
            neto: 999_999m,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "No cuenta.");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var desde = hoy.AddDays(-1).ToString("yyyy-MM-dd");
        var hasta = hoy.AddDays(1).ToString("yyyy-MM-dd");

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            $"/api/facturas/totales?desde={desde}&hasta={hasta}");

        var fila = Assert.Single(totales!, total => total.ClienteId == padron.Id);

        // El listado del mismo cliente y rango, excluyendo las anuladas con el filtro de estado.
        var pendientes = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&desde={desde}&hasta={hasta}&estado=pendiente");

        var pagadas = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&desde={desde}&hasta={hasta}&estado=pagada");

        var vencidas = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&desde={desde}&hasta={hasta}&estado=vencida");

        var sumaDelListado =
            pendientes!.Items.Sum(f => f.Total) +
            pagadas!.Items.Sum(f => f.Total) +
            vencidas!.Items.Sum(f => f.Total);

        Assert.Equal(fila.Facturado, sumaDelListado);
        Assert.Equal(3, fila.Cantidad);
    }

    /// <summary>Un rango sin facturas devuelve la lista vacía, que es una respuesta legítima.</summary>
    [Fact]
    public async Task Un_RangoSinFacturas_DevuelveLaListaVacia()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            "/api/facturas/totales?desde=1999-01-01&hasta=1999-12-31");

        Assert.Empty(totales!);
    }

    /// <summary>Los totales se agrupan por cliente, ordenados por razón social.</summary>
    [Fact]
    public async Task Los_Totales_SeAgrupanPorCliente()
    {
        var hoy = FechaHoyArgentina.Hoy();

        var unCliente = await app.CrearClienteAsync(razonSocial: "AAA Primera");
        var otroCliente = await app.CrearClienteAsync(razonSocial: "ZZZ Ultima");

        await app.CrearFacturaAsync(unCliente.Id, fecha: hoy, neto: 100_000m);
        await app.CrearFacturaAsync(otroCliente.Id, fecha: hoy, neto: 200_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var totales = await cliente.GetFromJsonAsync<List<TotalPorClienteLeido>>(
            $"/api/facturas/totales?desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}");

        var deUno = Assert.Single(totales!, total => total.ClienteId == unCliente.Id);
        var deOtro = Assert.Single(totales!, total => total.ClienteId == otroCliente.Id);

        Assert.Equal(121_000m, deUno.Facturado);
        Assert.Equal(242_000m, deOtro.Facturado);

        // Ordenados por razón social: el de "AAA" viene antes que el de "ZZZ".
        var posicionDeUno = totales!.FindIndex(total => total.ClienteId == unCliente.Id);
        var posicionDeOtro = totales.FindIndex(total => total.ClienteId == otroCliente.Id);

        Assert.True(posicionDeUno < posicionDeOtro);
    }
}
