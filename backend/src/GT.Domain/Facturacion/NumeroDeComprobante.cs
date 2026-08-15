using System.Text.RegularExpressions;

namespace GT.Domain.Facturacion;

/// <summary>
/// El formato del número de comprobante: <c>0000-00000000</c> (FR-027).
///
/// Cuatro dígitos de punto de venta, un guion y ocho de número correlativo. Es lo que tipea quien
/// emite, porque el número sale de AFIP/ARCA por fuera del sistema: el sistema no lo genera, lo valida.
///
/// <b>La validación es del formato y nada más.</b> Que el número sea el que AFIP autorizó no lo puede
/// saber este sistema, igual que no puede verificar que un CUIT exista. Lo que sí garantiza es que dos
/// facturas vigentes no compartan número, y eso lo sostiene el índice único filtrado.
/// </summary>
public static partial class NumeroDeComprobante
{
    /// <summary>Largo exacto: cuatro dígitos, el guion y ocho dígitos.</summary>
    public const int Largo = 13;

    [GeneratedRegex(@"^\d{4}-\d{8}$")]
    private static partial Regex Formato();

    public static bool EsValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Formato().IsMatch(valor);

    /// <summary>
    /// Arma el número a partir del punto de venta y el correlativo, para <b>proponerlo</b> en la
    /// pantalla de alta. No lo asigna: quien emite lo puede cambiar (FR-027).
    /// </summary>
    public static string Armar(string puntoDeVenta, int correlativo) =>
        $"{puntoDeVenta.PadLeft(4, '0')}-{correlativo:D8}";
}
