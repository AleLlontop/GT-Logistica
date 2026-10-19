using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class RepositorioConsultasOrdenesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task FiltrosCombinadosPaginacionTotalYProveedorHistoricoInactivo()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            for (var i = 0; i < 20; i++) db.OrdenesPagoProveedor.Add(new()
            {
                ProveedorId = s.Orden.ProveedorId, Fecha = s.Orden.Fecha, ProveedorRazonSocial = s.Orden.ProveedorRazonSocial,
                ProveedorCuit = s.Orden.ProveedorCuit, TipoProveedorAlRegistrar = s.Orden.TipoProveedorAlRegistrar, ProveedorActivoAlGenerar = true,
                ImporteTotal = 121m, UsuarioId = s.UsuarioId, RegistradaEn = s.Orden.RegistradaEn,
                Estado = i == 0 ? EstadoOrdenPagoProveedor.Anulada : EstadoOrdenPagoProveedor.Pendiente,
                MotivoAnulacion = i == 0 ? "Prueba histórica" : null, AnuladaPorId = i == 0 ? s.UsuarioId : null,
                AnuladaEn = i == 0 ? s.Orden.RegistradaEn : null,
            });
            await db.SaveChangesAsync();
            await db.Proveedores.Where(x => x.Id == s.Orden.ProveedorId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false));
        });
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var filtro = new FiltroOrdenes(s.Orden.Fecha, s.Orden.Fecha, s.Orden.ProveedorId, null);
        var a = await repo.OrdenesAsync(filtro);
        var b = await repo.OrdenesAsync(filtro with { Pagina = 2 });
        Assert.Equal(21, a.Total); Assert.Equal(20, a.Items.Count); Assert.Single(b.Items);
        var items = a.Items.Concat(b.Items).ToArray();
        Assert.Equal(items.Select(x => x.Id).OrderDescending(), items.Select(x => x.Id));
        Assert.Equal(21, items.Select(x => x.Id).Distinct().Count());
        Assert.All(items, o => { Assert.StartsWith("OPP-", o.Numero); Assert.True(o.Proveedor.ActivoAlGenerar); Assert.False(o.Proveedor.ActivoActual); });
        Assert.Single((await repo.OrdenesAsync(filtro with { Estado = EstadoOrdenPagoProveedor.Anulada })).Items);
        Assert.Equal(20, (await repo.OrdenesAsync(filtro with { Estado = EstadoOrdenPagoProveedor.Pendiente })).Total);
        Assert.Empty((await repo.OrdenesAsync(filtro with { Desde = s.Orden.Fecha.AddDays(1), Hasta = null })).Items);
        var proveedores = await repo.ProveedoresHistoricosAsync(s.Orden.ProveedorCuit, 1);
        Assert.False(Assert.Single(proveedores.Items).Activo);
        var detalle = await repo.OrdenAsync(s.Orden.Id, s.Orden.Fecha);
        Assert.NotNull(detalle); Assert.Equal(s.Orden.ProveedorRazonSocial, detalle.Proveedor.RazonSocial);
        foreach (var invalido in new[] { filtro with { Pagina = 0 }, filtro with { ProveedorId = 0 }, filtro with { Estado = (EstadoOrdenPagoProveedor)99 }, filtro with { Desde = s.Orden.Fecha.AddDays(1) } })
            Assert.Equal(400, (await Assert.ThrowsAsync<ReglaPagoException>(() => repo.OrdenesAsync(invalido))).EstadoHttp);
    }
}
