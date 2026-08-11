using GT.Application.Choferes;

namespace GT.Application.Viajes.Clientes;

/// <summary>
/// Listado paginado del padrón (FR-009).
///
/// Un padrón vacío es una respuesta legítima —es el estado de toda instalación nueva— y la pantalla
/// lo dice con un mensaje explícito en vez de mostrar una tabla sin filas (US1 esc. 1).
/// </summary>
public class ConsultarClientes(IRepositorioClientes clientes)
{
    public async Task<PaginaDe<ClienteDto>> EjecutarAsync(
        FiltrosDeClientes filtros,
        CancellationToken cancelacion = default)
    {
        var pagina = await clientes.ConsultarAsync(filtros, cancelacion);

        return new PaginaDe<ClienteDto>(
            [.. pagina.Items.Select(ClienteDto.Desde)],
            pagina.Total,
            pagina.Pagina,
            pagina.TamanioPagina);
    }
}
