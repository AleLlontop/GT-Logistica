namespace GT.Domain.Choferes;

/// <summary>
/// Mayoría de edad de un chofer (FR-011): se rechaza el registro de un menor de 18 años a la fecha
/// del alta.
///
/// La edad se calcula por fecha cumplida, no restando años: alguien que cumple 18 <b>hoy</b> es
/// mayor de edad, y alguien que los cumple mañana todavía no.
/// </summary>
public static class MayoriaDeEdad
{
    public const int EdadMinima = 18;

    /// <param name="fechaNacimiento">Fecha de nacimiento del chofer.</param>
    /// <param name="hoy">Día en curso en Argentina (<see cref="FechaHoyArgentina"/>).</param>
    public static bool EsMayor(DateOnly fechaNacimiento, DateOnly hoy) =>
        fechaNacimiento <= hoy.AddYears(-EdadMinima);
}
