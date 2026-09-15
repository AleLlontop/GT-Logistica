using System.Text.Json.Serialization;
using GT.Application.Adelantos;
using GT.Application.Autenticacion;

namespace GT.Api.Adelantos;

/// <summary>
/// Traducción de un <see cref="ResultadoAdelanto"/> fallido a su respuesta HTTP, en un solo lugar
/// (research §6, convención [005]): <c>400</c> cuando el problema está en lo que se tipeó o se eligió,
/// <c>409</c> cuando está en el estado del adelanto y en las dos confirmaciones, <c>404</c> si no existe.
///
/// Un error sin código propio cae en <c>datos_invalidos</c>: una respuesta de error nunca se convierte en
/// un <c>500</c>.
/// </summary>
public static class RespuestasDeAdelanto
{
    public static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorAdelantos.AdelantoNoEncontrado,
        MensajesAdelantos.NoEncontrado));

    public static IResult TraducirFallo(ResultadoAdelanto resultado)
    {
        var mensaje = resultado.Mensaje ?? MensajesAdelantos.DatosInvalidos;

        return resultado.Error switch
        {
            ErrorAdelanto.NoEncontrado => NoEncontrado(),

            // ── 400 ─────────────────────────────────────────────────────────────────────────────
            ErrorAdelanto.FechaFueraDeRango => Results.BadRequest(
                new ErrorDeFechaDeAdelanto(CodigosErrorAdelantos.FechaFueraDeRango, mensaje, resultado.Campo)
                {
                    Desde = resultado.Desde?.ToString("yyyy-MM-dd"),
                    Hasta = resultado.Hasta?.ToString("yyyy-MM-dd"),
                }),

            ErrorAdelanto.BeneficiarioNoElegible => Results.BadRequest(
                new ErrorDeBeneficiario(CodigosErrorAdelantos.BeneficiarioNoElegible, mensaje, resultado.Campo)
                {
                    Motivo = resultado.Motivo is { } motivo ? NombresDeEstadoAdelanto.EnJson(motivo) : null,
                }),

            ErrorAdelanto.EmpresaEmisoraNoConfigurada or
            ErrorAdelanto.MotivoRequerido or
            ErrorAdelanto.RangoInvalido => Results.BadRequest(
                new ErrorResponse(CodigoDe(resultado.Error), mensaje, resultado.Campo)),

            // ── 409 ─────────────────────────────────────────────────────────────────────────────
            ErrorAdelanto.AdelantoNoResoluble or
            ErrorAdelanto.AdelantoNoAnulable or
            ErrorAdelanto.RechazoRequiereConfirmacion or
            ErrorAdelanto.AnulacionRequiereConfirmacion => Results.Json(
                new ErrorDeEstadoDeAdelanto(CodigoDe(resultado.Error), mensaje)
                {
                    Estado = resultado.Estado is { } estado ? NombresDeEstadoAdelanto.EnJson(estado) : null,
                },
                statusCode: StatusCodes.Status409Conflict),

            _ => Results.BadRequest(new ErrorResponse(
                CodigosErrorAdelantos.DatosInvalidos,
                mensaje,
                resultado.Campo)),
        };
    }

    private static string CodigoDe(ErrorAdelanto error) => error switch
    {
        ErrorAdelanto.EmpresaEmisoraNoConfigurada => CodigosErrorAdelantos.EmpresaEmisoraNoConfigurada,
        ErrorAdelanto.MotivoRequerido => CodigosErrorAdelantos.MotivoRequerido,
        ErrorAdelanto.RangoInvalido => CodigosErrorAdelantos.RangoInvalido,
        ErrorAdelanto.AdelantoNoResoluble => CodigosErrorAdelantos.AdelantoNoResoluble,
        ErrorAdelanto.AdelantoNoAnulable => CodigosErrorAdelantos.AdelantoNoAnulable,
        ErrorAdelanto.RechazoRequiereConfirmacion => CodigosErrorAdelantos.RechazoRequiereConfirmacion,
        ErrorAdelanto.AnulacionRequiereConfirmacion => CodigosErrorAdelantos.AnulacionRequiereConfirmacion,
        _ => CodigosErrorAdelantos.DatosInvalidos,
    };
}

/// <summary>Contrato §ErrorDeFecha: el rango admitido, para que la pantalla lo diga.</summary>
public record ErrorDeFechaDeAdelanto(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    public string? Desde { get; init; }

    public string? Hasta { get; init; }
}

/// <summary>Contrato §ErrorDeBeneficiario: por qué la persona no puede recibir el adelanto.</summary>
public record ErrorDeBeneficiario(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    public string? Motivo { get; init; }
}

/// <summary>Contrato §ErrorDeEstado: el estado en que está, ausente en las dos confirmaciones.</summary>
public record ErrorDeEstadoDeAdelanto(string Codigo, string Mensaje) : ErrorResponse(Codigo, Mensaje)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Estado { get; init; }
}
