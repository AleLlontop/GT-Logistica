using GT.Application.Choferes;

namespace GT.Application.Pagos;

/// <summary>Un contexto scoped y transacciones externas únicas para Generar/Anular.</summary>
public interface IRepositorioPagos
{
    Task<ReferenciasPago> ReferenciasAsync(int usuarioId, DateOnly hoy, CancellationToken ct = default);
    Task<PaginaDe<ProveedorOpcion>> ProveedoresElegiblesAsync(string? q, int pagina, CancellationToken ct = default);
    Task<PaginaDe<FacturaElegible>> FacturasElegiblesAsync(int proveedorId, int pagina, CancellationToken ct = default);
    Task<SeleccionFacturas> SeleccionAsync(int[] ids, CancellationToken ct = default);
    Task<ResultadoEscrituraPago> GenerarAsync(GenerarOrdenEntrada entrada, int usuarioId, bool puedeConsultar, DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default);
    Task<ResultadoEscrituraPago> AnularAsync(int id, AnularOrdenEntrada entrada, int usuarioId, bool puedeConsultar, DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default);
    Task<PaginaDe<OrdenResumen>> OrdenesAsync(FiltroOrdenes filtro, CancellationToken ct = default);
    Task<PaginaDe<ProveedorOpcion>> ProveedoresHistoricosAsync(string? q, int pagina, CancellationToken ct = default);
    Task<DetalleOrden?> OrdenAsync(int id, DateOnly hoy, CancellationToken ct = default);
    Task<PaginaDe<ChequeResumen>> ChequesAsync(FiltroCheques filtro, DateOnly hoy, CancellationToken ct = default);
    Task<IReadOnlyList<BancoReferencia>> BancosHistoricosAsync(CancellationToken ct = default);
    Task<DetalleCheque?> ChequeAsync(int id, DateOnly hoy, CancellationToken ct = default);
    Task<DetalleCheque?> CambiarEstadoAsync(int id, EstadoChequeEntrada entrada, int usuarioId, DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default);
    Task<PaginaDe<BancoDto>> BancosAsync(int pagina, CancellationToken ct = default);
    Task<BancoDto?> BancoAsync(int id, CancellationToken ct = default);
    Task<BancoDto> CrearBancoAsync(BancoEntrada entrada, CancellationToken ct = default);
    Task<BancoDto?> EditarBancoAsync(int id, BancoEntrada entrada, CancellationToken ct = default);
    Task<BancoDto?> ActivarBancoAsync(int id, bool activo, CancellationToken ct = default);
}
