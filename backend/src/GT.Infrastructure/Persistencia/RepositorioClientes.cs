using GT.Application.Choferes;
using GT.Application.Viajes.Clientes;
using GT.Domain.Viajes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioClientes(GtDbContext contexto) : IRepositorioClientes
{
    private const string IndiceCuit = "IX_Clientes_Cuit";

    public Task AgregarAsync(Cliente cliente, CancellationToken cancelacion = default)
    {
        contexto.Clientes.Add(cliente);
        return Task.CompletedTask;
    }

    public async Task<PaginaDe<Cliente>> ConsultarAsync(
        FiltrosDeClientes filtros,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Clientes.AsQueryable();

        if (filtros.SoloActivos)
        {
            consulta = consulta.Where(cliente => cliente.Activo);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
        {
            // La colación explícita hace la comparación insensible a mayúsculas **y a acentos**, que
            // es lo que hace que `parana` encuentre `Paraná` (research §8).
            var patron = $"%{filtros.Busqueda.Trim()}%";

            consulta = consulta.Where(cliente => EF.Functions.Like(
                EF.Functions.Collate(cliente.RazonSocial, "Latin1_General_CI_AI"),
                patron));
        }

        // El total cuenta las coincidencias completas con los filtros, no las de esta página (FR-009).
        var total = await consulta.CountAsync(cancelacion);

        // Orden **total**, terminando en `Id`: sin eso, dos clientes homónimos se intercambian entre
        // páginas y uno aparece dos veces mientras el otro no aparece nunca (convención [003]).
        var items = await consulta
            .OrderBy(cliente => cliente.RazonSocial)
            .ThenBy(cliente => cliente.Id)
            .Skip((filtros.Pagina - 1) * PaginaDe<Cliente>.TamanioPorDefecto)
            .Take(PaginaDe<Cliente>.TamanioPorDefecto)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return new PaginaDe<Cliente>(
            items,
            total,
            filtros.Pagina,
            PaginaDe<Cliente>.TamanioPorDefecto);
    }

    public Task<Cliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Clientes.FirstOrDefaultAsync(cliente => cliente.Id == id, cancelacion);

    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(cliente => cliente.Id == id, cancelacion);

    public Task<Cliente?> ObtenerPorCuitAsync(
        string cuitNormalizado,
        int? idAExcluir = null,
        CancellationToken cancelacion = default) =>
        contexto.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cliente => cliente.Cuit == cuitNormalizado &&
                    (idAExcluir == null || cliente.Id != idAExcluir),
                cancelacion);

    /// <summary>
    /// El predicado es <c>Estado IN (Pendiente, EnCurso)</c>, el mismo criterio de "dependientes
    /// vivos" con que el Módulo 3 rechaza la baja de un transportista (FR-006).
    /// </summary>
    public Task<int> ContarViajesVivosAsync(int clienteId, CancellationToken cancelacion = default) =>
        contexto.Viajes.CountAsync(
            viaje => viaje.ClienteId == clienteId &&
                (viaje.Estado == EstadoViaje.Pendiente || viaje.Estado == EstadoViaje.EnCurso),
            cancelacion);

    /// <summary>
    /// Traduce la violación del índice único del CUIT a una excepción de la capa de aplicación
    /// (convención [003]): la consulta previa cierra la ventana normal y el índice cierra la carrera.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsCuitDuplicado(excepcion))
        {
            throw new CuitDeClienteDuplicadoException(excepcion);
        }
    }

    private static bool EsCuitDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains(IndiceCuit, StringComparison.Ordinal);
}
