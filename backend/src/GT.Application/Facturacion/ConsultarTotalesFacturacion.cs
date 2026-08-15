namespace GT.Application.Facturacion;

/// <summary>
/// Facturado, cobrado y pendiente por cliente entre dos fechas (FR-061, FR-062).
///
/// <b>El rango de fechas es obligatorio</b> y sin él no se calcula nada: se responde
/// <c>rango_de_fechas_requerido</c> para que la pantalla diga que falta elegirlo, en vez de mostrar un
/// cuadro vacío que se lee como "no hay facturas" (FR-061).
///
/// <b>La fecha de corte es la fecha de facturación</b>, no la de cobro: es la misma con la que el listado
/// ordena y filtra, y eso es lo que hace que la suma de los importes del listado filtrado coincida con la
/// columna *facturado* (SC-011).
///
/// <b>Las anuladas no suman en ninguna columna</b>, y la exclusión vive en la consulta, escrita una sola
/// vez sobre el conjunto del que salen las tres columnas (FR-062).
/// </summary>
public class ConsultarTotalesFacturacion(IRepositorioFacturas facturas)
{
    public async Task<(ResultadoFactura? Rechazo, IReadOnlyList<TotalPorCliente>? Totales)>
        EjecutarAsync(DateOnly? desde, DateOnly? hasta, CancellationToken cancelacion = default)
    {
        if (desde is null || hasta is null)
        {
            return (new ResultadoFactura(
                ErrorFactura.RangoDeFechasRequerido,
                Mensaje: MensajesFacturas.RangoDeFechasRequerido), null);
        }

        // Un rango invertido no es un error de tipeo que valga la pena distinguir: devuelve la lista
        // vacía y la pantalla dice que no hay facturas entre esas fechas, que es lo que pasó.
        return (null, await facturas.ConsultarTotalesAsync(desde.Value, hasta.Value, cancelacion));
    }
}
