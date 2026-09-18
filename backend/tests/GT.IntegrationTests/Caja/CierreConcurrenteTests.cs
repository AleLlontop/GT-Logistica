using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// Las dos carreras que cierra el lock de la caja (research §2). Cada pedido corre en su propio alcance de
/// servicio, con su propio <c>DbContext</c> y su propia conexión.
/// </summary>
public class CierreConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_CierresSimultaneos_ExactamenteUnoGana()
    {
        for (var intento = 0; intento < 5; intento++)
        {
            var (primero, segundo, cajaId) = await DosPestaniasAsync();

            var respuestas = await Task.WhenAll(
                primero.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m),
                segundo.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m));

            var estados = respuestas.Select(respuesta => respuesta.StatusCode).OrderBy(estado => estado).ToList();
            Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Conflict], estados);

            var perdedora = respuestas.Single(respuesta => respuesta.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal("caja_cerrada", (await perdedora.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);
        }
    }

    [Fact]
    public async Task Un_MovimientoYUnCierreSimultaneos_TerminanSiempreEnUnOrdenValido()
    {
        for (var intento = 0; intento < 10; intento++)
        {
            var (primero, segundo, cajaId) = await DosPestaniasAsync();

            var movimiento = primero.RegistrarMovimientoAsync(cajaId, "ingreso", 500m);
            var cierre = segundo.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m);

            await Task.WhenAll(movimiento, cierre);

            var estadoMovimiento = (await movimiento).StatusCode;
            var estadoCierre = (await cierre).StatusCode;
            var releida = (await app.RecargarCajaAsync(cajaId))!;

            if (estadoMovimiento == HttpStatusCode.Created)
            {
                // Entró el movimiento primero: el cierre vio otro número y no cerró.
                Assert.Equal(HttpStatusCode.Conflict, estadoCierre);
                Assert.Equal(
                    "cierre_desactualizado",
                    (await (await cierre).Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);
                Assert.Equal(EstadoCaja.Abierta, releida.Estado);
            }
            else
            {
                // Cerró primero: el movimiento encontró la caja cerrada.
                Assert.Equal(HttpStatusCode.OK, estadoCierre);
                Assert.Equal(HttpStatusCode.Conflict, estadoMovimiento);
                Assert.Equal(
                    "caja_cerrada",
                    (await (await movimiento).Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);
                Assert.Equal(EstadoCaja.Cerrada, releida.Estado);
            }

            // Nunca una caja cerrada cuyo saldo final discrepe de sus movimientos.
            if (releida.Estado is EstadoCaja.Cerrada)
            {
                var ingresos = releida.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Ingreso).Sum(m => m.Importe);
                var egresos = releida.Movimientos.Where(m => m.Tipo == TipoMovimientoCaja.Egreso).Sum(m => m.Importe);

                Assert.Equal(ReglasDeCaja.SaldoFinal(releida.SaldoInicial, ingresos, egresos), releida.SaldoFinal);
            }
        }
    }

    /// <summary>Una caja abierta con $10.000 y dos clientes con la sesión de su responsable.</summary>
    private async Task<(HttpClient Primero, HttpClient Segundo, int CajaId)> DosPestaniasAsync()
    {
        var (usuario, primero) = await app.EmpleadoAsync();
        var segundo = await app.CrearClienteAutenticadoAsync(usuario.Username, DatosDePruebaCaja.Password);
        var cajaId = (await (await primero.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;

        return (primero, segundo, cajaId);
    }
}
