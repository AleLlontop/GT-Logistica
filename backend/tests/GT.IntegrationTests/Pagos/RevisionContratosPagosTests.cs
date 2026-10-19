using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GT.Api.Pagos;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Pagos;

public class RevisionContratosPagosTests(AplicacionChequesFija app) : IClassFixture<AplicacionChequesFija>
{
    [Theory]
    [InlineData("{\"estadoNuevo\":\"entregado\"}")]
    [InlineData("{\"estadoAnterior\":\"pendiente\"}")]
    [InlineData("{}")]
    public async Task EstadosDebenEstarPresentes_NoSeInfierenDelCeroDelEnum(string json)
    {
        using var cliente = await app.CrearClienteAutenticadoAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/api/pagos/cheques/2147483647/estado", new StringContent(json, Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Fact]
    public async Task BancoEdicionExigeVersion_YAltaNoAdmiteCamposDeEdicion()
    {
        var s = await EscenarioPagos.CrearAsync(app); using var cliente = await app.CrearClienteAutenticadoAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PutAsJsonAsync($"/api/pagos/bancos/{s.Banco.Id}", new { nombre = "Otro" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/pagos/bancos", new { nombre = "Alta", version = 12 })).StatusCode);
        await app.EnLaBaseAsync(async db => { var b = await db.Bancos.SingleAsync(b => b.Id == s.Banco.Id); Assert.Equal(s.Banco.Nombre, b.Nombre); Assert.Equal(0, b.Version); });
    }
    [Fact]
    public async Task TotalInformadoEsCampoRequerido_PeroNoSustituyeElCalculoServidor()
    {
        var s = await EscenarioPagos.CrearAsync(app); using var cliente = await app.CrearClienteAutenticadoAsync();
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, new(2026, 10, 18), null, [s.Factura.Id],
            [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "numero", new(2026, 10, 18), new(2026, 10, 20), null)], 121m, true);
        var json = JsonNode.Parse(JsonSerializer.Serialize(entrada, SerializadorPagos.Opciones))!; json.AsObject().Remove("importeTotalConfirmado");
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/api/pagos/ordenes", new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json"))).StatusCode);
        var adulterada = entrada with { Medios = [new ChequeEntrada(Guid.NewGuid(), 120m, s.Banco.Id, "numero", new(2026, 10, 18), new(2026, 10, 20), null)], ImporteTotalConfirmado = 120m };
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/api/pagos/ordenes", new StringContent(JsonSerializer.Serialize(adulterada, SerializadorPagos.Opciones), Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumeroDuplicadoApuntaAlRenglonInclusoHistoricoAnulado(bool historico)
    {
        var s = await EscenarioPagos.CrearAsync(app); var hoy = new DateOnly(2026, 10, 18);
        if (historico) await app.EnLaBaseAsync(async db => { db.Cheques.Add(new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id,
            NumeroCheque = "000-1", EstadoCheque = EstadoCheque.Anulado, RenglonId = Guid.NewGuid(), Fecha = hoy, FechaVencimiento = hoy, Importe = 1m }); await db.SaveChangesAsync(); });
        MedioEntrada primero = historico ? new TransferenciaEntrada(Guid.NewGuid(), 60m, hoy, new string('0', 22), new string('1', 22), "C1", null)
            : new ChequeEntrada(Guid.NewGuid(), 60m, s.Banco.Id, "000-1", hoy, hoy, null);
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, hoy, null, [s.Factura.Id],
            [primero, new ChequeEntrada(Guid.NewGuid(), 61m, s.Banco.Id, "  000-1  ", hoy, hoy, null)], 121m, true);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        var respuesta = await cliente.PostAsync("/api/pagos/ordenes", new StringContent(JsonSerializer.Serialize(entrada, SerializadorPagos.Opciones), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("medios[1].numeroCheque", json.RootElement.GetProperty("campo").GetString());
        await app.EnLaBaseAsync(async db => Assert.Equal(GT.Domain.Proveedores.EstadoFacturaProveedor.Pendiente, (await db.FacturasProveedor.SingleAsync(f => f.Id == s.Factura.Id)).Estado));
    }
}
