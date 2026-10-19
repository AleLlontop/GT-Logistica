using GT.Application.Pagos;
using GT.Domain.Pagos;

namespace GT.UnitTests.Pagos;

public class ValidarAnulacionPagoTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MotivoEsEntradaObligatoria(string? motivo)
    {
        var e = Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarEntrada(new(Guid.NewGuid(), motivo, true)));
        Assert.Equal(400, e.EstadoHttp); Assert.Equal("motivo", e.Campo);
    }
    [Fact]
    public void ConfirmacionYClaveSonEntrada_SinHandshake409()
    {
        Assert.Equal(400, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarEntrada(new(Guid.NewGuid(), "Motivo", false))).EstadoHttp);
        Assert.Equal(400, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarEntrada(new(Guid.Empty, "Motivo", true))).EstadoHttp);
        AnularOrdenProveedor.ValidarEntrada(new(Guid.NewGuid(), " Motivo ", true));
    }
    [Theory]
    [InlineData(EstadoCheque.Entregado)]
    [InlineData(EstadoCheque.Depositado)]
    [InlineData(EstadoCheque.Cobrado)]
    [InlineData(EstadoCheque.Rechazado)]
    [InlineData(EstadoCheque.Anulado)]
    public void CualquierChequeNoPendienteImpideTodaAnulacion(EstadoCheque estado) => Assert.Equal(409,
        Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(EstadoOrdenPagoProveedor.Pendiente,
            [EstadoCheque.Pendiente, estado], false, true, true)).EstadoHttp);
    [Fact]
    public void TransferenciaCajaOReversionNoAptasSonConflictos()
    {
        AnularOrdenProveedor.ValidarSituacion(EstadoOrdenPagoProveedor.Pendiente, [EstadoCheque.Pendiente], false, true, true);
        foreach (var caso in new[] { (true, true, true), (false, false, true), (false, true, false) })
            Assert.Equal(409, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(EstadoOrdenPagoProveedor.Pendiente,
                [], caso.Item1, caso.Item2, caso.Item3)).EstadoHttp);
    }
}
