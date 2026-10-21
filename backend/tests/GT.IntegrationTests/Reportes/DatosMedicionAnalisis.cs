using System.Globalization;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Flota;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

internal static class DatosMedicionAnalisis
{
    public const int Anio = 2026;
    public const int Semilla = 15040;
    public const decimal Neto = 100.01m;
    private static DateOnly Fecha(int i) => new(Anio, (i / 50) % 10 + 1, i % 18 + 1);

    public static async Task CrearAsync(AplicacionAnalisisDePrueba app, int cantidadVehiculos = 20)
    {
        await app.EnLaBaseAsync(async db =>
        {
            var usuario = await DatosDePruebaLiquidaciones.AdministradorAsync(db);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var clientes = Enumerable.Range(0,50).Select(i => new Cliente
            {
                RazonSocial = $"Cliente sintético015 {i:D2}", Cuit = Choferes.DatosDePrueba.CuilValidoPara(90015000+i),
                Telefono = "0000000000", Email = $"cliente{i}@example.invalid", Direccion = "Domicilio sintético015", Activo = i % 5 != 0,
            }).ToArray();
            var transportistas = Enumerable.Range(0,50).Select(i => new Transportista
            {
                Nombre = $"Transportista sintético015 {i:D2}", Cuit = Choferes.DatosDePrueba.CuilValidoPara(90015100+i),
                Tipo = TipoPersona.Fisica, Telefono = "0000000000", Email = $"transportista{i}@example.invalid", Activo = i % 5 != 0,
            }).ToArray();
            var tipos = Enumerable.Range(0,5).Select(i => new TipoVehiculo { Nombre = $"Tipo sintético015 {i}", Activo = true }).ToArray();
            db.AddRange(clientes); db.AddRange(transportistas); db.AddRange(tipos);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var propietariosActivos = transportistas.Where(t => t.Activo).ToArray();
            var vehiculos = Enumerable.Range(0,cantidadVehiculos).Select(i => new Vehiculo
            {
                Patente = $"ZZ{i:D3}AA", Marca = "Sintética015", Modelo = "Medición", TipoVehiculoId = tipos[i%5].Id,
                TransportistaId = propietariosActivos[i%propietariosActivos.Length].Id, EstadoOperativo = VehiculoEstado.FueraDeServicio, Activo = i % 5 != 0,
            }).ToArray();
            db.AddRange(vehiculos); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var facturas = Enumerable.Range(0,5000).Select(i =>
            {
                var fecha = Fecha(i); var cliente = clientes[i%50];
                var estado = i%5==0 ? EstadoFactura.Anulada : i%2==0 ? EstadoFactura.Pagada : EstadoFactura.Pendiente;
                var factura = new FacturaCliente
                {
                    NumeroComprobante = $"0015-{i+1:D8}", Fecha = fecha, TipoComprobante = TipoComprobante.FacturaA,
                    TipoFacturacion = TipoFacturacion.Original, CondicionDeVenta = CondicionDeVenta.CuentaCorriente,
                    PeriodoMes = (byte)fecha.Month, PeriodoAnio = Anio, ClienteId = cliente.Id,
                    ClienteRazonSocial = cliente.RazonSocial, ClienteCuit = cliente.Cuit, ClienteDomicilio = cliente.Direccion!,
                    EmisorRazonSocial = "Emisor sintético015", EmisorCuit = Choferes.DatosDePrueba.CuilValidoPara(90015999),
                    EmisorDomicilio = "Domicilio sintético015", EmisorCondicionIva = "Responsable inscripto", EmisorPuntoDeVenta = "0015",
                    Neto = Neto, Iva = 21m, Total = 121.01m, Cae = "12345678901234", CaeVencimiento = fecha.AddDays(10),
                    VencimientoPago = fecha.AddDays(30), Estado = estado, FechaCobro = estado==EstadoFactura.Pagada ? fecha.AddDays(2) : null,
                    MotivoAnulacion = estado==EstadoFactura.Anulada ? "Anulación sintética015" : null, DocumentoRuta = $"facturas/sintetica015-{i:D5}.pdf",
                };
                factura.CambiosDeEstado.Add(new() { FacturaId = 0, EstadoAnterior = null, EstadoNuevo = EstadoFactura.Pendiente, UsuarioId = usuario.Id, OcurridoEn = fecha.ToDateTime(new(12,0),DateTimeKind.Utc) });
                if(estado!=EstadoFactura.Pendiente)factura.CambiosDeEstado.Add(new() { FacturaId = 0, EstadoAnterior = EstadoFactura.Pendiente, EstadoNuevo = estado, UsuarioId = usuario.Id, OcurridoEn = fecha.AddDays(2).ToDateTime(new(12,0),DateTimeKind.Utc) });
                return factura;
            }).ToArray();
            db.AddRange(facturas); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var viajes = Enumerable.Range(0,10000).Select(i =>
            {
                var factura = i<5000 ? facturas[i] : null;
                var estado = factura is not null ? factura.Estado==EstadoFactura.Anulada ? EstadoViaje.Rendido : EstadoViaje.Facturado
                    : i%5==0 ? EstadoViaje.Anulado : i%5==1 ? EstadoViaje.Pendiente : EstadoViaje.Rendido;
                return new Viaje
                {
                    ClienteId = clientes[i%50].Id, Fecha = Fecha(i), Origen = "Origen sintético015", Destino = "Destino sintético015",
                    NumeroRemito = estado is EstadoViaje.Rendido or EstadoViaje.Facturado ? $"S015-{i:D5}" : null,
                    Importe = Neto, Estado = estado, MotivoAnulacion = estado==EstadoViaje.Anulado ? "Anulación sintética015" : null,
                    VehiculoId = i%10==0 ? null : vehiculos[i%vehiculos.Length].Id, TransportistaId = transportistas[i%50].Id,
                    FacturaId = estado==EstadoViaje.Facturado ? factura!.Id : null,
                };
            }).ToArray();
            db.AddRange(viajes); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var liquidaciones = Enumerable.Range(0,5000).Select(i =>
            {
                var fecha = Fecha(i); var estado = i%5==0 ? EstadoLiquidacion.Anulada : i%2==0 ? EstadoLiquidacion.Pagada : EstadoLiquidacion.Pendiente;
                var liquidacion = new Liquidacion
                {
                    TransportistaId = transportistas[i%50].Id, PeriodoMes = (byte)fecha.Month, PeriodoAnio = Anio,
                    ImporteTotal = Neto, ImportePagado = estado==EstadoLiquidacion.Pagada ? Neto : estado==EstadoLiquidacion.Pendiente ? 50m : 0m,
                    Estado = estado, MotivoAnulacion = estado==EstadoLiquidacion.Anulada ? "Anulación sintética015" : null,
                };
                liquidacion.Viajes.Add(new() { LiquidacionId = 0, ViajeId = viajes[i].Id, Vigente = estado!=EstadoLiquidacion.Anulada });
                liquidacion.Cambios.Add(new() { LiquidacionId = 0, Operacion = OperacionDeLiquidacion.Generacion, UsuarioId = usuario.Id, OcurridoEn = fecha.ToDateTime(new(12,0),DateTimeKind.Utc) });
                if(liquidacion.ImportePagado>0m)liquidacion.OrdenesDePago.Add(new() { LiquidacionId = 0, FechaPago = fecha.AddDays(1), Importe = liquidacion.ImportePagado, UsuarioId = usuario.Id, RegistradaEn = fecha.AddDays(1).ToDateTime(new(12,0),DateTimeKind.Utc) });
                if(estado==EstadoLiquidacion.Anulada)liquidacion.Cambios.Add(new() { LiquidacionId = 0, Operacion = OperacionDeLiquidacion.Anulacion, UsuarioId = usuario.Id, OcurridoEn = fecha.AddDays(1).ToDateTime(new(12,0),DateTimeKind.Utc) });
                return liquidacion;
            }).ToArray();
            db.AddRange(liquidaciones); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var caja = new GT.Domain.Caja.Caja { UsuarioResponsableId = usuario.Id, FechaApertura = new(Anio,1,1,3,0,0,DateTimeKind.Utc), SaldoInicial = 100000m };
            db.Add(caja); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            db.AddRange(Enumerable.Range(0,10000).Select(i => new GT.Domain.Caja.MovimientoDeCaja
            {
                CajaId = caja.Id, UsuarioId = usuario.Id, Tipo = i%2==0 ? GT.Domain.Caja.TipoMovimientoCaja.Ingreso : GT.Domain.Caja.TipoMovimientoCaja.Egreso,
                Importe = 1.01m, Concepto = "Movimiento sintético015", Fecha = Fecha(i).ToDateTime(new(12,0),DateTimeKind.Utc),
            }));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        });
    }
}
