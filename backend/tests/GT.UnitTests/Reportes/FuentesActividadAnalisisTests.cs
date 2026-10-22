using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;

namespace GT.UnitTests.Reportes;

public class FuentesActividadAnalisisTests
{
    private static ContextoAnalisis Contexto(CriterioTemporalAnalisis criterio) => ContextoAnalisis.DesdeInstante(
        new(2026, 11, 1, 0, 30, 0, TimeSpan.Zero), new(new(2026, 10, 1), new(2026, 10, 31)), criterio);

    [Fact]
    public void ViajesConservaResumenCompleto_AunqueNoHayaFilas_YNoLoConvierteEnDinero()
    {
        var datos = new AnalisisViajes(Contexto(CriterioTemporalAnalisis.FechaViaje), new(0, 0, 0, 0, 0, 0), [],
            new(new([], null, 0), new([], null, 0), new([], null, 0)), new([], 0, 1, 20));
        var reporte = FuenteAnalisisViajes.Crear(datos, "Fecha del viaje", "gerencia");
        Assert.Empty(reporte.Filas);
        Assert.Equal(5, reporte.Totales.Count(t => t.EsCantidad));
        Assert.Single(reporte.Totales, t => !t.EsCantidad);
        Assert.Equal(datos.Contexto.ConsultadoEn.UtcDateTime, reporte.Encabezado.GeneradoEn);
    }

    [Fact]
    public void CajaUsaResumenExacto_NetoNoEsSaldo()
    {
        var datos = new AnalisisCaja(Contexto(CriterioTemporalAnalisis.FechaMovimientoArgentina), new(0, 1.01m, 3.02m, -2.01m), [],
            new(new([], null, 0)), new([], 0, 1, 20));
        var reporte = FuenteAnalisisCaja.Crear(datos, "Día argentino", "gerencia");
        Assert.Equal(-2.01m, reporte.Totales.Single(t => t.Etiqueta == "Movimiento neto").Importe);
        Assert.True(reporte.Totales[0].EsCantidad);
        Assert.Equal(3, reporte.Totales.Count(t => !t.EsCantidad));
    }
}
