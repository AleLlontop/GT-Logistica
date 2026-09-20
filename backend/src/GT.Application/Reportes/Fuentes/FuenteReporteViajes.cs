using GT.Application.Choferes.Transportistas;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;

namespace GT.Application.Reportes.Fuentes;

/// <summary>
/// Los filtros del listado de viajes, tal como la pantalla los aplica. <b>Los mismos nombres</b> que
/// el listado ya recibe: el reporte no inventa un enlace de parámetros propio.
/// </summary>
/// <param name="Estado">El código del JSON —<c>enCurso</c>—, igual que en el listado.</param>
public record FiltrosDelReporteDeViajes(
    int? ClienteId = null,
    int? TransportistaId = null,
    string? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    string? Busqueda = null);

/// <summary>
/// El reporte de viajes con los filtros aplicados (US1, data-model §3.1).
///
/// <b>Llama a la misma consulta del listado</b>, con <c>Pagina: 1</c> y el tamaño de página puesto en
/// el tope: así el orden —fecha descendente, luego número— no se replica, <b>se hereda</b>, y el
/// <c>Total</c> que esa consulta ya calcula con un <c>CountAsync</c> sobre el filtro completo es el
/// número de FR-016, sin una consulta nueva (research §3).
///
/// Diez columnas y sólo diez (FR-008). <i>Ruta</i> y <i>Asignación</i> se abren en dos columnas cada
/// una: en un archivo que se va a ordenar y filtrar, cada dato va en su columna. Las señales que la
/// pantalla agrega adentro de una celda —<i>Demorado</i>, <i>Carga retroactiva</i>,
/// <c>(inactivo)</c>— <b>no son columnas</b> y no van.
/// </summary>
public class FuenteReporteViajes(
    ConsultarViajes consulta,
    IRepositorioClientes clientes,
    IRepositorioTransportistas transportistas,
    ArmadoDeReporte armado)
{
    public const string Titulo = "Reporte de viajes";

    private static readonly IReadOnlyList<ColumnaDeReporte> Columnas =
    [
        ColumnaDeReporte.Entero("Número"),
        ColumnaDeReporte.Fecha("Fecha"),
        ColumnaDeReporte.Texto("Cliente"),
        ColumnaDeReporte.Texto("Origen"),
        ColumnaDeReporte.Texto("Destino"),
        ColumnaDeReporte.Texto("Chofer"),
        ColumnaDeReporte.Texto("Vehículo"),
        ColumnaDeReporte.Texto("Transportista"),
        ColumnaDeReporte.Texto("Estado"),
        ColumnaDeReporte.Importe("Importe"),
    ];

    public async Task<ResultadoReporte> EjecutarAsync(
        FiltrosDelReporteDeViajes filtros,
        FormatoDeReporte formato,
        string generadoPor,
        CancellationToken cancelacion = default)
    {
        var estado = NombresDeEstadoViaje.Leer(filtros.Estado);

        var pagina = await consulta.EjecutarAsync(
            new FiltrosDeViajes(
                filtros.ClienteId,
                filtros.TransportistaId,
                estado,
                filtros.Desde,
                filtros.Hasta,
                filtros.Busqueda,
                Pagina: 1),
            armado.TamanioParaTraerTodo,
            cancelacion);

        if (armado.SuperaElTope(pagina.Total))
        {
            return armado.TopeSuperado(pagina.Total);
        }

        var filas = pagina.Items.Select(Fila).ToList();

        return armado.Entregar(
            TipoDeReporte.Viajes,
            formato,
            generadoPor,
            Titulo,
            await LineaDeFiltrosAsync(filtros, estado, cancelacion),
            Columnas,
            filas,
            // FR-009: el total se calcula **sobre las filas del propio reporte**, que es lo que hace
            // que SC-004 se compruebe sumando las filas del archivo.
            [new TotalDeReporte("Total de importes", pagina.Items.Sum(viaje => viaje.Importe))]);
    }

    private static IReadOnlyList<CeldaDeReporte> Fila(ViajeListado viaje) =>
    [
        CeldaDeReporte.Entero(viaje.Numero),
        CeldaDeReporte.Fecha(DateOnly.Parse(viaje.Fecha)),
        CeldaDeReporte.Texto(viaje.Cliente.Nombre),
        CeldaDeReporte.Texto(viaje.Origen),
        CeldaDeReporte.Texto(viaje.Destino),
        // Sin asignar, la celda queda **vacía**: ni guion, ni "Sin asignar" (spec §Edge Cases).
        CeldaDeReporte.Texto(viaje.Chofer?.Nombre),
        CeldaDeReporte.Texto(viaje.Vehiculo?.Nombre),
        CeldaDeReporte.Texto(viaje.Transportista?.Nombre),
        // **La palabra de la pantalla, no el código del JSON** (FR-010, data-model §2.5).
        CeldaDeReporte.Texto(NombresDeEstadoViaje.EnPantalla(
            NombresDeEstadoViaje.Leer(viaje.Estado)
                ?? throw new InvalidOperationException(
                    $"El viaje {viaje.Numero} llegó con un estado desconocido: {viaje.Estado}."))),
        CeldaDeReporte.Importe(viaje.Importe),
    ];

    /// <summary>
    /// Los filtros en palabras. <b>Cliente y transportista se resuelven a su nombre acá</b>, en el
    /// backend: la pantalla tiene identificadores, no nombres, y el frontend no manda ningún texto
    /// (research §9).
    /// </summary>
    private async Task<string> LineaDeFiltrosAsync(
        FiltrosDelReporteDeViajes filtros,
        Domain.Viajes.EstadoViaje? estado,
        CancellationToken cancelacion)
    {
        var cliente = filtros.ClienteId is { } clienteId
            ? (await clientes.ObtenerPorIdAsync(clienteId, cancelacion))?.RazonSocial
            : null;

        var transportista = filtros.TransportistaId is { } transportistaId
            ? (await transportistas.ObtenerPorIdAsync(transportistaId, cancelacion))?.Nombre
            : null;

        return new LineaDeFiltros()
            .Con("Cliente", cliente)
            .Con("Transportista", transportista)
            .Con("Estado", estado is { } valor ? NombresDeEstadoViaje.EnPantalla(valor) : null)
            .Con("Desde", filtros.Desde)
            .Con("Hasta", filtros.Hasta)
            .Con("Búsqueda", filtros.Busqueda)
            .ToString();
    }
}
