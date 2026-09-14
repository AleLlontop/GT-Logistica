using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Los rechazos posibles, uno por código de <see cref="CodigosErrorLiquidaciones"/>. Los de arriba salen
/// como <c>400</c> y los de abajo de la línea como <c>409</c>: <c>RespuestasDeLiquidacion</c> hace esa
/// traducción en un solo lugar.
/// </summary>
public enum ErrorLiquidacion
{
    Ninguno,
    NoEncontrada,
    DatosInvalidos,
    PeriodoInvalido,
    EmpresaEmisoraNoConfigurada,
    TransportistaNoLiquidable,
    SinViajes,
    TotalEnCero,
    ViajeNoLiquidable,
    FechaDePagoFueraDeRango,
    ImporteSuperaSaldo,
    MotivoRequerido,

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    ViajeYaLiquidado,
    LiquidacionNoEditable,
    LiquidacionModificada,
    LiquidacionNoAnulable,
    LiquidacionNoPagable,
    PagoRequiereConfirmacion,
    AnulacionRequiereConfirmacion,
}

/// <summary>Por qué un viaje pedido no entra en la liquidación (FR-011).</summary>
public enum MotivoViajeNoLiquidable
{
    YaLiquidado,
    NoRendidoNiFacturado,
    DeOtroTransportista,
    DeOtroPeriodo,
}

/// <summary>Un viaje nombrado en un rechazo, en el cuerpo además de en el mensaje (convención [004]).</summary>
/// <param name="Liquidacion">La que lo tiene, armada —<c>LQ-5</c>—, cuando el motivo es <c>yaLiquidado</c>.</param>
public record ViajeEnConflictoDeLiquidacion(int Id, int Numero, string Motivo, string? Liquidacion);

/// <summary>Los importes de la confirmación del pago, calculados por el servidor sobre el saldo actual.</summary>
public record ConfirmacionDePagoCalculada(
    decimal Importe,
    decimal RestaPagarAntes,
    decimal RestaPagarDespues,
    bool QuedaPagada);

/// <summary>
/// Lo que devuelve cualquier operación del módulo: el detalle releído, o el rechazo con todo lo que el
/// mensaje necesita nombrar.
/// </summary>
public record ResultadoLiquidacion(
    ErrorLiquidacion Error,
    LiquidacionDetalle? Liquidacion = null,
    string? Campo = null,
    string? Mensaje = null,
    IReadOnlyList<ViajeEnConflictoDeLiquidacion>? Viajes = null,
    MotivoDeBloqueoLiquidacion? Motivo = null,
    int? CantidadOrdenesDePago = null,
    decimal? ImportePagado = null,
    decimal? RestaPagar = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    ConfirmacionDePagoCalculada? Confirmacion = null)
{
    public bool Exitoso => Error is ErrorLiquidacion.Ninguno;

    public static ResultadoLiquidacion Exito(LiquidacionDetalle detalle) => new(ErrorLiquidacion.Ninguno, detalle);

    public static ResultadoLiquidacion Rechazo(ErrorLiquidacion error, string mensaje, string? campo = null) =>
        new(error, Campo: campo, Mensaje: mensaje);
}
