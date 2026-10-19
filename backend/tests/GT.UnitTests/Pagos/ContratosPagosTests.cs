using System.Text.Json;
using GT.Api.Pagos;
using GT.Application.Pagos;
using GT.Domain.Pagos;

namespace GT.UnitTests.Pagos;

public class ContratosPagosTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("363.00")]
    [InlineData("9999999999999999.99")]
    public void ImporteCanonico_HaceIdaYVueltaSinPerderCentavos(string valor)
    {
        var importe = JsonSerializer.Deserialize<decimal>('"' + valor + '"', SerializadorPagos.Opciones);
        Assert.Equal('"' + valor + '"', new SerializadorPagos().Serializar(importe));
    }

    [Theory]
    [InlineData("1.00")]
    [InlineData("\"1.1\"")]
    [InlineData("\"1.001\"")]
    [InlineData("\"01.00\"")]
    [InlineData("\"-1.00\"")]
    [InlineData("\"10000000000000000.00\"")]
    public void ImporteNoCanonicoOFueraDeCapacidadEsRechazado(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(json, SerializadorPagos.Opciones));

    [Fact]
    public void Medios_SerializanSoloSusCamposYNoDependenDeOrdenDePropiedades()
    {
        MedioEntrada[] medios = [new EfectivoEntrada(Guid.NewGuid(), 121m),
            new ChequeEntrada(Guid.NewGuid(), 121m, 1, "000123", new(2026,10,18), new(2026,10,19), null)];
        var json = new SerializadorPagos().Serializar(medios);
        using var doc = JsonDocument.Parse(json);
        var efectivo = doc.RootElement[0];
        Assert.Equal("efectivo", efectivo.GetProperty("tipo").GetString());
        Assert.Equal("121.00", efectivo.GetProperty("importe").GetString());
        Assert.False(efectivo.TryGetProperty("bancoId", out _));
        Assert.Equal("000123", doc.RootElement[1].GetProperty("numeroCheque").GetString());
        var reordenado = "{\"importe\":\"0.01\",\"renglonId\":\"" + Guid.NewGuid() + "\",\"tipo\":\"efectivo\"}";
        Assert.IsType<EfectivoEntrada>(JsonSerializer.Deserialize<MedioEntrada>(reordenado, SerializadorPagos.Opciones));
    }

    [Fact]
    public void CampoAjenoATipoNoSeIgnora()
    {
        var json = "{\"tipo\":\"efectivo\",\"importe\":\"1.00\",\"renglonId\":\"" + Guid.NewGuid() + "\",\"bancoId\":1}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MedioEntrada>(json, SerializadorPagos.Opciones));
    }

    [Fact]
    public void FechasYEstados_ConFormatoDelContratoSinCambiarJsonLegacy()
    {
        var serializador = new SerializadorPagos();
        Assert.Equal("\"2026-10-18\"", serializador.Serializar(new DateOnly(2026, 10, 18)));
        Assert.Equal("\"2026-10-18T06:15:00Z\"", serializador.Serializar(new DateTime(2026,10,18,6,15,0,DateTimeKind.Utc)));
        Assert.Equal("\"pendiente\"", serializador.Serializar(EstadoOrdenPagoProveedor.Pendiente));
        Assert.Equal("\"rechazado\"", serializador.Serializar(EstadoCheque.Rechazado));
        using var legacy = JsonDocument.Parse(JsonSerializer.Serialize(new { importe = 121m }));
        Assert.Equal(JsonValueKind.Number, legacy.RootElement.GetProperty("importe").ValueKind);
    }
}
