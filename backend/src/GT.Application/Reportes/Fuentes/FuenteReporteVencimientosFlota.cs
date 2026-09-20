using GT.Application.Choferes;
using GT.Application.Flota;
using GT.Application.Flota.Documentacion;

namespace GT.Application.Reportes.Fuentes;

/// <summary>
/// El reporte del panel de vencimientos de la flota (US2, data-model §3.3).
///
/// <b>Llama a <see cref="ConsultarVencimientosFlota"/> tal cual está</b>: el panel ya devuelve la
/// lista entera y en su orden por urgencia, así que <b>Flota no cambia ni una línea</b>.
///
/// Mismo criterio que el de choferes sobre el escaneo: **no va**, porque el permiso separado existe
/// justamente para que Gerencia vea qué vence sin llegar al adjunto.
/// </summary>
public class FuenteReporteVencimientosFlota(
    ConsultarVencimientosFlota consulta,
    ArmadoDeReporte armado)
{
    public const string Titulo = "Reporte de vencimientos de flota";

    private static readonly IReadOnlyList<ColumnaDeReporte> Columnas =
    [
        ColumnaDeReporte.Texto("Patente"),
        ColumnaDeReporte.Texto("Transportista"),
        ColumnaDeReporte.Texto("Documento"),
        ColumnaDeReporte.Fecha("Fecha de vencimiento"),
        ColumnaDeReporte.Texto("Estado"),
    ];

    public async Task<ResultadoReporte> EjecutarAsync(
        FormatoDeReporte formato,
        string generadoPor,
        CancellationToken cancelacion = default)
    {
        var alertas = await consulta.EjecutarAsync(cancelacion);

        if (armado.SuperaElTope(alertas.Count))
        {
            return armado.TopeSuperado(alertas.Count);
        }

        return armado.Entregar(
            TipoDeReporte.VencimientosFlota,
            formato,
            generadoPor,
            Titulo,
            LineaDeFiltros.SinFiltros,
            Columnas,
            [.. alertas.Select(Fila)]);
    }

    private static IReadOnlyList<CeldaDeReporte> Fila(AlertaVencimientoFlota alerta) =>
    [
        CeldaDeReporte.Texto(alerta.Patente),
        CeldaDeReporte.Texto(alerta.Transportista.Nombre),
        CeldaDeReporte.Texto(alerta.Documento.Tipo.Nombre),
        CeldaDeReporte.Fecha(DateOnly.Parse(alerta.Documento.FechaVencimiento)),
        // La palabra del panel **de Flota**, que dice `Vigente` donde el de Choferes dice `Al día`
        // (FR-010, data-model §2.5).
        CeldaDeReporte.Texto(NombresDeEstadoFlota.EnPantalla(
            NombresDeEstado.LeerDocumento(alerta.Documento.Estado))),
    ];
}
