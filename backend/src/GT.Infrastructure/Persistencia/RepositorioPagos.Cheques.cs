using GT.Application.Choferes;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<PaginaDe<ChequeResumen>> ChequesAsync(FiltroCheques filtro, DateOnly hoy, CancellationToken ct = default)
    {
        ConsultarChequesPagos.Validar(filtro);
        var limite = DateOnly.FromDayNumber(Math.Min(DateOnly.MaxValue.DayNumber, hoy.DayNumber + 30));
        var consulta = contexto.Cheques.AsNoTracking().Where(c => c.FechaVencimiento <= limite);
        if (filtro.BancoId is { } banco) consulta = consulta.Where(c => c.BancoId == banco);
        if (filtro.Estado is { } estado) consulta = consulta.Where(c => c.EstadoCheque == estado);
        if (filtro.DesdeVencimiento is { } desde) consulta = consulta.Where(c => c.FechaVencimiento >= desde);
        if (filtro.HastaVencimiento is { } hasta) consulta = consulta.Where(c => c.FechaVencimiento <= hasta);
        var total = await consulta.CountAsync(ct);
        var items = await consulta.OrderBy(c => c.FechaVencimiento).ThenBy(c => c.Id).Skip((filtro.Pagina - 1) * 20).Take(20)
            .Select(c => new ChequeResumen(c.Id, c.NumeroCheque, new(c.BancoId, c.Banco!.Nombre, c.Banco.Activo),
                c.Fecha, c.FechaVencimiento, c.Importe, c.EstadoCheque, c.FechaVencimiento < hoy ? "vencido" : "proximoAvencer",
                new(c.OrdenPagoProveedorId, NumeroVisiblePagoProveedor.Desde(c.OrdenPagoProveedor!.Numero)))).ToListAsync(ct);
        return new(items, total, filtro.Pagina, 20);
    }

    public async Task<IReadOnlyList<BancoReferencia>> BancosHistoricosAsync(CancellationToken ct = default) =>
        await contexto.Bancos.AsNoTracking().Where(b => contexto.Cheques.Any(c => c.BancoId == b.Id))
            .OrderBy(b => b.Nombre).ThenBy(b => b.Id).Select(b => new BancoReferencia(b.Id, b.Nombre, b.Activo)).ToListAsync(ct);

    public async Task<DetalleCheque?> ChequeAsync(int id, DateOnly hoy, CancellationToken ct = default)
    {
        var c = await contexto.Cheques.AsNoTracking().Include(c => c.Banco).Include(c => c.OrdenPagoProveedor).SingleOrDefaultAsync(c => c.Id == id, ct);
        if (c is null) return null;
        var historial = await contexto.HistorialEstadosCheque.AsNoTracking().Where(h => h.ChequeId == id)
            .OrderBy(h => h.OcurridoEn).ThenBy(h => h.Id)
            .Select(h => new CambioChequeDto(h.Id, h.EstadoAnterior, h.EstadoNuevo, new(h.UsuarioId, h.Usuario!.Username), h.OcurridoEn)).ToListAsync(ct);
        return new(c.Id, c.NumeroCheque, new(c.BancoId, c.Banco!.Nombre, c.Banco.Activo), c.Fecha,
            c.FechaVencimiento, c.Importe, c.EstadoCheque, SituacionCheque(c.FechaVencimiento, hoy),
            new(c.OrdenPagoProveedorId, NumeroVisiblePagoProveedor.Desde(c.OrdenPagoProveedor!.Numero)), c.DetalleCheque, historial);
    }
}
