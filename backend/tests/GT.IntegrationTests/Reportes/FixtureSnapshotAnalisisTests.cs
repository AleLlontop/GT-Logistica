using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class FixtureSnapshotAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task OptInEstaON_YFixtureOriginalEsIndependiente_YSeEliminaAlCerrar()
    {
        await app.EnLaBaseAsync(async db=>Assert.True((await new DetectorSnapshotAnalisis(db).ConsultarAsync()).Disponible));
        var aparte=new AplicacionDePrueba();
        string nombre="";
        try
        {
            await aparte.InitializeAsync();
            await aparte.EnLaBaseAsync(async db=>
            {
                nombre=db.Database.GetDbConnection().Database;
                Assert.False((await new DetectorSnapshotAnalisis(db).ConsultarAsync()).Disponible);
            });
        }
        finally { await aparte.DisposeAsync(); }
        await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
        await admin.OpenAsync();
        await using var cmd=admin.CreateCommand();
        cmd.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@nombre";
        cmd.Parameters.AddWithValue("@nombre",nombre);
        Assert.Equal(0,Convert.ToInt32(await cmd.ExecuteScalarAsync()));
        await app.EnLaBaseAsync(async db=>Assert.True((await new DetectorSnapshotAnalisis(db).ConsultarAsync()).Disponible));
    }
}
