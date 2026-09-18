using System.Net;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// El doble clic de CL3: dos aperturas simultáneas del mismo usuario. La consulta previa las deja pasar a
/// las dos y el índice único filtrado corta a la segunda (research §1, SC-002).
/// </summary>
public class AperturaConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_AperturasSimultaneas_TerminanConUnaSolaCaja()
    {
        for (var intento = 0; intento < 5; intento++)
        {
            var (usuario, _) = await app.EmpleadoAsync();

            // Dos clientes con la misma sesión de usuario: cada pedido corre en su propio alcance de servicio.
            var primero = await app.CrearClienteAutenticadoAsync(usuario.Username, DatosDePruebaCaja.Password);
            var segundo = await app.CrearClienteAutenticadoAsync(usuario.Username, DatosDePruebaCaja.Password);

            var respuestas = await Task.WhenAll(primero.AbrirCajaAsync(10_000m), segundo.AbrirCajaAsync(10_000m));
            var estados = respuestas.Select(respuesta => respuesta.StatusCode).OrderBy(estado => estado).ToList();

            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], estados);
            Assert.Equal(1, await app.ContarCajasDeAsync(usuario.Id));
        }
    }
}
