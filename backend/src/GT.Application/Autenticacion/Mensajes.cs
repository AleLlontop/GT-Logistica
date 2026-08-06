using System.Text.Json.Serialization;

namespace GT.Application.Autenticacion;

/// <summary>
/// Identificadores estables de error, para que el frontend decida qué hacer sin depender del texto.
/// </summary>
public static class CodigosError
{
    public const string DatosIncompletos = "datos_incompletos";
    public const string CredencialesInvalidas = "credenciales_invalidas";
    public const string CuentaNoHabilitada = "cuenta_no_habilitada";
    public const string DemasiadosIntentos = "demasiados_intentos";
    public const string SesionExpirada = "sesion_expirada";
    public const string SinPermiso = "sin_permiso";
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Ninguno expone detalles técnicos ni indica cuál de los dos datos de acceso falló (FR-003,
/// FR-015). Son los mismos que fija <c>contracts/README.md</c>.
/// </summary>
public static class Mensajes
{
    public const string DatosIncompletos = "Completá el nombre de usuario y la contraseña.";

    /// <summary>
    /// Único mensaje para username inexistente, contraseña incorrecta y contraseña temporal
    /// vencida: los tres casos deben ser indistinguibles para no revelar qué cuentas existen
    /// (FR-003, FR-017).
    /// </summary>
    public const string CredencialesInvalidas = "El usuario o la contraseña no son correctos.";

    public const string CuentaNoHabilitada =
        "Tu cuenta no está habilitada. Contactá al responsable de sistemas.";

    public const string DemasiadosIntentos =
        "Hubo demasiados intentos fallidos. Esperá un minuto y volvé a intentar.";

    public const string SesionExpirada = "Tu sesión expiró. Ingresá de nuevo.";

    public const string SinPermiso = "No tenés permiso para acceder a esta funcionalidad.";
}

/// <summary>
/// Cuerpo de toda respuesta de error del sistema.
/// </summary>
/// <param name="Campo">
/// Cuando el error corresponde a un campo puntual de un formulario, lo identifica para que la
/// pantalla pueda marcarlo en rojo en el lugar correcto (Módulo 2). Se omite del JSON cuando no
/// aplica, así las respuestas del Módulo 1 no cambian de forma.
/// </param>
public record ErrorResponse(
    string Codigo,
    string Mensaje,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Campo = null)
{
    public static ErrorResponse DatosIncompletos() =>
        new(CodigosError.DatosIncompletos, Mensajes.DatosIncompletos);

    public static ErrorResponse CredencialesInvalidas() =>
        new(CodigosError.CredencialesInvalidas, Mensajes.CredencialesInvalidas);

    public static ErrorResponse CuentaNoHabilitada() =>
        new(CodigosError.CuentaNoHabilitada, Mensajes.CuentaNoHabilitada);

    public static ErrorResponse DemasiadosIntentos() =>
        new(CodigosError.DemasiadosIntentos, Mensajes.DemasiadosIntentos);

    public static ErrorResponse SesionExpirada() =>
        new(CodigosError.SesionExpirada, Mensajes.SesionExpirada);

    public static ErrorResponse SinPermiso() =>
        new(CodigosError.SinPermiso, Mensajes.SinPermiso);
}
