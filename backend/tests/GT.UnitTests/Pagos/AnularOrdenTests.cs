using GT.Application.Pagos;
using GT.Domain.Pagos;

namespace GT.UnitTests.Pagos;

public class AnularOrdenTests
{
    [Fact]
    public void AnuladaEsFinal_YUnaTransferenciaImpideInclusoConChequesValidos()
    {
        Assert.Equal(409, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(
            EstadoOrdenPagoProveedor.Anulada, [], false, true, true)).EstadoHttp);
        Assert.Equal(409, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(
            EstadoOrdenPagoProveedor.Pendiente, [EstadoCheque.Pendiente], true, true, true)).EstadoHttp);
        Assert.Equal(409, Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(
            EstadoOrdenPagoProveedor.Pendiente, [], true, true, true)).EstadoHttp);
    }
    [Fact]
    public void CajaOriginalYReversionPropiaSonCondicionesIndependientes()
    {
        Assert.Equal("caja_no_disponible", Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(
            EstadoOrdenPagoProveedor.Pendiente, [], false, false, true)).Codigo);
        Assert.Equal("pago_no_reversible", Assert.Throws<ReglaPagoException>(() => AnularOrdenProveedor.ValidarSituacion(
            EstadoOrdenPagoProveedor.Pendiente, [EstadoCheque.Pendiente], false, true, false)).Codigo);
    }
    [Fact]
    public void ComandoAnularCanonicoIncluyeOrdenYMotivo_SinClaveUsuarioConfirmacion()
    {
        var primera = CanonicalizadorPago.Anular(5, new(Guid.NewGuid(), "  Corregir  ", true));
        var replay = CanonicalizadorPago.Anular(5, new(Guid.NewGuid(), "Corregir", false));
        Assert.Equal(primera.Contenido, replay.Contenido); Assert.Equal(primera.Hash, replay.Hash);
        Assert.NotEqual(primera.Contenido, CanonicalizadorPago.Anular(6, new(Guid.NewGuid(), "Corregir", true)).Contenido);
        Assert.NotEqual(primera.Contenido, CanonicalizadorPago.Anular(5, new(Guid.NewGuid(), "Otro motivo", true)).Contenido);
        var generar = CanonicalizadorPago.Generar(new(Guid.NewGuid(), 1, new(2026, 10, 18), null, [1], [new EfectivoEntrada(Guid.NewGuid(), 1m)], 1m, true));
        Assert.NotEqual(primera.Contenido, generar.Contenido);
    }
}
