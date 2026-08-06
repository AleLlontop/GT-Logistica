using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

/// <summary>
/// Corrección de un documento mal cargado (FR-015b), con <b>las mismas validaciones que el alta</b>.
///
/// Sobre el adjunto (research §10):
/// <list type="bullet">
///   <item>Si no viene uno nuevo, el actual se conserva.</item>
///   <item>Si viene, se escribe primero, se confirma la fila y recién después se borra el viejo.</item>
///   <item>Si la fila no llega a confirmarse, se borra el archivo nuevo y el viejo queda intacto: el
///   documento tiene que quedar exactamente como estaba (FR-015e).</item>
/// </list>
///
/// Corregir la fecha de vencimiento puede cambiar cuál es el documento vigente de su tipo y, con
/// eso, el estado del chofer. No hay nada que actualizar: se recalcula al leer (FR-020a).
/// </summary>
public class CorregirDocumento(
    IRepositorioDocumentacion repositorio,
    IAlmacenDeArchivos almacen,
    IValidadorDeArchivo validador)
{
    public async Task<ResultadoDocumento> EjecutarAsync(
        int id,
        DocumentoRequest peticion,
        ArchivoCargado? archivo,
        CancellationToken cancelacion = default)
    {
        if (ValidadorDocumento.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoDocumento(ErrorDocumento.DatosInvalidos, Campo: invalido);
        }

        var documento = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (documento is null)
        {
            return new ResultadoDocumento(ErrorDocumento.NoEncontrado);
        }

        var tipo = await repositorio.ObtenerTipoActivoAsync(peticion.DocumentacionTipoId!.Value, cancelacion);
        if (tipo is null)
        {
            return new ResultadoDocumento(ErrorDocumento.TipoInexistente, Campo: "documentacionTipoId");
        }

        var emision = DateOnly.Parse(peticion.FechaEmision!);
        var vencimiento = DateOnly.Parse(peticion.FechaVencimiento!);

        if (vencimiento <= emision)
        {
            return new ResultadoDocumento(
                ErrorDocumento.VencimientoAnteriorAEmision,
                Campo: "fechaVencimiento");
        }

        var rutaAnterior = documento.ArchivoRuta;
        string? rutaNueva = null;

        if (archivo is not null)
        {
            await using var contenido = archivo.Abrir();

            var validacion = await validador.ValidarAsync(contenido, archivo.TamanioEnBytes, cancelacion);
            if (!validacion.EsValido)
            {
                return new ResultadoDocumento(ErrorDocumento.ArchivoNoAdmitido, Campo: "archivo");
            }

            try
            {
                rutaNueva = await almacen.GuardarAsync(contenido, cancelacion);
            }
            catch (ArchivoNoGuardadoException)
            {
                // El documento queda exactamente como estaba, con su adjunto anterior (FR-015e).
                return new ResultadoDocumento(ErrorDocumento.ArchivoNoGuardado, Campo: "archivo");
            }

            documento.ArchivoRuta = rutaNueva;
            documento.ArchivoNombre = archivo.Nombre;
            documento.ArchivoTipoContenido = validacion.TipoContenido;
        }

        documento.DocumentacionTipoId = tipo.Id;
        documento.Numero = peticion.Numero!.Trim();
        documento.FechaEmision = emision;
        documento.FechaVencimiento = vencimiento;

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch
        {
            if (rutaNueva is not null)
            {
                await almacen.BorrarAsync(rutaNueva, CancellationToken.None);
            }

            throw;
        }

        // Recién ahora, con el cambio confirmado: al adjunto viejo ya no lo referencia nadie.
        if (rutaNueva is not null && rutaAnterior is not null)
        {
            await almacen.BorrarAsync(rutaAnterior, CancellationToken.None);
        }

        documento.Tipo = tipo;

        var delChofer = await repositorio.ConsultarDelChoferAsync(documento.ChoferId, cancelacion);
        var esVigente = CalculadorEstadoChofer
            .VigentesDeCadaTipo(delChofer)
            .Any(vigente => vigente.Id == documento.Id);

        return new ResultadoDocumento(
            ErrorDocumento.Ninguno,
            DocumentoDto.Desde(documento, esVigente, FechaHoyArgentina.Hoy()));
    }
}
