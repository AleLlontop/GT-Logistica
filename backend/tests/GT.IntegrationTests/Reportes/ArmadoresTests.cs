using ClosedXML.Excel;
using GT.Application.Reportes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Las dos bibliotecas ejercitadas <b>de verdad</b>, y <b>resolviendo el servicio del contenedor</b> en
/// vez de instanciar las clases (convención [006]).
///
/// Por qué así y no con <c>new</c>: lo que falla en producción es lo que el arranque configura. La
/// licencia de QuestPDF se declara en <c>Program.cs</c> y sus requisitos nativos —<c>libfontconfig1</c>,
/// <c>libfreetype6</c>— los instala el <c>Dockerfile</c>; instanciar a mano pasaría con la aplicación
/// real fallando. ClosedXML no tiene requisitos nativos, pero lleva el mismo test por la misma razón:
/// compilar, restaurar y arrancar bien no prueban nada sobre ella (research §1).
/// </summary>
public class ArmadoresTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateTime Generado = new(2026, 9, 20, 17, 32, 0, DateTimeKind.Utc);

    /// <summary>
    /// El PDF se genera de verdad. Es el caso que el Módulo 6 ya vive: sin las fuentes nativas, el
    /// backend compila, arranca y falla recién al armar el primer documento.
    /// </summary>
    [Fact]
    public void ElPdfSeGeneraDeVerdad()
    {
        using var alcance = app.Services.CreateScope();
        var armador = alcance.ServiceProvider.GetRequiredService<IArmadorReportePdf>();

        var bytes = armador.Armar(Reporte());

        Assert.NotEmpty(bytes);

        // La firma de un PDF: `%PDF`.
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }

    /// <summary>
    /// **FR-011 y SC-007.** El <c>.xlsx</c> se reabre y se verifica que las celdas de importe sean
    /// número y las de fecha sean fecha: si fueran texto, la planilla no podría sumarlas ni ordenarlas,
    /// que es justo lo que la spec pide poder hacer.
    /// </summary>
    [Fact]
    public void ElExcelGuardaImportesComoNumeroYFechasComoFecha()
    {
        using var alcance = app.Services.CreateScope();
        var armador = alcance.ServiceProvider.GetRequiredService<IArmadorReporteExcel>();

        var bytes = armador.Armar(Reporte());

        using var memoria = new MemoryStream(bytes);
        using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        var (filaDeEncabezados, columnas) = UbicarTabla(hoja);
        var primeraFila = filaDeEncabezados + 1;

        // Columna 1: Número, entero.
        Assert.Equal(XLDataType.Number, hoja.Cell(primeraFila, 1).DataType);
        Assert.Equal(1041, hoja.Cell(primeraFila, 1).GetValue<int>());

        // Columna 2: Fecha. **Fecha de verdad, no texto.**
        Assert.Equal(XLDataType.DateTime, hoja.Cell(primeraFila, 2).DataType);
        Assert.Equal(new DateTime(2026, 9, 14), hoja.Cell(primeraFila, 2).GetValue<DateTime>());

        // Columna 4: Importe. **Número de verdad, no `"$ 1.240.000,00"`.**
        var importe = hoja.Cell(primeraFila, 4);
        Assert.Equal(XLDataType.Number, importe.DataType);
        Assert.Equal(1_240_000m, importe.GetValue<decimal>());
        Assert.Equal("#,##0.00", importe.Style.NumberFormat.Format);

        Assert.Equal(4, columnas);
    }

    /// <summary>El total del pie también va como número, para poder comprobar SC-004 con la planilla.</summary>
    [Fact]
    public void ElExcelGuardaElTotalComoNumero()
    {
        using var alcance = app.Services.CreateScope();
        var armador = alcance.ServiceProvider.GetRequiredService<IArmadorReporteExcel>();

        using var memoria = new MemoryStream(armador.Armar(Reporte()));
        using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        var celda = hoja.CellsUsed(celda => celda.DataType == XLDataType.Number)
            .First(celda => celda.GetValue<decimal>() == 1_240_000m + 260_000m);

        Assert.Equal(XLDataType.Number, celda.DataType);
        Assert.Equal("#,##0.00", celda.Style.NumberFormat.Format);
    }

    /// <summary>Una celda sin dato queda **vacía**: ni guion, ni "Sin asignar" (spec §Edge Cases).</summary>
    [Fact]
    public void ElExcelDejaVaciaLaCeldaSinDato()
    {
        using var alcance = app.Services.CreateScope();
        var armador = alcance.ServiceProvider.GetRequiredService<IArmadorReporteExcel>();

        using var memoria = new MemoryStream(armador.Armar(Reporte()));
        using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        var (filaDeEncabezados, _) = UbicarTabla(hoja);

        // Columna 3 de la segunda fila: el chofer que no está asignado.
        Assert.True(hoja.Cell(filaDeEncabezados + 2, 3).IsEmpty());
    }

    /// <summary>El encabezado de FR-006 está en las primeras filas del <c>.xlsx</c>.</summary>
    [Fact]
    public void ElExcelLlevaElEncabezadoDeFr006()
    {
        using var alcance = app.Services.CreateScope();
        var armador = alcance.ServiceProvider.GetRequiredService<IArmadorReporteExcel>();

        using var memoria = new MemoryStream(armador.Armar(Reporte()));
        using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        Assert.Equal("Reporte de viajes", hoja.Cell(1, 1).GetString());
        Assert.Equal("Cliente: Aceitera del Sur", hoja.Cell(2, 1).GetString());
        Assert.Equal("Filas incluidas: 2", hoja.Cell(3, 1).GetString());
        Assert.Contains("Generado el 20/09/2026 a las 14:32", hoja.Cell(4, 1).GetString());
        Assert.Contains("Generado por jlopez", hoja.Cell(4, 1).GetString());
    }

    /// <summary>Encuentra la fila de encabezados de la tabla, sin fijar en qué renglón cae.</summary>
    private static (int Fila, int Columnas) UbicarTabla(IXLWorksheet hoja)
    {
        for (var fila = 1; fila <= 20; fila++)
        {
            if (hoja.Cell(fila, 1).GetString() == "Número")
            {
                var columnas = 0;

                while (!hoja.Cell(fila, columnas + 1).IsEmpty())
                {
                    columnas++;
                }

                return (fila, columnas);
            }
        }

        throw new InvalidOperationException("No se encontró la fila de encabezados de la tabla.");
    }

    private static ReporteTabular Reporte() => new(
        new EncabezadoDeReporte("Reporte de viajes", "Cliente: Aceitera del Sur", 2, Generado, "jlopez"),
        [
            ColumnaDeReporte.Entero("Número"),
            ColumnaDeReporte.Fecha("Fecha"),
            ColumnaDeReporte.Texto("Chofer"),
            ColumnaDeReporte.Importe("Importe"),
        ],
        [
            [
                CeldaDeReporte.Entero(1041),
                CeldaDeReporte.Fecha(new DateOnly(2026, 9, 14)),
                CeldaDeReporte.Texto("Pérez, Juan"),
                CeldaDeReporte.Importe(1_240_000m),
            ],
            [
                CeldaDeReporte.Entero(1042),
                CeldaDeReporte.Fecha(new DateOnly(2026, 9, 15)),
                // Sin chofer asignado: la celda queda vacía.
                CeldaDeReporte.Texto(null),
                CeldaDeReporte.Importe(260_000m),
            ],
        ],
        [new TotalDeReporte("Total de importes", 1_500_000m)]);
}
