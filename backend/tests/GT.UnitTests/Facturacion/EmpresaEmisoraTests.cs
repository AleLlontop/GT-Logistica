using GT.Application.Facturacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Choferes;
using Entidad = GT.Domain.Facturacion.EmpresaEmisora;

namespace GT.UnitTests.Facturacion;

/// <summary>
/// Las reglas puras de la configuración del emisor (FR-002).
///
/// <b>Reutiliza <c>ValidadorCuit</c> y <c>NormalizadorDocumentoNumerico</c> del Módulo 3</b> sin
/// modificarlos: son reglas sobre once dígitos y no saben de choferes ni de empresas (research §13).
/// Estos tests verifican que este módulo los use en el orden correcto, que es lo que puede salir mal.
/// </summary>
public class EmpresaEmisoraTests
{
    private static EmpresaEmisoraRequest Completa(
        string? razonSocial = "G&T Logística S.R.L.",
        string? cuit = "30712345671",
        string? domicilio = "Av. Pellegrini 1234, Rosario",
        string? condicionIva = "IVA Responsable Inscripto",
        string? ingresosBrutos = null,
        string? puntoDeVenta = null,
        string? cbu = null,
        string? telefono = null,
        string? email = null) =>
        new(razonSocial, cuit, domicilio, condicionIva, ingresosBrutos, null, puntoDeVenta, cbu,
            telefono, email);

    // ── Normalización y validación del CUIT ─────────────────────────────────────────────────────

    /// <summary>
    /// El CUIT se normaliza <b>antes</b> de validar. Al revés, escribir <c>30-71234567-1</c> sería
    /// rechazado por formato en vez de aceptado, que es lo que FR-002 pide explícitamente.
    /// </summary>
    [Theory]
    [InlineData("30712345671")]
    [InlineData("30-71234567-1")]
    [InlineData("30.71234567.1")]
    [InlineData(" 30 71234567 1 ")]
    public void ElCuitSeNormalizaAntesDeValidarse(string tipeado)
    {
        var normalizado = NormalizadorDocumentoNumerico.Normalizar(tipeado);

        Assert.Equal("30712345671", normalizado);
        Assert.True(ValidadorCuit.EsValido(tipeado));
    }

    /// <summary>
    /// Verificar sólo la longitud dejaría pasar cualquier número tipeado de más, y un CUIT mal cargado
    /// se descubre recién cuando el comprobante ya salió impreso.
    /// </summary>
    [Theory]
    [InlineData("30712345670")]  // once dígitos, verificador equivocado: el que cierra es el 1
    [InlineData("3071234567")]   // diez dígitos
    [InlineData("307123456710")] // doce dígitos
    [InlineData("")]
    public void ElCuitConVerificadorOLargoEquivocadoSeRechaza(string valor) =>
        Assert.False(ValidadorCuit.EsValido(valor));

    /// <summary>
    /// El rechazo por verificador llega <b>después</b> del validador de campos y con su código propio.
    /// Es la distinción que importa: <c>cuit_invalido</c> le dice a quien opera que el número está mal
    /// tipeado, mientras <c>datos_invalidos</c> le diría sólo que revise el campo.
    /// </summary>
    [Fact]
    public void ElCuitInvalidoSeRechazaConSuCodigoPropioYElCampoMarcado()
    {
        // El validador de campos pasa —el CUIT no está vacío— y el rechazo llega por el verificador.
        Assert.Null(ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa(cuit: "30712345670")));
        Assert.False(ValidadorCuit.EsValido("30712345670"));
    }

    // ── Los cuatro obligatorios ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cada obligatorio vacío se nombra en el mensaje —<c>Completá el domicilio para poder guardar.</c>—
    /// en vez de mandar un "revisá los campos marcados" genérico: son cuatro de un formulario de diez,
    /// y decir cuál falta ahorra buscarlo (contracts/README §Empresa emisora).
    /// </summary>
    [Theory]
    [InlineData("razonSocial", "razón social")]
    [InlineData("domicilio", "domicilio")]
    [InlineData("condicionIva", "condición de IVA")]
    public void CadaObligatorioVacioSeNombraEnElMensaje(string campo, string nombreVisible)
    {
        var peticion = campo switch
        {
            "razonSocial" => Completa(razonSocial: "   "),
            "domicilio" => Completa(domicilio: null),
            _ => Completa(condicionIva: ""),
        };

        var invalido = ValidadorEmpresaEmisora.PrimerCampoInvalido(peticion);

        Assert.NotNull(invalido);
        Assert.Equal(campo, invalido!.Value.Campo);
        Assert.Equal(ErrorFactura.DatosInvalidos, invalido.Value.Error);
        Assert.Equal($"Completá {nombreVisible} para poder guardar.", invalido.Value.Mensaje);
    }

    [Fact]
    public void ElCuitVacioSeNombraTambien()
    {
        var invalido = ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa(cuit: null));

        Assert.NotNull(invalido);
        Assert.Equal("cuit", invalido!.Value.Campo);
        Assert.Equal("Completá CUIT para poder guardar.", invalido.Value.Mensaje);
    }

    [Fact]
    public void LaConfiguracionCompletaNoTieneCamposInvalidos() =>
        Assert.Null(ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa()));

    // ── Los seis opcionales ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los seis opcionales vacíos son válidos: sin logo, sin CBU y sin ingresos brutos la factura se
    /// emite igual (FR-002, FR-004).
    /// </summary>
    [Fact]
    public void LosOpcionalesVaciosSonValidos() =>
        Assert.Null(ValidadorEmpresaEmisora.PrimerCampoInvalido(
            Completa(ingresosBrutos: "", puntoDeVenta: "", cbu: "", telefono: "", email: "")));

    /// <summary>El email es opcional, pero si viene tiene que tener formato válido (FR-002).</summary>
    [Fact]
    public void ElEmailMalFormadoSeRechazaConSuCodigoPropio()
    {
        var invalido = ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa(email: "administracion"));

        Assert.NotNull(invalido);
        Assert.Equal(ErrorFactura.EmailInvalido, invalido!.Value.Error);
        Assert.Equal("email", invalido.Value.Campo);
        Assert.Equal("Escribí un email con formato válido.", invalido.Value.Mensaje);
    }

    /// <summary>El punto de venta arma después el número de comprobante, así que son cuatro dígitos.</summary>
    [Theory]
    [InlineData("14")]
    [InlineData("00014")]
    [InlineData("00A4")]
    public void ElPuntoDeVentaQueNoSonCuatroDigitosSeRechaza(string valor)
    {
        var invalido = ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa(puntoDeVenta: valor));

        Assert.NotNull(invalido);
        Assert.Equal("puntoDeVenta", invalido!.Value.Campo);
    }

    [Fact]
    public void ElPuntoDeVentaDeCuatroDigitosSeAcepta() =>
        Assert.Null(ValidadorEmpresaEmisora.PrimerCampoInvalido(Completa(puntoDeVenta: "0014")));

    // ── Los faltantes que devuelve el GET ───────────────────────────────────────────────────────

    /// <summary>
    /// Con la fila ausente faltan los cuatro: la ausencia <b>es</b> el estado "sin configurar", y eso
    /// es lo que el <c>GET</c> tiene que poder decir sin inventar una fila vacía (US1 esc. 1).
    /// </summary>
    [Fact]
    public void SinFilaFaltanLosCuatroObligatorios()
    {
        var dto = EmpresaEmisoraDto.SinConfigurar();

        Assert.False(dto.Configurada);
        Assert.Equal(
            ["razón social", "CUIT", "domicilio", "condición de IVA"],
            dto.Faltantes);
        Assert.Null(dto.Logo);
    }

    /// <summary>
    /// Con la fila completa no falta nada. Es lo que la emisión consulta antes de armar la factura: si
    /// esta lista viene vacía, FR-006 no rechaza (FR-002, FR-006).
    /// </summary>
    [Fact]
    public void ConLaFilaCompletaNoFaltaNada()
    {
        var empresa = new Entidad
        {
            RazonSocial = "G&T Logística S.R.L.",
            Cuit = "30712345671",
            Domicilio = "Av. Pellegrini 1234, Rosario",
            CondicionIva = "IVA Responsable Inscripto",
        };

        var dto = EmpresaEmisoraDto.Desde(empresa);

        Assert.True(dto.Configurada);
        Assert.Empty(dto.Faltantes);
    }

    /// <summary>
    /// La fila puede existir con un obligatorio vacío sólo si alguien la escribió sin pasar por el caso
    /// de uso. El <c>GET</c> igual lo informa en vez de decir que está configurada (FR-006).
    /// </summary>
    [Fact]
    public void ConUnObligatorioVacioLaFilaExistePeroNoEstaConfigurada()
    {
        var empresa = new Entidad
        {
            RazonSocial = "G&T Logística S.R.L.",
            Cuit = "30712345671",
            Domicilio = "   ",
            CondicionIva = "IVA Responsable Inscripto",
        };

        var dto = EmpresaEmisoraDto.Desde(empresa);

        Assert.False(dto.Configurada);
        Assert.Equal(["domicilio"], dto.Faltantes);
    }

    /// <summary>
    /// El logo se informa con su nombre original y la URL del endpoint autorizado, nunca con la ruta
    /// del volumen: quien mira la pantalla no tiene por qué conocer dónde vive el archivo (Principio V).
    /// </summary>
    [Fact]
    public void ElLogoSeInformaConSuNombreYLaUrlDelEndpoint()
    {
        var empresa = new Entidad
        {
            RazonSocial = "G&T Logística S.R.L.",
            Cuit = "30712345671",
            Domicilio = "Av. Pellegrini 1234",
            CondicionIva = "IVA Responsable Inscripto",
            LogoRuta = "a1/b2c3d4.png",
            LogoTipoContenido = "image/png",
            LogoNombreOriginal = "logo-gt.png",
        };

        var dto = EmpresaEmisoraDto.Desde(empresa);

        Assert.NotNull(dto.Logo);
        Assert.Equal("logo-gt.png", dto.Logo!.Nombre);
        Assert.Equal("/api/facturacion/empresa-emisora/logo", dto.Logo.Url);
        Assert.DoesNotContain("a1/b2c3d4", dto.Logo.Url, StringComparison.Ordinal);
    }
}
