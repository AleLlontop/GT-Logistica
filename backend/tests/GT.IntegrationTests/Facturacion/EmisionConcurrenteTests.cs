using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// La carrera de SC-005: <b>dos administrativos confirmando en el mismo milisegundo facturas que
/// comparten un viaje</b>.
///
/// <b>Esto no lo puede verificar una persona operando la aplicación</b>, y el Principio IV obliga a
/// declararlo en vez de fingir que sí. Queda acá, contra el SQL Server del compose, y el
/// <c>quickstart.md</c> lo dice en vez de pedirle a quien valida algo que no puede hacer (research §14).
///
/// <b>Lo que cierra la carrera es el <c>UPDATE</c> condicional</b> cuyo número de filas afectadas se
/// verifica dentro de la transacción, no un índice: <c>Viajes.FacturaId</c> es una columna escalar, así
/// que la unicidad ya es estructural y no hay nada que un índice agregue (research §4). Bajo el nivel de
/// aislamiento por defecto, la segunda transacción se bloquea sobre la fila que la primera está
/// modificando y, al desbloquearse, reevalúa el <c>WHERE</c> contra el dato ya comprometido.
/// </summary>
public class EmisionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// SC-005: <b>se crea exactamente una</b> factura, la otra recibe el rechazo nombrando el viaje y su
    /// comprobante, y no queda ninguna factura con viajes sin marcar.
    /// </summary>
    [Fact]
    public async Task Dos_EmisionesQueCompartenUnViaje_CreanExactamenteUna()
    {
        var (clienteId, compartido, propioDeA, propioDeB) = await EscenarioAsync();

        var administrativoA = await app.CrearClienteAutenticadoAsync();
        var administrativoB = await app.CrearClienteAutenticadoAsync();

        var numeroDeA = DatosDePruebaFacturas.NumeroUnico();
        var numeroDeB = DatosDePruebaFacturas.NumeroUnico();

        // Las dos en paralelo y de verdad: sin `await` en el medio, las dos peticiones salen antes de
        // que ninguna termine. Es lo más cerca que se puede estar de dos operadores simultáneos.
        var emisionDeA = administrativoA.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, [compartido.Id, propioDeA.Id], numeroDeA));

        var emisionDeB = administrativoB.PostAsJsonAsync(
            "/api/facturas",
            Cuerpo(clienteId, [compartido.Id, propioDeB.Id], numeroDeB));

        var respuestas = await Task.WhenAll(emisionDeA, emisionDeB);

        var creadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rechazadas = respuestas.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();

        Assert.Equal(1, creadas);
        Assert.Single(rechazadas);

        // El rechazo es 409 —el estado de algo compartido cambió— y nombra el viaje que lo produjo, en
        // el cuerpo además de en el mensaje (research §11, convención [004]).
        var rechazada = rechazadas[0];
        Assert.Equal(HttpStatusCode.Conflict, rechazada.StatusCode);

        var error = await rechazada.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("viaje_ya_facturado", error!.Codigo);
        Assert.NotNull(error.Viajes);
        Assert.Contains(error.Viajes!, viaje => viaje.Id == compartido.Id);
        Assert.Contains(compartido.Numero.ToString(), error.Mensaje, StringComparison.Ordinal);

        // ── Lo que hace que la garantía sea real: qué quedó en la base ───────────────────────────
        var facturas = await app.ConAlcanceAsync(contexto => contexto.Facturas
            .Where(factura => factura.ClienteId == clienteId)
            .Include(factura => factura.Viajes)
            .AsNoTracking()
            .ToListAsync());

        var unica = Assert.Single(facturas);

        // El viaje compartido quedó en **una sola** factura, la que ganó.
        var compartidoDespues = await app.RecargarViajeAsync(compartido.Id);
        Assert.Equal(EstadoViaje.Facturado, compartidoDespues!.Estado);
        Assert.Equal(unica.Id, compartidoDespues.FacturaId);

        // Y **no quedó ninguna factura con viajes sin marcar**: los dos viajes de la que se creó están
        // en `facturado` apuntándola, y el de la que se rechazó quedó intacto en `rendido`.
        Assert.Equal(2, unica.Viajes.Count);
        Assert.All(unica.Viajes, viaje =>
        {
            Assert.Equal(EstadoViaje.Facturado, viaje.Estado);
            Assert.Equal(unica.Id, viaje.FacturaId);
        });

        var perdedor = unica.Viajes.Any(viaje => viaje.Id == propioDeA.Id) ? propioDeB : propioDeA;
        var intacto = await app.RecargarViajeAsync(perdedor.Id);

        Assert.Equal(EstadoViaje.Rendido, intacto!.Estado);
        Assert.Null(intacto.FacturaId);
    }

    /// <summary>
    /// La otra carrera del módulo: <b>dos emisiones simultáneas con el mismo número de comprobante</b>
    /// (FR-027). Acá sí es un índice único filtrado lo que la cierra, porque la exclusividad no es
    /// estructural: dos filas distintas pueden tener el mismo texto en la columna.
    ///
    /// La consulta previa deja pasar a las dos y el índice corta a la segunda. El caso de uso traduce la
    /// violación y responde con el rechazo que corresponde, no con un <c>500</c>.
    /// </summary>
    [Fact]
    public async Task Dos_EmisionesConElMismoNumero_CreanExactamenteUna()
    {
        var (clienteId, _, propioDeA, propioDeB) = await EscenarioAsync();

        var administrativoA = await app.CrearClienteAutenticadoAsync();
        var administrativoB = await app.CrearClienteAutenticadoAsync();

        var numero = DatosDePruebaFacturas.NumeroUnico();

        var respuestas = await Task.WhenAll(
            administrativoA.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, [propioDeA.Id], numero)),
            administrativoB.PostAsJsonAsync("/api/facturas", Cuerpo(clienteId, [propioDeB.Id], numero)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Created));

        var rechazada = respuestas.Single(r => r.StatusCode != HttpStatusCode.Created);

        // `400` y no `409`: un número repetido es un duplicado, como el remito del Módulo 5, y se
        // corrige tipeando otro (research §11).
        Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);

        var error = await rechazada.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("numero_duplicado", error!.Codigo);

        var conEseNumero = await app.ConAlcanceAsync(contexto => contexto.Facturas
            .CountAsync(factura => factura.NumeroComprobante == numero));

        Assert.Equal(1, conEseNumero);
    }

    private async Task<(int ClienteId, Viaje Compartido, Viaje PropioDeA, Viaje PropioDeB)>
        EscenarioAsync()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var padron = await app.CrearClienteAsync();

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == padron.Id)
                .ExecuteUpdateAsync(cambio =>
                    cambio.SetProperty(c => c.Direccion, "Ruta 9 km 312, Rosario"));
        });

        return (
            padron.Id,
            await RendidoAsync(padron.Id),
            await RendidoAsync(padron.Id),
            await RendidoAsync(padron.Id));
    }

    private Task<Viaje> RendidoAsync(int clienteId) => app.CrearViajeAsync(
        clienteId,
        estado: EstadoViaje.Rendido,
        numeroRemito: ArmadoDeEscenarios.RemitoUnico(),
        importe: 40_000m);

    private static object Cuerpo(int clienteId, int[] viajeIds, string numero)
    {
        var hoy = FechaHoyArgentina.Hoy();

        return new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion = "original",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = numero,
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            viajeIds,
        };
    }
}
