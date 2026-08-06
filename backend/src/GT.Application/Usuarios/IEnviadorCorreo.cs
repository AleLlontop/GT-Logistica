namespace GT.Application.Usuarios;

/// <summary>
/// Envío de correo saliente.
///
/// Devuelve si se pudo entregar en vez de lanzar una excepción, a propósito: para la spec un envío
/// fallido **no es un error de la operación**. El restablecimiento ya quedó registrado y lo único
/// que corresponde es informarlo (FR-021). Si esto lanzara, quien llama tendría que envolverlo en un
/// try/catch para no revertir lo que sí funcionó.
/// </summary>
public interface IEnviadorCorreo
{
    /// <returns><c>true</c> si el correo salió; <c>false</c> si no se pudo entregar.</returns>
    Task<bool> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Texto del correo de restablecimiento, en español rioplatense (Principio II).
///
/// Vive en la capa de aplicación y no en la de infraestructura porque es contenido del producto, no
/// un detalle del transporte.
/// </summary>
public static class CorreoRestablecimiento
{
    public const string Asunto = "Tu contraseña del Sistema Integral de Gestión";

    /// <summary>
    /// Le dice al usuario qué hacer y en cuánto tiempo. El plazo de 24 horas lo fija el Módulo 1;
    /// el aviso de cambiarla apunta a la pantalla que entrega la User Story 7.
    /// </summary>
    public static string Cuerpo(string username, string passwordTemporal) =>
        $"""
        Hola:

        El responsable de sistemas de G&T Logística restableció la contraseña de tu cuenta.

        Usuario: {username}
        Contraseña temporal: {passwordTemporal}

        Esta contraseña vence en 24 horas. Ingresá al sistema y cambiala por una propia desde la
        opción "Cambiar contraseña", arriba a la derecha.

        Si no pediste este cambio, avisale al responsable de sistemas.
        """;
}
