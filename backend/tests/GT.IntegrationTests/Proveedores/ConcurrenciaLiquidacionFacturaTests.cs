using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using static GT.IntegrationTests.Proveedores.EscenariosAsociacionLiquidacion;
using static GT.IntegrationTests.Proveedores.EscenariosConcurrenciaLiquidacionFactura;

namespace GT.IntegrationTests.Proveedores;

public class ConcurrenciaLiquidacionFacturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData("edicion")] [InlineData("anulacion")] [InlineData("pagoTotal")] [InlineData("pagoParcial")]
    public async Task OperacionesExistentesPosterioresOConcurrentesConservanSnapshot(string operacion)
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarOperacionesRealesAsync(cliente, app.EnLaBaseAsync, operacion);
    }

    [Fact]
    public async Task DosFacturasCompitenPorUnVinculoYPdf()
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarDosFacturasAsync(cliente, app.EnLaBaseAsync, app.RutaDeArchivos);
    }

    [Theory]
    [InlineData("edicion")] [InlineData("anulacion")] [InlineData("pagoTotal")] [InlineData("pagoParcial")]
    public async Task EsperaElMismoLockDeLiquidacionesYRevalida(string operacion)
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        await VerificarEsperaAsync(cliente, app.EnLaBaseAsync, app.RutaDeArchivos, operacion);
    }
}

public static class EscenariosConcurrenciaLiquidacionFactura
{

    public static async Task VerificarOperacionesRealesAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string operacion)
    {
        foreach (var concurrente in new[] { false, true })
        {
            var (p, l, t) = await PrepararAsync(enBase);
            int viaje = 0;
            await enBase(async db =>
            {
                var c = new Cliente { RazonSocial = "Cliente US3", Cuit = GT.IntegrationTests.Viajes.DatosDePruebaViajes.CuitUnico(), Telefono = "341123456", Email = "cliente@example.com" };
                var v = new Viaje { ClienteId = 0, Cliente = c, TransportistaId = t, Fecha = new(2026, 10, 1), Origen = "Rosario", Destino = "Santa Fe", Estado = EstadoViaje.Rendido, Importe = 121 };
                var nuevo = new Viaje { ClienteId = 0, Cliente = c, TransportistaId = t, Fecha = v.Fecha, Origen = v.Origen, Destino = v.Destino, Estado = v.Estado, Importe = 242 };
                db.Viajes.AddRange(v, nuevo); await db.SaveChangesAsync(); viaje = nuevo.Id;
                var liquidacion = await db.Liquidaciones.SingleAsync(x => x.Id == l);
                liquidacion.Viajes.Add(new LiquidacionViaje { LiquidacionId = l, ViajeId = v.Id });
                liquidacion.Cambios.Add(new CambioDeLiquidacion { LiquidacionId = l, UsuarioId = await db.Usuarios.Select(u => u.Id).FirstAsync(), Operacion = OperacionDeLiquidacion.Generacion, OcurridoEn = DateTime.UtcNow });
                await db.SaveChangesAsync();
            });
            Task<HttpResponseMessage> Cambiar() => operacion switch
            {
                "edicion" => cliente.PutAsJsonAsync($"/api/liquidaciones/{l}", new { viajeIds = new[] { viaje }, version = 0 }),
                "anulacion" => cliente.PostAsJsonAsync($"/api/liquidaciones/{l}/anulacion", new { motivo = "Prueba", confirmado = true }),
                _ => cliente.PostAsJsonAsync($"/api/liquidaciones/{l}/ordenes-de-pago", new { fechaPago = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"), importe = operacion == "pagoTotal" ? 121m : 1m, confirmado = true }),
            };
            int? facturaId = null;
            if (concurrente)
            {
                using var form = CargarFacturaProveedorTests.Formulario(p, "Carrera real", "100.00");
                form.Add(new StringContent(l.ToString()), "liquidacionId");
                var rr = await Task.WhenAll(cliente.PostAsync("/api/proveedores/facturas", form), Cambiar());
                Assert.Equal(operacion.StartsWith("pago") ? HttpStatusCode.Created : HttpStatusCode.OK, rr[1].StatusCode);
                Assert.Contains(rr[0].StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
                if (operacion == "pagoParcial") Assert.Equal(HttpStatusCode.Created, rr[0].StatusCode);
                if (rr[0].StatusCode == HttpStatusCode.Created)
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(await rr[0].Content.ReadAsStringAsync()); facturaId = doc.RootElement.GetProperty("id").GetInt32();
                }
            }
            else
            {
                using var factura = await PostAsync(cliente, p, l, "Antes de cambio", HttpStatusCode.Created);
                facturaId = factura.RootElement.GetProperty("id").GetInt32();
                Assert.Equal(operacion.StartsWith("pago") ? HttpStatusCode.Created : HttpStatusCode.OK, (await Cambiar()).StatusCode);
            }
            await enBase(async db =>
            {
                if (facturaId.HasValue)
                {
                    var f = (await new RepositorioFacturasProveedor(db).ObtenerAsync(facturaId.Value, default))!;
                    Assert.Equal(121m, f.ImporteTotal); Assert.Equal(121m, f.Liquidacion!.ImporteAlRegistrar);
                    Assert.Equal(operacion == "edicion" ? 242m : 121m, f.Liquidacion.ImporteActual);
                    Assert.Equal(operacion == "anulacion" ? "anulada" : operacion == "pagoTotal" ? "pagada" : "pendiente", f.Liquidacion.EstadoActual);
                }
                else Assert.False(await db.FacturasProveedor.AnyAsync(f => f.ProveedorId == p));
            });
        }
    }
    public static async Task VerificarDosFacturasAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string ruta)
    {
        var (p, l, _) = await PrepararAsync(enBase); var antes = Archivos(ruta);
        using var f1 = CargarFacturaProveedorTests.Formulario(p, "Uno", "100.00", CargarFacturaProveedorTests.Pdf());
        using var f2 = CargarFacturaProveedorTests.Formulario(p, "Dos", "100.00", CargarFacturaProveedorTests.Pdf());
        f1.Add(new StringContent(l.ToString()), "liquidacionId"); f2.Add(new StringContent(l.ToString()), "liquidacionId");
        var rr = await Task.WhenAll(cliente.PostAsync("/api/proveedores/facturas", f1), cliente.PostAsync("/api/proveedores/facturas", f2));
        Assert.Single(rr, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(rr, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(antes + 1, Archivos(ruta));
        await enBase(async db => Assert.Equal(1, await db.FacturasProveedor.CountAsync(f => f.LiquidacionId == l)));
    }

    public static async Task VerificarEsperaAsync(HttpClient cliente, Func<Func<GtDbContext, Task>, Task> enBase, string ruta, string operacion)
    {
        var (p, l, _) = await PrepararAsync(enBase); var antes = Archivos(ruta);
        Task<System.Text.Json.JsonDocument>? guardar = null;
        await enBase(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            // Exactamente la fila y UPDATE condicional que toman editar/anular/pagar del Módulo 9.
            Assert.Equal(1, await db.Liquidaciones.Where(x => x.Id == l && x.Estado == EstadoLiquidacion.Pendiente)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, x => x.Estado)));
            guardar = PostAsync(cliente, p, l, "Tras espera", operacion == "pagoParcial" ? HttpStatusCode.Created : HttpStatusCode.Conflict, pdf: true);
            // Esperar que el PDF ya esté escrito: el POST está en curso antes de liberar el lock.
            using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (Archivos(ruta) == antes) await Task.Delay(10, limite.Token);
            Assert.False(guardar.IsCompleted);
            switch (operacion)
            {
                case "edicion": await db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImporteTotal, 122m).SetProperty(x => x.Version, 1)); break;
                case "anulacion": await db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoLiquidacion.Anulada).SetProperty(x => x.MotivoAnulacion, "Prueba")); break;
                case "pagoTotal": await db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoLiquidacion.Pagada).SetProperty(x => x.ImportePagado, 121m)); break;
                case "pagoParcial": await db.Liquidaciones.Where(x => x.Id == l).ExecuteUpdateAsync(s => s.SetProperty(x => x.ImportePagado, 1m)); break;
            }
            await tx.CommitAsync();
        });
        using var respuesta = await guardar!;
        var exito = operacion == "pagoParcial";
        Assert.Equal(antes + (exito ? 1 : 0), Archivos(ruta));
        await enBase(async db => Assert.Equal(exito ? 1 : 0, await db.FacturasProveedor.CountAsync(f => f.LiquidacionId == l)));
        if (operacion == "edicion") Assert.Equal("122.00", respuesta.RootElement.GetProperty("importeTotalLiquidacion").GetString());
    }
}
