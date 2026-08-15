using GT.Api.Archivos;
using GT.Api.Autorizacion;
using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Usuarios;

namespace GT.Api.Facturacion;

/// <summary>
/// Configuración de la empresa emisora y su logo: cinco endpoints (FR-001 a FR-004).
///
/// <b>No hay <c>POST</c> ni <c>DELETE</c> de la configuración</b>, y no es una omisión: es única para
/// todo el sistema, así que se edita y nunca se crea una segunda ni se borra. El <c>PUT</c> crea la
/// fila la primera vez y la actualiza siempre después (research §12).
///
/// <b>El <c>GET</c> exige <c>facturacion.consultar</c> y las escrituras <c>facturacion.gestionar</c>.</b>
/// La pantalla, en cambio, está atada a <c>gestionar</c> en el menú: no es de lectura para nadie. Pero
/// el <c>GET</c> lo consume además la ficha de una factura, que sí es de lectura, así que exigir
/// <c>gestionar</c> acá dejaría sin datos a quien sólo consulta (contracts/facturacion-api.yaml).
///
/// El logo va como <c>multipart/form-data</c> porque trae el archivo, y se sirve <b>en línea</b> con
/// <c>ResultadoArchivo.EnLinea</c>, igual que los adjuntos de los Módulos 3 y 4 (convención [003]).
/// </summary>
public static class EmpresaEmisoraEndpoints
{
    public static void MapearEmpresaEmisora(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/facturacion/empresa-emisora");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.FacturacionConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.FacturacionGestionar);

        grupo.MapGet("/", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPut("/", GuardarAsync).RequireAuthorization(gestionar);

        grupo.MapGet("/logo", VerLogoAsync).RequireAuthorization(consultar);
        grupo.MapPut("/logo", SubirLogoAsync).RequireAuthorization(gestionar).DisableAntiforgery();
        grupo.MapDelete("/logo", QuitarLogoAsync).RequireAuthorization(gestionar);
    }

    /// <summary>
    /// Responde <c>200</c> también cuando la empresa nunca se configuró, con <c>configurada: false</c>
    /// y los obligatorios faltantes. La pantalla muestra el formulario vacío con el mensaje explícito,
    /// nunca una pantalla en blanco (US1 esc. 1).
    /// </summary>
    private static async Task<IResult> ObtenerAsync(
        ConsultarEmpresaEmisora consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    private static async Task<IResult> GuardarAsync(
        EmpresaEmisoraRequest peticion,
        GuardarEmpresaEmisora guardar,
        CancellationToken cancelacion)
    {
        var resultado = await guardar.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Empresa)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }

    private static async Task<IResult> VerLogoAsync(
        GestionarLogo logo,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        var archivo = await logo.ServirAsync(cancelacion);

        // En línea y no como descarga: quien abre el logo lo quiere ver. Es seguro porque el tipo salió
        // de la firma del archivo y se limita a JPG y PNG — nada que el navegador ejecute como página.
        return archivo is null
            ? RespuestasDeFactura.NoEncontrada()
            : ResultadoArchivo.EnLinea(contexto, archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
    }

    private static async Task<IResult> SubirLogoAsync(
        HttpRequest peticion,
        GestionarLogo logo,
        CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType)
        {
            return Results.BadRequest(new Application.Autenticacion.ErrorResponse(
                CodigosErrorFacturas.ArchivoNoAdmitido,
                MensajesFacturas.LogoNoAdmitido,
                "archivo"));
        }

        var formulario = await peticion.ReadFormAsync(cancelacion);
        var resultado = await logo.SubirAsync(LeerArchivo(formulario), cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Empresa)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }

    /// <summary>
    /// Idempotente: quitar un logo que no está responde <c>204</c> igual. No pide confirmación aparte,
    /// porque no destruye nada que no se pueda volver a subir (precedente [004], FR-003).
    /// </summary>
    private static async Task<IResult> QuitarLogoAsync(
        GestionarLogo logo,
        CancellationToken cancelacion)
    {
        await logo.QuitarAsync(cancelacion);

        return Results.NoContent();
    }

    private static ArchivoCargado? LeerArchivo(IFormCollection formulario)
    {
        var archivo = formulario.Files["archivo"];

        if (archivo is null || archivo.Length == 0)
        {
            return null;
        }

        return new ArchivoCargado(
            Path.GetFileName(archivo.FileName),
            archivo.Length,
            archivo.OpenReadStream);
    }
}
