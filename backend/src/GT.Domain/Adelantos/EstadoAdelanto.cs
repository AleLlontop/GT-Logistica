namespace GT.Domain.Adelantos;

/// <summary>
/// Estado del adelanto: cuatro valores excluyentes (FR-033).
///
/// Todo adelanto nace <see cref="Pendiente"/> (FR-010). Las únicas transiciones son a
/// <see cref="Aprobado"/> o <see cref="Rechazado"/> desde pendiente, y a <see cref="Anulado"/> desde
/// aprobado (FR-034). Rechazado y anulado son finales.
///
/// <b>⚠ Los números importan y no son un detalle de serialización.</b> <c>CK_Adelantos_Estado</c> lleva
/// <c>0</c> a <c>3</c> escritos a mano: reordenar este enum <b>no falla al compilar</b> y deja al
/// <c>CHECK</c> protegiendo el estado equivocado. Lo cubre <c>RestriccionesDeAdelantoTests</c>
/// (research §9.1).
/// </summary>
public enum EstadoAdelanto : byte
{
    Pendiente = 0,

    /// <summary>Firme, no pagado: el sistema no registra la entrega del dinero (FR-023).</summary>
    Aprobado = 1,

    /// <summary>Con <c>MotivoRechazo</c>, y la base lo exige.</summary>
    Rechazado = 2,

    /// <summary>Con <c>MotivoAnulacion</c>, y la base lo exige.</summary>
    Anulado = 3,
}
