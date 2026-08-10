namespace GT.Domain.Flota;

/// <summary>
/// Estado operativo de un vehículo, con exactamente dos valores (FR-012). No hay estado intermedio:
/// una unidad parada por reparación se registra como <see cref="FueraDeServicio"/>.
///
/// <b>Es lo que eligió el operador</b>, no necesariamente lo que el listado muestra. El valor que se
/// muestra y por el que se filtra se <i>deriva</i> al consultar: si la documentación está vencida o
/// falta, la unidad figura fuera de servicio aunque tenga <see cref="Disponible"/> guardado (FR-014,
/// <c>CalculadorEstadoOperativo</c>).
///
/// La columna guardada no sobra por eso: distingue "parado porque está en el taller" de "parado
/// porque le venció el seguro". Sin ella, al renovar el seguro el sistema marcaría disponible un
/// camión roto (research §4).
/// </summary>
public enum VehiculoEstado : byte
{
    Disponible = 1,

    FueraDeServicio = 2,
}
