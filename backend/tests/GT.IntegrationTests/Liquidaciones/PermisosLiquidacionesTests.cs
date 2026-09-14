using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Los dos permisos del módulo, endpoint por endpoint y rol por rol (FR-063 a FR-066, SC-012).
///
/// <b>Ocultar el botón es una cortesía; la restricción es ésta.</b> Las escrituras se invocan con cuerpos
/// que no cambian nada —sin <c>confirmado</c>, con viajes ajenos— para que quien sí tiene el permiso no
/// deje la base distinta.
/// </summary>
public class PermisosLiquidacionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Password = "Permisos.1234";

    private static readonly string[] Lecturas = ["GET /", "GET /{id}", "GET /transportistas"];

    [Fact]
    public async Task Sin_Sesion_Recibe401EnLosOchoEndpoints()
    {
        var (id, transportistaId) = await UnaLiquidacionAsync();
        var anonimo = app.CrearCliente();

        foreach (var (nombre, pedir) in Endpoints(id, transportistaId))
        {
            Assert.True(
                (await pedir(anonimo)).StatusCode == HttpStatusCode.Unauthorized,
                $"{nombre} tendría que responder 401 sin sesión");
        }
    }

    [Fact]
    public async Task Trafico_Recibe403EnLosOchoEndpoints()
    {
        var (id, transportistaId) = await UnaLiquidacionAsync();
        var trafico = await ConRolAsync("trafico_liquidaciones", CodigosRol.Trafico);

        foreach (var (nombre, pedir) in Endpoints(id, transportistaId))
        {
            Assert.True(
                (await pedir(trafico)).StatusCode == HttpStatusCode.Forbidden,
                $"{nombre} tendría que responder 403 a Tráfico");
        }
    }

    [Fact]
    public async Task Gerencia_LeeLasTresLecturas_YRecibe403EnLoDemas()
    {
        var (id, transportistaId) = await UnaLiquidacionAsync();
        var gerencia = await ConRolAsync("gerencia_liquidaciones", CodigosRol.Gerencia);

        foreach (var (nombre, pedir) in Endpoints(id, transportistaId))
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
    [InlineData("administracion_liquidaciones", CodigosRol.Administracion)]
    [InlineData("sistemas_liquidaciones", CodigosRol.AdministradorSistema)]
    public async Task Administracion_YAdministrador_AccedenATodo(string username, string rol)
    {
        var (id, transportistaId) = await UnaLiquidacionAsync();
        var cliente = await ConRolAsync(username, rol);

        foreach (var (nombre, pedir) in Endpoints(id, transportistaId))
        {
            var estado = (await pedir(cliente)).StatusCode;

            Assert.True(
                estado is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                $"{nombre} respondió {(int)estado} a {rol}");
        }
    }

    [Fact]
    public async Task El_Menu_TraeLasDosEntradas_SoloConsultar_ONinguna_SegunElRol()
    {
        var administracion = await LeerMenuAsync(await ConRolAsync("menu_admin_empresa", CodigosRol.Administracion));
        Assert.Contains("consultar-liquidacion", administracion);
        Assert.Contains("generar-liquidacion", administracion);

        var gerencia = await LeerMenuAsync(await ConRolAsync("menu_gerencia_liq", CodigosRol.Gerencia));
        Assert.Contains("consultar-liquidacion", gerencia);
        Assert.DoesNotContain("generar-liquidacion", gerencia);

        var trafico = await LeerMenuAsync(await ConRolAsync("menu_trafico_liq", CodigosRol.Trafico));
        Assert.DoesNotContain("consultar-liquidacion", trafico);
        Assert.DoesNotContain("generar-liquidacion", trafico);
    }

    private static (string Nombre, Func<HttpClient, Task<HttpResponseMessage>> Pedir)[] Endpoints(
        int id,
        int transportistaId) =>
    [
        ("GET /", cliente => cliente.GetAsync("/api/liquidaciones")),
        ("GET /{id}", cliente => cliente.GetAsync($"/api/liquidaciones/{id}")),
        ("GET /transportistas", cliente => cliente.GetAsync("/api/liquidaciones/transportistas")),
        ("GET /disponibles", cliente => cliente.GetAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(transportistaId))),
        ("POST /", cliente => cliente.PostAsJsonAsync(
            "/api/liquidaciones",
            DatosDePruebaLiquidaciones.CuerpoDeGeneracion(transportistaId, [int.MaxValue]))),
        ("PUT /{id}", cliente => cliente.PutAsJsonAsync(
            $"/api/liquidaciones/{id}",
            new { viajeIds = new[] { int.MaxValue }, version = 0 })),
        ("POST /{id}/anulacion", cliente => cliente.PostAsJsonAsync(
            $"/api/liquidaciones/{id}/anulacion",
            new { motivo = "Sin confirmar: no cambia nada." })),
        ("POST /{id}/ordenes-de-pago", cliente => cliente.PostAsJsonAsync(
            $"/api/liquidaciones/{id}/ordenes-de-pago",
            new { fechaPago = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"), importe = 1m })),
    ];

    private async Task<(int Id, int TransportistaId)> UnaLiquidacionAsync()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);

        return (liquidacion.Id, fletero.Id);
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
