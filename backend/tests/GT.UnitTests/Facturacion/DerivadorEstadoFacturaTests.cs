using GT.Domain.Facturacion;

namespace GT.UnitTests.Facturacion;

/// <summary>
/// La derivación de <c>vencida</c> (FR-041).
///
/// <b>La regla recibe la fecha por parámetro y no lee el reloj</b>, y eso es lo que hace posible este
/// archivo: probar que una factura figura vencida sin esperar a que venza (convención [005]).
/// </summary>
public class DerivadorEstadoFacturaTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 12);

    /// <summary>
    /// Impaga y con el vencimiento pasado: <c>vencida</c>. Nadie tocó nada — no hay proceso que escriba
    /// una columna.
    /// </summary>
    [Fact]
    public void Impaga_ConVencimientoPasado_EsVencida() =>
        Assert.Equal(
            EstadoFacturaVisible.Vencida,
            DerivadorEstadoFactura.Derivar(EstadoFactura.Pendiente, Hoy.AddDays(-1), Hoy));

    [Fact]
    public void Impaga_EnPlazo_EsPendiente() =>
        Assert.Equal(
            EstadoFacturaVisible.Pendiente,
            DerivadorEstadoFactura.Derivar(EstadoFactura.Pendiente, Hoy.AddDays(1), Hoy));

    /// <summary>
    /// El límite exacto: una factura que vence <b>hoy</b> todavía está en plazo. Con <c>&lt;=</c> la
    /// pantalla la mostraría vencida el mismo día en que se puede cobrar sin atraso.
    /// </summary>
    [Fact]
    public void Impaga_QueVenceHoy_TodaviaEsPendiente() =>
        Assert.Equal(
            EstadoFacturaVisible.Pendiente,
            DerivadorEstadoFactura.Derivar(EstadoFactura.Pendiente, Hoy, Hoy));

    /// <summary>
    /// <b><c>pagada</c> manda sobre el vencimiento</b>: una factura cobrada tarde no es una factura
    /// vencida, es una factura cobrada (FR-041).
    /// </summary>
    [Fact]
    public void Pagada_ConVencimientoPasado_SigueSiendoPagada() =>
        Assert.Equal(
            EstadoFacturaVisible.Pagada,
            DerivadorEstadoFactura.Derivar(EstadoFactura.Pagada, Hoy.AddDays(-100), Hoy));

    /// <summary><c>anulada</c> también manda: una anulada no se cobra ni vence.</summary>
    [Fact]
    public void Anulada_ConVencimientoPasado_SigueSiendoAnulada() =>
        Assert.Equal(
            EstadoFacturaVisible.Anulada,
            DerivadorEstadoFactura.Derivar(EstadoFactura.Anulada, Hoy.AddDays(-100), Hoy));

    /// <summary>
    /// Los cuatro valores son <b>excluyentes</b>: para un mismo dato, la derivación devuelve exactamente
    /// uno. Si <c>vencida</c> saliera también bajo <c>pendiente</c>, el filtro del listado contradiría a
    /// la columna que la propia fila muestra (FR-058a, US3 esc. 11).
    /// </summary>
    [Theory]
    [InlineData(EstadoFactura.Pendiente, -1, EstadoFacturaVisible.Vencida)]
    [InlineData(EstadoFactura.Pendiente, 1, EstadoFacturaVisible.Pendiente)]
    [InlineData(EstadoFactura.Pagada, -1, EstadoFacturaVisible.Pagada)]
    [InlineData(EstadoFactura.Anulada, -1, EstadoFacturaVisible.Anulada)]
    public void Los_CuatroValoresSonExcluyentes(
        EstadoFactura guardado,
        int diasHastaVencimiento,
        EstadoFacturaVisible esperado)
    {
        var derivado = DerivadorEstadoFactura.Derivar(
            guardado,
            Hoy.AddDays(diasHastaVencimiento),
            Hoy);

        Assert.Equal(esperado, derivado);

        // Y no es ninguno de los otros tres.
        var otros = Enum.GetValues<EstadoFacturaVisible>().Where(valor => valor != esperado);
        Assert.All(otros, valor => Assert.NotEqual(valor, derivado));
    }

    /// <summary>
    /// <b>El vencimiento del CAE no influye</b> en el estado de cobro: son dos plazos distintos y sólo
    /// el de pago mueve la factura a <c>vencida</c> (FR-041, US5 esc. 10).
    ///
    /// Se verifica por la forma de la función: no recibe el vencimiento del CAE, así que no puede
    /// mirarlo. Es la clase de garantía que un test de comportamiento no puede dar y la firma sí.
    /// </summary>
    [Fact]
    public void El_VencimientoDelCae_NoEsUnParametroDeLaRegla()
    {
        var parametros = typeof(DerivadorEstadoFactura)
            .GetMethod(nameof(DerivadorEstadoFactura.Derivar))!
            .GetParameters()
            .Select(parametro => parametro.Name);

        Assert.Equal(["guardado", "vencimientoPago", "hoy"], parametros);
    }

    /// <summary>
    /// Los días de atraso o de plazo, en días corridos: negativo es atraso (FR-063). Vive al lado de la
    /// derivación porque es la otra cara de la misma comparación.
    /// </summary>
    [Theory]
    [InlineData(-3, -3)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    public void Los_DiasSonCorridosYNegativosCuandoHayAtraso(int desplazamiento, int esperado) =>
        Assert.Equal(esperado, DerivadorEstadoFactura.DiasHasta(Hoy.AddDays(desplazamiento), Hoy));
}
