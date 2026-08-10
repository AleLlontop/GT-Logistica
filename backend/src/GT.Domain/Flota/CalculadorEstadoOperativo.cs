namespace GT.Domain.Flota;

/// <summary>
/// El estado operativo que el listado y la ficha <b>muestran</b>, derivado del guardado y del estado
/// de la documentación (FR-014).
///
/// <code>
/// estadoOperativoDerivado =
///     (estadoDocumentacion ∈ { vencida, sinDocumentacion })
///         ? fueraDeServicio
///         : estadoOperativoGuardado
/// </code>
///
/// La columna guardada <b>no se toca nunca</b>: al renovar el documento, la unidad vuelve a estar
/// disponible sola, sin proceso nocturno y sin que nadie edite nada (research §4). Sobrescribirla
/// exigiría un proceso que la mantuviera al día y —peor— uno que la <i>revirtiera</i> al renovar.
///
/// <b>Esto no es lo mismo que FR-014a</b>, y conviene no confundirlos: FR-014a es la validación del
/// formulario, que al guardar rechaza <c>disponible</c> y explica cuál documento lo impide. Ésta es
/// la derivación al consultar, que cubre el paso del tiempo —el seguro que vence de un día para el
/// otro sin que nadie abra la pantalla—. Una sola de las dos deja un agujero.
/// </summary>
public static class CalculadorEstadoOperativo
{
    public static VehiculoEstado Derivar(
        VehiculoEstado estadoGuardado,
        EstadoDocumentacionVehiculo estadoDocumentacion) =>
        ImpideEstarDisponible(estadoDocumentacion)
            ? VehiculoEstado.FueraDeServicio
            : estadoGuardado;

    /// <summary>
    /// Si el estado de la documentación impide que la unidad quede disponible. Es la condición que
    /// FR-014a valida en el formulario y FR-014 aplica al consultar: una sola definición para las
    /// dos, para que no puedan separarse con el tiempo.
    /// </summary>
    public static bool ImpideEstarDisponible(EstadoDocumentacionVehiculo estadoDocumentacion) =>
        estadoDocumentacion is EstadoDocumentacionVehiculo.Vencida
            or EstadoDocumentacionVehiculo.SinDocumentacion;
}
