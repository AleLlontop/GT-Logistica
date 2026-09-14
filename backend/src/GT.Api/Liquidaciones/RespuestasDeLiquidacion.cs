using System.Text.Json.Serialization;
using GT.Application.Autenticacion;
using GT.Application.Liquidaciones;

namespace GT.Api.Liquidaciones;

/// <summary>
/// Traducción de un <see cref="ResultadoLiquidacion"/> fallido a su respuesta HTTP, en un solo lugar
/// (research §8, convención [005]): <c>400</c> cuando el problema está en lo que se tipeó o se eligió,
/// <c>409</c> cuando está en el estado de algo compartido o que cambió, y en las dos confirmaciones.
///
/// Un error sin código propio cae en <c>datos_invalidos</c>: una respuesta de error nunca se convierte en
/// un <c>500</c>.
/// </summary>
public static class RespuestasDeLiquidacion
{
    public static IResult NoEncontrada() => Results.NotFound(new ErrorResponse(
        CodigosErrorLiquidaciones.LiquidacionNoEncontrada,
        MensajesLiquidaciones.NoEncontrada));

    public static IResult TraducirFallo(ResultadoLiquidacion resultado)
    {
        var mensaje = resultado.Mensaje ?? MensajesLiquidaciones.DatosInvalidos;

        return resultado.Error switch
        {
            ErrorLiquidacion.NoEncontrada => NoEncontrada(),

            // ── 400 ─────────────────────────────────────────────────────────────────────────────
            ErrorLiquidacion.PeriodoInvalido or
            ErrorLiquidacion.EmpresaEmisoraNoConfigurada or
            ErrorLiquidacion.TransportistaNoLiquidable or
            ErrorLiquidacion.SinViajes or
            ErrorLiquidacion.TotalEnCero or
            ErrorLiquidacion.ViajeNoLiquidable or
            ErrorLiquidacion.MotivoRequerido => Results.BadRequest(ConViajes(resultado, mensaje)),

            ErrorLiquidacion.FechaDePagoFueraDeRango or
            ErrorLiquidacion.ImporteSuperaSaldo => Results.BadRequest(
                new ErrorDePago(CodigoDe(resultado.Error), mensaje, resultado.Campo)
                {
                    RestaPagar = resultado.RestaPagar,
                    Desde = resultado.Desde?.ToString("yyyy-MM-dd"),
                    Hasta = resultado.Hasta?.ToString("yyyy-MM-dd"),
                }),

            // ── 409 ─────────────────────────────────────────────────────────────────────────────
            ErrorLiquidacion.ViajeYaLiquidado => Conflicto(ConViajes(resultado, mensaje)),

            ErrorLiquidacion.LiquidacionNoEditable or
            ErrorLiquidacion.LiquidacionModificada or
            ErrorLiquidacion.LiquidacionNoAnulable or
            ErrorLiquidacion.LiquidacionNoPagable or
            ErrorLiquidacion.AnulacionRequiereConfirmacion => Conflicto(
                new ErrorDeEstado(CodigoDe(resultado.Error), mensaje, resultado.Campo)
                {
                    Motivo = resultado.Motivo is { } motivo ? NombresDeEstadoLiquidacion.EnJson(motivo) : null,
                    CantidadOrdenesDePago = resultado.CantidadOrdenesDePago,
                    ImportePagado = resultado.ImportePagado,
                }),

            // No cambió nada: el primer intento sólo pide confirmación con los importes del servidor.
            ErrorLiquidacion.PagoRequiereConfirmacion when resultado.Confirmacion is { } confirmacion => Conflicto(
                new ConfirmacionDePago(CodigosErrorLiquidaciones.PagoRequiereConfirmacion, mensaje)
                {
                    Importe = confirmacion.Importe,
                    RestaPagarAntes = confirmacion.RestaPagarAntes,
                    RestaPagarDespues = confirmacion.RestaPagarDespues,
                    QuedaPagada = confirmacion.QuedaPagada,
                }),

            _ => Results.BadRequest(new ErrorResponse(
                CodigosErrorLiquidaciones.DatosInvalidos,
                mensaje,
                resultado.Campo)),
        };
    }

    private static ErrorConViajes ConViajes(ResultadoLiquidacion resultado, string mensaje) =>
        new(CodigoDe(resultado.Error), mensaje, resultado.Campo) { Viajes = resultado.Viajes };

    /// <summary>Genérico para que se serialice el tipo concreto del cuerpo, con sus campos propios.</summary>
    private static IResult Conflicto<TCuerpo>(TCuerpo cuerpo) =>
        Results.Json(cuerpo, statusCode: StatusCodes.Status409Conflict);

    private static string CodigoDe(ErrorLiquidacion error) => error switch
    {
        ErrorLiquidacion.PeriodoInvalido => CodigosErrorLiquidaciones.PeriodoInvalido,
        ErrorLiquidacion.EmpresaEmisoraNoConfigurada => CodigosErrorLiquidaciones.EmpresaEmisoraNoConfigurada,
        ErrorLiquidacion.TransportistaNoLiquidable => CodigosErrorLiquidaciones.TransportistaNoLiquidable,
        ErrorLiquidacion.SinViajes => CodigosErrorLiquidaciones.SinViajes,
        ErrorLiquidacion.TotalEnCero => CodigosErrorLiquidaciones.TotalEnCero,
        ErrorLiquidacion.ViajeNoLiquidable => CodigosErrorLiquidaciones.ViajeNoLiquidable,
        ErrorLiquidacion.FechaDePagoFueraDeRango => CodigosErrorLiquidaciones.FechaDePagoFueraDeRango,
        ErrorLiquidacion.ImporteSuperaSaldo => CodigosErrorLiquidaciones.ImporteSuperaSaldo,
        ErrorLiquidacion.MotivoRequerido => CodigosErrorLiquidaciones.MotivoRequerido,
        ErrorLiquidacion.ViajeYaLiquidado => CodigosErrorLiquidaciones.ViajeYaLiquidado,
        ErrorLiquidacion.LiquidacionNoEditable => CodigosErrorLiquidaciones.LiquidacionNoEditable,
        ErrorLiquidacion.LiquidacionModificada => CodigosErrorLiquidaciones.LiquidacionModificada,
        ErrorLiquidacion.LiquidacionNoAnulable => CodigosErrorLiquidaciones.LiquidacionNoAnulable,
        ErrorLiquidacion.LiquidacionNoPagable => CodigosErrorLiquidaciones.LiquidacionNoPagable,
        ErrorLiquidacion.AnulacionRequiereConfirmacion => CodigosErrorLiquidaciones.AnulacionRequiereConfirmacion,
        _ => CodigosErrorLiquidaciones.DatosInvalidos,
    };
}

/// <summary>Rechazo que nombra los viajes que lo producen, uno por uno (contrato §ErrorConViajes).</summary>
public record ErrorConViajes(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ViajeEnConflictoDeLiquidacion>? Viajes { get; init; }
}

/// <summary>Rechazo por el estado de la liquidación, con su motivo y, con pagos, cuántos y por cuánto.</summary>
public record ErrorDeEstado(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Motivo { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadOrdenesDePago { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? ImportePagado { get; init; }
}

/// <summary>Rechazo de una orden de pago, con el saldo o el rango permitido.</summary>
public record ErrorDePago(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? RestaPagar { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Desde { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hasta { get; init; }
}

/// <summary>El <c>409</c> de FR-040, con los importes que la pantalla muestra en el paso de confirmación.</summary>
public record ConfirmacionDePago(string Codigo, string Mensaje) : ErrorResponse(Codigo, Mensaje)
{
    public decimal Importe { get; init; }

    public decimal RestaPagarAntes { get; init; }

    public decimal RestaPagarDespues { get; init; }

    public bool QuedaPagada { get; init; }
}
