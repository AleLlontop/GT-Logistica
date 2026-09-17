using Microsoft.Net.Http.Headers;

namespace GT.Api.Archivos;

/// <summary>
/// Cómo se sirven los escaneos de documentación, en los dos módulos que los tienen (choferes y
/// flota).
///
/// Vive fuera de los dos porque la decisión es la misma y tiene que seguir siéndolo: si un módulo
/// abriera el archivo y el otro lo descargara, la misma acción —<i>Abrir archivo</i>— haría cosas
/// distintas según de dónde se la toque.
/// </summary>
public static class ResultadoArchivo
{
    /// <summary>
    /// Devuelve el archivo para que el navegador lo <b>muestre</b>, no para que lo baje.
    ///
    /// La diferencia es el <c>Content-Disposition</c>: <c>attachment</c> obliga a descargar y
    /// después abrir a mano; <c>inline</c> deja que la pestaña lo renderice. El nombre viaja igual,
    /// así que "Guardar como" sigue proponiendo el original.
    ///
    /// Servir contenido en línea desde el propio origen sólo es seguro porque el tipo se dedujo de
    /// la <b>firma</b> del archivo al cargarlo y únicamente se admiten PDF, JPG y PNG (FR-025):
    /// nada que el navegador pueda ejecutar como página. <c>nosniff</c> cierra el resto —sin él, el
    /// navegador puede ignorar el tipo declarado y adivinar otro mirando el contenido—.
    /// </summary>
    public static IResult EnLinea(
        HttpContext contexto,
        Stream contenido,
        string tipoContenido,
        string nombre)
    {
        var disposicion = new ContentDispositionHeaderValue("inline");
        disposicion.SetHttpFileName(nombre);

        contexto.Response.Headers.ContentDisposition = disposicion.ToString();
        contexto.Response.Headers.XContentTypeOptions = "nosniff";

        // Sin `fileDownloadName`: pasárselo escribiría `attachment` y pisaría la cabecera de arriba.
        return Results.File(contenido, tipoContenido);
    }
}
