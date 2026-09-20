namespace GT.Application.Reportes;

/// <summary>
/// Los filtros aplicados, en palabras, para el encabezado del archivo (FR-006, research §9).
///
/// Una sola forma para los cinco reportes: pares <c>Etiqueta: valor</c> unidos por <c> · </c>, o el
/// literal <c>Sin filtros aplicados</c> cuando no hay ninguno.
///
/// <code>
/// Cliente: Aceitera del Sur · Estado: En curso · Desde: 01/09/2026 · Hasta: 20/09/2026
/// Sin filtros aplicados
/// </code>
///
/// **La arma el backend y el frontend no manda ningún texto.** La pantalla tiene identificadores
/// —<c>clienteId=7</c>—, no nombres, y cada fuente los resuelve contra los repositorios que ya
/// existen. Escrito en C# y en TypeScript serían dos formatos que se pueden separar (convención [009]).
/// </summary>
public class LineaDeFiltros
{
    public const string SinFiltros = "Sin filtros aplicados";

    private const string Separador = " · ";

    private readonly List<string> partes = [];

    /// <summary>Agrega el par sólo si hay valor: un filtro no aplicado no figura en la línea.</summary>
    public LineaDeFiltros Con(string etiqueta, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            partes.Add($"{etiqueta}: {valor.Trim()}");
        }

        return this;
    }

    /// <summary>Las fechas van en <c>dd/MM/yyyy</c>, como la pantalla las muestra.</summary>
    public LineaDeFiltros Con(string etiqueta, DateOnly? fecha) =>
        Con(etiqueta, fecha?.ToString("dd/MM/yyyy"));

    public override string ToString() =>
        partes.Count == 0 ? SinFiltros : string.Join(Separador, partes);
}
