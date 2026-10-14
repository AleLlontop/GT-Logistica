using GT.Application.Choferes;
using GT.Application.Facturacion;
using GT.Application.Facturacion.Afip;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using GT.Infrastructure.Afip;

namespace GT.UnitTests.Facturacion;

public class AfipDemoFacturacionServiceTests
{
    private class RepositorioFacturasFalso(int ultimoCorrelativo) : IRepositorioFacturas
    {
        public Task<int> ObtenerUltimoNumeroCorrelativoAsync(string puntoDeVenta, CancellationToken cancelacion = default) =>
            Task.FromResult(ultimoCorrelativo);

        public Task<IReadOnlyList<Viaje>> ConsultarFacturablesAsync(int clienteId, int mes, int anio, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(IReadOnlyList<int> ids, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<FacturaCliente?> ObtenerPorNumeroAsync(string numeroComprobante, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FacturaCliente>> ConsultarAnuladasSinReemplazoAsync(int clienteId, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<PaginaDe<FacturaListado>> ConsultarAsync(FiltrosDeFacturas filtros, DateOnly hoy, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<FacturaCliente?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<FacturaCliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<FacturaCliente?> ObtenerQueLaReemplazaAsync(int id, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<ResultadoDeEmision> EmitirAsync(FacturaCliente factura, IReadOnlyList<int> viajeIds, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task CorregirAsync(FacturaCliente factura, int usuarioId, DateTime ocurridoEn, Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task AnularAsync(FacturaCliente factura, string motivo, int usuarioId, DateTime ocurridoEn, Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task RegistrarCobroAsync(FacturaCliente factura, DateOnly fechaCobro, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FilaDeVencimiento>> ConsultarVencimientosAsync(DateOnly hoy, CancellationToken cancelacion = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TotalPorCliente>> ConsultarTotalesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task AutorizarComprobante_PrimerComprobante_GeneraNumeroUno()
    {
        var repo = new RepositorioFacturasFalso(0);
        var servicio = new AfipDemoFacturacionService(repo);
        var fecha = new DateOnly(2026, 10, 14);

        var resultado = await servicio.AutorizarComprobanteAsync(
            new SolicitudAutorizacionAfip("14", "facturaA", fecha, 50000m));

        Assert.Equal("0014-00000001", resultado.NumeroComprobante);
        Assert.Equal(14, resultado.Cae.Length);
        Assert.StartsWith("74", resultado.Cae);
        Assert.Equal(new DateOnly(2026, 10, 24), resultado.CaeVencimiento);
        Assert.Equal("Demo", resultado.Modo);
    }

    [Fact]
    public async Task AutorizarComprobante_ConComprobantesPrevios_IncrementaCorrelativo()
    {
        var repo = new RepositorioFacturasFalso(42);
        var servicio = new AfipDemoFacturacionService(repo);
        var fecha = new DateOnly(2026, 10, 14);

        var resultado = await servicio.AutorizarComprobanteAsync(
            new SolicitudAutorizacionAfip("0014", "facturaB", fecha, 120000m));

        Assert.Equal("0014-00000043", resultado.NumeroComprobante);
        Assert.Equal(14, resultado.Cae.Length);
        Assert.Equal(new DateOnly(2026, 10, 24), resultado.CaeVencimiento);
    }
}
