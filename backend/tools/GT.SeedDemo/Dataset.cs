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
    public const int Version=1;
    readonly GtDbContext db=services.GetRequiredService<GtDbContext>();
    readonly Random random=new(15015);
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
        var ready=$"demo.DEMO015.v{Version}.ready";
        if(await db.Usuarios.AnyAsync(u=>u.Username==ready))
        { await Validate();Console.WriteLine("Dataset DEMO015 existente. No se generan registros.");return; }
        if(await db.Usuarios.AnyAsync(u=>u.Username.StartsWith("demo.DEMO015"))) throw new InvalidOperationException("Dataset DEMO incompleto o de otra versión; usá --reset-demo --confirm. No se regenera automáticamente.");
        var reserved=Enumerable.Range(0,30).SelectMany(i=>new[]{Key(20000+i),Key(30000+i)}).ToArray();
        if(await db.ResultadosOperacionPago.AnyAsync(r=>reserved.Contains(r.Clave)))
            throw new InvalidOperationException("Hay claves económicas reservadas DEMO015 existentes sin marca de versión válida. No se reaplican ni se anulan operaciones.");
        generated=true;
        Console.WriteLine("Creando DEMO015 versión1: operaciones por servicios reales; fallo deja marca incomplete y requiere reset explícito.");
        var pass=Environment.GetEnvironmentVariable("GT_DEMO_PASSWORD");
        if(string.IsNullOrEmpty(pass))throw new InvalidOperationException("Definí GT_DEMO_PASSWORD externa para las cuentas locales demo.");
        var hash=Get<GT.Infrastructure.Seguridad.IHasheadorPassword>().Hashear(pass);
        var roles=await db.Roles.ToListAsync();
        if(roles.Count==0)throw new InvalidOperationException("Faltan semillas estructurales. Ejecutá --prepare.");
        foreach(var pair in new[]{($"seed.v{Version}.incomplete",CodigosRol.AdministradorSistema),("gerencia",CodigosRol.Gerencia),("operaciones",CodigosRol.Trafico),("caja",CodigosRol.Administracion)})
        {
            var name=$"demo.DEMO015.{pair.Item1}";
            var user=new Usuario{Username=name,UsernameNormalizado=name.ToUpperInvariant(),Email=name+"@example.invalid",EmailNormalizado=(name+"@example.invalid").ToLowerInvariant(),PasswordHash=hash,FechaAlta=clock.Now.UtcDateTime,PasswordActualizadaEn=clock.Now.UtcDateTime,Roles=[roles.Single(r=>r.Codigo==pair.Item2)]};
            await Save(user);if(actor==0)actor=user.Id;
        }
        if(!await db.EmpresaEmisora.AnyAsync()) await Save(new GT.Domain.Facturacion.EmpresaEmisora{RazonSocial="Operador logístico DEMO015",Cuit=Cuit(98000001),Domicilio="Parque sintético DEMO015 15015",CondicionIva="Responsable inscripto",PuntoDeVenta="9015",Email="empresa.demo015@example.invalid"});
        else if((await db.EmpresaEmisora.SingleAsync()).ObligatoriosFaltantes().Count>0)throw new InvalidOperationException("Empresa emisora existente incompleta. No se modifica configuración no DEMO.");
        await Masters();
        await Operations();
        await Validate();
        var marker=await db.Usuarios.SingleAsync(u=>u.Id==actor);
        marker.Username=ready;marker.UsernameNormalizado=marker.Username.ToUpperInvariant();
        await db.SaveChangesAsync();
        Console.WriteLine($"Dataset DEMO015 v{Version} listo. Cuenta administradora local: {ready}; contraseña externa GT_DEMO_PASSWORD.");
    }
    readonly List<int> people=[],drivers=[],driverCarriers=[],carriers=[],clients=[],vehicles=[],types=[],providers=[],banks=[];
    async Task Masters()
    {
        string[] names=["Lucía","Mateo","Valentina","Santiago","Camila","Tomás","Sofía","Julián","Martina","Nicolás"];
        string[] surnames=["Sauce","Cedro","Ceibo","Algarrobo","Lapacho","Jacarandá","Aromito","Tala","Espinillo","Arrayán"];
        for(int i=0;i<100;i++)
        {
            var r=Ok(await Get<GT.Application.Usuarios.Personas.CrearPersona>().EjecutarAsync(new(names[i%10],$"{surnames[i/10]} DEMO015 {i:000}",(98000000+i).ToString(),i<50?"chofer":"empleado","115550"+i.ToString("D4"),$"persona.demo015.{i}@example.invalid",new DateOnly(1970+i%30,1+i%12,1+i%27))));
            people.Add(r.Persona!.Id);
        }
        for(int i=0;i<20;i++)
        {
            var r=Ok(await Get<GT.Application.Choferes.Transportistas.CrearTransportista>().EjecutarAsync(new($"{Company(i)} Transportes DEMO015 {i:000}",Cuit(97000000+i),"juridica","115551"+i.ToString("D4"),$"transporte.demo015.{i}@example.invalid")));
            carriers.Add(r.Transportista!.Id);
        }
        for(int i=0;i<35;i++)
        {
            var r=Ok(await Get<GT.Application.Viajes.Clientes.CrearCliente>().EjecutarAsync(new($"{Company(i)} Industria DEMO015 {i:000}",Cuit(96000000+i),"115552"+i.ToString("D4"),$"cliente.demo015.{i}@example.invalid",$"Avenida ficticia DEMO015 {100+i*15}")));
            clients.Add(r.Cliente!.Id);
        }
        var companyCuit=await db.EmpresaEmisora.Select(e=>e.Cuit).SingleAsync();
        var own=await db.Transportistas.SingleOrDefaultAsync(t=>t.Cuit==companyCuit);
        int ownId;
        if(own is null)ownId=Ok(await Get<GT.Application.Choferes.Transportistas.CrearTransportista>().EjecutarAsync(new("Flota propia DEMO015",companyCuit,"juridica","1155519999","propia.demo015@example.invalid"))).Transportista!.Id;
        else ownId=own.Id;
        for(int i=0;i<50;i++)
        {
            var p=await db.Personas.SingleAsync(p=>p.Id==people[i]);
            var r=Ok(await Get<GT.Application.Choferes.CrearChofer>().EjecutarAsync(new(p.Nombre,p.Apellido,p.Dni,Cuit(int.Parse(p.Dni),"20"),p.FechaNacimiento.ToString("yyyy-MM-dd"),p.Telefono,p.Email,i<5?ownId:carriers[i<42?i%16:16+(i-42)%4])));
            drivers.Add(r.Chofer!.Id);driverCarriers.Add(i<5?ownId:carriers[i<42?i%16:16+(i-42)%4]);
        }
        foreach(var name in new[]{"Tractor","Camión","Semirremolque sider","Semirremolque playo","Utilitario","Acoplado","Equipo especial"})
            types.Add(Ok(await Get<GT.Application.Flota.TiposVehiculo.GestionTiposVehiculo>().CrearAsync(new(name+" DEMO015"))).Tipo!.Id);
        for(int i=0;i<75;i++)
        {
            int t=i<15?0:i<25?1:i<35?2:i<45?3:i<55?4:i<65?5:6;
            // Disponible al alta está permitido sin documentos; la asignación usa las reglas reales.
            var r=Ok(await Get<GT.Application.Flota.CrearVehiculo>().EjecutarAsync(new($"ZZ{150+i:000}ZZ",new[]{"Scania","Volvo","Mercedes-Benz","Iveco","Volkswagen","Renault"}[i%6],$"Serie DEMO015 {2000+i%25}",types[t],carriers[i%20],"fueraDeServicio")));
            vehicles.Add(r.Vehiculo!.Id);
        }
        foreach(var x in new[]{("Licencia DEMO015","chofer"),("Seguro DEMO015","vehiculo"),("VTV DEMO015","vehiculo"),("Habilitación DEMO015","vehiculo")})
            Ok(await Get<GestionTiposDocumentacion>().CrearAsync(new(x.Item1,30,x.Item2)));
        var insurance=await db.DocumentacionTipos.SingleAsync(t=>t.Nombre=="Seguro DEMO015");
        for(int i=0;i<55;i++)
        {
            Ok(await Get<GT.Application.Flota.Documentacion.CargarDocumentoVehiculo>().EjecutarAsync(vehicles[i],new(insurance.Id,$"DEMO015-SEG-{i}","2025-01-01","2027-12-31"),null));
            var v=await db.Vehiculos.SingleAsync(v=>v.Id==vehicles[i]);
            Ok(await Get<GT.Application.Flota.ModificarVehiculo>().EjecutarAsync(v.Id,new(v.Patente,v.Marca,v.Modelo,v.TipoVehiculoId,v.TransportistaId,"disponible")));
        }
        for(int i=0;i<30;i++)
        {
            var p=await Get<GestionarProveedores>().GuardarAsync(null,i<20?new ProveedorPeticion("proveedorNormal",$"{Company(i)} Suministros DEMO015 {i:000}",Cuit(95000000+i),"1155530000",$"proveedor.demo015.{i}@example.invalid",$"Comercial DEMO015 {i}",$"Calle sintética DEMO015 {i+1}"):new ProveedorPeticion("fletero",NombreComercial:$"Fletero DEMO015 {i:000}",TransportistaId:carriers[i-20]));
            providers.Add(p.Id);
        }
        for(int i=0;i<6;i++)banks.Add((await Get<GestionBancosPagos>().CrearAsync(new($"Banco sintético DEMO015 {i+1}"),default)).Id);
        Console.WriteLine("Maestros completos:100 personas,35 clientes,20 transportistas externos + propio,50 choferes,75 vehículos,30 proveedores,6 bancos.");
    }
    static string Company(int i)=>new[]{"Ruta del Ceibo","Horizonte Austral","Nexo Serrano","Puerto del Tala","Circuito Pampeano","Valle del Sauce","Red Cordillerana","Parque del Cedro","Estepa Central","Sendero Norte","Llanura Verde","Terminal del Sol"}[i%12];
}
