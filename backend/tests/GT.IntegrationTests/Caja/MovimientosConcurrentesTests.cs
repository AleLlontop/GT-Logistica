using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// Dos pestañas del mismo empleado registrando a la vez sobre la misma caja abierta: el lock de la caja las
/// ordena, no las rechaza. Los dos movimientos se registran (edge case de dos pestañas).
/// </summary>
public class MovimientosConcurrentesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_MovimientosSimultaneos_SeRegistranLosDos()
    {
        var (usuario, primero) = await app.EmpleadoAsync();
        var segundo = await app.CrearClienteAutenticadoAsync(usuario.Username, DatosDePruebaCaja.Password);
        var cajaId = (await (await primero.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;

        for (var intento = 0; intento < 5; intento++)
        {
            var respuestas = await Task.WhenAll(
                primero.RegistrarMovimientoAsync(cajaId, "ingreso", 100m),
                segundo.RegistrarMovimientoAsync(cajaId, "egreso", 40m));

            Assert.All(respuestas, respuesta => Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode));
        }

        Assert.Equal(10, await app.ContarMovimientosDeAsync(cajaId));
        Assert.Equal(10_300m, (await primero.DetalleAsync(cajaId)).SaldoActual);
    }
}
