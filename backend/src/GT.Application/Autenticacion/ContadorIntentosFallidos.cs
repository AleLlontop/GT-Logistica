namespace GT.Application.Autenticacion;

/// <summary>
/// Límite de intentos de ingreso fallidos (FR-021).
///
/// El contador va por la combinación de **origen y cuenta**, no sólo por origen. G&amp;T Logística
/// sale a internet por una única conexión: contando sólo por origen, cinco errores de tipeo de
/// personas distintas dejarían fuera a toda la oficina durante un minuto (research §4).
///
/// Ninguna cuenta cambia de estado por esto: no hay bloqueo automático de cuentas (FR-016), y la
/// restricción se levanta sola al cumplirse el plazo.
/// </summary>
public interface IContadorIntentosFallidos
{
    /// <summary>Tiempo que falta para poder reintentar, o <c>null</c> si no hay restricción activa.</summary>
    TimeSpan? TiempoDeEspera(string origen, string usernameNormalizado);

    void RegistrarFallo(string origen, string usernameNormalizado);

    /// <summary>Un ingreso exitoso borra el contador de esa combinación.</summary>
    void RegistrarExito(string origen, string usernameNormalizado);
}

/// <summary>Parámetros de FR-021, en un solo lugar para que el test y la implementación no se separen.</summary>
public static class LimiteIntentos
{
    public const int FallosPermitidos = 5;

    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan Espera = TimeSpan.FromMinutes(1);
}
