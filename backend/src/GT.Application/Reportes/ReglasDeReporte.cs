namespace GT.Application.Reportes;

/// <summary>
/// Las reglas que gobiernan un reporte. Hoy es una sola: el tope de filas de FR-016.
///
/// **El <c>5000</c> está escrito una sola vez, en el backend.** El frontend no lo conoce y no lo
/// pre-verifica aunque la pantalla sepa el total del listado: el mensaje que lo nombra lo arma el
/// servidor y viaja en el <c>409</c> (research §4). Escribirlo en TypeScript además de en C# serían
/// dos textos que se separan.
///
/// Es un <c>record</c> inyectado y no un <c>const</c> porque un <c>const</c> no se puede bajar para un
/// test, y research §13 pide probar el <c>409</c> <b>sin sembrar 5.001 filas</b>: la aplicación de
/// prueba lo reemplaza por <c>new OpcionesDeReporte(3)</c> y siembra cuatro. Lo que se agrega es poder
/// inyectarlo, no un segundo lugar donde vive el número.
/// </summary>
public sealed record OpcionesDeReporte(int TopeDeFilas)
{
    /// <summary>El valor de FR-016.</summary>
    public const int TopePorDefecto = 5000;

    public static OpcionesDeReporte PorDefecto => new(TopePorDefecto);

    /// <summary>
    /// Se rechaza <b>más de</b> el tope: con filas iguales al tope el reporte se entrega, porque
    /// FR-016 dice "más de" (research §4).
    /// </summary>
    public bool Supera(int filas) => filas > TopeDeFilas;
}
