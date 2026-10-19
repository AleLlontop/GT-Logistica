using GT.Application.Pagos;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<DetalleCheque?> CambiarEstadoAsync(int id, EstadoChequeEntrada entrada, int usuarioId,
        DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(entrada.EstadoAnterior) || !Enum.IsDefined(entrada.EstadoNuevo))
            throw new ReglaPagoException("estado_invalido", "Elegí un estado de cheque válido.", campo: "estadoNuevo");
        var padre = await contexto.Cheques.AsNoTracking().Where(c => c.Id == id)
            .Select(c => (int?)c.OrdenPagoProveedorId).SingleOrDefaultAsync(ct);
        if (padre is null) return null;
        await using var tx = await contexto.Database.BeginTransactionAsync(ct);
        try
        {
            var locks = new BloqueosPagos(contexto);
            await locks.OrdenAsync(padre.Value, ct);
            await locks.ChequesAsync([id], ct);
            var actual = await contexto.Cheques.AsNoTracking().Where(c => c.Id == id).Select(c => c.EstadoCheque).SingleAsync(ct);
            if (actual != entrada.EstadoAnterior)
                throw new ReglaPagoException("estado_cheque_modificado", "El estado del cheque cambió. Actualizá la información.", 409);
            if (!ReglasDePagos.TransicionChequePermitida(actual, entrada.EstadoNuevo))
                throw new ReglaPagoException("transicion_no_permitida", "El cambio de estado solicitado no está permitido.", 409);
            if (await contexto.Cheques.Where(c => c.Id == id && c.EstadoCheque == actual)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.EstadoCheque, entrada.EstadoNuevo), ct) != 1)
                throw new ReglaPagoException("estado_cheque_modificado", "El estado del cheque cambió. Actualizá la información.", 409);
            contexto.HistorialEstadosCheque.Add(new CambioEstadoCheque { ChequeId = id, EstadoAnterior = actual,
                EstadoNuevo = entrada.EstadoNuevo, UsuarioId = usuarioId, OcurridoEn = ahoraUtc });
            await contexto.SaveChangesAsync(ct);
            var detalle = await ChequeAsync(id, hoy, ct);
            await tx.CommitAsync(ct);
            return detalle;
        }
        catch (Exception error) when (BloqueosPagos.Traducir(error) is not null)
        {
            throw BloqueosPagos.Traducir(error)!;
        }
    }
}
