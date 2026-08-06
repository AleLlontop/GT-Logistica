namespace GT.Domain.Choferes;

/// <summary>
/// Normalización de DNI, CUIL y CUIT antes de validar su unicidad (FR-025).
///
/// Deja sólo dígitos, descartando espacios, guiones y puntos. Es lo que hace que
/// <c>20-12345678-3</c> y <c>20123456783</c> no puedan convivir como dos registros distintos, que es
/// el caso límite que la spec declara.
///
/// Sigue el mismo patrón que <c>NormalizadorEmail</c> y <c>NormalizadorUsername</c> de los módulos
/// anteriores: se normaliza al crear y al modificar, y lo normalizado es lo que se guarda en la
/// columna con índice único.
/// </summary>
public static class NormalizadorDocumentoNumerico
{
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        return string.Concat(valor.Where(char.IsAsciiDigit));
    }
}
