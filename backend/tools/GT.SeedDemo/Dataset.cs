using System.Collections;
using System.Reflection;
using GT.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using GT.Domain.Usuarios;
using GT.Domain.Personas;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.Application.Viajes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Application.Proveedores;
using GT.Application.Facturacion;
using GT.Application.Liquidaciones;
using GT.Application.Caja;
using GT.Application.Adelantos;

namespace GT.SeedDemo;
public sealed partial class Dataset(IServiceProvider services,DemoClock clock)
{
    public const string Mark="DEMO015";
    public const int Version=2;
    readonly GtDbContext db=services.GetRequiredService<GtDbContext>();
    readonly Random random=new(15015);
    readonly Ownership ownership=services.GetRequiredService<Ownership>();
    public const string AdminUsername="administrador";
    static readonly string[] Usernames=[AdminUsername,"gerencia","operaciones","tesoreria"];
    T Get<T>() where T:notnull { db.ChangeTracker.Clear(); return services.GetRequiredService<T>(); }
    static T Ok<T>(T result)
    {
        var error=typeof(T).GetProperty("Error")?.GetValue(result)?.ToString();
        if(error is not null && error!="Ninguno") throw new InvalidOperationException($"{typeof(T).Name}: {error}, campo {typeof(T).GetProperty("Campo")?.GetValue(result)}");
        return result;
    }
    static string Cuit(int number,string prefix="30")
    {
        var body=prefix+number.ToString("D8");int[] weights=[5,4,3,2,7,6,5,4,3,2];
        int digit=11-body.Select((c,i)=>(c-'0')*weights[i]).Sum()%11;
        if(digit==11)digit=0;if(digit==10)return Cuit(number+1000000,prefix);
        return body+digit;
    }
    static Guid Key(int n)=>new($"15015000-0001-4000-8000-{n:D12}");
    int actor;
    bool generated;
    async Task Save(params object[] rows){db.AddRange(rows);await db.SaveChangesAsync();}
    public async Task Seed()
    {
        var ready=ownership.Current;
        if(ready is { Complete:true } && ready.Version==Version)
        { await Validate();Console.WriteLine("Dataset DEMO015 existente. No se generan registros.");return; }
        if(ready is not null || await db.Usuarios.AnyAsync(u=>u.Username.StartsWith("demo.DEMO015")))
            throw new InvalidOperationException("Dataset DEMO incompleto o de otra versión; usá --reset-demo --confirm. No se regenera automáticamente.");
        await Preflight();
        var reserved=Enumerable.Range(0,30).SelectMany(i=>new[]{Key(20000+i),Key(30000+i)}).ToArray();
        if(await db.ResultadosOperacionPago.AnyAsync(r=>reserved.Contains(r.Clave)))
            throw new InvalidOperationException("Hay claves económicas reservadas DEMO015 existentes sin marca de versión válida. No se reaplican ni se anulan operaciones.");
        generated=true;
        Console.WriteLine($"Creando DEMO015 versión{Version}: pertenencia por PK técnica; fallo deja metadata incompleta y requiere reset explícito.");
        var pass=Environment.GetEnvironmentVariable("GT_DEMO_PASSWORD");
        if(string.IsNullOrEmpty(pass))throw new InvalidOperationException("Definí GT_DEMO_PASSWORD externa para las cuentas locales demo.");
        var hash=Get<GT.Infrastructure.Seguridad.IHasheadorPassword>().Hashear(pass);
        var roles=await db.Roles.ToListAsync();
        if(roles.Count==0)throw new InvalidOperationException("Faltan semillas estructurales. Ejecutá --prepare.");
        await ownership.Start(db);
        foreach(var pair in new[]{(AdminUsername,CodigosRol.AdministradorSistema),("gerencia",CodigosRol.Gerencia),("operaciones",CodigosRol.Trafico),("tesoreria",CodigosRol.Administracion)})
        {
            var name=pair.Item1;
            var user=new Usuario{Username=name,UsernameNormalizado=name.ToUpperInvariant(),Email=name+"@gtlogistica.invalid",EmailNormalizado=(name+"@gtlogistica.invalid").ToLowerInvariant(),PasswordHash=hash,FechaAlta=clock.Now.UtcDateTime,PasswordActualizadaEn=clock.Now.UtcDateTime,Roles=[roles.Single(r=>r.Codigo==pair.Item2)]};
            await Save(user);if(actor==0)actor=user.Id;
        }
        if(!await db.EmpresaEmisora.AnyAsync()) await Save(new GT.Domain.Facturacion.EmpresaEmisora{RazonSocial="G&T Logística S.A.",Cuit=Cuit(98000001),Domicilio="Av. 44 1850, La Plata",CondicionIva="Responsable inscripto",PuntoDeVenta="9015",Email="administracion@gtlogistica.invalid"});
        else if((await db.EmpresaEmisora.SingleAsync()).ObligatoriosFaltantes().Count>0)throw new InvalidOperationException("Empresa emisora existente incompleta. No se modifica configuración no DEMO.");
        await Masters();
        await Operations();
        await Validate();
        await ownership.Complete(db);
        Console.WriteLine($"Dataset DEMO015 v{Version} listo. Cuenta administradora local: {AdminUsername}; contraseña externa GT_DEMO_PASSWORD.");
    }
    readonly List<int> people=[],drivers=[],driverCarriers=[],carriers=[],clients=[],vehicles=[],types=[],providers=[],banks=[];
    async Task Masters()
    {
        var names=Presentation.Names;
        var surnames=Presentation.Surnames;
        for(int i=0;i<100;i++)
        {
            var r=Ok(await Get<GT.Application.Usuarios.Personas.CrearPersona>().EjecutarAsync(new(names[i%10],surnames[i/10],(98000000+i).ToString(),i<50?"chofer":"empleado","115550"+i.ToString("D4"),Presentation.Email(names[i%10]+surnames[i/10],"personal"),new DateOnly(1970+i%30,1+i%12,1+i%27))));
            people.Add(r.Persona!.Id);
        }
        for(int i=0;i<20;i++)
        {
            var r=Ok(await Get<GT.Application.Choferes.Transportistas.CrearTransportista>().EjecutarAsync(new(Presentation.Carriers[i],Cuit(97000000+i),"juridica","115551"+i.ToString("D4"),Presentation.Email(Presentation.Carriers[i]))));
            carriers.Add(r.Transportista!.Id);
        }
        for(int i=0;i<35;i++)
        {
            var r=Ok(await Get<GT.Application.Viajes.Clientes.CrearCliente>().EjecutarAsync(new(Presentation.Clients[i],Cuit(96000000+i),"115552"+i.ToString("D4"),Presentation.Email(Presentation.Clients[i],"facturacion"),Presentation.Addresses[i%Presentation.Addresses.Length])));
            clients.Add(r.Cliente!.Id);
        }
        var companyCuit=await db.EmpresaEmisora.Select(e=>e.Cuit).SingleAsync();
        var own=await db.Transportistas.SingleOrDefaultAsync(t=>t.Cuit==companyCuit);
        int ownId;
        if(own is null)ownId=Ok(await Get<GT.Application.Choferes.Transportistas.CrearTransportista>().EjecutarAsync(new("G&T Transporte Propio",companyCuit,"juridica","1155519999","operaciones@gtlogistica.invalid"))).Transportista!.Id;
        else ownId=own.Id;
        for(int i=0;i<50;i++)
        {
            var p=await db.Personas.SingleAsync(p=>p.Id==people[i]);
            var r=Ok(await Get<GT.Application.Choferes.CrearChofer>().EjecutarAsync(new(p.Nombre,p.Apellido,p.Dni,Cuit(int.Parse(p.Dni),"20"),p.FechaNacimiento.ToString("yyyy-MM-dd"),p.Telefono,p.Email,i<5?ownId:carriers[i<42?i%16:16+(i-42)%4])));
            drivers.Add(r.Chofer!.Id);driverCarriers.Add(i<5?ownId:carriers[i<42?i%16:16+(i-42)%4]);
        }
        foreach(var name in Presentation.VehicleTypes)
        {
            var existing=await db.TiposVehiculo.SingleOrDefaultAsync(t=>t.Nombre==name);
            if(existing is not null && !existing.Activo)throw new InvalidOperationException("Tipo de vehículo existente inactivo; no se modifica configuración ajena.");
            types.Add(existing?.Id ?? Ok(await Get<GT.Application.Flota.TiposVehiculo.GestionTiposVehiculo>().CrearAsync(new(name))).Tipo!.Id);
        }
        for(int i=0;i<75;i++)
        {
            int t=i<15?0:i<25?1:i<35?2:i<45?3:i<55?4:i<65?5:6;
            // Disponible al alta está permitido sin documentos; la asignación usa las reglas reales.
            var model=Presentation.Vehicle(t,i);
            var r=Ok(await Get<GT.Application.Flota.CrearVehiculo>().EjecutarAsync(new($"ZZ{150+i:000}ZZ",model.Brand,model.Model,types[t],carriers[i%20],"fueraDeServicio")));
            vehicles.Add(r.Vehiculo!.Id);
        }
        foreach(var x in Presentation.DocumentTypes)
        {
            var existing=await db.DocumentacionTipos.SingleOrDefaultAsync(t=>t.Nombre==x.Name);
            if(existing is null)Ok(await Get<GestionTiposDocumentacion>().CrearAsync(new(x.Name,30,x.Scope)));
            else if(!existing.Activo || existing.Ambito.ToString().ToLowerInvariant()!=x.Scope)
                throw new InvalidOperationException("Tipo documental existente incompatible; no se modifica configuración ajena.");
        }
        var insurance=await db.DocumentacionTipos.SingleAsync(t=>t.Nombre==Presentation.DocumentTypes[1].Name);
        for(int i=0;i<55;i++)
        {
            Ok(await Get<GT.Application.Flota.Documentacion.CargarDocumentoVehiculo>().EjecutarAsync(vehicles[i],new(insurance.Id,$"SEG-{150150+i:000000}","2025-01-01","2027-12-31"),null));
            var v=await db.Vehiculos.SingleAsync(v=>v.Id==vehicles[i]);
            Ok(await Get<GT.Application.Flota.ModificarVehiculo>().EjecutarAsync(v.Id,new(v.Patente,v.Marca,v.Modelo,v.TipoVehiculoId,v.TransportistaId,"disponible")));
        }
        for(int i=0;i<30;i++)
        {
            var p=await Get<GestionarProveedores>().GuardarAsync(null,i<20?new ProveedorPeticion("proveedorNormal",Presentation.Providers[i],Cuit(95000000+i),"1155530000",Presentation.Email(Presentation.Providers[i],"pagos"),Presentation.Providers[i],Presentation.Addresses[i%Presentation.Addresses.Length]):new ProveedorPeticion("fletero",NombreComercial:Presentation.Carriers[i-20],TransportistaId:carriers[i-20]));
            providers.Add(p.Id);
        }
        foreach(var name in Presentation.Banks)
        {
            var existing=await db.Bancos.SingleOrDefaultAsync(b=>b.Nombre==name);
            if(existing is not null && !existing.Activo)throw new InvalidOperationException("Banco existente inactivo; no se modifica configuración ajena.");
            banks.Add(existing?.Id ?? (await Get<GestionBancosPagos>().CrearAsync(new(name),default)).Id);
        }
        Console.WriteLine("Maestros completos:100 personas,35 clientes,20 transportistas externos + propio,50 choferes,75 vehículos,30 proveedores,6 bancos.");
    }
    async Task Preflight()
    {
        // Evita recuperar por DNI una persona ajena inactiva (el ABM permite reingreso).
        var dnis=Enumerable.Range(98000000,100).Select(i=>i.ToString()).ToArray();
        var cuits=Enumerable.Range(0,35).Select(i=>Cuit(96000000+i)).ToArray();
        var carrierCuits=Enumerable.Range(0,20).Select(i=>Cuit(97000000+i)).ToArray();
        var providerCuits=Enumerable.Range(0,20).Select(i=>Cuit(95000000+i)).ToArray();
        var plates=Enumerable.Range(150,75).Select(i=>$"ZZ{i:000}ZZ").ToArray();
        if(await db.Personas.AnyAsync(p=>dnis.Contains(p.Dni)) || await db.Clientes.AnyAsync(c=>cuits.Contains(c.Cuit))
            || await db.Transportistas.AnyAsync(t=>carrierCuits.Contains(t.Cuit)) || await db.Proveedores.AnyAsync(p=>providerCuits.Contains(p.Cuit))
            || await db.Vehiculos.AnyAsync(v=>plates.Contains(v.Patente)) || await db.Usuarios.AnyAsync(u=>Usernames.Contains(u.Username)))
            throw new InvalidOperationException("Identificador reservado ocupado por datos sin metadata DEMO015. No se adopta ni modifica ningún registro ajeno.");
    }
}
