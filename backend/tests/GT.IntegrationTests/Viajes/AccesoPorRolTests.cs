using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Los dos permisos del módulo y su reparto por rol (FR-050 a FR-053, SC-012; quickstart paso 1).
///
/// <list type="bullet">
///   <item><c>viajes.gestionar</c> — Tráfico y Administrador del sistema.</item>
///   <item><c>viajes.consultar</c> — <b>los cuatro roles</b>. Es el primer permiso que llega a
///   Administración de la empresa y a Gerencia, que hasta el Módulo 4 no tenían ninguno.</item>
/// </list>
///
/// Se verifica contra el servidor y no contra el menú: la autorización se evalúa acá, sin importar si
/// la acción estaba visible u oculta en la pantalla. Ocultar los botones es una cortesía; el
/// <c>403</c> es la restricción.
/// </summary>
public class AccesoPorRolTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDePrueba = "Viajes.Acceso.1234";

    /// <summary>Gerencia llega a las tres lecturas del módulo.</summary>
    [Theory]
    [InlineData("/api/viajes")]
    [InlineData("/api/clientes")]
    [InlineData("/api/viajes/totales?desde=2026-08-01&hasta=2026-08-31")]
    public async Task Gerencia_LlegaALasTresLecturas(string ruta)
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Gerencia, $"gerencia-{Sufijo(ruta)}");

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// SC-012: y recibe <c>403</c> en toda escritura, aunque invoque el endpoint a mano. La
    /// restricción no vive sólo en la pantalla (FR-052).
    /// </summary>
    [Fact]
    public async Task Gerencia_RecibeProhibidoEnTodaEscritura()
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Gerencia, "gerencia-escrituras");
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var cuerpoCliente = new
        {
            razonSocial = "No debería entrar",
            cuit = DatosDePruebaViajes.CuitUnico(),
            telefono = "0341-555-5555",
            email = "no@entra.com.ar",
        };

        var cuerpoViaje = new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
        };

        HttpResponseMessage[] escrituras =
        [
            await cliente.PostAsJsonAsync("/api/clientes", cuerpoCliente),
            await cliente.PutAsJsonAsync($"/api/clientes/{padron.Id}", cuerpoCliente),
            await cliente.DeleteAsync($"/api/clientes/{padron.Id}"),
            await cliente.PostAsync($"/api/clientes/{padron.Id}/alta", null),
            await cliente.PostAsJsonAsync("/api/viajes", cuerpoViaje),
            await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", cuerpoViaje),
            await cliente.PostAsJsonAsync(
                $"/api/viajes/{viaje.Id}/asignacion",
                new { choferId = 1, vehiculoId = 1 }),
            await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null),
            await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null),
            await cliente.PostAsJsonAsync(
                $"/api/viajes/{viaje.Id}/anulacion",
                new { motivo = "No debería entrar." }),
        ];

        Assert.All(escrituras, respuesta =>
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode));

        // Y nada cambió.
        var sinCambios = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(Domain.Viajes.EstadoViaje.Pendiente, sinCambios!.Estado);
    }

    /// <summary>
    /// La lista de asignables exige <c>viajes.gestionar</c>: sólo le sirve a quien puede asignar.
    /// </summary>
    [Fact]
    public async Task Gerencia_NoLlegaALaListaDeAsignables()
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Gerencia, "gerencia-asignables");

        var respuesta = await cliente.GetAsync("/api/viajes/asignables");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>Administración de la empresa también consulta: los cuatro roles lo tienen.</summary>
    [Fact]
    public async Task Administracion_LlegaALasLecturas()
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Administracion, "administracion-viajes");

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/viajes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/clientes")).StatusCode);
    }

    /// <summary>Tráfico llega a todo: tiene los dos permisos.</summary>
    [Fact]
    public async Task Trafico_LlegaATodo()
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Trafico, "trafico-viajes");
        var padron = await app.CrearClienteAsync();

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/viajes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/viajes/asignables")).StatusCode);

        var alta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
        });

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
    }

    /// <summary>
    /// Sin sesión no se llega a nada, ni siquiera al <c>404</c>: la autenticación corre antes que
    /// la búsqueda del recurso.
    /// </summary>
    [Theory]
    [InlineData("/api/viajes")]
    [InlineData("/api/clientes")]
    [InlineData("/api/viajes/asignables")]
    [InlineData("/api/viajes/totales?desde=2026-08-01&hasta=2026-08-31")]
    public async Task Sin_Sesion_NoSeLlegaANada(string ruta)
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// El menú devuelve las tres entradas del módulo a quien puede consultar, y ninguna a quien no
    /// tiene el permiso (FR-050).
    /// </summary>
    [Fact]
    public async Task El_Menu_DevuelveLasTresEntradasAQuienPuedeConsultar()
    {
        var cliente = await ClienteConRolAsync(CodigosRol.Gerencia, "gerencia-menu");

        var sesion = await cliente.GetFromJsonAsync<SesionLeida>("/api/auth/sesion");

        var rutas = sesion!.OpcionesMenu.Select(opcion => opcion.Ruta).ToList();

        Assert.Contains("/viajes", rutas);
        Assert.Contains("/clientes", rutas);
        Assert.Contains("/viajes/totales", rutas);

        // Y el permiso viaja en la sesión para que la pantalla sepa qué acciones ofrecer (FR-052).
        Assert.Contains("viajes.consultar", sesion.Permisos);
        Assert.DoesNotContain("viajes.gestionar", sesion.Permisos);
    }

    private static string Sufijo(string ruta) => $"{Math.Abs(ruta.GetHashCode()):X}";

    private async Task<HttpClient> ClienteConRolAsync(string codigoRol, string username)
    {
        var usuario = await app.CrearUsuarioConRolViajesAsync(
            username,
            PasswordDePrueba,
            codigoRol);

        return await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDePrueba);
    }

    private record OpcionMenuLeida(string Codigo, string Etiqueta, string Ruta);

    private record SesionLeida(
        string Username,
        List<OpcionMenuLeida> OpcionesMenu,
        List<string> Permisos);
}
