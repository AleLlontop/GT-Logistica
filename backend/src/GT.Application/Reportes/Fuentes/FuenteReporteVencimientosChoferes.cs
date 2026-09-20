using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;

namespace GT.Application.Reportes.Fuentes;

/// <summary>
/// El reporte del panel de vencimientos de choferes (US2, data-model §3.2).
///
/// <b>Llama a <see cref="ConsultarVencimientos"/> tal cual está</b>: el panel ya devuelve la lista
/// entera y en su orden por urgencia, así que <b>Choferes no cambia ni una línea</b> (research §3).
///
/// <b>No lleva el escaneo del documento ni su enlace</b>, y es deliberado: el permiso separado de los
/// Módulos 3 y 4 existe para que Gerencia vea qué vence <i>sin</i> llegar al dato personal sensible, y
/// el reporte no puede ser una puerta lateral a eso (plan §Constitution Check, Principio V).
///
/// El panel no tiene filtros y esta feature no se los agrega: su encabezado dice siempre
/// <c>Sin filtros aplicados</c>.
/// </summary>
public class FuenteReporteVencimientosChoferes(
    ConsultarVencimientos consulta,
    ArmadoDeReporte armado)
{
    public const string Titulo = "Reporte de vencimientos de choferes";

    private static readonly IReadOnlyList<ColumnaDeReporte> Columnas =
    [
        ColumnaDeReporte.Texto("Chofer"),
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
            TipoDeReporte.VencimientosChoferes,
            formato,
            generadoPor,
            Titulo,
            LineaDeFiltros.SinFiltros,
            Columnas,
            [.. alertas.Select(Fila)]);
    }

    private static IReadOnlyList<CeldaDeReporte> Fila(AlertaVencimiento alerta) =>
    [
        CeldaDeReporte.Texto($"{alerta.Apellido}, {alerta.Nombre}"),
        CeldaDeReporte.Texto(alerta.Transportista.Nombre),
        CeldaDeReporte.Texto(alerta.Documento.Tipo.Nombre),
        CeldaDeReporte.Fecha(DateOnly.Parse(alerta.Documento.FechaVencimiento)),
        // **La palabra del panel, no el código del JSON** (FR-010, data-model §2.5). Es el estado
        // **del documento**, no el del chofer: es el que el panel muestra.
        CeldaDeReporte.Texto(NombresDeEstado.EnPantalla(
            NombresDeEstado.LeerDocumento(alerta.Documento.Estado))),
    ];
}
