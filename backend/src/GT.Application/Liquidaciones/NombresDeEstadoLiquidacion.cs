using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Traducción de las enumeraciones del módulo al JSON, en <b>camelCase</b> (convención [003]). El estado
/// de los viajes lo traduce <c>NombresDeEstadoViaje</c> del Módulo 5, sin copiarlo.
///
/// La declaración del filtro en español —"Mostrando sólo las liquidaciones pendientes"— la arma la
/// pantalla, igual que en el listado de facturas: el servidor no la usa en ningún mensaje.
/// </summary>
public static class NombresDeEstadoLiquidacion
{
    public static string EnJson(EstadoLiquidacion estado) => estado switch
    {
        EstadoLiquidacion.Pendiente => "pendiente",
        EstadoLiquidacion.Pagada => "pagada",
        EstadoLiquidacion.Anulada => "anulada",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    /// <summary>
    /// Lee el filtro de estado de la query. Un valor desconocido devuelve <c>null</c> y el filtro se
    /// ignora en vez de romper: el listado responde su vista por defecto, todas incluidas las anuladas.
    /// </summary>
    public static EstadoLiquidacion? LeerEstado(string? valor) => valor switch
    {
        "pendiente" => EstadoLiquidacion.Pendiente,
        "pagada" => EstadoLiquidacion.Pagada,
        "anulada" => EstadoLiquidacion.Anulada,
        _ => null,
    };

    public static string EnJson(OperacionDeLiquidacion operacion) => operacion switch
    {
        OperacionDeLiquidacion.Generacion => "generacion",
        OperacionDeLiquidacion.Edicion => "edicion",
        OperacionDeLiquidacion.Anulacion => "anulacion",
        OperacionDeLiquidacion.Pagada => "pagada",
        _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null),
    };

    public static string EnJson(MotivoDeBloqueoLiquidacion motivo) => motivo switch
    {
        MotivoDeBloqueoLiquidacion.Pagada => "pagada",
        MotivoDeBloqueoLiquidacion.Anulada => "anulada",
        MotivoDeBloqueoLiquidacion.ConOrdenesDePago => "conOrdenesDePago",
        MotivoDeBloqueoLiquidacion.TransportistaNoLiquidable => "transportistaNoLiquidable",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    public static string EnJson(MotivoViajeNoLiquidable motivo) => motivo switch
    {
        MotivoViajeNoLiquidable.YaLiquidado => "yaLiquidado",
        MotivoViajeNoLiquidable.NoRendidoNiFacturado => "noRendidoNiFacturado",
        MotivoViajeNoLiquidable.DeOtroTransportista => "deOtroTransportista",
        MotivoViajeNoLiquidable.DeOtroPeriodo => "deOtroPeriodo",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };
}
