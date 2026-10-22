using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Liquidaciones;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

internal record PagoParaAnular(int OrdenId, int UsuarioId, int[] Facturas, int[] Liquidaciones, int? CajaId, GenerarOrdenEntrada Generar, string JsonOriginal);
internal static class EscenarioAnulacion
{
    public static readonly DateOnly Hoy = new(2026, 10, 18);
    public static readonly DateTime Ahora = new(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
    public static async Task<PagoParaAnular> CrearAsync(AplicacionDePrueba app, string tipo = "cheque")
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var usuario = await app.UsuarioAsync();
        var facturas = new[] { s.Factura };
        if (tipo == "fletero") facturas = await app.ConAlcanceAsync(async db =>
        {
            var transportista = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
            var proveedor = EscenarioProveedores.Fletero(transportista);
            var lista = new List<FacturaProveedor>();
            for (var i = 0; i < 3; i++)
            {
                var liquidacion = EscenarioProveedores.Pendiente(transportista, 121m);
                var f = new FacturaProveedor { Proveedor = proveedor, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                    ProveedorRazonSocial = transportista.Nombre, ProveedorCuit = transportista.Cuit, Numero = Guid.NewGuid().ToString("N"),
                    Fecha = Hoy, Detalle = "Flete", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy,
                    Liquidacion = liquidacion, ImporteLiquidacionAlRegistrar = 121m };
                db.Add(f); db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = liquidacion, Operacion = OperacionDeLiquidacion.Generacion,
                    UsuarioId = s.UsuarioId, OcurridoEn = Ahora.AddDays(-2) }); lista.Add(f);
            }
            await db.SaveChangesAsync(); return lista.ToArray();
        });
        var total = 121m * facturas.Length;
        MedioEntrada[] medios = tipo switch
        {
            "efectivo" => [new EfectivoEntrada(Guid.NewGuid(), total)],
            "transferencia" => [new TransferenciaEntrada(Guid.NewGuid(), total, Hoy, new string('0', 22), new string('1', 22), "C1", null)],
            "mixta" => [new ChequeEntrada(Guid.NewGuid(), 60m, s.Banco.Id, Guid.NewGuid().ToString(), Hoy, Hoy.AddDays(1), null),
                new TransferenciaEntrada(Guid.NewGuid(), total - 60m, Hoy, new string('0', 22), new string('1', 22), "C1", null)],
            _ => [new ChequeEntrada(Guid.NewGuid(), total, s.Banco.Id, Guid.NewGuid().ToString(), Hoy, Hoy.AddDays(1), null)],
        };
        int? cajaId = tipo == "efectivo" ? (await app.CrearCajaAsync(usuario.Id, saldoInicial: total)).Id : null;
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), facturas[0].ProveedorId, Hoy, null, facturas.Select(f => f.Id).ToArray(), medios, total, true);
        using var scope = app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, usuario.Id, true, Hoy, Ahora);
        using var json = JsonDocument.Parse(resultado.Json);
        return new(json.RootElement.GetProperty("id").GetInt32(), usuario.Id, entrada.FacturaIds,
            facturas.Where(f => f.LiquidacionId.HasValue).Select(f => f.LiquidacionId!.Value).ToArray(), cajaId, entrada, resultado.Json);
    }
}
