using GT.Application.Choferes.Documentacion;

namespace GT.Application.Facturacion;

/// <summary>
/// El documento de la factura, para abrirlo (FR-031a, FR-031d).
///
/// <b>Siempre por endpoint autorizado y nunca por una URL pública</b>: el comprobante de un cliente es
/// un dato de la empresa, así que conocer la ruta del archivo no puede alcanzar para verlo. Exige
/// <c>facturacion.consultar</c>, que es el mismo permiso con el que se mira la ficha (Principio V).
///
/// <b>Disponible en cualquier estado.</b> Si la factura está anulada, el documento ya trae impresas la
/// leyenda y el motivo, porque se regeneró al anularla: acá no se estampa nada. El documento se arma en
/// un solo lugar, y ese lugar es el armador (FR-031d).
/// </summary>
public class ServirDocumentoFactura(IRepositorioFacturas facturas, IAlmacenDeArchivos almacen)
{
    public record ArchivoDeFactura(Stream Contenido, string TipoContenido, string Nombre);

    /// <summary>
    /// El archivo, o <c>null</c> si la factura no existe o su documento ya no está en el volumen. Las
    /// dos situaciones se comunican igual: <c>no_encontrado</c>.
    /// </summary>
    public async Task<ArchivoDeFactura?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var factura = await facturas.ObtenerFichaAsync(id, cancelacion);

        if (factura is null)
        {
            return null;
        }

        var contenido = await almacen.AbrirAsync(factura.DocumentoRuta, cancelacion);

        if (contenido is null)
        {
            return null;
        }

        // Un nombre que identifica la factura, no el generado por el sistema: quien hace "Guardar como"
        // tiene que poder distinguir dos comprobantes en su carpeta de descargas (FR-031a).
        return new ArchivoDeFactura(
            contenido,
            "application/pdf",
            $"Factura {factura.NumeroComprobante}.pdf");
    }
}
