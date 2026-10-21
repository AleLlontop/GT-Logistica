using System.Text.Json;
using GT.Application.Reportes.Analisis;

namespace GT.UnitTests.Reportes;

public class ContratosActividadAnalisisTests
{
    [Fact]
    public void DenegadaNoTransportaDatosYNuncaEquivaleACero()
    {
        var seccion = SeccionAnalisis<ResumenCobros>.SinPermiso();
        Assert.Equal(EstadoSeccionAnalisis.SinPermiso, seccion.Estado);
        Assert.Null(seccion.Datos);
        var cero = SeccionAnalisis<ResumenCobros>.Disponible(new(0, 0m));
        Assert.Equal(EstadoSeccionAnalisis.Disponible, cero.Estado);
        Assert.NotNull(cero.Datos);
        Assert.Throws<ArgumentNullException>(() => SeccionAnalisis<ResumenCobros>.Disponible(null!));
    }

    [Fact]
    public void CobroPreservaFechaFacturaAnteriorSinConvertirlaEnFechaDelFlujo()
    {
        var fila = new FilaCobroAnalisis(1, "0001-00000001", 3, "Cliente histórico", new(2026, 9, 5), new(2026, 10, 3), 121m);
        var recuperada = JsonSerializer.Deserialize<FilaCobroAnalisis>(JsonSerializer.Serialize(fila));
        Assert.Equal(fila, recuperada);
        Assert.NotEqual(fila.FechaFactura.Month, fila.FechaCobro.Month);
    }

    [Fact]
    public void DocumentoConservaNetoIvaTotalYSnapshotSinRecalculoDelContrato()
    {
        var fila = new FilaFacturaAnalisis(1, "0001-00000001", new(2026, 9, 5), 3, "Razón al emitir", "pagada", 100m, 21m, 121m, new(2026, 10, 3));
        var recuperada = JsonSerializer.Deserialize<FilaFacturaAnalisis>(JsonSerializer.Serialize(fila));
        Assert.Equal(fila, recuperada);
        Assert.Equal("Razón al emitir", recuperada!.ClienteRazonSocial);
    }

    [Fact]
    public void ContratosNoExponenInfraestructuraNiConsultasDiferidas()
    {
        var tipos = new[] { typeof(AnalisisViajes), typeof(AnalisisFacturacion), typeof(AnalisisCobros), typeof(DashboardViajes), typeof(DashboardFacturacion), typeof(DashboardAnalisis), typeof(SeccionesDashboard) };
        foreach (var propiedad in tipos.SelectMany(t => t.GetProperties()))
        {
            Assert.DoesNotContain("GT.Domain", propiedad.PropertyType.FullName ?? "");
            Assert.False(typeof(IQueryable).IsAssignableFrom(propiedad.PropertyType));
        }
    }
}
