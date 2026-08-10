using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota.Documentacion;

/// <summary>
/// Corrección de un documento mal cargado (FR-026), con <b>las mismas validaciones que el alta</b>.
///
/// Sobre el adjunto:
/// <list type="bullet">
///   <item>Si no viene uno nuevo, el actual se conserva.</item>
///   <item>Si viene, se escribe primero, se confirma la fila y <b>recién después se borra el
///   viejo</b> (FR-026a, CHK023). Un escaneo que ya no corresponde deja de existir en vez de quedar
///   guardado por las dudas, y el borrado va al final para que el peor final posible sea un archivo
///   huérfano y nunca una fila que apunta a un archivo que no está (convención [003]).</item>
///   <item>Si la fila no llega a confirmarse, se borra el archivo nuevo y el viejo queda intacto: el
///   documento tiene que quedar exactamente como estaba (FR-029).</item>
/// </list>
///
/// Corregir la fecha de vencimiento puede cambiar cuál es el documento vigente de su tipo y, con eso,
/// el estado del vehículo. No hay nada que actualizar: se recalcula al leer (FR-024).
/// </summary>
public class CorregirDocumentoVehiculo(
    IRepositorioDocumentacionVehiculo repositorio,
    IAlmacenDeArchivos almacen,
    IValidadorDeArchivo validador)
{
    public async Task<ResultadoDocumentoVehiculo> EjecutarAsync(
        int id,
        DocumentoRequest peticion,
        ArchivoCargado? archivo,
        CancellationToken cancelacion = default)
    {
        if (ValidadorDocumento.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoDocumentoVehiculo(
                ErrorDocumentoVehiculo.DatosInvalidos,
                Campo: invalido);
        }

        var documento = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (documento is null)
        {
            return new ResultadoDocumentoVehiculo(ErrorDocumentoVehiculo.NoEncontrado);
        }

        // Mismas validaciones que el alta: el tipo tiene que seguir activo y de ámbito vehículo, así
        // que corregir el tipo nunca puede llevarlo a uno de otro ámbito (FR-017a, FR-026).
        var tipo = await repositorio.ObtenerTipoActivoDeVehiculoAsync(
            peticion.DocumentacionTipoId!.Value,
            cancelacion);

        if (tipo is null)
        {
            return new ResultadoDocumentoVehiculo(
                ErrorDocumentoVehiculo.TipoInexistente,
                Campo: "documentacionTipoId");
        }

        var emision = DateOnly.Parse(peticion.FechaEmision!);
        var vencimiento = DateOnly.Parse(peticion.FechaVencimiento!);

        if (vencimiento <= emision)
        {
            return new ResultadoDocumentoVehiculo(
                ErrorDocumentoVehiculo.VencimientoAnteriorAEmision,
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
                return new ResultadoDocumentoVehiculo(
                    ErrorDocumentoVehiculo.ArchivoNoAdmitido,
                    Campo: "archivo");
            }

            try
            {
                rutaNueva = await almacen.GuardarAsync(contenido, cancelacion);
            }
            catch (ArchivoNoGuardadoException)
            {
                // El documento queda exactamente como estaba, con su adjunto anterior (FR-029).
                return new ResultadoDocumentoVehiculo(
                    ErrorDocumentoVehiculo.ArchivoNoGuardado,
                    Campo: "archivo");
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

        // Recién ahora, con el cambio confirmado: al adjunto viejo ya no lo referencia nadie
        // (FR-026a, CHK023).
        if (rutaNueva is not null && rutaAnterior is not null)
        {
            await almacen.BorrarAsync(rutaAnterior, CancellationToken.None);
        }

        documento.Tipo = tipo;

        var delVehiculo = await repositorio.ConsultarDelVehiculoAsync(documento.VehiculoId, cancelacion);
        var esVigente = CalculadorEstadoVehiculo
            .VigentesDeCadaTipo(delVehiculo)
            .Any(vigente => vigente.Id == documento.Id);

        return new ResultadoDocumentoVehiculo(
            ErrorDocumentoVehiculo.Ninguno,
            DocumentoVehiculoDto.Desde(documento, esVigente, FechaHoyArgentina.Hoy()));
    }
}
