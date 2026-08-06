namespace GT.Domain.Usuarios;

/// <summary>
/// Estado de una cuenta de usuario. Sólo <see cref="Activo"/> puede iniciar sesión (FR-001).
/// Las transiciones entre estados son responsabilidad del Módulo 2; este módulo sólo las lee.
/// </summary>
public enum EstadoUsuario : byte
{
    Activo = 1,
    Inactivo = 2,
    Bloqueado = 3,
}
