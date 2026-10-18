using GT.Domain.Facturacion;

namespace GT.Domain.Proveedores;

public class ReglaFacturaProveedorException(string campo,string mensaje) : Exception(mensaje)
{
    public string Campo { get; } = campo;
}

public static class ReglasFacturaProveedor
{
    public const decimal Maximo = 9999999999999999.99m;
    public static ImportesDeFactura Validar(string? numero,DateOnly? fecha,string? detalle,decimal? neto,
        string? cae,DateOnly? vencimiento,DateOnly hoy)
    {
        if(string.IsNullOrWhiteSpace(numero)) Fallo("numero","Escribí el número de factura.");
        if(numero!.Trim().Length>400) Fallo("numero","El número admite hasta 400 caracteres.");
        if(fecha is null || fecha>hoy) Fallo("fecha","La fecha de factura no puede ser posterior a hoy en Argentina.");
        if(string.IsNullOrWhiteSpace(detalle)) Fallo("detalle","Escribí el detalle.");
        if(string.IsNullOrWhiteSpace(cae)) Fallo("cae","Escribí el CAE.");
        if(vencimiento is null || vencimiento<fecha) Fallo("fechaVencimientoCae","El vencimiento del CAE debe ser igual o posterior a la fecha de factura.");
        if(neto is null or <=0 or >Maximo || decimal.Round(neto.Value,2)!=neto.Value)
            Fallo("neto","Ingresá un neto mayor a cero, con hasta dos decimales y dentro de la capacidad de almacenamiento.");
        var iva=CalculadorImportes.IvaSobre(neto!.Value,TipoComprobante.FacturaA);
        var total=neto.Value+iva;
        if(total>Maximo) Fallo("neto","El total calculado excede la capacidad de almacenamiento.");
        return new(neto.Value,iva,total);
    }
    private static void Fallo(string campo,string mensaje)=>throw new ReglaFacturaProveedorException(campo,mensaje);
}
