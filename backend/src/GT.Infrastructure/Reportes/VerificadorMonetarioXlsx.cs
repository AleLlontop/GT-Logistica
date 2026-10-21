using System.Globalization;

namespace GT.Infrastructure.Reportes;

/// <summary>Frontera numérica XLSX; nunca cambia el decimal fuente ni calcula importes de negocio.</summary>
internal static class VerificadorMonetarioXlsx
{
    internal static bool Coincide(decimal fuente, double recuperado)
    {
        if (decimal.Round(fuente, 2, MidpointRounding.AwayFromZero) != fuente || !double.IsFinite(recuperado))
            return false;
        // No usar cast decimal(double): puede reducir dígitos antes de evaluar el centavo perdido.
        if (!decimal.TryParse(recuperado.ToString("R", CultureInfo.InvariantCulture),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var leido)) return false;
        // Escala monetaria de la lectura exclusivamente para comparar; no se modifica ningún dato del libro.
        return decimal.Round(leido, 2, MidpointRounding.AwayFromZero) == fuente;
    }
}
