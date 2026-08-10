namespace GT.Domain.Choferes;

/// <summary>
/// A qué se aplica un tipo de documentación, y por lo tanto en qué módulo se ofrece (FR-017).
///
/// El catálogo <see cref="DocumentacionTipo"/> es uno solo para todo el sistema —duplicarlo por
/// módulo duplicaría el ABM, la regla de días de aviso y la pantalla—, así que cada tipo declara su
/// ámbito y cada módulo ofrece únicamente los suyos (research §3).
///
/// <b>Vive acá, junto a <see cref="DocumentacionTipo"/>, aunque ya no describa algo sólo de
/// choferes.</b> Mover el catálogo a una carpeta propia sería más prolijo y toca una decena de
/// archivos del Módulo 3 sin cambiar una sola conducta; la spec del Módulo 4 acotó los cambios a ese
/// módulo, así que la relocación queda anotada para una spec futura (plan §Structure Decision).
/// </summary>
public enum DocumentacionAmbito : byte
{
    Chofer = 1,

    Vehiculo = 2,
}
