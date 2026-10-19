using GT.Domain.Liquidaciones;

namespace GT.Domain.Pagos;

/// <summary>Proyección de entrada, no entidad. Estado null representa liquidación ausente.</summary>
/// <param name="ProveedorActivo">Informativo: la baja no impide cancelar deuda elegible (AC-13).</param>
public record FacturaFleteroParaPago(
    bool FacturaPendiente,
    EstadoLiquidacion? EstadoLiquidacion,
    bool TienePagoVigenteIncompatible,
    decimal TotalLiquidacionActual,
    decimal ImporteHistoricoLiquidacion,
    decimal SaldoLiquidacionActual,
    decimal TotalFactura,
    bool ProveedorActivo);

public enum MotivoFacturaFleteroNoElegible
{
    FacturaNoPendiente,
    LiquidacionInexistente,
    LiquidacionNoPendiente,
    PagoVigenteIncompatible,
    TotalModificado,
    SaldoDistinto,
}

public static class ElegibilidadFacturaFletero
{
    /// <summary>AC-03: null cuando puede pagarse; no consulta DB, reloj ni condición Activo.</summary>
    public static MotivoFacturaFleteroNoElegible? Evaluar(FacturaFleteroParaPago factura) =>
        factura switch
        {
            { FacturaPendiente: false } => MotivoFacturaFleteroNoElegible.FacturaNoPendiente,
            { EstadoLiquidacion: null } => MotivoFacturaFleteroNoElegible.LiquidacionInexistente,
            { EstadoLiquidacion: not EstadoLiquidacion.Pendiente } => MotivoFacturaFleteroNoElegible.LiquidacionNoPendiente,
            { TienePagoVigenteIncompatible: true } => MotivoFacturaFleteroNoElegible.PagoVigenteIncompatible,
            _ when factura.TotalLiquidacionActual != factura.ImporteHistoricoLiquidacion => MotivoFacturaFleteroNoElegible.TotalModificado,
            _ when factura.SaldoLiquidacionActual != factura.TotalFactura => MotivoFacturaFleteroNoElegible.SaldoDistinto,
            _ => null,
        };
}
