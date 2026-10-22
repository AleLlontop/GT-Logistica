using System.Net;
using System.Net.Http.Json;
using GT.Application.Autenticacion;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Pagos;

public class PermisosPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    public static IEnumerable<object[]> Permisos() => new[] { CodigosPermiso.PagosGenerar, CodigosPermiso.PagosConsultar,
        CodigosPermiso.PagosAnular, CodigosPermiso.PagosChequesConsultar, CodigosPermiso.PagosChequesCambiarEstado,
        CodigosPermiso.PagosBancosConsultar, CodigosPermiso.PagosBancosGestionar }.Select(p => new object[] { p });
    private static readonly (string Metodo, string Ruta, string Permiso)[] Rutas =
    [
        ("GET", "/api/pagos/referencias", CodigosPermiso.PagosGenerar),
        ("GET", "/api/pagos/referencias/proveedores", CodigosPermiso.PagosGenerar),
        ("GET", "/api/pagos/referencias/proveedores/2147483647/facturas", CodigosPermiso.PagosGenerar),
        ("GET", "/api/pagos/referencias/facturas?ids=2147483647", CodigosPermiso.PagosGenerar),
        ("POST", "/api/pagos/ordenes", CodigosPermiso.PagosGenerar),
        ("POST", "/api/pagos/ordenes/2147483647/anulacion", CodigosPermiso.PagosAnular),
        ("GET", "/api/pagos/ordenes", CodigosPermiso.PagosConsultar),
        ("GET", "/api/pagos/ordenes/proveedores", CodigosPermiso.PagosConsultar),
        ("GET", "/api/pagos/ordenes/2147483647", CodigosPermiso.PagosConsultar),
        ("GET", "/api/pagos/cheques", CodigosPermiso.PagosChequesConsultar),
        ("GET", "/api/pagos/cheques/bancos", CodigosPermiso.PagosChequesConsultar),
        ("GET", "/api/pagos/cheques/2147483647", CodigosPermiso.PagosChequesConsultar),
        ("POST", "/api/pagos/cheques/2147483647/estado", CodigosPermiso.PagosChequesCambiarEstado),
        ("GET", "/api/pagos/bancos", CodigosPermiso.PagosBancosConsultar),
        ("GET", "/api/pagos/bancos/2147483647", CodigosPermiso.PagosBancosConsultar),
        ("POST", "/api/pagos/bancos", CodigosPermiso.PagosBancosGestionar),
        ("PUT", "/api/pagos/bancos/2147483647", CodigosPermiso.PagosBancosGestionar),
        ("PUT", "/api/pagos/bancos/2147483647/activacion", CodigosPermiso.PagosBancosGestionar),
        ("PUT", "/api/pagos/bancos/2147483647/desactivacion", CodigosPermiso.PagosBancosGestionar),
    ];
    private static Task<HttpResponseMessage> Pedir(HttpClient cliente, string metodo, string ruta) => cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta)
        { Content = metodo == "GET" ? null : JsonContent.Create(new { }) });

    [Theory]
    [MemberData(nameof(Permisos))]
    public async Task PermisoAisladoNoImplicaOtrosNiAccesoLateral(string permiso)
    {
        var nombre = "mat_" + Guid.NewGuid().ToString("N")[..12];
        await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", permiso);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        foreach (var (metodo, ruta, requerido) in Rutas)
        {
            using var respuesta = await Pedir(cliente, metodo, ruta);
            if (permiso != requerido) Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            else Assert.Contains(respuesta.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound });
        }
        foreach (var ruta in new[] { "/api/caja", "/api/liquidaciones", "/api/proveedores", "/api/proveedores/facturas" })
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(ruta)).StatusCode);
        var menu = CatalogoOpcionesMenu.Autorizadas([permiso]);
        Assert.Equal(permiso is CodigosPermiso.PagosGenerar or CodigosPermiso.PagosConsultar or CodigosPermiso.PagosChequesConsultar or CodigosPermiso.PagosBancosConsultar ? 1 : 0, menu.Count());
    }

    [Fact]
    public async Task SinSesionSiempre401_YRevocacionNoConservaConsulta()
    {
        using var anonimo = app.CrearCliente();
        foreach (var (metodo, ruta, _) in Rutas) Assert.Equal(HttpStatusCode.Unauthorized, (await Pedir(anonimo, metodo, ruta)).StatusCode);
        var nombre = "rev_" + Guid.NewGuid().ToString("N")[..12];
        var usuario = await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosConsultar);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/pagos/ordenes")).StatusCode);
        await app.EnLaBaseAsync(async db => { var u = await db.Usuarios.Include(u => u.Roles).SingleAsync(u => u.Id == usuario.Id); u.Roles.Clear(); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/pagos/ordenes")).StatusCode);
    }
    [Theory]
    [InlineData("efectivo")]
    [InlineData("fletero")]
    public async Task AnularConConsultaSinGestionAjena_CompensaYRevocacionImpideReplay(string tipo)
    {
        var s = await EscenarioAnulacion.CrearAsync(app, tipo);
        var lectorNombre = "lect_" + Guid.NewGuid().ToString("N")[..10];
        await app.CrearUsuarioConPermisosAsync(lectorNombre, "Pagos.12345678", CodigosPermiso.PagosConsultar);
        using var lector = await app.CrearClienteAutenticadoAsync(lectorNombre, "Pagos.12345678");
        var ruta = $"/api/pagos/ordenes/{s.OrdenId}/anulacion";
        var entrada = new { claveOperacion = Guid.NewGuid(), motivo = "Corregir", confirmado = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await lector.PostAsJsonAsync(ruta, entrada)).StatusCode);
        await app.EnLaBaseAsync(async db => Assert.Equal(GT.Domain.Pagos.EstadoOrdenPagoProveedor.Pendiente,
            (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == s.OrdenId)).Estado));
        var nombre = "comp_" + Guid.NewGuid().ToString("N")[..10];
        var usuario = await app.CrearUsuarioConPermisosAsync(nombre, "Pagos.12345678", CodigosPermiso.PagosConsultar, CodigosPermiso.PagosAnular);
        using var cliente = await app.CrearClienteAutenticadoAsync(nombre, "Pagos.12345678");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/pagos/ordenes/{s.OrdenId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync(ruta, entrada)).StatusCode);
        foreach (var externa in new[] { "/api/caja", "/api/liquidaciones", "/api/proveedores" }) Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(externa)).StatusCode);
        await app.EnLaBaseAsync(async db =>
        {
            var u = await db.Usuarios.Include(u => u.Roles).SingleAsync(u => u.Id == usuario.Id); u.Roles.Clear(); await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync(ruta, entrada)).StatusCode);
        await app.EnLaBaseAsync(async db => Assert.Single(await db.ResultadosOperacionPago.Where(r => r.OrdenPagoProveedorId == s.OrdenId && r.Operacion == GT.Domain.Pagos.TipoOperacionPago.Anular).ToListAsync()));
    }

}
