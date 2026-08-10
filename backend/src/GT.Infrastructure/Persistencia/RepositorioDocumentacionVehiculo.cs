using GT.Application.Flota.Documentacion;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioDocumentacionVehiculo(GtDbContext contexto) : IRepositorioDocumentacionVehiculo
{
    public Task<DocumentacionVehiculo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        contexto.DocumentacionesVehiculo
            .Include(documento => documento.Tipo)
            .FirstOrDefaultAsync(documento => documento.Id == id, cancelacion);

    public Task<List<DocumentacionVehiculo>> ConsultarDelVehiculoAsync(
        int vehiculoId,
        CancellationToken cancelacion = default) =>
        contexto.DocumentacionesVehiculo
            .Include(documento => documento.Tipo)
            .Where(documento => documento.VehiculoId == vehiculoId)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<bool> ExisteVehiculoAsync(int vehiculoId, CancellationToken cancelacion = default) =>
        contexto.Vehiculos.AnyAsync(vehiculo => vehiculo.Id == vehiculoId, cancelacion);

    /// <summary>
    /// Activo <b>y</b> de ámbito vehículo (FR-017a). Las dos condiciones van en la misma consulta:
    /// un tipo de chofer es, para este módulo, tan inexistente como uno que no está.
    /// </summary>
    public Task<DocumentacionTipo?> ObtenerTipoActivoDeVehiculoAsync(
        int tipoId,
        CancellationToken cancelacion = default) =>
        contexto.DocumentacionTipos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                tipo => tipo.Id == tipoId &&
                    tipo.Activo &&
                    tipo.Ambito == DocumentacionAmbito.Vehiculo,
                cancelacion);

    public Task AgregarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default)
    {
        contexto.DocumentacionesVehiculo.Add(documento);
        return Task.CompletedTask;
    }

    public Task EliminarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default)
    {
        contexto.DocumentacionesVehiculo.Remove(documento);
        return Task.CompletedTask;
    }

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);

    /// <summary>
    /// El predicado del vigente va escrito acá y no extraído a un método porque EF Core sólo traduce
    /// lo que ve en el árbol de expresión (convención [003], research §5).
    /// </summary>
    public Task<List<DocumentacionVehiculo>> ConsultarVigentesDeVehiculosActivosAsync(
        CancellationToken cancelacion = default) =>
        contexto.DocumentacionesVehiculo
            .Include(documento => documento.Tipo)
            .Include(documento => documento.Vehiculo!).ThenInclude(vehiculo => vehiculo.Transportista)
            // Una unidad dada de baja no alerta aunque tenga todo vencido: ya no forma parte de la
            // flota operativa y nadie va a renovar esos papeles (FR-035).
            .Where(documento => documento.Vehiculo!.Activo)
            // Sólo el vigente de cada tipo: un seguro ya renovado no alerta (FR-024).
            .Where(documento => !contexto.DocumentacionesVehiculo.Any(otro =>
                otro.VehiculoId == documento.VehiculoId &&
                otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                (otro.FechaVencimiento > documento.FechaVencimiento ||
                 (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))))
            .AsNoTracking()
            .ToListAsync(cancelacion);
}
