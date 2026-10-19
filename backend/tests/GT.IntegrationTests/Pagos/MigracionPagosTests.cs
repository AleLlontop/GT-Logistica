using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GT.IntegrationTests.Pagos;

using CajaEntidad = GT.Domain.Caja.Caja;

public class MigracionPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Previa = "20261016162244_Modulo13Proveedores";

    [Fact]
    public async Task DesdeVacio_AplicaTodaLaHistoriaYLasTablasNuevas()
    {
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Contains("20261018164612_Modulo14GestionPagos", await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Empty(await db.OrdenesPagoProveedor.ToListAsync());
            Assert.Empty(await db.Bancos.ToListAsync());
            Assert.Empty(await db.MediosPagoProveedor.ToListAsync());
            Assert.Empty(await db.HistorialEstadosCheque.ToListAsync());
            Assert.Empty(await db.ResultadosOperacionPago.ToListAsync());
        });
    }

    [Fact]
    public async Task UpgradeDesde013_ConservaFacturaPagoManualMovimientoYNumero()
    {
        var usuario = await app.ObtenerAdministradorAsync();
        await app.EnLaBaseAsync(async db =>
        {
            var transportista = EscenarioProveedores.Externo();
            var liquidacion = EscenarioProveedores.Pendiente(transportista, 121m);
            liquidacion.ImportePagado = 30m;
            var op = new OrdenDePago { LiquidacionId = 0, Liquidacion = liquidacion, FechaPago = new(2026, 10, 18),
                Importe = 30m, UsuarioId = usuario.Id, RegistradaEn = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc) };
            var proveedor = EscenarioProveedores.Normal();
            var factura = new FacturaProveedor { Proveedor = proveedor, ProveedorRazonSocial = proveedor.RazonSocial!,
                ProveedorCuit = proveedor.Cuit!, TipoProveedorAlRegistrar = TipoProveedor.ProveedorNormal,
                Numero = "MIG-014", Fecha = new(2026, 10, 18), Detalle = "Historia previa", Neto = 100m, Iva = 21m,
                ImporteTotal = 121m, Cae = "123", FechaVencimientoCae = new(2026, 10, 20) };
            var caja = new CajaEntidad { SaldoInicial = 100m, FechaApertura = op.RegistradaEn, UsuarioResponsableId = usuario.Id };
            var movimiento = new MovimientoDeCaja { CajaId = 0, Caja = caja, Tipo = TipoMovimientoCaja.Egreso,
                Importe = 30m, Concepto = "Pago manual previo", UsuarioId = usuario.Id, Fecha = op.RegistradaEn, OrdenDePago = op };
            db.AddRange(op, factura, movimiento);
            await db.SaveChangesAsync();
            var numero = op.Numero;
            var idOp = op.Id;
            var idFactura = factura.Id;
            var idMovimiento = movimiento.Id;
            db.ChangeTracker.Clear();
            // Sólo esta base efímera sin datos 014: obtener esquema anterior conservando los datos legados.
            var migrador = db.GetService<IMigrator>();
            await migrador.MigrateAsync(Previa);
            Assert.DoesNotContain("20261018164612_Modulo14GestionPagos", await db.Database.GetAppliedMigrationsAsync());
            await migrador.MigrateAsync();
            var opLeida = await db.OrdenesDePago.AsNoTracking().SingleAsync(x => x.Id == idOp);
            Assert.Equal(numero, opLeida.Numero);
            Assert.True(opLeida.Vigente);
            Assert.Null(opLeida.OrdenPagoProveedorId);
            Assert.Null(opLeida.RevertidaEn);
            Assert.Equal(30m, opLeida.Importe);
            var facturaLeida = await db.FacturasProveedor.AsNoTracking().SingleAsync(x => x.Id == idFactura);
            Assert.Equal(EstadoFacturaProveedor.Pendiente, facturaLeida.Estado);
            Assert.Equal(121m, facturaLeida.ImporteTotal);
            var movimientoLeido = await db.MovimientosDeCaja.AsNoTracking().SingleAsync(x => x.Id == idMovimiento);
            Assert.Equal(idOp, movimientoLeido.OrdenDePagoId);
            Assert.Null(movimientoLeido.OrdenPagoProveedorId);
            Assert.Equal(30m, movimientoLeido.Importe);
            Assert.Equal(30m, await db.Liquidaciones.Where(x => x.Id == opLeida.LiquidacionId).Select(x => x.ImportePagado).SingleAsync());
        });
    }
}
