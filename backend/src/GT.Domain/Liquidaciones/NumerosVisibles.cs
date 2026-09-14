namespace GT.Domain.Liquidaciones;

/// <summary>
/// El formato visible de los dos números del módulo, <b>en un solo lugar</b> (research §4).
///
/// Viajan ya armados en el JSON porque también los usan los mensajes de rechazo —"ya está en la
/// liquidación LQ-12"—: escribirlos en C# y en TypeScript serían dos formatos que se pueden separar.
///
/// Llevan prefijo, a diferencia del <c>#13</c> del viaje, porque el detalle muestra tres clases de número
/// a la vez y tres <c>#</c> en la misma pantalla no se distinguen.
/// </summary>
public static class NumerosVisibles
{
    public static string Liquidacion(int numero) => $"LQ-{numero}";

    public static string OrdenDePago(int numero) => $"OP-{numero}";
}
