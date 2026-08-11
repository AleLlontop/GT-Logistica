using System.Net;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Autenticacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;

namespace GT.IntegrationTests.Autorizacion;

/// <summary>
/// Cubre la User Story 2. Estos escenarios necesitan una segunda cuenta con otros roles, que hasta
/// que existió el Módulo 2 no se podía crear desde la aplicación: por eso el quickstart los designa
/// como verificación automatizada y no manual.
///
/// Usan <c>GET /api/personas</c> sólo como endpoint protegido de referencia: lo que se verifica acá
/// es el comportamiento de autorización, no el padrón. Antes apuntaban al <c>GET /api/usuarios</c>
/// provisional que el Módulo 1 dejó como andamio; ese andamio se retiró al implementar el Módulo 2,
/// y cualquier endpoint con el mismo permiso <c>usuarios.gestionar</c> sirve igual.
/// </summary>
[Collection(nameof(AutorizacionTests))]
public class AutorizacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDeTrafico = "Trafico.1234";

    /// <summary>
    /// FR-007: toda funcionalidad salvo la pantalla de ingreso exige sesión activa. Sin sesión, el
    /// servidor rechaza aunque se pida la URL directamente.
    /// </summary>
    [Fact]
    public async Task RechazaSinSesion_AlPedirUrlDirecta()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/personas");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeRespuesta>();
        Assert.Equal("sesion_expirada", error!.Codigo);
    }

    /// <summary>
    /// FR-008 y SC-004: con sesión válida pero sin el permiso que exige la operación, el servidor la
    /// rechaza — sin importar que la opción nunca estuviera visible en su menú.
    /// </summary>
    [Fact]
    public async Task RechazaOperacionSinPermiso()
    {
        var usuario = await CrearUsuarioDeTraficoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDeTrafico);

        // El menú de este usuario trae las tres entradas del Módulo 3 —el primer módulo abierto a un
        // rol que no es el administrador (FR-027)—, la de flota del Módulo 4 y las tres del Módulo 5.
        // Ninguna del Módulo 2.
        var sesion = await cliente.GetFromJsonAsync<SesionDeRespuesta>("/api/auth/sesion");

        Assert.Equal(
            [
                "choferes",
                "transportistas",
                "tipos-documentacion",
                "flota",
                "viajes",
                "clientes",
                "totales",
            ],
            sesion!.OpcionesMenu.Select(opcion => opcion.Codigo));

        Assert.DoesNotContain(sesion.OpcionesMenu, opcion => opcion.Codigo is "usuarios" or "personas");

        // Módulo 4, FR-039: Tráfico gestiona la flota pero **no** el catálogo de tipos de vehículo,
        // que es sólo del administrador. Es el primer módulo con dos niveles de acceso adentro.
        Assert.DoesNotContain(sesion.OpcionesMenu, opcion => opcion.Codigo is "tipos-vehiculo");

        // Y pedir la URL igual, salteando el menú, tampoco alcanza.
        var respuesta = await cliente.GetAsync("/api/personas");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeRespuesta>();
        Assert.Equal("sin_permiso", error!.Codigo);
    }

    /// <summary>
    /// FR-006: los permisos efectivos se calculan con los roles vigentes en el momento de cada
    /// operación, no con los que el usuario tenía al ingresar.
    ///
    /// Es la prueba de fondo de la decisión de usar cookie con revalidación en vez de un token
    /// autocontenido: con un token, los roles viajarían congelados adentro y este test fallaría.
    /// </summary>
    [Fact]
    public async Task UsaRolesVigentesNoLosDelIngreso()
    {
        var usuario = await CrearUsuarioAsync(
            "supervisor",
            "Supervisor.1234",
            CodigosRol.AdministradorSistema);

        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, "Supervisor.1234");

        // Con el rol puesto, la operación pasa.
        var antes = await cliente.GetAsync("/api/personas");
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);

        // Se le quita el único rol que se lo permitía, con la sesión ya abierta.
        await app.EnLaBaseAsync(async contexto =>
        {
            var enBase = await contexto.Usuarios
                .Include(u => u.Roles)
                .FirstAsync(u => u.Id == usuario.Id);

            enBase.Roles.Clear();
            await contexto.SaveChangesAsync();
        });

        // La operación siguiente ya se rechaza, sin necesidad de volver a ingresar.
        var despues = await cliente.GetAsync("/api/personas");
        Assert.Equal(HttpStatusCode.Forbidden, despues.StatusCode);
    }

    private Task<Usuario> CrearUsuarioDeTraficoAsync() =>
        CrearUsuarioAsync("trafico", PasswordDeTrafico, CodigosRol.Trafico);

    private async Task<Usuario> CrearUsuarioAsync(
        string username,
        string password,
        string codigoRol)
    {
        return await app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();

            var rol = await contexto.Roles.FirstAsync(r => r.Codigo == codigoRol);

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(password),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });
    }
}
