using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

// ── Entradas ────────────────────────────────────────────────────────────────────────────────────

/// <summary>Lo que llega al registrar (FR-001 a FR-007). Todo anulable: la validación dice qué falta.</summary>
/// <param name="Tipo"><c>chofer</c> o <c>empleado</c>; cualquier otro valor es inválido.</param>
public record RegistroRequest(string? Tipo, int? PersonaId, DateOnly? Fecha, string? Motivo, decimal? Importe);

/// <summary>El diálogo es la confirmación explícita y manda <c>confirmado: true</c> (research §4).</summary>
public record RechazoRequest(string? Motivo, bool? Confirmado);

/// <summary>El diálogo es la confirmación explícita y manda <c>confirmado: true</c> (research §4).</summary>
public record AnulacionAdelantoRequest(string? Motivo, bool? Confirmado);

/// <param name="Estado">
/// <c>null</c> significa <b>todos, incluidos rechazados y anulados</b>. Opera sobre la columna, que es
/// exactamente lo que la fila muestra (FR-033).
/// </param>
public record FiltrosDeAdelantos(
    int? PersonaId = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    EstadoAdelanto? Estado = null,
    int Pagina = 1);

// ── Salidas ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>Una persona como la nombra el módulo: siempre del padrón vigente (FR-020).</summary>
/// <param name="Dni">Sólo dígitos.</param>
public record PersonaResumen(int Id, string Apellido, string Nombre, string Dni);

/// <param name="EmpresaEmisoraConfigurada">Sólo decide algo con tipo chofer (FR-002a).</param>
public record Beneficiarios(bool EmpresaEmisoraConfigurada, IReadOnlyList<PersonaResumen> Personas);

/// <summary>Fila del listado (FR-013).</summary>
/// <param name="Fecha"><c>yyyy-MM-dd</c>.</param>
public record AdelantoListado(
    int Id,
    string Fecha,
    PersonaResumen Persona,
    string Tipo,
    string Motivo,
    decimal Importe,
    string Estado);

/// <summary>
/// La forma de paginación de la convención [003] con <b>un campo más</b>. <c>PaginaDe&lt;T&gt;</c> no se toca.
/// </summary>
/// <param name="TotalAdelantado">
/// Suma de los <c>aprobado</c> entre <b>todos</b> los que cumplen los filtros, no sólo los de la página (FR-016).
/// </param>
public record PaginaDeAdelantos(
    IReadOnlyList<AdelantoListado> Items,
    int Total,
    int Pagina,
    int TamanioPagina,
    decimal TotalAdelantado);

/// <param name="Motivo">En <c>rechazo</c> y <c>anulacion</c>, leído de la columna del adelanto; <c>null</c> en las otras.</param>
public record EntradaDeHistorialAdelanto(string Operacion, string Usuario, DateTime OcurridoEn, string? Motivo);

/// <summary>Detalle completo (FR-019). Es también la respuesta de las cuatro escrituras, releída.</summary>
/// <param name="Tipo">El elegido al registrar; no se recalcula (FR-020).</param>
/// <param name="PuedeResolverse">Sólo por estado. Que el usuario tenga el permiso lo decide la sesión.</param>
public record AdelantoDetalle(
    int Id,
    string Fecha,
    PersonaResumen Persona,
    string Tipo,
    string Motivo,
    decimal Importe,
    string Estado,
    string? MotivoRechazo,
    string? MotivoAnulacion,
    IReadOnlyList<EntradaDeHistorialAdelanto> Historial,
    bool PuedeResolverse,
    bool PuedeAnularse);
