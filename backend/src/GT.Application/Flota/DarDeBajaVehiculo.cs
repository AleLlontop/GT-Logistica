namespace GT.Application.Flota;

/// <summary>
/// Baja lógica de una unidad (FR-001, FR-008).
///
/// No borra nada y <b>no toca su documentación</b>: los documentos y sus archivos se conservan
/// intactos y se siguen viendo en la ficha (FR-008, FR-028). Es lo que permite reactivarla más
/// adelante con todo su historial (FR-008e).
///
/// A partir de la baja deja de aparecer en el listado sin filtros y en el panel de vencimientos. Eso
/// no se hace acá: sale solo de que las dos consultas filtran por vehículo activo (FR-031, FR-035).
/// </summary>
public class DarDeBajaVehiculo(IRepositorioVehiculos vehiculos)
{
    public async Task<ResultadoVehiculo> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var vehiculo = await vehiculos.ObtenerParaModificarAsync(id, cancelacion);
        if (vehiculo is null)
        {
            return new ResultadoVehiculo(ErrorVehiculo.NoEncontrado);
        }

        // Dar de baja una unidad que ya está de baja no es un error: el resultado buscado ya se
        // cumple, y fallar sólo complicaría a quien tocó dos veces el botón.
        vehiculo.Activo = false;
        await vehiculos.GuardarCambiosAsync(cancelacion);

        return new ResultadoVehiculo(ErrorVehiculo.Ninguno);
    }
}
