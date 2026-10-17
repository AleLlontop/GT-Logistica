using GT.Domain.Proveedores;

namespace GT.Application.Proveedores;

public interface IRepositorioFacturasProveedor
{
    Task<GT.Application.Choferes.PaginaDe<FacturaProveedorResumen>> ConsultarAsync(FiltrosFacturaProveedor filtros, CancellationToken cancelacion);
    Task<IReadOnlyList<LiquidacionDisponibleProveedor>> ConsultarLiquidacionesDisponiblesAsync(int proveedorId, CancellationToken cancelacion);
    Task<int> RegistrarAsync(FacturaProveedor factura,CancellationToken cancelacion);
    Task<DetalleFacturaProveedor?> ObtenerAsync(int id,CancellationToken cancelacion);
    Task<bool> EstaConfirmadaAsync(int id,string ruta,CancellationToken cancelacion);
    Task<PdfFacturaPreparado?> ObtenerPdfAsync(int id,CancellationToken cancelacion);
}

public interface IRegistroErroresFacturaProveedor
{
    void Error(Exception error,string mensaje);
}
