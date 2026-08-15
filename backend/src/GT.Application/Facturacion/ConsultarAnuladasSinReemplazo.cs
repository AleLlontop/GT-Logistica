namespace GT.Application.Facturacion;

/// <summary>
/// Las facturas anuladas de un cliente que todavía nadie refacturó (FR-049, FR-049a).
///
/// Alimenta el desplegable <i>Factura que reemplaza</i> del alta, que sólo aparece con
/// <c>Refacturación</c>.
///
/// <b>Ofrece sólo las que nadie reemplazó todavía</b>, y eso es la consulta previa que da el mensaje
/// bueno; el índice único filtrado sobre <c>FacturaReemplazadaId</c> es lo que cierra la carrera entre
/// dos operadores simultáneos (FR-049a, research §4).
/// </summary>
public class ConsultarAnuladasSinReemplazo(IRepositorioFacturas facturas)
{
    public async Task<IReadOnlyList<FacturaResumen>> EjecutarAsync(
        int clienteId,
        CancellationToken cancelacion = default)
    {
        var anuladas = await facturas.ConsultarAnuladasSinReemplazoAsync(clienteId, cancelacion);

        return [.. anuladas.Select(PreparadorDeFactura.ResumenDe)];
    }
}
