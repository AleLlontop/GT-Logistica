using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Personas;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

internal static class DocumentacionMedicionAnalisis
{
    public static Task CrearAsync(AplicacionAnalisisDePrueba app) => app.EnLaBaseAsync(async db =>
    {
        var hoy = FechaHoyArgentina.Desde(TimeProvider.System.GetUtcNow());
        var transportista = await db.Transportistas.Where(t=>t.Activo).OrderBy(t=>t.Id).Select(t=>t.Id).FirstAsync();
        var tipos = new[]
        {
            new DocumentacionTipo { Nombre="Documento chofer sintético015", DiasAvisoVencimiento=30, Ambito=DocumentacionAmbito.Chofer },
            new DocumentacionTipo { Nombre="Documento vehículo sintético015", DiasAvisoVencimiento=30, Ambito=DocumentacionAmbito.Vehiculo },
        };
        var personas = Enumerable.Range(0,200).Select(i=>new Persona
        {
            Nombre=$"Chofer{i:D3}",Apellido="Sintético015",Dni=$"{90200000+i}",Tipo=TipoIntegrante.Chofer,
            Telefono="0000000000",Email=$"chofer{i}@example.invalid",FechaNacimiento=new(1980,1,1),
        }).ToArray();
        db.AddRange(tipos);db.AddRange(personas);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var choferes = personas.Select((p,i)=>new Chofer { PersonaId=p.Id,Cuil=Choferes.DatosDePrueba.CuilValidoPara(90200000+i),TransportistaId=transportista }).ToArray();
        db.AddRange(choferes);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        db.AddRange(choferes.Select((c,i)=>new Documentacion
        {
            ChoferId=c.Id,DocumentacionTipoId=tipos[0].Id,Numero=$"S015-C{i:D3}",FechaEmision=hoy.AddYears(-1),FechaVencimiento=hoy.AddDays(i%2==0?-1:5),
        }));
        var vehiculos=await db.Vehiculos.Select(v=>v.Id).ToArrayAsync();
        db.AddRange(vehiculos.Select((id,i)=>new DocumentacionVehiculo
        {
            VehiculoId=id,DocumentacionTipoId=tipos[1].Id,Numero=$"S015-V{i:D3}",FechaEmision=hoy.AddYears(-1),FechaVencimiento=hoy.AddDays(i%2==0?-1:5),
        }));
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
    });
}
