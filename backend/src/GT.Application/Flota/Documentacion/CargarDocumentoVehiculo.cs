using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota.Documentacion;

/// <summary>
/// Carga de un documento de una unidad, con su escaneo opcional (FR-016, FR-016a, FR-018, FR-029).
///
/// <b>El orden entre el disco y la base no es casual</b> (convención [003]): el archivo se escribe
/// primero y la fila se confirma después. Si la fila falla, se borra el archivo recién escrito. Así
/// el único estado roto posible es un archivo que ninguna fila referencia —invisible para quien
/// opera— y nunca una fila que dice tener adjunto y no lo tiene, que es lo que FR-029 prohíbe.
///
/// Un documento del mismo tipo con vencimiento posterior se acepta como <b>renovación</b>: el
/// anterior queda como historial, deja de contar para el estado del vehículo y deja de alertar
/// (FR-023, FR-024, SC-010). No hay nada que actualizar para eso: el vigente se elige al leer.
///
/// Reutiliza <see cref="IAlmacenDeArchivos"/> y <see cref="IValidadorDeArchivo"/> del Módulo 3 sin
/// modificarlos: guardan un stream y reconocen una firma, y no saben a qué entidad pertenece el
/// archivo (research §2). Los adjuntos van al <b>mismo volumen</b>, sin variable de entorno nueva.
/// </summary>
public class CargarDocumentoVehiculo(
    IRepositorioDocumentacionVehiculo repositorio,
    IAlmacenDeArchivos almacen,
    IValidadorDeArchivo validador)
{
    public async Task<ResultadoDocumentoVehiculo> EjecutarAsync(
        int vehiculoId,
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

        if (!await repositorio.ExisteVehiculoAsync(vehiculoId, cancelacion))
        {
            return new ResultadoDocumentoVehiculo(ErrorDocumentoVehiculo.VehiculoNoEncontrado);
        }

        // Activo y de ámbito vehículo: un tipo de chofer se rechaza igual que uno inexistente
        // (FR-017a, US3 esc. 12).
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

        // FR-018: posterior, no igual.
        if (vencimiento <= emision)
        {
            return new ResultadoDocumentoVehiculo(
                ErrorDocumentoVehiculo.VencimientoAnteriorAEmision,
                Campo: "fechaVencimiento");
        }

        var adjunto = await PrepararAdjuntoAsync(archivo, cancelacion);
        if (adjunto.Error is not ErrorDocumentoVehiculo.Ninguno)
        {
            return new ResultadoDocumentoVehiculo(adjunto.Error, Campo: "archivo");
        }

        var documento = new DocumentacionVehiculo
        {
            VehiculoId = vehiculoId,
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
            // lo que deja el estado roto aceptable en vez del prohibido (FR-029).
            if (adjunto.Ruta is not null)
            {
                await almacen.BorrarAsync(adjunto.Ruta, CancellationToken.None);
            }

            throw;
        }

        documento.Tipo = tipo;

        // Recién cargado: es el vigente de su tipo si ninguno de los otros vence más lejos.
        var esVigente = await EsElVigenteDeSuTipoAsync(documento, cancelacion);

        return new ResultadoDocumentoVehiculo(
            ErrorDocumentoVehiculo.Ninguno,
            DocumentoVehiculoDto.Desde(documento, esVigente, FechaHoyArgentina.Hoy()));
    }

    /// <summary>
    /// Valida y escribe el adjunto, si vino alguno. Devuelve la ruta y el tipo de contenido
    /// <b>deducido de la firma</b>, no el declarado por el navegador (FR-025).
    /// </summary>
    private async Task<(ErrorDocumentoVehiculo Error, string? Ruta, string? TipoContenido)>
        PrepararAdjuntoAsync(ArchivoCargado? archivo, CancellationToken cancelacion)
    {
        // Sin archivo el documento es válido igual: queda como documentación sin respaldo, y eso no
        // altera el estado general del vehículo (FR-016a).
        if (archivo is null)
        {
            return (ErrorDocumentoVehiculo.Ninguno, null, null);
        }

        await using var contenido = archivo.Abrir();

        var validacion = await validador.ValidarAsync(contenido, archivo.TamanioEnBytes, cancelacion);
        if (!validacion.EsValido)
        {
            return (ErrorDocumentoVehiculo.ArchivoNoAdmitido, null, null);
        }

        try
        {
            var ruta = await almacen.GuardarAsync(contenido, cancelacion);
            return (ErrorDocumentoVehiculo.Ninguno, ruta, validacion.TipoContenido);
        }
        catch (ArchivoNoGuardadoException)
        {
            return (ErrorDocumentoVehiculo.ArchivoNoGuardado, null, null);
        }
    }

    private async Task<bool> EsElVigenteDeSuTipoAsync(
        DocumentacionVehiculo documento,
        CancellationToken cancelacion)
    {
        var delVehiculo = await repositorio.ConsultarDelVehiculoAsync(documento.VehiculoId, cancelacion);

        return CalculadorEstadoVehiculo
            .VigentesDeCadaTipo(delVehiculo)
            .Any(vigente => vigente.Id == documento.Id);
    }
}
