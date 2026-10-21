namespace GT.Application.Reportes.Analisis;

public record ResumenLiquidaciones(long Cantidad, decimal ImporteLiquidado, decimal PagadoVigenteDocumental, decimal PendienteVigenteDocumental, long CantidadAnuladas);
public record MesLiquidaciones(string Periodo, decimal ImporteLiquidado, decimal PagadoVigenteDocumental, decimal PendienteVigenteDocumental);
public record ResumenPagos(long CantidadPagosVigentes, decimal ImportePagadoVigente, long CantidadRevertidos, decimal ImporteRevertidoHistorico);
public record MesPagos(string Periodo, decimal ImportePagadoVigente);
public record ResumenCajaAnalisis(long Cantidad, decimal Ingresos, decimal Egresos, decimal Neto);
public record MesCajaAnalisis(string Periodo, decimal Ingresos, decimal Egresos, decimal Neto);
public record ResumenFlota(long CantidadUnidades, long TotalViajesConVehiculo, long SinVehiculoEnPeriodo);
public record MesFlota(string Periodo, long CantidadViajes);
public record ResumenVencimientos(long Cantidad);
public record ResumenVencimientosFacturas(long Cantidad, decimal ImporteTotal);

public record DashboardLiquidaciones(ResumenLiquidaciones Documentos, IReadOnlyList<MesLiquidaciones> SerieDocumentos,
    ResumenPagos Pagos, IReadOnlyList<MesPagos> SeriePagos, DistribucionImporte Transportista);
public record DashboardCaja(ResumenCajaAnalisis Resumen, IReadOnlyList<MesCajaAnalisis> Series);
public record DashboardFlota(ResumenFlota Resumen, IReadOnlyList<MesFlota> Series, DistribucionCantidad Tipo, DistribucionCantidad Ranking);
public record DashboardVencimientos(ContextoAnalisis Contexto, ResumenVencimientos Resumen);
public record DashboardVencimientosFacturas(ContextoAnalisis Contexto, ResumenVencimientosFacturas Resumen);
public record SeccionesDashboard(
    SeccionAnalisis<DashboardViajes> Viajes,
    SeccionAnalisis<DashboardFacturacion> Facturacion,
    SeccionAnalisis<DashboardLiquidaciones> Liquidaciones,
    SeccionAnalisis<DashboardCaja> Caja,
    SeccionAnalisis<DashboardFlota> Flota,
    SeccionAnalisis<DashboardVencimientos> VencimientosChoferes,
    SeccionAnalisis<DashboardVencimientos> VencimientosFlota,
    SeccionAnalisis<DashboardVencimientosFacturas> VencimientosFacturas);
public record DashboardAnalisis(ContextoAnalisis Contexto, SeccionesDashboard Secciones);
