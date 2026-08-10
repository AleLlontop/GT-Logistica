using GT.Domain.Flota;

namespace GT.Application.Flota.TiposVehiculo;

/// <summary>Un tipo con la cantidad de vehículos que lo usan, que es lo que impide su baja (FR-010).</summary>
public record TipoConVehiculos(TipoVehiculo Tipo, int Vehiculos);

public interface IRepositorioTiposVehiculo
{
    /// <summary>
    /// El catálogo, con cuántos vehículos usa cada tipo. Puede venir vacío: arranca así y no se
    /// precarga por migración (US1 esc. 1).
    /// </summary>
    Task<List<TipoConVehiculos>> ConsultarAsync(bool soloActivos, CancellationToken cancelacion);

    Task<TipoConVehiculos?> ObtenerConVehiculosAsync(int id, CancellationToken cancelacion);

    Task<TipoVehiculo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion);

    /// <param name="idAExcluir">
    /// Al modificar, el propio registro no cuenta como duplicado: conservar el propio nombre tiene
    /// que poder guardarse (FR-009).
    /// </param>
    Task<bool> ExisteNombreAsync(string nombre, int? idAExcluir, CancellationToken cancelacion);

    Task AgregarAsync(TipoVehiculo tipo, CancellationToken cancelacion);

    Task GuardarCambiosAsync(CancellationToken cancelacion);
}

/// <summary>
/// Violación del índice único del nombre detectada al guardar. Existe para no filtrar tipos de EF
/// Core ni de SqlClient hacia la capa de aplicación, igual que en el Módulo 3.
///
/// La consulta previa cierra la ventana normal y el índice cierra la carrera entre dos altas
/// simultáneas (convención [003]).
/// </summary>
public class NombreDeTipoVehiculoDuplicadoException(Exception interna)
    : Exception("Ya existe un tipo de vehículo con ese nombre.", interna);
