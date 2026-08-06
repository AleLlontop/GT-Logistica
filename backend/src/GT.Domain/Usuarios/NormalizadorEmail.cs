namespace GT.Domain.Usuarios;

/// <summary>
/// Normalización del email (FR-020).
///
/// Recorta espacios al costado y pasa a minúsculas invariantes, de modo que " Juan@GT.com " y
/// "juan@gt.com" no puedan convivir como dos usuarios distintos. Es lo que se guarda en la columna
/// con índice único, y también la columna por la que filtra el listado (research §4).
///
/// Minúsculas invariantes por el mismo motivo que el username usa mayúsculas invariantes: la cultura
/// del servidor no puede cambiar el resultado.
/// </summary>
public static class NormalizadorEmail
{
    public static string Normalizar(string? email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();
}
