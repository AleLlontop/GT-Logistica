namespace GT.Application.Proveedores;

public class GestionarProveedores(IRepositorioProveedores repositorio)
{
    public Task<ProveedorDetalle> GuardarAsync(int? id, ProveedorPeticion datos, CancellationToken cancelacion = default) =>
        repositorio.GuardarAsync(id, ReglasProveedor.Normalizar(datos, id.HasValue), cancelacion);
    public Task<ProveedorDetalle> CambiarEstadoAsync(int id, long version, bool activo, CancellationToken cancelacion = default)
    {
        if (version <= 0) throw new ProveedorException("datos_invalidos", "Volvé a abrir el proveedor antes de cambiar su estado.", "version");
        return repositorio.CambiarEstadoAsync(id, version, activo, cancelacion);
    }
}
