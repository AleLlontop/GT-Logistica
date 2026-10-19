using GT.Domain.Liquidaciones;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Pagos;

public class ElegibilidadPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task NormalInactivoConDeudaSeOfrece_PagadaOVinculadaNo()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            await db.Proveedores.Where(x => x.Id == s.Orden.ProveedorId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false));
            var opciones = await ElegibilidadPagosSql.ProveedoresAsync(db, s.Orden.ProveedorCuit, 1, default);
            Assert.False(Assert.Single(opciones.Items).Activo);
            var pagina = await ElegibilidadPagosSql.FacturasAsync(db, s.Orden.ProveedorId, 1, default);
            Assert.Equal(s.Factura.Id, Assert.Single(pagina.Items).Id);
            var seleccion = await ElegibilidadPagosSql.SeleccionAsync(db, [s.Factura.Id], default);
            Assert.Equal(121m, seleccion.ImporteTotal);
            db.OrdenPagoProveedorFacturas.Add(new() { OrdenPagoProveedorId = s.Orden.Id,
                FacturaProveedorId = s.Factura.Id, ImporteTotalAlPagar = 121m });
            await db.SaveChangesAsync();
            Assert.Empty((await ElegibilidadPagosSql.FacturasAsync(db, s.Orden.ProveedorId, 1, default)).Items);
            await db.OrdenPagoProveedorFacturas.Where(x => x.FacturaProveedorId == s.Factura.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Vigente, false));
            Assert.Single((await ElegibilidadPagosSql.FacturasAsync(db, s.Orden.ProveedorId, 1, default)).Items);
            await db.FacturasProveedor.Where(x => x.Id == s.Factura.Id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, EstadoFacturaProveedor.Pagada));
            Assert.Empty((await ElegibilidadPagosSql.FacturasAsync(db, s.Orden.ProveedorId, 1, default)).Items);
        });
    }

    [Theory]
    [InlineData("valida", true)]
    [InlineData("inactivo", true)]
    [InlineData("total_modificado", false)]
    [InlineData("pago_parcial", false)]
    [InlineData("pagada", false)]
    [InlineData("anulada", false)]
    [InlineData("op_vigente", false)]
    public async Task Fletero_ElegibilidadSqlNoRelajaCondiciones(string caso, bool esperado)
    {
        var usuario = await app.ObtenerAdministradorAsync();
        await app.EnLaBaseAsync(async db =>
        {
            var transportista = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000,999999999));
            var proveedor = EscenarioProveedores.Fletero(transportista);
            proveedor.Activo = caso != "inactivo";
            var liquidacion = EscenarioProveedores.Pendiente(transportista, 121m);
            var factura = new FacturaProveedor { Proveedor = proveedor, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                ProveedorRazonSocial = transportista.Nombre, ProveedorCuit = transportista.Cuit,
                Numero = Guid.NewGuid().ToString("N"), Fecha = new(2026,10,18), Detalle = "Fletero",
                Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = new(2026,10,20),
                Liquidacion = liquidacion, ImporteLiquidacionAlRegistrar = 121m };
            if (caso == "total_modificado") liquidacion.ImporteTotal = 122m;
            if (caso == "pago_parcial") liquidacion.ImportePagado = 1m;
            if (caso == "pagada") { liquidacion.Estado = EstadoLiquidacion.Pagada; liquidacion.ImportePagado = 121m; }
            if (caso == "anulada") { liquidacion.Estado = EstadoLiquidacion.Anulada; liquidacion.MotivoAnulacion = "Prueba"; }
            db.Add(factura);
            await db.SaveChangesAsync();
            if (caso == "op_vigente")
            {
                db.OrdenesDePago.Add(new() { LiquidacionId = liquidacion.Id, FechaPago = new(2026,10,18),
                    Importe = 1m, UsuarioId = usuario.Id, RegistradaEn = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
            Assert.Equal(esperado, await ElegibilidadPagosSql.Elegibles(db).AnyAsync(x => x.Id == factura.Id));
            var seleccion = await ElegibilidadPagosSql.SeleccionAsync(db, [factura.Id], default);
            Assert.Equal(esperado, seleccion.NoDisponibles.Count == 0);
            if (esperado) Assert.NotNull(Assert.Single(seleccion.Items).Liquidacion);
        });
    }
}
