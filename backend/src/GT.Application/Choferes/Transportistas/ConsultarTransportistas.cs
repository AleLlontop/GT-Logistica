using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public class ConsultarTransportistas(IRepositorioTransportistas repositorio)
{
    public async Task<List<TransportistaDto>> EjecutarAsync(
        string? texto = null,
        bool soloActivos = false,
        CancellationToken cancelacion = default)
    {
        var textoNormalizado = texto?.Trim();
        if (string.IsNullOrEmpty(textoNormalizado)) textoNormalizado = null;

        // El CUIT se guarda sin guiones ni puntos, así que buscar "30-71" contra la columna no
        // encontraría nada aunque la pantalla lo muestre con guiones. Se compara también la versión
        // normalizada del texto tipeado (FR-025).
        var cuitNormalizado = textoNormalizado is null
            ? null
            : NormalizadorDocumentoNumerico.Normalizar(textoNormalizado);

        if (string.IsNullOrEmpty(cuitNormalizado)) cuitNormalizado = null;

        var transportistas = await repositorio.ConsultarAsync(
            textoNormalizado,
            cuitNormalizado,
            soloActivos,
            cancelacion);

        return transportistas.Select(TransportistaDto.Desde).ToList();
    }
}

public class ConsultarTransportistaPorId(IRepositorioTransportistas repositorio)
{
    public async Task<ResultadoTransportista> EjecutarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var fila = await repositorio.ObtenerConDependenciasActivasAsync(id, cancelacion);

        if (fila is null)
        {
            return new ResultadoTransportista(ErrorTransportista.NoEncontrado, null);
        }

        return new ResultadoTransportista(ErrorTransportista.Ninguno, TransportistaDto.Desde(fila));
    }
}
