using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class RepositorioChequesPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task UniversoSqlCoincideConDominio_SeisEstados_BancoInactivoYDetalleFueraDeAlerta()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18);
        var cheques = await app.ConAlcanceAsync(async db =>
        {
            var lista = new List<Cheque>();
            foreach (var estado in Enum.GetValues<EstadoCheque>())
                foreach (var dias in new[] { -1, 0, 30, 31 }) lista.Add(new() { OrdenPagoProveedorId = s.Orden.Id,
                    BancoId = s.Banco.Id, RenglonId = Guid.NewGuid(), NumeroCheque = $"{estado}-{dias}", Importe = 1m,
                    Fecha = hoy.AddDays(-2), FechaVencimiento = hoy.AddDays(dias), EstadoCheque = estado });
            db.AddRange(lista); await db.SaveChangesAsync();
            await db.Bancos.Where(b => b.Id == s.Banco.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Activo, false));
            return lista;
        });
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var filtro = new FiltroCheques(s.Banco.Id, null, null, null);
        var resultado = await repo.ChequesAsync(filtro, hoy);
        Assert.Equal(18, resultado.Total);
        Assert.Equal(cheques.Where(c => VencimientoCheque.EnAlerta(c.FechaVencimiento, hoy)).OrderBy(c => c.FechaVencimiento).ThenBy(c => c.Id).Select(c => c.Id), resultado.Items.Select(c => c.Id));
        Assert.All(resultado.Items, c =>
        {
            Assert.False(c.Banco.Activo);
            Assert.Equal(NombresVencimientoCheque.EnJson(VencimientoCheque.Evaluar(c.FechaVencimiento, hoy)), c.SituacionVencimiento);
        });
        Assert.Equal(18, (await repo.ChequesAsync(filtro with { DesdeVencimiento = hoy.AddDays(-30), HastaVencimiento = hoy.AddDays(100) }, hoy)).Total);
        Assert.Equal(3, (await repo.ChequesAsync(filtro with { Estado = EstadoCheque.Anulado }, hoy)).Total);
        Assert.Contains(await repo.BancosHistoricosAsync(), b => b.Id == s.Banco.Id && !b.Activo);
        var fuera = cheques.First(c => c.FechaVencimiento == hoy.AddDays(31));
        var detalle = await repo.ChequeAsync(fuera.Id, hoy);
        Assert.NotNull(detalle); Assert.Equal("fueraDelPeriodoDeAlerta", detalle.SituacionVencimiento); Assert.Empty(detalle.Historial);
        Assert.Equal(EstadoCheque.Pendiente, detalle.Estado);
    }
}
