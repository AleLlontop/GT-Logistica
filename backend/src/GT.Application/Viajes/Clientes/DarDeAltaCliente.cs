namespace GT.Application.Viajes.Clientes;

/// <summary>
/// Alta de nuevo de un cliente dado de baja (FR-007).
///
/// <b>Recurso propio y no un campo del <c>PUT</c></b>, por el precedente [004]: así corregir una
/// razón social no puede reactivar en silencio a alguien que estaba dado de baja.
///
/// <b>No pide confirmación aparte</b> —no destruye nada y se deshace con la baja, que sí la pide— y
/// <b>es idempotente</b>: darle de alta a un cliente ya activo no cambia nada y responde igual.
///
/// Sus viajes históricos quedan intactos: la baja nunca los tocó (US1 esc. 9).
/// </summary>
public class DarDeAltaCliente(IRepositorioClientes clientes)
{
    public async Task<ResultadoCliente> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var cliente = await clientes.ObtenerParaModificarAsync(id, cancelacion);

        if (cliente is null)
        {
            return new ResultadoCliente(ErrorCliente.NoEncontrado);
        }

        if (!cliente.Activo)
        {
            cliente.Activo = true;
            await clientes.GuardarCambiosAsync(cancelacion);
        }

        return new ResultadoCliente(ErrorCliente.Ninguno, ClienteDto.Desde(cliente));
    }
}
