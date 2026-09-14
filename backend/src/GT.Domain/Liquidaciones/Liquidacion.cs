using GT.Domain.Choferes;

namespace GT.Domain.Liquidaciones;

/// <summary>
/// Lo que G&amp;T Logística le debe a un transportista externo por los viajes de un período
/// (FR-001 a FR-060). Entidad principal del Módulo 9.
///
/// <b>El total y lo pagado se guardan, siendo sumas</b>, porque son la condición de los <c>UPDATE</c>
/// que cierran las carreras de pagar, editar y anular (research §2). Un <c>CHECK</c> los ata al estado y
/// <c>CoherenciaDeLiquidacionTests</c> los compara con sus sumas.
///
/// <b>La fecha de generación no es columna</b>: sale de la entrada <c>generacion</c> del historial
/// (research §3).
/// </summary>
public class Liquidacion
{
    public int Id { get; set; }

    /// <summary>
    /// Número visible, generado por la secuencia <c>NumeroDeLiquidacion</c> y nunca reutilizado, ni
    /// siquiera al anular (FR-016). Se muestra como <c>LQ-{Numero}</c>.
    /// </summary>
    /// <remarks>
    /// <b>Sin <c>required</c> y con <c>private set</c>, a propósito</b>: lo pone el <c>DEFAULT</c> de la
    /// columna. Si el código lo asignara, EF mandaría el <c>0</c> en el <c>INSERT</c> y la secuencia no se
    /// aplicaría nunca (research §12.2, el mismo caso que <c>Viaje.Numero</c>).
    /// </remarks>
    public int Numero { get; private set; }

    /// <summary>Externo y activo <b>al generar</b> (FR-001). Si después se da de baja, no cambia nada.</summary>
    public required int TransportistaId { get; set; }

    public Transportista? Transportista { get; set; }

    public required byte PeriodoMes { get; set; }

    public required short PeriodoAnio { get; set; }

    /// <summary>
    /// Suma exacta de los importes de sus viajes (FR-007). Nunca llega desde el cliente HTTP: la calcula
    /// el servidor con los viajes que encuentra en la base.
    /// </summary>
    public required decimal ImporteTotal { get; set; }

    /// <summary>Suma de sus órdenes de pago. Nunca supera el total (FR-043).</summary>
    public decimal ImportePagado { get; set; }

    public EstadoLiquidacion Estado { get; set; } = EstadoLiquidacion.Pendiente;

    /// <summary>Obligatorio al anular y nulo en cualquier otro estado (FR-055).</summary>
    public string? MotivoAnulacion { get; set; }

    /// <summary>
    /// Sube de a uno con cada edición guardada <b>y con nada más</b>. La edición sólo se aplica si sigue
    /// siendo la versión con la que se abrió: es lo que impide que una edición pise a otra (FR-048).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Los viajes que agrupa —o que agrupaba, si está anulada (FR-028)—. Los vigentes y los no vigentes
    /// conviven acá: la marca la lleva cada vínculo.
    /// </summary>
    public ICollection<LiquidacionViaje> Viajes { get; } = [];

    public ICollection<OrdenDePago> OrdenesDePago { get; } = [];

    /// <summary>Historial de FR-033, de la entrada más vieja a la más nueva.</summary>
    public ICollection<CambioDeLiquidacion> Cambios { get; } = [];
}
