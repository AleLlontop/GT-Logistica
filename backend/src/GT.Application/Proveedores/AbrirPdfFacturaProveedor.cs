using GT.Application.Choferes.Documentacion;

namespace GT.Application.Proveedores;

public record PdfFacturaAbierto(Stream Contenido, string Nombre, string TipoContenido);

public class AbrirPdfFacturaProveedor(IRepositorioFacturasProveedor repositorio, IAlmacenDeArchivos almacen)
{
    public async Task<PdfFacturaAbierto> EjecutarAsync(int id, CancellationToken cancelacion)
    {
        var archivo = await repositorio.ObtenerPdfAsync(id, cancelacion) ?? throw SinArchivo();
        var contenido = await almacen.AbrirAsync(archivo.Ruta, cancelacion) ?? throw SinArchivo();
        return new(contenido, archivo.Nombre, archivo.TipoContenido);
    }
    private static ProveedorException SinArchivo() => new("no_encontrado", "No encontramos un PDF adjunto para esa factura.", tipo: TipoErrorProveedor.NoEncontrado);
}
