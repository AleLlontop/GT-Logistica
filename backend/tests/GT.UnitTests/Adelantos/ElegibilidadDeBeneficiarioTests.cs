using GT.Domain.Adelantos;
using GT.Domain.Personas;

namespace GT.UnitTests.Adelantos;

/// <summary>
/// La tabla completa de data-model §<c>ElegibilidadDeBeneficiario</c>, una fila por caso, más la
/// precedencia entre motivos. Es la única escritura de FR-002 y FR-009 (research §1).
/// </summary>
public class ElegibilidadDeBeneficiarioTests
{
    private const string CuitEmisora = "30712345671";
    private const string CuitExterno = "20123456786";

    private static PersonaParaAdelanto Persona(
        bool activa = true,
        TipoIntegrante tipo = TipoIntegrante.Chofer,
        bool tieneFicha = true,
        bool fichaActiva = true,
        string? cuitTransportista = CuitEmisora) =>
        new(1, "Pérez", "Juan", "30123456", activa, tipo, tieneFicha, tieneFicha && fichaActiva,
            tieneFicha ? cuitTransportista : null);

    private static PersonaParaAdelanto Empleado(bool activa = true) =>
        Persona(activa, TipoIntegrante.Empleado, tieneFicha: false);

    // ── Las filas de la tabla ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Persona_Nula_EsInexistente() =>
        Assert.Equal(
            MotivoNoElegible.Inexistente,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, null, CuitEmisora));

    [Fact]
    public void Persona_Inactiva_EsInactiva() =>
        Assert.Equal(
            MotivoNoElegible.Inactiva,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Empleado, Empleado(activa: false), CuitEmisora));

    [Fact]
    public void Chofer_SinEmpresaEmisora_LoInforma() =>
        Assert.Equal(
            MotivoNoElegible.EmpresaEmisoraNoConfigurada,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, Persona(), null));

    [Fact]
    public void Chofer_SinFicha_EsDeTipoDistinto() =>
        Assert.Equal(
            MotivoNoElegible.TipoDistinto,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, Persona(tieneFicha: false), CuitEmisora));

    [Fact]
    public void Chofer_ConFichaInactiva_EsInactiva() =>
        Assert.Equal(
            MotivoNoElegible.Inactiva,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, Persona(fichaActiva: false), CuitEmisora));

    [Fact]
    public void Chofer_DeUnTransportistaExterno_EsChoferExterno() =>
        Assert.Equal(
            MotivoNoElegible.ChoferExterno,
            ElegibilidadDeBeneficiario.Evaluar(
                TipoBeneficiario.Chofer,
                Persona(cuitTransportista: CuitExterno),
                CuitEmisora));

    [Fact]
    public void Empleado_ConFicha_EsDeTipoDistinto() =>
        Assert.Equal(
            MotivoNoElegible.TipoDistinto,
            ElegibilidadDeBeneficiario.Evaluar(
                TipoBeneficiario.Empleado,
                Persona(tipo: TipoIntegrante.Empleado),
                CuitEmisora));

    /// <summary>Quien tiene la ficha dada de baja no aparece bajo ningún tipo (spec §Edge Cases).</summary>
    [Fact]
    public void Empleado_ConFichaInactiva_TambienEsDeTipoDistinto() =>
        Assert.Equal(
            MotivoNoElegible.TipoDistinto,
            ElegibilidadDeBeneficiario.Evaluar(
                TipoBeneficiario.Empleado,
                Persona(tipo: TipoIntegrante.Empleado, fichaActiva: false),
                CuitEmisora));

    [Fact]
    public void Persona_DeTipoChoferSinFicha_PedidaComoEmpleado_EsDeTipoDistinto() =>
        Assert.Equal(
            MotivoNoElegible.TipoDistinto,
            ElegibilidadDeBeneficiario.Evaluar(
                TipoBeneficiario.Empleado,
                Persona(tipo: TipoIntegrante.Chofer, tieneFicha: false),
                CuitEmisora));

    [Fact]
    public void Chofer_Propio_Activo_EsElegible() =>
        Assert.Null(ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, Persona(), CuitEmisora));

    [Fact]
    public void Empleado_Activo_SinFicha_EsElegible() =>
        Assert.Null(ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Empleado, Empleado(), CuitEmisora));

    // ── Precedencia y casos cruzados ────────────────────────────────────────────────────────────

    [Fact]
    public void Persona_InactivaYChoferExterno_SeInformaComoInactiva() =>
        Assert.Equal(
            MotivoNoElegible.Inactiva,
            ElegibilidadDeBeneficiario.Evaluar(
                TipoBeneficiario.Chofer,
                Persona(activa: false, cuitTransportista: CuitExterno),
                CuitEmisora));

    /// <summary>FR-002a: los empleados no dependen de la empresa emisora.</summary>
    [Fact]
    public void Empleado_SinEmpresaEmisora_EsElegible() =>
        Assert.Null(ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Empleado, Empleado(), null));

    /// <summary>
    /// Una persona cargada como empleado y registrada después como chofer propio aparece bajo
    /// <i>Chofer</i> y deja de aparecer bajo <i>Empleado</i>: lo decide la ficha, no <c>Persona.Tipo</c>.
    /// </summary>
    [Fact]
    public void Persona_DeTipoEmpleadoConFichaPropiaActiva_EsChoferYNoEmpleado()
    {
        var persona = Persona(tipo: TipoIntegrante.Empleado);

        Assert.Null(ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Chofer, persona, CuitEmisora));
        Assert.Equal(
            MotivoNoElegible.TipoDistinto,
            ElegibilidadDeBeneficiario.Evaluar(TipoBeneficiario.Empleado, persona, CuitEmisora));
    }
}
