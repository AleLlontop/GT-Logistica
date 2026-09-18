using GT.Application.Choferes;

namespace GT.Application.Caja;

/// <summary>
/// Los rechazos posibles, uno por código de <see cref="CodigosErrorCaja"/>. Los de arriba salen como
/// <c>400</c> —o <c>404</c> el primero— y los de abajo de la línea como <c>409</c>: <c>RespuestasDeCaja</c>
/// hace esa traducción en un solo lugar.
/// </summary>
public enum ErrorCaja
{
    Ninguno,
    CajaNoEncontrada,
    DatosInvalidos,
    ReferenciaInvalida,
    RangoInvalido,

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    CajaYaAbierta,
    CajaCerrada,
    CajaAjena,
    ConfirmacionRequerida,
    CierreDesactualizado,
}

/// <summary>
/// Lo que devuelve cualquier operación del módulo: lo que se pidió, o el rechazo con todo lo que su cuerpo
/// de error necesita.
/// </summary>
/// <param name="Resumen">
/// Con <c>confirmacion_requerida</c> y <c>cierre_desactualizado</c>: el resumen actual. También es el
/// resultado de la consulta del resumen de cierre.
/// </param>
public record ResultadoCaja(
    ErrorCaja Error,
    CajaDetalle? Caja = null,
    MovimientoListado? Movimiento = null,
    ResumenDeCierre? Resumen = null,
    string? Campo = null,
    string? Mensaje = null)
{
    public bool Exitoso => Error is ErrorCaja.Ninguno;

    public static ResultadoCaja Exito(CajaDetalle caja) => new(ErrorCaja.Ninguno, Caja: caja);

    public static ResultadoCaja Exito(MovimientoListado movimiento) =>
        new(ErrorCaja.Ninguno, Movimiento: movimiento);

    public static ResultadoCaja Exito(ResumenDeCierre resumen) => new(ErrorCaja.Ninguno, Resumen: resumen);

    public static ResultadoCaja Rechazo(ErrorCaja error, string mensaje, string? campo = null) =>
        new(error, Campo: campo, Mensaje: mensaje);

    public static ResultadoCaja Invalido(string mensaje, string campo) =>
        Rechazo(ErrorCaja.DatosInvalidos, mensaje, campo);

    public static ResultadoCaja NoEncontrada() => Rechazo(ErrorCaja.CajaNoEncontrada, MensajesCaja.NoEncontrada);

    /// <summary>La caja no se puede operar por quien la pide: no existe, está cerrada o es de otro.</summary>
    public static ResultadoCaja NoOperable(MotivoNoOperable motivo) => motivo switch
    {
        MotivoNoOperable.Cerrada => Rechazo(ErrorCaja.CajaCerrada, MensajesCaja.CajaCerrada),
        MotivoNoOperable.Ajena => Rechazo(ErrorCaja.CajaAjena, MensajesCaja.CajaAjena),
        _ => NoEncontrada(),
    };

    public static ResultadoCaja ConResumen(ErrorCaja error, string mensaje, ResumenDeCierre resumen) =>
        new(error, Resumen: resumen, Mensaje: mensaje);
}

/// <summary>El listado de movimientos, o el rechazo del rango invertido, que no llega a consultar (FR-023).</summary>
public record ResultadoConsultaMovimientos(PaginaDe<MovimientoListado>? Pagina, ResultadoCaja? Rechazo);
