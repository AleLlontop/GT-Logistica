namespace GT.Domain.Adelantos;

/// <summary>
/// Qué registra cada entrada del historial (FR-036). Cada una ocurre a lo sumo una vez por adelanto:
/// lo garantiza <c>IX_CambiosDeAdelanto_Operacion</c>, sin filtro y sin valores escritos a mano.
/// </summary>
public enum OperacionDeAdelanto : byte
{
    Registro = 0,
    Aprobacion = 1,
    Rechazo = 2,
    Anulacion = 3,
}
