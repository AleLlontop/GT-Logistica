using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Persistencia del módulo. <b>Lee</b> <c>Personas</c>, <c>Choferes</c>, <c>Transportistas</c> y
/// <c>EmpresaEmisora</c> sin escribirles nada (data-model §Consultas).
///
/// Los instantes llegan por parámetro y ninguna consulta lee el reloj (convención [005]).
/// </summary>
public interface IRepositorioAdelantos
{
    // ── Registro ────────────────────────────────────────────────────────────────────────────────

    /// <summary>El CUIT de la empresa emisora, o <c>null</c> si todavía no se configuró (FR-002a).</summary>
    Task<string?> ObtenerCuitEmpresaEmisoraAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Las personas activas con lo que la regla de elegibilidad necesita, por apellido, nombre e <c>Id</c>.
    /// <b>Sin filtrar por tipo</b>: lo filtra <see cref="ElegibilidadDeBeneficiario"/> en memoria (research §1).
    /// </summary>
    Task<IReadOnlyList<PersonaParaAdelanto>> ConsultarPersonasActivasParaAdelantoAsync(
        CancellationToken cancelacion = default);

    /// <summary>
    /// La <b>misma</b> proyección, por <c>Id</c> y sin la condición de activa: así la regla puede decir
    /// <c>inactiva</c> y no <c>inexistente</c>.
    /// </summary>
    Task<PersonaParaAdelanto?> ObtenerPersonaParaAdelantoAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Inserta el adelanto pendiente y la entrada <c>registro</c>, todo o nada. Devuelve el <c>Id</c>.</summary>
    Task<int> RegistrarAsync(
        Adelanto adelanto,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los cuatro filtros aplicados <b>antes</b> de contar, sumar y paginar (FR-014, FR-016, FR-017). El rango
    /// ya llega validado.
    /// </summary>
    Task<PaginaDeAdelantos> ConsultarAsync(FiltrosDeAdelantos filtros, CancellationToken cancelacion = default);

    /// <summary>Las personas con al menos un adelanto, activas o no, de cualquier tipo (FR-014).</summary>
    Task<IReadOnlyList<PersonaResumen>> ConsultarPersonasConAdelantosAsync(CancellationToken cancelacion = default);

    /// <summary>El adelanto con su persona y su historial con el usuario, sin rastrear.</summary>
    Task<Adelanto?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default);

    // ── Las tres transiciones (data-model §Transacciones) ───────────────────────────────────────

    /// <summary>
    /// <c>UPDATE</c> condicional sobre <c>Estado = pendiente</c> y la entrada <c>aprobacion</c>. <c>false</c>
    /// si no afectó ninguna fila: no se tocó nada.
    /// </summary>
    Task<bool> AprobarAsync(int id, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default);

    /// <summary>Igual que <see cref="AprobarAsync"/>, fijando además el motivo del rechazo.</summary>
    Task<bool> RechazarAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary><c>UPDATE</c> condicional sobre <c>Estado = aprobado</c>, con el motivo, y la entrada <c>anulacion</c>.</summary>
    Task<bool> AnularAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);
}
