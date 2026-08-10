using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioTiposDocumentacion(GtDbContext contexto) : IRepositorioTiposDocumentacion
{
    public async Task<List<TipoConDocumentos>> ConsultarAsync(
        bool soloActivos,
        DocumentacionAmbito? ambito,
        CancellationToken cancelacion)
    {
        var consulta = contexto.DocumentacionTipos.AsQueryable();

        if (soloActivos)
        {
            consulta = consulta.Where(tipo => tipo.Activo);
        }

        // Cada módulo ofrece únicamente los tipos de su ámbito (Módulo 4, FR-017a). Sin el
        // parámetro se devuelven los dos, que es lo que muestra la pantalla de mantenimiento.
        if (ambito is { } soloDeEsteAmbito)
        {
            consulta = consulta.Where(tipo => tipo.Ambito == soloDeEsteAmbito);
        }

        var filas = await consulta
            .OrderBy(tipo => tipo.Nombre)
            .ThenBy(tipo => tipo.Id)
            .Select(tipo => new
            {
                Tipo = tipo,
                // Las dos tablas, en la misma consulta (Módulo 4, FR-017b).
                Documentos = tipo.Documentos.Count +
                    contexto.DocumentacionesVehiculo.Count(documento =>
                        documento.DocumentacionTipoId == tipo.Id),
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return filas.Select(fila => new TipoConDocumentos(fila.Tipo, fila.Documentos)).ToList();
    }

    public async Task<TipoConDocumentos?> ObtenerConDocumentosAsync(int id, CancellationToken cancelacion)
    {
        var fila = await contexto.DocumentacionTipos
            .Where(tipo => tipo.Id == id)
            .Select(tipo => new
            {
                Tipo = tipo,
                Documentos = tipo.Documentos.Count +
                    contexto.DocumentacionesVehiculo.Count(documento =>
                        documento.DocumentacionTipoId == tipo.Id),
            })
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

    /// <summary>
    /// Cuenta los documentos de <b>las dos</b> tablas —choferes y vehículos— (Módulo 4, FR-017b).
    ///
    /// Es el único método del Módulo 3 cuyo resultado cambió con el Módulo 4, y cambia hacia el lado
    /// seguro: bloquea más bajas, nunca menos. Un tipo con documentos de vehículo ya no se puede dar
    /// de baja desde la pantalla de choferes, que es lo correcto.
    /// </summary>
    public async Task<int> ContarDocumentosAsync(int tipoId, CancellationToken cancelacion)
    {
        var deChoferes = await contexto.Documentaciones.CountAsync(
            documento => documento.DocumentacionTipoId == tipoId,
            cancelacion);

        var deVehiculos = await contexto.DocumentacionesVehiculo.CountAsync(
            documento => documento.DocumentacionTipoId == tipoId,
            cancelacion);

        return deChoferes + deVehiculos;
    }

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
