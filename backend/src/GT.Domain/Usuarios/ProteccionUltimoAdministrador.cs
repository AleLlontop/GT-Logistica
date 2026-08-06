namespace GT.Domain.Usuarios;

/// <summary>Operaciones que pueden dejar al sistema sin administradores activos (FR-019).</summary>
public enum OperacionSobreAdministrador
{
    /// <summary>Sacar la cuenta de <c>activo</c>, sea a <c>inactivo</c> o a <c>bloqueado</c>.</summary>
    CambiarEstado,

    /// <summary>Quitarle el rol <i>Administrador del sistema</i>.</summary>
    QuitarRolAdministrador,

    /// <summary>Baja lógica de la cuenta.</summary>
    DarDeBaja,
}

/// <summary>
/// Siempre tiene que quedar al menos un usuario activo con el rol <i>Administrador del sistema</i>
/// (FR-019).
///
/// Es una función pura para poder verificar las tres operaciones con tests unitarios rápidos en vez
/// de tres tests de integración lentos (research §8). Quien la llama es responsable de contar
/// <b>excluyendo al usuario afectado</b>: ese detalle es el que hace que la regla funcione cuando el
/// afectado <i>es</i> el único administrador, que es justamente el caso que hay que frenar —incluida
/// la variante de que sea la propia cuenta de quien opera.
/// </summary>
public static class ProteccionUltimoAdministrador
{
    /// <param name="administradoresActivosRestantes">
    /// Cuántos usuarios <c>activos</c> con el rol <i>Administrador del sistema</i> quedarían si la
    /// operación se ejecutara. No incluye al usuario afectado.
    /// </param>
    /// <param name="operacion">
    /// Sólo se usa para documentar la intención en quien llama; las tres se juzgan igual, porque las
    /// tres tienen exactamente la misma consecuencia: un administrador activo menos.
    /// </param>
    /// <returns><c>true</c> si la operación puede ejecutarse.</returns>
    public static bool SePuedeEjecutar(
        int administradoresActivosRestantes,
        OperacionSobreAdministrador operacion) =>
        administradoresActivosRestantes > 0;

    /// <summary>
    /// Variante para cuando el usuario afectado no es administrador activo, o la operación no lo
    /// deja de serlo: en ese caso la regla no aplica y no hace falta contar nada.
    /// </summary>
    public static bool SePuedeEjecutar(
        bool elAfectadoEsAdministradorActivo,
        int administradoresActivosRestantes,
        OperacionSobreAdministrador operacion) =>
        !elAfectadoEsAdministradorActivo ||
        SePuedeEjecutar(administradoresActivosRestantes, operacion);
}
