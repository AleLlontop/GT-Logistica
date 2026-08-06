using GT.Domain.Choferes;

using DocumentoDeChofer = GT.Domain.Choferes.Documentacion;

namespace GT.Application.Choferes.Documentacion;

/// <summary>
/// Carga de un documento con su escaneo opcional (FR-015, FR-016, FR-015e).
///
/// <b>El orden entre el disco y la base no es casual</b> (research §10): el archivo se escribe
/// primero y la fila se confirma después. Si la fila falla, se borra el archivo recién escrito. Así
/// el único estado roto posible es un archivo que ninguna fila referencia —invisible para quien
/// opera— y nunca una fila que dice tener adjunto y no lo tiene, que es lo que FR-015e prohíbe.
/// </summary>
public class CargarDocumento(
    IRepositorioDocumentacion repositorio,
    IAlmacenDeArchivos almacen,
    IValidadorDeArchivo validador)
{
    public async Task<ResultadoDocumento> EjecutarAsync(
        int choferId,
        DocumentoRequest peticion,
        ArchivoCargado? archivo,
        CancellationToken cancelacion = default)
    {
        if (ValidadorDocumento.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoDocumento(ErrorDocumento.DatosInvalidos, Campo: invalido);
        }

        if (!await repositorio.ExisteChoferAsync(choferId, cancelacion))
        {
            return new ResultadoDocumento(ErrorDocumento.ChoferNoEncontrado);
        }

        var tipo = await repositorio.ObtenerTipoActivoAsync(peticion.DocumentacionTipoId!.Value, cancelacion);
        if (tipo is null)
        {
            return new ResultadoDocumento(ErrorDocumento.TipoInexistente, Campo: "documentacionTipoId");
        }

        var emision = DateOnly.Parse(peticion.FechaEmision!);
        var vencimiento = DateOnly.Parse(peticion.FechaVencimiento!);

        // FR-016: posterior, no igual.
        if (vencimiento <= emision)
        {
            return new ResultadoDocumento(
                ErrorDocumento.VencimientoAnteriorAEmision,
                Campo: "fechaVencimiento");
        }

        var adjunto = await PrepararAdjuntoAsync(archivo, cancelacion);
        if (adjunto.Error is not ErrorDocumento.Ninguno)
        {
            return new ResultadoDocumento(adjunto.Error, Campo: "archivo");
        }

        var documento = new DocumentoDeChofer
        {
            ChoferId = choferId,
            DocumentacionTipoId = tipo.Id,
            Numero = peticion.Numero!.Trim(),
            FechaEmision = emision,
            FechaVencimiento = vencimiento,
            ArchivoRuta = adjunto.Ruta,
            ArchivoNombre = adjunto.Ruta is null ? null : archivo!.Nombre,
            ArchivoTipoContenido = adjunto.TipoContenido,
        };

        await repositorio.AgregarAsync(documento, cancelacion);

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch
        {
            // El archivo ya está escrito y la fila no llegó a existir: se compensa borrándolo, que es
            // lo que deja el estado roto aceptable en vez del prohibido (research §10).
            if (adjunto.Ruta is not null)
            {
                await almacen.BorrarAsync(adjunto.Ruta, CancellationToken.None);
            }

            throw;
        }

        documento.Tipo = tipo;

        // Recién cargado: es el vigente de su tipo si ninguno de los otros vence más lejos.
        var esVigente = await EsElVigenteDeSuTipoAsync(documento, cancelacion);

        return new ResultadoDocumento(
            ErrorDocumento.Ninguno,
            DocumentoDto.Desde(documento, esVigente, FechaHoyArgentina.Hoy()));
    }

    /// <summary>
    /// Valida y escribe el adjunto, si vino alguno. Devuelve la ruta y el tipo de contenido
    /// <b>deducido de la firma</b>, no el declarado por el navegador (FR-015a).
    /// </summary>
    private async Task<(ErrorDocumento Error, string? Ruta, string? TipoContenido)> PrepararAdjuntoAsync(
        ArchivoCargado? archivo,
        CancellationToken cancelacion)
    {
        // Sin archivo el documento es válido igual: queda como documentación sin respaldo (FR-015).
        if (archivo is null)
        {
            return (ErrorDocumento.Ninguno, null, null);
        }

        await using var contenido = archivo.Abrir();

        var validacion = await validador.ValidarAsync(contenido, archivo.TamanioEnBytes, cancelacion);
        if (!validacion.EsValido)
        {
            return (ErrorDocumento.ArchivoNoAdmitido, null, null);
        }

        try
        {
            var ruta = await almacen.GuardarAsync(contenido, cancelacion);
            return (ErrorDocumento.Ninguno, ruta, validacion.TipoContenido);
        }
        catch (ArchivoNoGuardadoException)
        {
            return (ErrorDocumento.ArchivoNoGuardado, null, null);
        }
    }

    private async Task<bool> EsElVigenteDeSuTipoAsync(
        DocumentoDeChofer documento,
        CancellationToken cancelacion)
    {
        var delChofer = await repositorio.ConsultarDelChoferAsync(documento.ChoferId, cancelacion);

        return CalculadorEstadoChofer
            .VigentesDeCadaTipo(delChofer)
            .Any(vigente => vigente.Id == documento.Id);
    }
}
