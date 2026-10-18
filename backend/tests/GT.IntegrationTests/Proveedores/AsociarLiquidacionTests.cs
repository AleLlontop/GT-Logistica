using System.Globalization;
using System.Net;
using System.Text.Json;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static GT.IntegrationTests.Proveedores.EscenariosAsociacionLiquidacion;

namespace GT.IntegrationTests.Proveedores;

public class AsociarLiquidacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task SeleccionPertenenciaEstadoDisponibilidadYAtomicidad()
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarReglasAsync(cliente, app.EnLaBaseAsync, app.RutaDeArchivos);
    }

    [Theory]
    [InlineData("100000000000000.01", "121000000000000.01")]
    [InlineData("100000000000000.02", "121000000000000.02")]
    public async Task CentavosGrandesSonCadenasExactas(string neto, string total)
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarImportesAsync(cliente, app.EnLaBaseAsync, neto, total);
    }
}

public static class EscenariosAsociacionLiquidacion
{

    // El escenario acepta conexiones y cliente reales para poder ejecutarlo también en SQL local
    // aislado, sin reemplazar ni reconfigurar el fixture original del compose.
    public static async Task VerificarReglasAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string ruta)
    {
        var (proveedor, liquidacion, transportista) = await PrepararAsync(enBase);
        int normal = 0, ajena = 0;
        await enBase(async db =>
        {
            var otro = EscenarioProveedores.Externo(PadronProveedorTests.Cuit(904));
            var p = EscenarioProveedores.Normal(PadronProveedorTests.Cuit(905));
            var l = EscenarioProveedores.Pendiente(otro, 121);
            db.AddRange(p, l); await db.SaveChangesAsync(); normal = p.Id; ajena = l.Id;
        });
        Assert.Empty(await DisponiblesAsync(cliente, normal));
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/proveedores/2147483647/liquidaciones-disponibles")).StatusCode);
        await PostAsync(cliente, proveedor, null, "Sin selección", HttpStatusCode.BadRequest);
        await PostAsync(cliente, normal, liquidacion, "Normal", HttpStatusCode.BadRequest);
        await PostAsync(cliente, proveedor, ajena, "Ajena", HttpStatusCode.Conflict);
        var antes = Archivos(ruta);
        using (var distinto = await PostAsync(cliente, proveedor, liquidacion, "Distinta", HttpStatusCode.Conflict, "101.00", true))
        {
            Assert.Equal("122.21", distinto.RootElement.GetProperty("importeTotalFactura").GetString());
            Assert.Equal("121.00", distinto.RootElement.GetProperty("importeTotalLiquidacion").GetString());
        }
        Assert.Equal(antes, Archivos(ruta));
        await enBase(async db => Assert.False(await db.FacturasProveedor.AnyAsync(f => f.ProveedorId == proveedor)));
        // Pago parcial: el saldo es 120, pero se compara el total de 121.
        await enBase(db => db.Liquidaciones.Where(l => l.Id == liquidacion).ExecuteUpdateAsync(s => s.SetProperty(l => l.ImportePagado, 1m)));
        Assert.Equal("121.00", Assert.Single(await DisponiblesAsync(cliente, proveedor)).GetProperty("importeTotal").GetString());
        using (var creada = await PostAsync(cliente, proveedor, liquidacion, "Correcta", HttpStatusCode.Created, pdf: true))
        {
            var l = creada.RootElement.GetProperty("liquidacion");
            Assert.Equal("121.00", l.GetProperty("importeAlRegistrar").GetString());
            Assert.Equal("121.00", l.GetProperty("importeActual").GetString());
            Assert.StartsWith("LQ-", l.GetProperty("numero").GetString());
            Assert.Equal("10/2026", l.GetProperty("periodo").GetString());
        }
        Assert.Equal(antes + 1, Archivos(ruta));
        Assert.Empty(await DisponiblesAsync(cliente, proveedor));
        await PostAsync(cliente, proveedor, liquidacion, "Ocupada", HttpStatusCode.Conflict, pdf: true);
        Assert.Equal(antes + 1, Archivos(ruta));
        await enBase(async db =>
        {
            var l = await db.Liquidaciones.SingleAsync(l => l.Id == liquidacion);
            Assert.Equal(EstadoLiquidacion.Pendiente, l.Estado); Assert.Equal(1m, l.ImportePagado);
            Assert.Equal(121m, l.ImporteTotal);
        });
        foreach (var estado in new[] { EstadoLiquidacion.Pagada, EstadoLiquidacion.Anulada })
        {
            var (p, l, _) = await PrepararAsync(enBase);
            await enBase(db => db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Estado, estado).SetProperty(x => x.ImportePagado, estado == EstadoLiquidacion.Pagada ? 121m : 0m)
                .SetProperty(x => x.MotivoAnulacion, estado == EstadoLiquidacion.Anulada ? "Prueba" : null)));
            Assert.Empty(await DisponiblesAsync(cliente, p));
            await PostAsync(cliente, p, l, "No pendiente", HttpStatusCode.Conflict);
        }
        var (sin, _, _) = await PrepararAsync(enBase, crearLiquidacion: false);
        Assert.Empty(await DisponiblesAsync(cliente, sin));
        await PostAsync(cliente, sin, null, "Sin disponibles", HttpStatusCode.BadRequest);
        var (propio, propia, t) = await PrepararAsync(enBase);
        await enBase(async db =>
        {
            var cuit = await db.Transportistas.Where(x => x.Id == t).Select(x => x.Cuit).SingleAsync();
            await db.EmpresaEmisora.ExecuteUpdateAsync(s => s.SetProperty(e => e.Cuit, cuit));
        });
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.GetAsync($"/api/proveedores/{propio}/liquidaciones-disponibles")).StatusCode);
        await PostAsync(cliente, propio, propia, "Propia", HttpStatusCode.Conflict);
    }

    public static async Task VerificarImportesAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string neto, string total)
    {
        var (p, l, _) = await PrepararAsync(enBase, decimal.Parse(total, CultureInfo.InvariantCulture));
        Assert.Equal(total, Assert.Single(await DisponiblesAsync(cliente, p)).GetProperty("importeTotal").GetString());
        using var creada = await PostAsync(cliente, p, l, "Grande", HttpStatusCode.Created, neto);
        Assert.Equal(total, creada.RootElement.GetProperty("liquidacion").GetProperty("importeAlRegistrar").GetString());
        Assert.Equal(total, creada.RootElement.GetProperty("liquidacion").GetProperty("importeActual").GetString());
        await enBase(db => db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImporteTotal, decimal.Parse(total, CultureInfo.InvariantCulture) + .01m)));
        await enBase(async db =>
        {
            var detalle = (await new RepositorioFacturasProveedor(db).ObtenerAsync(creada.RootElement.GetProperty("id").GetInt32(), default))!;
            Assert.Equal(decimal.Parse(total, CultureInfo.InvariantCulture), detalle.Liquidacion!.ImporteAlRegistrar);
            Assert.Equal(detalle.Liquidacion.ImporteAlRegistrar + .01m, detalle.Liquidacion.ImporteActual);
        });
        var (otro, otra, _) = await PrepararAsync(enBase, decimal.Parse(total, CultureInfo.InvariantCulture) + .01m);
        using var conflicto = await PostAsync(cliente, otro, otra, "Grande distinta", HttpStatusCode.Conflict, neto);
        Assert.Equal(total, conflicto.RootElement.GetProperty("importeTotalFactura").GetString());
        Assert.Equal((decimal.Parse(total, CultureInfo.InvariantCulture) + .01m).ToString("F2", CultureInfo.InvariantCulture), conflicto.RootElement.GetProperty("importeTotalLiquidacion").GetString());
    }

    public static async Task<(int Proveedor, int Liquidacion, int Transportista)> PrepararAsync(Func<Func<GtDbContext, Task>, Task> enBase, decimal total = 121m, bool crearLiquidacion = true)
    {
        (int, int, int) resultado = default;
        await enBase(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            // Identidad distinta por escenario, incluso si se repiten carreras sobre la misma base.
            var t = EscenarioProveedores.Externo(GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico());
            var p = EscenarioProveedores.Fletero(t); db.Proveedores.Add(p);
            var l = EscenarioProveedores.Pendiente(t, total); if (crearLiquidacion) db.Liquidaciones.Add(l);
            await db.SaveChangesAsync(); resultado = (p.Id, l.Id, t.Id);
        });
        return resultado;
    }
    public static async Task<JsonElement[]> DisponiblesAsync(HttpClient cliente, int p)
    {
        var r = await cliente.GetAsync($"/api/proveedores/{p}/liquidaciones-disponibles"); Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync()); return doc.RootElement.EnumerateArray().Select(x => x.Clone()).ToArray();
    }
    public static async Task<JsonDocument> PostAsync(HttpClient cliente, int p, int? l, string numero, HttpStatusCode esperado, string neto = "100.00", bool pdf = false)
    {
        using var form = CargarFacturaProveedorTests.Formulario(p, numero, neto, pdf ? CargarFacturaProveedorTests.Pdf() : null);
        if (l.HasValue) form.Add(new StringContent(l.ToString()!), "liquidacionId");
        var r = await cliente.PostAsync("/api/proveedores/facturas", form); Assert.Equal(esperado, r.StatusCode);
        return JsonDocument.Parse(await r.Content.ReadAsStringAsync());
    }
    public static int Archivos(string ruta) => Directory.Exists(ruta) ? Directory.EnumerateFiles(ruta, "*", SearchOption.AllDirectories).Count() : 0;
}
