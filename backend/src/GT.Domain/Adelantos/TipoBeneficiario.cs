namespace GT.Domain.Adelantos;

/// <summary>
/// Para quién es el adelanto: el primer desplegable del registro (FR-001). Queda guardado tal como se
/// eligió y no se recalcula (FR-010, FR-020).
///
/// <b>No es <c>TipoIntegrante</c></b>, aunque tenga los mismos dos valores: aquél es un dato informativo
/// que el operador carga en el padrón, y éste lo decide la ficha de chofer (research §1). Con un solo
/// enum, validar "es chofer" con <c>Persona.Tipo == Chofer</c> compilaría y estaría mal. Los números son
/// los mismos para que nadie tenga que traducir al leer la base.
///
/// <b>⚠</b> <c>CK_Adelantos_TipoBeneficiario</c> lleva el <c>1</c> y el <c>2</c> escritos a mano.
/// </summary>
public enum TipoBeneficiario : byte
{
    Chofer = 1,
    Empleado = 2,
}
