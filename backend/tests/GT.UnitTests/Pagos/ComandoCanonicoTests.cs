using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GT.Api.Pagos;
using GT.Application.Pagos;

namespace GT.UnitTests.Pagos;

public class ComandoCanonicoTests
{
    private static GenerarOrdenEntrada Entrada() => new(Guid.NewGuid(), 7, new(2026,10,18), "  Pago  proveedor  ",
        [4, 2], [new ChequeEntrada(Guid.NewGuid(), 121m, 1, "  000123  ", new(2026,10,18), new(2026,10,19), null),
        new EfectivoEntrada(Guid.NewGuid(), 242m)], 363m, true);

    [Fact]
    public void MismaSemantica_NoDependeDeOrdenClavesNiRenglonesUI()
    {
        var original = Entrada();
        var equivalente = original with { ClaveOperacion = Guid.NewGuid(), Confirmado = false, Detalle = "Pago  proveedor",
            FacturaIds = [2, 4], Medios = [new EfectivoEntrada(Guid.NewGuid(), 242m),
                ((ChequeEntrada)original.Medios[0]) with { RenglonId = Guid.NewGuid(), NumeroCheque = "000123" }] };
        Assert.Equal(CanonicalizadorPago.Generar(original).Contenido, CanonicalizadorPago.Generar(equivalente).Contenido);
        Assert.Equal(CanonicalizadorPago.Generar(original).Hash, CanonicalizadorPago.Generar(equivalente).Hash);
    }

    [Fact]
    public void OrdenDePropiedadesJsonRecibidas_NoCambiaHuella()
    {
        var original = Entrada();
        using var json = JsonDocument.Parse(new SerializadorPagos().Serializar(original));
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var propiedad in json.RootElement.EnumerateObject().Reverse()) propiedad.WriteTo(writer);
            writer.WriteEndObject();
        }
        var reordenado = JsonSerializer.Deserialize<GenerarOrdenEntrada>(stream.ToArray(), SerializadorPagos.Opciones)!;
        Assert.Equal(CanonicalizadorPago.Generar(original).Hash, CanonicalizadorPago.Generar(reordenado).Hash);
    }

    [Theory]
    [InlineData("fecha")]
    [InlineData("proveedor")]
    [InlineData("facturas")]
    [InlineData("importe")]
    [InlineData("detalle")]
    [InlineData("numero")]
    [InlineData("banco")]
    [InlineData("vencimiento")]
    [InlineData("medio")]
    public void CambioSignificativo_CambiaComandoYHash(string campo)
    {
        var a = Entrada();
        var cheque = (ChequeEntrada)a.Medios[0];
        var b = campo switch
        {
            "fecha" => a with { Fecha = a.Fecha!.Value.AddDays(-1) },
            "proveedor" => a with { ProveedorId = 8 },
            "facturas" => a with { FacturaIds = [4, 3] },
            "importe" => a with { ImporteTotalConfirmado = 363.01m },
            "detalle" => a with { Detalle = "Pago proveedor" },
            "numero" => a with { Medios = [cheque with { NumeroCheque = "123" }, a.Medios[1]] },
            "banco" => a with { Medios = [cheque with { BancoId = 2 }, a.Medios[1]] },
            "vencimiento" => a with { Medios = [cheque with { FechaVencimiento = new(2026,10,20) }, a.Medios[1]] },
            _ => a with { Medios = [cheque, new EfectivoEntrada(Guid.NewGuid(), 242.01m)] },
        };
        Assert.NotEqual(CanonicalizadorPago.Generar(a).Contenido, CanonicalizadorPago.Generar(b).Contenido);
        Assert.NotEqual(CanonicalizadorPago.Generar(a).Hash, CanonicalizadorPago.Generar(b).Hash);
    }

    [Fact]
    public void DuplicadosDeMedios_ConservanMultiplicidad()
    {
        var a = Entrada() with { Medios = [new EfectivoEntrada(Guid.NewGuid(), 121m)] };
        var b = a with { Medios = [a.Medios[0], new EfectivoEntrada(Guid.NewGuid(), 121m)] };
        Assert.NotEqual(CanonicalizadorPago.Generar(a).Contenido, CanonicalizadorPago.Generar(b).Contenido);
    }

    [Fact]
    public void OpcionalesSoloSeNormalizanComoApruebaNegocio()
    {
        var a = Entrada() with { Detalle = null };
        Assert.Equal(CanonicalizadorPago.Generar(a).Contenido, CanonicalizadorPago.Generar(a with { Detalle = "  " }).Contenido);
        var cbu = "0000000000000000000001";
        var medio = new TransferenciaEntrada(Guid.NewGuid(), 121m, new(2026,10,18), cbu, cbu, "01", " Texto ");
        a = a with { Medios = [medio] };
        var canonico = CanonicalizadorPago.Generar(a);
        Assert.Contains(cbu, canonico.Contenido);
        Assert.NotEqual(canonico.Contenido, CanonicalizadorPago.Generar(a with { Medios = [medio with { Observaciones = "Texto" }] }).Contenido);
    }

    [Fact]
    public void HashEsSha256DeUtf8SinBom_YAnularTieneComandoDistinto()
    {
        var generar = CanonicalizadorPago.Generar(Entrada());
        Assert.Equal(1, generar.Version);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(generar.Contenido)), generar.Hash);
        var anular = CanonicalizadorPago.Anular(5, new(Guid.NewGuid(), " Motivo ", true));
        Assert.Equal(anular.Contenido, CanonicalizadorPago.Anular(5, new(Guid.NewGuid(), "Motivo", false)).Contenido);
        Assert.NotEqual(anular.Contenido, CanonicalizadorPago.Anular(6, new(Guid.NewGuid(), "Motivo", true)).Contenido);
        Assert.NotEqual(generar.Contenido, anular.Contenido);
    }

    [Fact]
    public void NoRedondeaNiEliminaFacturasRepetidas()
    {
        var a = Entrada();
        Assert.Throws<ReglaPagoException>(() => CanonicalizadorPago.Generar(a with { FacturaIds = [2, 2] }));
        Assert.Throws<ReglaPagoException>(() => CanonicalizadorPago.Generar(a with { Medios = [new EfectivoEntrada(Guid.NewGuid(), 1.001m)] }));
    }
}
