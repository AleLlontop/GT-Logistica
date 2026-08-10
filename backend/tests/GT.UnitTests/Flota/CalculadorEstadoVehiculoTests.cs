using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.UnitTests.Flota;

/// <summary>
/// Cubre FR-033 (los cuatro valores y su precedencia), FR-024 (de cada tipo manda el de vencimiento
/// más lejano), FR-034 (ningún tipo es obligatorio) y FR-016a (la falta de archivo no altera el
/// estado del vehículo).
/// </summary>
public class CalculadorEstadoVehiculoTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 8);

    private const int Seguro = 1;
    private const int Vtv = 2;

    private static DocumentacionVehiculo Documento(
        int id,
        int tipoId,
        int venceEnDias,
        int diasAviso = 30,
        string? archivoRuta = null)
    {
        return new DocumentacionVehiculo
        {
            Id = id,
            VehiculoId = 1,
            DocumentacionTipoId = tipoId,
            Numero = $"N-{id}",
            FechaEmision = Hoy.AddDays(-400),
            FechaVencimiento = Hoy.AddDays(venceEnDias),
            ArchivoRuta = archivoRuta,
            Tipo = new DocumentacionTipo
            {
                Id = tipoId,
                Nombre = $"Tipo {tipoId}",
                DiasAvisoVencimiento = diasAviso,
                Ambito = DocumentacionAmbito.Vehiculo,
            },
        };
    }

    // ── FR-033: los cuatro valores y su precedencia ─────────────────────────────────────────────

    [Fact]
    public void SinDocumentos_EsSinDocumentacion_NoEnRegla()
    {
        // Una unidad sin papeles no está al día por ausencia de papeles, y por eso tampoco puede
        // quedar disponible (FR-013, FR-033).
        Assert.Equal(
            EstadoDocumentacionVehiculo.SinDocumentacion,
            CalculadorEstadoVehiculo.Calcular([], Hoy));
    }

    [Fact]
    public void ConTodoAlDia_EsEnRegla()
    {
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: 200),
            Documento(2, Vtv, venceEnDias: 120),
        };

        Assert.Equal(
            EstadoDocumentacionVehiculo.EnRegla,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    [Fact]
    public void LaVencidaGanaALaQueEstaAlDia()
    {
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: 200),
            Documento(2, Vtv, venceEnDias: -3),
        };

        Assert.Equal(
            EstadoDocumentacionVehiculo.Vencida,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    [Fact]
    public void LaVencidaGanaALaProximaAvencer()
    {
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: 10),
            Documento(2, Vtv, venceEnDias: -1),
        };

        Assert.Equal(
            EstadoDocumentacionVehiculo.Vencida,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    [Fact]
    public void LaProximaAvencerGanaALaQueEstaAlDia()
    {
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: 200),
            Documento(2, Vtv, venceEnDias: 10),
        };

        Assert.Equal(
            EstadoDocumentacionVehiculo.ProximaAvencer,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    /// <summary>Vence exactamente hoy → próxima a vencer, no vencida (FR-019, borde declarado).</summary>
    [Fact]
    public void ElQueVenceHoy_EsProximaAvencer_NoVencida()
    {
        Assert.Equal(
            EstadoDocumentacionVehiculo.ProximaAvencer,
            CalculadorEstadoVehiculo.Calcular([Documento(1, Seguro, venceEnDias: 0)], Hoy));
    }

    // ── FR-024: de cada tipo manda uno solo ─────────────────────────────────────────────────────

    [Fact]
    public void UnaRenovacionTapaAlDocumentoViejo_AunqueElViejoEsteVencido()
    {
        // Es lo que hace que cargar la renovación saque la alerta sin que nadie borre el papel viejo
        // (SC-010).
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: -60),
            Documento(2, Seguro, venceEnDias: 300),
        };

        Assert.Equal(
            EstadoDocumentacionVehiculo.EnRegla,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    [Fact]
    public void ConEmpateDeVencimiento_MandaElDeIdMayor()
    {
        // Dos documentos del mismo tipo con la misma fecha son un error de carga plausible. Sin
        // desempate, el resultado cambiaría entre dos consultas idénticas (research §12).
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: 50, diasAviso: 0),
            Documento(2, Seguro, venceEnDias: 50, diasAviso: 90),
        };

        var vigente = Assert.Single(CalculadorEstadoVehiculo.VigentesDeCadaTipo(documentos));

        Assert.Equal(2, vigente.Id);
    }

    [Fact]
    public void VigentesDeCadaTipo_DevuelveUnoPorTipo()
    {
        var documentos = new[]
        {
            Documento(1, Seguro, venceEnDias: -60),
            Documento(2, Seguro, venceEnDias: 300),
            Documento(3, Vtv, venceEnDias: 40),
        };

        var vigentes = CalculadorEstadoVehiculo.VigentesDeCadaTipo(documentos)
            .OrderBy(documento => documento.Id)
            .ToList();

        Assert.Equal(2, vigentes.Count);
        Assert.Equal([2, 3], vigentes.Select(documento => documento.Id));
    }

    // ── FR-034: ningún tipo es obligatorio ──────────────────────────────────────────────────────

    [Fact]
    public void ConUnSoloDocumentoAlDia_EsEnRegla_AunqueLeFaltenOtrosTipos()
    {
        // El sistema informa sobre lo cargado y no infiere que falte un documento que nunca se
        // cargó. Qué documentación es obligatoria quedó fuera del alcance del módulo.
        var documentos = new[] { Documento(1, Seguro, venceEnDias: 200) };

        Assert.Equal(
            EstadoDocumentacionVehiculo.EnRegla,
            CalculadorEstadoVehiculo.Calcular(documentos, Hoy));
    }

    // ── FR-016a: el archivo adjunto no entra en la cuenta ───────────────────────────────────────

    [Fact]
    public void LaFaltaDeArchivoAdjunto_NoAlteraElEstadoDelVehiculo()
    {
        // Que un documento no tenga escaneo es un dato del documento, no del vehículo: los cuatro
        // valores se conservan exactamente (FR-016a).
        var sinArchivo = Documento(1, Seguro, venceEnDias: 200);
        var conArchivo = Documento(2, Vtv, venceEnDias: 200, archivoRuta: "2026/08/abc.pdf");

        Assert.False(sinArchivo.TieneArchivo);
        Assert.True(conArchivo.TieneArchivo);

        Assert.Equal(
            CalculadorEstadoVehiculo.Calcular([conArchivo], Hoy),
            CalculadorEstadoVehiculo.Calcular([sinArchivo], Hoy));
    }
}
