using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GT.Domain.Pagos;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class AplicacionChequesFija : AplicacionDePrueba
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(s => s.AddSingleton<TimeProvider>(new RelojCheques()));
    }
    private sealed class RelojCheques : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 1, 0, 0, TimeSpan.Zero);
    }
}
public class RutasChequesPagosTests(AplicacionChequesFija app) : IClassFixture<AplicacionChequesFija>
{
    [Fact]
    public async Task CambiarEstadoSoloConPermisoPropio_SinConcederConsulta()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var id = await app.ConAlcanceAsync(async db =>
        {
            var c = new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id, NumeroCheque = Guid.NewGuid().ToString(),
                RenglonId = Guid.NewGuid(), Fecha = new(2026, 10, 18), FechaVencimiento = new(2026, 10, 20), Importe = 121m };
            db.Add(c); await db.SaveChangesAsync(); return c.Id;
        });
        var nombre = "estado_" + Guid.NewGuid().ToString("N")[..10];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosChequesCambiarEstado);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        var ruta = $"/api/pagos/cheques/{id}/estado";
        var respuesta = await cliente.PostAsJsonAsync(ruta, new { estadoAnterior = "pendiente", estadoNuevo = "entregado" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("entregado", json.RootElement.GetProperty("estado").GetString());
        Assert.Single(json.RootElement.GetProperty("historial").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pagos/cheques/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(ruta, new { estadoAnterior = "pendiente", estadoNuevo = "anulado" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(ruta, new { estadoAnterior = "entregado", estadoNuevo = "cobrado" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync(ruta, new { estadoAnterior = "otro", estadoNuevo = "cobrado" })).StatusCode);
        using var lector = await app.CrearClienteAutenticadoAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await lector.PostAsJsonAsync("/api/pagos/cheques/2147483647/estado", new { estadoAnterior = "pendiente", estadoNuevo = "entregado" })).StatusCode);
    }
    [Fact]
    public async Task ListadoAlertasConPermisoAislado_UsaDiaArgentinoYEstadosFinales()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18);
        await app.EnLaBaseAsync(async db =>
        {
            foreach (var dias in new[] { -1, 0, 30, 31 }) db.Cheques.Add(new() { OrdenPagoProveedorId = s.Orden.Id,
                BancoId = s.Banco.Id, NumeroCheque = "V-" + dias, Fecha = hoy.AddDays(-2), FechaVencimiento = hoy.AddDays(dias),
                Importe = 1m, EstadoCheque = EstadoCheque.Cobrado, RenglonId = Guid.NewGuid() });
            await db.SaveChangesAsync();
            await db.Bancos.Where(b => b.Id == s.Banco.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Activo, false));
        });
        var nombre = "cheq_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosChequesConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/cheques?bancoId={s.Banco.Id}&estado=cobrado&hastaVencimiento=2026-12-31"));
        Assert.Equal(3, json.RootElement.GetProperty("total").GetInt32());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("vencido", items[0].GetProperty("situacionVencimiento").GetString());
        Assert.Equal("proximoAvencer", items[1].GetProperty("situacionVencimiento").GetString());
        Assert.All(items, c => { Assert.Equal("cobrado", c.GetProperty("estado").GetString()); Assert.False(c.GetProperty("banco").GetProperty("activo").GetBoolean()); Assert.Equal("1.00", c.GetProperty("importe").GetString()); });
        Assert.DoesNotContain(items, c => c.GetProperty("numeroCheque").GetString() == "V-31");
        foreach (var invalido in new[] { "estado=pagado", "bancoId=0", "pagina=abc", "desdeVencimiento=2026-10-19&hastaVencimiento=2026-10-18" })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/pagos/cheques?" + invalido)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pagos/bancos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pagos/ordenes/{s.Orden.Id}")).StatusCode);
        using var anonimo = app.CrearCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/pagos/cheques")).StatusCode);
    }
    [Fact]
    public async Task BancosHistoricos_SoloUsadosIncluidosInactivos_SinAbmImplicito()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            db.Cheques.Add(new() { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id, NumeroCheque = "H-1", RenglonId = Guid.NewGuid(),
                Fecha = new(2026,10,17), FechaVencimiento = new(2026,10,18), Importe = 1m });
            await db.SaveChangesAsync();
            await db.Bancos.Where(b => b.Id == s.Banco.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Activo, false));
        });
        var nombre = "bch_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosChequesConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/api/pagos/cheques/bancos"));
        var banco = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray(), b => b.GetProperty("id").GetInt32() == s.Banco.Id);
        Assert.False(banco.GetProperty("activo").GetBoolean());
        Assert.False(banco.TryGetProperty("version", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pagos/bancos/{s.Banco.Id}")).StatusCode);
    }

    [Fact]
    public async Task DetalleConHistorialInclusoFueraAlerta_OrdenRequiereSuPermiso()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var cheque = await app.ConAlcanceAsync(async db =>
        {
            var c = new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id, NumeroCheque = "D-001",
                RenglonId = Guid.NewGuid(), Fecha = new(2026,10,17), FechaVencimiento = new(2026,11,18), Importe = 121m, EstadoCheque = EstadoCheque.Entregado };
            db.Add(c); await db.SaveChangesAsync();
            db.HistorialEstadosCheque.Add(new() { ChequeId = c.Id, EstadoAnterior = EstadoCheque.Pendiente, EstadoNuevo = EstadoCheque.Entregado,
                UsuarioId = s.UsuarioId, OcurridoEn = new(2026,10,19,1,0,0,DateTimeKind.Utc) });
            await db.SaveChangesAsync(); return c;
        });
        var nombre = "dch_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosChequesConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/cheques/{cheque.Id}"));
        Assert.Equal("fueraDelPeriodoDeAlerta", json.RootElement.GetProperty("situacionVencimiento").GetString());
        Assert.Equal("entregado", json.RootElement.GetProperty("estado").GetString());
        var cambio = Assert.Single(json.RootElement.GetProperty("historial").EnumerateArray());
        Assert.Equal("pendiente", cambio.GetProperty("estadoAnterior").GetString());
        Assert.Equal("entregado", cambio.GetProperty("estadoNuevo").GetString());
        Assert.EndsWith("Z", cambio.GetProperty("ocurridoEn").GetString());
        Assert.Equal(s.UsuarioId, cambio.GetProperty("usuario").GetProperty("id").GetInt32());
        Assert.Equal(s.Orden.Id, json.RootElement.GetProperty("ordenPagoProveedor").GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/pagos/ordenes/{s.Orden.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/pagos/cheques/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/pagos/cheques/bancos")).StatusCode);
    }

}
