namespace GT.Application.Choferes;

/// <summary>
/// Una página de resultados (FR-030).
///
/// Esta forma —<c>items</c> + <c>total</c> + <c>pagina</c> + <c>tamanioPagina</c>— es la primera
/// paginación del sistema y queda como precedente para los módulos siguientes (research §9).
/// </summary>
/// <param name="Total">
/// Coincidencias con los filtros aplicados sobre <b>todo</b> el padrón, no las de esta página. Es lo
/// que permite decir "mostrando 20 de 73" en vez de sólo "20".
/// </param>
public record PaginaDe<T>(IReadOnlyList<T> Items, int Total, int Pagina, int TamanioPagina)
{
    /// <summary>
    /// Filas por página. Fijo, no configurable: la spec lo decide y nadie pidió elegirlo desde la
    /// pantalla.
    /// </summary>
    public const int TamanioPorDefecto = 20;

    public static PaginaDe<T> Vacia(int pagina) => new([], 0, pagina, TamanioPorDefecto);
}
