using System.Globalization;

namespace GT.Application.Reportes;

/// <summary>
/// Los cuatro códigos de error del módulo, exactamente los de <c>contracts/README.md</c>.
///
/// <b>La regla de códigos HTTP</b> (convención [005]): <c>400</c> cuando el problema está en lo que se
/// pidió —el formato— y <c>409</c> cuando está en el estado de los datos —cuántas filas hay ahora—.
/// Los filtros que se tipearon son válidos: el listado los acepta y muestra sus resultados. Lo que
/// rechaza la generación es un número que cambia sin que nadie toque el filtro (research §4).
/// </summary>
public static class CodigosErrorReporte
{
    public const string FormatoInvalido = "formato_invalido";
    public const string SinPermiso = "sin_permiso";

    /// <summary>409: es estado, no tipeo.</summary>
    public const string TopeDeFilasSuperado = "tope_de_filas_superado";

    /// <summary>500: falló el motor de PDF o de planilla (FR-017).</summary>
    public const string ReporteNoGenerado = "reporte_no_generado";
}

/// <summary>
/// Textos que se muestran tal cual, en español rioplatense (Principio II, contracts §Cuerpos de error).
///
/// **Los cuatro los escribe el backend y la pantalla los muestra tal como llegan.** El frontend no
/// compone ninguna de estas frases: escritas en C# y en TypeScript serían dos textos que se separan
/// (research §4, convención [009]).
/// </summary>
public static class MensajesDeReporte
{
    public const string FormatoInvalido = "El formato del reporte tiene que ser PDF o Excel.";

    public const string SinPermiso =
        "No tenés permiso para emitir reportes. Pedíselo a quien administra el sistema.";

    public const string ReporteNoGenerado =
        "No pudimos generar el reporte. Volvé a intentar en unos minutos.";

    /// <summary>
    /// El del tope se arma con las filas y el tope <b>como números con separador de miles</b>:
    /// <c>El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a
    /// intentar.</c>
    /// </summary>
    public static string TopeDeFilasSuperado(int filas, int tope) =>
        $"El filtro dejó {EnMiles(filas)} filas y el tope de un reporte es {EnMiles(tope)}. " +
        "Acotá los filtros y volvé a intentar.";

    /// <summary>Punto como separador de miles, que es el de es-AR y no el de la cultura del servidor.</summary>
    private static string EnMiles(int numero) =>
        numero.ToString("N0", CultureInfo.GetCultureInfo("es-AR"));
}
