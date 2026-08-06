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

    Task<bool> ExistePorCuilAsync(string cuil, CancellationToken cancelacion = default);

    Task<bool> ExistePorPersonaAsync(int personaId, CancellationToken cancelacion = default);

    Task<Chofer?> ObtenerPorIdConRelacionesAsync(int id, CancellationToken cancelacion = default);
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
