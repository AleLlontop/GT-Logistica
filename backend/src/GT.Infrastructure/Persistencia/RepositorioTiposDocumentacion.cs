using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioTiposDocumentacion(GtDbContext contexto) : IRepositorioTiposDocumentacion
{
    public async Task<List<TipoConDocumentos>> ConsultarAsync(
        bool soloActivos,
        CancellationToken cancelacion)
    {
        var consulta = contexto.DocumentacionTipos.AsQueryable();

        if (soloActivos)
        {
            consulta = consulta.Where(tipo => tipo.Activo);
        }

        var filas = await consulta
            .OrderBy(tipo => tipo.Nombre)
            .ThenBy(tipo => tipo.Id)
            .Select(tipo => new { Tipo = tipo, Documentos = tipo.Documentos.Count })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return filas.Select(fila => new TipoConDocumentos(fila.Tipo, fila.Documentos)).ToList();
    }

    public async Task<TipoConDocumentos?> ObtenerConDocumentosAsync(int id, CancellationToken cancelacion)
    {
        var fila = await contexto.DocumentacionTipos
            .Where(tipo => tipo.Id == id)
            .Select(tipo => new { Tipo = tipo, Documentos = tipo.Documentos.Count })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return fila is null ? null : new TipoConDocumentos(fila.Tipo, fila.Documentos);
    }

    public Task<DocumentacionTipo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) =>
        contexto.DocumentacionTipos.FirstOrDefaultAsync(tipo => tipo.Id == id, cancelacion);

    public Task<bool> ExisteNombreAsync(string nombre, int? idAExcluir, CancellationToken cancelacion) =>
        contexto.DocumentacionTipos.AnyAsync(
            tipo => tipo.Nombre == nombre && (idAExcluir == null || tipo.Id != idAExcluir),
            cancelacion);

    public Task<int> ContarDocumentosAsync(int tipoId, CancellationToken cancelacion) =>
        contexto.Documentaciones.CountAsync(
            documento => documento.DocumentacionTipoId == tipoId,
            cancelacion);

    public Task AgregarAsync(DocumentacionTipo tipo, CancellationToken cancelacion)
    {
        contexto.DocumentacionTipos.Add(tipo);
        return Task.CompletedTask;
    }

    public async Task GuardarCambiosAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsNombreDuplicado(excepcion))
        {
            throw new NombreDeTipoDuplicadoException(excepcion);
        }
    }

    private static bool EsNombreDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_DocumentacionTipos_Nombre", StringComparison.Ordinal);
}
