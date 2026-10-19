using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Una instancia por operación; controla el orden, no abre ni confirma transacciones.</summary>
public class BloqueosPagos(GtDbContext db)
{
    private int nivel;
    private int ultimoId;
    private void Avanzar(int siguiente, int id = 0)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Falta transacción exterior de Pagos.");
        if (siguiente < nivel || siguiente == nivel && id < ultimoId)
            throw new InvalidOperationException("Se intentó invertir el orden global de locks de Pagos.");
        if (siguiente != nivel) ultimoId = 0;
        nivel = siguiente;
        ultimoId = id;
    }

    public async Task IdempotenciaAsync(TipoOperacionPago tipo, Guid clave, CancellationToken ct = default)
    {
        Avanzar(1);
        var recurso = $"pagos:{(byte)tipo}:{clave:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @resultado int;
            EXEC @resultado=sp_getapplock @Resource={recurso}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
            IF @resultado<0 THROW 50015, N'No se pudo tomar la clave idempotente.', 1;", ct);
    }

    public async Task<int> OrdenAsync(int id, CancellationToken ct = default)
    {
        Avanzar(2, id);
        return await db.OrdenesPagoProveedor.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, x => x.Estado), ct);
    }
    public async Task BancosAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct().Order())
        {
            Avanzar(3, id);
            if (await db.Bancos.Where(x => x.Id == id && x.Activo).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, x => x.Activo), ct) != 1)
                throw new ReglaPagoException("banco_no_disponible", "El banco no existe o dejó de estar Activo. Actualizá la selección.", 409, "bancoId");
        }
    }
    public async Task FacturasAsync(IEnumerable<int> ids, bool paraGenerar, CancellationToken ct = default)
    {
        var noDisponibles = new List<int>();
        foreach (var id in ids.Distinct().Order())
        {
            Avanzar(4, id);
            var consulta = db.FacturasProveedor.Where(x => x.Id == id);
            if (paraGenerar) consulta = consulta.Where(x => x.Estado == EstadoFacturaProveedor.Pendiente
                && !db.OrdenPagoProveedorFacturas.Any(v => v.FacturaProveedorId == x.Id && v.Vigente));
            if (await consulta.ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, x => x.Estado), ct) != 1) noDisponibles.Add(id);
        }
        if (noDisponibles.Count != 0) throw new ReglaPagoException("facturas_no_disponibles",
            "Una o más facturas ya fueron pagadas o dejaron de estar disponibles. Actualizá la selección.", 409)
            { FacturasNoDisponibles = noDisponibles.ToArray() };
    }
    public async Task LiquidacionesAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct().Order())
        {
            Avanzar(5, id);
            if (await new PrimitivasPagosLiquidacion(db).TomarAsync(id, ct) != 1)
                throw new ReglaPagoException("liquidacion_no_disponible", "Una liquidación dejó de estar disponible.", 409);
        }
    }
    public async Task PagosAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct().Order())
        {
            Avanzar(6, id);
            if (await db.OrdenesDePago.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Vigente, x => x.Vigente), ct) != 1)
                throw new ReglaPagoException("pago_no_disponible", "Un pago de liquidación dejó de estar disponible.", 409);
        }
    }
    public async Task ChequesAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        foreach (var id in ids.Distinct().Order())
        {
            Avanzar(7, id);
            if (await db.Cheques.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.EstadoCheque, x => x.EstadoCheque), ct) != 1)
                throw new ReglaPagoException("cheque_no_disponible", "El cheque dejó de estar disponible.", 409);
        }
    }
    public async Task CajaAsync(int id, int? usuarioId, CancellationToken ct = default)
    {
        Avanzar(8, id);
        if (await new PrimitivasMovimientosCaja(db).TomarAsync(id, usuarioId, ct) != 1)
            throw new ReglaPagoException("caja_no_disponible", "La caja original o propia ya no está abierta y disponible.", 409);
    }

    public static ReglaPagoException? Traducir(Exception error)
    {
        // EF puede envolver un deadlock en InvalidOperationException → DbUpdateException → SqlException.
        // Inspeccionar toda la cadena, sin habilitar reintentos que repitan economía fuera de idempotencia.
        SqlException? sql = null;
        for (Exception? actual = error; actual is not null; actual = actual.InnerException)
            if (actual is SqlException encontrada) { sql = encontrada; break; }
        if (sql is null) return null;
        if (sql.Number is 1205 or -2 or 50015)
            return new("operacion_transitoria", "La operación no pudo completarse. Reintentá la misma intención y clave.", 503);
        if (sql.Number is not (2601 or 2627)) return null;
        if (sql.Message.Contains("IX_OPPFactura_Vigente", StringComparison.Ordinal))
            return new("facturas_no_disponibles", "Una o más facturas ya fueron pagadas o dejaron de estar disponibles. Actualizá la selección.", 409);
        if (sql.Message.Contains("IX_Cheques_Banco_Numero", StringComparison.Ordinal))
            return new("numero_cheque_duplicado", "Ese número de cheque ya está registrado para el banco.", 400, "numeroCheque");
        if (sql.Message.Contains("IX_Bancos_Nombre", StringComparison.Ordinal))
            return new("nombre_banco_duplicado", "Ya existe un banco con ese nombre.", 400, "nombre");
        return null;
    }
}
