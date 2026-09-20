using System.Net.Http.Json;
using GT.Application.Reportes;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Andamio de los tests del Módulo 12: cuentas con rol, la aplicación con el tope bajado y la lectura
/// de las respuestas de los cinco endpoints.
/// </summary>
public static class DatosDePruebaReportes
{
    public const string Password = "Reportes.1234";

    private static int _contador;

    /// <summary>Las cinco rutas del contrato, para recorrerlas de a una.</summary>
    public static readonly (string Nombre, string Ruta)[] Rutas =
    [
        ("viajes", "/api/viajes/reporte"),
        ("vencimientos de choferes", "/api/vencimientos/reporte"),
        ("vencimientos de flota", "/api/flota/vencimientos/reporte"),
        ("vencimientos de facturas", "/api/facturas/vencimientos/reporte"),
        ("movimientos de caja", "/api/movimientos-caja/reporte"),
    ];

    public static async Task<HttpClient> ConRolAsync(this AplicacionDePrueba app, string codigoRol)
    {
        var username = $"reportes_{codigoRol}_{Interlocked.Increment(ref _contador)}";

        await app.CrearUsuarioAsync(username, [codigoRol]);

        return await app.CrearClienteAutenticadoAsync(username, Password);
    }

    /// <summary>
    /// Una cuenta con un rol <b>hecho para el test</b>, que tiene <c>reportes.emitir</c> y ningún
    /// permiso de lectura. Es lo que permite comprobar que la conjunción de FR-015 es real: quien
    /// puede emitir pero no puede mirar la pantalla igual recibe <c>403</c>.
    /// </summary>
    public static async Task<HttpClient> SoloConEmitirAsync(this AplicacionDePrueba app)
    {
        var sufijo = Interlocked.Increment(ref _contador);
        var codigoRol = $"solo_emitir_{sufijo}";

        await app.ConAlcanceAsync(async contexto =>
        {
            var permiso = await contexto.Permisos
                .FirstAsync(p => p.Codigo == CodigosPermiso.ReportesEmitir);

            var rol = new Rol { Codigo = codigoRol, Nombre = $"Sólo emitir {sufijo}" };
            rol.Permisos.Add(permiso);

            contexto.Roles.Add(rol);

            return await contexto.SaveChangesAsync();
        });

        var username = $"reportes_solo_emitir_{sufijo}";

        await app.CrearUsuarioAsync(username, [codigoRol]);

        return await app.CrearClienteAutenticadoAsync(username, Password);
    }

    public static Task CrearUsuarioAsync(
        this AplicacionDePrueba app,
        string username,
        string[] codigosRol) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(Password),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            foreach (var codigo in codigosRol)
            {
                usuario.Roles.Add(await contexto.Roles.FirstAsync(rol => rol.Codigo == codigo));
            }

            contexto.Usuarios.Add(usuario);

            return await contexto.SaveChangesAsync();
        });

    /// <summary>
    /// La misma aplicación con el <b>tope de filas bajado</b>. Es lo que permite probar el <c>409</c> de
    /// FR-016 sin sembrar 5.001 filas: el tope se inyecta como <see cref="OpcionesDeReporte"/>
    /// justamente para esto (data-model §4, research §13).
    /// </summary>
    public static WebApplicationFactory<Program> ConTopeDe(this AplicacionDePrueba app, int tope) =>
        app.WithWebHostBuilder(constructor => constructor.ConfigureServices(servicios =>
            servicios.AddSingleton(new OpcionesDeReporte(tope))));

    /// <summary>La misma aplicación con un armador de PDF que siempre falla (FR-017).</summary>
    public static WebApplicationFactory<Program> ConArmadorDePdfQueFalla(this AplicacionDePrueba app) =>
        app.WithWebHostBuilder(constructor => constructor.ConfigureServices(servicios =>
            servicios.AddSingleton<IArmadorReportePdf, ArmadorQueSiempreFalla>()));

    public static async Task<HttpClient> ClienteAdministradorAsync(
        this WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                username = GT.Infrastructure.DatosIniciales.SembradorInicial.UsernameAdministrador,
                password = AplicacionDePrueba.PasswordAdministrador,
            });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
    }

    private sealed class ArmadorQueSiempreFalla : IArmadorReportePdf
    {
        public byte[] Armar(ReporteTabular reporte) =>
            throw new ReporteNoGeneradoException(new InvalidOperationException("Falla simulada."));
    }
}

/// <summary>El cuerpo de error del contrato, con los dos campos que suma el <c>409</c> del tope.</summary>
public record ErrorDeReporte(string Codigo, string Mensaje)
{
    public int? Filas { get; init; }

    public int? Tope { get; init; }
}
