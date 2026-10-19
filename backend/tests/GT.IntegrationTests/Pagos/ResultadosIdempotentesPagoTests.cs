using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class ResultadosIdempotentesPagoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ResultadoEsGlobalInmutableYAutorizado_ContenidoDistintoEsConflicto()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var clave = Guid.NewGuid();
        var entrada = new GenerarOrdenEntrada(clave, s.Orden.ProveedorId, s.Orden.Fecha, null, [s.Factura.Id],
            [new EfectivoEntrada(Guid.NewGuid(), 121m)], 121m, true);
        var comando = CanonicalizadorPago.Generar(entrada);
        var json = "{\"id\":" + s.Orden.Id + ",\"importeTotal\":\"121.00\"}";
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await new BloqueosPagos(db).IdempotenciaAsync(TipoOperacionPago.Generar, clave);
            var store = new ResultadosIdempotentesPago(db);
            Assert.Null(await store.BuscarAsync(TipoOperacionPago.Generar, clave, comando, s.UsuarioId, false));
            await store.GuardarAsync(TipoOperacionPago.Generar, clave, comando, s.UsuarioId, s.Orden.Id,
                new(201, json, $"/api/pagos/ordenes/{s.Orden.Id}"), s.Orden.RegistradaEn);
            await tx.CommitAsync();
        });
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await new BloqueosPagos(db).IdempotenciaAsync(TipoOperacionPago.Generar, clave);
            var store = new ResultadosIdempotentesPago(db);
            var replay = await store.BuscarAsync(TipoOperacionPago.Generar, clave, comando, s.UsuarioId, false);
            Assert.Equal(json, replay!.Json);
            Assert.Equal(201, replay.EstadoHttp);
            var sinPermiso = await Assert.ThrowsAsync<ReglaPagoException>(() => store.BuscarAsync(TipoOperacionPago.Generar, clave, comando, int.MaxValue, false));
            Assert.Equal(403, sinPermiso.EstadoHttp);
            Assert.Equal(json, (await store.BuscarAsync(TipoOperacionPago.Generar, clave, comando, int.MaxValue, true))!.Json);
            var conflicto = await Assert.ThrowsAsync<ReglaPagoException>(() => store.BuscarAsync(TipoOperacionPago.Generar, clave,
                CanonicalizadorPago.Generar(entrada with { Detalle = "Distinto" }), s.UsuarioId, false));
            Assert.Equal(409, conflicto.EstadoHttp);
            Assert.Null(await store.BuscarAsync(TipoOperacionPago.Anular, clave, CanonicalizadorPago.Anular(s.Orden.Id, new(clave, "Motivo", true)), s.UsuarioId, true));
            await tx.RollbackAsync();
        });
        var recuperado = await ResultadosIdempotentesPago.RecuperarAsync(app.Services.GetRequiredService<IServiceScopeFactory>(),
            TipoOperacionPago.Generar, clave, comando, s.UsuarioId, false);
        Assert.Equal(json, recuperado.Json);
        var incierto = await Assert.ThrowsAsync<ReglaPagoException>(() => ResultadosIdempotentesPago.RecuperarAsync(
            app.Services.GetRequiredService<IServiceScopeFactory>(), TipoOperacionPago.Generar, Guid.NewGuid(), comando, s.UsuarioId, false));
        Assert.Equal(503, incierto.EstadoHttp);
    }

    [Fact]
    public async Task RollbackNoGuardaResultadoConfirmado()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var clave = Guid.NewGuid();
        var comando = CanonicalizadorPago.Anular(s.Orden.Id, new(clave, "Motivo", true));
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await new BloqueosPagos(db).IdempotenciaAsync(TipoOperacionPago.Anular, clave);
            await new ResultadosIdempotentesPago(db).GuardarAsync(TipoOperacionPago.Anular, clave, comando, s.UsuarioId, s.Orden.Id,
                new(200, "{}", null), s.Orden.RegistradaEn);
            await tx.RollbackAsync();
        });
        await app.EnLaBaseAsync(async db => Assert.False(await db.ResultadosOperacionPago.AnyAsync(x => x.Clave == clave)));
    }
}
