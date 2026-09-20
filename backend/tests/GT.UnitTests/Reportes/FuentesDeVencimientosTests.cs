using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion;
using GT.Application.Flota.Documentacion;
using GT.Application.Reportes;
using GT.Application.Reportes.Fuentes;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Personas;
using Microsoft.Extensions.Time.Testing;

namespace GT.UnitTests.Reportes;

/// <summary>
/// Las tres fuentes de los paneles de vencimientos (US2, data-model §3.2, §3.3 y §3.4).
///
/// Las tres comparten dos cosas: <b>no tienen filtros</b> —su encabezado dice siempre
/// <c>Sin filtros aplicados</c>— y <b>no llevan el escaneo del documento ni su enlace</b>, porque el
/// permiso separado de los Módulos 3 y 4 existe para que Gerencia vea qué vence sin llegar al dato
/// personal sensible, y el reporte no puede ser una puerta lateral a eso.
/// </summary>
public class FuentesDeVencimientosTests
{
    private static readonly FakeTimeProvider Reloj =
        new(new DateTimeOffset(2026, 9, 20, 17, 32, 0, TimeSpan.Zero));

    /// <summary>Vence en tres días desde el 20/09/2026, con aviso de treinta: próxima a vencer.</summary>
    private static readonly DateOnly ProximaAvencer = new(2026, 9, 23);

    private static readonly DateOnly YaVencida = new(2026, 9, 10);

    // ── Choferes (data-model §3.2) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Choferes_LasCincoColumnasEnOrden()
    {
        var (_, capturador) = await ChoferesAsync([DocumentoDeChofer()]);

        Assert.Equal(
            ["Chofer", "Transportista", "Documento", "Fecha de vencimiento", "Estado"],
            capturador.NombresDeColumna);
    }

    /// <summary>El chofer va como <c>Apellido, Nombre</c>, igual que en el panel.</summary>
    [Fact]
    public async Task Choferes_ElChoferVaApellidoNombre()
    {
        var (_, capturador) = await ChoferesAsync([DocumentoDeChofer()]);

        Assert.Equal("Pérez, Juan", capturador.Fila(0)[0].ValorTexto);
    }

    /// <summary>La palabra del panel, **nunca** el <c>proximaAvencer</c> del JSON (FR-010).</summary>
    [Theory]
    [InlineData(false, "Próxima a vencer")]
    [InlineData(true, "Vencida")]
    public async Task Choferes_ElEstadoVaConLaPalabraDelPanel(bool vencida, string esperada)
    {
        var (_, capturador) = await ChoferesAsync(
            [DocumentoDeChofer(vencimiento: vencida ? YaVencida : ProximaAvencer)]);

        Assert.Equal(esperada, capturador.Fila(0)[4].ValorTexto);
    }

    [Fact]
    public async Task Choferes_SinFiltrosYSinTotales()
    {
        var (_, capturador) = await ChoferesAsync([DocumentoDeChofer()]);

        Assert.Equal("Sin filtros aplicados", capturador.Ultimo!.Encabezado.Filtros);
        Assert.Empty(capturador.Ultimo.Totales);
        Assert.Equal("Reporte de vencimientos de choferes", capturador.Ultimo.Encabezado.Titulo);
    }

    /// <summary>**Cinco columnas, y ninguna es el escaneo ni su enlace.**</summary>
    [Fact]
    public async Task Choferes_NoLlevaElEscaneoNiSuEnlace()
    {
        var (_, capturador) = await ChoferesAsync([DocumentoDeChofer(conArchivo: true)]);

        Assert.Equal(5, capturador.Ultimo!.Columnas.Count);
        Assert.DoesNotContain(
            capturador.Fila(0),
            celda => celda.ValorTexto?.Contains("licencia.pdf") == true);
    }

    // ── Flota (data-model §3.3) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Flota_LasCincoColumnasEnOrden()
    {
        var (_, capturador) = await FlotaAsync([DocumentoDeVehiculo()]);

        Assert.Equal(
            ["Patente", "Transportista", "Documento", "Fecha de vencimiento", "Estado"],
            capturador.NombresDeColumna);
    }

    /// <summary>
    /// **El panel de Flota dice <c>Vigente</c> donde el de Choferes dice <c>Al día</c>.** El estado
    /// vigente no llega al panel, pero la palabra es la de su propia pantalla.
    /// </summary>
    [Theory]
    [InlineData(false, "Próxima a vencer")]
    [InlineData(true, "Vencida")]
    public async Task Flota_ElEstadoVaConLaPalabraDelPanel(bool vencida, string esperada)
    {
        var (_, capturador) = await FlotaAsync(
            [DocumentoDeVehiculo(vencimiento: vencida ? YaVencida : ProximaAvencer)]);

        Assert.Equal(esperada, capturador.Fila(0)[4].ValorTexto);
    }

    [Fact]
    public async Task Flota_SinFiltrosYSinTotales()
    {
        var (_, capturador) = await FlotaAsync([DocumentoDeVehiculo()]);

        Assert.Equal("Sin filtros aplicados", capturador.Ultimo!.Encabezado.Filtros);
        Assert.Empty(capturador.Ultimo.Totales);
        Assert.Equal("Reporte de vencimientos de flota", capturador.Ultimo.Encabezado.Titulo);
    }

    // ── Facturas (data-model §3.4) ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Facturas_LasCincoColumnasEnOrden()
    {
        var (_, capturador) = await FacturasAsync([Vencimiento()]);

        Assert.Equal(
            ["Cliente", "Número", "Importe", "Vencimiento", "Situación"],
            capturador.NombresDeColumna);
    }

    /// <summary>Las tres ramas de <c>situacion(dias)</c>, incluida la del cero.</summary>
    [Theory]
    [InlineData(-3, "Vencida hace 3 días")]
    [InlineData(-1, "Vencida hace 1 día")]
    [InlineData(0, "Vence hoy")]
    [InlineData(1, "Vence en 1 día")]
    [InlineData(5, "Vence en 5 días")]
    public async Task Facturas_LaSituacionTieneLasTresRamas(int dias, string esperada)
    {
        var (_, capturador) = await FacturasAsync([Vencimiento(dias: dias)]);

        Assert.Equal(esperada, capturador.Fila(0)[4].ValorTexto);
    }

    /// <summary>El número viaja **ya armado por el backend** y no se vuelve a componer acá.</summary>
    [Fact]
    public async Task Facturas_ElNumeroLlegaArmado()
    {
        var (_, capturador) = await FacturasAsync([Vencimiento(numero: "0001-00000012")]);

        Assert.Equal("0001-00000012", capturador.Fila(0)[1].ValorTexto);
    }

    /// <summary>**El total es la suma de las filas del propio reporte** (FR-009, SC-004).</summary>
    [Fact]
    public async Task Facturas_ElTotalEsLaSumaDeSusFilas()
    {
        var (_, capturador) = await FacturasAsync(
            [Vencimiento(total: 120_000m), Vencimiento(total: 64_500.25m)]);

        var total = Assert.Single(capturador.Ultimo!.Totales);

        Assert.Equal("Total de importes", total.Etiqueta);
        Assert.Equal(184_500.25m, total.Importe);
        Assert.Equal(
            capturador.Ultimo.Filas.Sum(fila => fila[2].ValorImporte ?? 0m),
            total.Importe);
    }

    [Fact]
    public async Task Facturas_SinFiltros()
    {
        var (_, capturador) = await FacturasAsync([Vencimiento()]);

        Assert.Equal("Sin filtros aplicados", capturador.Ultimo!.Encabezado.Filtros);
        Assert.Equal("Reporte de vencimientos de facturas", capturador.Ultimo.Encabezado.Titulo);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    private static async Task<(ResultadoReporte, ArmadorCapturador)> ChoferesAsync(
        List<Documentacion> documentos)
    {
        var (armado, capturador) = Dobles.Armado();

        var fuente = new FuenteReporteVencimientosChoferes(
            new GT.Application.Choferes.Documentacion.ConsultarVencimientos(
                new RepositorioDocumentacionDoble(documentos)),
            armado);

        return (await fuente.EjecutarAsync(FormatoDeReporte.Pdf, "jlopez"), capturador);
    }

    private static async Task<(ResultadoReporte, ArmadorCapturador)> FlotaAsync(
        List<DocumentacionVehiculo> documentos)
    {
        var (armado, capturador) = Dobles.Armado();

        var fuente = new FuenteReporteVencimientosFlota(
            new ConsultarVencimientosFlota(new RepositorioDocumentacionVehiculoDoble(documentos)),
            armado);

        return (await fuente.EjecutarAsync(FormatoDeReporte.Pdf, "jlopez"), capturador);
    }

    private static async Task<(ResultadoReporte, ArmadorCapturador)> FacturasAsync(
        IReadOnlyList<FilaDeVencimiento> filas)
    {
        var (armado, capturador) = Dobles.Armado();

        var fuente = new FuenteReporteVencimientosFacturas(
            new GT.Application.Facturacion.ConsultarVencimientos(
                new RepositorioFacturasDoble(filas), Reloj),
            armado);

        return (await fuente.EjecutarAsync(FormatoDeReporte.Pdf, "jlopez"), capturador);
    }

    private static Documentacion DocumentoDeChofer(
        DateOnly? vencimiento = null,
        bool conArchivo = false) =>
        new()
        {
            Id = 1,
            ChoferId = 1,
            DocumentacionTipoId = 1,
            Numero = "12345",
            FechaEmision = new DateOnly(2024, 1, 1),
            FechaVencimiento = vencimiento ?? ProximaAvencer,
            ArchivoRuta = conArchivo ? "2026/09/abc.pdf" : null,
            ArchivoNombre = conArchivo ? "licencia.pdf" : null,
            Tipo = Tipo("Licencia de conducir", DocumentacionAmbito.Chofer),
            Chofer = new Chofer
            {
                Id = 1,
                PersonaId = 1,
                Cuil = "20123456789",
                TransportistaId = 1,
                Persona = new Persona
                {
                    Id = 1,
                    Nombre = "Juan",
                    Apellido = "Pérez",
                    Dni = "12345678",
                    Tipo = TipoIntegrante.Chofer,
                    Telefono = "",
                    Email = "",
                    FechaNacimiento = new DateOnly(1985, 3, 2),
                },
                Transportista = new Transportista
                {
                    Id = 1,
                    Nombre = "Fletes del Norte",
                    Cuit = "30712345678",
                    Tipo = TipoPersona.Juridica,
                    Telefono = "",
                    Email = "",
                },
            },
        };

    private static DocumentacionVehiculo DocumentoDeVehiculo(DateOnly? vencimiento = null) =>
        new()
        {
            Id = 1,
            VehiculoId = 1,
            DocumentacionTipoId = 2,
            Numero = "98765",
            FechaEmision = new DateOnly(2024, 1, 1),
            FechaVencimiento = vencimiento ?? ProximaAvencer,
            Tipo = Tipo("Seguro del vehículo", DocumentacionAmbito.Vehiculo),
            Vehiculo = new Vehiculo
            {
                Id = 1,
                Patente = "AB123CD",
                Marca = "Scania",
                Modelo = "R450",
                TipoVehiculoId = 1,
                TransportistaId = 1,
                EstadoOperativo = VehiculoEstado.Disponible,
                Transportista = new Transportista
                {
                    Id = 1,
                    Nombre = "Fletes del Norte",
                    Cuit = "30712345678",
                    Tipo = TipoPersona.Juridica,
                    Telefono = "",
                    Email = "",
                },
            },
        };

    private static DocumentacionTipo Tipo(string nombre, DocumentacionAmbito ambito) => new()
    {
        Id = 1,
        Nombre = nombre,
        DiasAvisoVencimiento = 30,
        Ambito = ambito,
    };

    private static FilaDeVencimiento Vencimiento(
        string numero = "0001-00000012",
        decimal total = 120_000m,
        int dias = 3) =>
        new(1, numero, "Aceitera del Sur", total, "2026-09-23", dias);
}
