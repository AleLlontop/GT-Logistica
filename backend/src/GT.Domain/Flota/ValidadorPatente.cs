using System.Text.RegularExpressions;

namespace GT.Domain.Flota;

/// <summary>
/// Formato de una patente argentina (FR-004), de los dos que conviven hoy en la ruta.
///
/// <b>Se valida sobre el valor ya normalizado</b>, y el orden importa: si se validara antes de
/// normalizar, <c>AB-123-CD</c> sería rechazada por formato en vez de aceptada como la patente que es
/// (research §6).
/// </summary>
public static partial class ValidadorPatente
{
    /// <summary>Formato viejo: tres letras y tres dígitos, por ejemplo <c>ABC123</c>.</summary>
    [GeneratedRegex(@"^[A-Z]{3}[0-9]{3}$")]
    private static partial Regex FormatoViejo();

    /// <summary>Formato Mercosur: dos letras, tres dígitos y dos letras, por ejemplo <c>AB123CD</c>.</summary>
    [GeneratedRegex(@"^[A-Z]{2}[0-9]{3}[A-Z]{2}$")]
    private static partial Regex FormatoMercosur();

    /// <param name="patenteNormalizada">
    /// La patente ya pasada por <see cref="NormalizadorPatente.Normalizar"/>. Pasarle una sin
    /// normalizar devuelve <c>false</c> para valores que en realidad son válidos.
    /// </param>
    public static bool EsValida(string patenteNormalizada) =>
        FormatoViejo().IsMatch(patenteNormalizada) || FormatoMercosur().IsMatch(patenteNormalizada);
}
