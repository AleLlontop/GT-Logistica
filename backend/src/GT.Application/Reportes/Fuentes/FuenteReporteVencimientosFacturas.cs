using GT.Application.Facturacion;

namespace GT.Application.Reportes.Fuentes;

/// <summary>
/// El reporte del panel de vencimientos de facturas (US2, data-model §3.4).
///
/// <b>Llama a <see cref="ConsultarVencimientos"/> de Facturación tal cual está</b>: el panel ya
/// devuelve la lista entera, así que <b>Facturación no cambia ni una línea</b>.
///
/// El número viaja <b>ya armado por el backend</b> —<c>NumeroComprobante</c>— y no se vuelve a
/// componer acá (convención [009]).
///
/// <i>Situación</i> es la única columna calculada, y sale de <see cref="SituacionDeVencimiento"/>, que
/// es la versión en C# de la <c>situacion(dias)</c> de la pantalla: no se puede llamar a la original
/// porque está en TypeScript (research §14).
/// </summary>
public class FuenteReporteVencimientosFacturas(
    ConsultarVencimientos consulta,
    ArmadoDeReporte armado)
{
    public const string Titulo = "Reporte de vencimientos de facturas";

    private static readonly IReadOnlyList<ColumnaDeReporte> Columnas =
    [
        ColumnaDeReporte.Texto("Cliente"),
        ColumnaDeReporte.Texto("Número"),
        ColumnaDeReporte.Importe("Importe"),
        ColumnaDeReporte.Fecha("Vencimiento"),
        ColumnaDeReporte.Texto("Situación"),
    ];

    public async Task<ResultadoReporte> EjecutarAsync(
        FormatoDeReporte formato,
        string generadoPor,
        CancellationToken cancelacion = default)
    {
        var filas = await consulta.EjecutarAsync(cancelacion);

        if (armado.SuperaElTope(filas.Count))
        {
            return armado.TopeSuperado(filas.Count);
        }

        return armado.Entregar(
            TipoDeReporte.VencimientosFacturas,
            formato,
            generadoPor,
            Titulo,
            LineaDeFiltros.SinFiltros,
            Columnas,
            [.. filas.Select(Fila)],
            // FR-009, sobre las filas del propio reporte.
            [new TotalDeReporte("Total de importes", filas.Sum(fila => fila.Total))]);
    }

    private static IReadOnlyList<CeldaDeReporte> Fila(FilaDeVencimiento fila) =>
    [
        CeldaDeReporte.Texto(fila.Cliente),
        CeldaDeReporte.Texto(fila.NumeroComprobante),
        CeldaDeReporte.Importe(fila.Total),
        CeldaDeReporte.Fecha(DateOnly.Parse(fila.VencimientoPago)),
        CeldaDeReporte.Texto(SituacionDeVencimiento.Para(fila.Dias)),
    ];
}
