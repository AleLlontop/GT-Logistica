using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// El listado: los cinco filtros combinables, la paginación y el orden (FR-057 a FR-059).
///
/// <b>Los filtros se aplican antes de paginar</b>, y eso es lo que este archivo verifica sobre datos de
/// verdad: filtrar después de paginar devolvería páginas incompletas y el total mentiría.
/// </summary>
public class ConsultaFacturasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// Sin filtro de estado se devuelven <b>todas, incluidas las anuladas</b> —al revés que el listado de
    /// viajes— y el control de la pantalla lo dice explícitamente (FR-064).
    /// </summary>
    [Fact]
    public async Task Sin_FiltroDeEstado_SeDevuelvenTodasIncluidasLasAnuladas()
    {
        var padron = await app.CrearClienteAsync();

        await app.CrearFacturaAsync(padron.Id);
        await app.CrearFacturaAsync(padron.Id, estado: EstadoFactura.Pagada);
        await app.CrearFacturaAsync(
            padron.Id,
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Cliente equivocado.");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}");

        Assert.Equal(3, pagina!.Total);
        Assert.Contains(pagina.Items, fila => fila.Estado == "anulada");
    }

    /// <summary>
    /// Los cinco filtros combinados. Cada uno recorta, y combinarlos recorta más: es lo que hace útil a
    /// la pantalla cuando el padrón crece (FR-058).
    /// </summary>
    [Fact]
    public async Task Los_CincoFiltros_SeCombinan()
    {
        var unCliente = await app.CrearClienteAsync();
        var otroCliente = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var buscada = await app.CrearFacturaAsync(
            unCliente.Id,
            fecha: hoy,
            tipo: TipoComprobante.FacturaB,
            mes: 8,
            anio: 2026);

        // Cada una difiere de la buscada en **un** filtro, así que cada filtro tiene que descartarla.
        await app.CrearFacturaAsync(otroCliente.Id, fecha: hoy, tipo: TipoComprobante.FacturaB, mes: 8, anio: 2026);
        await app.CrearFacturaAsync(unCliente.Id, fecha: hoy, tipo: TipoComprobante.FacturaA, mes: 8, anio: 2026);
        await app.CrearFacturaAsync(unCliente.Id, fecha: hoy, tipo: TipoComprobante.FacturaB, mes: 7, anio: 2026);
        await app.CrearFacturaAsync(unCliente.Id, fecha: hoy, tipo: TipoComprobante.FacturaB, mes: 8, anio: 2025);
        await app.CrearFacturaAsync(unCliente.Id, fecha: hoy.AddMonths(-6), tipo: TipoComprobante.FacturaB, mes: 8, anio: 2026);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ruta =
            $"/api/facturas?clienteId={unCliente.Id}" +
            $"&desde={hoy.AddDays(-1):yyyy-MM-dd}&hasta={hoy.AddDays(1):yyyy-MM-dd}" +
            "&mes=8&anio=2026&tipoComprobante=facturaB";

        var pagina = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(ruta);

        Assert.Equal(1, pagina!.Total);
        Assert.Equal(buscada.Id, Assert.Single(pagina.Items).Id);
    }

    /// <summary>
    /// FR-058a: <c>pendiente</c> y <c>vencida</c> son <b>excluyentes</b>. Una factura impaga y pasada de
    /// fecha aparece bajo <c>Vencida</c> y <b>no</b> bajo <c>Pendiente</c> (US3 esc. 11).
    /// </summary>
    [Fact]
    public async Task Pendiente_YVencida_SonExcluyentes()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var enPlazo = await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy,
            vencimientoPago: hoy.AddDays(10));

        var pasada = await app.CrearFacturaAsync(
            padron.Id,
            fecha: hoy.AddDays(-40),
            vencimientoPago: hoy.AddDays(-10));

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pendientes = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&estado=pendiente");

        var vencidas = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&estado=vencida");

        Assert.Equal(enPlazo.Id, Assert.Single(pendientes!.Items).Id);
        Assert.Equal(pasada.Id, Assert.Single(vencidas!.Items).Id);

        // Ninguna aparece bajo las dos.
        Assert.DoesNotContain(pendientes.Items, fila => fila.Id == pasada.Id);
        Assert.DoesNotContain(vencidas.Items, fila => fila.Id == enPlazo.Id);
    }

    /// <summary>
    /// Un valor desconocido de estado <b>se ignora</b> en vez de romper: filtrar de más no es un error, y
    /// el listado responde su vista por defecto (convención [003]).
    /// </summary>
    [Fact]
    public async Task Un_EstadoDesconocido_SeIgnora()
    {
        var padron = await app.CrearClienteAsync();
        await app.CrearFacturaAsync(padron.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&estado=inventado");

        Assert.Equal(1, pagina!.Total);
    }

    /// <summary>
    /// Página de 20 con el <b>total de coincidencias</b>, y los filtros aplicados <b>antes</b> de
    /// paginar: el total cuenta todo lo que cumple el filtro, no lo que cabe en la página (FR-059).
    /// </summary>
    [Fact]
    public async Task La_Paginacion_EsDe20ConElTotalDeCoincidencias()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        for (var i = 0; i < 25; i++)
        {
            await app.CrearFacturaAsync(padron.Id, fecha: hoy.AddDays(-i));
        }

        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&pagina=1");

        Assert.Equal(25, primera!.Total);
        Assert.Equal(20, primera.TamanioPagina);
        Assert.Equal(20, primera.Items.Count);
        Assert.Equal(1, primera.Pagina);

        var segunda = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}&pagina=2");

        Assert.Equal(25, segunda!.Total);
        Assert.Equal(5, segunda.Items.Count);

        // Ninguna fila se repite entre páginas: el orden es total (FR-059).
        var idsDeLaPrimera = primera.Items.Select(fila => fila.Id).ToHashSet();
        Assert.DoesNotContain(segunda.Items, fila => idsDeLaPrimera.Contains(fila.Id));
    }

    /// <summary>
    /// Pedir el listado <b>sin el parámetro de página</b> tiene que tomar el valor por defecto en vez de
    /// fallar al enlazar (convención [003]).
    /// </summary>
    [Fact]
    public async Task Sin_ElParametroDePagina_TomaLaPrimera()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/facturas");

        respuesta.EnsureSuccessStatusCode();

        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaDeFacturasLeida>();
        Assert.Equal(1, pagina!.Pagina);
    }

    /// <summary>
    /// FR-059: orden <b>fecha de facturación descendente</b> y, a igual fecha, <b>número de comprobante
    /// descendente</b>.
    /// </summary>
    [Fact]
    public async Task El_Orden_EsFechaDescendenteYLuegoNumeroDescendente()
    {
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Dos del mismo día, con números que ordenan al revés del alta.
        await app.CrearFacturaAsync(padron.Id, numeroComprobante: "0099-00000001", fecha: hoy);
        await app.CrearFacturaAsync(padron.Id, numeroComprobante: "0099-00000002", fecha: hoy);
        await app.CrearFacturaAsync(padron.Id, numeroComprobante: "0099-00000003", fecha: hoy.AddDays(-5));

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}");

        Assert.Equal(
            ["0099-00000002", "0099-00000001", "0099-00000003"],
            pagina!.Items.Select(fila => fila.NumeroComprobante));
    }

    /// <summary>
    /// Las ocho columnas de FR-057, con la razón social <b>congelada</b> y el <c>activo</c> del padrón: un
    /// cliente dado de baja después de facturado se muestra con su razón social de entonces y la palabra
    /// que lo señala (FR-011, US3 esc. 9).
    /// </summary>
    [Fact]
    public async Task La_Fila_TraeLaRazonSocialCongeladaYElActivoDelPadron()
    {
        var padron = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(padron.Id);

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == padron.Id)
                .ExecuteUpdateAsync(cambio => cambio
                    .SetProperty(c => c.Activo, false)
                    .SetProperty(c => c.RazonSocial, "Nombre cambiado después"));
        });

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeFacturasLeida>(
            $"/api/facturas?clienteId={padron.Id}");

        var fila = Assert.Single(pagina!.Items, item => item.Id == factura.Id);

        // La congelada, no la del padrón (FR-034a)…
        Assert.NotEqual("Nombre cambiado después", fila.Cliente.RazonSocial);

        // …y el `activo` sí sale del padrón, que es lo que permite mostrar `Inactivo` al lado.
        Assert.False(fila.Cliente.Activo);

        // Las ocho columnas están.
        Assert.False(string.IsNullOrWhiteSpace(fila.NumeroComprobante));
        Assert.False(string.IsNullOrWhiteSpace(fila.Fecha));
        Assert.Equal("facturaA", fila.TipoComprobante);
        Assert.InRange(fila.Mes, 1, 12);
        Assert.True(fila.Total > 0);
        Assert.False(string.IsNullOrWhiteSpace(fila.Estado));
        Assert.False(string.IsNullOrWhiteSpace(fila.VencimientoPago));
    }

    /// <summary>
    /// La ficha completa (FR-060), con los datos congelados, los viajes, el historial y el enlace al
    /// documento.
    /// </summary>
    [Fact]
    public async Task La_Ficha_TraeTodoLoQueFR060Pide()
    {
        var padron = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(padron.Id, detalle: "Servicios del período.");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<FacturaDetalleLeida>($"/api/facturas/{factura.Id}");

        Assert.NotNull(ficha);
        Assert.Equal(factura.NumeroComprobante, ficha!.NumeroComprobante);
        Assert.Equal("G&T Logística S.R.L.", ficha.Emisor.RazonSocial);
        Assert.Equal("Ruta 9 km 312, Rosario", ficha.Cliente.Domicilio);
        Assert.Equal("Servicios del período.", ficha.Detalle);
        Assert.Equal(21m, ficha.Alicuota);
        Assert.Equal($"/api/facturas/{factura.Id}/documento", ficha.DocumentoUrl);
        Assert.Null(ficha.ReemplazaA);
        Assert.Null(ficha.ReemplazadaPor);
    }

    [Fact]
    public async Task Una_FacturaInexistente_Responde404()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/facturas/999999");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// <b>La trampa de research §15.1</b>: las cinco rutas literales conviven con
    /// <c>/api/facturas/{id:int}</c>. Sin la restricción de tipo quedarían inalcanzables, y **no falla al
    /// compilar ni al arrancar**: falla al pedirlas. Este test es el que lo nota.
    /// </summary>
    [Theory]
    [InlineData("/api/facturas/vencimientos")]
    [InlineData("/api/facturas/totales?desde=2026-01-01&hasta=2026-12-31")]
    [InlineData("/api/facturas/facturables?clienteId=1&mes=8&anio=2026")]
    [InlineData("/api/facturas/anuladas-sin-reemplazo?clienteId=1")]
    public async Task Las_RutasLiterales_SonAlcanzablesJuntoALaDeIdentificador(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(ruta);

        // Lo que importa es que **no** se la trate como identificador: eso daría 404 de enrutamiento.
        // Cualquier respuesta del endpoint real —200 o 400 por parámetros— demuestra que llegó.
        Assert.NotEqual(System.Net.HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
