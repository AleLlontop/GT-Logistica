namespace GT.Application.Caja;

/// <summary>El desplegable del ingreso: sólo las facturas pendientes de cobro, filtradas en SQL (research §5).</summary>
public class ConsultarFacturasPendientes(IRepositorioCaja cajas)
{
    public Task<IReadOnlyList<OpcionDeReferencia>> EjecutarAsync(CancellationToken cancelacion = default) =>
        cajas.ConsultarFacturasPendientesAsync(cancelacion);
}
