namespace GT.Application.Reportes;

/// <summary>No se entrega ningún archivo si una celda monetaria o total pierde centavos.</summary>
public sealed class PrecisionMonetariaXlsxException() : Exception(MensajesDeReporte.PrecisionMonetariaXlsx);
