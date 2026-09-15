using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Las personas que se pueden elegir para un tipo (FR-002, FR-002a, FR-004).
///
/// <b>Filtra en memoria a propósito</b> con la misma <see cref="ElegibilidadDeBeneficiario"/> que valida el
/// registro (research §1): la convención [003] protege a una consulta que tiene que ir a SQL, y ésta es un
/// desplegable de decenas de filas sin paginar. Llevar el predicado a SQL separaría lo que se ofrece de lo
/// que se acepta.
/// </summary>
public class ConsultarBeneficiarios(IRepositorioAdelantos adelantos)
{
    public async Task<Beneficiarios> EjecutarAsync(TipoBeneficiario tipo, CancellationToken cancelacion = default)
    {
        var cuitEmisora = await adelantos.ObtenerCuitEmpresaEmisoraAsync(cancelacion);
        var personas = await adelantos.ConsultarPersonasActivasParaAdelantoAsync(cancelacion);

        return new Beneficiarios(
            cuitEmisora is not null,
            [.. personas
                .Where(persona => ElegibilidadDeBeneficiario.Evaluar(tipo, persona, cuitEmisora) is null)
                .Select(persona => new PersonaResumen(persona.Id, persona.Apellido, persona.Nombre, persona.Dni))]);
    }
}
