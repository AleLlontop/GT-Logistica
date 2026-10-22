using GT.Application.Choferes;
using GT.Domain.Choferes;
using GT.Domain.Pagos;

namespace GT.Application.Pagos;

public class ConsultarChequesPagos(IRepositorioPagos pagos, TimeProvider reloj)
{
    public Task<PaginaDe<ChequeResumen>> ListarAsync(FiltroCheques filtro, CancellationToken ct = default) =>
        pagos.ChequesAsync(filtro, FechaHoyArgentina.Desde(reloj.GetUtcNow()), ct);
    public Task<DetalleCheque?> DetalleAsync(int id, CancellationToken ct = default) =>
        pagos.ChequeAsync(id, FechaHoyArgentina.Desde(reloj.GetUtcNow()), ct);
    public static void Validar(FiltroCheques filtro)
    {
        if (filtro.DesdeVencimiento > filtro.HastaVencimiento)
            throw new ReglaPagoException("rango_invalido", "Desde no puede ser posterior a Hasta.", campo: "desdeVencimiento");
        if (filtro.Pagina < 1 || filtro.Pagina > int.MaxValue / 20)
            throw new ReglaPagoException("pagina_invalida", "La página solicitada no es válida.", campo: "pagina");
        if (filtro.BancoId is <= 0) throw new ReglaPagoException("banco_invalido", "Elegí un banco válido.", campo: "bancoId");
        if (filtro.Estado.HasValue && !Enum.IsDefined(filtro.Estado.Value))
            throw new ReglaPagoException("estado_invalido", "Elegí un estado de cheque válido.", campo: "estado");
    }
}
public static class NombresVencimientoCheque
{
    public static string EnJson(SituacionVencimientoCheque situacion) => situacion switch
    {
        SituacionVencimientoCheque.Vencido => "vencido",
        SituacionVencimientoCheque.ProximoAVencer => "proximoAvencer",
        SituacionVencimientoCheque.FueraDelPeriodoDeAlerta => "fueraDelPeriodoDeAlerta",
        _ => throw new ArgumentOutOfRangeException(nameof(situacion)),
    };
}
