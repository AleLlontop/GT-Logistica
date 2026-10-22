using GT.Domain.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GT.IntegrationTests.Pagos;

public class ProteccionMigracionPagosTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DownConDatosDeBancoUOPP_RechazaSinBorrarHistoria(bool soloBanco)
    {
        var app = new AplicacionDePrueba();
        try
        {
            await app.InitializeAsync();
            if (soloBanco) await app.EnLaBaseAsync(async db => { db.Bancos.Add(new Banco { Nombre = "Banco conservado" }); await db.SaveChangesAsync(); });
            else
            {
                var s = await EscenarioPagos.CrearAsync(app);
                await app.EnLaBaseAsync(db => db.Bancos.Where(b => b.Id == s.Banco.Id).ExecuteDeleteAsync());
            }
            var antes = await app.ConAlcanceAsync(async db => new[] { await db.Bancos.CountAsync(), await db.OrdenesPagoProveedor.CountAsync() });
            await app.EnLaBaseAsync(async db =>
            {
                Assert.False(db.Database.HasPendingModelChanges());
                var migrador = db.GetService<IMigrator>();
                var error = await Assert.ThrowsAnyAsync<Exception>(() => migrador.MigrateAsync("20261016162244_Modulo13Proveedores"));
                Assert.Equal(50014, Assert.IsType<SqlException>(error.GetBaseException()).Number);
            });
            await app.EnLaBaseAsync(async db =>
            {
                Assert.Contains("20261018164612_Modulo14GestionPagos", await db.Database.GetAppliedMigrationsAsync());
                Assert.Equal(antes, new[] { await db.Bancos.CountAsync(), await db.OrdenesPagoProveedor.CountAsync() });
            });
        }
        finally { await app.DisposeAsync(); }
    }
    [Fact]
    public async Task ModeloCoincideConSnapshot_YScriptIncrementalTieneIndicesYFKsSeparadas()
    {
        var app = new AplicacionDePrueba();
        try
        {
            await app.InitializeAsync();
            await app.EnLaBaseAsync(async db =>
            {
                Assert.False(db.Database.HasPendingModelChanges());
                var sql = db.GetService<IMigrator>().GenerateScript("20261016162244_Modulo13Proveedores", options: MigrationsSqlGenerationOptions.Idempotent);
                Assert.Contains("IX_OPPFactura_Vigente", sql); Assert.Contains("IX_Cheques_Banco_Numero", sql); Assert.Contains("IX_Bancos_Nombre", sql);
                Assert.Contains("OrdenPagoProveedorId", sql); Assert.Contains("Latin1_General_100_BIN2", sql);
                Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
                Directory.CreateDirectory("/tmp/gt014-final"); await File.WriteAllTextAsync("/tmp/gt014-final/migracion014.sql", sql);
            });
        }
        finally { await app.DisposeAsync(); }
    }
}
