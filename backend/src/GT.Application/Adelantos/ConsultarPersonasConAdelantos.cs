namespace GT.Application.Adelantos;

/// <summary>
/// Las personas del filtro del listado: sólo las que tienen algún adelanto, en cualquier estado, activas o
/// dadas de baja (FR-014). Elegir a alguien sin adelantos siempre daría un listado vacío.
/// </summary>
public class ConsultarPersonasConAdelantos(IRepositorioAdelantos adelantos)
{
    public Task<IReadOnlyList<PersonaResumen>> EjecutarAsync(CancellationToken cancelacion = default) =>
        adelantos.ConsultarPersonasConAdelantosAsync(cancelacion);
}
