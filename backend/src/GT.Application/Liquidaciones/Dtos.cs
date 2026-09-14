using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

// ── Entradas ────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lo que llega al generar (FR-011). <b>No lleva importes</b>, y es el requisito: el total lo calcula el
/// servidor con los viajes que encuentra en la base, así que no hay forma de mandarlo (FR-007).
/// </summary>
public record GeneracionRequest(int? TransportistaId, int? Mes, int? Anio, IReadOnlyList<int>? ViajeIds);

/// <summary>
/// Lo que llega al editar: el <b>conjunto final</b> de viajes y la <c>version</c> con que se abrió la
/// edición (FR-048, research §10). Tampoco lleva importes.
/// </summary>
public record EdicionRequest(IReadOnlyList<int>? ViajeIds, int? Version);

/// <summary>El diálogo es la confirmación explícita y manda <c>confirmado: true</c> (research §7).</summary>
public record AnulacionRequest(string? Motivo, bool? Confirmado);

/// <summary>Sin <c>confirmado</c> responde <c>409 pago_requiere_confirmacion</c> y no registra nada.</summary>
public record OrdenDePagoRequest(DateOnly? FechaPago, decimal? Importe, bool? Confirmado);

/// <param name="Estado">
/// <c>null</c> significa <b>todas, incluidas las anuladas</b>. Opera sobre la columna, que es exactamente
/// lo que la fila muestra (FR-029).
/// </param>
public record FiltrosDeLiquidaciones(
    int? TransportistaId = null,
    int? Mes = null,
    int? Anio = null,
    EstadoLiquidacion? Estado = null,
    int Pagina = 1);

// ── Armado ──────────────────────────────────────────────────────────────────────────────────────

/// <param name="Cuit">Once dígitos: el formato con guiones lo pone <c>compartido/cuit</c>.</param>
/// <param name="Activo">Del padrón: <c>false</c> se muestra con la palabra <c>Inactivo</c>.</param>
public record TransportistaResumen(int Id, string RazonSocial, string Cuit, bool Activo);

/// <param name="EmpresaEmisoraConfigurada">
/// <c>false</c> con la lista vacía: la generación muestra el bloqueo con su salida (FR-001a).
/// </param>
public record TransportistasLiquidables(
    bool EmpresaEmisoraConfigurada,
    IReadOnlyList<TransportistaResumen> Transportistas);

/// <param name="Estado"><c>rendido</c> o <c>facturado</c>: los dos se liquidan (spec §Clarifications).</param>
public record ViajeDisponible(
    int Id,
    int Numero,
    string Fecha,
    string Origen,
    string Destino,
    string Estado,
    decimal Importe);

// ── Consulta ────────────────────────────────────────────────────────────────────────────────────

/// <summary>Fila del listado (FR-021).</summary>
/// <param name="Numero">Armado: <c>LQ-12</c> (research §4).</param>
/// <param name="RestaPagar"><c>null</c> en una anulada: no se debe nada (FR-027).</param>
public record LiquidacionListado(
    int Id,
    string Numero,
    int Mes,
    int Anio,
    TransportistaResumen Transportista,
    decimal ImporteTotal,
    decimal? RestaPagar,
    string Estado,
    string? MotivoAnulacion);

public record ViajeLiquidado(int Id, int Numero, string Fecha, string Origen, string Destino, decimal Importe);

public record OrdenDePagoDetalle(
    int Id,
    string Numero,
    string FechaPago,
    decimal Importe,
    string RegistradaPor,
    DateTime RegistradaEn);

/// <param name="ViajesQuitados">Números de viaje. Vacío salvo en una <c>edicion</c> (FR-033).</param>
public record EntradaDeHistorialLiquidacion(
    string Operacion,
    string Usuario,
    DateTime OcurridoEn,
    IReadOnlyList<int> ViajesQuitados,
    IReadOnlyList<int> ViajesAgregados);

/// <summary>Detalle completo (FR-025). Es también la respuesta de las cuatro escrituras, releída.</summary>
/// <param name="FechaGeneracion">Fecha de Argentina del instante de la entrada <c>generacion</c>.</param>
/// <param name="Version">La edición la manda de vuelta al guardar (FR-048).</param>
/// <param name="Viajes">Los vigentes, o los que agrupaba si está anulada (FR-028).</param>
/// <param name="PuedeEditarse">
/// Por estado, pagos y transportista. Que el usuario tenga el permiso lo decide la sesión.
/// </param>
public record LiquidacionDetalle(
    int Id,
    string Numero,
    int Mes,
    int Anio,
    string FechaGeneracion,
    TransportistaResumen Transportista,
    string Estado,
    string? MotivoAnulacion,
    decimal ImporteTotal,
    decimal ImportePagado,
    decimal? RestaPagar,
    int Version,
    IReadOnlyList<ViajeLiquidado> Viajes,
    IReadOnlyList<OrdenDePagoDetalle> OrdenesDePago,
    IReadOnlyList<EntradaDeHistorialLiquidacion> Historial,
    bool PuedeEditarse,
    bool PuedeAnularse,
    bool PuedeRegistrarPago);
