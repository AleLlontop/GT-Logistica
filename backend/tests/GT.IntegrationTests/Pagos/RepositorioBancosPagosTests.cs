using GT.Application.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class RepositorioBancosPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task NombreVersionYEstadoSeEditanSeparadosConservandoId()
    {
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var banco = await repo.CrearBancoAsync(new("  Banco  Técnico  "));
        Assert.Equal("Banco  Técnico", banco.Nombre);
        Assert.True(banco.Activo);
        Assert.False((await repo.ActivarBancoAsync(banco.Id, false))!.Activo);
        var editado = await repo.EditarBancoAsync(banco.Id, new("Nombre nuevo", banco.Version));
        Assert.Equal(banco.Id, editado!.Id);
        Assert.False(editado.Activo);
        Assert.Equal(banco.Version + 1, editado.Version);
        var conflicto = await Assert.ThrowsAsync<ReglaPagoException>(() => repo.EditarBancoAsync(banco.Id, new("Otra edición", banco.Version)));
        Assert.Equal(409, conflicto.EstadoHttp);
        Assert.True((await repo.ActivarBancoAsync(banco.Id, true))!.Activo);
        await Assert.ThrowsAsync<ReglaPagoException>(() => repo.CrearBancoAsync(new("Nombre nuevo")));
        var invalido = await Assert.ThrowsAsync<ReglaPagoException>(() => repo.CrearBancoAsync(new(new string('X',201))));
        Assert.Equal("nombre", invalido.Campo);
        Assert.Equal(400, invalido.EstadoHttp);
    }
}
