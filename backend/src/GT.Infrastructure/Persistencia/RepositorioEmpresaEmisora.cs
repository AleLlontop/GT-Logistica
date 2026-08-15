using GT.Application.Facturacion;
using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Acceso a la única fila de configuración del emisor.
///
/// <b>No filtra por <c>Id == 1</c> en las consultas</b>, y no es descuido: la base garantiza con un
/// <c>CHECK</c> que no puede haber otra, así que <c>FirstOrDefault</c> sin condición devuelve
/// exactamente la fila o nada. Filtrar por el <c>1</c> repetiría acá el invariante que ya vive en la
/// base y dejaría dos lugares que pueden discrepar (research §12).
/// </summary>
public class RepositorioEmpresaEmisora(GtDbContext contexto) : IRepositorioEmpresaEmisora
{
    public Task<EmpresaEmisora?> ObtenerAsync(CancellationToken cancelacion = default) =>
        contexto.EmpresaEmisora.AsNoTracking().FirstOrDefaultAsync(cancelacion);

    public Task<EmpresaEmisora?> ObtenerParaModificarAsync(CancellationToken cancelacion = default) =>
        contexto.EmpresaEmisora.FirstOrDefaultAsync(cancelacion);

    public Task AgregarAsync(EmpresaEmisora empresa, CancellationToken cancelacion = default)
    {
        contexto.EmpresaEmisora.Add(empresa);

        return Task.CompletedTask;
    }

    public Task GuardarAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);
}
