using GT.Domain.Personas;

namespace GT.Domain.Adelantos;

/// <summary>
/// Dinero que G&amp;T Logística le entrega a un chofer o empleado a cuenta de su sueldo (FR-001 a FR-037).
/// Entidad principal del Módulo 10.
///
/// <b>No se borra ni se modifica</b> (FR-035): persona, tipo, fecha, motivo e importe quedan como se
/// registraron. Sólo cambia de estado, y lo hace con un <c>UPDATE</c> condicional del repositorio que
/// no pasa por esta clase (research §2). Por eso todo es <c>init</c>.
///
/// <b>La fecha de registro no es columna</b>: es el instante de la entrada <c>registro</c> del historial.
/// </summary>
public class Adelanto
{
    public int Id { get; init; }

    /// <summary>
    /// Elegible <b>al registrar</b> (FR-009). Si después se da de baja, el adelanto no cambia. Sin
    /// colección inversa en <see cref="Persona"/>: el Módulo 2 no se toca.
    /// </summary>
    public required int PersonaId { get; init; }

    public Persona? Persona { get; init; }

    /// <summary>El elegido al registrar, validado contra el padrón y congelado (FR-010, FR-020).</summary>
    public required TipoBeneficiario TipoBeneficiario { get; init; }

    /// <summary>
    /// El día en que se otorgó, entre el primer día del mes anterior y hoy (FR-005). Sin <c>CHECK</c>:
    /// el piso se mueve con el calendario.
    /// </summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>Recortado antes de guardar. Hasta 200 caracteres.</summary>
    public required string Motivo { get; init; }

    /// <summary>Mayor que cero y con a lo sumo dos decimales (FR-007). <c>decimal</c>, nunca flotante.</summary>
    public required decimal Importe { get; init; }

    /// <summary>
    /// Nace pendiente (FR-010). La columna no tiene <c>DEFAULT</c>: EF manda siempre este valor.
    /// </summary>
    public EstadoAdelanto Estado { get; init; } = EstadoAdelanto.Pendiente;

    /// <summary>Con texto exactamente cuando está rechazado (FR-024). La base lo exige.</summary>
    public string? MotivoRechazo { get; init; }

    /// <summary>Con texto exactamente cuando está anulado (FR-031). La base lo exige.</summary>
    public string? MotivoAnulacion { get; init; }

    /// <summary>Historial de FR-036, de la entrada más vieja a la más nueva. Nunca vacío.</summary>
    public ICollection<CambioDeAdelanto> Cambios { get; } = [];
}
