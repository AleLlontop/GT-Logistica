using System.Net;
using System.Text.Json;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Pagos;

public class ReferenciasPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task SoloGenerar_ReferenciasMinimasPropias_SinPermisosManualesNiAbm()
    {
        var nombre = "pago_" + Guid.NewGuid().ToString("N")[..16];
        var usuario = await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosGenerar);
        var caja = await app.CrearCajaAsync(usuario.Id, saldoInicial: 50m);
        var otro = await app.UsuarioAsync();
        await app.CrearCajaAsync(otro.Id, saldoInicial: 700m);
        var inactivo = "Inactivo " + Guid.NewGuid().ToString("N");
        await app.EnLaBaseAsync(async db => { db.Bancos.Add(new Banco { Nombre = inactivo, Activo = false }); await db.SaveChangesAsync(); });
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        var respuesta = await cliente.GetAsync("/api/pagos/referencias");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(caja.Id, json.RootElement.GetProperty("cajaPropia").GetProperty("id").GetInt32());
        Assert.Equal("50.00", json.RootElement.GetProperty("cajaPropia").GetProperty("saldoDisponible").GetString());
        Assert.True(DateOnly.TryParse(json.RootElement.GetProperty("hoyArgentina").GetString(), out _));
        Assert.DoesNotContain(json.RootElement.GetProperty("bancosActivos").EnumerateArray(), b => b.GetProperty("nombre").GetString() == inactivo);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pagos/bancos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/proveedores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/liquidaciones")).StatusCode);
    }
    [Fact]
    public async Task ProveedoresPaginados_MarcaInactivoYOrdenEstableSinDuplicarEntrePaginas()
    {
        var prefijo = "Paginado " + Guid.NewGuid().ToString("N");
        for (var i = 0; i < 21; i++)
        {
            var s = await EscenarioPagos.CrearAsync(app);
            await app.EnLaBaseAsync(db => db.Proveedores.Where(x => x.Id == s.Orden.ProveedorId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.RazonSocial, prefijo).SetProperty(x => x.Activo, false)));
        }
        var cliente = await app.CrearClienteAutenticadoAsync();
        using var primera = JsonDocument.Parse(await cliente.GetStringAsync("/api/pagos/referencias/proveedores?q=" + Uri.EscapeDataString(prefijo)));
        using var segunda = JsonDocument.Parse(await cliente.GetStringAsync("/api/pagos/referencias/proveedores?pagina=2&q=" + Uri.EscapeDataString(prefijo)));
        Assert.Equal(21, primera.RootElement.GetProperty("total").GetInt32());
        var a = primera.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var b = segunda.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(20, a.Length);
        Assert.Single(b);
        Assert.All(a.Concat(b), p => Assert.False(p.GetProperty("activo").GetBoolean()));
        Assert.Equal(21, a.Concat(b).Select(p => p.GetProperty("id").GetInt32()).Distinct().Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/pagos/referencias/proveedores?pagina=0")).StatusCode);
    }

    [Fact]
    public async Task FacturasPorProveedor_PaginadasExactasYNoOfrecenPagadasNiAjenas()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var otra = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            for (var i = 0; i < 20; i++) db.FacturasProveedor.Add(new FacturaProveedor
            {
                ProveedorId = s.Orden.ProveedorId, TipoProveedorAlRegistrar = TipoProveedor.ProveedorNormal,
                ProveedorRazonSocial = s.Orden.ProveedorRazonSocial, ProveedorCuit = s.Orden.ProveedorCuit,
                Numero = "Paginada-" + i, Fecha = s.Orden.Fecha, Detalle = "Factura de prueba",
                Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = s.Orden.Fecha.AddDays(1),
            });
            await db.SaveChangesAsync();
        });
        var cliente = await app.CrearClienteAutenticadoAsync();
        using var a = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/referencias/proveedores/{s.Orden.ProveedorId}/facturas"));
        using var b = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/referencias/proveedores/{s.Orden.ProveedorId}/facturas?pagina=2"));
        var items = a.RootElement.GetProperty("items").EnumerateArray().Concat(b.RootElement.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(21, a.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(21, items.Select(f => f.GetProperty("id").GetInt32()).Distinct().Count());
        Assert.All(items, f => Assert.Equal("121.00", f.GetProperty("importeTotal").GetString()));
        Assert.DoesNotContain(items, f => f.GetProperty("id").GetInt32() == otra.Factura.Id);
        await app.EnLaBaseAsync(db => db.FacturasProveedor.Where(f => f.Id == s.Factura.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(f => f.Estado, EstadoFacturaProveedor.Pagada)));
        using var actualizada = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/referencias/proveedores/{s.Orden.ProveedorId}/facturas"));
        Assert.Equal(20, actualizada.RootElement.GetProperty("total").GetInt32());
        Assert.DoesNotContain(actualizada.RootElement.GetProperty("items").EnumerateArray(), f => f.GetProperty("id").GetInt32() == s.Factura.Id);
    }

    [Fact]
    public async Task SeleccionEntrePaginas_TotalServidorYNoDisponibles_SinReducirIntencion()
    {
        var a = await EscenarioPagos.CrearAsync(app);
        var b = await EscenarioPagos.CrearAsync(app);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        using var valida = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/referencias/facturas?ids={a.Factura.Id}"));
        Assert.Equal("121.00", valida.RootElement.GetProperty("importeTotal").GetString());
        Assert.Single(valida.RootElement.GetProperty("items").EnumerateArray());
        Assert.Empty(valida.RootElement.GetProperty("noDisponibles").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync($"/api/pagos/referencias/facturas?ids={a.Factura.Id},{b.Factura.Id}")).StatusCode);
        foreach (var ids in new[] { "", "0", "abc", $"{a.Factura.Id},{a.Factura.Id}" })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/pagos/referencias/facturas?ids=" + ids)).StatusCode);
        await app.EnLaBaseAsync(db => db.FacturasProveedor.Where(f => f.Id == a.Factura.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(f => f.Estado, EstadoFacturaProveedor.Pagada)));
        using var perdida = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/referencias/facturas?ids={a.Factura.Id},{b.Factura.Id}"));
        Assert.Equal(a.Factura.Id, Assert.Single(perdida.RootElement.GetProperty("noDisponibles").EnumerateArray()).GetInt32());
        Assert.Equal(b.Factura.Id, Assert.Single(perdida.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal("121.00", perdida.RootElement.GetProperty("importeTotal").GetString());
    }

}
