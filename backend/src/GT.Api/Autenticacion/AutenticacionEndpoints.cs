using GT.Application.Autenticacion;
using GT.Domain.Autenticacion;
using GT.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace GT.Api.Autenticacion;

public static class AutenticacionEndpoints
{
    public static void MapearAutenticacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/auth");

        grupo.MapPost("/login", IniciarSesionAsync).AllowAnonymous();
        grupo.MapGet("/sesion", ObtenerSesionAsync).RequireAuthorization();

        // Cerrar sesión no exige sesión activa: es idempotente a propósito, así que llamarlo sin
        // sesión abierta responde igual y nunca deja al usuario trabado.
        grupo.MapPost("/logout", CerrarSesionAsync).AllowAnonymous();
    }

    /// <summary>
    /// Inicia sesión. La pantalla de ingreso es la única funcionalidad pública del sistema (FR-007).
    /// </summary>
    private static async Task<IResult> IniciarSesionAsync(
        CredencialesRequest credenciales,
        AutenticarUsuario autenticar,
        IContadorIntentosFallidos contadorIntentos,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        // El formulario ya lo valida en pantalla (FR-011); acá se repite porque el servidor no
        // puede confiar en que la petición venga del formulario.
        if (string.IsNullOrWhiteSpace(credenciales.Username) ||
            string.IsNullOrWhiteSpace(credenciales.Password))
        {
            return Results.BadRequest(ErrorResponse.DatosIncompletos());
        }

        var origen = ObtenerOrigen(contexto);
        var cuenta = NormalizadorUsername.Normalizar(credenciales.Username);

        // FR-021: el freno es por origen **y** cuenta. Los intentos sobre otras cuentas desde el
        // mismo origen no se ven afectados.
        var espera = contadorIntentos.TiempoDeEspera(origen, cuenta);

        if (espera is not null)
        {
            contexto.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(espera.Value.TotalSeconds)).ToString();

            return Results.Json(
                ErrorResponse.DemasiadosIntentos(),
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        var respuesta = await autenticar.EjecutarAsync(credenciales, cancelacion);

        switch (respuesta.Resultado)
        {
            case ResultadoAutenticacion.CredencialesInvalidas:
                contadorIntentos.RegistrarFallo(origen, cuenta);

                return Results.Json(
                    ErrorResponse.CredencialesInvalidas(),
                    statusCode: StatusCodes.Status401Unauthorized);

            case ResultadoAutenticacion.CuentaNoHabilitada:
                // Una cuenta no habilitada no suma al contador: la contraseña era correcta, así que
                // no hay nada que un atacante esté adivinando, y frenar a esa persona sólo agregaría
                // confusión a un problema que igual no puede resolver sola.
                return Results.Json(
                    ErrorResponse.CuentaNoHabilitada(),
                    statusCode: StatusCodes.Status403Forbidden);
        }

        contadorIntentos.RegistrarExito(origen, cuenta);

        var usuario = respuesta.Usuario!;

        await contexto.SignInAsync(
            ClaimsSesion.EsquemaCookie,
            ClaimsSesion.Construir(usuario),
            // FR-022: cookie no persistente. La sesión termina al cerrar el navegador.
            new AuthenticationProperties { IsPersistent = false });

        return Results.Ok(SesionResponse.Desde(usuario));
    }

    /// <summary>
    /// Identifica el origen de la petición por su dirección de red, tal como fija FR-021.
    /// </summary>
    private static string ObtenerOrigen(HttpContext contexto) =>
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido";

    /// <summary>
    /// Cierra la sesión (FR-013).
    ///
    /// <c>SignOutAsync</c> borra la cookie de inmediato: a diferencia de un token autocontenido, que
    /// seguiría siendo válido hasta vencer, acá la sesión queda invalidada al instante.
    ///
    /// La cabecera <c>Cache-Control: no-store</c> impide que el navegador sirva esta respuesta —o
    /// cualquier otra protegida— desde su caché al usar el botón "atrás".
    /// </summary>
    /// <remarks>
    /// El <see cref="CancellationToken"/> no se usa, pero tiene que estar: sin él la firma sería
    /// <c>Task Método(HttpContext)</c>, que coincide exactamente con <c>RequestDelegate</c>. Al
    /// elegir esa sobrecarga, ASP.NET ejecuta el método y descarta el <see cref="IResult"/>
    /// devuelto, de modo que la respuesta sale 200 vacía en lugar de 204.
    /// </remarks>
    private static async Task<IResult> CerrarSesionAsync(
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        await contexto.SignOutAsync(ClaimsSesion.EsquemaCookie);

        return Results.NoContent();
    }

    /// <summary>
    /// Devuelve la sesión vigente. El frontend la llama al arrancar para saber si hay sesión y qué
    /// menú dibujar.
    ///
    /// Los roles y el menú se recalculan en cada llamada: si al usuario le cambiaron los roles o la
    /// cuenta dejó de estar `activa`, se refleja acá (FR-006, FR-009).
    /// </summary>
    private static async Task<IResult> ObtenerSesionAsync(
        GtDbContext baseDatos,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        var idUsuario = ClaimsSesion.ObtenerIdUsuario(contexto.User);

        if (idUsuario is null)
        {
            return Results.Json(
                ErrorResponse.SesionExpirada(),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var usuario = await baseDatos.Usuarios
            .Include(u => u.Roles)
            .ThenInclude(rol => rol.Permisos)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario, cancelacion);

        if (usuario is null || !usuario.PuedeAutenticarse)
        {
            return Results.Json(
                ErrorResponse.SesionExpirada(),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(SesionResponse.Desde(usuario));
    }
}
