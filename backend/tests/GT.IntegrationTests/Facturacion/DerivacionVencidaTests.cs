using GT.Application.Facturacion;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// FR-058a: <b>la regla de <c>vencida</c> está escrita dos veces a propósito</b> —función pura en el
/// dominio y predicado dentro de la consulta— y este test evalúa las dos sobre el <b>mismo conjunto</b>
/// de facturas y compara.
///
/// <b>Por qué la duplicación existe</b>: el listado tiene que <i>filtrar</i> por el estado derivado, y
/// filtrar en memoria después de paginar devolvería páginas incompletas. El predicado va escrito en el
/// árbol de la consulta, y por eso hay dos escrituras de la misma regla (research §3).
///
/// <b>Por qué este test existe</b>: es la convención [003] del proyecto —cuando una regla derivada se
/// ejecuta en dos lados, va un test que compara las dos sobre el mismo dato— y ya tenía precedente en el
/// Módulo 3. Sin él, las dos podrían separarse y el listado mostraría bajo <c>pendiente</c> una factura
/// que la fila de al lado muestra como <c>vencida</c>.
///
/// <b>Esto no lo puede verificar una persona</b> operando la aplicación, y el <c>quickstart.md</c> lo
/// declara en vez de pedírselo (research §14).
/// </summary>
public class DerivacionVencidaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// Un conjunto armado para cubrir los cuatro estados visibles y los tres bordes del vencimiento:
    /// pasado, hoy y futuro.
    /// </summary>
    private async Task<int> ArmarConjuntoAsync()
    {
        var cliente = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Impagas: vencida hace mucho, vencida ayer, vence hoy —todavía en plazo— y vence en una semana.
        await app.CrearFacturaAsync(cliente.Id, vencimientoPago: hoy.AddDays(-45), fecha: hoy.AddDays(-60));
        await app.CrearFacturaAsync(cliente.Id, vencimientoPago: hoy.AddDays(-1), fecha: hoy.AddDays(-31));
        await app.CrearFacturaAsync(cliente.Id, vencimientoPago: hoy, fecha: hoy.AddDays(-30));
        await app.CrearFacturaAsync(cliente.Id, vencimientoPago: hoy.AddDays(7), fecha: hoy);

        // Pagada con el vencimiento pasado: manda `pagada` (FR-041).
        await app.CrearFacturaAsync(
            cliente.Id,
            estado: EstadoFactura.Pagada,
            fecha: hoy.AddDays(-60),
            vencimientoPago: hoy.AddDays(-45),
            fechaCobro: hoy.AddDays(-40));

        // Anulada con el vencimiento pasado: manda `anulada`.
        await app.CrearFacturaAsync(
            cliente.Id,
            estado: EstadoFactura.Anulada,
            fecha: hoy.AddDays(-60),
            vencimientoPago: hoy.AddDays(-45),
            motivoAnulacion: "Cliente equivocado.");

        return cliente.Id;
    }

    /// <summary>
    /// Para cada uno de los cuatro valores, el conjunto que devuelve <b>el filtro de la consulta</b> es
    /// exactamente el que devuelve <b>la regla del dominio</b> aplicada sobre las mismas filas.
    /// </summary>
    [Fact]
    public async Task El_FiltroEnSql_YLaReglaDelDominio_DanElMismoConjunto()
    {
        var clienteId = await ArmarConjuntoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        using var alcance = app.Services.CreateScope();
        var facturas = alcance.ServiceProvider.GetRequiredService<IRepositorioFacturas>();

        // Todas las filas del cliente, sin filtro de estado: es el conjunto sobre el que se comparan las
        // dos escrituras.
        var todas = await facturas.ConsultarAsync(
            new FiltrosDeFacturas(ClienteId: clienteId),
            hoy);

        Assert.Equal(6, todas.Total);

        foreach (var estado in Enum.GetValues<EstadoFacturaVisible>())
        {
            // 1. El filtro, resuelto **dentro de la consulta**.
            var enSql = await facturas.ConsultarAsync(
                new FiltrosDeFacturas(ClienteId: clienteId, Estado: estado),
                hoy);

            var idsDeSql = enSql.Items.Select(fila => fila.Id).OrderBy(id => id).ToList();

            // 2. La regla del dominio, aplicada en memoria sobre las mismas filas.
            var enElDominio = await app.ConAlcanceAsync(async contexto =>
            {
                var filas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .ToListAsync(contexto.Facturas.Where(factura => factura.ClienteId == clienteId));

                return filas
                    .Where(factura => DerivadorEstadoFactura.Derivar(
                        factura.Estado,
                        factura.VencimientoPago,
                        hoy) == estado)
                    .Select(factura => factura.Id)
                    .OrderBy(id => id)
                    .ToList();
            });

            Assert.Equal(enElDominio, idsDeSql);
        }
    }

    /// <summary>
    /// Los cuatro valores son <b>excluyentes</b> y <b>cubren todo</b>: cada factura sale bajo exactamente
    /// uno de los cuatro filtros. Ninguna aparece dos veces y ninguna se pierde (FR-058a, US3 esc. 11).
    /// </summary>
    [Fact]
    public async Task Cada_Factura_SaleBajoExactamenteUnoDeLosCuatroFiltros()
    {
        var clienteId = await ArmarConjuntoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        using var alcance = app.Services.CreateScope();
        var facturas = alcance.ServiceProvider.GetRequiredService<IRepositorioFacturas>();

        var apariciones = new Dictionary<int, int>();

        foreach (var estado in Enum.GetValues<EstadoFacturaVisible>())
        {
            var pagina = await facturas.ConsultarAsync(
                new FiltrosDeFacturas(ClienteId: clienteId, Estado: estado),
                hoy);

            foreach (var fila in pagina.Items)
            {
                apariciones[fila.Id] = apariciones.GetValueOrDefault(fila.Id) + 1;
            }
        }

        var todas = await facturas.ConsultarAsync(new FiltrosDeFacturas(ClienteId: clienteId), hoy);

        Assert.Equal(todas.Total, apariciones.Count);
        Assert.All(apariciones, entrada => Assert.Equal(1, entrada.Value));
    }

    /// <summary>
    /// El estado que la fila <b>muestra</b> coincide con el filtro bajo el que <b>aparece</b>. Sin esta
    /// coincidencia, el listado mostraría `Pendiente` en una fila que entró por el filtro `Vencida`.
    /// </summary>
    [Fact]
    public async Task El_EstadoQueLaFilaMuestra_CoincideConElFiltroQueLaTrajo()
    {
        var clienteId = await ArmarConjuntoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        using var alcance = app.Services.CreateScope();
        var facturas = alcance.ServiceProvider.GetRequiredService<IRepositorioFacturas>();

        foreach (var estado in Enum.GetValues<EstadoFacturaVisible>())
        {
            var pagina = await facturas.ConsultarAsync(
                new FiltrosDeFacturas(ClienteId: clienteId, Estado: estado),
                hoy);

            var esperado = NombresDeEstadoFactura.EnJson(estado);

            Assert.All(pagina.Items, fila => Assert.Equal(esperado, fila.Estado));
        }
    }

    /// <summary>
    /// La derivación se hace contra el <c>hoy</c> que llega por parámetro y no contra el reloj: la misma
    /// factura figura <c>pendiente</c> o <c>vencida</c> según la fecha que se le pase.
    ///
    /// Es lo que permite probar el vencimiento sin esperar treinta días (convención [005]).
    /// </summary>
    [Fact]
    public async Task La_Derivacion_SigueLaFechaQueSeLePasaYNoElReloj()
    {
        var cliente = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var factura = await app.CrearFacturaAsync(
            cliente.Id,
            fecha: hoy,
            vencimientoPago: hoy.AddDays(30));

        using var alcance = app.Services.CreateScope();
        var facturas = alcance.ServiceProvider.GetRequiredService<IRepositorioFacturas>();

        // Hoy: en plazo.
        var enPlazo = await facturas.ConsultarAsync(
            new FiltrosDeFacturas(ClienteId: cliente.Id, Estado: EstadoFacturaVisible.Pendiente),
            hoy);

        Assert.Contains(enPlazo.Items, fila => fila.Id == factura.Id);

        // Dentro de dos meses: vencida, sin que nadie haya tocado nada.
        var vencida = await facturas.ConsultarAsync(
            new FiltrosDeFacturas(ClienteId: cliente.Id, Estado: EstadoFacturaVisible.Vencida),
            hoy.AddMonths(2));

        Assert.Contains(vencida.Items, fila => fila.Id == factura.Id);

        // Y la columna sigue diciendo `pendiente`: el estado guardado no cambió.
        var enLaBase = await app.RecargarFacturaAsync(factura.Id);
        Assert.Equal(EstadoFactura.Pendiente, enLaBase!.Estado);
    }
}
