namespace GT.Application.Viajes.Clientes;

/// <summary>
/// Baja lógica de un cliente (FR-001, FR-005, FR-006).
///
/// <b>Se rechaza sólo si tiene viajes <c>pendiente</c> o <c>en curso</c></b>, y el mensaje dice
/// cuántos son, en el texto y en el cuerpo del error (SC-009, precedente [004]).
///
/// Los rendidos y los anulados no cuentan, y ése fue el hallazgo de la revisión de calidad de la
/// spec: la versión anterior de FR-006 rechazaba la baja por cualquier viaje no anulado, con lo que
/// el único cliente dado de baja posible era el que nunca había operado, mientras US1 justifica la
/// baja con "el que dejó de operar con la empresa" —que por definición tiene historial—. Ahora es el
/// mismo criterio de "dependientes vivos" con que el Módulo 3 rechaza la baja de un transportista.
///
/// La confirmación previa la pide la pantalla, no el endpoint (FR-005): la baja se deshace con el
/// alta, así que no hace falta que el servidor la exija.
/// </summary>
public class DarDeBajaCliente(IRepositorioClientes clientes)
{
    public async Task<ResultadoCliente> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var cliente = await clientes.ObtenerParaModificarAsync(id, cancelacion);

        if (cliente is null)
        {
            return new ResultadoCliente(ErrorCliente.NoEncontrado);
        }

        var viajesVivos = await clientes.ContarViajesVivosAsync(id, cancelacion);

        if (viajesVivos > 0)
        {
            return new ResultadoCliente(ErrorCliente.ConViajes, CantidadViajes: viajesVivos);
        }

        // Dar de baja a un cliente que ya está de baja no es un error: el resultado buscado ya se
        // cumple, y fallar sólo complicaría a quien tocó dos veces el botón.
        cliente.Activo = false;
        await clientes.GuardarCambiosAsync(cancelacion);

        return new ResultadoCliente(ErrorCliente.Ninguno, ClienteDto.Desde(cliente));
    }
}
