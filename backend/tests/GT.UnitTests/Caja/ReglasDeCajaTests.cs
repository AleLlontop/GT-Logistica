using System.Globalization;
using GT.Domain.Caja;

namespace GT.UnitTests.Caja;

/// <summary>Las reglas puras del Módulo 11, en sus bordes.</summary>
public class ReglasDeCajaTests
{
    private static decimal Importe(string texto) => decimal.Parse(texto, CultureInfo.InvariantCulture);

    // ── Saldo final (RN6) ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void SaldoFinal_EsInicialMasIngresosMenosEgresos() =>
        Assert.Equal(11500m, ReglasDeCaja.SaldoFinal(10000m, 3500m, 2000m));

    [Fact]
    public void SaldoFinal_SinMovimientos_EsElInicial() =>
        Assert.Equal(10000m, ReglasDeCaja.SaldoFinal(10000m, 0m, 0m));

    [Fact]
    public void SaldoFinal_ConEgresosMayores_DaNegativoSinFallar() =>
        Assert.Equal(-500m, ReglasDeCaja.SaldoFinal(1000m, 0m, 1500m));

    // ── Saldo inicial (RN4, FR-002) ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0")]
    [InlineData("10000")]
    [InlineData("0.01")]
    public void SaldoInicialValido_Acepta(string importe) =>
        Assert.True(ReglasDeCaja.SaldoInicialValido(Importe(importe)));

    [Theory]
    [InlineData("-1000")]
    [InlineData("0.001")]
    public void SaldoInicialValido_Rechaza(string importe) =>
        Assert.False(ReglasDeCaja.SaldoInicialValido(Importe(importe)));

    // ── Importe (RN3, FR-008) ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0.01")]
    [InlineData("3500")]
    public void ImporteValido_Acepta(string importe) =>
        Assert.True(ReglasDeCaja.ImporteValido(Importe(importe)));

    [Theory]
    [InlineData("0")]
    [InlineData("-200")]
    [InlineData("150.555")]
    public void ImporteValido_Rechaza(string importe) =>
        Assert.False(ReglasDeCaja.ImporteValido(Importe(importe)));

    // ── Concepto (RN5, FR-009) ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ConceptoValido_ConTexto_SeAcepta() => Assert.True(ReglasDeCaja.ConceptoValido("cobro flete"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConceptoValido_VacioOSoloEspacios_SeRechaza(string? concepto) =>
        Assert.False(ReglasDeCaja.ConceptoValido(concepto));

    [Fact]
    public void ConceptoValido_De200_SeAcepta() => Assert.True(ReglasDeCaja.ConceptoValido(new string('a', 200)));

    [Fact]
    public void ConceptoValido_De201_SeRechaza() => Assert.False(ReglasDeCaja.ConceptoValido(new string('a', 201)));

    // ── Referencia (RN8) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Referencia_IngresoConFactura_SeAdmite() =>
        Assert.Null(ReglasDeCaja.ReferenciaAdmitida(TipoMovimientoCaja.Ingreso, 1, null));

    [Fact]
    public void Referencia_EgresoConOrden_SeAdmite() =>
        Assert.Null(ReglasDeCaja.ReferenciaAdmitida(TipoMovimientoCaja.Egreso, null, 1));

    [Fact]
    public void Referencia_IngresoConOrden_SeRechaza() =>
        Assert.Equal(
            ReferenciaNoAdmitida.OrdenDePagoEnIngreso,
            ReglasDeCaja.ReferenciaAdmitida(TipoMovimientoCaja.Ingreso, null, 1));

    [Fact]
    public void Referencia_EgresoConFactura_SeRechaza() =>
        Assert.Equal(
            ReferenciaNoAdmitida.FacturaEnEgreso,
            ReglasDeCaja.ReferenciaAdmitida(TipoMovimientoCaja.Egreso, 1, null));

    [Theory]
    [InlineData(TipoMovimientoCaja.Ingreso)]
    [InlineData(TipoMovimientoCaja.Egreso)]
    public void Referencia_SinReferencia_SeAdmite(TipoMovimientoCaja tipo) =>
        Assert.Null(ReglasDeCaja.ReferenciaAdmitida(tipo, null, null));
}
