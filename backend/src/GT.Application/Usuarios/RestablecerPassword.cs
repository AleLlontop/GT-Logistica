namespace GT.Application.Usuarios;

/// <summary>Generación de contraseñas temporales. La implementa la capa de infraestructura.</summary>
public interface IGeneradorPasswordTemporal
{
    string Generar();
}

/// <summary>Acceso a usuarios que necesitan las operaciones de escritura del Módulo 2.</summary>
public interface IRepositorioEscrituraUsuarios
{
    /// <summary>Trae el usuario para modificarlo (con seguimiento), o <c>null</c> si no existe.</summary>
    Task<Domain.Usuarios.Usuario?> ObtenerParaEditarAsync(
        int id,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Cuántos usuarios <c>activos</c> con el rol <i>Administrador del sistema</i> quedarían si se
    /// excluyera al indicado. Es lo que consume <c>ProteccionUltimoAdministrador</c> (FR-019).
    /// </summary>
    Task<int> ContarAdministradoresActivosExcluyendoAsync(
        int idUsuarioExcluido,
        CancellationToken cancelacion = default);
}

/// <param name="Enviado"><c>false</c> si el correo no pudo entregarse (FR-021).</param>
public record ResultadoRestablecimiento(bool Encontrado, bool Enviado, string Email);

/// <summary>
/// Restablecimiento de contraseña (User Story 3).
///
/// El responsable de sistemas no elige la contraseña ni la ve en ningún momento (FR-009): el sistema
/// la genera, la hashea, se la manda por email al usuario y devuelve únicamente si el envío salió.
///
/// Si el envío falla, el restablecimiento **igual queda registrado** (FR-021). Es deliberado:
/// revertirlo dejaría al usuario con una contraseña que quizá ya no recuerda, y el responsable puede
/// reintentar el envío o corregir el email.
/// </summary>
public class RestablecerPassword(
    IRepositorioEscrituraUsuarios repositorio,
    IGeneradorPasswordTemporal generador,
    IHasheadorPasswordApp hasheador,
    IEnviadorCorreo correo,
    TimeProvider reloj)
{
    public async Task<ResultadoRestablecimiento> EjecutarAsync(
        int idUsuario,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerParaEditarAsync(idUsuario, cancelacion);

        if (usuario is null)
        {
            return new ResultadoRestablecimiento(Encontrado: false, Enviado: false, string.Empty);
        }

        var temporal = generador.Generar();
        var ahora = reloj.GetUtcNow().UtcDateTime;

        usuario.PasswordHash = hasheador.Hashear(temporal);
        // Con valor, la contraseña es temporal y vence a las 24 horas. La regla ya existe en el
        // Módulo 1; este módulo sólo escribe la marca.
        usuario.PasswordTemporalGeneradaEn = ahora;
        // FR-032: corta las sesiones que ese usuario tuviera abiertas. La contraseña anterior dejó
        // de ser válida, así que ninguna sesión sostenida por ella sobrevive.
        usuario.PasswordActualizadaEn = ahora;

        await repositorio.GuardarCambiosAsync(cancelacion);

        // El envío va después de guardar, no antes: si fallara y estuviera antes, habría que decidir
        // si se restablece igual, y la spec ya decidió que sí (FR-021).
        var enviado = await correo.EnviarAsync(
            usuario.Email,
            CorreoRestablecimiento.Asunto,
            CorreoRestablecimiento.Cuerpo(usuario.Username, temporal),
            cancelacion);

        return new ResultadoRestablecimiento(Encontrado: true, enviado, usuario.Email);
    }
}
