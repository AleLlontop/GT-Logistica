using GT.Application.Reportes;
using Microsoft.Extensions.Time.Testing;

namespace GT.UnitTests.Reportes;

/// <summary>
/// El encabezado trae las <b>cinco piezas de FR-006</b>: título, filtros en palabras, filas incluidas,
/// instante de generación y quién lo generó.
///
/// <b>Acá no hay ningún test de igualdad byte a byte sobre un reporte</b>, y es deliberado: el instante
/// de generación entra al archivo a propósito, así que un test así pasaría o fallaría según el reloj
/// (research §7). Lo que se verifica es que las cinco piezas estén y digan lo que tienen que decir.
/// </summary>
public class EncabezadoDeReporteTests
{
    /// <summary>20/09/2026 14:32 de Argentina.</summary>
    private static readonly DateTimeOffset Instante =
        new(2026, 9, 20, 17, 32, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _reloj = new(Instante);

    private ArmadoDeReporte Armado(int tope = OpcionesDeReporte.TopePorDefecto) =>
        new(new ArmadorQueDevuelveElReporte(), new ArmadorQueFalla(), new OpcionesDeReporte(tope), _reloj);

    [Fact]
    public void ElEncabezadoTraeLasCincoPiezas()
    {
        var capturador = new ArmadorQueDevuelveElReporte();

        var armado = new ArmadoDeReporte(
            capturador, new ArmadorQueFalla(), OpcionesDeReporte.PorDefecto, _reloj);

        armado.Entregar(
            TipoDeReporte.Viajes,
            FormatoDeReporte.Pdf,
            "jlopez",
            "Reporte de viajes",
            "Cliente: Aceitera del Sur",
            [ColumnaDeReporte.Texto("Cliente")],
            [[CeldaDeReporte.Texto("Aceitera del Sur")], [CeldaDeReporte.Texto("Molinos del Litoral")]]);

        var encabezado = capturador.Ultimo!.Encabezado;

        // 1. Título.
        Assert.Equal("Reporte de viajes", encabezado.Titulo);

        // 2. Filtros en palabras.
        Assert.Equal("Cliente: Aceitera del Sur", encabezado.Filtros);

        // 3. Filas incluidas: las del propio reporte, no las que el filtro podría dar.
        Assert.Equal(2, encabezado.CantidadDeFilas);
        Assert.Equal("Filas incluidas: 2", encabezado.LineaDeFilas);

        // 4. Instante de generación, **en hora de Argentina** y del reloj inyectado.
        Assert.Equal("Generado el 20/09/2026 a las 14:32", encabezado.LineaDeGeneracion);

        // 5. Quién lo generó.
        Assert.Equal("Generado por jlopez", encabezado.LineaDeAutor);
    }

    /// <summary>Las 00:30 UTC del 21 son las 21:30 del 20 en Argentina.</summary>
    [Fact]
    public void ElInstanteSeMuestraEnHoraDeArgentina()
    {
        var encabezado = new EncabezadoDeReporte(
            "Reporte de viajes",
            LineaDeFiltros.SinFiltros,
            0,
            new DateTime(2026, 9, 21, 0, 30, 0, DateTimeKind.Utc),
            "jlopez");

        Assert.Equal("Generado el 20/09/2026 a las 21:30", encabezado.LineaDeGeneracion);
    }

    /// <summary>El encabezado y el nombre del archivo salen del <b>mismo</b> instante.</summary>
    [Fact]
    public void ElNombreDelArchivoUsaElMismoInstanteQueElEncabezado()
    {
        var capturador = new ArmadorQueDevuelveElReporte();

        var armado = new ArmadoDeReporte(
            capturador, new ArmadorQueFalla(), OpcionesDeReporte.PorDefecto, _reloj);

        var resultado = armado.Entregar(
            TipoDeReporte.Viajes, FormatoDeReporte.Pdf, "jlopez", "Reporte de viajes",
            LineaDeFiltros.SinFiltros, [ColumnaDeReporte.Texto("Cliente")], []);

        Assert.Equal("viajes-2026-09-20-1432.pdf", resultado.NombreDeArchivo);
        Assert.Equal(
            capturador.Ultimo!.Encabezado.GeneradoEnArgentina,
            new DateTime(2026, 9, 20, 14, 32, 0));
    }

    /// <summary>Con filas **iguales** al tope el reporte se entrega: FR-016 rechaza "más de".</summary>
    [Fact]
    public void ConFilasIgualesAlTope_NoSeSupera() => Assert.False(Armado(tope: 3).SuperaElTope(3));

    [Fact]
    public void ConUnaFilaDeMas_SeSupera() => Assert.True(Armado(tope: 3).SuperaElTope(4));

    /// <summary>El <c>409</c> viaja con sus dos números y el mensaje **ya armado por el backend**.</summary>
    [Fact]
    public void ElTopeSuperadoTraeFilasTopeYMensaje()
    {
        var resultado = Armado(tope: 5000).TopeSuperado(7412);

        Assert.Equal(7412, resultado.Filas);
        Assert.Equal(5000, resultado.Tope);
        Assert.Equal(
            "El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a intentar.",
            MensajesDeReporte.TopeDeFilasSuperado(7412, 5000));
    }

    private sealed class ArmadorQueDevuelveElReporte : IArmadorReportePdf
    {
        public ReporteTabular? Ultimo { get; private set; }

        public byte[] Armar(ReporteTabular reporte)
        {
            Ultimo = reporte;

            return [1, 2, 3];
        }
    }

    private sealed class ArmadorQueFalla : IArmadorReporteExcel
    {
        public byte[] Armar(ReporteTabular reporte) =>
            throw new InvalidOperationException("No tendría que haberse llamado.");
    }
}
