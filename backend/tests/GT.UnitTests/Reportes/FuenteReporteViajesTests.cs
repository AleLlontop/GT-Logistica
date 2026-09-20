using GT.Application.Choferes;
using GT.Application.Reportes;
using GT.Application.Reportes.Fuentes;
using GT.Application.Viajes;
using Microsoft.Extensions.Time.Testing;

namespace GT.UnitTests.Reportes;

/// <summary>
/// La fuente del reporte de viajes: sus diez columnas, las palabras de sus estados, sus celdas vacías
/// y su total (US1, data-model §3.1).
/// </summary>
public class FuenteReporteViajesTests
{
    private static readonly FakeTimeProvider Reloj =
        new(new DateTimeOffset(2026, 9, 20, 17, 32, 0, TimeSpan.Zero));

    // ── Columnas ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Diez y sólo diez**, con los nombres de la pantalla y en orden. *Ruta* y *Asignación* van
    /// abiertas en dos columnas cada una: en un archivo que se va a ordenar y filtrar, cada dato va en
    /// su columna (FR-008).
    /// </summary>
    [Fact]
    public async Task LasDiezColumnasDeFr008EnOrden()
    {
        var (_, capturador) = await EjecutarAsync([Viaje()]);

        Assert.Equal(
            [
                "Número", "Fecha", "Cliente", "Origen", "Destino",
                "Chofer", "Vehículo", "Transportista", "Estado", "Importe",
            ],
            capturador.NombresDeColumna);
    }

    // ── Palabras de los estados (FR-010) ────────────────────────────────────────────────────────

    /// <summary>La columna Estado lleva <c>En curso</c>, **nunca** el <c>enCurso</c> del JSON.</summary>
    [Fact]
    public async Task ElEstadoVaConLaPalabraDeLaPantalla()
    {
        var (_, capturador) = await EjecutarAsync([Viaje(estado: "enCurso")]);

        Assert.Equal("En curso", capturador.Fila(0)[8].ValorTexto);
    }

    [Theory]
    [InlineData("pendiente", "Pendiente")]
    [InlineData("rendido", "Rendido")]
    [InlineData("anulado", "Anulado")]
    [InlineData("facturado", "Facturado")]
    public async Task LosDemasEstadosTambien(string enJson, string enPantalla)
    {
        var (_, capturador) = await EjecutarAsync([Viaje(estado: enJson)]);

        Assert.Equal(enPantalla, capturador.Fila(0)[8].ValorTexto);
    }

    // ── Celdas vacías (spec §Edge Cases) ────────────────────────────────────────────────────────

    /// <summary>
    /// Un viaje sin chofer ni vehículo deja **las dos celdas vacías**, sin texto de relleno: ni
    /// "Sin asignar", ni un guion. Como la pantalla.
    /// </summary>
    [Fact]
    public async Task UnViajeSinAsignarDejaLasDosCeldasVacias()
    {
        var (_, capturador) = await EjecutarAsync([Viaje(chofer: null, vehiculo: null)]);

        Assert.True(capturador.Fila(0)[5].EstaVacia);
        Assert.True(capturador.Fila(0)[6].EstaVacia);
    }

    /// <summary>El importe viaja como <c>decimal</c>, no como texto ya formateado (FR-011).</summary>
    [Fact]
    public async Task ElImporteVaComoNumero()
    {
        var (_, capturador) = await EjecutarAsync([Viaje(importe: 1_240_000m)]);

        Assert.Equal(TipoDeColumna.Importe, capturador.Fila(0)[9].Tipo);
        Assert.Equal(1_240_000m, capturador.Fila(0)[9].ValorImporte);
    }

    // ── Total (FR-009, SC-004) ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// **El total coincide con la suma de las filas del propio reporte**, que es lo que hace que
    /// SC-004 se compruebe sumando el archivo con la calculadora de la planilla.
    /// </summary>
    [Fact]
    public async Task ElTotalEsLaSumaDeLasFilasDelReporte()
    {
        var (_, capturador) = await EjecutarAsync(
            [Viaje(importe: 100_000m), Viaje(importe: 250_500.50m), Viaje(importe: 49_499.50m)]);

        var total = Assert.Single(capturador.Ultimo!.Totales);

        Assert.Equal("Total de importes", total.Etiqueta);
        Assert.Equal(400_000m, total.Importe);
        Assert.Equal(
            capturador.Ultimo.Filas.Sum(fila => fila[9].ValorImporte ?? 0m),
            total.Importe);
    }

    // ── Tope (FR-016) ───────────────────────────────────────────────────────────────────────────

    /// <summary>Con filas **iguales** al tope se entrega: FR-016 rechaza "más de".</summary>
    [Fact]
    public async Task ConFilasIgualesAlTope_SeEntrega()
    {
        var (resultado, _) = await EjecutarAsync([Viaje(), Viaje(), Viaje()], total: 3, tope: 3);

        Assert.True(resultado.SeEntrega);
    }

    [Fact]
    public async Task ConUnaFilaDeMas_ElTopeSeSuperaYNoSeArmaNada()
    {
        var (resultado, capturador) = await EjecutarAsync([Viaje()], total: 4, tope: 3);

        Assert.True(resultado.SuperaElTope);
        Assert.Equal(4, resultado.Filas);
        Assert.Equal(3, resultado.Tope);
        Assert.Null(capturador.Ultimo);
    }

    // ── La consulta (research §3) ───────────────────────────────────────────────────────────────

    /// <summary>
    /// **Pide la primera página con el tamaño del tope**: todas las filas del filtro en una sola
    /// vuelta, con el orden de la pantalla heredado de la misma consulta.
    /// </summary>
    [Fact]
    public async Task PideTodasLasFilasDelFiltroEnUnaSolaVuelta()
    {
        var repositorio = new RepositorioViajesDoble(Pagina([Viaje()], total: 1));
        var (armado, _) = Dobles.Armado(tope: 5000);

        await Fuente(repositorio, armado).EjecutarAsync(
            new FiltrosDelReporteDeViajes(), FormatoDeReporte.Pdf, "jlopez");

        Assert.Equal(5000, repositorio.TamanioPedido);
        Assert.Equal(1, repositorio.FiltrosPedidos!.Pagina);
    }

    // ── Línea de filtros (FR-006, research §9) ──────────────────────────────────────────────────

    /// <summary>
    /// **Cliente y transportista se resuelven a su nombre en el backend**: la pantalla tiene
    /// identificadores y el frontend no manda ningún texto.
    /// </summary>
    [Fact]
    public async Task LaLineaDeFiltrosResuelveLosNombres()
    {
        var repositorio = new RepositorioViajesDoble(Pagina([Viaje()], total: 1));
        var (armado, capturador) = Dobles.Armado();

        var fuente = new FuenteReporteViajes(
            new ConsultarViajes(repositorio, Reloj),
            new RepositorioClientesDoble(new() { [7] = "Aceitera del Sur" }),
            new RepositorioTransportistasDoble(new() { [3] = "Fletes del Norte" }),
            armado);

        await fuente.EjecutarAsync(
            new FiltrosDelReporteDeViajes(
                ClienteId: 7,
                TransportistaId: 3,
                Estado: "enCurso",
                Desde: new DateOnly(2026, 9, 1),
                Hasta: new DateOnly(2026, 9, 20),
                Busqueda: "rosario"),
            FormatoDeReporte.Pdf,
            "jlopez");

        Assert.Equal(
            "Cliente: Aceitera del Sur · Transportista: Fletes del Norte · Estado: En curso · " +
            "Desde: 01/09/2026 · Hasta: 20/09/2026 · Búsqueda: rosario",
            capturador.Ultimo!.Encabezado.Filtros);
    }

    [Fact]
    public async Task SinFiltros_LaLineaLoDice()
    {
        var (_, capturador) = await EjecutarAsync([Viaje()]);

        Assert.Equal("Sin filtros aplicados", capturador.Ultimo!.Encabezado.Filtros);
    }

    [Fact]
    public async Task ElTituloEsElDeLaPantalla()
    {
        var (_, capturador) = await EjecutarAsync([Viaje()]);

        Assert.Equal("Reporte de viajes", capturador.Ultimo!.Encabezado.Titulo);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    private static async Task<(ResultadoReporte Resultado, ArmadorCapturador Capturador)> EjecutarAsync(
        IReadOnlyList<ViajeListado> viajes,
        int? total = null,
        int tope = OpcionesDeReporte.TopePorDefecto)
    {
        var repositorio = new RepositorioViajesDoble(Pagina(viajes, total ?? viajes.Count));
        var (armado, capturador) = Dobles.Armado(tope);

        var resultado = await Fuente(repositorio, armado).EjecutarAsync(
            new FiltrosDelReporteDeViajes(), FormatoDeReporte.Pdf, "jlopez");

        return (resultado, capturador);
    }

    private static FuenteReporteViajes Fuente(RepositorioViajesDoble repositorio, ArmadoDeReporte armado) =>
        new(
            new ConsultarViajes(repositorio, Reloj),
            new RepositorioClientesDoble([]),
            new RepositorioTransportistasDoble([]),
            armado);

    private static PaginaDe<ViajeListado> Pagina(IReadOnlyList<ViajeListado> viajes, int total) =>
        new(viajes, total, 1, Math.Max(viajes.Count, 1));

    private static ViajeListado Viaje(
        int numero = 1041,
        string estado = "pendiente",
        decimal importe = 100_000m,
        string? chofer = "Pérez, Juan",
        string? vehiculo = "AB123CD") =>
        new(
            numero,
            numero,
            "2026-09-14",
            new Resumen(1, "Aceitera del Sur", true),
            "Rosario",
            "Córdoba",
            chofer is null ? null : new Resumen(2, chofer, true),
            vehiculo is null ? null : new Resumen(3, vehiculo, true),
            new Resumen(4, "Fletes del Norte", true),
            estado,
            importe,
            false,
            false,
            null);
}
