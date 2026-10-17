using GT.Application.Choferes.Documentacion;

namespace GT.Application.Proveedores;

public record PdfFacturaPreparado(string Ruta,string Nombre,string TipoContenido);
public class PrepararPdfFacturaProveedor(IAlmacenDeArchivos almacen,IValidadorDeArchivo validador,IRegistroErroresFacturaProveedor log)
{
    public const long MaximoBytes=10*1024*1024;
    public async Task<PdfFacturaPreparado?> EjecutarAsync(ArchivoCargado? archivo,CancellationToken cancelacion=default)
    {
        if(archivo is null) return null;
        if(archivo.TamanioEnBytes<=0 || archivo.TamanioEnBytes>MaximoBytes)
            throw Invalido("El PDF debe tener contenido y pesar como máximo 10 MB.");
        var nombre=Path.GetFileName(archivo.Nombre);
        if(string.IsNullOrWhiteSpace(nombre) || nombre.Length>255) throw Invalido("El nombre del PDF admite hasta 255 caracteres.");
        await using var contenido=archivo.Abrir();
        var validacion=await validador.ValidarAsync(contenido,archivo.TamanioEnBytes,cancelacion);
        if(!validacion.EsValido || validacion.TipoContenido!="application/pdf") throw Invalido("Adjuntá un archivo PDF válido.");
        // Abrir de nuevo permite escribir completo incluso si el flujo validado no admite Seek.
        await using var paraGuardar=archivo.Abrir();
        var ruta=await almacen.GuardarAsync(paraGuardar,cancelacion);
        if(string.IsNullOrWhiteSpace(ruta) || ruta.Length>400 || validacion.TipoContenido.Length>100)
        {
            try { await almacen.BorrarAsync(ruta,CancellationToken.None); }
            catch(Exception error) { log.Error(error,"No se pudo compensar un PDF con metadata fuera de capacidad."); }
            throw Invalido("La metadata del PDF excede la capacidad de almacenamiento.");
        }
        return new(ruta,nombre,validacion.TipoContenido);
    }
    private static ProveedorException Invalido(string mensaje)=>new("archivo_no_admitido",mensaje,"archivo");
}
