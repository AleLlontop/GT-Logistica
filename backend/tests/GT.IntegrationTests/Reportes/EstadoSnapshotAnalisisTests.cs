using System.Data;
using GT.Application.Reportes.Analisis;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class EstadoSnapshotAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task DetectorSoloLee_OffOnYRCSISeparados_ConservaConexion()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var detector=new DetectorSnapshotAnalisis(db);
            var previo=await detector.ConsultarAsync();
            // Cada prueba establece su precondición: la otra prueba comparte esta base efímera.
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            var nombre=db.Database.GetDbConnection().Database.Replace("]","]]",StringComparison.Ordinal);
            await using var comando=admin.CreateCommand();
            comando.CommandText=$"ALTER DATABASE [{nombre}] SET ALLOW_SNAPSHOT_ISOLATION OFF";
            await comando.ExecuteNonQueryAsync();
            var inicial=await detector.ConsultarAsync();
            Assert.Equal(EstadoSnapshotAnalisis.Desactivado,inicial.Estado);
            Assert.Equal(previo.ReadCommittedSnapshot,inicial.ReadCommittedSnapshot);
            Assert.False(inicial.Disponible);
            Assert.Equal(ConnectionState.Closed,db.Database.GetDbConnection().State);
            comando.CommandText=$"ALTER DATABASE [{nombre}] SET ALLOW_SNAPSHOT_ISOLATION ON";
            await comando.ExecuteNonQueryAsync();
            await db.Database.OpenConnectionAsync();
            var activado=await detector.ConsultarAsync();
            Assert.True(activado.Disponible);
            Assert.Equal("ON",activado.Descripcion);
            Assert.Equal(inicial.ReadCommittedSnapshot,activado.ReadCommittedSnapshot);
            Assert.Equal(ConnectionState.Open,db.Database.GetDbConnection().State);
            await db.Database.CloseConnectionAsync();
        });
    }

    [Fact]
    public async Task EstadoTransitorioEsVisibleYNoSeConsideraDisponible()
    {
        await app.EnLaBaseAsync(async db =>
        {
            // Una escritura real pendiente retiene el paso a ON; sólo usa una tabla sintética de test.
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE dbo.SondaSnapshot015 (Id int NOT NULL PRIMARY KEY, Valor int NOT NULL); INSERT dbo.SondaSnapshot015 VALUES (1,0)");
            var cadena=db.Database.GetConnectionString()!;
            await using var writer=new SqlConnection(cadena);
            await writer.OpenAsync();
            await using var tx=(SqlTransaction)await writer.BeginTransactionAsync();
            await using var cambio=writer.CreateCommand();
            cambio.Transaction=tx;
            cambio.CommandText="UPDATE dbo.SondaSnapshot015 SET Valor=1 WHERE Id=1";
            await cambio.ExecuteNonQueryAsync();
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            var nombre=db.Database.GetDbConnection().Database.Replace("]","]]",StringComparison.Ordinal);
            await using var preparar=admin.CreateCommand();
            preparar.CommandText=$"ALTER DATABASE [{nombre}] SET ALLOW_SNAPSHOT_ISOLATION OFF";
            await preparar.ExecuteNonQueryAsync();
            preparar.CommandText=$"ALTER DATABASE [{nombre}] SET ALLOW_SNAPSHOT_ISOLATION ON";
            var habilitacion=preparar.ExecuteNonQueryAsync();
            var detector=new DetectorSnapshotAnalisis(db);
            using var limite=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            DiagnosticoSnapshotAnalisis estado;
            try
            {
                // Esperar condición SQL observable, no un retardo elegido para adivinar la carrera.
                do { estado=await detector.ConsultarAsync(limite.Token); }
                while(estado.Estado==EstadoSnapshotAnalisis.Desactivado);
                Assert.Equal(EstadoSnapshotAnalisis.Activando,estado.Estado);
                Assert.False(estado.Disponible);
            }
            finally { await tx.RollbackAsync(); }
            await habilitacion;
            Assert.True((await detector.ConsultarAsync()).Disponible);
        });
    }
}
