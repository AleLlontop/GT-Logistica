using Microsoft.EntityFrameworkCore;
using GT.Infrastructure.Persistencia;

namespace GT.SeedDemo;

public static class SeedTests
{
    public static void Run()
    {
        static void Check(bool condition,string name) { if(!condition)throw new InvalidOperationException("Test seed: "+name); }
        var manifest=new Ownership.Manifest { Complete=true };
        for(int i=0;i<10000;i++)manifest.Keys.Add("Viajes|"+i);
        manifest.Keys.Add("UsuarioRoles|15|2");
        var restored=Ownership.Decode(Ownership.Encode(manifest));
        Check(restored.Version==Dataset.Version && restored.Complete && restored.Keys.SetEquals(manifest.Keys),"roundtrip exacto y pertenencia de PK compuesta");
        Check(!restored.Keys.Contains("Viajes|10001"),"una PK ajena nunca se adopta por relación/nombre");
        foreach(var forbidden in new[]{"DEMO015","deMo","TEST","prueba","fake","Sample"})
            Check(Dataset.ContainsForbidden("prefijo "+forbidden+" sufijo"),"anti-demo case insensitive");
        foreach(var text in Presentation.Clients.Concat(Presentation.Carriers).Concat(Presentation.Providers)
            .Concat(Presentation.Banks).Concat(Presentation.VehicleTypes).Concat(Presentation.Names).Concat(Presentation.Surnames)
            .Concat(Presentation.Addresses).Concat(Presentation.Cargo).Concat(Presentation.Expenses)
            .Concat(Presentation.DocumentTypes.Select(t=>t.Name)).Concat(Presentation.Routes.SelectMany(r=>new[]{r.Origin,r.Destination})))
            Check(!Dataset.ContainsForbidden(text),"catálogo presentable");
        Check(Presentation.Clients.Distinct().Count()==35 && Presentation.Carriers.Distinct().Count()==20 && Presentation.Providers.Distinct().Count()==20,"nombres distintos");
        using var db=new GtDbContext(new DbContextOptionsBuilder<GtDbContext>().UseSqlServer("Server=localhost;Database=GtLogistica;Integrated Security=true").Options);
        foreach(var entity in db.Model.GetEntityTypes())
        foreach(var property in entity.FindPrimaryKey()!.Properties)
            Check(property.ClrType==typeof(int) || property.ClrType==typeof(long),"PK soportada "+entity.Name);
        var user=db.Model.FindEntityType(typeof(GT.Domain.Usuarios.Usuario))!;
        Check(!Dataset.Visible(user,user.FindProperty("PasswordHash")!) && Dataset.Visible(user,user.FindProperty("Username")!),"excepción de secretos, username visible");
        Console.WriteLine("Tests propios: metadata/PK/anti-demo/catálogos/modelo OK.");
    }
}
