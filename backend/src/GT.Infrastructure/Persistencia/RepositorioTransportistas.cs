using GT.Application.Choferes.Transportistas;
using GT.Domain.Choferes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioTransportistas(GtDbContext contexto) : IRepositorioTransportistas
{
    public Task<bool> ExisteCuitAsync(string cuitNormalizado, int? idAExcluir, CancellationToken cancelacion)
    {
        var consulta = contexto.Transportistas.AsQueryable();

        if (idAExcluir.HasValue)
        {
            consulta = consulta.Where(t => t.Id != idAExcluir.Value);
        }

        return consulta.AnyAsync(t => t.Cuit == cuitNormalizado, cancelacion);
    }

    public Task AgregarAsync(Transportista transportista, CancellationToken cancelacion)
    {
        contexto.Transportistas.Add(transportista);
        return Task.CompletedTask;
    }

    public async Task GuardarCambiosAsync(CancellationToken cancelacion)
    {
        contexto.ChangeTracker.DetectChanges();
        // Sólo modificaciones de identidad/contacto. Alta y baja conservan sus reglas existentes.
        var modificados = contexto.ChangeTracker.Entries<Transportista>()
            .Where(e => e.State == EntityState.Modified &&
                new[] { nameof(Transportista.Cuit), nameof(Transportista.Nombre), nameof(Transportista.Telefono), nameof(Transportista.Email), nameof(Transportista.Tipo) }
                    .Any(nombre => e.Property(nombre).IsModified)).ToList();
        await using var transaccion = modificados.Count > 0
            ? await contexto.Database.BeginTransactionAsync(cancelacion) : null;
        if (modificados.Count > 0)
        {
            await new CoordinadorIdentidadProveedor(contexto).AdquirirAsync(async () =>
            {
                var ids = modificados.Select(e => e.Entity.Id).ToArray();
                var anteriores = await contexto.Transportistas.AsNoTracking().Where(t => ids.Contains(t.Id)).Select(t => t.Cuit).ToListAsync(cancelacion);
                return anteriores.Concat(modificados.Select(e => e.Entity.Cuit));
            }, cancelacion);
            foreach (var entrada in modificados.OrderBy(e => e.Entity.Id))
            {
                var transportista = entrada.Entity;
                await contexto.Transportistas.Where(t => t.Id == transportista.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Cuit, t => t.Cuit), cancelacion);
                var cuitActual = await contexto.Transportistas.AsNoTracking().Where(t => t.Id == transportista.Id)
                    .Select(t => t.Cuit).SingleAsync(cancelacion);
                // Cambiar sólo contacto conserva las reglas anteriores, incluso sin Fletero vinculado.
                if (cuitActual != transportista.Cuit && await contexto.Proveedores.AnyAsync(p => p.TransportistaId != transportista.Id &&
                    (p.Tipo == GT.Domain.Proveedores.TipoProveedor.Fletero ? p.Transportista!.Cuit : p.Cuit) == transportista.Cuit, cancelacion))
                    throw new CuitDuplicadoException(new InvalidOperationException("CUIT efectivo de otro proveedor."));
            }
        }
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
            if (transaccion is not null) await transaccion.CommitAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsCuitDuplicado(excepcion))
        {
            throw new CuitDuplicadoException(excepcion);
        }
    }

    public async Task<List<TransportistaConDependenciasActivas>> ConsultarAsync(
        string? textoBusqueda,
        string? cuitNormalizado,
        bool soloActivos,
        CancellationToken cancelacion)
    {
        var consulta = contexto.Transportistas.AsQueryable();

        if (soloActivos)
        {
            consulta = consulta.Where(t => t.Activo);
        }

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            var porNombre = $"%{textoBusqueda}%";
            var porCuit = $"%{cuitNormalizado ?? textoBusqueda}%";

            consulta = consulta.Where(t =>
                EF.Functions.Like(t.Nombre, porNombre) ||
                EF.Functions.Like(t.Cuit, porCuit));
        }

        // Las cantidades de dependientes activos se cuentan en la misma consulta (FR-010, FR-008d):
        // traen un número por fila en vez de las colecciones enteras.
        var filas = await consulta
            .OrderBy(t => t.Nombre)
            .ThenBy(t => t.Id)
            .Select(t => new
            {
                Transportista = t,
                ChoferesActivos = t.Choferes.Count(chofer => chofer.Activo),
                // Módulo 4, FR-008d: los vehículos activos viajan en la misma consulta que los
                // choferes. Dos números por fila, no dos colecciones traídas a memoria (research §8).
                VehiculosActivos = t.Vehiculos.Count(vehiculo => vehiculo.Activo),
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return filas
            .Select(fila => new TransportistaConDependenciasActivas(
                fila.Transportista,
                fila.ChoferesActivos,
                fila.VehiculosActivos))
            .ToList();
    }

    public async Task<TransportistaConDependenciasActivas?> ObtenerConDependenciasActivasAsync(
        int id,
        CancellationToken cancelacion)
    {
        var fila = await contexto.Transportistas
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Transportista = t,
                ChoferesActivos = t.Choferes.Count(chofer => chofer.Activo),
                // Módulo 4, FR-008d: los vehículos activos viajan en la misma consulta que los
                // choferes. Dos números por fila, no dos colecciones traídas a memoria (research §8).
                VehiculosActivos = t.Vehiculos.Count(vehiculo => vehiculo.Activo),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return fila is null
            ? null
            : new TransportistaConDependenciasActivas(
                fila.Transportista,
                fila.ChoferesActivos,
                fila.VehiculosActivos);
    }

    public Task<Transportista?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) =>
        contexto.Transportistas.FirstOrDefaultAsync(t => t.Id == id, cancelacion);

    private static bool EsCuitDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_Transportistas_Cuit", StringComparison.Ordinal);
}
