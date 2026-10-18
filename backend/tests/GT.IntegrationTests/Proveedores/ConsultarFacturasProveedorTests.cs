using System.Net;
using System.Text.Json;
using GT.Domain.Proveedores;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static GT.IntegrationTests.Proveedores.EscenariosConsultaFacturasProveedor;

namespace GT.IntegrationTests.Proveedores;

public class ConsultarFacturasProveedorTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task FiltrosCombinadosExtremosAbiertosPaginacionYDecimales()
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarListadoAsync(cliente, app.EnLaBaseAsync);
    }

    [Fact]
    public async Task IdentidadHistoricaTipoLiquidacionActualYPdfProtegido()
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarDetalleAsync(cliente, app.EnLaBaseAsync);
    }
}

public static class EscenariosConsultaFacturasProveedor
{
    // Escenarios HTTP/SQL compartidos con la validación local aislada; el fixture original no cambia.
    public static async Task VerificarListadoAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase)
    {
        await OtorgarConsultaAsync(enBase);
        int proveedor = 0; var esperados = new List<(int Id, DateOnly Fecha)>();
        await enBase(async db =>
        {
            var p = EscenarioProveedores.Normal(GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico());
            var otro = EscenarioProveedores.Normal(GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico());
            db.AddRange(p, otro); await db.SaveChangesAsync(); proveedor = p.Id;
            var facturas = Enumerable.Range(0, 23).Select(i => Factura(p.Id, $"L-{i}", new(2026, 10, 10), i == 0 ? 100000000000000.01m : i == 1 ? 100000000000000.02m : 100m)).ToList();
            facturas.AddRange(new[] { 9, 10, 11, 12 }.Select(d => Factura(p.Id, $"Borde-{d}", new(2026, 10, d))));
            db.FacturasProveedor.AddRange(facturas); db.FacturasProveedor.Add(Factura(otro.Id, "De otro proveedor", new(2026, 10, 10)));
            await db.SaveChangesAsync(); esperados = facturas.Where(f => f.Fecha >= new DateOnly(2026, 10, 10) && f.Fecha <= new DateOnly(2026, 10, 11)).Select(f => (f.Id, f.Fecha)).OrderByDescending(f => f.Fecha).ThenByDescending(f => f.Id).ToList();
            await db.Proveedores.Where(x => x.Id == p.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Activo, false).SetProperty(x => x.RazonSocial, "Nombre actual distinto"));
        });
        var consulta = $"/api/proveedores/facturas?proveedorId={proveedor}&desde=2026-10-10&hasta=2026-10-11&estado=pendiente";
        using var primera = await LeerAsync(cliente, consulta);
        using var segunda = await LeerAsync(cliente, consulta + "&pagina=2");
        Assert.Equal(25, primera.RootElement.GetProperty("total").GetInt32()); Assert.Equal(20, primera.RootElement.GetProperty("tamanioPagina").GetInt32());
        var filas = primera.RootElement.GetProperty("items").EnumerateArray().Concat(segunda.RootElement.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(20, primera.RootElement.GetProperty("items").GetArrayLength()); Assert.Equal(5, segunda.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(esperados.Select(f => f.Id), filas.Select(f => f.GetProperty("id").GetInt32()));
        Assert.All(filas, f => { Assert.Equal("Proveedor histórico", f.GetProperty("proveedorRazonSocial").GetString()); Assert.Equal("pendiente", f.GetProperty("estado").GetString()); Assert.Equal(JsonValueKind.String, f.GetProperty("importeTotal").ValueKind); });
        Assert.Contains(filas, f => f.GetProperty("importeTotal").GetString() == "121000000000000.01");
        Assert.Contains(filas, f => f.GetProperty("importeTotal").GetString() == "121000000000000.02");
        foreach (var (rango, total) in new[] { ("&desde=2026-10-11", 2), ("&hasta=2026-10-09", 1), ("", 27), ("&desde=2027-01-01", 0) })
        {
            using var pagina = await LeerAsync(cliente, $"/api/proveedores/facturas?proveedorId={proveedor}" + rango); Assert.Equal(total, pagina.RootElement.GetProperty("total").GetInt32());
        }
        foreach (var invalido in new[] { "desde=2026-10-12&hasta=2026-10-10", "estado=pagada", "pagina=0", "proveedorId=0", "desde=no-fecha", "hasta=2026-02-30", "pagina=abc", "proveedorId=abc" })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync("/api/proveedores/facturas?" + invalido)).StatusCode);
        using var fueraDeRango = await LeerAsync(cliente, consulta + "&pagina=2147483647");
        Assert.Equal(0, fueraDeRango.RootElement.GetProperty("items").GetArrayLength());
    }

    public static async Task VerificarDetalleAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase)
    {
        int p = 0;
        await enBase(async db => { var normal = EscenarioProveedores.Normal(GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico(), "Razón al emitir"); db.Add(normal); await db.SaveChangesAsync(); p = normal.Id; });
        using var form = CargarFacturaProveedorTests.Formulario(p, "Con PDF", "100000000000000.01", CargarFacturaProveedorTests.Pdf());
        var respuesta = await cliente.PostAsync("/api/proveedores/facturas", form); Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        using var post = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()); var id = post.RootElement.GetProperty("id").GetInt32();
        // Quitar consultar del rol sólo en esta base de prueba permite verificar que cargar no lo implica.
        await enBase(async db => { var rol = await db.Roles.Include(r => r.Permisos).SingleAsync(r => r.Codigo == CodigosRol.AdministradorSistema); rol.Permisos = rol.Permisos.Where(p => p.Codigo != CodigosPermiso.ProveedoresFacturasConsultar).ToList(); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/proveedores/facturas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"/api/proveedores/facturas/{id}/archivo")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/proveedores/facturas")).StatusCode);
        await OtorgarConsultaAsync(enBase);
        await enBase(db => db.Proveedores.Where(x => x.Id == p).ExecuteUpdateAsync(s => s.SetProperty(x => x.RazonSocial, "Nombre actual").SetProperty(x => x.Cuit, GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico()).SetProperty(x => x.Activo, false)));
        using var detalle = await LeerAsync(cliente, $"/api/proveedores/facturas/{id}");
        Assert.Equal("Razón al emitir", detalle.RootElement.GetProperty("proveedorRazonSocial").GetString());
        Assert.Equal(post.RootElement.GetProperty("proveedorCuit").GetString(), detalle.RootElement.GetProperty("proveedorCuit").GetString());
        Assert.Equal("proveedorNormal", detalle.RootElement.GetProperty("tipoProveedorAlRegistrar").GetString()); Assert.Equal(JsonValueKind.Null, detalle.RootElement.GetProperty("liquidacion").ValueKind);
        foreach (var campo in new[] { "neto", "iva", "importeTotal" }) Assert.Equal(post.RootElement.GetProperty(campo).GetString(), detalle.RootElement.GetProperty(campo).GetString());
        var pdf = await cliente.GetAsync(detalle.RootElement.GetProperty("archivo").GetProperty("url").GetString()); Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType); Assert.Equal("inline", pdf.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Contains("factura.pdf", pdf.Content.Headers.ContentDisposition.ToString()); Assert.Equal("nosniff", Assert.Single(pdf.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(CargarFacturaProveedorTests.Pdf(), await pdf.Content.ReadAsByteArrayAsync()); Assert.False(detalle.RootElement.GetProperty("archivo").TryGetProperty("ruta", out _));
        var (fletero, liquidacion, _) = await EscenariosAsociacionLiquidacion.PrepararAsync(enBase, 121000000000000.02m);
        using var f = await EscenariosAsociacionLiquidacion.PostAsync(cliente, fletero, liquidacion, "Fletero", HttpStatusCode.Created, "100000000000000.02");
        await enBase(async db =>
        {
            await db.Liquidaciones.Where(l => l.Id == liquidacion).ExecuteUpdateAsync(s => s.SetProperty(l => l.ImporteTotal, 121000000000000.03m).SetProperty(l => l.Estado, GT.Domain.Liquidaciones.EstadoLiquidacion.Anulada).SetProperty(l => l.MotivoAnulacion, "Prueba"));
            // Cambiar el tipo actual tampoco cambia el tipo histórico de factura.
            await db.Proveedores.Where(x => x.Id == fletero).ExecuteUpdateAsync(s => s.SetProperty(x => x.Tipo, TipoProveedor.ProveedorNormal).SetProperty(x => x.TransportistaId, (int?)null).SetProperty(x => x.RazonSocial, "Ahora normal").SetProperty(x => x.Cuit, GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico()));
        });
        var fid = f.RootElement.GetProperty("id").GetInt32(); using var historica = await LeerAsync(cliente, $"/api/proveedores/facturas/{fid}");
        Assert.Equal("fletero", historica.RootElement.GetProperty("tipoProveedorAlRegistrar").GetString());
        var l = historica.RootElement.GetProperty("liquidacion"); Assert.Equal("121000000000000.02", l.GetProperty("importeAlRegistrar").GetString()); Assert.Equal("121000000000000.03", l.GetProperty("importeActual").GetString()); Assert.Equal("anulada", l.GetProperty("estadoActual").GetString());
        Assert.Equal(JsonValueKind.Null, historica.RootElement.GetProperty("archivo").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"/api/proveedores/facturas/{fid}/archivo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/api/proveedores/facturas/2147483647")).StatusCode);
    }

    private static FacturaProveedor Factura(int proveedor, string numero, DateOnly fecha, decimal neto = 100m)
    {
        var iva = decimal.Round(neto * .21m, 2, MidpointRounding.AwayFromZero);
        return new() { ProveedorId = proveedor, TipoProveedorAlRegistrar = TipoProveedor.ProveedorNormal, ProveedorRazonSocial = "Proveedor histórico", ProveedorCuit = "30712345671", Numero = numero, Fecha = fecha, Detalle = "Servicio", Neto = neto, Iva = iva, ImporteTotal = neto + iva, Cae = "CAE", FechaVencimientoCae = fecha };
    }
    public static async Task OtorgarConsultaAsync(Func<Func<GtDbContext, Task>, Task> enBase) => await enBase(async db =>
    {
        var permiso = await db.Permisos.SingleOrDefaultAsync(p => p.Codigo == CodigosPermiso.ProveedoresFacturasConsultar);
        if (permiso is null) { permiso = new() { Codigo = CodigosPermiso.ProveedoresFacturasConsultar, Modulo = "Proveedores", Descripcion = "Consultar facturas" }; db.Permisos.Add(permiso); }
        var rol = await db.Roles.Include(r => r.Permisos).SingleAsync(r => r.Codigo == CodigosRol.AdministradorSistema);
        if (!rol.Permisos.Any(p => p.Codigo == permiso.Codigo)) rol.Permisos.Add(permiso); await db.SaveChangesAsync();
    });
    private static async Task<JsonDocument> LeerAsync(HttpClient cliente, string ruta)
    {
        var r = await cliente.GetAsync(ruta); Assert.Equal(HttpStatusCode.OK, r.StatusCode); return JsonDocument.Parse(await r.Content.ReadAsStringAsync());
    }
}
