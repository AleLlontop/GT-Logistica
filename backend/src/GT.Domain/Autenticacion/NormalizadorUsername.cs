namespace GT.Domain.Autenticacion;

/// <summary>
/// Normalización del username (FR-012).
///
/// Recorta espacios al costado y pasa a mayúsculas invariantes, de modo que "Juan", " juan " y
/// "JUAN" resuelvan a la misma cuenta. Se usa tanto al validar credenciales como al crear la cuenta
/// (Módulo 2), y es lo que se guarda en la columna con índice único.
///
/// Las mayúsculas invariantes evitan que la cultura del servidor cambie el resultado: en turco, por
/// ejemplo, "i" en mayúscula no es "I", y eso alcanzaría para que un usuario no pudiera entrar.
/// </summary>
public static class NormalizadorUsername
{
    public static string Normalizar(string? username) =>
        (username ?? string.Empty).Trim().ToUpperInvariant();
}
