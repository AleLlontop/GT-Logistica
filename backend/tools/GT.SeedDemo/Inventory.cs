using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using GT.Domain.Viajes;
using GT.Application.Reportes.Analisis;

namespace GT.SeedDemo;
public sealed partial class Dataset
{
    static readonly Dictionary<string,string> Ownership=new()
    {
        ["Choferes"]="Personas",["Proveedores"]="Transportistas",["Cajas"]="Usuarios",["OrdenesDePago"]="Liquidaciones",
        ["LiquidacionViajes"]="Liquidaciones",["CambiosDeEstadoViaje"]="Viajes",["CambiosDeEstadoFactura"]="Facturas",
        ["CambiosDeLiquidacion"]="Liquidaciones",["CambiosDeLiquidacionViajes"]="CambiosDeLiquidacion",
        ["CambiosDeAdelanto"]="Adelantos",["OrdenPagoProveedorFacturas"]="OrdenesPagoProveedor",
        ["MediosPagoProveedor"]="OrdenesPagoProveedor",["HistorialEstadosCheque"]="MediosPagoProveedor",
        ["ResultadosOperacionPago"]="OrdenesPagoProveedor",["UsuarioRoles"]="Usuarios",["MovimientosDeCaja"]="OrdenesPagoProveedor"
    };
    static readonly HashSet<string> Protected=["Roles","Permisos","RolPermisos"];
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
    static HashSet<EntityEntry> Owned(List<EntityEntry> rows)
    {
        var owned=rows.Where(Marked).ToHashSet();bool changed;
        do
        {
            changed=false;
            foreach(var row in rows.Where(r=>!owned.Contains(r)&&r.Entity is GT.Domain.Liquidaciones.Liquidacion))
            if(rows.Any(h=>h.Entity is GT.Domain.Liquidaciones.CambioDeLiquidacion c&&owned.Any(u=>u.Entity is GT.Domain.Usuarios.Usuario user&&user.Id==c.UsuarioId)&&c.LiquidacionId==(int)row.Property("Id").CurrentValue!&&c.Operacion==GT.Domain.Liquidaciones.OperacionDeLiquidacion.Generacion)){owned.Add(row);changed=true;}
            foreach(var row in rows.Where(r=>!owned.Contains(r)&&Ownership.ContainsKey(r.Metadata.GetTableName()!)&&(r.Metadata.GetTableName()!="Proveedores"||r.Property("NombreComercial").CurrentValue==null)))
            if(row.Metadata.GetForeignKeys().Any(f=>f.PrincipalEntityType.GetTableName()==Ownership[row.Metadata.GetTableName()!]&&owned.Any(p=>Refers(row,f,p)))){owned.Add(row);changed=true;}
        }while(changed);
        return owned;
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
    public async Task Reset()
    {
        var rows=await Load();var owned=Owned(rows);
        foreach(var other in rows.Where(r=>!owned.Contains(r)))
        foreach(var fk in other.Metadata.GetForeignKeys())
            if(owned.Any(p=>Refers(other,fk,p)))throw new InvalidOperationException($"Reset rechazado: {other.Metadata.GetTableName()} no DEMO referencia datos DEMO. No se borró nada.");
        var paths=owned.SelectMany(r=>r.Properties.Where(p=>p.Metadata.Name.EndsWith("Ruta")&&p.CurrentValue is string).Select(p=>(string)p.CurrentValue!)).Distinct().ToArray();
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
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
            await tx.CommitAsync();
        }
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
        var expected=new Dictionary<string,int>
        {
            ["Adelantos"]=40,["Bancos"]=6,["Cajas"]=2,["CambiosDeAdelanto"]=80,
            ["CambiosDeEstadoFactura"]=197,["CambiosDeEstadoViaje"]=1336,
            ["CambiosDeLiquidacion"]=164,["CambiosDeLiquidacionViajes"]=60,
            ["Choferes"]=50,["Clientes"]=35,["Documentaciones"]=45,
            ["DocumentacionesVehiculo"]=280,["DocumentacionTipos"]=4,
            ["Facturas"]=140,["FacturasProveedor"]=120,["HistorialEstadosCheque"]=22,
            ["Liquidaciones"]=60,["LiquidacionViajes"]=254,["MediosPagoProveedor"]=36,
            ["MovimientosDeCaja"]=185,["OrdenesDePago"]=51,["OrdenesPagoProveedor"]=30,
            ["OrdenPagoProveedorFacturas"]=50,["Personas"]=100,["Proveedores"]=30,
            ["ResultadosOperacionPago"]=35,["TiposVehiculo"]=7,["Usuarios"]=4,
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
        var count=await db.Viajes.CountAsync(v=>v.Origen.Contains(Mark));
        if(count!=450)throw new InvalidOperationException($"Dataset incompleto: {count}/450 viajes.");
        if(!await db.MovimientosDeCaja.AnyAsync(m=>m.OrdenPagoProveedorId!=null&&m.Tipo==GT.Domain.Caja.TipoMovimientoCaja.Ingreso))
            throw new InvalidOperationException("Dataset incompleto: falta compensación de efectivo OPP.");
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
        var oct=await db.Viajes.Where(v=>v.Origen.Contains(Mark)&&v.Fecha.Month==10).GroupBy(v=>v.Estado).Select(g=>new{Estado=g.Key,Cantidad=g.Count()}).ToListAsync();
        Console.WriteLine("Viajes octubre DEMO: "+System.Text.Json.JsonSerializer.Serialize(oct));
    }
}
