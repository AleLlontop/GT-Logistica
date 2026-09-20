using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// La matriz completa: <b>cuatro roles × los cinco reportes</b> (FR-013, FR-014, FR-015, SC-005).
///
/// <b>Ocultar la acción es una cortesía; la restricción es ésta.</b> Los cinco endpoints son de
/// lectura y no cambian nada, así que se pueden invocar con cualquier rol sin dejar la base distinta.
///
/// El reparto del Módulo 12 <b>invierte</b> el de todos los anteriores: <c>reportes.emitir</c> lo
/// reciben Gerencia y el administrador, y <b>no</b> lo reciben Tráfico ni Administración de la
/// empresa. Hasta el Módulo 11, Gerencia siempre tenía un subconjunto de lo de Administración.
/// </summary>
public class PermisosDeReporteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task SinSesion_RecibeCuatrocientosUnoEnLosCinco()
    {
        var anonimo = app.CrearCliente();

        foreach (var (nombre, ruta) in DatosDePruebaReportes.Rutas)
        {
            var respuesta = await anonimo.GetAsync($"{ruta}?formato=pdf");

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.Unauthorized,
                $"El reporte de {nombre} tendría que responder 401 sin sesión");
        }
    }

    /// <summary>
    /// Gerencia los emite los cinco: tiene <c>reportes.emitir</c> y los cinco permisos de lectura.
    /// </summary>
    [Fact]
    public async Task Gerencia_ObtieneLosCincoArchivos()
    {
        var gerencia = await app.ConRolAsync(CodigosRol.Gerencia);

        foreach (var (nombre, ruta) in DatosDePruebaReportes.Rutas)
        {
            var respuesta = await gerencia.GetAsync($"{ruta}?formato=pdf");

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.OK,
                $"El reporte de {nombre} tendría que responder 200 a Gerencia, respondió {(int)respuesta.StatusCode}");

            Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task AdministradorDelSistema_ObtieneLosCincoArchivos()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();

        foreach (var (nombre, ruta) in DatosDePruebaReportes.Rutas)
        {
            var respuesta = await administrador.GetAsync($"{ruta}?formato=excel");

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.OK,
                $"El reporte de {nombre} tendría que responder 200 al administrador");
        }
    }

    /// <summary>
    /// **Tráfico y Administración de la empresa reciben <c>403</c> en los cinco**, aunque tengan el
    /// permiso de lectura de varias de esas pantallas: les falta <c>reportes.emitir</c> (FR-014).
    /// </summary>
    [Theory]
    [InlineData(CodigosRol.Trafico)]
    [InlineData(CodigosRol.Administracion)]
    public async Task LosDosRolesOperativos_RecibenCuatrocientosTresEnLosCinco(string rol)
    {
        var cliente = await app.ConRolAsync(rol);

        foreach (var (nombre, ruta) in DatosDePruebaReportes.Rutas)
        {
            var respuesta = await cliente.GetAsync($"{ruta}?formato=pdf");

            Assert.True(
                respuesta.StatusCode == HttpStatusCode.Forbidden,
                $"El reporte de {nombre} tendría que responder 403 a {rol}, respondió {(int)respuesta.StatusCode}");
        }
    }

    /// <summary>
    /// **La conjunción de FR-015 es real**: quien tiene <c>reportes.emitir</c> y ningún permiso de
    /// lectura recibe <c>403</c> en los cinco, con el mismo texto.
    ///
    /// Los dos permisos dan el mismo <c>403</c>: distinguirlos le diría a quien invoca la ruta a mano
    /// exactamente qué permiso le falta, y eso no ayuda a nadie que esté operando la aplicación.
    /// </summary>
    [Fact]
    public async Task ConEmitirPeroSinLeerLaPantalla_RecibeElMismoCuatrocientosTres()
    {
        var soloEmitir = await app.SoloConEmitirAsync();
        var trafico = await app.ConRolAsync(CodigosRol.Trafico);

        foreach (var (nombre, ruta) in DatosDePruebaReportes.Rutas)
        {
            var sinLectura = await soloEmitir.GetAsync($"{ruta}?formato=pdf");

            Assert.True(
                sinLectura.StatusCode == HttpStatusCode.Forbidden,
                $"El reporte de {nombre} tendría que responder 403 a quien no puede mirar la pantalla");

            var sinEmitir = await trafico.GetAsync($"{ruta}?formato=pdf");

            // El mismo cuerpo en los dos casos.
            Assert.Equal(
                (await sinEmitir.Content.ReadFromJsonAsync<ErrorDeReporte>())!.Mensaje,
                (await sinLectura.Content.ReadFromJsonAsync<ErrorDeReporte>())!.Mensaje);
        }
    }

    /// <summary>El <c>403</c> lleva el texto del contrato del módulo (contracts §Cuerpos de error).</summary>
    [Fact]
    public async Task ElCuatrocientosTres_LlevaElTextoDelContrato()
    {
        var trafico = await app.ConRolAsync(CodigosRol.Trafico);

        var respuesta = await trafico.GetAsync("/api/viajes/reporte?formato=pdf");
        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeReporte>();

        Assert.Equal("sin_permiso", error!.Codigo);
        Assert.Equal(
            "No tenés permiso para emitir reportes. Pedíselo a quien administra el sistema.",
            error.Mensaje);
    }

    /// <summary>
    /// **El permiso no agrega ninguna entrada al menú**: no hay pantalla nueva, y el catálogo es una
    /// lista de pares permiso → pantalla (research §5, spec §Aclaración sobre el nombre).
    /// </summary>
    [Fact]
    public async Task ElPermisoNoAgregaNingunaEntradaAlMenu()
    {
        var gerencia = await app.ConRolAsync(CodigosRol.Gerencia);

        var sesion = await gerencia.GetFromJsonAsync<SesionConMenu>("/api/auth/sesion");

        Assert.Contains("reportes.emitir", sesion!.Permisos);
        Assert.DoesNotContain(sesion.OpcionesMenu, opcion => opcion.Codigo.Contains("reporte"));
    }

    private record SesionConMenu(List<string> Permisos, List<OpcionDeMenu> OpcionesMenu);

    private record OpcionDeMenu(string Codigo, string Etiqueta, string Ruta);
}
