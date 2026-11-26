namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Comparación de búsquedas de nombres y texto de negocio: ignora mayúsculas y acentos,
/// pero conserva la ñ como letra distinta. Se aplica sólo al predicado SQL, antes de contar
/// y paginar; no cambia los datos, su orden, la collation de las columnas ni la unicidad.
/// </summary>
internal static class BusquedaTextoSql
{
    public const string Colacion = "Modern_Spanish_100_CI_AI";
}
