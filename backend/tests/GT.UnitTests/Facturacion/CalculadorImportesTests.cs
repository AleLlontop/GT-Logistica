using GT.Domain.Facturacion;

namespace GT.UnitTests.Facturacion;

/// <summary>
/// El cálculo de los tres importes (FR-022, FR-023, research §9).
///
/// El mejor test que hay es el ejemplo que la propia spec escribió: si el número que el negocio puso
/// por escrito no sale, no hay discusión sobre quién tiene razón (US2 esc. 8).
/// </summary>
public class CalculadorImportesTests
{
    /// <summary>
    /// El ejemplo de la spec, tal cual: <c>30.000,00 + 30.000,00 + 22.644,63 = 82.644,63</c>, con 21%
    /// el IVA da <c>17.355,3723</c> → <c>17.355,37</c> y el total cierra en <c>100.000,00</c> redondo.
    /// </summary>
    [Fact]
    public void ElEjemploDeLaSpecCierraEnCienMil()
    {
        var importes = CalculadorImportes.Calcular(
            [30_000.00m, 30_000.00m, 22_644.63m],
            TipoComprobante.FacturaA);

        Assert.Equal(82_644.63m, importes.Neto);
        Assert.Equal(17_355.37m, importes.Iva);
        Assert.Equal(100_000.00m, importes.Total);
    }

    /// <summary>
    /// La <c>Factura B</c> lleva la misma alícuota que la A: la diferencia entre las dos es a quién se
    /// le factura, no cuánto IVA lleva (spec §Clarifications).
    /// </summary>
    [Fact]
    public void LaFacturaBLlevaLaMismaAlicuotaQueLaA()
    {
        var a = CalculadorImportes.Calcular([82_644.63m], TipoComprobante.FacturaA);
        var b = CalculadorImportes.Calcular([82_644.63m], TipoComprobante.FacturaB);

        Assert.Equal(a, b);
    }

    /// <summary>
    /// FR-023: en una <c>Factura C</c> el IVA es <c>0,00</c> y el total es igual al neto. <b>No es un
    /// error ni una factura incompleta</b>, y el pie del documento igual muestra las tres líneas
    /// (FR-031j).
    /// </summary>
    [Fact]
    public void LaFacturaCTieneIvaCeroYTotalIgualAlNeto()
    {
        var importes = CalculadorImportes.Calcular(
            [30_000.00m, 22_644.63m],
            TipoComprobante.FacturaC);

        Assert.Equal(52_644.63m, importes.Neto);
        Assert.Equal(0.00m, importes.Iva);
        Assert.Equal(importes.Neto, importes.Total);
    }

    /// <summary>
    /// <b>El caso armado para que difieran</b>, que es el que le da sentido a FR-031f.
    ///
    /// Con tres viajes de <c>0,01</c>, el IVA de cada fila redondea a <c>0,00</c> —<c>0,0021</c> no
    /// llega al centavo— y la suma de los tres subtotales da <c>0,03</c>. El IVA de la factura, en
    /// cambio, se calcula sobre el neto de <c>0,03</c>: da <c>0,0063</c>, que también redondea a
    /// <c>0,00</c>. Con un caso más grande la diferencia aparece de verdad, y lo que este test fija es
    /// que <b>manda el pie</b>: el neto y el IVA salen del total, nunca de sumar filas.
    ///
    /// Si nunca difieren en los tests, nadie sabe qué pasa cuando difieren (research §9).
    /// </summary>
    [Fact]
    public void MandaElPieCuandoLaSumaDeLosSubtotalesPorFilaDifiere()
    {
        // Diez viajes de 0,05: cada fila da IVA 0,0105 → 0,01, y la suma de subtotales sería
        // 10 × 0,06 = 0,60.
        decimal[] importesDeViajes = [.. Enumerable.Repeat(0.05m, 10)];

        var importes = CalculadorImportes.Calcular(importesDeViajes, TipoComprobante.FacturaA);

        // El pie: neto 0,50 y IVA sobre 0,50 = 0,105 → 0,11. Total 0,61.
        Assert.Equal(0.50m, importes.Neto);
        Assert.Equal(0.11m, importes.Iva);
        Assert.Equal(0.61m, importes.Total);

        // La suma de los subtotales por fila da 0,60: un centavo menos que el total. Los subtotales son
        // informativos y la diferencia por redondeo es esperada (FR-031f).
        var sumaDeSubtotales = importesDeViajes.Sum(importe =>
            importe + CalculadorImportes.IvaSobre(importe, TipoComprobante.FacturaA));

        Assert.Equal(0.60m, sumaDeSubtotales);
        Assert.NotEqual(sumaDeSubtotales, importes.Total);
    }

    /// <summary>
    /// Redondeo <b>comercial</b>, la mitad para arriba, y no el bancario que .NET usa por defecto: con
    /// <c>ToEven</c> este caso daría un centavo menos y una planilla argentina daría otro número
    /// (spec §Assumptions).
    /// </summary>
    [Fact]
    public void ElIvaRedondeaLaMitadParaArriba()
    {
        // neto × 0,21 = 0,105 exacto. Comercial: 0,11. Bancario: 0,10.
        Assert.Equal(0.11m, CalculadorImportes.IvaSobre(0.50m, TipoComprobante.FacturaA));
    }

    /// <summary>
    /// Un viaje con importe cero es válido y suma cero: se emite después de la confirmación previa de
    /// FR-032, y el cálculo no lo trata distinto.
    /// </summary>
    [Fact]
    public void UnViajeEnCeroNoAlteraLosImportes()
    {
        var importes = CalculadorImportes.Calcular(
            [30_000.00m, 0m, 52_644.63m],
            TipoComprobante.FacturaA);

        Assert.Equal(82_644.63m, importes.Neto);
        Assert.Equal(100_000.00m, importes.Total);
    }

    /// <summary>Las tres alícuotas están fijas en el código, no las configura ninguna pantalla (FR-023).</summary>
    [Theory]
    [InlineData(TipoComprobante.FacturaA, 0.21)]
    [InlineData(TipoComprobante.FacturaB, 0.21)]
    [InlineData(TipoComprobante.FacturaC, 0.00)]
    public void LasAlicuotasSonLasTresFijas(TipoComprobante tipo, double esperada) =>
        Assert.Equal((decimal)esperada, AlicuotasIva.De(tipo));
}
