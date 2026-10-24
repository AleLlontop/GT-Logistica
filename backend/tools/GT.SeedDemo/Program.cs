using System.Reflection;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Archivos;
using GT.Application.Choferes.Documentacion;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GT.SeedDemo;
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if(args.Contains("--self-test")) { Safety.Tests(); Console.WriteLine("Guardias locales: OK"); return 0; }
            var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var env=config["ASPNETCORE_ENVIRONMENT"];
            var enabled=config["GT_SEED_DEMO"];
            if(env!="Development" || enabled!="true") { Console.WriteLine("DEMO inactiva: requiere Development y GT_SEED_DEMO=true."); return 0; }
            if(!args.Contains("--confirm")) throw new InvalidOperationException("Falta --confirm.");
            var cs=config.GetConnectionString("Gt") ?? throw new InvalidOperationException("Falta ConnectionStrings__Gt externa.");
            Safety.Validate(env,enabled,cs);
            var b=new SqlConnectionStringBuilder(cs);
            Console.WriteLine($"Environment: {env}\nServer: {b.DataSource}\nDatabase: {b.InitialCatalog}");
            var clock=new DemoClock();
            var services=new ServiceCollection().AddLogging(x=>x.SetMinimumLevel(LogLevel.Error));
            services.AddSingleton<IConfiguration>(config).AddSingleton<TimeProvider>(clock);
            services.AddDbContext<GtDbContext>(x=>x.UseSqlServer(cs));
            // Registro exclusivo del ejecutable: no ejecuta Program de la API ni cambia su arranque.
            foreach(var assembly in new[]{typeof(GT.Application.Viajes.CrearViaje).Assembly,typeof(GtDbContext).Assembly,typeof(GT.Api.Pagos.SerializadorPagos).Assembly})
            foreach(var type in assembly.GetExportedTypes().Where(t=>t.IsClass&&!t.IsAbstract&&!t.ContainsGenericParameters&&t.GetConstructors().Length>0))
            {
                services.AddScoped(type);
                foreach(var i in type.GetInterfaces().Where(i=>i.Namespace?.StartsWith("GT.Application.")==true || i.Namespace=="GT.Infrastructure.Seguridad")) services.AddScoped(i,type);
            }
            services.AddSingleton(GT.Application.Reportes.OpcionesDeReporte.PorDefecto);
            var files=config["GT_ARCHIVOS_RUTA"] ?? throw new InvalidOperationException("Falta GT_ARCHIVOS_RUTA externa.");
            services.AddSingleton<IAlmacenDeArchivos>(sp=>new AlmacenDeArchivos(files,sp.GetRequiredService<ILogger<AlmacenDeArchivos>>()));
            QuestPDF.Settings.License=QuestPDF.Infrastructure.LicenseType.Community;
            await using var provider=services.BuildServiceProvider();
            await using var scope=provider.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
            if(args.Contains("--prepare"))
            {
                await db.Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<GT.Infrastructure.DatosIniciales.SembradorInicial>().SembrarAsync(config["GT_ADMIN_PASSWORD_INICIAL"]);
                // Opción administrada del tooling, fuera de la API y fuera de transacciones.
                await db.Database.ExecuteSqlRawAsync("ALTER DATABASE [GtLogistica] SET ALLOW_SNAPSHOT_ISOLATION ON;");
            }
            // Exclusión del seeder entre procesos/Compose; no se usa para operaciones productivas.
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sp_getapplock @Resource=N'GT.SeedDemo.DEMO015',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; IF @r<0 THROW 51015,'Otro seeder está ejecutándose.',1;");
            var demo=new Dataset(scope.ServiceProvider,clock);
            if(args.Contains("--summary")) { await demo.Summary(); return 0; }
            if(!args.Contains("--seed")&&!args.Contains("--reset-demo")) throw new InvalidOperationException("Indicá --seed, --reset-demo o --summary.");
            await demo.CaptureBefore();
            if(args.Contains("--reset-demo")) await demo.Reset();
            await demo.Seed();
            await demo.Summary();
            return 0;
        }
        catch(Exception e) { Console.Error.WriteLine($"Seed detenido: {e.GetType().Name}: {SafeMessage(e)}"); return 1; }
    }
    static string SafeMessage(Exception e)=>e is SqlException?"SQL rechazó la operación. No se imprimen parámetros ni credenciales.":e.Message;
}
public sealed class DemoClock:TimeProvider
{
    public DateTimeOffset Now {get;set;}=new(2026,7,1,15,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>Now;
    public void At(int month,int day=20)=>Now=new DateTimeOffset(2026,month,day,15,0,0,TimeSpan.Zero);
}
public static class Safety
{
    public static void Validate(string? env,string? enabled,string cs)
    {
        var b=new SqlConnectionStringBuilder(cs);
        var server=b.DataSource.ToLowerInvariant();
        if(server.StartsWith("tcp:")) server=server[4..];
        var host=server.Split(',')[0];
        if(env!="Development"||enabled!="true"||b.InitialCatalog!="GtLogistica"||!new[]{"localhost","127.0.0.1","::1","sqlserver"}.Contains(host))
            throw new InvalidOperationException("Destino rechazado: sólo Development, GT_SEED_DEMO=true, SQL local conocido y Database=GtLogistica.");
    }
    public static void Tests()
    {
        foreach(var host in new[]{"localhost,1433","127.0.0.1,1433","sqlserver,1433"}) Validate("Development","true",$"Server={host};Database=GtLogistica;Integrated Security=true");
        foreach(var tuple in new[]{("Production","true","localhost","GtLogistica"),("Development","false","localhost","GtLogistica"),("Development","true","remote.example","GtLogistica"),("Development","true","localhost","Otra")})
        { bool rejected=false;try {Validate(tuple.Item1,tuple.Item2,$"Server={tuple.Item3};Database={tuple.Item4};Integrated Security=true");}catch(InvalidOperationException){rejected=true;}if(!rejected)throw new Exception("Guardia insegura."); }
    }
}
