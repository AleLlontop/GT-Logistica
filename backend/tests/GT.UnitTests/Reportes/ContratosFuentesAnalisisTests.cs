using System.Text.Json;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;

namespace GT.UnitTests.Reportes;

public class ContratosFuentesAnalisisTests
{
    [Fact]
    public void PagoManualYRevertidoNoPierdenOrigenNiHistoriaEnRoundTrip()
    {
        var manual = new FilaPagoAnalisis(1, "OP-25", 4, "LQ-4", "2026-09", new(2, "Transportista", false), new(2026, 10, 3), 50m,
            true, "manual", null, null, null, null);
        var revertido = manual with { Id = 2, Numero = "OP-26", Vigente = false, Origen = "opp", OppId = 25, OppNumero = "OPP-25",
            RevertidaEn = new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc), RevertidaPor = new(3, "Autorizado") };
        Assert.Equal(manual, JsonSerializer.Deserialize<FilaPagoAnalisis>(JsonSerializer.Serialize(manual)));
        Assert.Equal(revertido, JsonSerializer.Deserialize<FilaPagoAnalisis>(JsonSerializer.Serialize(revertido)));
        Assert.NotEqual(revertido.Numero, revertido.OppNumero);
    }

    [Fact]
    public void CajaMantieneIdsIndependientesYFlotaPuedeMostrarCeroInactivo()
    {
        var movimiento = new FilaMovimientoAnalisis(1, 2, "Caja", DateTime.UnixEpoch, "egreso", 50m, "Pago OPP-25",
            new(3, "Usuario"), "OPP-25", null, 25);
        Assert.Null(movimiento.OrdenDePagoId);
        Assert.Equal(25, movimiento.OrdenPagoProveedorId);
        var unidad = new FilaFlotaAnalisis(1, "AA123BB", new(2, "Tipo actual"), new(3, "Propietario actual"), false, "disponible", "sinDocumentacion", 0);
        Assert.Equal(unidad, JsonSerializer.Deserialize<FilaFlotaAnalisis>(JsonSerializer.Serialize(unidad)));
    }

    [Fact]
    public void PaginaNoConfundeTotalConLaCantidadVisibleYReferenciasNoIncluyenVersion()
    {
        var pagina = new PaginaDe<ReferenciaAnalisis>([new(1, "Banco de datos", false)], 21, 1, PaginaDe<ReferenciaAnalisis>.TamanioPorDefecto);
        Assert.Single(pagina.Items);
        Assert.Equal(21, pagina.Total);
        Assert.Equal(20, pagina.TamanioPagina);
        var nombres = typeof(ReferenciaAnalisis).GetProperties().Select(p => p.Name).ToArray();
        Assert.Equal(new[] { "Id", "Nombre", "Activo" }, nombres);
    }
}
