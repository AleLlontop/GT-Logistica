using GT.Application.Choferes.Documentacion;

namespace GT.Application.Flota.Documentacion;

/// <summary>
/// Eliminación definitiva de un documento (FR-027, FR-028).
///
/// Es la única entidad del módulo que se borra de verdad, y va a propósito contra la convención de
/// baja lógica del resto: un documento cargado por error no es un hecho histórico que convenga
/// conservar, y encima puede tapar el estado real del vehículo, porque el vigente de cada tipo es el
/// de vencimiento más lejano (FR-024).
///
/// Primero la fila y después el archivo, nunca al revés: si el proceso se cayera en el medio, sobra
/// un archivo que nadie referencia en vez de faltar el archivo de una fila que dice tenerlo
/// (convención [003]).
///
/// Si el eliminado era el vigente de su tipo, el más reciente de los que quedan vuelve a mandar y el
/// estado del vehículo cambia solo, sin actualizar ninguna fila (FR-024, SC-010).
/// </summary>
public class EliminarDocumentoVehiculo(
    IRepositorioDocumentacionVehiculo repositorio,
    IAlmacenDeArchivos almacen)
{
    public async Task<ResultadoDocumentoVehiculo> EjecutarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (documento is null)
        {
            return new ResultadoDocumentoVehiculo(ErrorDocumentoVehiculo.NoEncontrado);
        }

        var ruta = documento.ArchivoRuta;

        await repositorio.EliminarAsync(documento, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        if (ruta is not null)
        {
            await almacen.BorrarAsync(ruta, CancellationToken.None);
        }

        return new ResultadoDocumentoVehiculo(ErrorDocumentoVehiculo.Ninguno);
    }
}
