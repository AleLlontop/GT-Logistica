using System.Globalization;

namespace GT.Application.Reportes.Analisis;

public static class NombreCajaAnalisis
{
    public static string Desde(string responsable, DateTime aperturaUtc) =>
        $"{responsable} · {aperturaUtc.AddHours(-3).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
}
