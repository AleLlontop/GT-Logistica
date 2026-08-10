using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public interface IRepositorioTransportistas
{
    Task<bool> ExisteCuitAsync(string cuitNormalizado, int? idAExcluir, CancellationToken cancelacion);
    Task AgregarAsync(Transportista transportista, CancellationToken cancelacion);
    Task GuardarCambiosAsync(CancellationToken cancelacion);

    /// <summary>
    /// Transportistas que cumplen los filtros, con sus cantidades de <b>dependientes activos</b>
    /// —choferes y vehículos—. El texto busca por nombre o CUIT, parcial y sin distinguir mayúsculas;
    /// el CUIT se compara ya normalizado a sólo dígitos, así que <c>30-71</c> y <c>3071</c>
    /// encuentran lo mismo (FR-025).
    /// </summary>
    Task<List<TransportistaConDependenciasActivas>> ConsultarAsync(
        string? textoBusqueda,
        string? cuitNormalizado,
        bool soloActivos,
        CancellationToken cancelacion);

    /// <summary>
    /// El transportista con sus dos cantidades de dependientes activos. Es lo que decide si la baja
    /// procede y lo que el mensaje de rechazo informa (FR-010, FR-008d).
    /// </summary>
    Task<TransportistaConDependenciasActivas?> ObtenerConDependenciasActivasAsync(
        int id,
        CancellationToken cancelacion);

    Task<Transportista?> ObtenerPorIdAsync(int id, CancellationToken cancelacion);
}
