namespace GT.Domain.Choferes;

/// <summary>
/// Cálculo del estado de un documento (FR-017).
///
/// El estado no se guarda en ningún lado: se deriva de la fecha de vencimiento, de los días de aviso
/// de su tipo y del día en curso (research §2). Por eso un documento pasa solo de vigente a próximo
/// a vencer y luego a vencido, sin que nadie ejecute nada (FR-019), y por eso cambiar los días de
/// aviso de un tipo recalcula sus documentos sin actualizar ninguna fila.
///
/// Los dos bordes que la spec fija explícitamente:
/// <list type="bullet">
///   <item>Vence <b>exactamente hoy</b> → próximo a vencer, no vencido. Pasa a vencido mañana.</item>
///   <item>Días de aviso en <b>cero</b> → no hay ventana intermedia: es vigente hasta el día del
///   vencimiento inclusive, y vencido al día siguiente.</item>
/// </list>
///
/// Esta misma regla se traduce a SQL para poder filtrar por estado sin traer las filas a memoria.
/// Si cambia acá, hay que cambiarla también en la consulta del listado.
/// </summary>
public static class CalculadorEstadoDocumento
{
    /// <param name="fechaVencimiento">Fecha hasta la que el documento vale.</param>
    /// <param name="diasAvisoVencimiento">Días de anticipación del tipo. Mayor o igual a cero.</param>
    /// <param name="hoy">Día en curso en Argentina (<see cref="FechaHoyArgentina"/>).</param>
    public static DocumentacionEstado Calcular(
        DateOnly fechaVencimiento,
        int diasAvisoVencimiento,
        DateOnly hoy)
    {
        if (fechaVencimiento < hoy)
        {
            return DocumentacionEstado.Vencida;
        }

        var finDeLaVentanaDeAviso = hoy.AddDays(diasAvisoVencimiento);

        // "Entre hoy inclusive y la ventana de aviso". Con la ventana en cero, esto sólo alcanza al
        // documento que vence hoy mismo, que es justo lo que describe el caso límite de la spec.
        return fechaVencimiento <= finDeLaVentanaDeAviso
            ? DocumentacionEstado.ProximaAvencer
            : DocumentacionEstado.Vigente;
    }

    /// <summary>
    /// Días que faltan para el vencimiento, negativo si ya pasó. Es lo que el panel muestra como
    /// "vence en" o "venció hace" (FR-021).
    /// </summary>
    public static int DiasHastaVencimiento(DateOnly fechaVencimiento, DateOnly hoy) =>
        fechaVencimiento.DayNumber - hoy.DayNumber;
}
