using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Reportes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Infraestructura;

/// <summary>Opt-in de Snapshot sólo en pruebas analíticas. AplicacionDePrueba original conserva OFF.</summary>
public class AplicacionAnalisisDePrueba : AplicacionDePrueba, IAsyncLifetime
{
    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await EnLaBaseAsync(async db =>
        {
            var nombre=db.Database.GetDbConnection().Database;
            if(!nombre.StartsWith("GtLogistica_Test_",StringComparison.Ordinal))
                throw new InvalidOperationException("Snapshot de prueba sólo se prepara en base efímera.");
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            await using var cmd=admin.CreateCommand();
            cmd.CommandText=PreparacionSnapshotAnalisisTests.LeerSql().Replace("$(Base015)",nombre,StringComparison.Ordinal);
            await cmd.ExecuteNonQueryAsync();
            if(!(await new DetectorSnapshotAnalisis(db).ConsultarAsync()).Disponible)
                throw new InvalidOperationException("No se pudo preparar Snapshot en la base de prueba.");
        });
    }
}
