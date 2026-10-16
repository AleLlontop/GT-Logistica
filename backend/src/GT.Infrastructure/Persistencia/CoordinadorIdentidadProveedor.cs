using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Protocolo único de 013: global → CUIT normalizados ordenados → filas.</summary>
public class CoordinadorIdentidadProveedor(GtDbContext contexto)
{
    public async Task AdquirirAsync(Func<Task<IEnumerable<string>>> resolverCuits, CancellationToken cancelacion = default)
    {
        if (contexto.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La coordinación de identidad requiere una transacción activa.");
        await TomarAsync("GT.Proveedores.Identidad", cancelacion);
        // Resolver recién dentro del lock global: el origen puede haber cambiado mientras esperamos.
        var cuits = (await resolverCuits()).Select(NormalizadorDocumentoNumerico.Normalizar)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var cuit in cuits)
        {
            if (!ValidadorCuit.EsValido(cuit))
                throw new ArgumentException("El CUIT de coordinación debe ser válido y estar normalizado.");
            await TomarAsync($"GT.Proveedores.Cuit:{cuit}", cancelacion);
        }
    }

    private Task<int> TomarAsync(string recurso, CancellationToken cancelacion) =>
        contexto.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @resultado int;
            EXEC @resultado = sys.sp_getapplock @Resource={recurso}, @LockMode=N'Exclusive',
                @LockOwner=N'Transaction', @LockTimeout=10000, @DbPrincipal=N'public';
            IF @resultado < 0 THROW 51013, N'No se pudo coordinar la identidad del proveedor. Volvé a intentar.', 1;
            """, cancelacion);
}
