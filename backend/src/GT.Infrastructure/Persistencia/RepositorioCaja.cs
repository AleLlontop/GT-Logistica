using System.Linq.Expressions;
using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Facturacion;
using GT.Domain.Caja;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia.Configuraciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Persistencia de las cajas y sus movimientos, con las tres transacciones de data-model §Transacciones.
///
/// <b>Registrar un movimiento y cerrar empiezan con el mismo <c>UPDATE</c> que no cambia nada</b> —
/// <c>SET Estado = Estado WHERE Id = @id AND Estado = Abierta AND UsuarioResponsableId = @usuario</c>—, sólo
/// para tomar el lock de la fila de la caja antes de leer o insertar (research §2). Un cierre y un movimiento
/// simultáneos quedan así en uno de los dos órdenes válidos: nunca un movimiento fuera del resumen de una caja
/// cerrada. Sin edición no hace falta <c>Version</c> (convención [010]).
/// </summary>
public class RepositorioCaja(GtDbContext contexto) : IRepositorioCaja
{
    /// <summary>Lo que un listado de movimientos lee de SQL. La referencia se arma después, en memoria.</summary>
    private sealed record FilaDeMovimiento(
        int Id,
        int CajaId,
        DateTime Fecha,
        TipoMovimientoCaja Tipo,
        decimal Importe,
        string Concepto,
        int UsuarioId,
        string Usuario,
        string? FacturaNumero,
        string? FacturaCliente,
        int? OrdenDePagoNumero);

    /// <summary>
    /// La proyección de un movimiento, <b>una sola vez</b> para el listado de la caja, el resumen de cierre y la
    /// consulta global: las tres leen igual (convención [010]). Es una expresión y no un método, así que EF la
    /// traduce entera.
    /// </summary>
    private static readonly Expression<Func<MovimientoDeCaja, FilaDeMovimiento>> ProyeccionDeMovimiento =
        movimiento => new FilaDeMovimiento(
            movimiento.Id,
            movimiento.CajaId,
            movimiento.Fecha,
            movimiento.Tipo,
            movimiento.Importe,
            movimiento.Concepto,
            movimiento.UsuarioId,
            movimiento.Usuario!.Username,
            movimiento.Factura != null ? movimiento.Factura.NumeroComprobante : null,
            movimiento.Factura != null ? movimiento.Factura.ClienteRazonSocial : null,
            movimiento.OrdenDePago != null ? (int?)movimiento.OrdenDePago.Numero : null);

    /// <summary>El formato visible de la referencia vive acá y viaja armado (convención [009]).</summary>
    private static MovimientoListado ALista(FilaDeMovimiento fila) =>
        new(
            fila.Id,
            fila.CajaId,
            fila.Fecha,
            NombresDeEstadoCaja.EnJson(fila.Tipo),
            fila.Importe,
            fila.Concepto,
            new UsuarioResumen(fila.UsuarioId, fila.Usuario),
            fila.FacturaNumero is not null
                ? $"Factura {fila.FacturaNumero} · {fila.FacturaCliente}"
                : fila.OrdenDePagoNumero is { } numero
                    ? NumerosVisibles.OrdenDePago(numero)
                    : null);

    // ── Consulta de cajas ───────────────────────────────────────────────────────────────────────

    public Task<CajaDetalle?> ObtenerDetalleAsync(int id, int usuarioId, CancellationToken cancelacion = default) =>
        DetalleAsync(contexto.Cajas.Where(caja => caja.Id == id), usuarioId, cancelacion);

    public Task<CajaDetalle?> ObtenerCajaAbiertaDeAsync(int usuarioId, CancellationToken cancelacion = default) =>
        DetalleAsync(
            contexto.Cajas.Where(caja =>
                caja.UsuarioResponsableId == usuarioId && caja.Estado == EstadoCaja.Abierta),
            usuarioId,
            cancelacion);

    /// <summary>
    /// Los totales se suman en SQL, en la misma consulta. EF envuelve cada <c>SUM</c> en <c>COALESCE</c>, así
    /// que una caja sin movimientos da <c>0</c> y no falla.
    /// </summary>
    private async Task<CajaDetalle?> DetalleAsync(
        IQueryable<Caja> consulta,
        int usuarioId,
        CancellationToken cancelacion)
    {
        var fila = await consulta
            .Select(caja => new
            {
                caja.Id,
                caja.UsuarioResponsableId,
                caja.UsuarioResponsable!.Username,
                caja.FechaApertura,
                caja.Estado,
                caja.FechaCierre,
                caja.SaldoFinal,
                caja.SaldoInicial,
                Ingresos = caja.Movimientos
                    .Where(movimiento => movimiento.Tipo == TipoMovimientoCaja.Ingreso)
                    .Sum(movimiento => movimiento.Importe),
                Egresos = caja.Movimientos
                    .Where(movimiento => movimiento.Tipo == TipoMovimientoCaja.Egreso)
                    .Sum(movimiento => movimiento.Importe),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        if (fila is null)
        {
            return null;
        }

        return new CajaDetalle(
            fila.Id,
            new UsuarioResumen(fila.UsuarioResponsableId, fila.Username),
            fila.FechaApertura,
            NombresDeEstadoCaja.EnJson(fila.Estado),
            fila.FechaCierre,
            fila.SaldoFinal,
            fila.SaldoInicial,
            fila.Ingresos,
            fila.Egresos,
            ReglasDeCaja.SaldoFinal(fila.SaldoInicial, fila.Ingresos, fila.Egresos),
            fila.Estado == EstadoCaja.Abierta && fila.UsuarioResponsableId == usuarioId);
    }

    public async Task<PaginaDe<CajaListado>> ConsultarCajasAsync(int pagina, CancellationToken cancelacion = default)
    {
        var total = await contexto.Cajas.CountAsync(cancelacion);

        pagina = Math.Max(pagina, 1);
        var tamanio = PaginaDe<CajaListado>.TamanioPorDefecto;

        // `FechaApertura DESC, Id DESC` es orden total: el `Id` desempata (convención [003]).
        var filas = await contexto.Cajas
            .OrderByDescending(caja => caja.FechaApertura)
            .ThenByDescending(caja => caja.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(caja => new
            {
                caja.Id,
                caja.UsuarioResponsableId,
                caja.UsuarioResponsable!.Username,
                caja.FechaApertura,
                caja.Estado,
                caja.FechaCierre,
                caja.SaldoFinal,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila => new CajaListado(
                fila.Id,
                new UsuarioResumen(fila.UsuarioResponsableId, fila.Username),
                fila.FechaApertura,
                NombresDeEstadoCaja.EnJson(fila.Estado),
                fila.FechaCierre,
                fila.SaldoFinal))
            .ToList();

        return new PaginaDe<CajaListado>(items, total, pagina, tamanio);
    }

    /// <summary>
    /// Cerrada antes que ajena: a quien intenta operar una caja cerrada de otro, lo que la vuelve inoperable
    /// para cualquiera es que está cerrada.
    /// </summary>
    public async Task<MotivoNoOperable?> MotivoNoOperableAsync(
        int id,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var leida = await contexto.Cajas
            .Where(caja => caja.Id == id)
            .Select(caja => new { caja.Estado, caja.UsuarioResponsableId })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return leida switch
        {
            null => MotivoNoOperable.NoEncontrada,
            { Estado: EstadoCaja.Cerrada } => MotivoNoOperable.Cerrada,
            _ when leida.UsuarioResponsableId != usuarioId => MotivoNoOperable.Ajena,
            _ => null,
        };
    }

    // ── Abrir ───────────────────────────────────────────────────────────────────────────────────

    public Task<bool> ExisteCajaAbiertaAsync(int usuarioId, CancellationToken cancelacion = default) =>
        contexto.Cajas.AnyAsync(
            caja => caja.UsuarioResponsableId == usuarioId && caja.Estado == EstadoCaja.Abierta,
            cancelacion);

    /// <summary>
    /// Si <c>IX_Cajas_UsuarioResponsable_Abierta</c> cortó la inserción, vacía el rastreador antes de avisar:
    /// si no, un reintento en el mismo alcance arrastraría la fila rechazada. El índice se distingue <b>por su
    /// nombre</b>: cualquier otra violación sigue su curso.
    /// </summary>
    public async Task<int> AbrirAsync(Caja caja, CancellationToken cancelacion = default)
    {
        contexto.Cajas.Add(caja);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (
            excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            sql.Message.Contains(CajaConfiguracion.IndiceResponsableAbierta, StringComparison.Ordinal))
        {
            contexto.ChangeTracker.Clear();

            throw new CajaYaAbiertaException(excepcion);
        }

        return caja.Id;
    }

    // ── Movimientos ─────────────────────────────────────────────────────────────────────────────

    public async Task<MovimientoRegistrado> RegistrarMovimientoAsync(
        MovimientoDeCaja movimiento,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        if (!await TomarCajaAsync(movimiento.CajaId, movimiento.UsuarioId, cancelacion))
        {
            await transaccion.RollbackAsync(cancelacion);

            return new MovimientoRegistrado(
                null,
                await MotivoNoOperableAsync(movimiento.CajaId, movimiento.UsuarioId, cancelacion)
                    ?? MotivoNoOperable.Cerrada);
        }

        // Con el lock tomado: una factura cobrada entre el desplegable y el guardado se rechaza (FR-011). El
        // predicado es el mismo del desplegable, escrito en el árbol de la consulta (convención [003]).
        if (movimiento.FacturaId is { } facturaId &&
            !await contexto.Facturas.AnyAsync(
                factura => factura.Id == facturaId && factura.Estado == EstadoFactura.Pendiente,
                cancelacion))
        {
            await transaccion.RollbackAsync(cancelacion);

            return new MovimientoRegistrado(null, FacturaNoPendiente: true);
        }

        contexto.MovimientosDeCaja.Add(movimiento);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return new MovimientoRegistrado(movimiento.Id);
    }

    public async Task<MovimientoListado?> ObtenerMovimientoAsync(int id, CancellationToken cancelacion = default)
    {
        var fila = await contexto.MovimientosDeCaja
            .Where(movimiento => movimiento.Id == id)
            .Select(ProyeccionDeMovimiento)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return fila is null ? null : ALista(fila);
    }

    public Task<PaginaDe<MovimientoListado>> ConsultarMovimientosDeCajaAsync(
        int cajaId,
        int pagina,
        CancellationToken cancelacion = default) =>
        PaginarAsync(contexto.MovimientosDeCaja.Where(movimiento => movimiento.CajaId == cajaId), pagina, cancelacion);

    public async Task<IReadOnlyList<OpcionDeReferencia>> ConsultarFacturasPendientesAsync(
        CancellationToken cancelacion = default)
    {
        var filas = await contexto.Facturas
            .Where(factura => factura.Estado == EstadoFactura.Pendiente)
            .OrderByDescending(factura => factura.Fecha)
            .ThenByDescending(factura => factura.Id)
            .Select(factura => new { factura.Id, factura.NumeroComprobante, factura.ClienteRazonSocial })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return [.. filas.Select(fila =>
            new OpcionDeReferencia(fila.Id, $"Factura {fila.NumeroComprobante} · {fila.ClienteRazonSocial}"))];
    }

    /// <summary>
    /// Sin filtro de estado: <c>OrdenDePago</c> no tiene ninguno (research §4). El tope va en SQL y la pantalla
    /// lo declara debajo del campo, así no oculta filas en silencio (convención [003]).
    /// </summary>
    public async Task<IReadOnlyList<OpcionDeReferencia>> ConsultarOrdenesDePagoAsync(
        CancellationToken cancelacion = default)
    {
        var filas = await contexto.OrdenesDePago
            .OrderByDescending(orden => orden.Numero)
            .Take(50)
            .Select(orden => new
            {
                orden.Id,
                orden.Numero,
                LiquidacionNumero = orden.Liquidacion!.Numero,
                orden.Importe,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return [.. filas.Select(fila => new OpcionDeReferencia(
            fila.Id,
            $"{NumerosVisibles.OrdenDePago(fila.Numero)} · {NumerosVisibles.Liquidacion(fila.LiquidacionNumero)} · " +
            FormatoDeDocumento.Pesos(fila.Importe)))];
    }

    public Task<bool> ExisteOrdenDePagoAsync(int id, CancellationToken cancelacion = default) =>
        contexto.OrdenesDePago.AnyAsync(orden => orden.Id == id, cancelacion);

    // ── Cierre ──────────────────────────────────────────────────────────────────────────────────

    public async Task<ResumenDeCierre> ConsultarResumenAsync(int cajaId, CancellationToken cancelacion = default)
    {
        var saldoInicial = await contexto.Cajas
            .Where(caja => caja.Id == cajaId)
            .Select(caja => caja.SaldoInicial)
            .FirstAsync(cancelacion);

        var totalIngresos = await SumaAsync(cajaId, TipoMovimientoCaja.Ingreso, cancelacion);
        var totalEgresos = await SumaAsync(cajaId, TipoMovimientoCaja.Egreso, cancelacion);

        // Orden cronológico: el resumen se lee como el día pasó.
        var filas = await contexto.MovimientosDeCaja
            .Where(movimiento => movimiento.CajaId == cajaId)
            .OrderBy(movimiento => movimiento.Fecha)
            .ThenBy(movimiento => movimiento.Id)
            .Select(ProyeccionDeMovimiento)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return new ResumenDeCierre(
            saldoInicial,
            totalIngresos,
            totalEgresos,
            [.. filas.Select(ALista)],
            ReglasDeCaja.SaldoFinal(saldoInicial, totalIngresos, totalEgresos));
    }

    public async Task<CierreIntentado> CerrarAsync(
        int cajaId,
        int usuarioId,
        decimal saldoFinalConfirmado,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        if (!await TomarCajaAsync(cajaId, usuarioId, cancelacion))
        {
            await transaccion.RollbackAsync(cancelacion);

            return new CierreIntentado(
                false,
                await MotivoNoOperableAsync(cajaId, usuarioId, cancelacion) ?? MotivoNoOperable.Cerrada);
        }

        // Con el lock tomado: un movimiento que entró antes ya está confirmado y se ve, y uno que llega después
        // espera y encuentra la caja cerrada (research §2).
        var resumen = await ConsultarResumenAsync(cajaId, cancelacion);

        if (resumen.SaldoFinal != saldoFinalConfirmado)
        {
            await transaccion.RollbackAsync(cancelacion);

            return new CierreIntentado(false, ResumenActual: resumen);
        }

        // Misma transacción y mismo lock: no puede fallar por carrera.
        await contexto.Cajas
            .Where(caja => caja.Id == cajaId && caja.Estado == EstadoCaja.Abierta)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(caja => caja.Estado, EstadoCaja.Cerrada)
                    .SetProperty(caja => caja.FechaCierre, ocurridoEn)
                    .SetProperty(caja => caja.SaldoFinal, resumen.SaldoFinal),
                cancelacion);

        await transaccion.CommitAsync(cancelacion);

        return new CierreIntentado(true);
    }

    // ── Consulta global ─────────────────────────────────────────────────────────────────────────

    public Task<PaginaDe<MovimientoListado>> ConsultarMovimientosAsync(
        DateTime? desde,
        DateTime? hastaExcluido,
        int? cajaId,
        int pagina,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.MovimientosDeCaja.AsQueryable();

        if (cajaId is { } caja)
        {
            consulta = consulta.Where(movimiento => movimiento.CajaId == caja);
        }

        if (desde is { } inicio)
        {
            consulta = consulta.Where(movimiento => movimiento.Fecha >= inicio);
        }

        if (hastaExcluido is { } fin)
        {
            consulta = consulta.Where(movimiento => movimiento.Fecha < fin);
        }

        return PaginarAsync(consulta, pagina, cancelacion);
    }

    // ── Piezas comunes ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El <c>UPDATE</c> que no cambia nada: toma el mismo lock de fila que uno real y verifica, en el mismo
    /// paso, que la caja siga abierta y sea del usuario (research §2, FR-035). <c>true</c> si afectó una fila.
    /// </summary>
    private async Task<bool> TomarCajaAsync(int cajaId, int usuarioId, CancellationToken cancelacion)
    {
        var afectadas = await contexto.Cajas
            .Where(caja =>
                caja.Id == cajaId &&
                caja.Estado == EstadoCaja.Abierta &&
                caja.UsuarioResponsableId == usuarioId)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(caja => caja.Estado, caja => caja.Estado), cancelacion);

        return afectadas == 1;
    }

    private Task<decimal> SumaAsync(int cajaId, TipoMovimientoCaja tipo, CancellationToken cancelacion) =>
        contexto.MovimientosDeCaja
            .Where(movimiento => movimiento.CajaId == cajaId && movimiento.Tipo == tipo)
            .SumAsync(movimiento => movimiento.Importe, cancelacion);

    /// <summary>Cuenta y pagina <b>después</b> de filtrar, por <c>Fecha DESC, Id DESC</c> (convención [003]).</summary>
    private async Task<PaginaDe<MovimientoListado>> PaginarAsync(
        IQueryable<MovimientoDeCaja> consulta,
        int pagina,
        CancellationToken cancelacion)
    {
        var total = await consulta.CountAsync(cancelacion);

        pagina = Math.Max(pagina, 1);
        var tamanio = PaginaDe<MovimientoListado>.TamanioPorDefecto;

        var filas = await consulta
            .OrderByDescending(movimiento => movimiento.Fecha)
            .ThenByDescending(movimiento => movimiento.Id)
            .Skip((pagina - 1) * tamanio)
            .Take(tamanio)
            .Select(ProyeccionDeMovimiento)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return new PaginaDe<MovimientoListado>([.. filas.Select(ALista)], total, pagina, tamanio);
    }

}
