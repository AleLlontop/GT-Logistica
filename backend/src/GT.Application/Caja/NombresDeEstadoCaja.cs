using GT.Domain.Caja;

namespace GT.Application.Caja;

/// <summary>Traducción de las enumeraciones del módulo al JSON, en <b>camelCase</b> (convención [003]).</summary>
public static class NombresDeEstadoCaja
{
    public static string EnJson(EstadoCaja estado) => estado switch
    {
        EstadoCaja.Abierta => "abierta",
        EstadoCaja.Cerrada => "cerrada",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    public static string EnJson(TipoMovimientoCaja tipo) => tipo switch
    {
        TipoMovimientoCaja.Ingreso => "ingreso",
        TipoMovimientoCaja.Egreso => "egreso",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// La palabra que <b>la tabla de movimientos muestra</b>, para los reportes del Módulo 12 (FR-010).
    ///
    /// Con <b>mayúscula inicial</b>: no es el <c>ingreso</c>/<c>egreso</c> del JSON, que es un código.
    /// La fija el test <c>PalabrasDeEstadoTests</c> (data-model §2.5, research §14).
    /// </summary>
    public static string EnPantalla(TipoMovimientoCaja tipo) => tipo switch
    {
        TipoMovimientoCaja.Ingreso => "Ingreso",
        TipoMovimientoCaja.Egreso => "Egreso",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// Lee el tipo del cuerpo. <b>Estricto</b>: el tipo es obligatorio, así que ausente o desconocido devuelve
    /// <c>null</c> y quien llama lo rechaza como dato inválido.
    /// </summary>
    public static TipoMovimientoCaja? LeerTipo(string? valor) => valor switch
    {
        "ingreso" => TipoMovimientoCaja.Ingreso,
        "egreso" => TipoMovimientoCaja.Egreso,
        _ => null,
    };
}
