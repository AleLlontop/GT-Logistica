using GT.Domain.Choferes;

namespace GT.Domain.Flota;

/// <summary>
/// Documento obligatorio de una unidad: VTV, seguro, RUTA, cédula verde (FR-016).
///
/// <b>Tabla propia, separada de la documentación de choferes</b> (research §1). La alternativa
/// natural —una sola tabla con dos dueños posibles— obligaba a volver <c>Documentaciones.ChoferId</c>
/// anulable y a cambiar una garantía real de la base por una restricción escrita a mano. Lo que se
/// comparte de verdad no es la tabla: es la <i>regla</i> de vencimientos
/// (<see cref="CalculadorEstadoDocumento"/>) y el <i>almacén</i> de archivos, y las dos se reutilizan
/// tal cual sin compartir filas.
///
/// <b>No tiene columna de estado</b>: se calcula al leer, con la fecha de vencimiento, los días de
/// aviso de su tipo y el día en curso en Argentina (FR-019, FR-020).
///
/// <b>Tampoco tiene <c>Activo</c></b>: es la única entidad del módulo que se borra físicamente
/// (FR-027, FR-028). Un documento cargado por error no es historia que convenga conservar, y además
/// taparía el estado real del vehículo, porque el vigente de cada tipo es el de vencimiento más
/// lejano (FR-024).
/// </summary>
public class DocumentacionVehiculo
{
    public int Id { get; set; }

    public required int VehiculoId { get; set; }

    public Vehiculo? Vehiculo { get; set; }

    /// <summary>El tipo tiene que estar <b>activo y de ámbito vehículo</b> (FR-017a).</summary>
    public required int DocumentacionTipoId { get; set; }

    public DocumentacionTipo? Tipo { get; set; }

    /// <summary>
    /// Número del papel. Obligatorio, hasta 50 caracteres y <b>sin unicidad</b>: una póliza conserva
    /// su número al renovarse, así que dos documentos del mismo vehículo y tipo pueden repetirlo
    /// (FR-016).
    /// </summary>
    public required string Numero { get; set; }

    public required DateOnly FechaEmision { get; set; }

    /// <summary><b>Posterior</b> a la fecha de emisión, no igual (FR-018).</summary>
    public required DateOnly FechaVencimiento { get; set; }

    /// <summary>
    /// Ruta relativa dentro del volumen de adjuntos —el <b>mismo</b> que usa el Módulo 3, sin
    /// variable de entorno nueva—. <c>null</c> es un documento sin respaldo, que es válido y no
    /// altera el estado general del vehículo (FR-016a).
    ///
    /// El nombre en disco lo genera el sistema, nunca el usuario: un nombre cargado por alguien puede
    /// contener <c>../</c> y escaparse del directorio, o repetirse y pisar otro documento.
    /// </summary>
    public string? ArchivoRuta { get; set; }

    /// <summary>Nombre original del archivo, sólo para mostrarlo y para la descarga.</summary>
    public string? ArchivoNombre { get; set; }

    /// <summary>
    /// <c>application/pdf</c>, <c>image/jpeg</c> o <c>image/png</c>, determinado por la <b>firma</b>
    /// del archivo y no por su extensión ni por el <c>Content-Type</c> declarado (FR-025).
    /// </summary>
    public string? ArchivoTipoContenido { get; set; }

    /// <summary>
    /// <c>true</c> si el documento tiene un archivo escaneado asociado. Es lo que la ficha usa para
    /// distinguir, documento por documento, cuál tiene respaldo (FR-016a).
    /// </summary>
    public bool TieneArchivo => ArchivoRuta is not null;
}
