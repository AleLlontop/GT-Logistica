using GT.Domain.Usuarios;

namespace GT.Domain.Liquidaciones;

/// <summary>
/// Pago que cancela total o parcialmente una liquidación (FR-036 a FR-044).
///
/// <b>No se edita ni se borra por ninguna vía</b> (FR-044): no hay endpoint que lo haga. Que la suma no
/// supere el total no lo garantiza esta tabla sino <c>Liquidaciones.ImportePagado</c> con su
/// <c>CHECK</c>: un <c>CHECK</c> no puede sumar filas de otra tabla.
/// </summary>
public class OrdenDePago
{
    public int Id { get; set; }

    /// <summary>
    /// Sale de la secuencia <c>NumeroDeOrdenDePago</c> (FR-039) y se muestra como <c>OP-{Numero}</c>. Sin
    /// <c>required</c> y con <c>private set</c> por el mismo motivo que <see cref="Liquidacion.Numero"/>.
    /// </summary>
    public int Numero { get; private set; }

    public required int LiquidacionId { get; set; }

    public Liquidacion? Liquidacion { get; set; }

    /// <summary>Entre la fecha de generación de la liquidación y el día en curso, incluidos (FR-038).</summary>
    public required DateOnly FechaPago { get; set; }

    public required decimal Importe { get; set; }

    /// <summary>Llega por parámetro desde el endpoint (FR-034).</summary>
    public required int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>Instante UTC del servidor, con <c>TimeProvider</c>. Sale con <c>Z</c> (convención [002]).</summary>
    public required DateTime RegistradaEn { get; set; }
}
