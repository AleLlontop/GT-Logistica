using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Domain.Viajes;

/// <summary>
/// Unidad de trabajo de la empresa: un cliente pide llevar una carga de un origen a un destino
/// (FR-010 a FR-047). Entidad principal del módulo.
/// </summary>
public class Viaje
{
    /// <summary>
    /// Días corridos en <c>en curso</c> a partir de los cuales el viaje se destaca como demorado
    /// (FR-039). Umbral único: la regla en C# y la subconsulta en SQL lo leen de acá.
    /// </summary>
    public const int DiasParaDemora = 5;

    public int Id { get; set; }

    /// <summary>
    /// Número visible del viaje, generado por el sistema y <b>nunca reutilizado</b>, ni siquiera tras
    /// anular el viaje que lo tenía (FR-011). No es editable por nadie en ningún estado (FR-017).
    /// </summary>
    /// <remarks>
    /// <b>No lleva <c>required</c> y el constructor no lo asigna, a propósito.</b> El valor lo pone el
    /// <c>DEFAULT NEXT VALUE FOR dbo.NumeroDeViaje</c> de la columna, y EF lo recupera por
    /// <c>OUTPUT</c> después del <c>INSERT</c>. Declararlo <c>required int</c> obligaría al código a
    /// asignarlo, con lo que EF mandaría el <c>0</c> del constructor en el <c>INSERT</c> y el default
    /// de la base no se aplicaría nunca: el primer viaje saldría con número 0 (tasks §trampa 2).
    ///
    /// El <c>private set</c> cierra el otro extremo: ningún caso de uso puede escribirlo aunque quiera.
    /// </remarks>
    public int Numero { get; private set; }

    /// <summary>Todo viaje pertenece a exactamente un cliente, que tiene que estar activo (FR-012).</summary>
    public required int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    /// <summary>
    /// Fecha del servicio. Admite pasado —carga retroactiva— y futuro —viaje planificado—, sin límite
    /// (FR-016). Es la fecha contra la que se evalúa toda la documentación (FR-024, SC-014) y la de
    /// corte de los totales (FR-046a).
    /// </summary>
    public required DateOnly Fecha { get; set; }

    /// <summary>Texto libre obligatorio, con <c>Trim</c> (FR-012).</summary>
    public required string Origen { get; set; }

    /// <summary>Texto libre obligatorio, con <c>Trim</c> (FR-012).</summary>
    public required string Destino { get; set; }

    /// <summary>
    /// Opcional. Cuando se carga, único entre los viajes <b>no anulados</b>: el remito de un viaje
    /// anulado vuelve a estar libre (FR-014).
    /// </summary>
    public string? NumeroRemito { get; set; }

    /// <summary>Opcional (FR-012).</summary>
    public string? DetalleCarga { get; set; }

    /// <summary>
    /// Pesos argentinos. <c>decimal</c> y nunca punto flotante: un total que alguien va a comparar
    /// contra una planilla no puede acumular error de representación. El cero es válido (FR-013).
    /// </summary>
    public decimal Importe { get; set; }

    /// <summary>Todo viaje nace <c>pendiente</c> (FR-032).</summary>
    public EstadoViaje Estado { get; set; } = EstadoViaje.Pendiente;

    /// <summary>Obligatorio al anular, <c>null</c> en cualquier otro estado (FR-036).</summary>
    public string? MotivoAnulacion { get; set; }

    /// <summary>
    /// Chofer asignado. Anulable porque la asignación no es obligatoria para el alta (FR-019), y
    /// <b>siempre acompañado de <see cref="VehiculoId"/></b>: no hay asignación parcial, un viaje
    /// tiene los dos o ninguno (FR-019b).
    /// </summary>
    public int? ChoferId { get; set; }

    public Chofer? Chofer { get; set; }

    public int? VehiculoId { get; set; }

    public Vehiculo? Vehiculo { get; set; }

    /// <summary>
    /// Transportista <b>del chofer al momento de asignarlo</b>. Se escribe al asignar y no se mueve
    /// sola: si después el chofer cambia de transportista, este viaje sigue perteneciendo al que lo
    /// hizo (FR-028, SC-010). Es una referencia, no una copia del nombre: corregirle la razón social
    /// al transportista sí se refleja acá.
    /// </summary>
    public int? TransportistaId { get; set; }

    public Transportista? Transportista { get; set; }

    /// <summary>
    /// La factura que incluye este viaje, o <c>null</c> mientras no esté facturado (Módulo 6, FR-053).
    ///
    /// <b>Es lo que garantiza que un viaje no entre en dos facturas</b>, y la garantía es estructural:
    /// una columna escalar no puede apuntar a dos facturas, así que no hay índice que agregue nada. Lo
    /// que queda por cerrar es la carrera entre dos operadores simultáneos, y eso lo cierra el
    /// <c>UPDATE</c> condicional con verificación de filas afectadas de <c>RepositorioFacturas</c>
    /// (Módulo 6, research §4).
    ///
    /// El listado y la ficha del Módulo 5 muestran el número y la fecha de la factura resolviéndolos
    /// por esta navegación, nunca por columnas copiadas al viaje (FR-055).
    /// </summary>
    public int? FacturaId { get; set; }

    public Facturacion.FacturaCliente? Factura { get; set; }

    /// <summary>Historial de cambios de estado, de la más vieja a la más nueva (FR-035).</summary>
    public ICollection<CambioDeEstadoViaje> CambiosDeEstado { get; } = [];

    /// <summary>
    /// Regla pura de FR-039: un viaje está demorado si lleva <b>más</b> de
    /// <see cref="DiasParaDemora"/> días corridos en curso.
    ///
    /// El instante llega por parámetro y no se lee del reloj acá adentro, que es lo que permite fijarlo
    /// en un test en vez de esperar cinco días (plan §Principio IV).
    /// </summary>
    /// <param name="enCursoDesde">
    /// Instante UTC en que el viaje pasó a <c>en curso</c>, tomado del historial. <c>null</c> si nunca
    /// arrancó, y entonces no hay demora posible.
    /// </param>
    public static bool EstaDemorado(DateTime? enCursoDesde, DateTime ahora) =>
        enCursoDesde is { } desde && ahora - desde > TimeSpan.FromDays(DiasParaDemora);
}
