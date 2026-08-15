namespace GT.Application.Facturacion;

/// <summary>
/// Los viajes que se le pueden facturar a un cliente en un período (FR-015 a FR-019a, FR-021).
///
/// Las tres condiciones —del cliente, <c>rendido</c>, con fecha en el mes y año— las resuelve la
/// consulta. Acá se decide una sola cosa: <b>qué se hace con los que no tienen remito</b>.
///
/// <b>Se devuelven igual, marcados.</b> Esconderlos dejaría a quien opera buscando un viaje que sabe
/// que existe y que la pantalla no muestra, sin ninguna pista de por qué. Un listado no oculta filas en
/// silencio y tampoco las ofrece sin decir lo que sabe de ellas (FR-019a, convención [003]).
///
/// Una lista vacía es una respuesta legítima, y la pantalla la explica nombrando la combinación de
/// cliente, mes y año en vez de mostrar una tabla sin filas (FR-021).
/// </summary>
public class ConsultarFacturables(IRepositorioFacturas facturas)
{
    /// <summary>El único motivo por el que hoy un viaje llega marcado como no facturable.</summary>
    public const string MotivoSinRemito = "sinRemito";

    public async Task<IReadOnlyList<ViajeFacturable>> EjecutarAsync(
        int clienteId,
        int mes,
        int anio,
        CancellationToken cancelacion = default)
    {
        var viajes = await facturas.ConsultarFacturablesAsync(clienteId, mes, anio, cancelacion);

        return [.. viajes.Select(viaje =>
        {
            var tieneRemito = !string.IsNullOrWhiteSpace(viaje.NumeroRemito);

            return new ViajeFacturable(
                viaje.Id,
                viaje.Numero,
                viaje.Fecha.ToString("yyyy-MM-dd"),
                viaje.NumeroRemito,
                viaje.Origen,
                viaje.Destino,
                viaje.Importe,
                tieneRemito,
                tieneRemito ? null : MotivoSinRemito);
        })];
    }
}
