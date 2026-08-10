namespace GT.Domain.Flota;

/// <summary>
/// Categoría de unidad: tractor, semirremolque, chasis, utilitario (FR-009).
///
/// El catálogo <b>arranca vacío</b> y se completa desde la pantalla del módulo: no se precarga por
/// migración. Sin al menos un tipo activo no se puede registrar ningún vehículo (FR-005).
/// </summary>
public class TipoVehiculo
{
    public int Id { get; set; }

    /// <summary>Único en el catálogo, garantizado con un índice en la base (FR-009).</summary>
    public required string Nombre { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica: el tipo deja de ofrecerse al registrar o modificar un vehículo
    /// y su registro no se borra nunca (FR-009, FR-028). Los vehículos ya registrados con un tipo
    /// inactivo lo conservan y lo siguen mostrando (FR-011).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Vehículos de este tipo. La baja se rechaza si hay <b>cualquiera</b>, activo o dado de baja, y
    /// el mensaje dice cuántos son (FR-010).
    ///
    /// A diferencia del transportista —que se rechaza sólo por dependientes activos— acá cuentan
    /// todos, porque un vehículo dado de baja sigue mostrando su tipo (FR-011, research §8).
    /// </summary>
    public ICollection<Vehiculo> Vehiculos { get; } = [];
}
