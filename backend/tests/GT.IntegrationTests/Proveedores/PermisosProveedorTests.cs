using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GT.Application.Proveedores;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Seguridad;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Proveedores;

public class PermisosProveedorTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData("anonimo")] [InlineData("ninguno")] [InlineData("gestionar")] [InlineData("cargar")]
    [InlineData("consultar")] [InlineData("otros")] [InlineData("trafico")] [InlineData("gerencia")]
    [InlineData("administracion")] [InlineData("administrador")]
    public Task MatrizRealConCookies(string perfil) => EscenariosPermisosProveedor.VerificarAsync(perfil, app.CrearCliente, app.EnLaBaseAsync);

    [Fact]
    public Task SiembraIdempotentePreservaAsignacionesYNoOtorgaATraficoNiGerencia() => EscenariosPermisosProveedor.VerificarSiembraAsync(app.EnLaBaseAsync);
}

public static class EscenariosPermisosProveedor
{
    private static readonly string[] Permisos = [CodigosPermiso.ProveedoresGestionar, CodigosPermiso.ProveedoresFacturasCargar, CodigosPermiso.ProveedoresFacturasConsultar];
    public static async Task VerificarAsync(string perfil, Func<HttpClient> crearCliente, Func<Func<GtDbContext, Task>, Task> enBase)
    {
        int proveedor = 0, factura = 0;
        await enBase(async db =>
        {
            if (!await db.EmpresaEmisora.AnyAsync()) db.EmpresaEmisora.Add(EscenarioProveedores.Emisor());
            var p = EscenarioProveedores.Normal(GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico()); db.Add(p); await db.SaveChangesAsync(); proveedor = p.Id;
        });
        // Preparar un PDF real por el mismo caso de uso y almacén; ninguna autorización se sustituye.
        using (var preparador = crearCliente())
        {
            // La preparación usa una cuenta propia con cargar; nunca necesita consultar.
            await IngresarAsync(preparador, enBase, null, [CodigosPermiso.ProveedoresFacturasCargar]);
            using var form = CargarFacturaProveedorTests.Formulario(proveedor, "Previa", "100.00", CargarFacturaProveedorTests.Pdf());
            var r = await preparador.PostAsync("/api/proveedores/facturas", form); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync()); factura = d.RootElement.GetProperty("id").GetInt32();
        }
        string? rol = perfil switch { "trafico" => CodigosRol.Trafico, "gerencia" => CodigosRol.Gerencia, "administracion" => CodigosRol.Administracion, "administrador" => CodigosRol.AdministradorSistema, _ => null };
        string[] permisos = perfil switch
        {
            "gestionar" => [Permisos[0]], "cargar" => [Permisos[1]], "consultar" => [Permisos[2]],
            "otros" => [CodigosPermiso.ChoferesGestionar, CodigosPermiso.LiquidacionesConsultar, CodigosPermiso.LiquidacionesGestionar],
            "administracion" or "administrador" => Permisos, _ => [],
        };
        using var cliente = crearCliente();
        if (perfil != "anonimo")
        {
            await IngresarAsync(cliente, enBase, rol, permisos);
            using var sesion = JsonDocument.Parse(await cliente.GetStringAsync("/api/auth/sesion"));
            var codigos = sesion.RootElement.GetProperty("permisos").EnumerateArray().Select(x => x.GetString()).Where(x => x!.StartsWith("proveedores.")).Order().ToArray();
            Assert.Equal(permisos.Where(x => x.StartsWith("proveedores.")).Order(), codigos);
            var rutas = sesion.RootElement.GetProperty("opcionesMenu").EnumerateArray().Select(x => x.GetProperty("ruta").GetString()).Where(x => x!.StartsWith("/proveedores")).Order().ToArray();
            var esperadas = new List<string>();
            if (permisos.Contains(Permisos[0])) esperadas.Add("/proveedores");
            if (permisos.Contains(Permisos[1])) esperadas.Add("/proveedores/facturas/nueva");
            if (permisos.Contains(Permisos[2])) esperadas.Add("/proveedores/facturas");
            Assert.Equal(esperadas.Order(), rutas);
        }
        var baseRuta = "/api/proveedores";
        string cuit = ""; await enBase(async db => cuit = (await db.Proveedores.AsNoTracking().SingleAsync(p => p.Id == proveedor)).Cuit!);
        var peticiones = new List<(string Nombre, string[] Alguno, HttpStatusCode Correcto, Func<Task<HttpResponseMessage>> Pedir)>
        {
            ("padrón",[Permisos[0]],HttpStatusCode.OK,()=>cliente.GetAsync(baseRuta)),
            ("proveedor por Id",[Permisos[0]],HttpStatusCode.OK,()=>cliente.GetAsync($"{baseRuta}/{proveedor}")),
            ("transportistas externos",[Permisos[0]],HttpStatusCode.OK,()=>cliente.GetAsync(baseRuta+"/transportistas-externos")),
            ("alta proveedor",[Permisos[0]],HttpStatusCode.Created,()=>cliente.PostAsJsonAsync(baseRuta,new ProveedorPeticion("proveedorNormal","Nuevo",GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico()))),
            ("edición proveedor",[Permisos[0]],HttpStatusCode.OK,()=>cliente.PutAsJsonAsync($"{baseRuta}/{proveedor}",new ProveedorPeticion("proveedorNormal","Editado",cuit,Version:1))),
            ("baja proveedor",[Permisos[0]],HttpStatusCode.OK,()=>cliente.PostAsJsonAsync($"{baseRuta}/{proveedor}/baja",new{version=2})),
            ("alta de estado",[Permisos[0]],HttpStatusCode.OK,()=>cliente.PostAsJsonAsync($"{baseRuta}/{proveedor}/alta",new{version=3})),
            ("búsqueda mínima",Permisos,HttpStatusCode.OK,()=>cliente.GetAsync(baseRuta+"/busqueda?soloActivos=true")),
            ("disponibles",[Permisos[1]],HttpStatusCode.OK,()=>cliente.GetAsync($"{baseRuta}/{proveedor}/liquidaciones-disponibles")),
            ("POST factura",[Permisos[1]],HttpStatusCode.Created,async()=> { using var f=CargarFacturaProveedorTests.Formulario(proveedor,"Sólo cargar","100.00"); return await cliente.PostAsync(baseRuta+"/facturas",f); }),
            ("listado facturas",[Permisos[2]],HttpStatusCode.OK,()=>cliente.GetAsync(baseRuta+"/facturas")),
            ("detalle factura",[Permisos[2]],HttpStatusCode.OK,()=>cliente.GetAsync($"{baseRuta}/facturas/{factura}")),
            ("PDF",[Permisos[2]],HttpStatusCode.OK,()=>cliente.GetAsync($"{baseRuta}/facturas/{factura}/archivo")),
        };
        foreach (var peticion in peticiones)
        {
            using var r = await peticion.Pedir();
            var esperado = perfil == "anonimo" ? HttpStatusCode.Unauthorized : peticion.Alguno.Any(permisos.Contains) ? peticion.Correcto : HttpStatusCode.Forbidden;
            Assert.True(r.StatusCode == esperado, $"{perfil}: {peticion.Nombre}, esperaba {(int)esperado}, obtuvo {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
            if (peticion.Nombre == "POST factura" && esperado == HttpStatusCode.Created)
            {
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                foreach (var campo in new[] { "id", "proveedorId", "proveedorRazonSocial", "proveedorCuit", "tipoProveedorAlRegistrar", "numero", "fecha", "detalle", "neto", "iva", "importeTotal", "cae", "fechaVencimientoCae", "estado", "archivo", "liquidacion" }) Assert.True(d.RootElement.TryGetProperty(campo, out _), campo);
                Assert.Equal("121.00", d.RootElement.GetProperty("importeTotal").GetString());
            }
            if (peticion.Nombre == "búsqueda mínima" && esperado == HttpStatusCode.OK)
            {
                using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                Assert.All(d.RootElement.GetProperty("items").EnumerateArray(), item => Assert.Equal(new[] { "cuit", "estado", "id", "nombreComercial", "razonSocial", "tipo" }, item.EnumerateObject().Select(p => p.Name).Order()));
            }
        }
        if (perfil is "cargar" or "consultar") Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/transportistas")).StatusCode);
    }

    public static async Task IngresarAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string? codigoRol, string[] permisos)
    {
        var nombre = "us5_" + Guid.NewGuid().ToString("N"); var password = "Temporal.1" + Guid.NewGuid().ToString("N");
        await enBase(async db =>
        {
            Rol rol;
            if (codigoRol is not null) rol = await db.Roles.SingleAsync(r => r.Codigo == codigoRol);
            else
            {
                rol = new Rol { Codigo = nombre, Nombre = "Personalizado US5" };
                foreach (var codigo in permisos)
                {
                    var permiso = await db.Permisos.SingleOrDefaultAsync(p => p.Codigo == codigo);
                    permiso ??= new Permiso { Codigo = codigo, Modulo = "Proveedores", Descripcion = "Permiso aislado de prueba" };
                    rol.Permisos.Add(permiso);
                }
            }
            var usuario = new Usuario { Username = nombre, UsernameNormalizado = nombre.ToUpperInvariant(), Email = nombre+"@example.com", EmailNormalizado = nombre+"@example.com", PasswordHash = new HasheadorPassword().Hashear(password), FechaAlta = DateTime.UtcNow, PasswordActualizadaEn = DateTime.UtcNow };
            usuario.Roles.Add(rol); db.Usuarios.Add(usuario); await db.SaveChangesAsync();
        });
        (await cliente.PostAsJsonAsync("/api/auth/login",new{username=nombre,password})).EnsureSuccessStatusCode();
    }

    public static async Task VerificarSiembraAsync(Func<Func<GtDbContext, Task>, Task> enBase) => await enBase(async db =>
    {
        var antes = await db.Roles.Include(r => r.Permisos).ToDictionaryAsync(r => r.Codigo, r => r.Permisos.Select(p => p.Codigo).ToArray());
        var sembrador = new SembradorInicial(db,new HasheadorPassword()); await sembrador.SembrarAsync(null); await sembrador.SembrarAsync(null);
        db.ChangeTracker.Clear(); var roles = await db.Roles.Include(r => r.Permisos).ToListAsync();
        Assert.Equal(3, await db.Permisos.CountAsync(p => Permisos.Contains(p.Codigo)));
        foreach (var rol in roles)
        {
            Assert.All(antes[rol.Codigo], codigo => Assert.Contains(rol.Permisos, p => p.Codigo == codigo));
            Assert.Equal(rol.Permisos.Count, rol.Permisos.Select(p => p.Id).Distinct().Count());
            var esperados = rol.Codigo is CodigosRol.Administracion or CodigosRol.AdministradorSistema ? Permisos : antes[rol.Codigo].Where(Permisos.Contains).ToArray();
            Assert.Equal(esperados.Order(), rol.Permisos.Where(p => Permisos.Contains(p.Codigo)).Select(p => p.Codigo).Order());
            if (rol.Codigo is CodigosRol.Trafico or CodigosRol.Gerencia) Assert.DoesNotContain(rol.Permisos, p => Permisos.Contains(p.Codigo));
        }
    });
}
