using System.Text.Json.Serialization;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;

namespace GT.Application.Pagos;

public record GenerarOrdenEntrada(Guid ClaveOperacion, int ProveedorId, DateOnly? Fecha, string? Detalle,
    int[] FacturaIds, MedioEntrada[] Medios, [property: JsonRequired] decimal ImporteTotalConfirmado, bool Confirmado);
public record AnularOrdenEntrada(Guid ClaveOperacion, string? Motivo, bool Confirmado);
public record BancoEntrada(string? Nombre, int Version = 0);
public record BancoAltaEntrada(string? Nombre);
public record BancoEdicionEntrada(string? Nombre, [property: JsonRequired] int Version);
public record EstadoChequeEntrada([property: JsonRequired] EstadoCheque EstadoAnterior, [property: JsonRequired] EstadoCheque EstadoNuevo);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(ChequeEntrada), "cheque")]
[JsonDerivedType(typeof(TransferenciaEntrada), "transferencia")]
[JsonDerivedType(typeof(EfectivoEntrada), "efectivo")]
public abstract record MedioEntrada(Guid RenglonId, decimal Importe);
public record ChequeEntrada(Guid RenglonId, decimal Importe, int BancoId, string? NumeroCheque,
    DateOnly? FechaEmision, DateOnly? FechaVencimiento, string? Detalle) : MedioEntrada(RenglonId, Importe);
public record TransferenciaEntrada(Guid RenglonId, decimal Importe, DateOnly? Fecha, string? CbuOrigen,
    string? CbuDestino, string? NumeroComprobante, string? Observaciones) : MedioEntrada(RenglonId, Importe);
public record EfectivoEntrada(Guid RenglonId, decimal Importe) : MedioEntrada(RenglonId, Importe);

public record BancoDto(int Id, string Nombre, bool Activo, int Version);
public record BancoReferencia(int Id, string Nombre, bool Activo);
public record ProveedorOpcion(int Id, string RazonSocial, string Cuit, TipoProveedor Tipo, bool Activo);
public record UsuarioPago(int Id, string Nombre);
public record ReferenciaPago(int Id, string Numero);
public record CajaReferencia(int Id, string Estado, decimal SaldoDisponible);
public record ReferenciasPago(DateOnly HoyArgentina, CajaReferencia? CajaPropia, IReadOnlyList<BancoReferencia> BancosActivos);
public record LiquidacionElegible(int Id, string Numero, decimal ImporteHistorico, decimal ImporteActual, decimal SaldoActual, string Estado);
public record FacturaElegible(int Id, string Numero, DateOnly Fecha, decimal ImporteTotal, int ProveedorId,
    TipoProveedor TipoProveedorAlRegistrar, LiquidacionElegible? Liquidacion);
public record SeleccionFacturas(IReadOnlyList<FacturaElegible> Items, IReadOnlyList<int> NoDisponibles, decimal ImporteTotal);
public record ProveedorEnOrden(int Id, string RazonSocial, string Cuit, TipoProveedor Tipo, bool ActivoAlGenerar, bool ActivoActual);
public record OrdenResumen(int Id, string Numero, DateOnly Fecha, ProveedorEnOrden Proveedor, decimal ImporteTotal, EstadoOrdenPagoProveedor Estado);
public record PagoLiquidacionDto(int Id, string Numero, DateOnly FechaPago, decimal Importe, bool Vigente, DateTime? RevertidaEn);
public record LiquidacionEnOrden(int Id, string Numero, decimal ImporteHistorico, decimal ImporteActual, decimal SaldoActual,
    string EstadoActual, PagoLiquidacionDto Pago);
public record FacturaEnOrden(int Id, string Numero, DateOnly Fecha, decimal ImporteTotalAlPagar, string EstadoActual,
    bool VinculoVigente, string ProveedorRazonSocial, string ProveedorCuit, LiquidacionEnOrden? Liquidacion);
public record MovimientoReferencia(int Id, int CajaId, string Tipo, decimal Importe, string Concepto, UsuarioPago Usuario,
    DateTime Fecha, ReferenciaPago OrdenPagoProveedor);
public record CajaEnOrden(int Id, MovimientoReferencia Egreso, MovimientoReferencia? IngresoCompensatorio);
public record EfectosAnulacion(int[] FacturasRestituidas, int[] ChequesAnulados, ReferenciaPago[] PagosRevertidos,
    MovimientoReferencia? MovimientoCompensatorio);
public record AnulacionDto(string Motivo, UsuarioPago Usuario, DateTime OcurridaEn, EfectosAnulacion Efectos);
public record DetalleOrden(int Id, string Numero, DateOnly Fecha, ProveedorEnOrden Proveedor, decimal ImporteTotal,
    EstadoOrdenPagoProveedor Estado, string? Detalle, UsuarioPago RegistradaPor, DateTime RegistradaEn,
    IReadOnlyList<FacturaEnOrden> Facturas, IReadOnlyList<MedioDetalle> Medios, CajaEnOrden? Caja, AnulacionDto? Anulacion);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(ChequeEnOrden), "cheque")]
[JsonDerivedType(typeof(TransferenciaEnOrden), "transferencia")]
[JsonDerivedType(typeof(EfectivoEnOrden), "efectivo")]
public abstract record MedioDetalle(int Id, Guid RenglonId, decimal Importe, DateOnly Fecha);
public record ChequeEnOrden(int Id, Guid RenglonId, decimal Importe, DateOnly Fecha, int BancoId, string NumeroCheque,
    DateOnly FechaVencimiento, string? Detalle, EstadoCheque Estado, string SituacionVencimiento, BancoReferencia Banco)
    : MedioDetalle(Id, RenglonId, Importe, Fecha)
{
    public DateOnly FechaEmision => Fecha;
}
public record TransferenciaEnOrden(int Id, Guid RenglonId, decimal Importe, DateOnly Fecha, string CbuOrigen,
    string CbuDestino, string NumeroComprobante, string? Observaciones) : MedioDetalle(Id, RenglonId, Importe, Fecha);
public record EfectivoEnOrden(int Id, Guid RenglonId, decimal Importe, DateOnly Fecha) : MedioDetalle(Id, RenglonId, Importe, Fecha);
public record CambioChequeDto(long Id, EstadoCheque EstadoAnterior, EstadoCheque EstadoNuevo, UsuarioPago Usuario, DateTime OcurridoEn);
public record ChequeResumen(int Id, string NumeroCheque, BancoReferencia Banco, DateOnly FechaEmision,
    DateOnly FechaVencimiento, decimal Importe, EstadoCheque Estado, string SituacionVencimiento, ReferenciaPago OrdenPagoProveedor);
public record DetalleCheque(int Id, string NumeroCheque, BancoReferencia Banco, DateOnly FechaEmision,
    DateOnly FechaVencimiento, decimal Importe, EstadoCheque Estado, string SituacionVencimiento,
    ReferenciaPago OrdenPagoProveedor, string? Detalle, IReadOnlyList<CambioChequeDto> Historial);
public record FiltroOrdenes(DateOnly? Desde, DateOnly? Hasta, int? ProveedorId, EstadoOrdenPagoProveedor? Estado, int Pagina = 1);
public record FiltroCheques(int? BancoId, EstadoCheque? Estado, DateOnly? DesdeVencimiento, DateOnly? HastaVencimiento, int Pagina = 1);

public record ErrorCampoPago(string Campo, string Mensaje);
public class ReglaPagoException(string codigo, string mensaje, int estadoHttp = 400, string? campo = null) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public int EstadoHttp { get; } = estadoHttp;
    public string? Campo { get; } = campo;
    public int[]? FacturasNoDisponibles { get; init; }
    public decimal? SaldoDisponible { get; init; }
    public decimal? ImporteEfectivo { get; init; }
}
/// <summary>Respuesta guardada, incluido el replay original; no se reconstruye con estado actual.</summary>
public record ResultadoEscrituraPago(int EstadoHttp, string Json, string? Location);
