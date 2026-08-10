namespace GT.Domain.Flota;

/// <summary>
/// Normalización de una patente antes de compararla y de guardarla (FR-003).
///
/// Pasa a mayúsculas y descarta todo lo que no sea letra o dígito, así que <c>ab 123 cd</c>,
/// <c>AB-123-CD</c>, <c>AB.123.CD</c> y <c>AB123CD</c> quedan todas en <c>AB123CD</c>. Sin esto,
/// dos de esas formas convivirían como dos unidades distintas, que es justo el caso límite que la
/// spec declara (FR-002).
///
/// <b>No se reutiliza <c>NormalizadorDocumentoNumerico</c></b> del Módulo 3: ése descarta las letras,
/// que en una patente son la mitad del dato (research §6).
/// </summary>
public static class NormalizadorPatente
{
    public static string Normalizar(string patente) =>
        new(patente.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
