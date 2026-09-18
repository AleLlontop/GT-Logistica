using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// Los dos permisos del módulo, endpoint por endpoint y rol por rol (FR-031 a FR-034, SC-009).
///
/// <b>Ocultar el botón es una cortesía; la restricción es ésta.</b> Las escrituras se invocan con pedidos que
/// no cambian nada —un movimiento y un cierre sobre una caja cerrada, una apertura inválida— para que quien
/// sí tiene el permiso no deje la base distinta.
/// </summary>
public class PermisosCajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>Los que Gerencia puede leer: todo lo de <c>caja.consultar</c>.</summary>
    private static readonly string[] Lecturas =
        ["GET /", "GET /{id}", "GET /{id}/movimientos", "GET /movimientos-caja"];

    [Fact]
    public async Task Sin_Sesion_Recibe401EnTodos()
    {
        var id = await UnaCajaCerradaAsync();
        var anonimo = app.CrearCliente();

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            Assert.True(
                (await pedir(anonimo)).StatusCode == HttpStatusCode.Unauthorized,
                $"{nombre} tendría que responder 401 sin sesión");
        }
    }

    [Fact]
    public async Task Trafico_Recibe403EnTodos()
    {
        var id = await UnaCajaCerradaAsync();
        var (_, trafico) = await app.ConRolAsync(CodigosRol.Trafico);

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            Assert.True(
                (await pedir(trafico)).StatusCode == HttpStatusCode.Forbidden,
                $"{nombre} tendría que responder 403 a Tráfico");
        }
    }

    [Fact]
    public async Task Gerencia_LeeLasConsultas_YRecibe403EnLoDemas()
    {
        var id = await UnaCajaCerradaAsync();
        var (_, gerencia) = await app.ConRolAsync(CodigosRol.Gerencia);

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
    [InlineData(CodigosRol.Administracion)]
    [InlineData(CodigosRol.AdministradorSistema)]
    public async Task Administracion_YAdministrador_AccedenATodo(string rol)
    {
        var id = await UnaCajaCerradaAsync();
        var (usuario, cliente) = await app.ConRolAsync(rol);

        foreach (var (nombre, pedir) in Endpoints(id))
        {
            var estado = (await pedir(cliente)).StatusCode;

            Assert.True(
                estado is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                $"{nombre} respondió {(int)estado} a {rol}");
        }

        // Ningún pedido cambió nada.
        Assert.Equal(0, await app.ContarCajasDeAsync(usuario.Id));
        Assert.Equal(0, await app.ContarMovimientosDeAsync(id));
    }

    [Fact]
    public async Task El_Menu_TraeLasDosEntradas_ParaLosTresRolesConConsulta_YNingunaParaTrafico()
    {
        foreach (var rol in new[] { CodigosRol.Administracion, CodigosRol.AdministradorSistema, CodigosRol.Gerencia })
        {
            var menu = await LeerMenuAsync((await app.ConRolAsync(rol)).Cliente);

            Assert.Contains("consultar-caja", menu);
            Assert.Contains("consultar-movimientos-caja", menu);
        }

        var trafico = await LeerMenuAsync((await app.ConRolAsync(CodigosRol.Trafico)).Cliente);
        Assert.DoesNotContain("consultar-caja", trafico);
        Assert.DoesNotContain("consultar-movimientos-caja", trafico);
    }

    private static (string Nombre, Func<HttpClient, Task<HttpResponseMessage>> Pedir)[] Endpoints(int id) =>
    [
        ("GET /abierta", cliente => cliente.GetAsync("/api/caja/abierta")),
        ("GET /", cliente => cliente.GetAsync("/api/caja")),
        ("GET /{id}", cliente => cliente.GetAsync($"/api/caja/{id}")),
        ("POST /", cliente => cliente.AbrirCajaAsync(-1m)),
        ("GET /facturas-pendientes", cliente => cliente.GetAsync("/api/caja/facturas-pendientes")),
        ("GET /ordenes-de-pago", cliente => cliente.GetAsync("/api/caja/ordenes-de-pago")),
        ("POST /{id}/movimientos", cliente => cliente.RegistrarMovimientoAsync(id)),
        ("GET /{id}/movimientos", cliente => cliente.GetAsync($"/api/caja/{id}/movimientos")),
        ("GET /{id}/cierre", cliente => cliente.GetAsync($"/api/caja/{id}/cierre")),
        ("POST /{id}/cierre", cliente => cliente.CerrarCajaAsync(id, confirmado: true, saldoFinalConfirmado: 0m)),
        ("GET /movimientos-caja", cliente => cliente.GetAsync("/api/movimientos-caja")),
    ];

    /// <summary>De otro usuario y cerrada: ni un movimiento ni un cierre la cambian.</summary>
    private async Task<int> UnaCajaCerradaAsync()
    {
        var responsable = await app.UsuarioAsync();

        return (await app.CrearCajaAsync(responsable.Id, EstadoCaja.Cerrada)).Id;
    }

    private static async Task<List<string>> LeerMenuAsync(HttpClient cliente)
    {
        var sesion = await cliente.GetFromJsonAsync<SesionLeida>("/api/auth/sesion");

        return [.. sesion!.OpcionesMenu.Select(opcion => opcion.Codigo)];
    }
}
