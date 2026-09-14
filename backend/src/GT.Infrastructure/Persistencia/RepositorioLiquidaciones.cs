using GT.Application.Choferes;
using GT.Application.Liquidaciones;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia.Configuraciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
// El Módulo 3 tiene su propio `TransportistaResumen`, visible por `PaginaDe`.
using TransportistaResumen = GT.Application.Liquidaciones.TransportistaResumen;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Persistencia de las liquidaciones, con las cuatro transacciones que tienen que ser atómicas adentro
/// (data-model §Transacciones).
///
/// <b>Las tres escrituras sobre una liquidación existente empiezan con un <c>UPDATE</c> condicional sobre
/// su fila</b> y verifican que haya afectado una: bajo el aislamiento por defecto, la segunda transacción
/// se bloquea sobre la fila y, al desbloquearse, reevalúa el <c>WHERE</c> contra el dato ya confirmado
/// (research §2). <c>ExecuteUpdateAsync</c> no pasa por el rastreador, así que los casos de uso releen el
/// detalle al terminar (research §12.4).
/// </summary>
public class RepositorioLiquidaciones(GtDbContext contexto) : IRepositorioLiquidaciones
{
    // ── Armado ──────────────────────────────────────────────────────────────────────────────────

    public async Task<string?> ObtenerCuitEmpresaEmisoraAsync(CancellationToken cancelacion = default)
    {
        var cuit = await contexto.EmpresaEmisora
            .AsNoTracking()
            .Select(empresa => empresa.Cuit)
            .FirstOrDefaultAsync(cancelacion);

        return string.IsNullOrWhiteSpace(cuit) ? null : cuit;
    }

    public Task<Transportista?> ObtenerTransportistaAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Transportistas
            .AsNoTracking()
            .FirstOrDefaultAsync(transportista => transportista.Id == id, cancelacion);

    /// <summary>
    /// Los dos CUIT ya están normalizados a once dígitos —Módulos 3 y 6—, así que la comparación es exacta
    /// sin transformar nada (research §5).
    /// </summary>
    public async Task<IReadOnlyList<Transportista>> ConsultarTransportistasLiquidablesAsync(
        string cuitEmpresaEmisora,
        bool incluirInactivos,
        CancellationToken cancelacion = default) =>
        await contexto.Transportistas
            .Where(transportista =>
                (incluirInactivos || transportista.Activo) &&
                transportista.Cuit != cuitEmpresaEmisora)
            .OrderBy(transportista => transportista.Nombre)
            .ThenBy(transportista => transportista.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    /// <summary>
    /// El predicado de FR-004 <b>entero en el árbol de la consulta</b>: extraído a un método dejaría de
    /// traducirse y la consulta se evaluaría en memoria (convención [003], research §6).
    ///
    /// Los viajes liberados por una anulación vuelven solos: su vínculo quedó con <c>Vigente = 0</c> y la
    /// subconsulta sólo mira los vigentes.
    /// </summary>
    public async Task<IReadOnlyList<Viaje>> ConsultarDisponiblesAsync(
        int transportistaId,
        int mes,
        int anio,
        CancellationToken cancelacion = default) =>
        await contexto.Viajes
            .Where(viaje =>
                viaje.TransportistaId == transportistaId &&
                (viaje.Estado == EstadoViaje.Rendido || viaje.Estado == EstadoViaje.Facturado) &&
                viaje.Fecha.Month == mes &&
                viaje.Fecha.Year == anio &&
                !contexto.LiquidacionViajes.Any(vinculo => vinculo.ViajeId == viaje.Id && vinculo.Vigente))
            .OrderBy(viaje => viaje.Fecha)
            .ThenBy(viaje => viaje.Numero)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Viajes
            .Where(viaje => ids.Contains(viaje.Id))
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<VinculoVigente>> ConsultarVinculosVigentesAsync(
        IReadOnlyCollection<int> viajeIds,
        CancellationToken cancelacion = default) =>
        await contexto.LiquidacionViajes
            .Where(vinculo => viajeIds.Contains(vinculo.ViajeId) && vinculo.Vigente)
            .OrderBy(vinculo => vinculo.Viaje!.Fecha)
            .ThenBy(vinculo => vinculo.Viaje!.Numero)
            .Select(vinculo => new VinculoVigente(
                vinculo.ViajeId,
                vinculo.Viaje!.Numero,
                vinculo.LiquidacionId,
                vinculo.Liquidacion!.Numero))
            .AsNoTracking()
            .ToListAsync(cancelacion);

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los cuatro filtros <b>antes</b> de paginar, y el estado sobre la columna, que es exactamente lo que
    /// la fila muestra (FR-029). El formato <c>LQ-…</c> y los nombres en JSON se aplican sobre las 20 filas
    /// ya traídas: ahí no hay nada que paginar.
    /// </summary>
    public async Task<PaginaDe<LiquidacionListado>> ConsultarAsync(
        FiltrosDeLiquidaciones filtros,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Liquidaciones.AsQueryable();

        if (filtros.TransportistaId is { } transportistaId)
        {
            consulta = consulta.Where(liquidacion => liquidacion.TransportistaId == transportistaId);
        }

        // Mes y año filtran por separado, igual que el listado de facturas: sólo `2026` trae todo el año.
        if (filtros.Mes is { } mes)
        {
            consulta = consulta.Where(liquidacion => liquidacion.PeriodoMes == mes);
        }

        if (filtros.Anio is { } anio)
        {
            consulta = consulta.Where(liquidacion => liquidacion.PeriodoAnio == anio);
        }

        if (filtros.Estado is { } estado)
        {
            consulta = consulta.Where(liquidacion => liquidacion.Estado == estado);
        }

        var total = await consulta.CountAsync(cancelacion);
        var pagina = Math.Max(filtros.Pagina, 1);

        // `Numero DESC` es orden total: la secuencia no repite (FR-023, convención [003]).
        var filas = await consulta
            .OrderByDescending(liquidacion => liquidacion.Numero)
            .Skip((pagina - 1) * PaginaDe<LiquidacionListado>.TamanioPorDefecto)
            .Take(PaginaDe<LiquidacionListado>.TamanioPorDefecto)
            .Select(liquidacion => new
            {
                liquidacion.Id,
                liquidacion.Numero,
                liquidacion.PeriodoMes,
                liquidacion.PeriodoAnio,
                liquidacion.TransportistaId,
                liquidacion.Transportista!.Nombre,
                liquidacion.Transportista.Cuit,
                liquidacion.Transportista.Activo,
                liquidacion.ImporteTotal,
                RestaPagar = liquidacion.ImporteTotal - liquidacion.ImportePagado,
                liquidacion.Estado,
                liquidacion.MotivoAnulacion,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila => new LiquidacionListado(
                fila.Id,
                NumerosVisibles.Liquidacion(fila.Numero),
                fila.PeriodoMes,
                fila.PeriodoAnio,
                new TransportistaResumen(fila.TransportistaId, fila.Nombre, fila.Cuit, fila.Activo),
                fila.ImporteTotal,
                // Una anulada no debe nada (FR-027, FR-059).
                fila.Estado is EstadoLiquidacion.Anulada ? null : fila.RestaPagar,
                NombresDeEstadoLiquidacion.EnJson(fila.Estado),
                fila.MotivoAnulacion))
            .ToList();

        return new PaginaDe<LiquidacionListado>(
            items,
            total,
            pagina,
            PaginaDe<LiquidacionListado>.TamanioPorDefecto);
    }

    public Task<Liquidacion?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Liquidaciones
            .Include(liquidacion => liquidacion.Transportista)
            .Include(liquidacion => liquidacion.Viajes).ThenInclude(vinculo => vinculo.Viaje)
            .Include(liquidacion => liquidacion.OrdenesDePago).ThenInclude(orden => orden.Usuario)
            .Include(liquidacion => liquidacion.Cambios).ThenInclude(cambio => cambio.Usuario)
            .Include(liquidacion => liquidacion.Cambios)
                .ThenInclude(cambio => cambio.Viajes)
                .ThenInclude(viaje => viaje.Viaje)
            // Cuatro colecciones en una sola consulta multiplicarían las filas entre sí.
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(liquidacion => liquidacion.Id == id, cancelacion);

    // ── Generar (data-model §Generar, FR-011, FR-014, FR-018) ───────────────────────────────────

    public async Task<int> GenerarAsync(
        Liquidacion liquidacion,
        IReadOnlyList<int> viajeIds,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        contexto.Liquidaciones.Add(liquidacion);

        // En orden de `Id`: dos generaciones que comparten viajes toman los bloqueos de clave en el mismo
        // orden y la segunda espera en vez de interbloquearse.
        foreach (var viajeId in viajeIds.Order())
        {
            liquidacion.Viajes.Add(new LiquidacionViaje { LiquidacionId = liquidacion.Id, ViajeId = viajeId });
        }

        // Toda liquidación tiene al menos una entrada: la de su generación (FR-033).
        liquidacion.Cambios.Add(new CambioDeLiquidacion
        {
            LiquidacionId = liquidacion.Id,
            Operacion = OperacionDeLiquidacion.Generacion,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await GuardarTraduciendoIndiceAsync(transaccion, cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return liquidacion.Id;
    }

    // ── Editar (data-model §Editar, FR-045 a FR-052) ────────────────────────────────────────────

    public async Task<bool> EditarAsync(
        int id,
        int versionAbierta,
        decimal nuevoTotal,
        IReadOnlyList<int> quitados,
        IReadOnlyList<int> agregados,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        // **Primero** a propósito: toma el bloqueo de la fila antes de tocar los vínculos, y es lo que hace
        // que un pago simultáneo espere o haga fallar a la edición. La condición de `Version` es lo que
        // vuelve segura una diferencia de viajes calculada antes de la transacción (research §2).
        var afectadas = await contexto.Liquidaciones
            .Where(liquidacion =>
                liquidacion.Id == id &&
                liquidacion.Estado == EstadoLiquidacion.Pendiente &&
                liquidacion.ImportePagado == 0m &&
                liquidacion.Version == versionAbierta)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(liquidacion => liquidacion.ImporteTotal, nuevoTotal)
                    .SetProperty(liquidacion => liquidacion.Version, liquidacion => liquidacion.Version + 1),
                cancelacion);

        if (afectadas != 1)
        {
            await transaccion.RollbackAsync(cancelacion);

            return false;
        }

        // Quitar borra el vínculo: la liquidación es pendiente y sin pagos, y qué se quitó queda en el
        // historial de la edición (research §1, §3b).
        if (quitados.Count > 0)
        {
            await contexto.LiquidacionViajes
                .Where(vinculo => vinculo.LiquidacionId == id && quitados.Contains(vinculo.ViajeId))
                .ExecuteDeleteAsync(cancelacion);
        }

        foreach (var viajeId in agregados.Order())
        {
            contexto.LiquidacionViajes.Add(new LiquidacionViaje { LiquidacionId = id, ViajeId = viajeId });
        }

        var entrada = new CambioDeLiquidacion
        {
            LiquidacionId = id,
            Operacion = OperacionDeLiquidacion.Edicion,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        };

        // La misma diferencia que se aplicó a los vínculos: salen del mismo cálculo y no pueden discrepar.
        foreach (var viajeId in quitados)
        {
            entrada.Viajes.Add(new CambioDeLiquidacionViaje
            {
                CambioDeLiquidacionId = entrada.Id,
                ViajeId = viajeId,
                Agregado = false,
            });
        }

        foreach (var viajeId in agregados)
        {
            entrada.Viajes.Add(new CambioDeLiquidacionViaje
            {
                CambioDeLiquidacionId = entrada.Id,
                ViajeId = viajeId,
                Agregado = true,
            });
        }

        contexto.CambiosDeLiquidacion.Add(entrada);

        await GuardarTraduciendoIndiceAsync(transaccion, cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return true;
    }

    // ── Anular (data-model §Anular, FR-053 a FR-060) ────────────────────────────────────────────

    public async Task<bool> AnularAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        var afectadas = await contexto.Liquidaciones
            .Where(liquidacion =>
                liquidacion.Id == id &&
                liquidacion.Estado == EstadoLiquidacion.Pendiente &&
                liquidacion.ImportePagado == 0m)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(liquidacion => liquidacion.Estado, EstadoLiquidacion.Anulada)
                    .SetProperty(liquidacion => liquidacion.MotivoAnulacion, motivo),
                cancelacion);

        if (afectadas != 1)
        {
            await transaccion.RollbackAsync(cancelacion);

            return false;
        }

        // Todos a la vez: los viajes quedan libres y la anulada sigue sabiendo qué agrupaba (FR-028, FR-057).
        await contexto.LiquidacionViajes
            .Where(vinculo => vinculo.LiquidacionId == id)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(vinculo => vinculo.Vigente, false), cancelacion);

        contexto.CambiosDeLiquidacion.Add(new CambioDeLiquidacion
        {
            LiquidacionId = id,
            Operacion = OperacionDeLiquidacion.Anulacion,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return true;
    }

    // ── Pagar (data-model §Pagar, FR-036 a FR-044) ──────────────────────────────────────────────

    public async Task<OrdenRegistrada?> RegistrarPagoAsync(
        int id,
        DateOnly fechaPago,
        decimal importe,
        int usuarioId,
        DateTime registradaEn,
        CancellationToken cancelacion = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        // Lo que cierra SC-007: el importe tiene que entrar **en el saldo confirmado**, no en el que se leyó.
        // El `CASE` del estado es la segunda escritura de `ReglasDeLiquidacion.EstadoTrasPago`, y los dos
        // lados de un `SET` leen los valores de antes del `UPDATE`.
        var afectadas = await contexto.Liquidaciones
            .Where(liquidacion =>
                liquidacion.Id == id &&
                liquidacion.Estado == EstadoLiquidacion.Pendiente &&
                liquidacion.ImportePagado + importe <= liquidacion.ImporteTotal)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(
                        liquidacion => liquidacion.ImportePagado,
                        liquidacion => liquidacion.ImportePagado + importe)
                    .SetProperty(
                        liquidacion => liquidacion.Estado,
                        liquidacion => liquidacion.ImportePagado + importe == liquidacion.ImporteTotal
                            ? EstadoLiquidacion.Pagada
                            : EstadoLiquidacion.Pendiente),
                cancelacion);

        if (afectadas != 1)
        {
            await transaccion.RollbackAsync(cancelacion);

            return null;
        }

        // La fila está bloqueada por esta transacción: lo que se lee es lo que dejó el `UPDATE`.
        var estado = await contexto.Liquidaciones
            .Where(liquidacion => liquidacion.Id == id)
            .Select(liquidacion => liquidacion.Estado)
            .FirstAsync(cancelacion);

        var orden = new OrdenDePago
        {
            LiquidacionId = id,
            FechaPago = fechaPago,
            Importe = importe,
            UsuarioId = usuarioId,
            RegistradaEn = registradaEn,
        };

        contexto.OrdenesDePago.Add(orden);

        // El paso a pagada entra en el historial con el usuario y el instante de esta orden; las parciales
        // no agregan entrada (FR-033).
        if (estado is EstadoLiquidacion.Pagada)
        {
            contexto.CambiosDeLiquidacion.Add(new CambioDeLiquidacion
            {
                LiquidacionId = id,
                Operacion = OperacionDeLiquidacion.Pagada,
                UsuarioId = usuarioId,
                OcurridoEn = registradaEn,
            });
        }

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return new OrdenRegistrada(orden.Numero, estado);
    }

    // ── Traducción de la violación de índice (convención [003], research §12.7) ─────────────────

    /// <summary>
    /// Guarda y, si <c>IX_LiquidacionViajes_ViajeVigente</c> cortó la inserción, deshace <b>y</b> vacía el
    /// rastreador antes de avisar: si no, un reintento en el mismo alcance arrastraría las filas insertadas
    /// (research §12.5). El índice se distingue <b>por su nombre</b>: cualquier otra violación sigue su curso.
    /// </summary>
    private async Task GuardarTraduciendoIndiceAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaccion,
        CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (
            excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            sql.Message.Contains(LiquidacionViajeConfiguracion.IndiceViajeVigente, StringComparison.Ordinal))
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            contexto.ChangeTracker.Clear();

            throw new ViajeYaLiquidadoException(excepcion);
        }
    }
}
