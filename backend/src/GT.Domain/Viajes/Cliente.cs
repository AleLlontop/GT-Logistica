namespace GT.Domain.Viajes;

/// <summary>
/// Empresa o persona que contrata el servicio de transporte (FR-001 a FR-009).
///
/// Vive dentro del módulo de viajes y no como módulo hermano: existe para sostener al viaje, no tiene
/// spec propia y comparte sus dos permisos (FR-053). Es el mismo criterio con el que
/// <c>Transportista</c> quedó dentro de choferes y <c>TipoVehiculo</c> dentro de flota.
///
/// <b>Nunca se borra físicamente</b> (FR-001): <see cref="Activo"/> en <c>false</c> es la baja lógica.
/// </summary>
public class Cliente
{
    public int Id { get; set; }

    /// <summary>Obligatoria, con <c>Trim</c> al guardar (FR-002).</summary>
    public required string RazonSocial { get; set; }

    /// <summary>
    /// Sólo dígitos, once, con dígito verificador válido. <b>Normalizado antes de validar y de
    /// guardar</b> (FR-004), con las mismas piezas del Módulo 3 —<c>NormalizadorDocumentoNumerico</c>
    /// y <c>ValidadorCuit</c>— sin modificarlas.
    ///
    /// Único en todo el padrón con un índice <b>sin filtro</b> por <see cref="Activo"/>: el CUIT de un
    /// cliente dado de baja sigue ocupado, y volver a registrarlo se rechaza pidiendo darlo de alta de
    /// nuevo en vez de dejar dos filas para el mismo contribuyente (FR-003, FR-007).
    /// </summary>
    public required string Cuit { get; set; }

    /// <summary>Obligatorio (FR-002).</summary>
    public required string Telefono { get; set; }

    /// <summary>Obligatorio, con formato válido y <b>sin</b> unicidad (FR-004).</summary>
    public required string Email { get; set; }

    /// <summary>Opcional: el módulo no la usa para operar (FR-002, Principio V).</summary>
    public string? Direccion { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica (FR-001). Un cliente inactivo deja de ofrecerse al registrar y
    /// al modificar viajes, pero los viajes que ya lo tienen lo conservan y lo siguen mostrando,
    /// señalado con la palabra que lo explica (FR-008).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Viajes del cliente. <b>Sólo los <c>pendiente</c> y <c>en curso</c> impiden la baja</b>, y el
    /// rechazo dice cuántos son (FR-006, SC-009): un cliente que dejó de operar con la empresa tiene
    /// historial por definición, y contarlo haría imposible justo el caso que US1 pide.
    /// </summary>
    public ICollection<Viaje> Viajes { get; } = [];
}
