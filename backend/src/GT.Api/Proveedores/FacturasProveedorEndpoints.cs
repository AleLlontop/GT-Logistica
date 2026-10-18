using System.Globalization;
using GT.Api.Archivos;
using GT.Api.Autorizacion;
using GT.Application.Choferes.Documentacion;
using GT.Application.Proveedores;
using GT.Domain.Choferes;
using GT.Domain.Proveedores;
using GT.Domain.Usuarios;

namespace GT.Api.Proveedores;

public static class FacturasProveedorEndpoints
{
    public static void MapearFacturasProveedor(this IEndpointRouteBuilder rutas)
    {
        rutas.MapPost("/api/proveedores/facturas",CargarAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ProveedoresFacturasCargar)).DisableAntiforgery();
        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.ProveedoresFacturasConsultar);
        rutas.MapGet("/api/proveedores/facturas",async(string? desde,string? hasta,string? proveedorId,string? estado,string? pagina,ConsultarFacturasProveedor consulta,CancellationToken ct)=>
        {
            try { return RespuestasProveedor.Json(await consulta.EjecutarAsync(new(FechaOpcional(desde,"desde"),FechaOpcional(hasta,"hasta"),EnteroOpcional(proveedorId,"proveedorId"),estado,EnteroOpcional(pagina,"pagina")??1),ct)); }
            catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        }).RequireAuthorization(consultar);
        rutas.MapGet("/api/proveedores/facturas/{id:int}",async(int id,ConsultarFacturasProveedor consulta,CancellationToken ct)=>
        {
            try { return RespuestasProveedor.Json(await consulta.ObtenerAsync(id,ct)); }
            catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        }).RequireAuthorization(consultar);
        rutas.MapGet("/api/proveedores/facturas/{id:int}/archivo",async(int id,AbrirPdfFacturaProveedor abrir,HttpContext http,CancellationToken ct)=>
        {
            try { var archivo=await abrir.EjecutarAsync(id,ct); return ResultadoArchivo.EnLinea(http,archivo.Contenido,archivo.TipoContenido,archivo.Nombre); }
            catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        }).RequireAuthorization(consultar);
    }
    private static async Task<IResult> CargarAsync(HttpRequest peticion,CargarFacturaProveedor cargar,TimeProvider reloj,CancellationToken ct)
    {
        try
        {
            if(!peticion.HasFormContentType || peticion.ContentType?.StartsWith("multipart/form-data",StringComparison.OrdinalIgnoreCase)!=true) throw Invalido("neto","Enviá los datos como multipart; los importes deben ser texto decimal exacto.");
            IFormCollection formulario;
            try { formulario=await peticion.ReadFormAsync(ct); }
            catch(InvalidDataException) { throw Invalido("archivo","El PDF debe pesar como máximo 10 MB y el formulario debe ser válido."); }
            catch(BadHttpRequestException error) when(error.StatusCode==413) { throw Invalido("archivo","El PDF debe pesar como máximo 10 MB."); }
            string? Uno(string campo)
            {
                if(formulario[campo].Count>1) throw Invalido(campo,"El campo no puede enviarse más de una vez.");
                return formulario[campo].FirstOrDefault();
            }
            int Entero(string campo)=>int.TryParse(Uno(campo),NumberStyles.None,CultureInfo.InvariantCulture,out var id)?id:throw Invalido(campo,"Elegí un valor válido.");
            DateOnly Fecha(string campo)=>DateOnly.TryParseExact(Uno(campo),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var fecha)?fecha:throw Invalido(campo,"Ingresá una fecha válida.");
            if(!ImporteDecimalJsonConverter.IntentarLeer(Uno("neto"),out var neto)) throw Invalido("neto","Ingresá el neto como decimal exacto con dos decimales.");
            var datos=new DatosFacturaProveedor(Entero("proveedorId"),Uno("numero"),Fecha("fecha"),Uno("detalle"),neto,Uno("cae"),Fecha("fechaVencimientoCae"),
                formulario.ContainsKey("liquidacionId")?Entero("liquidacionId"):null);
            var hoy=FechaHoyArgentina.Desde(reloj.GetUtcNow());
            var importes=ReglasFacturaProveedor.Validar(datos.Numero,datos.Fecha,datos.Detalle,neto,datos.Cae,datos.FechaVencimientoCae,hoy);
            foreach(var campo in new[]{"iva","importeTotal"})
                if(formulario.ContainsKey(campo) && (!ImporteDecimalJsonConverter.IntentarLeer(Uno(campo),out var importe) || importe!=(campo=="iva"?importes.Iva:importes.Total)))
                    throw Invalido(campo,"El importe calculado no coincide con el neto.");
            var admitidos=new[]{"proveedorId","numero","fecha","detalle","neto","cae","fechaVencimientoCae","liquidacionId","iva","importeTotal"};
            if(formulario.Keys.Any(k=>!admitidos.Contains(k,StringComparer.Ordinal))) throw Invalido("factura","La petición contiene campos no editables.");
            if(formulario.Files.Count>1 || formulario.Files.Any(f=>f.Name!="archivo")) throw Invalido("archivo","Adjuntá un único PDF.");
            var archivo=formulario.Files["archivo"];
            var detalle=await cargar.EjecutarAsync(datos,archivo is null?null:new ArchivoCargado(archivo.FileName,archivo.Length,archivo.OpenReadStream),hoy,ct);
            peticion.HttpContext.Response.Headers.Location=$"/api/proveedores/facturas/{detalle.Id}";
            return RespuestasProveedor.Json(detalle,201);
        }
        catch(ReglaFacturaProveedorException error) { return RespuestasProveedor.Error(Invalido(error.Campo,error.Message)); }
        catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        catch(ArchivoNoGuardadoException) { return RespuestasProveedor.Json(new{codigo="archivo_no_guardado",mensaje="No pudimos guardar el PDF. Conservá los datos y volvé a intentar.",campo="archivo"},500); }
    }
    private static ProveedorException Invalido(string campo,string mensaje)=>new("datos_invalidos",mensaje,campo);
    private static DateOnly? FechaOpcional(string? valor,string campo) => string.IsNullOrWhiteSpace(valor) ? null :
        DateOnly.TryParseExact(valor,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var fecha) ? fecha :
        throw Invalido(campo,"Ingresá una fecha válida.");
    private static int? EnteroOpcional(string? valor,string campo) => string.IsNullOrWhiteSpace(valor) ? null :
        int.TryParse(valor,NumberStyles.None,CultureInfo.InvariantCulture,out var entero) ? entero :
        throw Invalido(campo,"Ingresá un identificador o una página válidos.");
}
