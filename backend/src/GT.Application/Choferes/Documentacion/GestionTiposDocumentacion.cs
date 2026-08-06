using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

/// <summary>
/// Catálogo de tipos de documentación (FR-013, FR-014).
///
/// Los días de aviso de cada tipo son lo que decide desde cuándo un documento figura como próximo a
/// vencer. Cambiarlos <b>recalcula</b> el estado de los documentos existentes la próxima vez que se
/// consultan, sin actualizar ninguna fila: el estado no está guardado (research §2).
/// </summary>
public class GestionTiposDocumentacion(IRepositorioTiposDocumentacion repositorio)
{
    public async Task<List<TipoDocumentacionDto>> ConsultarAsync(
        bool soloActivos = false,
        CancellationToken cancelacion = default)
    {
        var tipos = await repositorio.ConsultarAsync(soloActivos, cancelacion);

        return tipos.Select(TipoDocumentacionDto.Desde).ToList();
    }

    public async Task<ResultadoTipoDocumentacion> CrearAsync(
        TipoDocumentacionRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorTipoDocumentacion.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTipoDocumentacion(ErrorTipoDocumentacion.DatosInvalidos, Campo: invalido);
        }

        var nombre = peticion.Nombre!.Trim();

        if (await repositorio.ExisteNombreAsync(nombre, null, cancelacion))
        {
            return new ResultadoTipoDocumentacion(
                ErrorTipoDocumentacion.NombreDuplicado,
                Campo: "nombre");
        }

        var tipo = new DocumentacionTipo
        {
            Nombre = nombre,
            DiasAvisoVencimiento = peticion.DiasAvisoVencimiento!.Value,
            Activo = true,
        };

        await repositorio.AgregarAsync(tipo, cancelacion);

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (NombreDeTipoDuplicadoException)
        {
            return new ResultadoTipoDocumentacion(
                ErrorTipoDocumentacion.NombreDuplicado,
                Campo: "nombre");
        }

        // Recién creado: todavía no lo usa ningún documento.
        return new ResultadoTipoDocumentacion(
            ErrorTipoDocumentacion.Ninguno,
            TipoDocumentacionDto.Desde(tipo, documentosAsociados: 0));
    }

    public async Task<ResultadoTipoDocumentacion> ModificarAsync(
        int id,
        TipoDocumentacionRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorTipoDocumentacion.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTipoDocumentacion(ErrorTipoDocumentacion.DatosInvalidos, Campo: invalido);
        }

        var tipo = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (tipo is null)
        {
            return new ResultadoTipoDocumentacion(ErrorTipoDocumentacion.NoEncontrado);
        }

        var nombre = peticion.Nombre!.Trim();

        // Conservar el propio nombre no es un duplicado.
        if (await repositorio.ExisteNombreAsync(nombre, id, cancelacion))
        {
            return new ResultadoTipoDocumentacion(
                ErrorTipoDocumentacion.NombreDuplicado,
                Campo: "nombre");
        }

        tipo.Nombre = nombre;
        tipo.DiasAvisoVencimiento = peticion.DiasAvisoVencimiento!.Value;

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (NombreDeTipoDuplicadoException)
        {
            return new ResultadoTipoDocumentacion(
                ErrorTipoDocumentacion.NombreDuplicado,
                Campo: "nombre");
        }

        var documentos = await repositorio.ContarDocumentosAsync(id, cancelacion);

        return new ResultadoTipoDocumentacion(
            ErrorTipoDocumentacion.Ninguno,
            TipoDocumentacionDto.Desde(tipo, documentos));
    }

    /// <summary>
    /// Baja lógica. Se rechaza si el tipo tiene documentos asociados, informando cuántos son
    /// (FR-014): borrarlo dejaría documentos sin tipo y sin forma de calcular su estado.
    /// </summary>
    public async Task<ResultadoTipoDocumentacion> DarDeBajaAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var tipo = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (tipo is null)
        {
            return new ResultadoTipoDocumentacion(ErrorTipoDocumentacion.NoEncontrado);
        }

        var documentos = await repositorio.ContarDocumentosAsync(id, cancelacion);
        if (documentos > 0)
        {
            return new ResultadoTipoDocumentacion(ErrorTipoDocumentacion.ConDocumentos)
            {
                CantidadDocumentos = documentos,
            };
        }

        tipo.Activo = false;
        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoTipoDocumentacion(
            ErrorTipoDocumentacion.Ninguno,
            TipoDocumentacionDto.Desde(tipo, documentosAsociados: 0));
    }
}
