using GT.Application.Choferes;

namespace GT.Application.Proveedores;

public interface IRepositorioProveedores
{
    Task<PaginaDe<ProveedorResumen>> ConsultarAsync(FiltrosProveedor filtros, CancellationToken cancelacion);
    Task<ProveedorDetalle?> ObtenerAsync(int id, CancellationToken cancelacion);
    Task<PaginaDe<TransportistaExterno>> ExternosAsync(string? q, int pagina, CancellationToken cancelacion);
    Task<ProveedorDetalle> GuardarAsync(int? id, ProveedorPeticion datos, CancellationToken cancelacion);
    Task<ProveedorDetalle> CambiarEstadoAsync(int id, long version, bool activo, CancellationToken cancelacion);
}
