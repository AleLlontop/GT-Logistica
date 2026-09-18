using GT.Application.Autenticacion;
using GT.Application.Caja;

namespace GT.Api.Caja;

/// <summary>
/// Traducción de un <see cref="ResultadoCaja"/> fallido a su respuesta HTTP, en un solo lugar (convención
/// [005]): <c>400</c> cuando el problema está en lo que se tipeó o se eligió, <c>409</c> cuando está en el
/// estado de la caja y en las dos respuestas del cierre, <c>404</c> si no existe.
///
/// Un error sin código propio cae en <c>datos_invalidos</c>: una respuesta de error nunca se convierte en un
/// <c>500</c>.
/// </summary>
public static class RespuestasDeCaja
{
    public static IResult NoEncontrada() => Results.NotFound(new ErrorResponse(
        CodigosErrorCaja.CajaNoEncontrada,
        MensajesCaja.NoEncontrada));

    public static IResult TraducirFallo(ResultadoCaja resultado)
    {
        var mensaje = resultado.Mensaje ?? MensajesCaja.DatosInvalidos;

        return resultado.Error switch
        {
            ErrorCaja.CajaNoEncontrada => NoEncontrada(),

            // ── 400 ─────────────────────────────────────────────────────────────────────────────
            ErrorCaja.ReferenciaInvalida or
            ErrorCaja.RangoInvalido => Results.BadRequest(
                new ErrorResponse(CodigoDe(resultado.Error), mensaje, resultado.Campo)),

            // ── 409 ─────────────────────────────────────────────────────────────────────────────
            ErrorCaja.ConfirmacionRequerida or
            ErrorCaja.CierreDesactualizado when resultado.Resumen is { } resumen => Results.Json(
                new ErrorDeCierre(CodigoDe(resultado.Error), mensaje)
                {
                    SaldoInicial = resumen.SaldoInicial,
                    TotalIngresos = resumen.TotalIngresos,
                    TotalEgresos = resumen.TotalEgresos,
                    Movimientos = resumen.Movimientos,
                    SaldoFinal = resumen.SaldoFinal,
                },
                statusCode: StatusCodes.Status409Conflict),

            ErrorCaja.CajaYaAbierta or
            ErrorCaja.CajaCerrada or
            ErrorCaja.CajaAjena => Results.Json(
                new ErrorResponse(CodigoDe(resultado.Error), mensaje),
                statusCode: StatusCodes.Status409Conflict),

            _ => Results.BadRequest(new ErrorResponse(
                CodigosErrorCaja.DatosInvalidos,
                mensaje,
                resultado.Campo)),
        };
    }

    private static string CodigoDe(ErrorCaja error) => error switch
    {
        ErrorCaja.ReferenciaInvalida => CodigosErrorCaja.ReferenciaInvalida,
        ErrorCaja.RangoInvalido => CodigosErrorCaja.RangoInvalido,
        ErrorCaja.CajaYaAbierta => CodigosErrorCaja.CajaYaAbierta,
        ErrorCaja.CajaCerrada => CodigosErrorCaja.CajaCerrada,
        ErrorCaja.CajaAjena => CodigosErrorCaja.CajaAjena,
        ErrorCaja.ConfirmacionRequerida => CodigosErrorCaja.ConfirmacionRequerida,
        ErrorCaja.CierreDesactualizado => CodigosErrorCaja.CierreDesactualizado,
        _ => CodigosErrorCaja.DatosInvalidos,
    };
}

/// <summary>
/// Contrato §Cuerpos de error: <c>confirmacion_requerida</c> y <c>cierre_desactualizado</c> traen el mismo
/// resumen que el <c>GET</c> del cierre, para que la pantalla lo muestre sin otra llamada (research §3).
/// </summary>
public record ErrorDeCierre(string Codigo, string Mensaje) : ErrorResponse(Codigo, Mensaje)
{
    public decimal SaldoInicial { get; init; }

    public decimal TotalIngresos { get; init; }

    public decimal TotalEgresos { get; init; }

    public IReadOnlyList<MovimientoListado> Movimientos { get; init; } = [];

    public decimal SaldoFinal { get; init; }
}
