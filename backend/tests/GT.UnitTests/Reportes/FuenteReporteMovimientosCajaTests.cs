using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Reportes;
using GT.Application.Reportes.Fuentes;

namespace GT.UnitTests.Reportes;

/// <summary>
/// La fuente del reporte de movimientos de caja: sus seis columnas, las palabras de su tipo, su
/// referencia vacía y sus <b>tres</b> totales (US3, data-model §3.5).
/// </summary>
public class FuenteReporteMovimientosCajaTests
{
    [Fact]
    public async Task LasSeisColumnasEnOrden()
    {
        var (_, capturador) = await EjecutarAsync([Movimiento()]);

        Assert.Equal(
            ["Fecha", "Tipo", "Importe", "Concepto", "Responsable", "Referencia"],
            capturador.NombresDeColumna);
    }

    /// <summary>
    /// **<c>Ingreso</c> y <c>Egreso</c>, con mayúscula**: no el <c>ingreso</c>/<c>egreso</c> del JSON,
    /// que es un código (FR-010).
    /// </summary>
    [Theory]
    [InlineData("ingreso", "Ingreso")]
    [InlineData("egreso", "Egreso")]
    public async Task ElTipoVaConLaPalabraDeLaPantalla(string enJson, string enPantalla)
    {
        var (_, capturador) = await EjecutarAsync([Movimiento(tipo: enJson)]);

        Assert.Equal(enPantalla, capturador.Fila(0)[1].ValorTexto);
    }

    /// <summary>Sin referencia, la celda queda **vacía**: ni guion ni texto de relleno.</summary>
    [Fact]
    public async Task SinReferencia_LaCeldaQuedaVacia()
    {
        var (_, capturador) = await EjecutarAsync([Movimiento(referencia: null)]);

        Assert.True(capturador.Fila(0)[5].EstaVacia);
    }

    /// <summary>Con referencia, va la que **armó el backend** y no una compuesta acá.</summary>
    [Fact]
    public async Task ConReferencia_VaLaQueArmoElBackend()
    {
        var (_, capturador) = await EjecutarAsync(
            [Movimiento(referencia: "Factura 0001-00000012 · Aceitera del Sur")]);

        Assert.Equal("Factura 0001-00000012 · Aceitera del Sur", capturador.Fila(0)[5].ValorTexto);
    }

    // ── Los tres totales (FR-009, SC-004) ───────────────────────────────────────────────────────

    /// <summary>
    /// **Los tres se calculan sobre las filas del propio reporte**, con <c>Neto = ingresos − egresos</c>.
    /// Es lo que hace que SC-004 se compruebe sumando las filas del archivo con la calculadora de la
    /// planilla.
    /// </summary>
    [Fact]
    public async Task LosTresTotalesSalenDeLasFilasDelReporte()
    {
        var (_, capturador) = await EjecutarAsync(
        [
            Movimiento(tipo: "ingreso", importe: 150_000m),
            Movimiento(tipo: "ingreso", importe: 34_500m),
            Movimiento(tipo: "egreso", importe: 22_000m),
        ]);

        var totales = capturador.Ultimo!.Totales;

        Assert.Equal(3, totales.Count);
        Assert.Equal(new TotalDeReporte("Total de ingresos", 184_500m), totales[0]);
        Assert.Equal(new TotalDeReporte("Total de egresos", 22_000m), totales[1]);
        Assert.Equal(new TotalDeReporte("Neto", 162_500m), totales[2]);
    }

    [Fact]
    public async Task ConMasEgresosQueIngresos_ElNetoDaNegativoSinFallar()
    {
        var (_, capturador) = await EjecutarAsync(
        [
            Movimiento(tipo: "ingreso", importe: 10_000m),
            Movimiento(tipo: "egreso", importe: 15_000m),
        ]);

        Assert.Equal(-5_000m, capturador.Ultimo!.Totales[2].Importe);
    }

    // ── Filtros (FR-006, research §9) ───────────────────────────────────────────────────────────

    /// <summary>
    /// La caja se resuelve **a su nombre visible en el backend**: el mismo par responsable + día de
    /// apertura con el que el desplegable la nombra, porque una caja no tiene número visible.
    /// </summary>
    [Fact]
    public async Task LaLineaDeFiltrosResuelveElNombreDeLaCaja()
    {
        var (_, capturador) = await EjecutarAsync(
            [Movimiento()],
            filtros: new FiltrosDelReporteDeCaja(
                new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 20), CajaId: 4),
            detalle: Detalle("mruiz"));

        Assert.Equal(
            "Desde: 01/09/2026 · Hasta: 20/09/2026 · Caja: mruiz · 14/09/2026",
            capturador.Ultimo!.Encabezado.Filtros);
    }

    [Fact]
    public async Task SinFiltros_LaLineaLoDice()
    {
        var (_, capturador) = await EjecutarAsync([Movimiento()]);

        Assert.Equal("Sin filtros aplicados", capturador.Ultimo!.Encabezado.Filtros);
        Assert.Equal("Reporte de movimientos de caja", capturador.Ultimo.Encabezado.Titulo);
    }

    // ── Tope (FR-016) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PideTodasLasFilasDelFiltroEnUnaSolaVuelta()
    {
        var repositorio = new RepositorioCajaDoble(Pagina([Movimiento()], 1));
        var (armado, _) = Dobles.Armado(tope: 5000);

        await new FuenteReporteMovimientosCaja(
                new ConsultarMovimientos(repositorio), repositorio, armado)
            .EjecutarAsync(new FiltrosDelReporteDeCaja(), FormatoDeReporte.Pdf, "jlopez");

        Assert.Equal(5000, repositorio.TamanioPedido);
    }

    [Fact]
    public async Task ConUnaFilaDeMas_ElTopeSeSupera()
    {
        var (resultado, capturador) = await EjecutarAsync([Movimiento()], total: 4, tope: 3);

        Assert.True(resultado.SuperaElTope);
        Assert.Null(capturador.Ultimo);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    private static async Task<(ResultadoReporte, ArmadorCapturador)> EjecutarAsync(
        IReadOnlyList<MovimientoListado> movimientos,
        int? total = null,
        int tope = OpcionesDeReporte.TopePorDefecto,
        FiltrosDelReporteDeCaja? filtros = null,
        CajaDetalle? detalle = null)
    {
        var repositorio = new RepositorioCajaDoble(
            Pagina(movimientos, total ?? movimientos.Count), detalle);

        var (armado, capturador) = Dobles.Armado(tope);

        var resultado = await new FuenteReporteMovimientosCaja(
                new ConsultarMovimientos(repositorio), repositorio, armado)
            .EjecutarAsync(
                filtros ?? new FiltrosDelReporteDeCaja(), FormatoDeReporte.Pdf, "jlopez");

        return (resultado, capturador);
    }

    private static PaginaDe<MovimientoListado> Pagina(
        IReadOnlyList<MovimientoListado> movimientos,
        int total) =>
        new(movimientos, total, 1, Math.Max(movimientos.Count, 1));

    private static MovimientoListado Movimiento(
        string tipo = "ingreso",
        decimal importe = 100_000m,
        string? referencia = null) =>
        new(
            1,
            4,
            new DateTime(2026, 9, 14, 17, 30, 0, DateTimeKind.Utc),
            tipo,
            importe,
            "Cobro de factura",
            new UsuarioResumen(9, "mruiz"),
            referencia);

    private static CajaDetalle Detalle(string responsable) =>
        new(
            4,
            new UsuarioResumen(9, responsable),
            new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc),
            "abierta",
            null,
            null,
            10_000m,
            0m,
            0m,
            10_000m,
            false);
}
