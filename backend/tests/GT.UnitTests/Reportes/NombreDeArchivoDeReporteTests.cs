using GT.Application.Reportes;

namespace GT.UnitTests.Reportes;

/// <summary>
/// El nombre del archivo (FR-012). Es una función pura de reporte, formato e instante, así que se
/// prueba sin base y sin aplicación.
/// </summary>
public class NombreDeArchivoDeReporteTests
{
    /// <summary>20/09/2026 14:32 de Argentina, escrito como el instante UTC que le corresponde.</summary>
    private static readonly DateTime Instante = new(2026, 9, 20, 17, 32, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TipoDeReporte.Viajes, "viajes-2026-09-20-1432.pdf")]
    [InlineData(TipoDeReporte.VencimientosChoferes, "vencimientos-choferes-2026-09-20-1432.pdf")]
    [InlineData(TipoDeReporte.VencimientosFlota, "vencimientos-flota-2026-09-20-1432.pdf")]
    [InlineData(TipoDeReporte.VencimientosFacturas, "vencimientos-facturas-2026-09-20-1432.pdf")]
    [InlineData(TipoDeReporte.MovimientosCaja, "movimientos-caja-2026-09-20-1432.pdf")]
    public void LosCincoPrefijos(TipoDeReporte reporte, string esperado) =>
        Assert.Equal(esperado, NombreDeArchivoDeReporte.Para(reporte, FormatoDeReporte.Pdf, Instante));

    [Theory]
    [InlineData(FormatoDeReporte.Pdf, ".pdf")]
    [InlineData(FormatoDeReporte.Excel, ".xlsx")]
    public void LaExtensionSaleDelFormato(FormatoDeReporte formato, string extension) =>
        Assert.EndsWith(
            extension,
            NombreDeArchivoDeReporte.Para(TipoDeReporte.Viajes, formato, Instante));

    /// <summary>
    /// La hora es **de Argentina**, no la del servidor: a las 00:30 UTC del 21 en Argentina todavía es
    /// el 20 a las 21:30, y el archivo tiene que decir el día que dice la pantalla.
    /// </summary>
    [Fact]
    public void LaHoraEsDeArgentina()
    {
        var medianocheUtcDelVeintiuno = new DateTime(2026, 9, 21, 0, 30, 0, DateTimeKind.Utc);

        Assert.Equal(
            "viajes-2026-09-20-2130.pdf",
            NombreDeArchivoDeReporte.Para(
                TipoDeReporte.Viajes, FormatoDeReporte.Pdf, medianocheUtcDelVeintiuno));
    }

    /// <summary>
    /// **Dos reportes del mismo listado el mismo día no colisionan.** Es lo que la hora del nombre
    /// viene a evitar: sin ella, el segundo pisaría al primero en la carpeta de descargas.
    /// </summary>
    [Fact]
    public void DosReportesDelMismoDiaNoColisionan()
    {
        var primero = NombreDeArchivoDeReporte.Para(
            TipoDeReporte.Viajes, FormatoDeReporte.Pdf, Instante);

        var segundo = NombreDeArchivoDeReporte.Para(
            TipoDeReporte.Viajes, FormatoDeReporte.Pdf, Instante.AddHours(2));

        Assert.NotEqual(primero, segundo);
    }

    /// <summary>
    /// Sólo ASCII, minúsculas y guiones: así viaja en el <c>filename</c> simple del
    /// <c>Content-Disposition</c> y el frontend lo lee con una expresión regular de una línea.
    /// </summary>
    [Theory]
    [InlineData(TipoDeReporte.Viajes)]
    [InlineData(TipoDeReporte.VencimientosChoferes)]
    [InlineData(TipoDeReporte.VencimientosFlota)]
    [InlineData(TipoDeReporte.VencimientosFacturas)]
    [InlineData(TipoDeReporte.MovimientosCaja)]
    public void SoloAsciiMinusculasYGuiones(TipoDeReporte reporte)
    {
        var nombre = NombreDeArchivoDeReporte.Para(reporte, FormatoDeReporte.Excel, Instante);

        Assert.Matches("^[a-z0-9.-]+$", nombre);
    }
}
