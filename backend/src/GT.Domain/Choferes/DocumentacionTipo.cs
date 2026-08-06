namespace GT.Domain.Choferes;

/// <summary>
/// Categoría de documento que el sistema controla: licencia de conducir, LiNTI, psicofísico, ART,
/// entre otros (FR-012).
///
/// El catálogo <b>arranca vacío</b> y se completa desde la pantalla de tipos: no se precarga por
/// migración. Sin al menos un tipo cargado no se puede registrar ningún documento.
///
/// Ningún tipo es obligatorio para un chofer: el estado general informa sobre los documentos
/// cargados y el sistema no infiere que falte uno que nunca se cargó (FR-029a).
/// </summary>
public class DocumentacionTipo
{
    public int Id { get; set; }

    /// <summary>Único en el catálogo (FR-012).</summary>
    public required string Nombre { get; set; }

    /// <summary>
    /// Con cuántos días de anticipación el sistema empieza a avisar del vencimiento. Entero mayor o
    /// igual a cero (FR-013). En cero no hay período de aviso intermedio: el documento pasa de
    /// vigente a vencido el día siguiente al vencimiento.
    /// </summary>
    public required int DiasAvisoVencimiento { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica: el tipo deja de ofrecerse al cargar documentación y su
    /// registro no se borra (FR-012). La baja se rechaza si tiene documentos asociados (FR-014).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Documentos de este tipo. Su cantidad es lo que impide la baja, y el mensaje de rechazo la
    /// informa (FR-014).
    /// </summary>
    public ICollection<Documentacion> Documentos { get; } = [];
}
