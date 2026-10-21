using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class SnapshotAnalisisOnTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task EscritorConfirmadoEntreCountYDetalle_NoMezclaLaFoto_NuevoRequestVeCambio()
    {
        await app.EnLaBaseAsync(async db=>
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.SondaFoto015 (Id int NOT NULL PRIMARY KEY, Importe decimal(18,2) NOT NULL); INSERT dbo.SondaFoto015 VALUES (1,100.00)");
        });
        using var alcance=app.Services.CreateScope();
        var lector=alcance.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        var primeraLectura=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var escritorConfirmado=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var limite=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var foto=lector.LeerAsync(async (db,ct)=>
        {
            var cantidad=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.SondaFoto015").SingleAsync(ct);
            primeraLectura.SetResult();
            await escritorConfirmado.Task.WaitAsync(ct);
            var total=await db.Database.SqlQueryRaw<decimal>("SELECT SUM(Importe) AS Value FROM dbo.SondaFoto015").SingleAsync(ct);
            var filas=await db.Database.SqlQueryRaw<decimal>("SELECT Importe AS Value FROM dbo.SondaFoto015").OrderBy(x=>x).ToArrayAsync(ct);
            return (Cantidad:cantidad,Total:total,Filas:filas);
        },limite.Token);
        try
        {
            await primeraLectura.Task.WaitAsync(limite.Token);
            await app.EnLaBaseAsync(async db=>
            {
                await using var transaccion=await db.Database.BeginTransactionAsync(limite.Token);
                await db.Database.ExecuteSqlRawAsync("UPDATE dbo.SondaFoto015 SET Importe=150.00 WHERE Id=1; INSERT dbo.SondaFoto015 VALUES (2,200.00)",limite.Token);
                await transaccion.CommitAsync(limite.Token);
            });
        }
        finally { escritorConfirmado.TrySetResult(); }
        var anterior=await foto;
        Assert.Equal(1,anterior.Cantidad);
        Assert.Equal(100m,anterior.Total);
        Assert.Equal(new[]{100m},anterior.Filas);
        var siguiente=await lector.LeerAsync(async (db,ct)=>new
        {
            Cantidad=await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.SondaFoto015").SingleAsync(ct),
            Total=await db.Database.SqlQueryRaw<decimal>("SELECT SUM(Importe) AS Value FROM dbo.SondaFoto015").SingleAsync(ct),
        });
        Assert.Equal(2,siguiente.Cantidad);
        Assert.Equal(350m,siguiente.Total);
    }
}
