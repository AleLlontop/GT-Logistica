namespace GT.Domain.Autenticacion;

/// <summary>
/// Vigencia de una contraseña temporal (FR-017).
///
/// El Módulo 2 genera la contraseña y la envía por email; este módulo sólo decide si todavía sirve
/// para entrar. Vale 24 horas desde que se generó: pasado ese plazo el ingreso se rechaza con el
/// mismo mensaje genérico de credenciales inválidas, para no revelar que la cuenta existe.
///
/// El plazo existe porque una contraseña temporal viajó por correo en texto plano. Sin vencimiento,
/// quedaría como credencial permanente de la cuenta para cualquiera con acceso a ese buzón.
/// </summary>
public static class VigenciaPasswordTemporal
{
    public static readonly TimeSpan Duracion = TimeSpan.FromHours(24);

    /// <param name="generadaEn">
    /// <c>null</c> significa que la contraseña es definitiva, no temporal: en ese caso no vence.
    /// </param>
    public static bool SigueVigente(DateTime? generadaEn, DateTime ahora)
    {
        if (generadaEn is null)
        {
            return true;
        }

        return ahora - generadaEn.Value < Duracion;
    }
}
