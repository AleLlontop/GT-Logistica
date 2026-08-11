using GT.Application.Choferes;
using GT.Domain.Viajes;

namespace GT.Application.Viajes.Clientes;

/// <summary>Los cuatro filtros del listado de clientes (FR-009).</summary>
/// <param name="SoloActivos">
/// <c>true</c> devuelve sólo los activos, que son los únicos que el formulario de viaje ofrece
/// (FR-008). El endpoint lo declara <c>bool?</c> con <c>?? false</c> (convención [003]).
/// </param>
public record FiltrosDeClientes(bool SoloActivos = false, string? Busqueda = null, int Pagina = 1);

public interface IRepositorioClientes
{
    Task AgregarAsync(Cliente cliente, CancellationToken cancelacion = default);

    Task<PaginaDe<Cliente>> ConsultarAsync(
        FiltrosDeClientes filtros,
        CancellationToken cancelacion = default);

    /// <summary>Seguido por el contexto: lo devuelto se modifica y se guarda.</summary>
    Task<Cliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default);

    Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// El cliente dueño de ese CUIT, o <c>null</c> si está libre. <b>Incluye los dados de baja</b>: su
    /// CUIT sigue ocupado, y quien lo intente tiene que recibir un rechazo distinto (FR-003, FR-007).
    /// </summary>
    Task<Cliente?> ObtenerPorCuitAsync(
        string cuitNormalizado,
        int? idAExcluir = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Cuántos viajes <c>pendiente</c> o <c>en curso</c> tiene el cliente. Es lo único que impide la
    /// baja, y el mensaje de rechazo informa el número (FR-006, SC-009).
    ///
    /// Los rendidos y los anulados <b>no cuentan</b>: un cliente que dejó de operar con la empresa
    /// tiene historial por definición, y contarlo haría imposible justo el caso que US1 pide.
    /// </summary>
    Task<int> ContarViajesVivosAsync(int clienteId, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// La carrera por el CUIT que la consulta previa no alcanza a cerrar: dos altas simultáneas del mismo
/// CUIT pasan las dos la verificación y el índice único corta la segunda. El repositorio traduce esa
/// violación a esta excepción de la capa de aplicación (convención [003]).
/// </summary>
public class CuitDeClienteDuplicadoException(Exception interna)
    : Exception("El CUIT ya pertenece a otro cliente.", interna);
