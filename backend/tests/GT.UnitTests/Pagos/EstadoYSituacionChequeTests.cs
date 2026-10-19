using GT.Application.Pagos;
using GT.Domain.Choferes;
using GT.Domain.Pagos;

namespace GT.UnitTests.Pagos;

public class EstadoYSituacionChequeTests
{
    public static IEnumerable<object[]> Pares() => from anterior in Enum.GetValues<EstadoCheque>()
        from nuevo in Enum.GetValues<EstadoCheque>() select new object[] { anterior, nuevo };

    [Theory]
    [MemberData(nameof(Pares))]
    public void SoloAristasExplicitas_SinSalidasDeFinales(EstadoCheque anterior, EstadoCheque nuevo)
    {
        var permitidas = new HashSet<(EstadoCheque, EstadoCheque)>
        {
            (EstadoCheque.Pendiente, EstadoCheque.Entregado), (EstadoCheque.Pendiente, EstadoCheque.Anulado),
            (EstadoCheque.Entregado, EstadoCheque.Depositado), (EstadoCheque.Entregado, EstadoCheque.Anulado),
            (EstadoCheque.Depositado, EstadoCheque.Cobrado), (EstadoCheque.Depositado, EstadoCheque.Rechazado),
        };
        Assert.Equal(permitidas.Contains((anterior, nuevo)), ReglasDePagos.TransicionChequePermitida(anterior, nuevo));
    }

    [Theory]
    [InlineData(-1, SituacionVencimientoCheque.Vencido, true)]
    [InlineData(0, SituacionVencimientoCheque.ProximoAVencer, true)]
    [InlineData(30, SituacionVencimientoCheque.ProximoAVencer, true)]
    [InlineData(31, SituacionVencimientoCheque.FueraDelPeriodoDeAlerta, false)]
    public void BordesInclusivosSinDependerDeEstado(int dias, SituacionVencimientoCheque esperada, bool alerta)
    {
        var hoy = new DateOnly(2026, 10, 18);
        Assert.Equal(esperada, VencimientoCheque.Evaluar(hoy.AddDays(dias), hoy));
        Assert.Equal(alerta, VencimientoCheque.EnAlerta(hoy.AddDays(dias), hoy));
    }

    [Fact]
    public void DiaArgentinoCambiaSituacion_SinMutarEstadoNiHistorial()
    {
        var vencimiento = new DateOnly(2026, 10, 18);
        var cheque = new Cheque { NumeroCheque = "0001", FechaVencimiento = vencimiento, EstadoCheque = EstadoCheque.Depositado };
        var antes = FechaHoyArgentina.Desde(new DateTimeOffset(2026, 10, 19, 2, 59, 0, TimeSpan.Zero));
        var despues = FechaHoyArgentina.Desde(new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero));
        Assert.Equal("proximoAvencer", NombresVencimientoCheque.EnJson(VencimientoCheque.Evaluar(vencimiento, antes)));
        Assert.Equal("vencido", NombresVencimientoCheque.EnJson(VencimientoCheque.Evaluar(vencimiento, despues)));
        Assert.Equal(EstadoCheque.Depositado, cheque.EstadoCheque);
    }
}
