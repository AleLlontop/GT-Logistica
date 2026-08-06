using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;

using DocumentoDeChofer = GT.Domain.Choferes.Documentacion;

namespace GT.Infrastructure.Persistencia;

public class RepositorioDocumentacion(GtDbContext contexto) : IRepositorioDocumentacion
{
    public Task<DocumentoDeChofer?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Documentaciones
            .Include(documento => documento.Tipo)
            .FirstOrDefaultAsync(documento => documento.Id == id, cancelacion);

    public Task<List<DocumentoDeChofer>> ConsultarDelChoferAsync(
        int choferId,
        CancellationToken cancelacion = default) =>
        contexto.Documentaciones
            .Include(documento => documento.Tipo)
            .Where(documento => documento.ChoferId == choferId)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<bool> ExisteChoferAsync(int choferId, CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(chofer => chofer.Id == choferId, cancelacion);

    public Task<DocumentacionTipo?> ObtenerTipoActivoAsync(
        int tipoId,
        CancellationToken cancelacion = default) =>
        contexto.DocumentacionTipos
            .AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.Id == tipoId && tipo.Activo, cancelacion);

    public Task AgregarAsync(DocumentoDeChofer documento, CancellationToken cancelacion = default)
    {
        contexto.Documentaciones.Add(documento);
        return Task.CompletedTask;
    }

    /// <summary>Borrado físico: el documento no lleva baja lógica (FR-015d).</summary>
    public Task EliminarAsync(DocumentoDeChofer documento, CancellationToken cancelacion = default)
    {
        contexto.Documentaciones.Remove(documento);
        return Task.CompletedTask;
    }

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);

    /// <summary>
    /// El predicado del vigente va escrito acá y no extraído a un método porque EF Core sólo traduce
    /// lo que ve en el árbol de expresión (mismo criterio que el listado de choferes, research §8).
    /// </summary>
    public Task<List<DocumentoDeChofer>> ConsultarVigentesDeChoferesActivosAsync(
        CancellationToken cancelacion = default) =>
        contexto.Documentaciones
            .Include(documento => documento.Tipo)
            .Include(documento => documento.Chofer!).ThenInclude(chofer => chofer.Persona)
            .Include(documento => documento.Chofer!).ThenInclude(chofer => chofer.Transportista)
            // Un chofer dado de baja no alerta aunque tenga todo vencido (FR-021).
            .Where(documento => documento.Chofer!.Activo)
            // Sólo el vigente de cada tipo: una licencia ya renovada no alerta (FR-020a).
            .Where(documento => !contexto.Documentaciones.Any(otro =>
                otro.ChoferId == documento.ChoferId &&
                otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                (otro.FechaVencimiento > documento.FechaVencimiento ||
                 (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))))
            .AsNoTracking()
            .ToListAsync(cancelacion);
}
