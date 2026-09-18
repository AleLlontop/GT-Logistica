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
