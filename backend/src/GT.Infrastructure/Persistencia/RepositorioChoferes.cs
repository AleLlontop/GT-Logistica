using GT.Application.Choferes;
using GT.Domain.Choferes;
using GT.Domain.Personas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioChoferes(GtDbContext contexto) : IRepositorioChoferes
{
    public async Task CrearAsync(
        Chofer chofer,
        Persona? personaNueva,
        CancellationToken cancelacion = default)
    {
        if (personaNueva is not null)
        {
            contexto.Personas.Add(personaNueva);

            // Se ata por la navegación y no por el Id: la persona todavía no lo tiene, y es EF quien
            // lo resuelve al escribir las dos filas en la misma transacción.
            chofer.Persona = personaNueva;
        }

        contexto.Choferes.Add(chofer);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (ViolaIndice(excepcion, "IX_Choferes_Cuil"))
        {
            throw new CuilDuplicadoException(excepcion);
        }
        catch (DbUpdateException excepcion) when (ViolaIndice(excepcion, "IX_Choferes_PersonaId"))
        {
            throw new PersonaYaEsChoferException(excepcion);
        }
    }

    public Task<bool> ExistePorCuilAsync(string cuil, CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(chofer => chofer.Cuil == cuil, cancelacion);

    public Task<bool> ExistePorPersonaAsync(int personaId, CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(chofer => chofer.PersonaId == personaId, cancelacion);

    public Task<Chofer?> ObtenerPorIdConRelacionesAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Choferes
            .Include(chofer => chofer.Persona)
            .Include(chofer => chofer.Transportista)
            .Include(chofer => chofer.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(chofer => chofer.Id == id, cancelacion);

    private static bool ViolaIndice(DbUpdateException excepcion, string indice) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(indice, StringComparison.Ordinal);
}
