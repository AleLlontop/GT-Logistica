using GT.Application.Pagos;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.Infrastructure.Persistencia;

/// <summary>El caller toma primero el applock y conserva la transacción exterior hasta el commit económico.</summary>
public class ResultadosIdempotentesPago(GtDbContext db)
{
    public async Task<ResultadoEscrituraPago?> BuscarAsync(TipoOperacionPago operacion, Guid clave,
        ComandoCanonicoPago comando, int usuarioId, bool puedeConsultar, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Falta la transacción idempotente.");
        var resultado = await db.ResultadosOperacionPago.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Operacion == operacion && x.Clave == clave, ct);
        if (resultado is null) return null;
        // Autorización antes de cualquier comparación que revele contenido o una OPP ajena.
        if (resultado.UsuarioId != usuarioId && !puedeConsultar)
            throw new ReglaPagoException("sin_autorizacion", "No tenés autorización para acceder a este recurso.", 403);
        if (resultado.VersionCanonica != comando.Version || !resultado.Hash.AsSpan().SequenceEqual(comando.Hash)
            || !string.Equals(resultado.ContenidoCanonico, comando.Contenido, StringComparison.Ordinal))
            throw new ReglaPagoException("clave_reutilizada", "La clave de operación ya fue utilizada con otros datos.", 409);
        return new(resultado.EstadoHttp, resultado.ResultadoJson,
            resultado.EstadoHttp == 201 ? $"/api/pagos/ordenes/{resultado.OrdenPagoProveedorId}" : null);
    }

    public async Task GuardarAsync(TipoOperacionPago operacion, Guid clave, ComandoCanonicoPago comando,
        int usuarioId, int oppId, ResultadoEscrituraPago respuesta, DateTime ahoraUtc, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Falta la transacción económica.");
        db.ResultadosOperacionPago.Add(new ResultadoOperacionPago { UsuarioId = usuarioId, Operacion = operacion,
            Clave = clave, VersionCanonica = comando.Version, ContenidoCanonico = comando.Contenido,
            Hash = comando.Hash.ToArray(), OrdenPagoProveedorId = oppId, EstadoHttp = checked((short)respuesta.EstadoHttp),
            ResultadoJson = respuesta.Json, CompletadaEn = ahoraUtc });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Después de disponer la transacción original incierta; lectura independiente, jamás repite el pago.</summary>
    public static async Task<ResultadoEscrituraPago> RecuperarAsync(IServiceScopeFactory scopes,
        TipoOperacionPago operacion, Guid clave, ComandoCanonicoPago comando, int usuarioId,
        bool puedeConsultar, CancellationToken ct = default)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var nuevo = scope.ServiceProvider.GetRequiredService<GtDbContext>();
            await using var tx = await nuevo.Database.BeginTransactionAsync(ct);
            await new BloqueosPagos(nuevo).IdempotenciaAsync(operacion, clave, ct);
            var respuesta = await new ResultadosIdempotentesPago(nuevo).BuscarAsync(operacion, clave, comando, usuarioId, puedeConsultar, ct);
            // Transacción sólo de lectura/lock: no contiene efectos económicos para confirmar.
            await tx.RollbackAsync(ct);
            return respuesta ?? throw Incierto();
        }
        catch (ReglaPagoException) { throw; }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw Incierto();
        }
    }
    private static ReglaPagoException Incierto() => new("resultado_no_determinable",
        "No se pudo determinar el resultado. Reintentá con la misma clave y los mismos datos.", 503);
}
