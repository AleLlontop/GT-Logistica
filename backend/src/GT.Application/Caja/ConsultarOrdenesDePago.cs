namespace GT.Application.Caja;

/// <summary>
/// El desplegable del egreso: las 50 órdenes de pago más recientes, sin filtro de estado porque el Módulo 9
/// no les da ninguno (research §4, FR-011).
/// </summary>
public class ConsultarOrdenesDePago(IRepositorioCaja cajas)
{
    public Task<IReadOnlyList<OpcionDeReferencia>> EjecutarAsync(CancellationToken cancelacion = default) =>
        cajas.ConsultarOrdenesDePagoAsync(cancelacion);
}
