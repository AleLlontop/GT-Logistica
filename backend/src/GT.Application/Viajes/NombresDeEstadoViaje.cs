using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Traducción de <see cref="EstadoViaje"/> entre el enum del dominio, el JSON y la pantalla.
///
/// El JSON usa <b>camelCase</b> —<c>enCurso</c>, no <c>EnCurso</c>— igual que los enums de los
/// Módulos 3 y 4 (convención [003]).
/// </summary>
public static class NombresDeEstadoViaje
{
    public static string EnJson(EstadoViaje estado) => estado switch
    {
        EstadoViaje.Pendiente => "pendiente",
        EstadoViaje.EnCurso => "enCurso",
        EstadoViaje.Rendido => "rendido",
        EstadoViaje.Anulado => "anulado",
        EstadoViaje.Facturado => "facturado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    public static string? EnJson(EstadoViaje? estado) => estado is { } valor ? EnJson(valor) : null;

    /// <summary>
    /// Lee el filtro de estado de la query.
    ///
    /// Un valor desconocido devuelve <c>null</c> y el filtro se ignora, en vez de romper: filtrar de
    /// más no es un error, y el listado responde su vista por defecto —todos menos los anulados—
    /// (convención [003], FR-044).
    /// </summary>
    public static EstadoViaje? Leer(string? valor) => valor switch
    {
        "pendiente" => EstadoViaje.Pendiente,
        "enCurso" => EstadoViaje.EnCurso,
        "rendido" => EstadoViaje.Rendido,
        "anulado" => EstadoViaje.Anulado,

        // Módulo 6, FR-055: el filtro de estado del listado acepta `facturado`.
        "facturado" => EstadoViaje.Facturado,

        _ => null,
    };

    /// <summary>
    /// La palabra que <b>la pantalla muestra</b>, para los reportes del Módulo 12 (FR-010).
    ///
    /// <b>Es una copia declarada de <c>NOMBRES_DE_ESTADO</c> de
    /// <c>frontend/src/modules/viajes/servicios/servicioViajes.ts</c></b>, y la fija el test
    /// <c>PalabrasDeEstadoTests</c>. Existe porque <see cref="EnJson"/> devuelve el <i>código</i> del
    /// contrato —<c>enCurso</c>— y las palabras visibles viven sólo en TypeScript: un reporte que se
    /// arma en C# no puede llamarlas (data-model §2.5, research §14). Si la palabra cambia en la
    /// pantalla, cambia acá.
    /// </summary>
    public static string EnPantalla(EstadoViaje estado) => estado switch
    {
        EstadoViaje.Pendiente => "Pendiente",
        EstadoViaje.EnCurso => "En curso",
        EstadoViaje.Rendido => "Rendido",
        EstadoViaje.Anulado => "Anulado",
        EstadoViaje.Facturado => "Facturado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    /// <summary>
    /// Cómo se nombra el estado <b>dentro de un mensaje</b> en español, con la minúscula que pide la
    /// oración: "El viaje 1041 está rendido y no se puede modificar", "No se puede pasar el viaje
    /// 1041 de pendiente a rendido" (contracts/README.md).
    /// </summary>
    public static string EnTexto(EstadoViaje estado) => estado switch
    {
        EstadoViaje.Pendiente => "pendiente",
        EstadoViaje.EnCurso => "en curso",
        EstadoViaje.Rendido => "rendido",
        EstadoViaje.Anulado => "anulado",
        EstadoViaje.Facturado => "facturado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };
}
