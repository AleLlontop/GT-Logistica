using GT.Application.Autenticacion;
using GT.Application.Viajes;

namespace GT.Api.Viajes;

/// <summary>
/// Traducción de un <see cref="ResultadoViaje"/> fallido a su respuesta HTTP.
///
/// Vive en un solo lugar y no repetida en los cuatro grupos de endpoints del módulo, porque la regla
/// de códigos que aplica es una sola y transversal (research §5):
///
/// <list type="bullet">
///   <item><b><c>400</c></b> cuando el problema está en <b>lo que se tipeó</b>: campos, duplicados,
///   dependencias.</item>
///   <item><b><c>409</c></b> cuando está en el <b>estado</b> de algo que se comparte o que cambió:
///   unidad ocupada, transición no permitida, viaje inmutable, confirmación pendiente.</item>
/// </list>
///
/// Con eso el frontend sabe, sin leer el código del backend, si tiene que marcar un campo o abrir un
/// diálogo.
/// </summary>
public static class RespuestasDeViaje
{
    public static IResult NoEncontrado() => Results.NotFound(
        new ErrorResponse(CodigosErrorViajes.NoEncontrado, MensajesViajes.NoEncontrado));

    public static IResult TraducirFallo(ResultadoViaje resultado)
    {
        var numero = resultado.NumeroDelViaje ?? 0;
        var relacionado = resultado.NumeroDeViajeRelacionado ?? 0;
        var unidad = resultado.Unidad ?? "la unidad";

        return resultado.Error switch
        {
            ErrorViaje.NoEncontrado => NoEncontrado(),

            // ── 400: el problema está en lo que se tipeó ───────────────────────────────────────
            ErrorViaje.ClienteInexistente => Invalido(
                CodigosErrorViajes.ClienteInexistente,
                MensajesViajes.ClienteInexistente,
                resultado.Campo),

            ErrorViaje.RemitoDuplicado => Invalido(
                CodigosErrorViajes.RemitoDuplicado,
                MensajesViajes.RemitoDuplicado(relacionado),
                resultado.Campo),

            ErrorViaje.ImporteNegativo => Invalido(
                CodigosErrorViajes.ImporteNegativo,
                MensajesViajes.ImporteNegativo,
                resultado.Campo),

            ErrorViaje.MotivoRequerido => Invalido(
                CodigosErrorViajes.MotivoRequerido, MensajesViajes.MotivoRequerido, resultado.Campo),

            ErrorViaje.ChoferInexistente => Invalido(
                CodigosErrorViajes.ChoferInexistente,
                MensajesViajes.ChoferInexistente,
                resultado.Campo),

            ErrorViaje.VehiculoInexistente => Invalido(
                CodigosErrorViajes.VehiculoInexistente,
                MensajesViajes.VehiculoInexistente,
                resultado.Campo),

            ErrorViaje.RangoDeFechasRequerido => Invalido(
                CodigosErrorViajes.RangoDeFechasRequerido,
                MensajesViajes.RangoDeFechasRequerido,
                resultado.Campo),

            // ── 409: el problema está en el estado de algo compartido o que cambió ─────────────
            ErrorViaje.ViajeRendidoInmutable => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.ViajeRendidoInmutable,
                MensajesViajes.ViajeRendidoInmutable(numero))),

            ErrorViaje.ViajeAnuladoInmutable => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.ViajeAnuladoInmutable,
                MensajesViajes.ViajeAnuladoInmutable(numero))),

            ErrorViaje.TransicionNoPermitida => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.TransicionNoPermitida,
                MensajesViajes.TransicionNoPermitida(
                    numero,
                    resultado.EstadoActual ?? "",
                    resultado.EstadoPedido ?? ""))),

            ErrorViaje.FaltaAsignacion => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.FaltaAsignacion, MensajesViajes.FaltaAsignacion)),

            ErrorViaje.UnidadDadaDeBaja => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.UnidadDadaDeBaja, MensajesViajes.UnidadDadaDeBaja(unidad))
            {
                UnidadQueBloquea = resultado.Unidad,
            }),

            // El rechazo nombra el viaje que la ocupa, en el texto **y** en el cuerpo: saber que está
            // ocupada sin saber por qué no ayuda a resolverlo (FR-026).
            ErrorViaje.ChoferOcupado => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.ChoferOcupado,
                MensajesViajes.ChoferOcupado(unidad, relacionado))
            {
                ViajeQueOcupa = resultado.NumeroDeViajeRelacionado,
                UnidadQueBloquea = resultado.Unidad,
            }),

            ErrorViaje.VehiculoOcupado => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.VehiculoOcupado,
                MensajesViajes.VehiculoOcupado(unidad, relacionado))
            {
                ViajeQueOcupa = resultado.NumeroDeViajeRelacionado,
                UnidadQueBloquea = resultado.Unidad,
            }),

            // No cambió nada: el primer intento sólo pide confirmación (FR-038, SC-007a).
            ErrorViaje.RendicionRequiereConfirmacion => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.RendicionRequiereConfirmacion,
                MensajesViajes.RendicionRequiereConfirmacion)),

            // Qué unidad y qué documento lo impiden, en el cuerpo además de en el texto (SC-004).
            ErrorViaje.DocumentacionVencida => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.DocumentacionVencida,
                MensajesViajes.DocumentacionVencida(
                    unidad,
                    resultado.Documento ?? "",
                    resultado.NumeroDocumento ?? "",
                    resultado.FechaDeReferencia ?? ""))
            {
                UnidadQueBloquea = resultado.Unidad,
                DocumentoQueBloquea = Documento(resultado),
            }),

            ErrorViaje.AsignacionNoPermitida => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.AsignacionNoPermitida,
                MensajesViajes.AsignacionNoPermitida(numero, resultado.EstadoActual ?? ""))),

            ErrorViaje.FechaBloqueaAsignacion => Conflicto(new ErrorDeBloqueo(
                CodigosErrorViajes.FechaBloqueaAsignacion,
                MensajesViajes.FechaBloqueaAsignacion(
                    resultado.FechaDeReferencia ?? "",
                    resultado.Documento ?? "",
                    unidad),
                resultado.Campo)
            {
                UnidadQueBloquea = resultado.Unidad,
                DocumentoQueBloquea = Documento(resultado),
            }),

            // Cualquier error sin código propio se comunica como datos inválidos, con el campo
            // marcado. Nunca se cae: una respuesta de error no puede convertirse en un 500.
            _ => Invalido(
                CodigosErrorViajes.DatosInvalidos, MensajesViajes.DatosInvalidos, resultado.Campo),
        };
    }

    private static string? Documento(ResultadoViaje resultado) =>
        resultado.Documento is null
            ? null
            : $"{resultado.Documento} N° {resultado.NumeroDocumento}";

    private static IResult Invalido(string codigo, string mensaje, string? campo) =>
        Results.BadRequest(new ErrorResponse(codigo, mensaje, campo));

    private static IResult Conflicto(ErrorDeBloqueo cuerpo) =>
        Results.Json(cuerpo, statusCode: StatusCodes.Status409Conflict);
}
