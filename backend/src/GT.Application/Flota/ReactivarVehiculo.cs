using GT.Application.Choferes.Transportistas;
using GT.Application.Flota.TiposVehiculo;

namespace GT.Application.Flota;

/// <summary>
/// Reactivación de una unidad dada de baja (FR-008e).
///
/// Vuelve a ponerla activa. Desde ese momento aparece en el listado por defecto y, si su
/// documentación lo amerita, vuelve a alertar en el panel sin que nadie recargue nada: el estado se
/// calcula al consultarlo (research §10).
///
/// <b>El cuerpo es opcional</b> y sólo hace falta si el transportista o el tipo de la unidad fueron
/// dados de baja mientras estuvo afuera. Si alguno está inactivo y no vino un reemplazo activo, se
/// rechaza indicando cuál falta: la reactivación tiene que dejar la unidad en un estado que el alta
/// también aceptaría, y un vehículo activo apuntando a un transportista inactivo es justo lo que
/// FR-008a prohíbe (research §11).
/// </summary>
public class ReactivarVehiculo(
    IRepositorioVehiculos vehiculos,
    IRepositorioTiposVehiculo tipos,
    IRepositorioTransportistas transportistas)
{
    public async Task<ResultadoVehiculo> EjecutarAsync(
        int id,
        ReactivacionRequest? peticion = null,
        CancellationToken cancelacion = default)
    {
        var vehiculo = await vehiculos.ObtenerParaModificarAsync(id, cancelacion);
        if (vehiculo is null)
        {
            return new ResultadoVehiculo(ErrorVehiculo.NoEncontrado);
        }

        // El reemplazo, si vino; si no, el que la unidad ya tenía.
        var transportistaId = peticion?.TransportistaId ?? vehiculo.TransportistaId;
        var transportista = await transportistas.ObtenerPorIdAsync(transportistaId, cancelacion);

        if (transportista is null || !transportista.Activo)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.TransportistaInactivoAlReactivar,
                Campo: "transportistaId");
        }

        var tipoId = peticion?.TipoVehiculoId ?? vehiculo.TipoVehiculoId;
        var tipo = await tipos.ObtenerPorIdAsync(tipoId, cancelacion);

        if (tipo is null || !tipo.Activo)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.TipoInactivoAlReactivar,
                Campo: "tipoVehiculoId");
        }

        vehiculo.TransportistaId = transportista.Id;
        vehiculo.TipoVehiculoId = tipo.Id;
        vehiculo.Activo = true;

        await vehiculos.GuardarCambiosAsync(cancelacion);

        return new ResultadoVehiculo(ErrorVehiculo.Ninguno);
    }
}
