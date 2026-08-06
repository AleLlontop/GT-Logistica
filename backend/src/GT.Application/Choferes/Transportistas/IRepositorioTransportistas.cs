using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public interface IRepositorioTransportistas
{
    Task<bool> ExisteCuitAsync(string cuitNormalizado, int? idAExcluir, CancellationToken cancelacion);
    Task AgregarAsync(Transportista transportista, CancellationToken cancelacion);
    Task GuardarCambiosAsync(CancellationToken cancelacion);

    /// <summary>
    /// Transportistas que cumplen los filtros, con su cantidad de choferes activos. El texto busca
    /// por nombre o CUIT, parcial y sin distinguir mayúsculas; el CUIT se compara ya normalizado a
    /// sólo dígitos, así que <c>30-71</c> y <c>3071</c> encuentran lo mismo (FR-025).
    /// </summary>
    Task<List<TransportistaConChoferesActivos>> ConsultarAsync(
        string? textoBusqueda,
        string? cuitNormalizado,
        bool soloActivos,
        CancellationToken cancelacion);

    Task<TransportistaConChoferesActivos?> ObtenerConChoferesActivosAsync(int id, CancellationToken cancelacion);

    Task<Transportista?> ObtenerPorIdAsync(int id, CancellationToken cancelacion);
}
