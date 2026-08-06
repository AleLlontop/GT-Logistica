using System.Net;
using System.Net.Http.Json;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Autenticacion;

public class LoginTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// FR-005 y SC-002: todo ingreso exitoso deja registrada la fecha y hora reales en
    /// `ultimoAcceso`.
    ///
    /// Este test es la verificación designada por el quickstart: hasta que exista el Módulo 2 no hay
    /// ninguna pantalla que muestre ese campo, así que no se puede comprobar operando la aplicación.
    /// </summary>
    [Fact]
    public async Task ActualizaUltimoAcceso_TrasIngresoExitoso()
    {
        var antesDelIngreso = DateTime.UtcNow.AddSeconds(-5);
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = SembradorInicial.UsernameAdministrador,
            password = AplicacionDePrueba.PasswordAdministrador,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var administrador = await app.ObtenerAdministradorAsync();

        Assert.NotNull(administrador.UltimoAcceso);
        Assert.InRange(
            administrador.UltimoAcceso!.Value,
            antesDelIngreso,
            DateTime.UtcNow.AddSeconds(5));
    }

    /// <summary>
    /// FR-012: el username se normaliza al validar credenciales, igual que al crear la cuenta, así
    /// que mayúsculas y espacios de más no impiden ingresar.
    /// </summary>
    [Fact]
    public async Task PermiteIngresar_ConMayusculasYEspaciosDeMas()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = "  ADMIN  ",
            password = AplicacionDePrueba.PasswordAdministrador,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-020 y SC-001: tras ingresar, la sesión trae el usuario, sus roles y las opciones de menú
    /// que sus roles autorizan.
    /// </summary>
    [Fact]
    public async Task DevuelveMenuDelAdministrador_TrasIngresoExitoso()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = SembradorInicial.UsernameAdministrador,
            password = AplicacionDePrueba.PasswordAdministrador,
        });

        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionDeRespuesta>();

        Assert.NotNull(sesion);
        Assert.Equal("admin", sesion!.Username);
        Assert.Contains(sesion.Roles, rol => rol.Codigo == "administrador_sistema");
        Assert.Contains(sesion.OpcionesMenu, opcion => opcion.Codigo == "usuarios");
    }

    /// <summary>FR-011: el servidor tampoco acepta la petición con algún campo vacío.</summary>
    [Fact]
    public async Task RechazaPeticion_ConCamposVacios()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = "",
            password = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeRespuesta>();
        Assert.Equal("datos_incompletos", error!.Codigo);
    }

    /// <summary>FR-019: la instalación no crea ninguna cuenta más allá del administrador inicial.</summary>
    [Fact]
    public async Task NoSiembraNingunaOtraCuenta()
    {
        var cantidad = await app.ConAlcanceAsync(contexto => contexto.Usuarios.CountAsync());

        Assert.Equal(1, cantidad);
    }
}

public record SesionDeRespuesta(
    string Username,
    List<RolDeRespuesta> Roles,
    List<OpcionMenuDeRespuesta> OpcionesMenu);

public record RolDeRespuesta(string Codigo, string Nombre);

public record OpcionMenuDeRespuesta(string Codigo, string Etiqueta, string Ruta);

public record ErrorDeRespuesta(string Codigo, string Mensaje);
