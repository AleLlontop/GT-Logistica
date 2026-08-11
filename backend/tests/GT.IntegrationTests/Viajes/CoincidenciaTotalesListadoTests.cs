using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// SC-008 y US7 esc. 4: la suma de los importes del listado filtrado por cliente y rango
/// <b>coincide</b> con el total de ese cliente en el mismo rango.
///
/// <b>Coincide porque las dos consultas excluyen los anulados con el mismo predicado</b>, escrito una
/// sola vez. Es el test que verifica que la pantalla de totales y la de listado cuentan la misma
/// historia: si alguien cambiara la exclusión en un solo lado, acá se rompe.
/// </summary>
public class CoincidenciaTotalesListadoTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task La_SumaDelListado_CoincideConElTotalDelCliente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        // Una mezcla de estados y fechas, para que la coincidencia no sea trivial.
        await app.CrearViajeAsync(padron.Id, fecha: hoy, importe: 120_000m);
        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-3), importe: 340_500m);
        await app.CrearViajeAsync(
            padron.Id,
            fecha: hoy.AddDays(-1),
            estado: EstadoViaje.Rendido,
            importe: 780_000m);

        await app.CrearViajeAsync(
            padron.Id,
            fecha: hoy.AddDays(-2),
            estado: EstadoViaje.Anulado,
            importe: 999_999m,
            motivoAnulacion: "No se hizo.");

        // Fuera del rango: no tiene que contar en ninguno de los dos lados.
        await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-60), importe: 500_000m);

        var desde = hoy.AddDays(-5);
        var hasta = hoy;

        var listado = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}" +
            $"&desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");

        var totales = await TotalesTests.TotalesDelRangoAsync(cliente, desde, hasta);

        var delCliente = totales.PorCliente.Single(fila => fila.Id == padron.Id);

        Assert.Equal(delCliente.CantidadViajes, listado!.Total);
        Assert.Equal(delCliente.ImporteTotal, listado.Items.Sum(fila => fila.Importe));
        Assert.Equal(3, delCliente.CantidadViajes);
    }
}
