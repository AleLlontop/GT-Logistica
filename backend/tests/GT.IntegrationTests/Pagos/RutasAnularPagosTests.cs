using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class RutasAnularPagosTests(AplicacionChequesFija app) : IClassFixture<AplicacionChequesFija>
{
    [Fact]
    public async Task PeticionIntencional200_ReplayIdempotente_ConsultaPreviaIndependiente()
    {
        var s = await EscenarioPagos.CrearAsync(app); int id;
        using (var scope = app.Services.CreateScope())
        {
            var generar = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(new(Guid.NewGuid(), s.Orden.ProveedorId, new(2026, 10, 18), null,
                [s.Factura.Id], [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "original", new(2026, 10, 18), new(2026, 10, 20), null)], 121m, true), s.UsuarioId, true, new(2026, 10, 18), new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
            using var json = JsonDocument.Parse(generar.Json); id = json.RootElement.GetProperty("id").GetInt32();
        }
        var nombre = "anul_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosAnular);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pagos/ordenes/{id}")).StatusCode);
        var ruta = $"/api/pagos/ordenes/{id}/anulacion"; var clave = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, new { claveOperacion = clave, motivo = "Motivo" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, new { claveOperacion = clave, motivo = " ", confirmado = true })).StatusCode);
        var entrada = new { claveOperacion = clave, motivo = "Corrección", confirmado = true };
        var respuesta = await cliente.PostAsJsonAsync(ruta, entrada); Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var original = await respuesta.Content.ReadAsStringAsync();
        using var jsonRespuesta = JsonDocument.Parse(original); Assert.Equal("anulada", jsonRespuesta.RootElement.GetProperty("estado").GetString());
        var replay = await cliente.PostAsJsonAsync(ruta, entrada); Assert.Equal(HttpStatusCode.OK, replay.StatusCode); Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(ruta, new { claveOperacion = Guid.NewGuid(), motivo = "Corrección", confirmado = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(ruta, new { claveOperacion = clave, motivo = "Otro motivo", confirmado = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsJsonAsync("/api/pagos/ordenes/2147483647/anulacion", new { claveOperacion = Guid.NewGuid(), motivo = "Motivo", confirmado = true })).StatusCode);
        using var anonimo = app.CrearCliente(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsJsonAsync(ruta, entrada)).StatusCode);
    }
}
