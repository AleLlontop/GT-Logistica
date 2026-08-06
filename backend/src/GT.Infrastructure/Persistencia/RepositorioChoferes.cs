using GT.Application.Choferes;
using GT.Application.Usuarios.Personas;
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

    public Task<bool> ExistePorCuilAsync(
        string cuil,
        int? idAExcluir = null,
        CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(
            chofer => chofer.Cuil == cuil && (idAExcluir == null || chofer.Id != idAExcluir),
            cancelacion);

    public Task<bool> ExistePorPersonaAsync(int personaId, CancellationToken cancelacion = default) =>
        contexto.Choferes.AnyAsync(chofer => chofer.PersonaId == personaId, cancelacion);

    public Task<Chofer?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Choferes
            .Include(chofer => chofer.Persona)
            .FirstOrDefaultAsync(chofer => chofer.Id == id, cancelacion);

    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (ViolaIndice(excepcion, "IX_Choferes_Cuil"))
        {
            throw new CuilDuplicadoException(excepcion);
        }
        catch (DbUpdateException excepcion) when (ViolaIndice(excepcion, "IX_Personas_Dni"))
        {
            throw new DniDuplicadoException(excepcion);
        }
    }

    public Task<Chofer?> ObtenerPorIdConRelacionesAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Choferes
            .Include(chofer => chofer.Persona)
            .Include(chofer => chofer.Transportista)
            .Include(chofer => chofer.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(chofer => chofer.Id == id, cancelacion);

    /// <summary>
    /// El listado con todo resuelto en la base (research §2, §8 y §9).
    ///
    /// Dos cosas viajan como subconsulta correlacionada y no como filas traídas a memoria:
    /// <list type="bullet">
    ///   <item><b>Cuál es el documento vigente de cada tipo</b>, expresado como "no existe otro del
    ///   mismo tipo que le gane por vencimiento, o por <c>Id</c> si empatan". EF lo traduce a un
    ///   <c>NOT EXISTS</c> que el índice <c>ChoferId, TipoId, FechaVencimiento DESC</c> resuelve
    ///   directo.</item>
    ///   <item><b>El estado de esos vigentes</b>, contado en tres números —cuántos hay, cuántos
    ///   vencidos y cuántos por vencer—. Con eso alcanza para los cuatro valores de FR-029 sin
    ///   traer un solo documento.</item>
    /// </list>
    ///
    /// El predicado del vigente va escrito a mano en cada conteo, y no extraído a un método, porque
    /// EF Core sólo traduce lo que ve en el árbol de expresión: una llamada a un método propio
    /// rompería la traducción y la consulta se evaluaría en memoria.
    /// </summary>
    public async Task<PaginaDe<ChoferListado>> ConsultarAsync(
        FiltrosDeChoferes filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Choferes.AsQueryable();

        // Sin filtro de estado se muestran sólo los activos (FR-022). No es lo mismo que "todos".
        consulta = filtros.SoloActivos is { } soloActivos
            ? consulta.Where(chofer => chofer.Activo == soloActivos)
            : consulta.Where(chofer => chofer.Activo);

        if (filtros.Apellido is { } apellido)
        {
            var patron = $"%{apellido}%";
            consulta = consulta.Where(chofer => EF.Functions.Like(chofer.Persona!.Apellido, patron));
        }

        if (filtros.Dni is { } dni)
        {
            var patron = $"%{dni}%";
            consulta = consulta.Where(chofer => EF.Functions.Like(chofer.Persona!.Dni, patron));
        }

        if (filtros.TransportistaId is { } transportistaId)
        {
            consulta = consulta.Where(chofer => chofer.TransportistaId == transportistaId);
        }

        var conEstado = consulta.Select(chofer => new
        {
            Chofer = chofer,

            Vigentes = chofer.Documentacion.Count(documento =>
                !chofer.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id)))),

            Vencidos = chofer.Documentacion.Count(documento =>
                !chofer.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))) &&
                documento.FechaVencimiento < hoy),

            PorVencer = chofer.Documentacion.Count(documento =>
                !chofer.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))) &&
                documento.FechaVencimiento >= hoy &&
                documento.FechaVencimiento <= hoy.AddDays(documento.Tipo!.DiasAvisoVencimiento)),
        });

        // El filtro por estado se aplica sobre el valor calculado, en la base (research §2).
        conEstado = filtros.EstadoDocumentacion switch
        {
            EstadoDocumentacionChofer.SinDocumentacion => conEstado.Where(fila => fila.Vigentes == 0),
            EstadoDocumentacionChofer.Vencida => conEstado.Where(fila => fila.Vencidos > 0),
            EstadoDocumentacionChofer.ProximaAvencer => conEstado.Where(fila =>
                fila.Vencidos == 0 && fila.PorVencer > 0),
            EstadoDocumentacionChofer.EnRegla => conEstado.Where(fila =>
                fila.Vigentes > 0 && fila.Vencidos == 0 && fila.PorVencer == 0),
            _ => conEstado,
        };

        // El total cuenta las coincidencias completas con los filtros, no las de esta página.
        var total = await conEstado.CountAsync(cancelacion);

        // Orden total: sin el Id final, dos homónimos pueden intercambiarse entre páginas y
        // aparecer duplicados o desaparecer (research §9).
        var filas = await conEstado
            .OrderBy(fila => fila.Chofer.Persona!.Apellido)
            .ThenBy(fila => fila.Chofer.Persona!.Nombre)
            .ThenBy(fila => fila.Chofer.Id)
            .Skip((filtros.Pagina - 1) * PaginaDe<ChoferListado>.TamanioPorDefecto)
            .Take(PaginaDe<ChoferListado>.TamanioPorDefecto)
            .Select(fila => new
            {
                fila.Chofer.Id,
                fila.Chofer.Persona!.Apellido,
                fila.Chofer.Persona.Nombre,
                fila.Chofer.Persona.Dni,
                TransportistaId = fila.Chofer.Transportista!.Id,
                TransportistaNombre = fila.Chofer.Transportista.Nombre,
                fila.Chofer.Activo,
                fila.Vigentes,
                fila.Vencidos,
                fila.PorVencer,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila => new ChoferListado(
                fila.Id,
                fila.Apellido,
                fila.Nombre,
                fila.Dni,
                new TransportistaResumen(fila.TransportistaId, fila.TransportistaNombre),
                fila.Activo,
                NombresDeEstado.DelChofer(EstadoDesde(fila.Vigentes, fila.Vencidos, fila.PorVencer))))
            .ToList();

        return new PaginaDe<ChoferListado>(
            items,
            total,
            filtros.Pagina,
            PaginaDe<ChoferListado>.TamanioPorDefecto);
    }

    /// <summary>
    /// Precedencia de FR-029: <c>vencida</c> &gt; <c>proximaAvencer</c> &gt; <c>enRegla</c>. Sin
    /// ningún documento vigente, <c>sinDocumentacion</c>, que no es lo mismo que estar en regla
    /// (FR-028).
    /// </summary>
    private static EstadoDocumentacionChofer EstadoDesde(int vigentes, int vencidos, int porVencer)
    {
        if (vigentes == 0) return EstadoDocumentacionChofer.SinDocumentacion;
        if (vencidos > 0) return EstadoDocumentacionChofer.Vencida;

        return porVencer > 0
            ? EstadoDocumentacionChofer.ProximaAvencer
            : EstadoDocumentacionChofer.EnRegla;
    }

    private static bool ViolaIndice(DbUpdateException excepcion, string indice) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(indice, StringComparison.Ordinal);
}
