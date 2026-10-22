namespace GT.Application.Reportes.Analisis;

public sealed class AnalisisNoDisponibleException(Exception? causa = null)
    : Exception("El análisis no está disponible en este momento. Volvé a intentar en unos minutos.", causa)
{
    public const string Codigo = "analisis_no_disponible";
}
