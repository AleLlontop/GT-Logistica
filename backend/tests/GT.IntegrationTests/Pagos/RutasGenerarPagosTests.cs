using System.Net;
using System.Text;
using System.Text.Json;
using GT.Application.Pagos;
using GT.Api.Pagos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Pagos;

public class RutasGenerarPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task PostIntencional_UnaSolicitud201ConLocation_ReplayOriginal_SinHandshake()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, new(2026, 10, 17), null,
            [s.Factura.Id], [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "API-001", new(2026, 10, 17), new(2026, 10, 18), null)], 121m, true);
        Task<HttpResponseMessage> Post(GenerarOrdenEntrada e) => cliente.PostAsync("/api/pagos/ordenes",
            new StringContent(JsonSerializer.Serialize(e, SerializadorPagos.Opciones), Encoding.UTF8, "application/json"));
        var sinConfirmar = await Post(entrada with { Confirmado = false });
        Assert.Equal(HttpStatusCode.BadRequest, sinConfirmar.StatusCode);
        var respuesta = await Post(entrada);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.NotNull(respuesta.Headers.Location);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, (await Post(entrada)).StatusCode);
        Assert.Equal(cuerpo, await (await Post(entrada)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(entrada with { Detalle = "Otra intención" })).StatusCode);
        using var anonimo = app.CrearCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/api/pagos/ordenes", new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
    }
}
