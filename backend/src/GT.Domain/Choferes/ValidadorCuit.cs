namespace GT.Domain.Choferes;

/// <summary>
/// Validación de CUIT y CUIL (FR-003, FR-007).
///
/// Comprueba que sean once dígitos y que el <b>dígito verificador</b> cierre con el algoritmo
/// estándar argentino. La misma regla sirve para los dos: comparten formato.
///
/// Verificar sólo la longitud dejaría pasar cualquier número tipeado de más, y un CUIT mal cargado
/// se descubre recién cuando alguien intenta facturar. El verificador es una multiplicación y un
/// módulo: cuesta nada y atrapa la enorme mayoría de los errores de tipeo.
///
/// La autenticidad frente a AFIP queda fuera de alcance: el sistema valida la forma, no la
/// existencia del contribuyente.
/// </summary>
public static class ValidadorCuit
{
    private static readonly int[] Multiplicadores = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    /// <param name="valor">
    /// Se acepta con guiones o puntos: se normaliza antes de validar (FR-025).
    /// </param>
    public static bool EsValido(string? valor)
    {
        var digitos = NormalizadorDocumentoNumerico.Normalizar(valor);

        if (digitos.Length != 11)
        {
            return false;
        }

        var suma = 0;

        for (var posicion = 0; posicion < Multiplicadores.Length; posicion++)
        {
            suma += (digitos[posicion] - '0') * Multiplicadores[posicion];
        }

        var resto = suma % 11;

        // Las dos excepciones del algoritmo: con resto 0 el verificador es 0, y con resto 1 es 9.
        var verificadorEsperado = resto switch
        {
            0 => 0,
            1 => 9,
            _ => 11 - resto,
        };

        return digitos[10] - '0' == verificadorEsperado;
    }
}
