using System.Net;
using System.Text.Json;
using GT.Domain.Usuarios;
using GT.Application.Pagos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Pagos;

public class RutasConsultarOrdenesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ListadoSoloConsulta_CadenasExactasYErroresDeCampo()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var nombre = "con_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/ordenes?proveedorId={s.Orden.ProveedorId}&desde=2026-10-18&hasta=2026-10-18&estado=pendiente"));
        Assert.Equal(20, json.RootElement.GetProperty("tamanioPagina").GetInt32());
        Assert.Equal("121.00", Assert.Single(json.RootElement.GetProperty("items").EnumerateArray()).GetProperty("importeTotal").GetString());
        foreach (var query in new[] { "desde=2026-10-19&hasta=2026-10-18", "desde=no-fecha", "hasta=2026-02-30", "pagina=0", "pagina=abc", "proveedorId=0", "estado=pagada" })
        {
            var respuesta = await cliente.GetAsync("/api/pagos/ordenes?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            using var error = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(error.RootElement.GetProperty("campo").GetString()));
        }
        using var anonimo = app.CrearCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/pagos/ordenes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pagos/referencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync("/api/pagos/ordenes", new StringContent("{}"))).StatusCode);
    }
    [Fact]
    public async Task ProveedoresHistoricos_IncluyeInactivoPorIdYSoloPermisoConsultar()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(db => db.Proveedores.Where(p => p.Id == s.Orden.ProveedorId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Activo, false)));
        var nombre = "hist_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/api/pagos/ordenes/proveedores?q=" + s.Orden.ProveedorCuit));
        var proveedor = Assert.Single(json.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(s.Orden.ProveedorId, proveedor.GetProperty("id").GetInt32());
        Assert.False(proveedor.GetProperty("activo").GetBoolean());
        Assert.Equal(20, json.RootElement.GetProperty("tamanioPagina").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/pagos/ordenes/proveedores?pagina=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pagos/referencias/proveedores")).StatusCode);
    }

    [Fact]
    public async Task DetalleCompleto_PermisoIndependienteY404SinAccesoLateral()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        using var scope = app.Services.CreateScope();
        var guardado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(
            new(Guid.NewGuid(), s.Orden.ProveedorId, new(2026, 10, 17), null, [s.Factura.Id],
                [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "000-API", new(2026, 10, 17), new(2026, 10, 18), null)], 121m, true),
            s.UsuarioId, true, new(2026, 10, 18), new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
        var nombre = "det_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        using var json = JsonDocument.Parse(await cliente.GetStringAsync(guardado.Location));
        Assert.StartsWith("OPP-", json.RootElement.GetProperty("numero").GetString());
        Assert.Equal("2026-10-17", json.RootElement.GetProperty("fecha").GetString());
        Assert.EndsWith("Z", json.RootElement.GetProperty("registradaEn").GetString());
        Assert.Equal("pagada", Assert.Single(json.RootElement.GetProperty("facturas").EnumerateArray()).GetProperty("estadoActual").GetString());
        Assert.Equal("000-API", Assert.Single(json.RootElement.GetProperty("medios").EnumerateArray()).GetProperty("numeroCheque").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/pagos/ordenes/2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/pagos/ordenes/proveedores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/proveedores/facturas/{s.Factura.Id}")).StatusCode);
        var generarNombre = "gen_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(generarNombre, "Pagos.12345678", CodigosPermiso.PagosGenerar);
        using var generador = await app.CrearClienteAutenticadoAsync(generarNombre, "Pagos.12345678");
        Assert.Equal(HttpStatusCode.Forbidden, (await generador.GetAsync(guardado.Location)).StatusCode);
    }

}
