using System.Data;
using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class CoordinadorFotoAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task MaterializaEnUnaSnapshot_SecuencialYSinTracking_YLiberaAntesDeConsumirDto()
    {
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var nombre="Foto015 "+Guid.NewGuid().ToString("N");
        db.Bancos.Add(new Banco { Nombre=nombre });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var lector=scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        var datos=await lector.LeerAsync(async (ctx,ct)=>
        {
            Assert.Same(db,ctx);
            var transaccion=ctx.Database.CurrentTransaction;
            Assert.NotNull(transaccion);
            Assert.Equal(IsolationLevel.Snapshot,transaccion.GetDbTransaction().IsolationLevel);
            var cantidad=await ctx.Bancos.AsNoTracking().CountAsync(b=>b.Nombre==nombre,ct);
            var filas=await ctx.Bancos.AsNoTracking().Where(b=>b.Nombre==nombre).Select(b=>b.Nombre).ToArrayAsync(ct);
            Assert.Same(transaccion,ctx.Database.CurrentTransaction);
            return (Cantidad:cantidad,Filas:filas);
        });
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(datos.Cantidad,datos.Filas.Length);
        Assert.Equal(nombre,Assert.Single(datos.Filas));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ErrorNoDejaTransaccionAbiertaNiReintentaLectura()
    {
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var lector=scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        var intentos=0;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>lector.LeerAsync<int>(async (ctx,ct)=>
        {
            intentos++;
            await ctx.Bancos.AsNoTracking().CountAsync(ct);
            throw new InvalidOperationException("Fallo del harness");
        }));
        Assert.Equal(1,intentos);
        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(0,await lector.LeerAsync(async (ctx,ct)=>await ctx.Bancos.CountAsync(b=>b.Id<0,ct)));
    }

    [Fact]
    public async Task NoPermiteConsultaDiferidaNiTransaccionAnidada()
    {
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var lector=scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>lector.LeerAsync((ctx,ct)=>Task.FromResult(ctx.Bancos.AsNoTracking())));
        Assert.Null(db.Database.CurrentTransaction);
        await using var externa=await db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>lector.LeerAsync((ctx,ct)=>Task.FromResult(1)));
        Assert.Same(externa,db.Database.CurrentTransaction);
    }
}
