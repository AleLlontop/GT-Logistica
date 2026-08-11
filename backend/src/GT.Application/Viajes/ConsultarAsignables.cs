namespace GT.Application.Viajes;

/// <summary>
/// Los choferes y vehículos que se pueden asignar (FR-021).
///
/// <b>Sin paginar</b>: son dos desplegables sobre padrones de decenas de filas, y paginar un
/// desplegable sería resolver un problema que no existe.
///
/// Las dos listas pueden venir vacías, y es una respuesta legítima: la pantalla informa qué falta
/// cargar y el viaje se queda <c>pendiente</c> sin asignar. Lo que no puede es pasar a <c>en curso</c>
/// (FR-019, FR-025).
/// </summary>
public class ConsultarAsignables(IRepositorioViajes viajes)
{
    public Task<Asignables> EjecutarAsync(CancellationToken cancelacion = default) =>
        viajes.ConsultarAsignablesAsync(cancelacion);
}
