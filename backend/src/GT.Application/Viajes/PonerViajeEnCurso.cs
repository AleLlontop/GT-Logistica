using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Pasa el viaje de <c>pendiente</c> a <c>en curso</c> (FR-025, FR-026, FR-033).
///
/// Es la <b>única transición que llega a <c>en curso</c></b>, y el único estado que ocupa al chofer y
/// al vehículo.
///
/// <b>Lo que exige</b>: las dos unidades asignadas y <b>activas en sus padrones</b> (FR-025), y
/// ninguna de las dos en otro viaje <c>en curso</c> (FR-026).
///
/// <b>Lo que deliberadamente no revisa</b>: la documentación y el estado operativo del vehículo. Se
/// controlaron al asignar, y volver a mirarlos acá dejaría en tierra un viaje planificado con la
/// unidad en regla el día que se lo asignó (FR-024, US4 esc. 11 y 15).
/// </summary>
public class PonerViajeEnCurso(IRepositorioViajes viajes, TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        int id,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var viaje = await viajes.ObtenerParaModificarAsync(id, cancelacion);

        if (viaje is null)
        {
            return new ResultadoViaje(ErrorViaje.NoEncontrado);
        }

        if (EstadoTerminal.Rechazo(viaje) is { } terminal)
        {
            return terminal;
        }

        if (!TransicionesDeViaje.EstaPermitida(viaje.Estado, EstadoViaje.EnCurso))
        {
            return new ResultadoViaje(
                ErrorViaje.TransicionNoPermitida,
                NumeroDelViaje: viaje.Numero,
                EstadoActual: NombresDeEstadoViaje.EnTexto(viaje.Estado),
                EstadoPedido: NombresDeEstadoViaje.EnTexto(EstadoViaje.EnCurso));
        }

        // FR-019b garantiza que estén los dos o ninguno, así que preguntar por uno alcanzaría; se
        // preguntan igual los dos porque la garantía vale para lo que escribe este módulo y no para
        // lo que alguien pueda dejar en la base.
        if (viaje.Chofer is not { } chofer || viaje.Vehiculo is not { } vehiculo)
        {
            return new ResultadoViaje(ErrorViaje.FaltaAsignacion, NumeroDelViaje: viaje.Numero);
        }

        var nombreDelChofer = chofer.Persona?.NombreCompleto ?? $"Chofer {chofer.Id}";

        // FR-025: **activas**, no sólo asignadas. Si alguna se dio de baja después de asignarla, el
        // viaje no arranca hasta que se lo reasigne (US4 esc. 14).
        if (!chofer.Activo)
        {
            return new ResultadoViaje(
                ErrorViaje.UnidadDadaDeBaja,
                NumeroDelViaje: viaje.Numero,
                Unidad: nombreDelChofer);
        }

        if (!vehiculo.Activo)
        {
            return new ResultadoViaje(
                ErrorViaje.UnidadDadaDeBaja,
                NumeroDelViaje: viaje.Numero,
                Unidad: vehiculo.Patente);
        }

        // FR-026. La consulta previa da el mensaje bueno —nombra el viaje que ocupa a la unidad—; el
        // índice único filtrado de la base cierra la carrera entre dos operadores simultáneos.
        if (await OcupacionAsync(viaje, nombreDelChofer, vehiculo.Patente, cancelacion) is { } ocupada)
        {
            return ocupada;
        }

        try
        {
            await viajes.RegistrarCambioDeEstadoAsync(
                viaje,
                EstadoViaje.EnCurso,
                usuarioId,
                reloj.GetUtcNow().UtcDateTime,
                cancelacion);
        }
        catch (UnidadOcupadaException excepcion)
        {
            // Dos operadores en el mismo milisegundo: el primero ganó y este perdió. Se vuelve a
            // consultar para poder nombrar el viaje que quedó ocupando la unidad (SC-005).
            return await OcupacionAsync(viaje, nombreDelChofer, vehiculo.Patente, cancelacion)
                ?? new ResultadoViaje(
                    excepcion.EsDelChofer ? ErrorViaje.ChoferOcupado : ErrorViaje.VehiculoOcupado,
                    NumeroDelViaje: viaje.Numero,
                    Unidad: excepcion.EsDelChofer ? nombreDelChofer : vehiculo.Patente);
        }

        var ficha = await viajes.ObtenerFichaAsync(id, cancelacion);

        return new ResultadoViaje(
            ErrorViaje.Ninguno,
            ViajeDetalle.Desde(ficha!, MomentoDeLectura.Desde(reloj)),
            NumeroDelViaje: viaje.Numero);
    }

    private async Task<ResultadoViaje?> OcupacionAsync(
        Viaje viaje,
        string nombreDelChofer,
        string patente,
        CancellationToken cancelacion)
    {
        if (await viajes.ViajeEnCursoDelChoferAsync(viaje.ChoferId!.Value, viaje.Id, cancelacion)
            is { } conChofer)
        {
            return new ResultadoViaje(
                ErrorViaje.ChoferOcupado,
                NumeroDelViaje: viaje.Numero,
                NumeroDeViajeRelacionado: conChofer.Numero,
                Unidad: nombreDelChofer);
        }

        if (await viajes.ViajeEnCursoDelVehiculoAsync(viaje.VehiculoId!.Value, viaje.Id, cancelacion)
            is { } conVehiculo)
        {
            return new ResultadoViaje(
                ErrorViaje.VehiculoOcupado,
                NumeroDelViaje: viaje.Numero,
                NumeroDeViajeRelacionado: conVehiculo.Numero,
                Unidad: patente);
        }

        return null;
    }
}
