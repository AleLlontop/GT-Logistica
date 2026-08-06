namespace GT.Domain.Choferes;

/// <summary>
/// Documento obligatorio de un chofer (FR-015).
///
/// <b>No tiene columna de estado</b>: se calcula al leer, con la fecha de vencimiento, los días de
/// aviso de su tipo y el día en curso (research §2). Es lo que hace que un documento pase solo de
/// vigente a próximo a vencer y luego a vencido, sin proceso nocturno ni intervención de nadie
/// (FR-019).
///
/// <b>Tampoco tiene <c>Activo</c></b>: es la única entidad del módulo que se borra físicamente
/// (FR-015d). Un documento cargado por error no es un hecho histórico que convenga conservar —es
/// basura que además puede tapar el estado real del chofer, porque el vigente de cada tipo es el de
/// vencimiento más lejano (research §8, §10)—.
/// </summary>
public class Documentacion
{
    public int Id { get; set; }

    public required int ChoferId { get; set; }

    public Chofer? Chofer { get; set; }

    public required int DocumentacionTipoId { get; set; }

    public DocumentacionTipo? Tipo { get; set; }

    /// <summary>
    /// Número del papel. Obligatorio, hasta 50 caracteres y <b>sin unicidad</b>: una licencia de
    /// conducir conserva su número al renovarse, así que dos documentos del mismo chofer y tipo
    /// pueden repetirlo (FR-015).
    /// </summary>
    public required string Numero { get; set; }

    public required DateOnly FechaEmision { get; set; }

    /// <summary>Posterior a la fecha de emisión (FR-016).</summary>
    public required DateOnly FechaVencimiento { get; set; }

    /// <summary>
    /// Ruta relativa dentro del volumen de adjuntos. <c>null</c> es un documento sin respaldo, que
    /// es válido y el sistema distingue de uno con archivo (FR-015).
    ///
    /// El nombre en disco lo genera el sistema, nunca el usuario: un nombre cargado por alguien
    /// puede contener <c>../</c> y escaparse del directorio, o repetirse y pisar otro documento
    /// (research §3).
    /// </summary>
    public string? ArchivoRuta { get; set; }

    /// <summary>Nombre original del archivo, sólo para mostrarlo y para la descarga.</summary>
    public string? ArchivoNombre { get; set; }

    /// <summary>
    /// <c>application/pdf</c>, <c>image/jpeg</c> o <c>image/png</c>, determinado por la firma del
    /// archivo y no por su extensión (FR-015a).
    /// </summary>
    public string? ArchivoTipoContenido { get; set; }

    /// <summary><c>true</c> si el documento tiene un archivo escaneado asociado.</summary>
    public bool TieneArchivo => ArchivoRuta is not null;
}
