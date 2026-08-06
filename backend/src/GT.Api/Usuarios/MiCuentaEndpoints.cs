using GT.Api.Autenticacion;
using GT.Application.Autenticacion;
using GT.Application.Usuarios;
using Microsoft.AspNetCore.Authentication;

namespace GT.Api.Usuarios;

/// <summary>
/// Operaciones que un usuario hace sobre su propia cuenta.
///
/// <b>Es el único grupo del módulo sin política de permiso</b> (FR-029): exige sesión activa y nada
/// más, porque quien recibe una contraseña temporal puede tener cualquier rol y, si no puede
/// cambiarla, el vencimiento de 24 horas lo deja afuera del sistema.
///
/// El usuario afectado sale siempre de los *claims* de la sesión, nunca de la petición: no hay
/// ningún parámetro donde indicar a otro, así que este endpoint no puede convertirse en una vía para
/// cambiarle la contraseña a un tercero (research §9).
/// </summary>
public static class MiCuentaEndpoints
{
    public static void MapearMiCuenta(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/mi-cuenta")
            // Sin política de permiso, a propósito. No copiar esto a los demás grupos del módulo.
            .RequireAuthorization();

        grupo.MapPost("/contrasena", CambiarPasswordAsync);
    }

    private static async Task<IResult> CambiarPasswordAsync(
        CambiarPasswordPropiaRequest peticion,
        CambiarPasswordPropia cambiar,
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

        var resultado = await cambiar.EjecutarAsync(idUsuario.Value, peticion, cancelacion);

        switch (resultado.Error)
        {
            case ErrorCambioPassword.NoEncontrado:
                return Results.Json(
                    ErrorResponse.SesionExpirada(),
                    statusCode: StatusCodes.Status401Unauthorized);

            case ErrorCambioPassword.PasswordActualIncorrecta:
                // 403 y no 400: el dato está bien formado, lo que falla es la credencial.
                return Results.Json(
                    new ErrorResponse(
                        CodigosErrorUsuarios.PasswordActualIncorrecta,
                        MensajesUsuarios.PasswordActualIncorrecta,
                        "passwordActual"),
                    statusCode: StatusCodes.Status403Forbidden);

            case ErrorCambioPassword.PasswordNuevaInvalida:
                return Results.BadRequest(new ErrorResponse(
                    CodigosErrorUsuarios.DatosInvalidos,
                    MensajesUsuarios.DatosInvalidos,
                    "passwordNueva"));
        }

        // FR-032: el cambio invalidó todas las cookies emitidas con la contraseña anterior, incluida
        // ésta. Reemitirla con la marca nueva es lo que deja viva la sesión desde la que se hizo el
        // cambio —escenario 2 de la User Story 7— sin necesidad de exceptuarla de la regla.
        await contexto.SignInAsync(
            ClaimsSesion.EsquemaCookie,
            ClaimsSesion.Construir(resultado.Usuario!),
            new AuthenticationProperties { IsPersistent = false });

        return Results.NoContent();
    }
}
