using GT.Application.Reportes;

namespace GT.UnitTests.Reportes;

public class ArmadoAnalisisTests
{
    [Fact]
    public void InstanteDeFotoGobiernaEncabezadoYNombre_SinConsultarRelojDeRender()
    {
        var capturador = new Capturador();
        var armado = new ArmadoDeReporte(capturador, capturador, OpcionesDeReporte.PorDefecto, new RelojProhibido());
        var foto = new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.Zero);
        IReadOnlyList<IReadOnlyList<CeldaDeReporte>> filas = [[CeldaDeReporte.Entero(21)]];
        var resultado = armado.Entregar(TipoDeReporte.Viajes, FormatoDeReporte.Pdf, "gerencia",
            "Viajes", "Fecha del viaje: septiembre", [ColumnaDeReporte.Entero("Cantidad")], filas,
            foto, [TotalDeReporte.Cantidad("Realizados", 21), new("Importe", 123.45m)]);

        var reporte = Assert.IsType<ReporteTabular>(capturador.Reporte);
        Assert.Equal(foto.UtcDateTime, reporte.Encabezado.GeneradoEn);
        Assert.Equal("viajes-2026-09-30-2130.pdf", resultado.NombreDeArchivo);
        Assert.Equal("gerencia", reporte.Encabezado.GeneradoPor);
        Assert.Equal("Fecha del viaje: septiembre", reporte.Encabezado.Filtros);
        Assert.Same(filas, reporte.Filas);
        Assert.True(reporte.Totales[0].EsCantidad);
        Assert.False(reporte.Totales[1].EsCantidad);
        Assert.Equal(21m, reporte.Totales[0].Importe);
        Assert.Equal(123.45m, reporte.Totales[1].Importe);
    }

    private sealed class Capturador : IArmadorReportePdf, IArmadorReporteExcel
    {
        public ReporteTabular? Reporte { get; private set; }
        public byte[] Armar(ReporteTabular reporte) { Reporte = reporte; return [1]; }
    }
    private sealed class RelojProhibido : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("El render no lee el reloj.");
    }
}
