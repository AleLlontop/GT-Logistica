using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Flota;
using GT.Application.Facturacion;

namespace GT.Application.Reportes.Analisis;

public record DistribucionesLiquidaciones(DistribucionCantidad Estado, DistribucionImporte Transportista);
public record FilaLiquidacionAnalisis(int Id, string Numero, string Periodo, ReferenciaAnalisis Transportista,
    string Estado, decimal ImporteTotal, decimal PagadoVigente, decimal? PendienteVigente);
public record AnalisisLiquidaciones(ContextoAnalisis Contexto, ResumenLiquidaciones Resumen, IReadOnlyList<MesLiquidaciones> Series,
    DistribucionesLiquidaciones Distribuciones, PaginaDe<FilaLiquidacionAnalisis> Detalle);

public record FilaPagoAnalisis(int Id, string Numero, int LiquidacionId, string LiquidacionNumero, string PeriodoLiquidado,
    ReferenciaAnalisis Transportista, DateOnly FechaPago, decimal Importe, bool Vigente, string Origen,
    int? OppId, string? OppNumero, DateTime? RevertidaEn, ReferenciaAnalisis? RevertidaPor);
public record DistribucionesPagos(DistribucionImporte Transportista);
public record AnalisisPagos(ContextoAnalisis Contexto, ResumenPagos Resumen, IReadOnlyList<MesPagos> Series,
    DistribucionesPagos Distribuciones, PaginaDe<FilaPagoAnalisis> Detalle);

public record FilaMovimientoAnalisis(int Id, int CajaId, string CajaNombre, DateTime Fecha, string Tipo, decimal Importe,
    string Concepto, ReferenciaAnalisis Responsable, string? Referencia, int? OrdenDePagoId, int? OrdenPagoProveedorId);
public record DistribucionesCaja(DistribucionImporte Tipo);
public record AnalisisCaja(ContextoAnalisis Contexto, ResumenCajaAnalisis Resumen, IReadOnlyList<MesCajaAnalisis> Series,
    DistribucionesCaja Distribuciones, PaginaDe<FilaMovimientoAnalisis> Detalle);

public record FilaFlotaAnalisis(int Id, string Patente, ReferenciaAnalisis Tipo, ReferenciaAnalisis Transportista,
    bool Activo, string EstadoOperativoGuardado, string DisponibilidadActual, long CantidadViajes);
public record DistribucionesFlota(DistribucionCantidad Tipo, DistribucionCantidad Transportista);
public record AnalisisFlota(ContextoAnalisis Contexto, ResumenFlota Resumen, IReadOnlyList<MesFlota> Series,
    DistribucionesFlota Distribuciones, PaginaDe<FilaFlotaAnalisis> Detalle);

// Las alertas conservan el contrato original del panel; no se copian sus reglas al módulo analítico.
public record AnalisisVencimientosChoferes(ContextoAnalisis Contexto, PaginaDe<AlertaVencimiento> Detalle);
public record AnalisisVencimientosFlota(ContextoAnalisis Contexto, PaginaDe<AlertaVencimientoFlota> Detalle);
public record AnalisisVencimientosFacturas(ContextoAnalisis Contexto, decimal ImporteTotal, PaginaDe<FilaDeVencimiento> Detalle);
public record GruposCantidadAnalisis(ContextoAnalisis Contexto, PaginaDe<GrupoCantidad> Detalle);
public record GruposImporteAnalisis(ContextoAnalisis Contexto, PaginaDe<GrupoImporte> Detalle);
public record ReferenciasAnalisis(PaginaDe<ReferenciaAnalisis> Detalle);
