using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;

namespace GT.IntegrationTests.Pagos;

internal static class EscenarioPagos
{
    public static async Task<(OrdenPagoProveedor Orden, FacturaProveedor Factura, Banco Banco, int UsuarioId)> CrearAsync(AplicacionDePrueba app)
    {
        var usuario = await app.ObtenerAdministradorAsync();
        return await app.ConAlcanceAsync(async db =>
        {
            // Identidad numérica única sin modificar los padrones de otros tests.
            var proveedor = EscenarioProveedores.Normal("30" + Random.Shared.NextInt64(100000000, 999999999), "Proveedor SQL Pagos");
            var factura = new FacturaProveedor { Proveedor = proveedor, ProveedorRazonSocial = proveedor.RazonSocial!,
                ProveedorCuit = proveedor.Cuit!, TipoProveedorAlRegistrar = TipoProveedor.ProveedorNormal,
                Numero = "F-" + Guid.NewGuid().ToString("N"), Fecha = new(2026, 10, 18), Detalle = "Prueba",
                Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "123", FechaVencimientoCae = new(2026, 10, 20) };
            var orden = new OrdenPagoProveedor { Proveedor = proveedor, ProveedorRazonSocial = proveedor.RazonSocial!,
                ProveedorCuit = proveedor.Cuit!, TipoProveedorAlRegistrar = TipoProveedor.ProveedorNormal,
                ProveedorActivoAlGenerar = true, ImporteTotal = 121m, Fecha = new(2026, 10, 18), UsuarioId = usuario.Id,
                RegistradaEn = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc) };
            var banco = new Banco { Nombre = "Banco " + Guid.NewGuid().ToString("N") };
            db.AddRange(orden, factura, banco);
            await db.SaveChangesAsync();
            return (orden, factura, banco, usuario.Id);
        });
    }
}
