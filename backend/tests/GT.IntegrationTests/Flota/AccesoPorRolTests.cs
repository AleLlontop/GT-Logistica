using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-039: el módulo tiene <b>dos permisos</b>, y es la primera vez que la spec distingue niveles de
/// acceso adentro de un módulo (quickstart paso 1).
///
/// <list type="bullet">
///   <item><c>flota.gestionar</c> — Tráfico y Administrador del sistema.</item>
///   <item><c>flota.tipos.gestionar</c> — sólo Administrador del sistema.</item>
/// </list>
///
/// Se verifica contra el servidor y no contra el menú: la autorización se evalúa acá, sin importar si
/// la opción estaba visible u oculta en el cliente.
/// </summary>
public class AccesoPorRolTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDePrueba = "Flota.1234";

    [Fact]
    public async Task Trafico_LlegaAlPadronDeFlota()
    {
        var usuario = await app.CrearUsuarioConRolAsync(
            "trafico-flota",
            PasswordDePrueba,
            CodigosRol.Trafico);

        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDePrueba);

        var respuesta = await cliente.GetAsync("/api/flota/vehiculos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// Tráfico <b>lee</b> el catálogo —lo necesita para el selector del formulario de vehículo— pero
    /// no puede administrarlo: ese ABM es sólo del administrador (FR-039, research §7).
    /// </summary>
    [Fact]
    public async Task Trafico_LeeElCatalogoDeTipos_PeroNoLoAdministra()
    {
        var usuario = await app.CrearUsuarioConRolAsync(
            "trafico-tipos",
            PasswordDePrueba,
            CodigosRol.Trafico);

        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDePrueba);

        var lectura = await cliente.GetAsync("/api/flota/tipos-vehiculo");
        Assert.Equal(HttpStatusCode.OK, lectura.StatusCode);

        var alta = await cliente.PostAsJsonAsync(
            "/api/flota/tipos-vehiculo",
            new { nombre = "Tipo que Tráfico no puede crear" });

        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);

        // Reactivar un tipo dado de baja también es administrar el catálogo (FR-009, FR-039).
        var inactivo = await app.CrearTipoVehiculoAsync(
            nombre: "Tipo que Tráfico no puede reactivar",
            activo: false);

        var reactivacion = await cliente.PostAsJsonAsync(
            $"/api/flota/tipos-vehiculo/{inactivo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, reactivacion.StatusCode);
    }

    /// <summary>Un rol sin ninguno de los dos permisos recibe 403 en los dos grupos.</summary>
    [Theory]
    [InlineData("/api/flota/vehiculos")]
    [InlineData("/api/flota/tipos-vehiculo")]
    [InlineData("/api/flota/vencimientos")]
    public async Task Un_RolSinPermisos_RecibeProhibido(string ruta)
    {
        var usuario = await app.CrearUsuarioConRolAsync(
            $"gerencia-{ruta.GetHashCode():X}",
            PasswordDePrueba,
            CodigosRol.Gerencia);

        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDePrueba);

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>El administrador tiene los dos permisos y llega a todo.</summary>
    [Fact]
    public async Task El_Administrador_LlegaALosDosGrupos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/flota/vehiculos")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await cliente.GetAsync("/api/flota/tipos-vehiculo")).StatusCode);
    }

    /// <summary>
    /// SC-011: la descarga de un adjunto exige el mismo permiso que el resto, aunque sea una lectura
    /// de archivo. Sin sesión no se llega ni al 404.
    /// </summary>
    [Fact]
    public async Task La_DescargaDeAdjuntos_ExigeSesion()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.GetAsync("/api/flota/documentacion/1/archivo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
