using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Api.Usuarios;
using GT.Api.Usuarios.Personas;
using GT.Api.Choferes;
using GT.Api.Flota;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Choferes.Transportistas;
using GT.Application.Flota;
using GT.Application.Flota.Documentacion;
using GT.Application.Flota.TiposVehiculo;
using GT.Application.Usuarios;
using GT.Application.Usuarios.Personas;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;
using GT.Api.Viajes;
using GT.Domain.Usuarios;
using GT.Infrastructure.Archivos;
using GT.Infrastructure.Correo;
using GT.Infrastructure.DatosIniciales;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Seguridad;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Persistencia ────────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<GtDbContext>(opciones =>
    opciones.UseSqlServer(builder.Configuration.GetConnectionString("Gt")));

builder.Services.AddScoped<IHasheadorPassword, HasheadorPassword>();
builder.Services.AddScoped<IRepositorioUsuarios, RepositorioUsuarios>();
builder.Services.AddScoped<IVerificadorPassword, VerificadorPassword>();
builder.Services.AddScoped<AutenticarUsuario>();

// ── Módulo 2: gestión de usuarios, roles y padrón de personas ──────────────────────────────────
builder.Services.AddScoped<IRepositorioPersonas, RepositorioPersonas>();
builder.Services.AddScoped<IRepositorioGestionUsuarios, RepositorioGestionUsuarios>();
builder.Services.AddScoped<IRepositorioConsultaUsuarios, RepositorioGestionUsuarios>();
builder.Services.AddScoped<IHasheadorPasswordApp, HasheadorPasswordApp>();
builder.Services.AddScoped<IRepositorioEscrituraUsuarios, RepositorioGestionUsuarios>();
builder.Services.AddSingleton<IGeneradorPasswordTemporal, GeneradorPasswordTemporal>();
builder.Services.AddScoped<ConsultarPersonas>();
builder.Services.AddScoped<CrearPersona>();
builder.Services.AddScoped<ModificarPersona>();
builder.Services.AddScoped<DarDeBajaPersona>();
builder.Services.AddScoped<CrearUsuario>();
builder.Services.AddScoped<ConsultarUsuarios>();
builder.Services.AddScoped<ModificarUsuario>();
builder.Services.AddScoped<RestablecerPassword>();
builder.Services.AddScoped<CambiarPasswordPropia>();
builder.Services.AddScoped<IRepositorioRoles, RepositorioRoles>();
builder.Services.AddScoped<AsignarRoles>();
builder.Services.AddScoped<DarDeBajaUsuario>();
builder.Services.AddScoped<ConsultarRoles>();

// ── Módulo 3: gestión de choferes ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<IRepositorioChoferes, RepositorioChoferes>();
builder.Services.AddScoped<IRepositorioTransportistas, RepositorioTransportistas>();
builder.Services.AddScoped<ConsultarTransportistas>();
builder.Services.AddScoped<ConsultarTransportistaPorId>();
builder.Services.AddScoped<CrearTransportista>();
builder.Services.AddScoped<CrearChofer>();
builder.Services.AddScoped<ConsultarChoferes>();
builder.Services.AddScoped<ConsultarFichaChofer>();
builder.Services.AddScoped<ModificarChofer>();
builder.Services.AddScoped<DarDeBajaChofer>();
builder.Services.AddScoped<ReactivarChofer>();
builder.Services.AddScoped<ModificarTransportista>();
builder.Services.AddScoped<DarDeBajaTransportista>();
builder.Services.AddScoped<IRepositorioTiposDocumentacion, RepositorioTiposDocumentacion>();
builder.Services.AddScoped<GestionTiposDocumentacion>();
builder.Services.AddScoped<IRepositorioDocumentacion, RepositorioDocumentacion>();
builder.Services.AddScoped<CargarDocumento>();
builder.Services.AddScoped<CorregirDocumento>();
builder.Services.AddScoped<EliminarDocumento>();
builder.Services.AddScoped<DescargarArchivoDocumento>();
builder.Services.AddScoped<ConsultarVencimientos>();

// ── Módulo 4: gestión de flota ─────────────────────────────────────────────────────────────────
// No agrega ninguna infraestructura: el almacén de archivos, el validador por firma y la paginación
// ya están registrados arriba y se consumen sin tocarlos (research §2).
builder.Services.AddScoped<IRepositorioTiposVehiculo, RepositorioTiposVehiculo>();
builder.Services.AddScoped<GestionTiposVehiculo>();
builder.Services.AddScoped<IRepositorioVehiculos, RepositorioVehiculos>();
builder.Services.AddScoped<CrearVehiculo>();
builder.Services.AddScoped<ConsultarFlota>();
builder.Services.AddScoped<ConsultarFichaVehiculo>();
builder.Services.AddScoped<ModificarVehiculo>();
builder.Services.AddScoped<DarDeBajaVehiculo>();
builder.Services.AddScoped<ReactivarVehiculo>();
builder.Services.AddScoped<IRepositorioDocumentacionVehiculo, RepositorioDocumentacionVehiculo>();
builder.Services.AddScoped<CargarDocumentoVehiculo>();
builder.Services.AddScoped<CorregirDocumentoVehiculo>();
builder.Services.AddScoped<EliminarDocumentoVehiculo>();
builder.Services.AddScoped<DescargarArchivoDocumentoVehiculo>();
builder.Services.AddScoped<ConsultarVencimientosFlota>();

// ── Módulo 5: gestión de viajes ────────────────────────────────────────────────────────────────
// No agrega ninguna infraestructura, ninguna variable de entorno y ninguna dependencia: la
// paginación, la autorización por permiso, el menú resuelto por el servidor, los calculadores de
// documentación y `TimeProvider` ya están arriba y se consumen tal como están (research §3, §7).
builder.Services.AddScoped<IRepositorioClientes, RepositorioClientes>();
builder.Services.AddScoped<CrearCliente>();
builder.Services.AddScoped<ConsultarClientes>();
builder.Services.AddScoped<ModificarCliente>();
builder.Services.AddScoped<DarDeBajaCliente>();
builder.Services.AddScoped<DarDeAltaCliente>();
builder.Services.AddScoped<IRepositorioViajes, RepositorioViajes>();
builder.Services.AddScoped<CrearViaje>();
builder.Services.AddScoped<ModificarViaje>();
builder.Services.AddScoped<ConsultarViajes>();
builder.Services.AddScoped<ConsultarFichaViaje>();
builder.Services.AddScoped<ConsultarAsignables>();
builder.Services.AddScoped<AsignarChoferYVehiculo>();
builder.Services.AddScoped<PonerViajeEnCurso>();
builder.Services.AddScoped<RendirViaje>();
builder.Services.AddScoped<AnularViaje>();
builder.Services.AddScoped<ConsultarTotales>();

// Los escaneos van a un volumen, no a la base (research §3). La ruta llega por variable de entorno
// para que el contenedor y una corrida local puedan apuntar a lugares distintos sin tocar el código.
builder.Services.AddSingleton<IValidadorDeArchivo, ValidadorDeArchivoPorFirma>();
builder.Services.AddSingleton<IAlmacenDeArchivos>(servicios => new AlmacenDeArchivos(
    builder.Configuration["GT_ARCHIVOS_RUTA"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "archivos"),
    servicios.GetRequiredService<ILogger<AlmacenDeArchivos>>()));

// El adjunto se corta en 10 MB (FR-015a). Rechazarlo acá evita leer en memoria un cuerpo enorme
// antes de descartarlo; el margen extra cubre los otros campos del formulario.
builder.Services.Configure<FormOptions>(opciones =>
{
    opciones.MultipartBodyLengthLimit = ValidadorArchivo.TamanioMaximoEnBytes + 64 * 1024;
});

// ── Correo saliente (FR-009, research §1) ──────────────────────────────────────────────────────
// Con `Correo:Host` configurado se manda por SMTP; sin él, el envío se registra en el log y todo lo
// demás funciona igual. Eso es lo que permite recorrer el quickstart completo con `compose up`, sin
// un servidor de correo real.
var opcionesCorreo = builder.Configuration
    .GetSection(OpcionesCorreo.Seccion)
    .Get<OpcionesCorreo>() ?? new OpcionesCorreo();

builder.Services.AddSingleton(opcionesCorreo);

if (opcionesCorreo.HaySmtpConfigurado)
{
    builder.Services.AddScoped<IEnviadorCorreo, EnviadorCorreoSmtp>();
}
else
{
    builder.Services.AddScoped<IEnviadorCorreo, EnviadorCorreoRegistrado>();
}

// FR-021: el contador es singleton porque tiene que sobrevivir entre peticiones; ahí vive la
// memoria de los intentos fallidos por origen y cuenta.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IContadorIntentosFallidos, ContadorIntentosFallidosEnMemoria>();
builder.Services.AddScoped<SembradorInicial>();
builder.Services.AddScoped<RevalidadorSesion>();
builder.Services.AddSingleton(TimeProvider.System);

// ── Sesión por cookie (FR-010, FR-013, FR-022, FR-023) ─────────────────────────────────────────
// Cookie en vez de token autocontenido: es lo que permite recalcular permisos en cada operación,
// cortar la sesión cuando la cuenta deja de estar activa y cerrarla de verdad (research §1).
builder.Services
    .AddAuthentication(ClaimsSesion.EsquemaCookie)
    .AddCookie(ClaimsSesion.EsquemaCookie, opciones =>
    {
        opciones.Cookie.Name = ClaimsSesion.EsquemaCookie;

        // FR-023: fuera del alcance de los scripts de la página, sólo por conexión cifrada, y sin
        // acompañar peticiones originadas en otros sitios.
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opciones.Cookie.SameSite = SameSiteMode.Strict;

        // FR-022: cookie de sesión, sin vencimiento propio. Muere al cerrar el navegador.
        opciones.Cookie.MaxAge = null;

        // FR-010: 8 horas de inactividad, renovadas con cada operación, sin tope absoluto por
        // encima. El plazo se lee de configuración para poder bajarlo en desarrollo y verificar el
        // vencimiento sin esperar 8 horas (quickstart).
        opciones.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue("Sesion:MinutosDeInactividad", 480));
        opciones.SlidingExpiration = true;

        // Este backend sirve una API: ante falta de sesión o de permiso responde con el contrato de
        // errores, nunca con una redirección a una pantalla de login del servidor (FR-015).
        opciones.Events.OnRedirectToLogin = contexto =>
            ResponderError(contexto.Response, StatusCodes.Status401Unauthorized,
                ErrorResponse.SesionExpirada());

        opciones.Events.OnRedirectToAccessDenied = contexto =>
            ResponderError(contexto.Response, StatusCodes.Status403Forbidden,
                ErrorResponse.SinPermiso());

        // FR-006 y FR-009: ver RevalidadorSesion.
        opciones.Events.OnValidatePrincipal = async contexto =>
        {
            var revalidador = contexto.HttpContext.RequestServices
                .GetRequiredService<RevalidadorSesion>();

            await revalidador.RevalidarAsync(contexto);
        };
    });

// FR-008: la autorización se evalúa en el servidor por permiso, no por rol, y sin importar si la
// opción estaba visible u oculta en el menú del cliente.
builder.Services.AddSingleton<IAuthorizationHandler, PermisoHandler>();
builder.Services.AddAuthorization(opciones =>
    opciones.AgregarPoliticasDePermisos(
        CodigosPermiso.UsuariosGestionar,
        CodigosPermiso.ChoferesGestionar,
        // Módulo 4: dos permisos, no uno. El catálogo de tipos de vehículo es sólo del administrador
        // y el resto del módulo también es de Tráfico (FR-039, research §7).
        CodigosPermiso.FlotaGestionar,
        CodigosPermiso.FlotaTiposGestionar,
        // Módulo 5: los `GET` van bajo `viajes.consultar` y las escrituras bajo `viajes.gestionar`.
        // No son niveles ordenados: quien gestiona tiene los dos, sembrados por separado (FR-050).
        CodigosPermiso.ViajesGestionar,
        CodigosPermiso.ViajesConsultar));

builder.Services.Configure<JsonOptions>(opciones =>
    opciones.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

// ── FR-024: ninguna credencial se acepta por una conexión sin cifrar ───────────────────────────
// En desarrollo la app corre detrás del proxy de Vite sobre localhost, que los navegadores tratan
// como origen seguro; fuera de desarrollo se fuerza HTTPS y se declara HSTS.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ── FR-015 y FR-018: errores sin detalles técnicos y sin rastro de la contraseña ───────────────
app.UseExceptionHandler(rama => rama.Run(async contexto =>
{
    contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
    contexto.Response.ContentType = "application/json";

    await contexto.Response.WriteAsJsonAsync(new ErrorResponse(
        "error_inesperado",
        "Ocurrió un problema inesperado. Volvé a intentar en unos minutos."));
}));

// FR-013: ninguna respuesta de la API se guarda en la caché del navegador, para que el botón
// "atrás" no pueda recuperar datos de una sesión ya cerrada.
app.Use(async (contexto, siguiente) =>
{
    contexto.Response.OnStarting(() =>
    {
        contexto.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        contexto.Response.Headers.Pragma = "no-cache";

        return Task.CompletedTask;
    });

    await siguiente();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/salud", () => Results.Ok(new { estado = "ok" }));

app.MapearAutenticacion();
app.MapearUsuarios();
app.MapearRoles();
app.MapearMiCuenta();
app.MapearPersonas();
app.MapearTransportistas();
app.MapearChoferes();
app.MapearTiposDocumentacion();
app.MapearDocumentacion();
app.MapearTiposVehiculo();
app.MapearVehiculos();
app.MapearDocumentacionVehiculo();
app.MapearClientes();
app.MapearViajes();
app.MapearAsignacion();
app.MapearCicloDeVida();
app.MapearTotales();

await AplicarMigracionesYSembrarAsync(app);

app.Run();

return;

static Task ResponderError(HttpResponse respuesta, int codigoHttp, ErrorResponse cuerpo)
{
    respuesta.StatusCode = codigoHttp;
    respuesta.ContentType = "application/json";

    return respuesta.WriteAsJsonAsync(cuerpo);
}

// ── Migraciones y datos iniciales (FR-019) ─────────────────────────────────────────────────────
static async Task AplicarMigracionesYSembrarAsync(WebApplication app)
{
    using var alcance = app.Services.CreateScope();

    var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();
    await contexto.Database.MigrateAsync();

    var sembrador = alcance.ServiceProvider.GetRequiredService<SembradorInicial>();

    // La variable sólo hace falta mientras el usuario `admin` no exista; el sembrador decide si es
    // obligatoria y detiene el arranque con un mensaje explícito si falta (research §6).
    var passwordInicial = app.Configuration[SembradorInicial.VariablePasswordInicial];

    await sembrador.SembrarAsync(passwordInicial);
}

/// <summary>Expuesto para que los tests de integración puedan levantar la aplicación.</summary>
public partial class Program;
