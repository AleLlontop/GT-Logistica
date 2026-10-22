using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class PreparacionSnapshotAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    internal static string LeerSql()
    {
        var directorio=new DirectoryInfo(AppContext.BaseDirectory);
        while(directorio is not null)
        {
            var archivo=Path.Combine(directorio.FullName,"backend","scripts","habilitar-snapshot-reportes.sql");
            if(File.Exists(archivo)) return File.ReadAllText(archivo);
            directorio=directorio.Parent;
        }
        throw new FileNotFoundException("No se encontró el script administrado de Snapshot.");
    }

    [Fact]
    public async Task ScriptAdministradoEsIdempotenteYNoCambiaRCSI()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var detector=new DetectorSnapshotAnalisis(db);
            var previo=await detector.ConsultarAsync();
            var nombre=db.Database.GetDbConnection().Database;
            Assert.StartsWith("GtLogistica_Test_",nombre);
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            for(var i=0;i<2;i++)
            {
                await using var cmd=admin.CreateCommand();
                cmd.CommandText=LeerSql().Replace("$(Base015)",nombre,StringComparison.Ordinal);
                await cmd.ExecuteNonQueryAsync();
                var actual=await detector.ConsultarAsync();
                Assert.True(actual.Disponible);
                Assert.Equal(previo.ReadCommittedSnapshot,actual.ReadCommittedSnapshot);
            }
        });
    }

    [Fact]
    public async Task ScriptRechazaBaseSistemaYTransaccionSinConfigurarla()
    {
        await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
        await admin.OpenAsync();
        await using var cmd=admin.CreateCommand();
        cmd.CommandText=LeerSql().Replace("$(Base015)","master",StringComparison.Ordinal);
        var sistema=await Assert.ThrowsAsync<SqlException>(()=>cmd.ExecuteNonQueryAsync());
        Assert.Equal(50015,sistema.Number);
        await using var tx=(SqlTransaction)await admin.BeginTransactionAsync();
        cmd.Transaction=tx;
        var dentro=await Assert.ThrowsAsync<SqlException>(()=>cmd.ExecuteNonQueryAsync());
        Assert.Equal(50015,dentro.Number);
        await tx.RollbackAsync();
    }
}
