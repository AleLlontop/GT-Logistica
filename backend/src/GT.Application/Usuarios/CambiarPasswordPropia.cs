using GT.Application.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>Motivo por el que un cambio de contraseña propia no se pudo hacer.</summary>
public enum ErrorCambioPassword
{
    Ninguno,
    NoEncontrado,
    PasswordActualIncorrecta,
    PasswordNuevaInvalida,
}

/// <param name="Usuario">Con los datos ya actualizados, para poder reemitir la cookie (FR-032).</param>
public record ResultadoCambioPassword(ErrorCambioPassword Error, Usuario? Usuario)
{
    public bool Exitoso => Error is ErrorCambioPassword.Ninguno;
}

/// <summary>
/// Cambio de la contraseña propia (User Story 7).
///
/// Es lo que cierra el circuito del restablecimiento: sin esto, la contraseña temporal vence a las
/// 24 horas y el usuario queda afuera sin forma de destrabarse solo.
///
/// El usuario afectado **no llega como parámetro de la petición**: quien llama lo saca de la sesión.
/// Es lo que impide que este endpoint —el único del módulo sin política de permiso— sirva para
/// cambiarle la contraseña a otro (research §9).
/// </summary>
public class CambiarPasswordPropia(
    IRepositorioEscrituraUsuarios repositorio,
    IVerificadorPassword verificador,
    IHasheadorPasswordApp hasheador,
    TimeProvider reloj)
{
    public async Task<ResultadoCambioPassword> EjecutarAsync(
        int idUsuarioDeLaSesion,
        CambiarPasswordPropiaRequest peticion,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerParaEditarAsync(idUsuarioDeLaSesion, cancelacion);

        if (usuario is null)
        {
            return new ResultadoCambioPassword(ErrorCambioPassword.NoEncontrado, null);
        }

        var actual = peticion.PasswordActual ?? string.Empty;
        var nueva = peticion.PasswordNueva ?? string.Empty;

        // FR-030: el mínimo se controla antes de tocar nada. Se valida el formato primero para no
        // gastar un hasheo cuando la nueva ni siquiera sirve.
        if (nueva.Length < CrearUsuario.LargoMinimoPassword)
        {
            return new ResultadoCambioPassword(ErrorCambioPassword.PasswordNuevaInvalida, null);
        }

        if (!verificador.Verificar(usuario.PasswordHash, actual))
        {
            return new ResultadoCambioPassword(ErrorCambioPassword.PasswordActualIncorrecta, null);
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;

        usuario.PasswordHash = hasheador.Hashear(nueva);
        // FR-031: la contraseña elegida por el propio usuario es definitiva. Limpiar la marca es lo
        // que la saca del vencimiento de 24 horas que le aplica el Módulo 1.
        usuario.PasswordTemporalGeneradaEn = null;
        // FR-032: corta las demás sesiones de este usuario. La sesión desde la que se hizo el cambio
        // la salva el endpoint, reemitiendo la cookie con la marca nueva.
        usuario.PasswordActualizadaEn = ahora;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoCambioPassword(ErrorCambioPassword.Ninguno, usuario);
    }
}
