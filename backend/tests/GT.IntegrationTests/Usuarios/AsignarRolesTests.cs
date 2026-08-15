using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 4: asignación de roles y consulta de permisos.
///
/// Cubre FR-018 (los roles quedan exactamente como se enviaron), FR-001 (al menos uno), FR-019 (no
/// dejar al sistema sin administradores) y FR-010 (los permisos se leen, no se editan).
/// </summary>
public class AsignarRolesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Roles(params string[] codigos) => new { roles = codigos };

    [Fact]
    public async Task Deja_LosRolesExactamenteComoSeEnviaron_NiMasNiMenos()
    {
        // FR-018: es un reemplazo, no un agregado. El usuario arranca con Gerencia y termina sólo
        // con los dos que se mandan.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("cambia.roles", CodigosRol.Gerencia);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/roles",
            Roles(CodigosRol.Trafico, CodigosRol.Administracion));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);
        var codigos = recargado.Roles.Select(rol => rol.Codigo).OrderBy(codigo => codigo).ToList();

        Assert.Equal([CodigosRol.Administracion, CodigosRol.Trafico], codigos);
        Assert.DoesNotContain(CodigosRol.Gerencia, codigos);
    }

    [Fact]
    public async Task Conserva_UnRolQueYaTenia_CuandoVuelveAEnviarse()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("conserva.rol", CodigosRol.Trafico);

        await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/roles",
            Roles(CodigosRol.Trafico, CodigosRol.Gerencia));

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        Assert.Equal(2, recargado.Roles.Count);
        Assert.Contains(recargado.Roles, rol => rol.Codigo == CodigosRol.Trafico);
    }

    [Fact]
    public async Task Rechaza_GuardarSinNingunRolMarcado()
    {
        // FR-001: todo usuario tiene que tener al menos un rol.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("sin.ningun.rol");

        var respuesta = await cliente.PutAsJsonAsync($"/api/usuarios/{usuario.Id}/roles", Roles());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("sin_roles", error!.Codigo);

        // Y el usuario conserva los que tenía.
        var recargado = await app.RecargarUsuarioAsync(usuario.Id);
        Assert.NotEmpty(recargado.Roles);
    }

    [Fact]
    public async Task Rechaza_QuitarleElRolDeAdministrador_AlUnicoAdministradorActivo()
    {
        // FR-019 y SC-005, por el camino de la desasignación de rol.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var administrador = await app.ObtenerAdministradorAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{administrador.Id}/roles",
            Roles(CodigosRol.Gerencia));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("ultimo_administrador", error!.Codigo);

        // Sigue siendo administrador.
        var recargado = await app.RecargarUsuarioAsync(administrador.Id);
        Assert.Contains(recargado.Roles, rol => rol.Codigo == CodigosRol.AdministradorSistema);
    }

    [Fact]
    public async Task Permite_QuitarleElRol_CuandoQuedaOtroAdministradorActivo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var segundo = await app.CrearUsuarioAsync("otro.admin.roles", CodigosRol.AdministradorSistema);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{segundo.Id}/roles",
            Roles(CodigosRol.Gerencia));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_UnCodigoDeRolInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("rol.inventado");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/roles",
            Roles("rol_que_no_existe"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Lista_LosCuatroRoles_ConSusPermisosAgrupadosPorModulo()
    {
        // FR-010: sólo lectura.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var roles = await cliente.GetFromJsonAsync<List<RolLeido>>("/api/roles");

        Assert.NotNull(roles);
        Assert.Equal(4, roles.Count);

        var administrador = roles.Single(rol => rol.Codigo == CodigosRol.AdministradorSistema);

        // Se busca el módulo por nombre en vez de asumir que hay uno solo: cada módulo nuevo suma el
        // suyo al catálogo, y este test no tiene por qué romperse cada vez que eso pasa.
        var usuarios = administrador.PermisosPorModulo.Single(modulo => modulo.Modulo == "Usuarios");

        Assert.Contains(usuarios.Permisos, permiso => permiso.Codigo == CodigosPermiso.UsuariosGestionar);
    }

    [Fact]
    public async Task Gerencia_RecibeSuPrimerPermisoConElModulo5()
    {
        // Este test verificaba que un rol **sin ningún permiso** devolviera la lista vacía sin ser un
        // error. Con el Módulo 5 ya no queda ningún rol así: `viajes.consultar` lo reciben los cuatro,
        // porque mirar el listado, la ficha y los totales no exige poder operar (Módulo 5, FR-051).
        //
        // Gerencia era el último ejemplo disponible, así que el test pasa a afirmar lo que ahora es
        // cierto. El **Módulo 6 sumó el segundo**: `facturacion.consultar`, con el mismo criterio —mirar
        // la cobranza no exige poder facturar—. Lo que sigue siendo verdad, y es lo que este test
        // protege, es que Gerencia **no recibe ningún permiso de gestión** (Módulo 6, FR-066).
        var cliente = await app.CrearClienteAutenticadoAsync();

        var roles = await cliente.GetFromJsonAsync<List<RolLeido>>("/api/roles");

        var gerencia = roles!.Single(rol => rol.Codigo == CodigosRol.Gerencia);

        var viajes = gerencia.PermisosPorModulo.Single(modulo => modulo.Modulo == "Viajes");

        Assert.Equal(
            [CodigosPermiso.ViajesConsultar],
            viajes.Permisos.Select(permiso => permiso.Codigo));

        var facturacion = gerencia.PermisosPorModulo.Single(modulo => modulo.Modulo == "Facturación");

        Assert.Equal(
            [CodigosPermiso.FacturacionConsultar],
            facturacion.Permisos.Select(permiso => permiso.Codigo));

        // Dos módulos y nada más: ningún permiso de gestión, ni de anulación.
        Assert.Equal(2, gerencia.PermisosPorModulo.Count);
    }

    [Fact]
    public async Task Trafico_RecibeElPermisoDelModuloDeChoferes()
    {
        // Es el primer permiso que un rol distinto del administrador recibe en todo el sistema
        // (FR-027 del Módulo 3): hasta acá, sólo el administrador habilitaba algo.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var roles = await cliente.GetFromJsonAsync<List<RolLeido>>("/api/roles");

        var trafico = roles!.Single(rol => rol.Codigo == CodigosRol.Trafico);

        var choferes = Assert.Single(trafico.PermisosPorModulo, modulo => modulo.Modulo == "Choferes");
        Assert.Contains(choferes.Permisos, permiso => permiso.Codigo == CodigosPermiso.ChoferesGestionar);

        // Desde el Módulo 4, Tráfico suma la gestión de la flota. **No** suma el catálogo de tipos de
        // vehículo, que es sólo del administrador: es el primer módulo con dos niveles de acceso
        // adentro (FR-039, research §7).
        var flota = Assert.Single(trafico.PermisosPorModulo, modulo => modulo.Modulo == "Flota");
        var permisoDeFlota = Assert.Single(flota.Permisos);

        Assert.Equal(CodigosPermiso.FlotaGestionar, permisoDeFlota.Codigo);
    }

    [Fact]
    public async Task Rechaza_ElAcceso_ParaUnUsuarioSinElPermisoDeGestion()
    {
        var cliente = await app.CrearClienteComoAsync("gerencia.roles", CodigosRol.Gerencia);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/roles")).StatusCode);
    }

    [Fact]
    public async Task Devuelve_NoEncontrado_CuandoElUsuarioNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/usuarios/999999/roles",
            Roles(CodigosRol.Trafico));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record RolLeido(string Codigo, string Nombre, IReadOnlyList<ModuloLeido> PermisosPorModulo);

    private record ModuloLeido(string Modulo, IReadOnlyList<PermisoLeido> Permisos);

    private record PermisoLeido(string Codigo, string Descripcion);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
