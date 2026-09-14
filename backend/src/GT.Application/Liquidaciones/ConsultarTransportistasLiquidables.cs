namespace GT.Application.Liquidaciones;

/// <summary>
/// Los transportistas externos: los que ofrece la generación y los del filtro del listado (FR-001,
/// FR-022). Sin empresa emisora configurada no hay externo posible, y la respuesta lo declara.
/// </summary>
public class ConsultarTransportistasLiquidables(IRepositorioLiquidaciones liquidaciones)
{
    public async Task<TransportistasLiquidables> EjecutarAsync(
        bool incluirInactivos,
        CancellationToken cancelacion = default)
    {
        var cuitEmisora = await liquidaciones.ObtenerCuitEmpresaEmisoraAsync(cancelacion);

        if (cuitEmisora is null)
        {
            return new TransportistasLiquidables(false, []);
        }

        var transportistas = await liquidaciones.ConsultarTransportistasLiquidablesAsync(
            cuitEmisora,
            incluirInactivos,
            cancelacion);

        return new TransportistasLiquidables(
            true,
            [.. transportistas.Select(transportista => new TransportistaResumen(
                transportista.Id,
                transportista.Nombre,
                transportista.Cuit,
                transportista.Activo))]);
    }
}
