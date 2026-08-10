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

    /// <summary>
    /// Único en <b>todo</b> el catálogo (FR-012), no por ámbito: el índice único no lleva filtro.
    /// Precio concreto de esa decisión: no pueden convivir un "Seguro" de chofer y un "Seguro" de
    /// vehículo, y si aparece la colisión se resuelve con el nombre ("Seguro del vehículo"), no con
    /// el esquema (Módulo 4, research §3).
    /// </summary>
    public required string Nombre { get; set; }

    /// <summary>
    /// Con cuántos días de anticipación el sistema empieza a avisar del vencimiento. Entero mayor o
    /// igual a cero (FR-013). En cero no hay período de aviso intermedio: el documento pasa de
    /// vigente a vencido el día siguiente al vencimiento.
    /// </summary>
    public required int DiasAvisoVencimiento { get; set; }

    /// <summary>
    /// A qué se aplica el tipo, y por lo tanto en qué módulo se ofrece: el formulario de documento de
    /// vehículo no muestra los de chofer, ni al revés (Módulo 4, FR-017, FR-017a).
    ///
    /// Obligatorio al crear y al modificar. Se puede corregir mientras el tipo no tenga ningún
    /// documento cargado —de ninguno de los dos lados—: con documentos asociados se rechaza, porque
    /// si no quedarían colgando de un tipo que su propio módulo ya no ofrece (FR-017d).
    ///
    /// Los tipos que ya existían antes del Módulo 4 quedaron con ámbito <c>Chofer</c> por la
    /// migración, así que ningún documento cargado cambió de comportamiento (FR-017c).
    /// </summary>
    public required DocumentacionAmbito Ambito { get; set; }

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
