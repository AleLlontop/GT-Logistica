using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Los dos permisos del módulo, endpoint por endpoint y rol por rol (FR-041 a FR-044, SC-010).
///
/// <b>Ocultar el botón es una cortesía; la restricción es ésta.</b> Las escrituras se invocan con pedidos
/// que no cambian nada —un aprobado que no se puede resolver, una anulación sin <c>confirmado</c>, una
/// persona inexistente— para que quien sí tiene el permiso no deje la base distinta.
/// </summary>
public class PermisosAdelantosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Password = "Permisos.1234";

    private static readonly string[] Lecturas = ["GET /", "GET /{id}", "GET /personas"];

    [Fact]
    public async Task Sin_Sesion_Recibe401EnLosOchoEndpoints()
    {
        var id = await UnAdelantoAprobadoAsync();
        var anonimo = app.CrearCliente();

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            Assert.True(
                (await pedir(anonimo)).StatusCode == HttpStatusCode.Unauthorized,
                $"{nombre} tendría que responder 401 sin sesión");
        }
    }

    [Fact]
    public async Task Trafico_Recibe403EnLosOchoEndpoints()
    {
        var id = await UnAdelantoAprobadoAsync();
        var trafico = await ConRolAsync("trafico_adelantos", CodigosRol.Trafico);

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            Assert.True(
                (await pedir(trafico)).StatusCode == HttpStatusCode.Forbidden,
                $"{nombre} tendría que responder 403 a Tráfico");
        }
    }

    [Fact]
    public async Task Gerencia_LeeLasTresLecturas_YRecibe403EnLoDemas()
    {
        var id = await UnAdelantoAprobadoAsync();
        var gerencia = await ConRolAsync("gerencia_adelantos", CodigosRol.Gerencia);

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            var estado = (await pedir(gerencia)).StatusCode;

            if (Lecturas.Contains(nombre))
            {
                Assert.True(estado == HttpStatusCode.OK, $"{nombre} tendría que responder 200 a Gerencia");
            }
            else
            {
                Assert.True(estado == HttpStatusCode.Forbidden, $"{nombre} tendría que responder 403 a Gerencia");
            }
        }
    }

    [Theory]
    [InlineData("administracion_adelantos", CodigosRol.Administracion)]
    [InlineData("sistemas_adelantos", CodigosRol.AdministradorSistema)]
    public async Task Administracion_YAdministrador_AccedenATodo(string username, string rol)
    {
        var id = await UnAdelantoAprobadoAsync();
        var cliente = await ConRolAsync(username, rol);

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            var estado = (await pedir(cliente)).StatusCode;

            Assert.True(
                estado is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                $"{nombre} respondió {(int)estado} a {rol}");
        }

        // Ningún pedido cambió nada.
        var releido = (await app.RecargarAdelantoAsync(id))!;
        Assert.Equal(EstadoAdelanto.Aprobado, releido.Estado);
        Assert.Equal(2, releido.Cambios.Count);
    }

    [Fact]
    public async Task El_Menu_TraeLasDosEntradas_SoloConsultar_ONinguna_SegunElRol()
    {
        var administracion = await LeerMenuAsync(await ConRolAsync("menu_admin_adelantos", CodigosRol.Administracion));
        Assert.Contains("consultar-adelanto", administracion);
        Assert.Contains("registrar-adelanto", administracion);

        var gerencia = await LeerMenuAsync(await ConRolAsync("menu_gerencia_adelantos", CodigosRol.Gerencia));
        Assert.Contains("consultar-adelanto", gerencia);
        Assert.DoesNotContain("registrar-adelanto", gerencia);

        var trafico = await LeerMenuAsync(await ConRolAsync("menu_trafico_adelantos", CodigosRol.Trafico));
        Assert.DoesNotContain("consultar-adelanto", trafico);
        Assert.DoesNotContain("registrar-adelanto", trafico);
    }

    private static (string Nombre, Func<HttpClient, Task<HttpResponseMessage>> Pedir)[] Endpoints(int id) =>
    [
        ("GET /", cliente => cliente.GetAsync("/api/adelantos")),
        ("GET /{id}", cliente => cliente.GetAsync($"/api/adelantos/{id}")),
        ("GET /personas", cliente => cliente.GetAsync("/api/adelantos/personas")),
        ("GET /beneficiarios", cliente => cliente.GetAsync("/api/adelantos/beneficiarios?tipo=empleado")),
        ("POST /", cliente => cliente.RegistrarAsync("empleado", int.MaxValue)),
        ("POST /{id}/aprobacion", cliente => cliente.AprobarAsync(id)),
        ("POST /{id}/rechazo", cliente => cliente.RechazarAsync(id, "Ya está aprobado: no cambia nada.", confirmado: true)),
        ("POST /{id}/anulacion", cliente => cliente.AnularAsync(id, "Sin confirmar: no cambia nada.", confirmado: null)),
    ];

    private async Task<int> UnAdelantoAprobadoAsync()
    {
        var persona = await app.EmpleadoAsync();

        return (await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado)).Id;
    }

    private async Task<HttpClient> ConRolAsync(string username, string rol)
    {
        await app.CrearUsuarioConRolViajesAsync(username, Password, rol);

        return await app.CrearClienteAutenticadoAsync(username, Password);
    }

    private static async Task<List<string>> LeerMenuAsync(HttpClient cliente)
    {
        var sesion = await cliente.GetFromJsonAsync<SesionLeida>("/api/auth/sesion");

        return [.. sesion!.OpcionesMenu.Select(opcion => opcion.Codigo)];
    }
}
