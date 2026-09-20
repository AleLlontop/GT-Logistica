using GT.Application.Autenticacion;
using GT.Application.Reportes;

namespace GT.Api.Reportes;

/// <summary>
/// Traducción de un <see cref="ResultadoReporte"/> a su respuesta HTTP, en un solo lugar.
///
/// Los cuatro cuerpos del contrato y sus códigos (convención [005]):
/// <list type="bullet">
///   <item><c>200</c> con el archivo, su <c>Content-Type</c>, su <c>Content-Disposition</c> —
///   <c>inline</c> para el PDF y <c>attachment</c> para el Excel— y <c>X-Content-Type-Options:
///   nosniff</c>.</item>
///   <item><c>400 formato_invalido</c>: el problema está en lo que se pidió.</item>
///   <item><c>403 sin_permiso</c>: lo emite el pipeline, con el texto que el endpoint declara.</item>
///   <item><c>409 tope_de_filas_superado</c>, <b>con <c>filas</c> y <c>tope</c> además del mensaje ya
///   armado</b>: el problema está en cuántas filas hay ahora, que cambia sin que nadie toque el
///   filtro.</item>
///   <item><c>500 reporte_no_generado</c>: falló el motor de PDF o de planilla.</item>
/// </list>
///
/// **Los cuatro textos los escribe el backend.** La pantalla los muestra tal cual llegan y no compone
/// ninguno en TypeScript (research §4).
/// </summary>
public static class RespuestasDeReporte
{
    public static IResult FormatoInvalido() => Results.BadRequest(new ErrorResponse(
        CodigosErrorReporte.FormatoInvalido,
        MensajesDeReporte.FormatoInvalido));

    public static ErrorResponse SinPermiso() => new(
        CodigosErrorReporte.SinPermiso,
        MensajesDeReporte.SinPermiso);

    public static IResult TopeSuperado(int filas, int tope) => Results.Json(
        new ErrorDeTope(
            CodigosErrorReporte.TopeDeFilasSuperado,
            MensajesDeReporte.TopeDeFilasSuperado(filas, tope))
        {
            Filas = filas,
            Tope = tope,
        },
        statusCode: StatusCodes.Status409Conflict);

    public static IResult NoGenerado() => Results.Json(
        new ErrorResponse(
            CodigosErrorReporte.ReporteNoGenerado,
            MensajesDeReporte.ReporteNoGenerado),
        statusCode: StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Entrega el archivo. Las tres cabeceras van acá y no en cada endpoint: quién decide cómo se sirve
    /// el contenido es el backend y no el enlace, así que la misma acción se comporta igual en las
    /// cinco pantallas (convención [003]).
    /// </summary>
    public static IResult Traducir(HttpResponse respuesta, ResultadoReporte resultado)
    {
        if (resultado.FormatoInvalido)
        {
            return FormatoInvalido();
        }

        if (resultado is { Filas: { } filas, Tope: { } tope })
        {
            return TopeSuperado(filas, tope);
        }

        if (resultado is not { Contenido: { } contenido, Formato: { } formato })
        {
            return NoGenerado();
        }

        respuesta.Headers.XContentTypeOptions = "nosniff";

        respuesta.Headers.ContentDisposition =
            $"{FormatosDeReporte.Disposicion(formato)}; filename=\"{resultado.NombreDeArchivo}\"";

        return Results.File(contenido, FormatosDeReporte.TipoDeContenido(formato));
    }
}

/// <summary>
/// El <c>409</c> del tope lleva <c>filas</c> y <c>tope</c> además del mensaje, para que la pantalla
/// pueda usarlos si alguna vez los necesita sin volver a parsear el texto.
///
/// **El mensaje ya viene armado** de todos modos: el frontend no compone esa frase aunque conozca el
/// total del listado, porque escribirla en TypeScript además de en C# son dos textos que se separan
/// (research §4).
/// </summary>
public record ErrorDeTope(string Codigo, string Mensaje) : ErrorResponse(Codigo, Mensaje)
{
    public int Filas { get; init; }

    public int Tope { get; init; }
}

/// <summary>
/// El texto del <c>403</c> de un endpoint, leído por el manejador de acceso denegado del Módulo 1.
///
/// Existe porque el cuerpo del <c>403</c> se arma una sola vez, en el pipeline, con un texto genérico
/// para todo el sistema, y el contrato del Módulo 12 pide uno propio: <i>"No tenés permiso para emitir
/// reportes. Pedíselo a quien administra el sistema."</i>
///
/// <b>No mueve ni una decisión de autorización al endpoint</b>: quien decide sigue siendo la política,
/// y esto es sólo el texto con el que se responde. Un <c>if</c> sobre permisos adentro del handler es
/// lo que la convención [004] prohíbe; declarar un mensaje no lo es.
/// </summary>
public record MensajeDeSinPermiso(ErrorResponse Cuerpo);
