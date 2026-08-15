namespace GT.Application.Facturacion.EmpresaEmisora;

/// <summary>
/// Los datos del emisor, configurados o no (FR-001, FR-006).
///
/// <b>Responde <c>200</c> también cuando la empresa nunca se configuró</b>, con
/// <c>configurada: false</c> y la lista de los cuatro obligatorios faltantes. Un <c>404</c> obligaría a
/// la pantalla a tratar la ausencia como un error, y la ausencia no es un error: es el punto de partida
/// del sistema recién instalado, y lo que la pantalla tiene que mostrar es el formulario vacío con el
/// mensaje que explica qué falta (US1 esc. 1, research §12).
/// </summary>
public class ConsultarEmpresaEmisora(IRepositorioEmpresaEmisora empresas)
{
    public async Task<EmpresaEmisoraDto> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var empresa = await empresas.ObtenerAsync(cancelacion);

        return empresa is null
            ? EmpresaEmisoraDto.SinConfigurar()
            : EmpresaEmisoraDto.Desde(empresa);
    }

    /// <summary>
    /// Los obligatorios que faltan para poder emitir, o una lista vacía si está todo.
    ///
    /// Lo consultan la emisión y la vista previa: el rechazo de FR-006 nombra los campos, y para eso
    /// alcanza esta lista sin traer el resto de la configuración.
    /// </summary>
    public async Task<IReadOnlyList<string>> FaltantesAsync(CancellationToken cancelacion = default)
    {
        var empresa = await empresas.ObtenerAsync(cancelacion);

        return empresa is null
            ? Domain.Facturacion.EmpresaEmisora.TodosLosObligatorios()
            : empresa.ObligatoriosFaltantes();
    }
}
