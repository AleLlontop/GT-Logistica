using GT.Application.Usuarios.Personas;
using GT.Domain.Personas;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioPersonas(GtDbContext contexto) : IRepositorioPersonas
{
    public async Task<IReadOnlyList<Persona>> BuscarAsync(
        string? texto,
        bool soloActivas,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Personas.AsNoTracking().AsQueryable();

        if (soloActivas)
        {
            consulta = consulta.Where(persona => persona.Activa);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            // Coincidencia parcial en cualquier posición, sin distinguir mayúsculas. Se compara
            // sobre el texto ya pasado a minúsculas en vez de confiar en la *collation* del
            // servidor, por el mismo motivo que el listado de usuarios (research §4).
            var buscado = texto.Trim().ToLowerInvariant();

            consulta = consulta.Where(persona =>
                persona.Nombre.ToLower().Contains(buscado) ||
                persona.Apellido.ToLower().Contains(buscado) ||
                persona.Dni.Contains(buscado));
        }

        return await consulta
            .OrderBy(persona => persona.Apellido)
            .ThenBy(persona => persona.Nombre)
            .ToListAsync(cancelacion);
    }

    public Task<Persona?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Personas.FirstOrDefaultAsync(persona => persona.Id == id, cancelacion);

    public Task<Persona?> ObtenerPorDniAsync(string dni, CancellationToken cancelacion = default) =>
        contexto.Personas.FirstOrDefaultAsync(persona => persona.Dni == dni, cancelacion);

    public Task<bool> EstaDisponibleAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Personas.AnyAsync(persona => persona.Id == id && persona.Activa, cancelacion);

    public Task<bool> ExisteDniAsync(
        string dni,
        int? excluyendoPersonaId = null,
        CancellationToken cancelacion = default) =>
        contexto.Personas.AnyAsync(
            persona => persona.Dni == dni &&
                       (excluyendoPersonaId == null || persona.Id != excluyendoPersonaId),
            cancelacion);

    public async Task AgregarAsync(Persona persona, CancellationToken cancelacion = default)
    {
        await contexto.Personas.AddAsync(persona, cancelacion);
    }

    /// <summary>
    /// Traduce la violación del índice único del DNI a una excepción de la capa de aplicación, para
    /// no filtrar tipos de EF Core ni de SqlClient hacia arriba (FR-027, research §3).
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsViolacionDelIndiceDeDni(excepcion))
        {
            throw new DniDuplicadoException(excepcion);
        }
    }

    private static bool EsViolacionDelIndiceDeDni(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_Personas_Dni", StringComparison.Ordinal);

    /// <summary>
    /// No filtra por estado del usuario a propósito: una persona sigue ocupada aunque el usuario que
    /// la tiene esté `inactivo` o `bloqueado` (FR-008, FR-028).
    /// </summary>
    public Task<string?> UsernameDelUsuarioVinculadoAsync(
        int personaId,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Where(usuario => usuario.PersonaId == personaId)
            .Select(usuario => (string?)usuario.Username)
            .FirstOrDefaultAsync(cancelacion);

    public Task<bool> EsChoferAsync(int personaId, CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(chofer => chofer.PersonaId == personaId, cancelacion);
}
