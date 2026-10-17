using GT.Application.Choferes.Documentacion;
using GT.Domain.Proveedores;

namespace GT.Application.Proveedores;

public class CargarFacturaProveedor(IRepositorioFacturasProveedor repositorio,PrepararPdfFacturaProveedor preparar,
    IAlmacenDeArchivos almacen,IRegistroErroresFacturaProveedor log)
{
    public async Task<DetalleFacturaProveedor> EjecutarAsync(DatosFacturaProveedor datos,ArchivoCargado? archivo,DateOnly hoy,CancellationToken cancelacion=default)
    {
        GT.Domain.Facturacion.ImportesDeFactura importes;
        try { importes=ReglasFacturaProveedor.Validar(datos.Numero,datos.Fecha,datos.Detalle,datos.Neto,datos.Cae,datos.FechaVencimientoCae,hoy); }
        catch(ReglaFacturaProveedorException error) { throw new ProveedorException("datos_invalidos",error.Message,error.Campo); }
        if(datos.ProveedorId<=0) throw new ProveedorException("datos_invalidos","Elegí un proveedor activo.","proveedorId");
        var pdf=await preparar.EjecutarAsync(archivo,cancelacion);
        var factura=new FacturaProveedor { ProveedorId=datos.ProveedorId,LiquidacionId=datos.LiquidacionId,ProveedorRazonSocial="",ProveedorCuit="",
            Numero=datos.Numero!.Trim(),Fecha=datos.Fecha!.Value,Detalle=datos.Detalle!.Trim(),Neto=importes.Neto,Iva=importes.Iva,ImporteTotal=importes.Total,
            Cae=datos.Cae!.Trim(),FechaVencimientoCae=datos.FechaVencimientoCae!.Value,ArchivoRuta=pdf?.Ruta,ArchivoNombre=pdf?.Nombre,ArchivoTipoContenido=pdf?.TipoContenido };
        int id;
        try { id=await repositorio.RegistrarAsync(factura,cancelacion); }
        catch(ConfirmacionFacturaInciertaException incierta)
        {
            if(pdf is null) throw;
            bool existe;
            try { existe=await repositorio.EstaConfirmadaAsync(incierta.FacturaId,pdf.Ruta,CancellationToken.None); }
            catch(Exception error) { log.Error(error,$"No se pudo comprobar el commit de factura {incierta.FacturaId}; se conserva el PDF."); throw incierta; }
            if(!existe) { await CompensarAsync(pdf); throw; }
            id=incierta.FacturaId;
        }
        catch { await CompensarAsync(pdf); throw; }
        // Fuera del bloque de compensación: después de confirmar, un fallo de lectura nunca borra el PDF.
        return await repositorio.ObtenerAsync(id,cancelacion) ?? throw new IOException("No se pudo releer la factura registrada.");
    }
    private async Task CompensarAsync(PdfFacturaPreparado? pdf)
    {
        if(pdf is null) return;
        try { await almacen.BorrarAsync(pdf.Ruta,CancellationToken.None); }
        catch(Exception error) { log.Error(error,"No se pudo compensar el PDF; queda un archivo huérfano sin factura."); }
    }
}
