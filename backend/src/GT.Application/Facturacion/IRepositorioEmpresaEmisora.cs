// La entidad se referencia con alias porque el módulo tiene una **subcarpeta** `EmpresaEmisora/` —así
// lo fija el plan §Project Structure—, y ese espacio de nombres oculta al tipo del mismo nombre.
using Entidad = GT.Domain.Facturacion.EmpresaEmisora;

namespace GT.Application.Facturacion;

/// <summary>
/// Acceso a la única fila de configuración del emisor (FR-001, research §12).
///
/// <b>No tiene <c>Agregar</c> público ni <c>Borrar</c>, y no es una omisión</b>: la configuración se
/// edita, nunca se crea una segunda ni se borra. <see cref="GuardarAsync"/> confirma lo que el caso de
/// uso preparó, que es lo único que el negocio hace con ella.
/// </summary>
public interface IRepositorioEmpresaEmisora
{
    /// <summary>
    /// La configuración, o <c>null</c> si todavía no se guardó nada. <b>El <c>null</c> es un estado
    /// legítimo</b> y no un error: la ausencia de la fila <i>es</i> el estado "sin configurar"
    /// (US1 esc. 1).
    /// </summary>
    Task<Entidad?> ObtenerAsync(CancellationToken cancelacion = default);

    /// <summary>Igual que <see cref="ObtenerAsync"/> pero seguida por el contexto, para modificarla.</summary>
    Task<Entidad?> ObtenerParaModificarAsync(CancellationToken cancelacion = default);

    /// <summary>Agrega la fila al contexto. Sólo se llama cuando <see cref="ObtenerAsync"/> dio <c>null</c>.</summary>
    Task AgregarAsync(Entidad empresa, CancellationToken cancelacion = default);

    Task GuardarAsync(CancellationToken cancelacion = default);
}
