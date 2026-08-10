using GT.Application.Choferes;
using GT.Domain.Choferes;

namespace GT.Application.Flota;

/// <summary>
/// Listado paginado de la flota con el estado de cada unidad (FR-030 a FR-032).
///
/// Es la pantalla que responde, antes de asignar un viaje, qué camión está en condiciones de salir a
/// la ruta.
///
/// Ni el estado de la documentación, ni el documento vigente de cada tipo, ni el estado operativo
/// derivado están guardados: los tres se resuelven <b>dentro de la consulta SQL</b> (research §4 y
/// §5). Es lo que permite filtrar por estado sin traer toda la flota a memoria, que era el riesgo de
/// haber elegido calcularlos al leer.
/// </summary>
public class ConsultarFlota(IRepositorioVehiculos repositorio)
{
    public Task<PaginaDe<VehiculoListado>> EjecutarAsync(
        FiltrosDeFlota filtros,
        CancellationToken cancelacion = default)
    {
        // Una página fuera de rango no es un error: devuelve items vacío con el total real.
        var pagina = filtros.Pagina < 1 ? 1 : filtros.Pagina;

        return repositorio.ConsultarAsync(
            filtros with { Pagina = pagina },
            FechaHoyArgentina.Hoy(),
            cancelacion);
    }
}

/// <summary>
/// Ficha de una unidad con toda su documentación, vigente e histórica (FR-038).
///
/// Devuelve el estado operativo <b>dos veces</b> —el derivado para mostrar y el guardado para poblar
/// el formulario de edición—, y eso no es redundancia: con uno solo, editar una unidad parada por
/// papeles vencidos le pisaría en silencio el motivo real a quien opera (plan §Reevaluación
/// post-diseño).
/// </summary>
public class ConsultarFichaVehiculo(IRepositorioVehiculos repositorio)
{
    public async Task<VehiculoDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var vehiculo = await repositorio.ObtenerPorIdConRelacionesAsync(id, cancelacion);

        return vehiculo is null ? null : VehiculoDetalle.Desde(vehiculo, FechaHoyArgentina.Hoy());
    }
}
