namespace GT.Domain.Choferes;

/// <summary>
/// Personería de un transportista (FR-002). Aplica <b>sólo</b> al transportista: el chofer es
/// siempre una persona física y no lleva este dato.
///
/// No confundir con <see cref="GT.Domain.Personas.TipoIntegrante"/>, que dice si una persona del
/// padrón es chofer o empleado. Son dos ejes distintos y por eso son dos enums distintos.
/// </summary>
public enum TipoPersona : byte
{
    Fisica = 1,
    Juridica = 2,
}
