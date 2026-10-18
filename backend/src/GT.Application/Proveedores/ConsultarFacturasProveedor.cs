using GT.Application.Choferes;

namespace GT.Application.Proveedores;

public record FiltrosFacturaProveedor(DateOnly? Desde = null, DateOnly? Hasta = null, int? ProveedorId = null,
    string? Estado = null, int Pagina = 1);
public record FacturaProveedorResumen(int Id, string Numero, DateOnly Fecha, int ProveedorId,
    string ProveedorRazonSocial, string ProveedorCuit, decimal ImporteTotal, string Estado);

public class ConsultarFacturasProveedor(IRepositorioFacturasProveedor repositorio)
{
    public Task<PaginaDe<FacturaProveedorResumen>> EjecutarAsync(FiltrosFacturaProveedor filtros, CancellationToken cancelacion)
    {
        Validar(filtros);
        return repositorio.ConsultarAsync(filtros, cancelacion);
    }

    public async Task<DetalleFacturaProveedor> ObtenerAsync(int id, CancellationToken cancelacion) =>
        await repositorio.ObtenerAsync(id, cancelacion) ?? throw NoEncontrada();

    public static void Validar(FiltrosFacturaProveedor filtros)
    {
        if (filtros.Desde > filtros.Hasta) throw Invalido("desde", "La fecha desde no puede ser posterior a la fecha hasta.");
        if (filtros.Pagina < 1) throw Invalido("pagina", "La página debe ser mayor a cero.");
        if (filtros.ProveedorId is <= 0) throw Invalido("proveedorId", "Elegí un proveedor válido.");
        if (!string.IsNullOrWhiteSpace(filtros.Estado) && filtros.Estado != "pendiente")
            throw Invalido("estado", "Elegí Pendiente o sin restricción de estado.");
    }
    private static ProveedorException Invalido(string campo, string mensaje) => new("datos_invalidos", mensaje, campo);
    public static ProveedorException NoEncontrada() => new("no_encontrado", "No encontramos esa factura de proveedor.", tipo: TipoErrorProveedor.NoEncontrado);
}
