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
    /// <param name="ambito">
    /// Desde el Módulo 4, cada módulo pide sólo los tipos de su ámbito: el formulario de documento de
    /// vehículo consume <c>?ambito=vehiculo&amp;soloActivos=true</c> y no ve los de chofer, ni al
    /// revés (FR-017a). <c>null</c> devuelve los dos ámbitos, que es lo que muestra la pantalla de
    /// mantenimiento.
    /// </param>
    public async Task<List<TipoDocumentacionDto>> ConsultarAsync(
        bool soloActivos = false,
        DocumentacionAmbito? ambito = null,
        CancellationToken cancelacion = default)
    {
        var tipos = await repositorio.ConsultarAsync(soloActivos, ambito, cancelacion);

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
            // El validador ya rechazó un ámbito ausente o desconocido (FR-017).
            Ambito = ValidadorTipoDocumentacion.LeerAmbito(peticion.Ambito)!.Value,
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

        var ambito = ValidadorTipoDocumentacion.LeerAmbito(peticion.Ambito)!.Value;
        var documentos = await repositorio.ContarDocumentosAsync(id, cancelacion);

        // FR-017d: el ámbito se corrige mientras el tipo no tenga ningún documento, de ninguno de los
        // dos lados. Con documentos asociados, cambiarlo los dejaría colgando de un tipo que su
        // propio módulo ya no ofrece, y su formulario de corrección no podría volver a elegirlo.
        //
        // El nombre y los días de aviso se modifican igual, tengan documentos o no.
        if (tipo.Ambito != ambito && documentos > 0)
        {
            return new ResultadoTipoDocumentacion(
                ErrorTipoDocumentacion.AmbitoNoModificable,
                Campo: "ambito")
            {
                CantidadDocumentos = documentos,
            };
        }

        tipo.Nombre = nombre;
        tipo.DiasAvisoVencimiento = peticion.DiasAvisoVencimiento!.Value;
        tipo.Ambito = ambito;

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
