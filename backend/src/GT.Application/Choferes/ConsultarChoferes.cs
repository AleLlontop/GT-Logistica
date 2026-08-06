using GT.Domain.Choferes;

namespace GT.Application.Choferes;

/// <summary>
/// Filtros del listado (FR-022). Los cinco se combinan con "y".
/// </summary>
/// <param name="Estado">
/// <c>null</c> significa <b>sólo activos</b>, no "todos": el listado responde por la operación del
/// día y quien quiera ver los dados de baja pide <c>inactivo</c> explícitamente (FR-022).
/// </param>
public record FiltrosDeChoferes(
    string? Apellido = null,
    string? Dni = null,
    int? TransportistaId = null,
    bool? SoloActivos = null,
    EstadoDocumentacionChofer? EstadoDocumentacion = null,
    int Pagina = 1);

public record ChoferListado(
    int Id,
    string Apellido,
    string Nombre,
    string Dni,
    TransportistaResumen Transportista,
    bool Activo,
    string EstadoDocumentacion);

/// <summary>
/// Listado paginado de choferes con el estado de su documentación (FR-022, FR-030).
///
/// El estado no está guardado y el documento vigente de cada tipo tampoco: los dos se resuelven
/// <b>dentro de la consulta SQL</b> (research §2 y §8). Es lo que permite filtrar por estado sin
/// traer todo el padrón a memoria, que era el riesgo de haber elegido calcularlo al leer.
/// </summary>
public class ConsultarChoferes(IRepositorioChoferes repositorio)
{
    public Task<PaginaDe<ChoferListado>> EjecutarAsync(
        FiltrosDeChoferes filtros,
        CancellationToken cancelacion = default)
    {
        var apellido = Limpiar(filtros.Apellido);
        var dni = filtros.Dni is null ? null : NormalizadorDocumentoNumerico.Normalizar(filtros.Dni);
        if (string.IsNullOrEmpty(dni)) dni = null;

        // Una página fuera de rango no es un error: devuelve items vacío con el total real.
        var pagina = filtros.Pagina < 1 ? 1 : filtros.Pagina;

        return repositorio.ConsultarAsync(
            filtros with { Apellido = apellido, Dni = dni, Pagina = pagina },
            FechaHoyArgentina.Hoy(),
            cancelacion);
    }

    private static string? Limpiar(string? texto)
    {
        var limpio = texto?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio;
    }
}

/// <summary>
/// Ficha de un chofer con toda su documentación, vigente e histórica (FR-024).
/// </summary>
public class ConsultarFichaChofer(IRepositorioChoferes repositorio)
{
    public async Task<ChoferDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var chofer = await repositorio.ObtenerPorIdConRelacionesAsync(id, cancelacion);

        return chofer is null ? null : ChoferDetalle.Desde(chofer, FechaHoyArgentina.Hoy());
    }
}
