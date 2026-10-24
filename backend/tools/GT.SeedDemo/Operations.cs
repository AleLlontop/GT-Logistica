using GT.Application.Viajes;
using GT.Application.Facturacion;
using GT.Application.Liquidaciones;
using GT.Application.Proveedores;
using GT.Application.Pagos;
using GT.Application.Caja;
using GT.Application.Adelantos;
using GT.Application.Choferes.Documentacion;
using GT.Domain.Viajes;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.SeedDemo;
public sealed partial class Dataset
{
    async Task Operations()
    {
        var completed=new List<(int Id,int Month,int Carrier)>();
        var inCourse=new List<(int Id,int Index)>();
        int serial=0,invoice=0;
        foreach(var block in new[]{(8,80,25),(9,100,30),(10,200,70),(11,70,15)})
        {
            for(int i=0;i<block.Item2;i++,serial++)
            {
                int month=block.Item1,day=1+i%25;
                clock.At(month,day);
                decimal amount=(50000+random.Next(0,19000)*100m)*1.21m;
                amount=decimal.Round(amount,2,MidpointRounding.AwayFromZero);
                if(carriers.IndexOf(driverCarriers[i%50])>=10&&i%7==0)amount+=0.37m;
                int ci=i<35?i:i%4==0?0:i%5==0?1:i%35,di=i%50,vi=i<55?i:i%3==0?0:i%5==0?1:i%55;
                // 200oct:70facturados+70rendidos+25pendientes+25EnCurso+10anulados.
                bool pending=month==10?i>=140&&i<165:i>=block.Item2-12;
                bool course=month==10&&i>=165&&i<190;
                bool annul=month==10&&i>=190;
                if(pending||course)ci%=30; //Las cinco bajas sólo conservan viajes históricos terminales.
                var created=Ok(await Get<CrearViaje>().EjecutarAsync(new(clients[ci],new(2026,month,day),Presentation.Routes[serial%Presentation.Routes.Length].Origin,Presentation.Routes[serial%Presentation.Routes.Length].Destination,$"0015-{150150+serial:00000000}",Presentation.Cargo[serial%Presentation.Cargo.Length],amount),actor));
                int id=created.Viaje!.Id;
                if(annul){Ok(await Get<AnularViaje>().EjecutarAsync(id,new("Cancelación solicitada por el cliente"),actor));continue;}
                // Inicios abiertos se ejecutan al final para no bloquear unidades de otras operaciones.
                if(course){inCourse.Add((id,i-165));continue;}
                if(pending)continue;
                Ok(await Get<AsignarChoferYVehiculo>().EjecutarAsync(id,new(drivers[di],vehicles[vi])));
                Ok(await Get<PonerViajeEnCurso>().EjecutarAsync(id,actor));
                Ok(await Get<RendirViaje>().EjecutarAsync(id,new(true),actor));
                completed.Add((id,month,driverCarriers[di]));
                if(i<block.Item3)
                {
                    clock.At(month,28);
                    var f=Ok(await Get<EmitirFactura>().EjecutarAsync(new(clients[ci],"facturaA","original","cuentaCorriente",month,2026,new(2026,month,28),$"9015-{++invoice:00000000}","Servicios de transporte y distribución","15015000000001",new(2026,month,28),new(2026,month,28),null,[id],true),actor));
                    if((month==9&&i<15)||(month==10&&i<25)||(month!=9&&month!=10&&i%3==0))
                    {clock.At(month==9?10:month,29);Ok(await Get<RegistrarCobro>().EjecutarAsync(f.Factura!.Id,new(new DateOnly(2026,month==9?10:month,29)),actor));}
                    else if(i==block.Item3-1)
                        Ok(await Get<AnularFactura>().EjecutarAsync(f.Factura!.Id,new("Rectificación del período facturado"),actor));
                }
                db.ChangeTracker.Clear();
            }
            Console.WriteLine($"Viajes y facturas mes{block.Item1}: completos.");
        }
        //60liquidaciones:15transportistas por4meses. Las últimas no consumen todos los viajes.
        var liquidations=new List<(int Id,int Carrier,int Month,decimal Amount)>();
        foreach(int month in new[]{8,9,10,11})
        foreach(int ca in carriers.Take(15))
        {
            var ids=completed.Where(x=>x.Month==month&&x.Carrier==ca).Select(x=>x.Id).ToArray();
            if(ids.Length==0)continue;
            clock.At(month,28);
            var l=Ok(await Get<GenerarLiquidacion>().EjecutarAsync(new(ca,month,2026,ids.Length>1?ids[..^1]:ids),actor));
            if(ids.Length>1)l=Ok(await Get<EditarLiquidacion>().EjecutarAsync(l.Liquidacion!.Id,new(ids,l.Liquidacion.Version),actor));
            if(month==8&&ca==carriers[14]){Ok(await Get<AnularLiquidacion>().EjecutarAsync(l.Liquidacion!.Id,new("Reprogramación de servicios",true),actor));continue;}
            liquidations.Add((l.Liquidacion!.Id,ca,month,l.Liquidacion.ImporteTotal));
            db.ChangeTracker.Clear();
        }
        int fpNo=0;
        var fletes=new List<(int Factura,int Proveedor,decimal Total,int Month)>();
        // Para vincular a LQ debe coincidir exactamente neto+IVA. Se elige un viaje cuya cifra
        // procede de neto*1.21; si la suma tiene otra escala, se busca neto a centavos sin aproximar total.
        foreach(var l in liquidations.Where(x=>x.Month<=10&&carriers.IndexOf(x.Carrier)<10))
        {
            decimal net=decimal.Round(l.Amount/1.21m,2,MidpointRounding.AwayFromZero);
            if(net+GT.Domain.Facturacion.CalculadorImportes.IvaSobre(net,GT.Domain.Facturacion.TipoComprobante.FacturaA)!=l.Amount)
            { Console.WriteLine($"LQ{l.Id}: sin factura fletero por escala; permanece liquidación operativa.");continue; }
            int pi=20+carriers.IndexOf(l.Carrier);clock.At(l.Month,29);
            var f=await Get<CargarFacturaProveedor>().EjecutarAsync(new(providers[pi],$"0018-{++fpNo:00000000}",new(2026,l.Month,29),"Servicios de transporte de cargas",net,"15015000000002",new DateOnly(2026,l.Month,29).AddDays(10),l.Id),Attachment(true),new(2026,11,30));
            fletes.Add((f.Id,providers[pi],f.ImporteTotal,l.Month));
        }
        var normal=new List<(int Factura,int Proveedor,decimal Total,int Month)>();
        for(int i=0;i<90;i++)
        {
            int month=8+i%4;clock.At(month,28);
            var f=await Get<CargarFacturaProveedor>().EjecutarAsync(new(providers[i%20],$"0024-{i+1:00000000}",new(2026,month,15),Presentation.Expenses[i%Presentation.Expenses.Length],50000m+random.Next(500000)+i%99/100m,"15015000000003",new(2026,month,28)),Attachment(i%3==0),new(2026,11,30));
            normal.Add((f.Id,providers[i%20],f.ImporteTotal,month));
        }
        //Manual fuera de las LQ reservadas para OPP, con saldo parcial y pagos septiembre en octubre.
        int li=0;
        foreach(var l in liquidations.Where(l=>!(l.Month<=10&&carriers.IndexOf(l.Carrier)<10)))
        {
            if(li%4==0){li++;continue;}
            int month=l.Month==9?10:l.Month;clock.At(month,30);
            Ok(await Get<RegistrarOrdenDePago>().EjecutarAsync(l.Id,new(new DateOnly(2026,month,29),li%3==0?l.Amount:decimal.Round(l.Amount/2m,2),true),actor));li++;db.ChangeTracker.Clear();
        }
        //Caja real del actor demo; ningún usuario existente comparte este responsable.
        clock.At(8,1);int cash=Ok(await Get<AbrirCaja>().EjecutarAsync(new(90000000m),actor)).Caja!.Id;
        int secondActor=await db.Usuarios.Where(u=>u.Username=="tesoreria").Select(u=>u.Id).SingleAsync();
        int secondCash=Ok(await Get<AbrirCaja>().EjecutarAsync(new(5000000m),secondActor)).Caja!.Id;
        for(int i=0;i<180;i++)
        {int month=8+i%4;clock.At(month,1+i%28);Ok(await Get<RegistrarMovimiento>().EjecutarAsync(i%2==0?cash:secondCash,new(i%3==0?"egreso":"ingreso",20000m+random.Next(200000)+i%99/100m,i%3==0?Presentation.Expenses[i%Presentation.Expenses.Length]:"Ingreso por servicios de transporte",null,null),i%2==0?actor:secondActor));}
        var targets=fletes.GroupBy(f=>f.Proveedor).Select(g=>new{Facturas=g.Select(f=>f.Factura).ToArray(),Proveedor=g.Key,Total=g.Sum(f=>f.Total),Month=10,Fletero=true})
            .Concat(normal.Take(20).Select(f=>new{Facturas=new[]{f.Factura},f.Proveedor,f.Total,f.Month,Fletero=false})).ToArray();
        for(int i=0;i<targets.Length;i++)
        {
            var f=targets[i];int month=f.Month==9?10:f.Month;clock.At(month,30);
            MedioEntrada[] means;
            if(!f.Fletero&&i%5==0)means=[new EfectivoEntrada(Key(10000+i),f.Total)];
            else if(!f.Fletero&&i%5==1)means=[new TransferenciaEntrada(Key(10000+i),f.Total,new DateOnly(2026,month,29),"0000000000000000000001","0000000000000000000002",$"TR-{150150+i:00000000}","Pago por transferencia bancaria")];
            else
            {
                var expiry=new DateOnly(2026,month,30).AddDays(10+i%30);
                if(f.Fletero||i%2==0)means=[new ChequeEntrada(Key(10000+i),f.Total,banks[i%6],$"{15015000+i*2:00000000}",new(2026,month,29),expiry,"Pago diferido a proveedor")];
                else
                {
                    var first=decimal.Round(f.Total/2,2);
                    means=[new ChequeEntrada(Key(10000+i),first,banks[i%6],$"{15015000+i*2:00000000}",new(2026,month,29),expiry,"Pago diferido a proveedor"),new ChequeEntrada(Key(11000+i),f.Total-first,banks[(i+1)%6],$"{15015001+i*2:00000000}",new(2026,month,29),expiry,"Pago diferido a proveedor")];
                }
            }
            var result=await Get<GenerarOrdenProveedor>().EjecutarAsync(new(Key(20000+i),f.Proveedor,new(2026,month,29),"Pago de comprobantes del período",f.Facturas,means,f.Total,true),actor,true);
            if(result.EstadoHttp!=201)throw new InvalidOperationException("OPP no creada.");
            if((i%7==0||i==20) && (f.Fletero||i%5!=1))
            {
                using var json=System.Text.Json.JsonDocument.Parse(result.Json);int id=json.RootElement.GetProperty("id").GetInt32();
                clock.At(11,30);
                await Get<AnularOrdenProveedor>().EjecutarAsync(id,new(Key(30000+i),"Reversión por reprogramación del pago",true),i==20?secondActor:actor,true);
            }
            db.ChangeTracker.Clear();
        }
        var ownOrders=await db.ResultadosOperacionPago.Where(r=>r.UsuarioId==actor).Select(r=>r.OrdenPagoProveedorId).ToListAsync();
        var checks=await db.Cheques.Where(c=>ownOrders.Contains(c.OrdenPagoProveedorId)&&c.EstadoCheque==EstadoCheque.Pendiente).OrderBy(c=>c.Id).Take(10).Select(c=>c.Id).ToListAsync();
        for(int i=0;i<checks.Count;i++)
        {
            clock.At(11,30);
            await Get<CambiarEstadoChequePago>().EjecutarAsync(checks[i],new(EstadoCheque.Pendiente,EstadoCheque.Entregado),actor);
            if(i%3==0)
            {
                await Get<CambiarEstadoChequePago>().EjecutarAsync(checks[i],new(EstadoCheque.Entregado,EstadoCheque.Depositado),actor);
                await Get<CambiarEstadoChequePago>().EjecutarAsync(checks[i],new(EstadoCheque.Depositado,i%2==0?EstadoCheque.Cobrado:EstadoCheque.Rechazado),actor);
            }
        }
        //Adelantos con reloj de la fecha del hecho para respetar la ventana del módulo10.
        for(int i=0;i<40;i++)
        {
            int month=8+i%4;clock.At(month,20);
            int person=people[i%2==0?50+i%50:i%5];
            var a=Ok(await Get<RegistrarAdelanto>().EjecutarAsync(new(i%2==0?"empleado":"chofer",person,new(2026,month,15),"Adelanto de haberes",20000m+random.Next(100000)+i/100m),actor));
            if(i%4==0)Ok(await Get<AprobarAdelanto>().EjecutarAsync(a.Adelanto!.Id,actor));
            else if(i%4==1)Ok(await Get<RechazarAdelanto>().EjecutarAsync(a.Adelanto!.Id,new("Solicitud fuera de las condiciones acordadas",true),actor));
            else if(i%4==2){Ok(await Get<AprobarAdelanto>().EjecutarAsync(a.Adelanto!.Id,actor));Ok(await Get<AnularAdelanto>().EjecutarAsync(a.Adelanto!.Id,new("Reprogramación de servicios",true),actor));}
        }
        //Doc completa sólo después de operar viajes; el estado actual puede estar vencido aunque
        //la unidad estuviera habilitada cuando se asignó. Archivo es opcional en el modelo real.
        var docNames=Presentation.DocumentTypes.Select(d=>d.Name).ToArray();
        var docTypes=await db.DocumentacionTipos.Where(t=>docNames.Contains(t.Nombre)).ToListAsync();
        for(int i=0;i<drivers.Count;i++)
        {
            if(i%10==9)continue;
            string expiry=i<40?"2027-12-31":i%2==0?"2026-09-30":"2026-10-28";
            Ok(await Get<CargarDocumento>().EjecutarAsync(drivers[i],new(docTypes.Single(t=>t.Ambito==GT.Domain.Choferes.DocumentacionAmbito.Chofer).Id,$"LIC-{98000000+i}","2025-01-01",expiry),Attachment(i%5==0)));
        }
        for(int i=0;i<vehicles.Count;i++)
        foreach(var t in docTypes.Where(t=>t.Ambito==GT.Domain.Choferes.DocumentacionAmbito.Vehiculo))
        {
            string expiry=i<55?"2027-12-31":i%2==0?"2026-09-30":"2026-10-28";
            Ok(await Get<GT.Application.Flota.Documentacion.CargarDocumentoVehiculo>().EjecutarAsync(vehicles[i],new(t.Id,$"DOC-{Array.IndexOf(Presentation.DocumentTypes.Select(d=>d.Name).ToArray(),t.Nombre)+1}-{150150+i:000000}","2025-01-01",expiry),Attachment(i%5==0)));
        }
        //25viajes EnCurso requieren25pares únicos, todavía activos y sin documento vencido.
        //Se eligen unidades cuya documentación permite la asignación: no se fuerza estado.
        clock.At(10,20);
        foreach(var x in inCourse)
        {
            //Usar unidades vigentes; quitar temporariamente documentación nunca es una opción.
            var ds=Enumerable.Range(0,25).ToArray();
            var vs=Enumerable.Range(0,55).Where(i=>i%20<16).Take(25).ToArray();
            if(x.Index>=Math.Min(ds.Length,vs.Length))continue;
            Ok(await Get<AsignarChoferYVehiculo>().EjecutarAsync(x.Id,new(drivers[ds[x.Index]],vehicles[vs[x.Index]])));
            Ok(await Get<PonerViajeEnCurso>().EjecutarAsync(x.Id,actor));
        }
        //Bajas mediante operaciones reales, preservando historia. No tocar recursos ocupados.
        for(int i=42;i<50;i++)if(!await db.Viajes.AnyAsync(v=>v.ChoferId==drivers[i]&&v.Estado==EstadoViaje.EnCurso))Ok(await Get<GT.Application.Choferes.DarDeBajaChofer>().EjecutarAsync(drivers[i]));
        for(int i=0;i<vehicles.Count;i++)if(i%20>=16)Ok(await Get<GT.Application.Flota.DarDeBajaVehiculo>().EjecutarAsync(vehicles[i]));
        for(int i=16;i<20;i++)Ok(await Get<GT.Application.Choferes.Transportistas.DarDeBajaTransportista>().EjecutarAsync(carriers[i]));
        for(int i=30;i<35;i++)Ok(await Get<GT.Application.Viajes.Clientes.DarDeBajaCliente>().EjecutarAsync(clients[i]));
        clock.At(11,30);
        var close=Ok(await Get<ConsultarResumenDeCierre>().EjecutarAsync(secondCash,secondActor));
        Ok(await Get<CerrarCaja>().EjecutarAsync(secondCash,new(true,close.Resumen!.SaldoFinal),secondActor));
        clock.At(10,21);
        Console.WriteLine("Operaciones completas; validando grafo y escenarios.");
    }
}
