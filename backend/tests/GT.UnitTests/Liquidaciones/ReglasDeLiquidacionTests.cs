using GT.Domain.Liquidaciones;

namespace GT.UnitTests.Liquidaciones;

/// <summary>
/// Las reglas puras del Módulo 9, en sus bordes. Reciben las fechas por parámetro, así que el borde de
/// "hoy" se prueba sin esperar a que llegue (convención [005]).
/// </summary>
public class ReglasDeLiquidacionTests
{
    // ── Período (FR-002) ────────────────────────────────────────────────────────────────────────

    private static readonly DateOnly HoyEn2026 = new(2026, 9, 14);

    [Theory]
    [InlineData(1, 2025)]
    [InlineData(12, 2025)]
    [InlineData(1, 2026)]
    [InlineData(12, 2026)]
    public void PeriodoValido_AceptaLosBordesDeLasListas(int mes, int anio)
    {
        Assert.True(ReglasDeLiquidacion.PeriodoValido(mes, anio, HoyEn2026));
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(7, 2024)]
    [InlineData(7, 2027)]
    public void PeriodoValido_RechazaLoQueQuedaFueraDeLasListas(int mes, int anio)
    {
        Assert.False(ReglasDeLiquidacion.PeriodoValido(mes, anio, HoyEn2026));
    }

    /// <summary>
    /// El año nuevo entra solo el 1 de enero, no el 31 de diciembre, y el anterior sigue: diciembre se
    /// liquida en enero.
    /// </summary>
    [Fact]
    public void PeriodoValido_AlCambiarDeAnio_SumaElNuevoYConservaElAnterior()
    {
        Assert.False(ReglasDeLiquidacion.PeriodoValido(1, 2027, new DateOnly(2026, 12, 31)));

        var primeroDeEnero = new DateOnly(2027, 1, 1);

        Assert.True(ReglasDeLiquidacion.PeriodoValido(1, 2027, primeroDeEnero));
        Assert.True(ReglasDeLiquidacion.PeriodoValido(12, 2026, primeroDeEnero));
        Assert.False(ReglasDeLiquidacion.PeriodoValido(1, 2028, primeroDeEnero));
    }

    // ── Fecha de pago (FR-038): los dos límites se aceptan ──────────────────────────────────────

    private static readonly DateOnly Generacion = new(2026, 9, 14);
    private static readonly DateOnly Hoy = new(2026, 9, 20);

    [Fact]
    public void FechaDePago_IgualALaGeneracion_SeAcepta() =>
        Assert.True(ReglasDeLiquidacion.FechaDePagoValida(Generacion, Generacion, Hoy));

    [Fact]
    public void FechaDePago_IgualAHoy_SeAcepta() =>
        Assert.True(ReglasDeLiquidacion.FechaDePagoValida(Hoy, Generacion, Hoy));

    [Fact]
    public void FechaDePago_UnDiaAntesDeLaGeneracion_SeRechaza() =>
        Assert.False(ReglasDeLiquidacion.FechaDePagoValida(Generacion.AddDays(-1), Generacion, Hoy));

    [Fact]
    public void FechaDePago_UnDiaDespuesDeHoy_SeRechaza() =>
        Assert.False(ReglasDeLiquidacion.FechaDePagoValida(Hoy.AddDays(1), Generacion, Hoy));

    // ── Resta pagar y estado tras un pago (FR-027, FR-030, FR-041) ──────────────────────────────

    [Fact]
    public void RestaPagar_EsElTotalMenosLoPagado() =>
        Assert.Equal(155_000m, ReglasDeLiquidacion.RestaPagar(355_000m, 200_000m));

    [Fact]
    public void EstadoTrasPago_ParcialQuedaPendiente() =>
        Assert.Equal(
            EstadoLiquidacion.Pendiente,
            ReglasDeLiquidacion.EstadoTrasPago(355_000m, 0m, 200_000m));

    [Fact]
    public void EstadoTrasPago_ExactoQuedaPagada() =>
        Assert.Equal(
            EstadoLiquidacion.Pagada,
            ReglasDeLiquidacion.EstadoTrasPago(355_000m, 200_000m, 155_000m));

    /// <summary>Un centavo de diferencia no es "pagada": el importe es <c>decimal</c> y la resta es exacta.</summary>
    [Fact]
    public void EstadoTrasPago_ConUnCentavoDeSaldoSigueePendiente() =>
        Assert.Equal(
            EstadoLiquidacion.Pendiente,
            ReglasDeLiquidacion.EstadoTrasPago(355_000.00m, 0m, 354_999.99m));

    // ── Por qué no se edita (FR-045): cuatro motivos ────────────────────────────────────────────

    [Fact]
    public void MotivoNoEditable_PendienteSinPagosYTransportistaLiquidable_EsNulo() =>
        Assert.Null(ReglasDeLiquidacion.MotivoNoEditable(EstadoLiquidacion.Pendiente, 0m, true));

    [Theory]
    [InlineData(EstadoLiquidacion.Pagada, 100, true, MotivoDeBloqueoLiquidacion.Pagada)]
    [InlineData(EstadoLiquidacion.Anulada, 0, true, MotivoDeBloqueoLiquidacion.Anulada)]
    [InlineData(EstadoLiquidacion.Pendiente, 1, true, MotivoDeBloqueoLiquidacion.ConOrdenesDePago)]
    [InlineData(EstadoLiquidacion.Pendiente, 0, false, MotivoDeBloqueoLiquidacion.TransportistaNoLiquidable)]
    public void MotivoNoEditable_NombraElMotivo(
        EstadoLiquidacion estado,
        int pagado,
        bool transportistaLiquidable,
        MotivoDeBloqueoLiquidacion esperado) =>
        Assert.Equal(
            esperado,
            ReglasDeLiquidacion.MotivoNoEditable(estado, pagado, transportistaLiquidable));

    /// <summary>
    /// Con pagos y con el transportista dado de baja a la vez, manda el pago: es lo que le dice a quien
    /// edita que la liquidación ya no se toca, sin importar qué pase después con el fletero.
    /// </summary>
    [Fact]
    public void MotivoNoEditable_ConPagosYTransportistaDadoDeBaja_DiceQueTienePagos() =>
        Assert.Equal(
            MotivoDeBloqueoLiquidacion.ConOrdenesDePago,
            ReglasDeLiquidacion.MotivoNoEditable(EstadoLiquidacion.Pendiente, 1m, false));

    // ── Por qué no se anula (FR-053): tres motivos ──────────────────────────────────────────────

    [Fact]
    public void MotivoNoAnulable_PendienteSinPagos_EsNulo() =>
        Assert.Null(ReglasDeLiquidacion.MotivoNoAnulable(EstadoLiquidacion.Pendiente, 0m));

    [Theory]
    [InlineData(EstadoLiquidacion.Pagada, 100, MotivoDeBloqueoLiquidacion.Pagada)]
    [InlineData(EstadoLiquidacion.Anulada, 0, MotivoDeBloqueoLiquidacion.Anulada)]
    [InlineData(EstadoLiquidacion.Pendiente, 1, MotivoDeBloqueoLiquidacion.ConOrdenesDePago)]
    public void MotivoNoAnulable_NombraElMotivo(
        EstadoLiquidacion estado,
        int pagado,
        MotivoDeBloqueoLiquidacion esperado) =>
        Assert.Equal(esperado, ReglasDeLiquidacion.MotivoNoAnulable(estado, pagado));
}
