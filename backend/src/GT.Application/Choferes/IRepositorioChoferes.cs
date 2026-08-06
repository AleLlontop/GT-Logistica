using GT.Domain.Choferes;
using GT.Domain.Personas;

namespace GT.Application.Choferes;

public interface IRepositorioChoferes
{
    /// <summary>
    /// Da de alta el chofer y, si la persona no estaba en el padrón, también la persona, en una
    /// <b>única</b> transacción.
    ///
    /// Que vayan juntas no es un detalle: si se guardara la persona por separado y el alta del
    /// chofer fallara después —una carrera contra el índice único del CUIL alcanza—, quedaría una
    /// persona suelta en el padrón del Módulo 2 que nadie pidió.
    /// </summary>
    /// <param name="personaNueva">
    /// La persona a crear, o <c>null</c> si se está reutilizando una que ya estaba en el padrón
    /// (FR-006). Cuando viene, el <c>PersonaId</c> del chofer lo resuelve la propia transacción.
    /// </param>
    Task CrearAsync(Chofer chofer, Persona? personaNueva, CancellationToken cancelacion = default);

    /// <param name="idAExcluir">
    /// Al modificar, el propio registro no cuenta como duplicado: conservar el propio CUIL tiene que
    /// poder guardarse (FR-007).
    /// </param>
    Task<bool> ExistePorCuilAsync(
        string cuil,
        int? idAExcluir = null,
        CancellationToken cancelacion = default);

    Task<bool> ExistePorPersonaAsync(int personaId, CancellationToken cancelacion = default);

    /// <summary>
    /// El chofer con su persona, <b>seguido por el contexto</b> para poder modificarlo. Distinto de
    /// <see cref="ObtenerPorIdConRelacionesAsync"/>, que trae todo para leer y no para escribir.
    /// </summary>
    Task<Chofer?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);

    Task<Chofer?> ObtenerPorIdConRelacionesAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// Página del listado con los filtros aplicados sobre todo el padrón antes de paginar (FR-030).
    ///
    /// El estado de la documentación y la elección del documento vigente de cada tipo se resuelven
    /// en SQL, no en memoria: es la única forma de poder filtrar por estado sin recorrer el padrón
    /// entero (research §2 y §8).
    /// </summary>
    /// <param name="hoy">Día en curso en Argentina, contra el que se calculan los estados (FR-017a).</param>
    Task<PaginaDe<ChoferListado>> ConsultarAsync(
        FiltrosDeChoferes filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default);
}

/// <summary>
/// Violación del índice único del CUIL detectada al guardar. Existe para no filtrar tipos de EF Core
/// ni de SqlClient hacia la capa de aplicación, igual que <c>DniDuplicadoException</c> del Módulo 2.
/// </summary>
public class CuilDuplicadoException(Exception interna)
    : Exception("El CUIL ya está registrado para otro chofer.", interna);

/// <summary>Violación del índice único de <c>PersonaId</c>: esa persona ya es chofer.</summary>
public class PersonaYaEsChoferException(Exception interna)
    : Exception("Esa persona ya está registrada como chofer.", interna);
