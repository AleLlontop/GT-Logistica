using System.Linq.Expressions;
using GT.Application.Adelantos;
using GT.Application.Choferes;
using GT.Domain.Adelantos;
using GT.Domain.Personas;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Persistencia de los adelantos.
///
/// <b>Las tres transiciones empiezan con un <c>UPDATE</c> condicional sobre la fila del adelanto</b> y
/// verifican que haya afectado una: bajo el aislamiento por defecto, la segunda transacción se bloquea
/// sobre la fila y, al desbloquearse, reevalúa el <c>WHERE</c> contra el dato ya confirmado (research §2).
/// Sin edición no hace falta <c>Version</c>: cada operación mueve el estado, así que la condición sobre el
/// estado distingue las cuatro carreras. <c>ExecuteUpdateAsync</c> no pasa por el rastreador, así que los
/// casos de uso releen el detalle al terminar (research §9.5).
/// </summary>
public class RepositorioAdelantos(GtDbContext contexto) : IRepositorioAdelantos
{
    /// <summary>
    /// La proyección que ve la regla de elegibilidad, <b>una sola vez</b> para las dos consultas que la
    /// usan: el desplegable y el guardado no pueden leer a la persona distinto (research §1). Es una
    /// expresión y no un método, así que EF la traduce entera.
    /// </summary>
    private static readonly Expression<Func<Persona, PersonaParaAdelanto>> ProyeccionParaAdelanto =
        persona => new PersonaParaAdelanto(
            persona.Id,
            persona.Apellido,
            persona.Nombre,
            persona.Dni,
            persona.Activa,
            persona.Tipo,
            persona.Chofer != null,
            persona.Chofer != null && persona.Chofer.Activo,
            persona.Chofer != null ? persona.Chofer.Transportista!.Cuit : null);

    // ── Registro ────────────────────────────────────────────────────────────────────────────────

    public async Task<string?> ObtenerCuitEmpresaEmisoraAsync(CancellationToken cancelacion = default)
    {
        var cuit = await contexto.EmpresaEmisora
            .AsNoTracking()
            .Select(empresa => empresa.Cuit)
            .FirstOrDefaultAsync(cancelacion);

        return string.IsNullOrWhiteSpace(cuit) ? null : cuit;
    }

    public async Task<IReadOnlyList<PersonaParaAdelanto>> ConsultarPersonasActivasParaAdelantoAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Personas
            .Where(persona => persona.Activa)
            .OrderBy(persona => persona.Apellido)
            .ThenBy(persona => persona.Nombre)
            .ThenBy(persona => persona.Id)
            .Select(ProyeccionParaAdelanto)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<PersonaParaAdelanto?> ObtenerPersonaParaAdelantoAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Personas
            .Where(persona => persona.Id == id)
            .Select(ProyeccionParaAdelanto)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

    /// <summary>
    /// Un solo <c>SaveChanges</c> inserta el adelanto y su entrada <c>registro</c> dentro de la misma
    /// transacción: todo adelanto tiene al menos una entrada (spec §Relationships).
    ///
    /// <b>La elegibilidad no la cierra la base</b>, y se declara: una baja de la persona entre la validación
    /// y este <c>INSERT</c> deja un adelanto de alguien dado de baja un instante después, que es un estado que
    /// la spec ya admite (research §2).
    /// </summary>
    public async Task<int> RegistrarAsync(
        Adelanto adelanto,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        adelanto.Cambios.Add(new CambioDeAdelanto
        {
            AdelantoId = adelanto.Id,
            Operacion = OperacionDeAdelanto.Registro,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        contexto.Adelantos.Add(adelanto);
        await contexto.SaveChangesAsync(cancelacion);

        return adelanto.Id;
    }

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los filtros <b>antes</b> de contar, sumar y paginar, y el estado sobre la columna (FR-033). El total
    /// adelantado sale de la misma consulta filtrada: toda la selección y no la página (FR-016). EF envuelve
    /// el <c>SUM</c> en <c>COALESCE</c>, así que sin aprobados da <c>0</c> y no falla (research §9.3).
    /// </summary>
    public async Task<PaginaDeAdelantos> ConsultarAsync(
        FiltrosDeAdelantos filtros,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Adelantos.AsQueryable();

        if (filtros.PersonaId is { } personaId)
        {
            consulta = consulta.Where(adelanto => adelanto.PersonaId == personaId);
        }

        // Los dos extremos se incluyen, y cada uno se aplica solo si el otro falta (FR-014).
        if (filtros.Desde is { } desde)
        {
            consulta = consulta.Where(adelanto => adelanto.Fecha >= desde);
        }

        if (filtros.Hasta is { } hasta)
        {
            consulta = consulta.Where(adelanto => adelanto.Fecha <= hasta);
        }

        if (filtros.Estado is { } estado)
        {
            consulta = consulta.Where(adelanto => adelanto.Estado == estado);
        }

        var total = await consulta.CountAsync(cancelacion);

        var totalAdelantado = await consulta
            .Where(adelanto => adelanto.Estado == EstadoAdelanto.Aprobado)
            .SumAsync(adelanto => adelanto.Importe, cancelacion);

        var pagina = Math.Max(filtros.Pagina, 1);
        var tamanio = PaginaDe<AdelantoListado>.TamanioPorDefecto;

        // `Fecha DESC, Id DESC` es orden total: el `Id` desempata a igual fecha (FR-017, convención [003]).
        var filas = await consulta
            .OrderByDescending(adelanto => adelanto.Fecha)
            .ThenByDescending(adelanto => adelanto.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(adelanto => new
            {
                adelanto.Id,
                adelanto.Fecha,
                adelanto.PersonaId,
                adelanto.Persona!.Apellido,
                adelanto.Persona.Nombre,
                adelanto.Persona.Dni,
                adelanto.TipoBeneficiario,
                adelanto.Motivo,
                adelanto.Importe,
                adelanto.Estado,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila => new AdelantoListado(
                fila.Id,
                fila.Fecha.ToString("yyyy-MM-dd"),
                new PersonaResumen(fila.PersonaId, fila.Apellido, fila.Nombre, fila.Dni),
                NombresDeEstadoAdelanto.EnJson(fila.TipoBeneficiario),
                fila.Motivo,
                fila.Importe,
                NombresDeEstadoAdelanto.EnJson(fila.Estado)))
            .ToList();

        return new PaginaDeAdelantos(items, total, pagina, tamanio, totalAdelantado);
    }

    public async Task<IReadOnlyList<PersonaResumen>> ConsultarPersonasConAdelantosAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Personas
            .Where(persona => contexto.Adelantos.Any(adelanto => adelanto.PersonaId == persona.Id))
            .OrderBy(persona => persona.Apellido)
            .ThenBy(persona => persona.Nombre)
            .ThenBy(persona => persona.Id)
            .Select(persona => new PersonaResumen(persona.Id, persona.Apellido, persona.Nombre, persona.Dni))
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<Adelanto?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Adelantos
            .Include(adelanto => adelanto.Persona)
            .Include(adelanto => adelanto.Cambios).ThenInclude(cambio => cambio.Usuario)
            .AsNoTracking()
            .FirstOrDefaultAsync(adelanto => adelanto.Id == id, cancelacion);

    // ── Transiciones (data-model §Aprobar, §Rechazar, §Anular) ──────────────────────────────────

    public async Task<bool> AprobarAsync(
        int id,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        var afectadas = await contexto.Adelantos
            .Where(adelanto => adelanto.Id == id && adelanto.Estado == EstadoAdelanto.Pendiente)
            .ExecuteUpdateAsync(
                cambio => cambio.SetProperty(adelanto => adelanto.Estado, EstadoAdelanto.Aprobado),
                cancelacion);

        return await RegistrarTransicionAsync(
            transaccion, afectadas, id, OperacionDeAdelanto.Aprobacion, usuarioId, ocurridoEn, cancelacion);
    }

    public async Task<bool> RechazarAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        var afectadas = await contexto.Adelantos
            .Where(adelanto => adelanto.Id == id && adelanto.Estado == EstadoAdelanto.Pendiente)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(adelanto => adelanto.Estado, EstadoAdelanto.Rechazado)
                    .SetProperty(adelanto => adelanto.MotivoRechazo, motivo),
                cancelacion);

        return await RegistrarTransicionAsync(
            transaccion, afectadas, id, OperacionDeAdelanto.Rechazo, usuarioId, ocurridoEn, cancelacion);
    }

    public async Task<bool> AnularAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        var afectadas = await contexto.Adelantos
            .Where(adelanto => adelanto.Id == id && adelanto.Estado == EstadoAdelanto.Aprobado)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(adelanto => adelanto.Estado, EstadoAdelanto.Anulado)
                    .SetProperty(adelanto => adelanto.MotivoAnulacion, motivo),
                cancelacion);

        return await RegistrarTransicionAsync(
            transaccion, afectadas, id, OperacionDeAdelanto.Anulacion, usuarioId, ocurridoEn, cancelacion);
    }

    /// <summary>
    /// La mitad común de las tres transiciones: <b>una fila afectada o nada</b>. La entrada del historial se
    /// inserta recién después de verificar el <c>UPDATE</c>, dentro de la misma transacción.
    /// </summary>
    private async Task<bool> RegistrarTransicionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion,
        int afectadas,
        int id,
        OperacionDeAdelanto operacion,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion)
    {
        if (afectadas != 1)
        {
            await transaccion.RollbackAsync(cancelacion);

            return false;
        }

        contexto.CambiosDeAdelanto.Add(new CambioDeAdelanto
        {
            AdelantoId = id,
            Operacion = operacion,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return true;
    }
}
