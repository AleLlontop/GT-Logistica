using GT.Domain.Adelantos;

namespace GT.UnitTests.Adelantos;

/// <summary>
/// Las reglas puras del Módulo 10, en sus bordes. Reciben el día por parámetro, así que el 1 de enero se
/// prueba sin esperar a enero (convención [005]).
/// </summary>
public class ReglasDeAdelantoTests
{
    // ── Piso de la fecha (FR-005) ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 9, 14, 2026, 8, 1)]
    [InlineData(2027, 1, 1, 2026, 12, 1)] // cruza el año
    [InlineData(2026, 3, 31, 2026, 2, 1)] // el último día de un mes largo no cae en un mes corto
    public void PrimeraFechaAdmitida_EsElPrimerDiaDelMesAnterior(
        int anio, int mes, int dia, int anioEsperado, int mesEsperado, int diaEsperado)
    {
        Assert.Equal(
            new DateOnly(anioEsperado, mesEsperado, diaEsperado),
            ReglasDeAdelanto.PrimeraFechaAdmitida(new DateOnly(anio, mes, dia)));
    }

    private static readonly DateOnly Hoy = new(2026, 9, 14);

    [Fact]
    public void FechaAdmitida_ElPiso_SeAcepta() =>
        Assert.True(ReglasDeAdelanto.FechaAdmitida(new DateOnly(2026, 8, 1), Hoy));

    [Fact]
    public void FechaAdmitida_UnDiaAntesDelPiso_SeRechaza() =>
        Assert.False(ReglasDeAdelanto.FechaAdmitida(new DateOnly(2026, 7, 31), Hoy));

    [Fact]
    public void FechaAdmitida_Hoy_SeAcepta() =>
        Assert.True(ReglasDeAdelanto.FechaAdmitida(Hoy, Hoy));

    [Fact]
    public void FechaAdmitida_Maniana_SeRechaza() =>
        Assert.False(ReglasDeAdelanto.FechaAdmitida(Hoy.AddDays(1), Hoy));

    // ── Importe (FR-007, FR-008) ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("0.01")]
    [InlineData("150000")]
    [InlineData("9999999999999999.99")]
    public void ImporteValido_Acepta(string importe) =>
        Assert.True(ReglasDeAdelanto.ImporteValido(decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0")]
    [InlineData("-20000")]
    [InlineData("150000.555")]
    [InlineData("10000000000000000")] // no entra en decimal(18,2)
    public void ImporteValido_Rechaza(string importe) =>
        Assert.False(ReglasDeAdelanto.ImporteValido(decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture)));

    // ── Qué estado admite cada operación (FR-021, FR-027) ───────────────────────────────────────

    [Fact]
    public void MotivoNoResoluble_Pendiente_EsNulo() =>
        Assert.Null(ReglasDeAdelanto.MotivoNoResoluble(EstadoAdelanto.Pendiente));

    [Theory]
    [InlineData(EstadoAdelanto.Aprobado)]
    [InlineData(EstadoAdelanto.Rechazado)]
    [InlineData(EstadoAdelanto.Anulado)]
    public void MotivoNoResoluble_EnOtroEstado_DevuelveEseEstado(EstadoAdelanto estado) =>
        Assert.Equal(estado, ReglasDeAdelanto.MotivoNoResoluble(estado));

    [Fact]
    public void MotivoNoAnulable_Aprobado_EsNulo() =>
        Assert.Null(ReglasDeAdelanto.MotivoNoAnulable(EstadoAdelanto.Aprobado));

    [Theory]
    [InlineData(EstadoAdelanto.Pendiente)]
    [InlineData(EstadoAdelanto.Rechazado)]
    [InlineData(EstadoAdelanto.Anulado)]
    public void MotivoNoAnulable_EnOtroEstado_DevuelveEseEstado(EstadoAdelanto estado) =>
        Assert.Equal(estado, ReglasDeAdelanto.MotivoNoAnulable(estado));
}
