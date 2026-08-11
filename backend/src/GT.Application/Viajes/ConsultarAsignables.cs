using GT.Domain.Viajes;

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
///
/// <b>El filtro es el estado operativo guardado; la documentación no filtra, observa.</b> Una unidad
/// con un documento vencido a la fecha del viaje se sigue ofreciendo —sacarla rompería la carga
/// retroactiva (SC-014)— pero se ofrece <b>con el motivo escrito al lado</b>, calculado con el mismo
/// <see cref="EvaluadorHabilitacion"/> y contra la misma fecha que va a usar la asignación. Así la
/// lista no puede decir algo distinto de lo que después responde el servidor, y quien opera entiende
/// por qué el Módulo 4 muestra esa unidad fuera de servicio mientras acá aparece.
/// </summary>
public class ConsultarAsignables(IRepositorioViajes viajes, TimeProvider reloj)
{
    /// <param name="fechaDelViaje">
    /// La fecha contra la que se evalúa la documentación. Sin ella se toma hoy, que es lo correcto
    /// para quien todavía no eligió un viaje —pero la pantalla de asignación siempre la manda, porque
    /// un viaje retroactivo se asigna con la documentación que estaba vigente ese día.
    /// </param>
    public async Task<Asignables> EjecutarAsync(
        DateOnly? fechaDelViaje = null,
        CancellationToken cancelacion = default)
    {
        var fecha = fechaDelViaje ?? MomentoDeLectura.Desde(reloj).Hoy;

        var choferes = await viajes.ConsultarChoferesAsignablesAsync(cancelacion);
        var vehiculos = await viajes.ConsultarVehiculosAsignablesAsync(cancelacion);

        return new Asignables(
            [.. choferes.Select(chofer => new Asignable(
                chofer.Id,
                chofer.Persona is { } persona ? $"{persona.Apellido}, {persona.Nombre}" : $"Chofer {chofer.Id}",
                Observacion(EvaluadorHabilitacion.ParaChofer(chofer.Documentacion, fecha))))],
            [.. vehiculos.Select(vehiculo => new Asignable(
                vehiculo.Id,
                vehiculo.Patente,
                Observacion(EvaluadorHabilitacion.ParaVehiculo(vehiculo.Documentacion, fecha))))]);
    }

    /// <summary>
    /// Sólo el bloqueo se escribe acá. Lo próximo a vencer no: eso es una advertencia que llega
    /// <b>con</b> la asignación ya guardada (FR-023, FR-015a), y adelantarla en el desplegable la
    /// convertiría en un reparo antes de elegir, que es justo lo que FR-015a decidió no hacer.
    /// </summary>
    private static string? Observacion(VeredictoHabilitacion veredicto) =>
        veredicto.Bloquea
            ? MensajesViajes.ObservacionDocumentoVencido(
                veredicto.DocumentoQueDecide!.Tipo,
                FormatoDeFecha.Corta(veredicto.DocumentoQueDecide.FechaVencimiento))
            : null;
}
