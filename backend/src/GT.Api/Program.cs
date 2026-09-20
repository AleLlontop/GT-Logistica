using GT.Api.Adelantos;
using GT.Api.Caja;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Api.Usuarios;
using GT.Api.Usuarios.Personas;
using GT.Api.Choferes;
using GT.Api.Facturacion;
using GT.Api.Flota;
using GT.Api.Liquidaciones;
using GT.Api.Reportes;
using GT.Application.Liquidaciones;
using GT.Application.Adelantos;
using GT.Application.Caja;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Choferes.Transportistas;
using GT.Application.Facturacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Application.Flota;
using GT.Application.Flota.Documentacion;
using GT.Application.Flota.TiposVehiculo;
using GT.Application.Reportes;
using GT.Application.Reportes.Fuentes;
using GT.Application.Usuarios;
using GT.Application.Usuarios.Personas;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;
using GT.Api.Viajes;
using GT.Domain.Usuarios;
using GT.Infrastructure.Archivos;
using GT.Infrastructure.Correo;
using GT.Infrastructure.DatosIniciales;
using GT.Infrastructure.Documentos;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Reportes;
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

// Calificado desde el Módulo 6, que tiene su propio `ConsultarVencimientos` —el de las facturas—. Este
// archivo es el único lugar del sistema que ve los dos.
builder.Services.AddScoped<GT.Application.Choferes.Documentacion.ConsultarVencimientos>();

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

// ── Módulo 6: gestión de facturación ───────────────────────────────────────────────────────────
// La única dependencia nueva del módulo, y hay que declararle la licencia **antes** de la primera
// invocación: sin esta línea el armador tira excepción al generar el primer documento y no antes
// (research §1). Va acá, junto al resto del registro de infraestructura, porque es configuración de
// arranque y no una decisión que el armador pueda tomar por su cuenta.
//
// `Community` es la licencia gratuita para organizaciones de menos de USD 1M de facturación anual.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// El armador es la frontera con QuestPDF: la capa de aplicación habla con la interfaz y el dominio no
// sabe que existe un PDF. Singleton porque no tiene estado: recibe los datos ya formateados y dibuja.
builder.Services.AddSingleton<IArmadorDocumentoFactura, ArmadorDocumentoFacturaQuestPdf>();

builder.Services.AddScoped<IRepositorioEmpresaEmisora, RepositorioEmpresaEmisora>();
builder.Services.AddScoped<ConsultarEmpresaEmisora>();
builder.Services.AddScoped<GuardarEmpresaEmisora>();
builder.Services.AddScoped<GestionarLogo>();

builder.Services.AddScoped<IRepositorioFacturas, RepositorioFacturas>();
builder.Services.AddScoped<PreparadorDeFactura>();
builder.Services.AddScoped<ConsultarFacturables>();
builder.Services.AddScoped<ConsultarAnuladasSinReemplazo>();
builder.Services.AddScoped<VistaPreviaFactura>();
builder.Services.AddScoped<EmitirFactura>();
builder.Services.AddScoped<ConsultarFacturas>();
builder.Services.AddScoped<ConsultarFichaFactura>();
builder.Services.AddScoped<ServirDocumentoFactura>();
builder.Services.AddScoped<CorregirFactura>();
builder.Services.AddScoped<RegistrarCobro>();
builder.Services.AddScoped<AnularFactura>();

// Calificado: el Módulo 3 ya tiene un `ConsultarVencimientos` —el de la documentación de choferes— y
// este archivo es el único lugar del sistema que ve los dos. Los espacios de nombres los distinguen; el
// Módulo 4 resolvió lo mismo poniéndole `Flota` al suyo, y acá el nombre corto sigue siendo el correcto
// dentro del módulo.
builder.Services.AddScoped<GT.Application.Facturacion.ConsultarVencimientos>();
builder.Services.AddScoped<ConsultarTotalesFacturacion>();

// ── Módulo 9: liquidación a transportistas ─────────────────────────────────────────────────────
// Sin dependencias, variables de entorno ni infraestructura nuevas: lee transportistas, viajes y la
// empresa emisora sin escribirles nada (research §11).
builder.Services.AddScoped<IRepositorioLiquidaciones, RepositorioLiquidaciones>();
builder.Services.AddScoped<ValidadorDeViajes>();
builder.Services.AddScoped<ConsultarDetalleLiquidacion>();
builder.Services.AddScoped<ConsultarTransportistasLiquidables>();
builder.Services.AddScoped<ConsultarViajesDisponibles>();
builder.Services.AddScoped<GenerarLiquidacion>();
builder.Services.AddScoped<ConsultarLiquidaciones>();
builder.Services.AddScoped<EditarLiquidacion>();
builder.Services.AddScoped<AnularLiquidacion>();
builder.Services.AddScoped<RegistrarOrdenDePago>();

// ── Módulo 10: gestión de adelantos de sueldo ──────────────────────────────────────────────────
// Sin dependencias, variables de entorno ni infraestructura nuevas: lee personas, fichas de chofer,
// transportistas y la empresa emisora sin escribirles nada (research §10).
builder.Services.AddScoped<IRepositorioAdelantos, RepositorioAdelantos>();
builder.Services.AddScoped<ConsultarDetalleAdelanto>();
builder.Services.AddScoped<ConsultarBeneficiarios>();
builder.Services.AddScoped<RegistrarAdelanto>();
builder.Services.AddScoped<ConsultarAdelantos>();
builder.Services.AddScoped<ConsultarPersonasConAdelantos>();
builder.Services.AddScoped<AprobarAdelanto>();
builder.Services.AddScoped<RechazarAdelanto>();
builder.Services.AddScoped<AnularAdelanto>();

// ── Módulo 11: gestión de caja ─────────────────────────────────────────────────────────────────
// Sin dependencias, variables de entorno ni infraestructura nuevas: lee usuarios, facturas y órdenes de
// pago sin escribirles nada (FR-012). `TimeProvider` ya está abajo y se consume tal como está.
builder.Services.AddScoped<IRepositorioCaja, RepositorioCaja>();
builder.Services.AddScoped<ConsultarDetalleCaja>();
builder.Services.AddScoped<AbrirCaja>();
builder.Services.AddScoped<ConsultarCajas>();
builder.Services.AddScoped<RegistrarMovimiento>();
builder.Services.AddScoped<ConsultarMovimientosDeCaja>();
builder.Services.AddScoped<ConsultarFacturasPendientes>();
builder.Services.AddScoped<ConsultarOrdenesDePago>();
builder.Services.AddScoped<ConsultarResumenDeCierre>();
builder.Services.AddScoped<CerrarCaja>();
builder.Services.AddScoped<ConsultarMovimientos>();

// ── Módulo 12: emitir reportes ─────────────────────────────────────────────────────────────────
// Una dependencia nueva —ClosedXML, sin requisitos nativos, así que el `Dockerfile` no cambia—, cinco
// fuentes y dos armadores. Ninguna tabla, ninguna migración y ninguna variable de entorno
// (data-model §7).
//
// Los dos armadores son singleton y son la frontera con las bibliotecas: no tienen estado, reciben el
// `ReporteTabular` y devuelven bytes. La licencia de QuestPDF ya quedó declarada arriba, en el
// Módulo 6, y vale igual para estos reportes.
builder.Services.AddSingleton<IArmadorReportePdf, ArmadorReportePdfQuestPdf>();
builder.Services.AddSingleton<IArmadorReporteExcel, ArmadorReporteExcelClosedXml>();

// El tope de FR-016 se registra como servicio y no se lee de una constante: un `const` no se puede
// bajar para un test, y research §13 pide probar el `409` sin sembrar 5.001 filas. El `5000` sigue
// escrito una sola vez, en `OpcionesDeReporte.TopePorDefecto` (data-model §4).
builder.Services.AddSingleton(OpcionesDeReporte.PorDefecto);
builder.Services.AddScoped<ArmadoDeReporte>();

builder.Services.AddScoped<FuenteReporteViajes>();
builder.Services.AddScoped<FuenteReporteVencimientosChoferes>();
builder.Services.AddScoped<FuenteReporteVencimientosFlota>();
builder.Services.AddScoped<FuenteReporteVencimientosFacturas>();
builder.Services.AddScoped<FuenteReporteMovimientosCaja>();

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

        // El cuerpo del `403` se arma acá, una sola vez para todo el sistema. Desde el Módulo 12 un
        // endpoint puede declarar el suyo como metadata —los cinco de reportes lo hacen, porque su
        // contrato fija un texto propio— y si no declara ninguno vale el genérico de siempre. **No
        // mueve ninguna decisión de autorización al endpoint**: quien decide sigue siendo la política.
        opciones.Events.OnRedirectToAccessDenied = contexto =>
            ResponderError(contexto.Response, StatusCodes.Status403Forbidden,
                contexto.HttpContext.GetEndpoint()?.Metadata
                    .GetMetadata<MensajeDeSinPermiso>()?.Cuerpo
                ?? ErrorResponse.SinPermiso());

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
        // Los dos paneles de vencimientos van bajo un permiso de lectura propio, para que Gerencia los
        // mire sin quedarse con el padrón ni con la descarga de escaneos. Quien gestiona el módulo los
        // tiene además de su permiso de gestión, sembrados por separado.
        CodigosPermiso.ChoferesVencimientosConsultar,
        CodigosPermiso.FlotaVencimientosConsultar,
        // Módulo 5: los `GET` van bajo `viajes.consultar` y las escrituras bajo `viajes.gestionar`.
        // No son niveles ordenados: quien gestiona tiene los dos, sembrados por separado (FR-050).
        CodigosPermiso.ViajesGestionar,
        CodigosPermiso.ViajesConsultar,
        // Módulo 6: **tres** permisos, el módulo con la autorización más granular del sistema. Se mira
        // con `consultar`, se opera con `gestionar`, y anular tiene el suyo porque devuelve viajes a
        // `rendido` y no se deshace. No agregó una línea de maquinaria: el `PermisoHandler` y el
        // catálogo de menú del Módulo 1 los absorbieron sin cambios (FR-066, FR-067, research §7).
        CodigosPermiso.FacturacionGestionar,
        CodigosPermiso.FacturacionConsultar,
        CodigosPermiso.FacturacionAnular,
        // Módulo 9: se mira con `consultar` y se opera con `gestionar`, anular incluido (FR-063).
        CodigosPermiso.LiquidacionesGestionar,
        CodigosPermiso.LiquidacionesConsultar,
        // Módulo 10: el mismo reparto que el 9; aprobar no lleva permiso aparte (FR-022, FR-041).
        CodigosPermiso.AdelantosGestionar,
        CodigosPermiso.AdelantosConsultar,
        // Módulo 11: el mismo reparto; que la caja sea propia lo decide cada escritura (FR-035).
        CodigosPermiso.CajaGestionar,
        CodigosPermiso.CajaConsultar,
        // Módulo 12: `reportes.emitir` sólo para Gerencia y el administrador (FR-014). La política
        // simple queda registrada junto a las demás; lo que los cinco endpoints piden es la
        // **combinada** de abajo.
        CodigosPermiso.ReportesEmitir));

// Módulo 12: las cinco políticas combinadas, una por reporte. Cada una exige el permiso de lectura de
// su pantalla **más** `reportes.emitir`, y ASP.NET Core pide que se satisfagan todos los requirements
// de una política, así que la conjunción de FR-015 sale gratis y el `PermisoHandler` del Módulo 1 no
// cambia una línea (research §5).
builder.Services.AddAuthorization(opciones =>
{
    opciones.AgregarPoliticaCombinada(
        CodigosPermiso.ViajesConsultar, CodigosPermiso.ReportesEmitir);

    opciones.AgregarPoliticaCombinada(
        CodigosPermiso.ChoferesVencimientosConsultar, CodigosPermiso.ReportesEmitir);

    opciones.AgregarPoliticaCombinada(
        CodigosPermiso.FlotaVencimientosConsultar, CodigosPermiso.ReportesEmitir);

    opciones.AgregarPoliticaCombinada(
        CodigosPermiso.FacturacionConsultar, CodigosPermiso.ReportesEmitir);

    opciones.AgregarPoliticaCombinada(
        CodigosPermiso.CajaConsultar, CodigosPermiso.ReportesEmitir);
});

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
app.MapearEmpresaEmisora();

// El orden entre estos dos no importa —ASP.NET Core enruta por especificidad, no por orden de
// registro—, pero la restricción `{id:int}` de `MapearFacturas` sí: sin ella las cinco rutas literales
// de los otros grupos quedan inalcanzables (convención [005], research §15.1).
app.MapearArmadoDeFacturas();
app.MapearReportesDeFacturacion();
app.MapearCicloDeVidaDeFacturas();
app.MapearFacturas();

// Módulo 9. El de armado va antes por legibilidad; lo que hace alcanzables sus dos rutas literales es la
// restricción `{id:int}` de los otros dos grupos (convención [005], research §12.3).
app.MapearArmadoDeLiquidaciones();
app.MapearCicloDeVidaDeLiquidaciones();
app.MapearLiquidaciones();

// Módulo 10. Lo que hace alcanzables `/beneficiarios` y `/personas` es la restricción `{id:int}` de los
// dos grupos (convención [005]).
app.MapearCicloDeVidaDeAdelantos();
app.MapearAdelantos();

// Módulo 11. Lo que hace alcanzables `/abierta`, `/facturas-pendientes` y `/ordenes-de-pago` es la
// restricción `{id:int}` de los tres grupos (convención [005]).
app.MapearCaja();
app.MapearMovimientosDeCaja();
app.MapearCierreDeCaja();

// Módulo 12: los cinco `MapGet` de reporte, juntos en un archivo para que la revisión cuente cinco
// (FR-001, research §6). Sus rutas son literales y conviven con las `{id:int}` de los grupos de
// arriba, que ya llevan su restricción de tipo (convención [005]).
app.MapearReportes();

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
