using System.Text.Json;
using GT.Application.Proveedores;

namespace GT.Api.Proveedores;

public static class RespuestasProveedor
{
    private static readonly JsonSerializerOptions Opciones=CrearOpciones();
    private static JsonSerializerOptions CrearOpciones()
    {
        var opciones=new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new ImporteDecimalJsonConverter()); return opciones;
    }
    public static IResult Json(object datos,int estado=200)=>Results.Json(datos,Opciones,statusCode:estado);
    public static IResult Error(ProveedorException error)=>error is ImportesLiquidacionDistintosException importes
        ? Json(new {codigo=error.Codigo,mensaje=error.Message,campo=error.Campo,importes.ImporteTotalFactura,importes.ImporteTotalLiquidacion},409)
        : Json(new {codigo=error.Codigo,mensaje=error.Message,campo=error.Campo},error.Tipo switch
    { TipoErrorProveedor.NoEncontrado=>404,TipoErrorProveedor.Conflicto=>409,_=>400 });
}
