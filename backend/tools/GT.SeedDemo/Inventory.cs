using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using GT.Domain.Viajes;
using GT.Application.Reportes.Analisis;

namespace GT.SeedDemo;
public sealed partial class Dataset
{
    static readonly Dictionary<string,string> LegacyOwnership=new()
    {
        ["Choferes"]="Personas",["Proveedores"]="Transportistas",["Cajas"]="Usuarios",["OrdenesDePago"]="Liquidaciones",
        ["LiquidacionViajes"]="Liquidaciones",["CambiosDeEstadoViaje"]="Viajes",["CambiosDeEstadoFactura"]="Facturas",
        ["CambiosDeLiquidacion"]="Liquidaciones",["CambiosDeLiquidacionViajes"]="CambiosDeLiquidacion",
        ["CambiosDeAdelanto"]="Adelantos",["OrdenPagoProveedorFacturas"]="OrdenesPagoProveedor",
        ["MediosPagoProveedor"]="OrdenesPagoProveedor",["HistorialEstadosCheque"]="MediosPagoProveedor",
        ["ResultadosOperacionPago"]="OrdenesPagoProveedor",["UsuarioRoles"]="Usuarios",["MovimientosDeCaja"]="OrdenesPagoProveedor"
    };

    async Task<List<EntityEntry>> Load()
    {
        db.ChangeTracker.Clear();
        foreach(var e in db.Model.GetEntityTypes().Where(e=>e.BaseType==null))
            await (Task)typeof(Dataset).GetMethod(nameof(LoadType),BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(e.ClrType).Invoke(this,[e])!;
        return db.ChangeTracker.Entries().ToList();
    }
    async Task LoadType<T>(IEntityType e) where T:class
    {
        if(e.HasSharedClrType)await db.Set<T>(e.Name).LoadAsync();else await db.Set<T>().LoadAsync();
    }
    static bool Marked(EntityEntry e)
    {
        //Sólo campos propios: un snapshot del emisor DEMO no convierte en DEMO una venta ajena.
        string[] fields=e.Metadata.GetTableName() switch
        {
            "Usuarios"=>["Username"], "Personas"=>["Apellido"], "Clientes"=>["RazonSocial"], "Proveedores"=>["RazonSocial","NombreComercial"],
            "Transportistas" or "TiposVehiculo" or "DocumentacionTipos" or "Bancos"=>["Nombre"],
            "Vehiculos"=>["Modelo"], "Documentaciones" or "DocumentacionesVehiculo" or "FacturasProveedor"=>["Numero"],
            "Viajes"=>["NumeroRemito"], "Facturas" or "OrdenesPagoProveedor"=>["Detalle"], "Adelantos"=>["Motivo"],
            "MovimientosDeCaja"=>["Concepto"], _=>[]
        };
        return fields.Any(f=>e.Property(f).CurrentValue is string s&&s.Contains(Mark,StringComparison.Ordinal));
    }
    static bool Refers(EntityEntry child,IForeignKey fk,EntityEntry parent)
    {
        if(!fk.PrincipalEntityType.IsAssignableFrom(parent.Metadata))return false;
        return fk.Properties.Select((p,i)=>child.Property(p.Name).CurrentValue is {} val&&Equals(val,parent.Property(fk.PrincipalKey.Properties[i].Name).CurrentValue)).All(x=>x);
    }
    static HashSet<EntityEntry> LegacyOwned(List<EntityEntry> rows)
    {
        var owned=rows.Where(Marked).ToHashSet();bool changed;
        do
        {
            changed=false;
            foreach(var row in rows.Where(r=>!owned.Contains(r)&&r.Entity is GT.Domain.Liquidaciones.Liquidacion))
            if(rows.Any(h=>h.Entity is GT.Domain.Liquidaciones.CambioDeLiquidacion c&&owned.Any(u=>u.Entity is GT.Domain.Usuarios.Usuario user&&user.Id==c.UsuarioId)&&c.LiquidacionId==(int)row.Property("Id").CurrentValue!&&c.Operacion==GT.Domain.Liquidaciones.OperacionDeLiquidacion.Generacion)){owned.Add(row);changed=true;}
            foreach(var row in rows.Where(r=>!owned.Contains(r)&&LegacyOwnership.ContainsKey(r.Metadata.GetTableName()!)&&(r.Metadata.GetTableName()!="Proveedores"||r.Property("NombreComercial").CurrentValue==null)))
            if(row.Metadata.GetForeignKeys().Any(f=>f.PrincipalEntityType.GetTableName()==LegacyOwnership[row.Metadata.GetTableName()!]&&owned.Any(p=>Refers(row,f,p)))){owned.Add(row);changed=true;}
        }while(changed);
        return owned;
    }
    HashSet<EntityEntry> Owned(List<EntityEntry> rows)
    {
        if(ownership.Current is {} manifest) return rows.Where(r=>manifest.Keys.Contains(Ownership.Key(r))).ToHashSet();
        // Compatibilidad exclusivamente para reset/summary de v1. Nunca se usa en v2.
        if(!rows.Any(r=>r.Entity is GT.Domain.Usuarios.Usuario u && (u.Username=="demo.DEMO015.v1.ready" || u.Username=="demo.DEMO015.seed.v1.incomplete")))return [];
        return LegacyOwned(rows);
    }
    Dictionary<string,int>? before;
    public async Task Summary()
    {
        var rows=await Load();var own=Owned(rows);
        Console.WriteLine("Entidad / tabla                              Antes  Creados    DEMO    Total");
        foreach(var e in db.Model.GetEntityTypes().OrderBy(e=>e.GetTableName()).ThenBy(e=>e.Name))
        {
            int total=rows.Count(r=>e.IsAssignableFrom(r.Metadata));int demo=own.Count(r=>e.IsAssignableFrom(r.Metadata));
            Console.WriteLine($"{e.ClrType.Name+" / "+e.GetTableName(),-48} {(before?.GetValueOrDefault(e.Name,total) ?? total),6} {(generated?demo:0),7} {demo,7} {total,8}"+(demo==0?"  (estructural/no generado; ver README)":""));
        }
        Console.WriteLine("37 DbSet; TPH Cheque/Transferencia/Efectivo comparten MediosPagoProveedor. Relaciones implícitas incluidas. __EFMigrationsHistory es técnica y no se siembra.");
    }
    public async Task CaptureBefore()
    {
        var rows=await Load();before=db.Model.GetEntityTypes().ToDictionary(e=>e.Name,e=>rows.Count(r=>e.IsAssignableFrom(r.Metadata)));
    }
    static void EnsureResetSafe(List<EntityEntry> rows,HashSet<EntityEntry> owned)
    {
        foreach(var other in rows.Where(r=>!owned.Contains(r)))
        foreach(var fk in other.Metadata.GetForeignKeys())
            if(owned.Any(p=>Refers(other,fk,p)))throw new InvalidOperationException($"Reset rechazado: {other.Metadata.GetTableName()} no DEMO referencia datos DEMO. No se borró nada.");
    }
    public async Task TestResetSafety()
    {
        if(ownership.Current?.Version!=Version)throw new InvalidOperationException("El test de reset exige metadata actual.");
        var rows=await Load();var owned=Owned(rows);
        EnsureResetSafe(rows,owned);
        var client=owned.First(r=>r.Entity is GT.Domain.Viajes.Cliente);
        var oldName=client.Property("RazonSocial").CurrentValue;
        client.Property("RazonSocial").CurrentValue="Nombre corregido por administración";
        if(!Owned(rows).Contains(client))throw new InvalidOperationException("Un nombre editado perdió pertenencia.");
        client.Property("RazonSocial").CurrentValue=oldName;
        // Sólo objetos en memoria: no INSERT, no SaveChanges ni borrados.
        var alien=db.Entry(new Viaje {Id=-15015,ClienteId=(int)client.Property("Id").CurrentValue!,Fecha=new(2026,10,1),Origen="La Plata",Destino="Campana",Importe=1});
        rows.Add(alien);
        if(Owned(rows).Contains(alien))throw new InvalidOperationException("Se adoptó una fila ajena por relación.");
        bool rejected=false;
        try { EnsureResetSafe(rows,Owned(rows)); }
        catch(InvalidOperationException e) when(e.Message.StartsWith("Reset rechazado:")) { rejected=true; }
        if(!rejected)throw new InvalidOperationException("No se detectó la referencia ajena.");
        db.ChangeTracker.Clear();
        Console.WriteLine("Tests reset: cambio de nombre conserva pertenencia; referencia ajena rechazada antes de borrar. Sin escrituras.");
    }
    public async Task Reset()
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var rows=await Load();var owned=Owned(rows);
        EnsureResetSafe(rows,owned);
        var paths=owned.SelectMany(r=>r.Properties.Where(p=>p.Metadata.Name.EndsWith("Ruta")&&p.CurrentValue is string).Select(p=>(string)p.CurrentValue!)).Distinct().ToArray();
        {
            // Sólo corrige la configuración exacta que el seed v1 creó; no una empresa ajena.
            if(ownership.Current is null && owned.Count>0)
            {
                var issuer=rows.SingleOrDefault(r=>r.Entity is GT.Domain.Facturacion.EmpresaEmisora);
                if(issuer?.Entity is GT.Domain.Facturacion.EmpresaEmisora e && e.RazonSocial=="Operador logístico DEMO015"
                    && e.Cuit==Cuit(98000001) && e.Domicilio=="Parque sintético DEMO015 15015" && e.Email=="empresa.demo015@example.invalid")
                    await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [EmpresaEmisora] SET [RazonSocial]={"G&T Logística S.A."}, [Domicilio]={Presentation.Addresses[0]}, [Email]={"administracion@gtlogistica.invalid"} WHERE [Id]={e.Id}");
            }
            // DELETE por PK explícita, en orden inverso de las FK del modelo real.
            // No RemoveRange: EF puede separar navegaciones requeridas antes de borrar hijos.
            foreach(var row in owned.Where(r=>r.Entity is Viaje))
            {
                var id=(int)row.Property("Id").CurrentValue!;
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Viajes] SET [FacturaId]=NULL WHERE [Id]={id}");
                row.Property("FacturaId").CurrentValue=null;
            }
            var remaining=owned.ToHashSet();
            while(remaining.Count>0)
            {
                var leaves=remaining.Where(parent=>!remaining.Any(child=>child!=parent&&child.Metadata.GetForeignKeys().Any(f=>Refers(child,f,parent)))).ToArray();
                if(leaves.Length==0)throw new InvalidOperationException("Ciclo FK no reconocido: reset cancelado sin confirmar.");
                foreach(var row in leaves)
                {
                    var key=row.Metadata.FindPrimaryKey()!;
                    var store=StoreObjectIdentifier.Table(row.Metadata.GetTableName()!,row.Metadata.GetSchema());
                    string Q(string v)=>"["+v.Replace("]","]]")+"]";
                    var parameters=key.Properties.Select((p,i)=>new Microsoft.Data.SqlClient.SqlParameter("@demo"+i,row.Property(p.Name).CurrentValue!)).ToArray();
                    var predicate=string.Join(" AND ",key.Properties.Select((p,i)=>Q(p.GetColumnName(store)!)+"=@demo"+i));
                    var table=(row.Metadata.GetSchema() is {} schema?Q(schema)+".":"")+Q(row.Metadata.GetTableName()!);
                    await db.Database.ExecuteSqlRawAsync("DELETE FROM "+table+" WHERE "+predicate,parameters.Cast<object>().ToArray());
                    remaining.Remove(row);
                }
            }
            await ownership.Delete(db);
            await tx.CommitAsync();
        }
        ownership.ResetMemory();
        foreach(var path in paths)await Get<GT.Application.Choferes.Documentacion.IAlmacenDeArchivos>().BorrarAsync(path,default);
        Console.WriteLine($"Reset exclusivo DEMO015: {owned.Count} filas eliminadas; resto conservado. Se regenerará versión{Version}.");
        db.ChangeTracker.Clear();
    }
    public async Task Validate()
    {
        // La marca ready no alcanza después de un corte: comprobar también las
        // filas propias de cada tabla, sin contar registros ajenos como cobertura.
        var rows=await Load();
        var owned=Owned(rows);
        if(ownership.Current is {} manifest && manifest.Keys.Except(rows.Select(Ownership.Key)).Any())
            throw new InvalidOperationException("Dataset incompleto: faltan PK registradas en la metadata.");
        var expected=new Dictionary<string,int>
        {
            ["Adelantos"]=40,["Cajas"]=2,["CambiosDeAdelanto"]=80,
            ["CambiosDeEstadoFactura"]=197,["CambiosDeEstadoViaje"]=1336,
            ["CambiosDeLiquidacion"]=164,["CambiosDeLiquidacionViajes"]=60,
            ["Choferes"]=50,["Clientes"]=35,["Documentaciones"]=45,
            ["DocumentacionesVehiculo"]=280,
            ["Facturas"]=140,["FacturasProveedor"]=120,["HistorialEstadosCheque"]=22,
            ["Liquidaciones"]=60,["LiquidacionViajes"]=254,["MediosPagoProveedor"]=36,
            ["MovimientosDeCaja"]=185,["OrdenesDePago"]=51,["OrdenesPagoProveedor"]=30,
            ["OrdenPagoProveedorFacturas"]=50,["Personas"]=100,["Proveedores"]=30,
            ["ResultadosOperacionPago"]=35,["Usuarios"]=4,
            ["UsuarioRoles"]=4,["Vehiculos"]=75,["Viajes"]=450
        };
        foreach(var (table,minimum) in expected)
        {
            var actual=owned.Count(r=>r.Metadata.GetTableName()==table);
            if(actual<minimum)throw new InvalidOperationException($"Dataset incompleto: {table}, {actual}/{minimum} filas DEMO015 requeridas.");
        }
        // El transportista propio puede ser compartido y, en ese caso, no es DEMO.
        if(owned.Count(r=>r.Metadata.GetTableName()=="Transportistas")<20)
            throw new InvalidOperationException("Dataset incompleto: faltan transportistas DEMO015.");
        var tripIds=owned.Where(r=>r.Entity is Viaje).Select(r=>(int)r.Property("Id").CurrentValue!).ToArray();
        var count=tripIds.Length;
        if(count!=450)throw new InvalidOperationException($"Dataset incompleto: {count}/450 viajes.");
        if(!await db.MovimientosDeCaja.AnyAsync(m=>m.OrdenPagoProveedorId!=null&&m.Tipo==GT.Domain.Caja.TipoMovimientoCaja.Ingreso))
            throw new InvalidOperationException("Dataset incompleto: falta compensación de efectivo OPP.");
        await CheckVisibleSql();
        clock.At(10,21);
        var result=await Get<ConsultarDashboard>().EjecutarAsync(new(new(2026,10,1),new(2026,10,31)),Enum.GetValues<SeccionReporte>().ToHashSet());
        //Sólo se imprimen importes analíticos, nunca datos de autenticación.
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        var s=result.Secciones;
        bool valid=s.Viajes.Datos is {} v&&v.Resumen.Cantidad>0&&v.Resumen.Realizados>0&&v.Resumen.PendientesRendicion>0&&v.Resumen.Pendientes>0
            &&s.Facturacion.Datos is {} f&&f.Documentos.Facturado>0&&f.Documentos.PendienteVigenteDocumental>0&&f.Cobros.ImporteCobrado>0
            &&s.Liquidaciones.Datos is {} l&&l.Documentos.ImporteLiquidado>0&&l.Documentos.PendienteVigenteDocumental>0&&l.Pagos.ImportePagadoVigente>0
            &&s.Caja.Datos is {} c&&c.Resumen.Ingresos>0&&c.Resumen.Egresos>0&&c.Resumen.Neto!=0
            &&s.Flota.Datos is {} fleet&&fleet.Resumen.CantidadUnidades>0&&fleet.Resumen.TotalViajesConVehiculo>0
            &&s.VencimientosChoferes.Datos?.Resumen.Cantidad>0&&s.VencimientosFlota.Datos?.Resumen.Cantidad>0&&s.VencimientosFacturas.Datos?.Resumen.Cantidad>0;
        if(!valid)throw new InvalidOperationException("Dashboard octubre no satisface todos los escenarios obligatorios.");
        var oct=await db.Viajes.Where(v=>tripIds.Contains(v.Id)&&v.Fecha.Month==10).GroupBy(v=>v.Estado).Select(g=>new{Estado=g.Key,Cantidad=g.Count()}).ToListAsync();
        Console.WriteLine("Viajes octubre DEMO: "+System.Text.Json.JsonSerializer.Serialize(oct));
    }
}
