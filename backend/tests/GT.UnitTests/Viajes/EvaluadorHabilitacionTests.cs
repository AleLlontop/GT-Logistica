using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;

namespace GT.UnitTests.Viajes;

/// <summary>
/// Cubre FR-022 a FR-024 y SC-014: el veredicto se calcula contra la <b>fecha del viaje</b>, no
/// contra el día en curso.
///
/// Los bordes que a mano dependerían del calendario —y por eso viven acá y no en el quickstart—:
/// el documento que vence exactamente el día del viaje, el tipo con cero días de aviso, la unidad sin
/// ningún documento, y el viaje retroactivo con un documento vencido hoy pero vigente ese día.
/// </summary>
public class EvaluadorHabilitacionTests
{
    private static readonly DateOnly FechaDelViaje = new(2026, 8, 10);

    private const int Licencia = 1;
    private const int Psicofisico = 2;

    private static Documentacion DelChofer(
        int id,
        int tipoId,
        DateOnly vencimiento,
        int diasAviso = 30,
        string? nombreTipo = null) =>
        new()
        {
            Id = id,
            ChoferId = 1,
            DocumentacionTipoId = tipoId,
            Numero = $"N-{id}",
            FechaEmision = vencimiento.AddYears(-1),
            FechaVencimiento = vencimiento,
            Tipo = new DocumentacionTipo
            {
                Id = tipoId,
                Nombre = nombreTipo ?? $"Tipo {tipoId}",
                DiasAvisoVencimiento = diasAviso,
                Ambito = DocumentacionAmbito.Chofer,
            },
        };

    private static DocumentacionVehiculo DelVehiculo(
        int id,
        int tipoId,
        DateOnly vencimiento,
        int diasAviso = 30) =>
        new()
        {
            Id = id,
            VehiculoId = 1,
            DocumentacionTipoId = tipoId,
            Numero = $"V-{id}",
            FechaEmision = vencimiento.AddYears(-1),
            FechaVencimiento = vencimiento,
            Tipo = new DocumentacionTipo
            {
                Id = tipoId,
                Nombre = $"Tipo {tipoId}",
                DiasAvisoVencimiento = diasAviso,
                Ambito = DocumentacionAmbito.Vehiculo,
            },
        };

    // ── FR-024: ningún documento cargado habilita ───────────────────────────────────────────────

    [Fact]
    public void ChoferSinNingunDocumento_EstaHabilitado()
    {
        // Contradice al Módulo 4, donde una unidad sin documentación no puede quedar disponible, y es
        // deliberado: acá se pregunta si hay algo cargado que **prohíba** este viaje (FR-024).
        var veredicto = EvaluadorHabilitacion.ParaChofer([], FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.Habilitado, veredicto.Habilitacion);
        Assert.Null(veredicto.DocumentoQueDecide);
    }

    [Fact]
    public void VehiculoSinNingunDocumento_EstaHabilitado()
    {
        var veredicto = EvaluadorHabilitacion.ParaVehiculo([], FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.Habilitado, veredicto.Habilitacion);
    }

    // ── El borde declarado: vence exactamente el día del viaje ──────────────────────────────────

    [Fact]
    public void ElQueVenceElDiaDelViaje_EsConAdvertencia_NoBloqueado()
    {
        // Es el mismo borde que el Módulo 3 fija para "hoy", evaluado contra otra fecha: el documento
        // vale hasta su fecha de vencimiento inclusive (FR-024, SC-014).
        var documentos = new[] { DelChofer(1, Licencia, FechaDelViaje) };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.ConAdvertencia, veredicto.Habilitacion);
    }

    [Fact]
    public void ElQueVencioElDiaAnterior_Bloquea()
    {
        var documentos = new[] { DelChofer(1, Licencia, FechaDelViaje.AddDays(-1)) };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.Bloqueado, veredicto.Habilitacion);
    }

    // ── Tipo con cero días de aviso: no hay ventana intermedia ──────────────────────────────────

    [Fact]
    public void ConCeroDiasDeAviso_SoloElQueVenceEseDiaAdvierte()
    {
        var alDia = new[] { DelChofer(1, Licencia, FechaDelViaje.AddDays(1), diasAviso: 0) };
        var eseDia = new[] { DelChofer(2, Licencia, FechaDelViaje, diasAviso: 0) };

        Assert.Equal(
            HabilitacionAsignacion.Habilitado,
            EvaluadorHabilitacion.ParaChofer(alDia, FechaDelViaje).Habilitacion);

        Assert.Equal(
            HabilitacionAsignacion.ConAdvertencia,
            EvaluadorHabilitacion.ParaChofer(eseDia, FechaDelViaje).Habilitacion);
    }

    // ── SC-014: la carga retroactiva dice la verdad ─────────────────────────────────────────────

    [Fact]
    public void UnDocumentoVencidoHoy_PeroVigenteALaFechaDelViaje_NoBloquea()
    {
        // El viaje se hizo el mes pasado con la licencia en regla; hoy está vencida. Evaluar contra
        // hoy rechazaría un viaje que realmente ocurrió (SC-014, US3 esc. 13).
        var fechaDelViajeRetroactivo = new DateOnly(2026, 7, 1);
        var venceDespuesDelViajePeroAntesDeHoy = new DateOnly(2026, 7, 20);

        var documentos = new[]
        {
            DelChofer(1, Licencia, venceDespuesDelViajePeroAntesDeHoy, diasAviso: 0),
        };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, fechaDelViajeRetroactivo);

        Assert.Equal(HabilitacionAsignacion.Habilitado, veredicto.Habilitacion);
    }

    [Fact]
    public void UnDocumentoVigenteHoy_PeroVencidoALaFechaDeUnViajeFuturo_Bloquea()
    {
        // El espejo del anterior: el viaje del mes que viene se rechaza si el papel vence antes
        // (US3 esc. 6).
        var fechaDelViajeFuturo = new DateOnly(2026, 9, 15);

        var documentos = new[] { DelChofer(1, Licencia, new DateOnly(2026, 9, 1)) };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, fechaDelViajeFuturo);

        Assert.Equal(HabilitacionAsignacion.Bloqueado, veredicto.Habilitacion);
    }

    // ── Precedencia y qué documento nombra el mensaje ───────────────────────────────────────────

    [Fact]
    public void ElVencidoGanaAlProximoAvencer()
    {
        var documentos = new[]
        {
            DelChofer(1, Licencia, FechaDelViaje.AddDays(5)),
            DelChofer(2, Psicofisico, FechaDelViaje.AddDays(-3)),
        };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.Bloqueado, veredicto.Habilitacion);
        Assert.Equal("N-2", veredicto.DocumentoQueDecide!.Numero);
    }

    [Fact]
    public void ElBloqueoNombraTipoNumeroYVencimiento()
    {
        // El mensaje de FR-022 los usa los tres: sin eso, quien opera sabe que no puede pero no qué
        // resolver.
        var vencimiento = FechaDelViaje.AddDays(-10);

        var documentos = new[] { DelChofer(7, Licencia, vencimiento, nombreTipo: "Licencia") };

        var documento = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje).DocumentoQueDecide;

        Assert.NotNull(documento);
        Assert.Equal("Licencia", documento.Tipo);
        Assert.Equal("N-7", documento.Numero);
        Assert.Equal(vencimiento, documento.FechaVencimiento);
    }

    [Fact]
    public void ConVariosVencidos_NombraElQueVencioPrimero()
    {
        // Cualquiera de los dos serviría para explicar el rechazo; lo que no puede pasar es que dos
        // consultas idénticas nombren documentos distintos.
        var documentos = new[]
        {
            DelChofer(1, Licencia, FechaDelViaje.AddDays(-2)),
            DelChofer(2, Psicofisico, FechaDelViaje.AddDays(-40)),
        };

        var documento = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje).DocumentoQueDecide;

        Assert.Equal("N-2", documento!.Numero);
    }

    // ── La renovación tapa al viejo, igual que en los Módulos 3 y 4 ─────────────────────────────

    [Fact]
    public void UnaRenovacionTapaAlDocumentoVencidoDelMismoTipo()
    {
        var documentos = new[]
        {
            DelChofer(1, Licencia, FechaDelViaje.AddDays(-60)),
            DelChofer(2, Licencia, FechaDelViaje.AddDays(300)),
        };

        var veredicto = EvaluadorHabilitacion.ParaChofer(documentos, FechaDelViaje);

        Assert.Equal(HabilitacionAsignacion.Habilitado, veredicto.Habilitacion);
    }

    // ── La misma regla vale para el vehículo, sobre su propia tabla ─────────────────────────────

    [Fact]
    public void ElVehiculoSeEvaluaConLaMismaRegla()
    {
        var vencido = new[] { DelVehiculo(1, Licencia, FechaDelViaje.AddDays(-1)) };
        var porVencer = new[] { DelVehiculo(2, Licencia, FechaDelViaje.AddDays(5)) };
        var alDia = new[] { DelVehiculo(3, Licencia, FechaDelViaje.AddDays(400)) };

        Assert.Equal(
            HabilitacionAsignacion.Bloqueado,
            EvaluadorHabilitacion.ParaVehiculo(vencido, FechaDelViaje).Habilitacion);

        Assert.Equal(
            HabilitacionAsignacion.ConAdvertencia,
            EvaluadorHabilitacion.ParaVehiculo(porVencer, FechaDelViaje).Habilitacion);

        Assert.Equal(
            HabilitacionAsignacion.Habilitado,
            EvaluadorHabilitacion.ParaVehiculo(alDia, FechaDelViaje).Habilitacion);
    }
}
