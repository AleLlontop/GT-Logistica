using GT.Domain.Choferes;

namespace GT.UnitTests.Choferes;

/// <summary>
/// Cubre FR-029 (los cuatro valores y su precedencia), FR-020a (de cada tipo manda el de vencimiento
/// más lejano) y FR-029a (ningún tipo es obligatorio).
/// </summary>
public class CalculadorEstadoChoferTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 6);

    private const int Licencia = 1;
    private const int Psicofisico = 2;

    private static Documentacion Documento(
        int id,
        int tipoId,
        int venceEnDias,
        int diasAviso = 30)
    {
        return new Documentacion
        {
            Id = id,
            ChoferId = 1,
            DocumentacionTipoId = tipoId,
            Numero = $"N-{id}",
            FechaEmision = Hoy.AddDays(-400),
            FechaVencimiento = Hoy.AddDays(venceEnDias),
            Tipo = new DocumentacionTipo
            {
                Id = tipoId,
                Nombre = $"Tipo {tipoId}",
                DiasAvisoVencimiento = diasAviso,
            },
        };
    }

    [Fact]
    public void SinDocumentos_EsSinDocumentacion_NoEnRegla()
    {
        // FR-028: un chofer sin papeles no está al día por ausencia de papeles.
        Assert.Equal(
            EstadoDocumentacionChofer.SinDocumentacion,
            CalculadorEstadoChofer.Calcular([], Hoy));
    }

    [Fact]
    public void ConTodoAlDia_EsEnRegla()
    {
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: 200),
            Documento(2, Psicofisico, venceEnDias: 120),
        };

        Assert.Equal(
            EstadoDocumentacionChofer.EnRegla,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    [Fact]
    public void ManaElPeorEstado_LaVencidaGanaALaQueEstaAlDia()
    {
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: 200),
            Documento(2, Psicofisico, venceEnDias: -3),
        };

        Assert.Equal(
            EstadoDocumentacionChofer.Vencida,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    [Fact]
    public void LaVencidaGanaALaProximaAvencer()
    {
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: 10),
            Documento(2, Psicofisico, venceEnDias: -1),
        };

        Assert.Equal(
            EstadoDocumentacionChofer.Vencida,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    [Fact]
    public void LaProximaAvencerGanaALaQueEstaAlDia()
    {
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: 200),
            Documento(2, Psicofisico, venceEnDias: 10),
        };

        Assert.Equal(
            EstadoDocumentacionChofer.ProximaAvencer,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    // ── FR-020a: de cada tipo manda uno solo ────────────────────────────────────────────────────

    [Fact]
    public void UnaRenovacionTapaAlDocumentoViejo_AunqueElViejoEsteVencido()
    {
        // El caso que motiva FR-020a: si el historial contara, este chofer arrastraría para siempre
        // la licencia vencida y el panel se llenaría de alertas que nadie puede resolver.
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: -60),
            Documento(2, Licencia, venceEnDias: 300),
        };

        Assert.Equal(
            EstadoDocumentacionChofer.EnRegla,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    [Fact]
    public void ConEmpateDeVencimiento_MandaElDeIdMayor()
    {
        // Dos documentos del mismo tipo con la misma fecha son un error de carga plausible. Sin
        // desempate, el resultado cambiaría entre dos consultas idénticas (research §8).
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: 50, diasAviso: 0),
            Documento(2, Licencia, venceEnDias: 50, diasAviso: 90),
        };

        var vigente = Assert.Single(CalculadorEstadoChofer.VigentesDeCadaTipo(documentos));

        Assert.Equal(2, vigente.Id);
    }

    [Fact]
    public void VigentesDeCadaTipo_DevuelveUnoPorTipo()
    {
        var documentos = new[]
        {
            Documento(1, Licencia, venceEnDias: -60),
            Documento(2, Licencia, venceEnDias: 300),
            Documento(3, Psicofisico, venceEnDias: 40),
        };

        var vigentes = CalculadorEstadoChofer.VigentesDeCadaTipo(documentos)
            .OrderBy(documento => documento.Id)
            .ToList();

        Assert.Equal(2, vigentes.Count);
        Assert.Equal([2, 3], vigentes.Select(documento => documento.Id));
    }

    // ── FR-029a: ningún tipo es obligatorio ─────────────────────────────────────────────────────

    [Fact]
    public void ConUnSoloDocumentoAlDia_EsEnRegla_AunqueLeFaltenOtrosTipos()
    {
        // El sistema informa sobre lo cargado y no infiere que falte un documento que nunca se
        // cargó. Qué documentación es obligatoria quedó fuera del alcance del módulo.
        var documentos = new[] { Documento(1, Licencia, venceEnDias: 200) };

        Assert.Equal(
            EstadoDocumentacionChofer.EnRegla,
            CalculadorEstadoChofer.Calcular(documentos, Hoy));
    }

    [Fact]
    public void ElArchivoAdjuntoNoAlteraElEstado()
    {
        // Que un documento no tenga escaneo es un dato del documento, no del chofer (FR-029).
        var sinArchivo = Documento(1, Licencia, venceEnDias: 200);

        Assert.False(sinArchivo.TieneArchivo);
        Assert.Equal(
            EstadoDocumentacionChofer.EnRegla,
            CalculadorEstadoChofer.Calcular([sinArchivo], Hoy));
    }
}
