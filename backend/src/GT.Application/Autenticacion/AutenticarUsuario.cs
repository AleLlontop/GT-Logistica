using GT.Domain.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.Application.Autenticacion;

/// <summary>Acceso a usuarios que necesita la autenticación. Lo implementa la capa de infraestructura.</summary>
public interface IRepositorioUsuarios
{
    Task<Usuario?> BuscarPorUsernameNormalizadoAsync(
        string usernameNormalizado,
        CancellationToken cancelacion = default);

    Task RegistrarUltimoAccesoAsync(
        int idUsuario,
        DateTime momento,
        CancellationToken cancelacion = default);
}

/// <summary>Verificación de contraseñas. La implementa la capa de infraestructura.</summary>
public interface IVerificadorPassword
{
    bool Verificar(string hashAlmacenado, string passwordIngresada);

    /// <summary>
    /// Verifica contra un hash ficticio y descarta el resultado.
    ///
    /// Sin esto, un username inexistente responde en milisegundos y una contraseña incorrecta tarda
    /// lo que tarda el hasheo. Esa diferencia de tiempo delata qué cuentas existen y anula el
    /// propósito del mensaje genérico de FR-003.
    /// </summary>
    void VerificarEnVano(string passwordIngresada);
}

public enum ResultadoAutenticacion
{
    Exitoso,

    /// <summary>Username inexistente o contraseña incorrecta: un solo resultado para no distinguirlos (FR-003).</summary>
    CredencialesInvalidas,

    /// <summary>La contraseña era correcta pero la cuenta no está `activa` (FR-004).</summary>
    CuentaNoHabilitada,
}

public record RespuestaAutenticacion(ResultadoAutenticacion Resultado, Usuario? Usuario);

/// <summary>
/// Caso de uso de autenticación.
///
/// El orden de las validaciones no es casual y está fijado por la spec (data-model.md):
/// el estado de la cuenta se controla **después** de verificar la contraseña, para que una
/// contraseña incorrecta sobre una cuenta inactiva devuelva el mensaje genérico y no confirme que
/// la cuenta existe (User Story 4, escenario 3).
/// </summary>
public class AutenticarUsuario(
    IRepositorioUsuarios repositorio,
    IVerificadorPassword verificador,
    TimeProvider reloj)
{
    public async Task<RespuestaAutenticacion> EjecutarAsync(
        CredencialesRequest credenciales,
        CancellationToken cancelacion = default)
    {
        var normalizado = NormalizadorUsername.Normalizar(credenciales.Username);

        var usuario = await repositorio.BuscarPorUsernameNormalizadoAsync(normalizado, cancelacion);

        var password = credenciales.Password ?? string.Empty;

        if (usuario is null)
        {
            // Se hashea igual, contra un hash ficticio, para que el tiempo de respuesta no delate
            // que la cuenta no existe (FR-003, research §3).
            verificador.VerificarEnVano(password);

            return new RespuestaAutenticacion(ResultadoAutenticacion.CredencialesInvalidas, null);
        }

        if (!verificador.Verificar(usuario.PasswordHash, password))
        {
            return new RespuestaAutenticacion(ResultadoAutenticacion.CredencialesInvalidas, null);
        }

        // FR-017: una contraseña temporal vencida se rechaza con el mensaje genérico, igual que una
        // contraseña incorrecta, para no confirmar que la cuenta existe.
        var vigente = VigenciaPasswordTemporal.SigueVigente(
            usuario.PasswordTemporalGeneradaEn,
            reloj.GetUtcNow().UtcDateTime);

        if (!vigente)
        {
            return new RespuestaAutenticacion(ResultadoAutenticacion.CredencialesInvalidas, null);
        }

        if (!usuario.PuedeAutenticarse)
        {
            return new RespuestaAutenticacion(ResultadoAutenticacion.CuentaNoHabilitada, null);
        }

        // FR-005: todo ingreso exitoso deja registrada la fecha y hora reales.
        var momento = reloj.GetUtcNow().UtcDateTime;
        await repositorio.RegistrarUltimoAccesoAsync(usuario.Id, momento, cancelacion);
        usuario.UltimoAcceso = momento;

        return new RespuestaAutenticacion(ResultadoAutenticacion.Exitoso, usuario);
    }
}
