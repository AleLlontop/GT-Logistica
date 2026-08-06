namespace GT.Application.Usuarios;

/// <summary>
/// Identificadores estables de error del Módulo 2, para que el frontend decida qué hacer sin
/// depender del texto.
///
/// Los del Módulo 1 (<c>sesion_expirada</c>, <c>sin_permiso</c>, <c>error_inesperado</c>) no se
/// repiten acá: siguen viviendo en <c>GT.Application.Autenticacion.CodigosError</c> y se usan tal
/// cual.
/// </summary>
public static class CodigosErrorUsuarios
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string UsernameDuplicado = "username_duplicado";
    public const string EmailDuplicado = "email_duplicado";
    public const string SinRoles = "sin_roles";
    public const string PersonaYaVinculada = "persona_ya_vinculada";
    public const string PersonaInexistente = "persona_inexistente";
    public const string UltimoAdministrador = "ultimo_administrador";
    public const string DniDuplicado = "dni_duplicado";
    public const string PersonaVinculada = "persona_vinculada";
    public const string PersonaEsChofer = "persona_es_chofer";
    public const string PasswordActualIncorrecta = "password_actual_incorrecta";
    public const string NoEncontrado = "no_encontrado";
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Son exactamente los que fija <c>contracts/README.md</c>. Ninguno expone detalles técnicos,
/// códigos de error ni nombres de campos internos.
/// </summary>
public static class MensajesUsuarios
{
    // ── Errores de validación y de regla de negocio ────────────────────────────────────────────
    public const string DatosInvalidos = "Revisá los campos marcados en rojo.";

    public const string UsernameDuplicado = "Ese nombre de usuario ya está en uso. Elegí otro.";

    public const string EmailDuplicado = "Ese email ya está registrado para otro usuario.";

    public const string SinRoles = "Todo usuario tiene que tener al menos un rol asignado.";

    public const string PersonaInexistente =
        "La persona seleccionada ya no está disponible. Actualizá la lista y volvé a elegir.";

    public const string UltimoAdministrador =
        "No se puede hacer: tiene que quedar siempre al menos un usuario activo con el rol " +
        "Administrador del sistema.";

    public const string DniDuplicado = "Ese DNI ya está registrado en el padrón.";

    public const string PasswordActualIncorrecta = "Tu contraseña actual no es correcta.";

    public const string NoEncontrado =
        "Ese registro ya no existe. Puede que lo hayan eliminado desde otra sesión.";

    /// <summary>Identifica a qué usuario pertenece la persona, para que se la pueda liberar (FR-008).</summary>
    public static string PersonaYaVinculada(string username) =>
        $"Esa persona ya está asociada al usuario {username}. Desvinculala de esa cuenta antes de " +
        "asignarla acá.";

    /// <summary>Identifica a qué usuario pertenece la persona que se quiso dar de baja (FR-028).</summary>
    public static string PersonaVinculada(string username) =>
        $"No se puede dar de baja: está asociada al usuario {username}. Desvinculala primero.";

    /// <summary>
    /// La persona está registrada como chofer (Módulo 3). Se la manda a la pantalla correcta en vez
    /// de dejarla adivinando por qué el sistema no la deja.
    /// </summary>
    public const string PersonaEsChofer =
        "No se puede dar de baja: esta persona está registrada como chofer. Dalo de baja desde la " +
        "pantalla de Choferes.";

    // ── Confirmaciones de operación ────────────────────────────────────────────────────────────
    public const string CambiosGuardados = "Los cambios se guardaron correctamente.";

    public const string PersonaRegistrada = "La persona se registró correctamente.";

    public const string PasswordPropiaCambiada = "Tu contraseña se cambió correctamente.";

    public static string UsuarioCreado(string username) =>
        $"El usuario {username} se creó correctamente.";

    public static string RolesActualizados(string username) =>
        $"Los roles de {username} se actualizaron.";

    public static string UsuarioDadoDeBaja(string username) =>
        $"El usuario {username} quedó inactivo.";

    public static string PersonaDadaDeBaja(string nombre, string apellido) =>
        $"{nombre} {apellido} quedó dada de baja.";

    /// <summary>
    /// El plazo de 24 horas lo fija el Módulo 1; el aviso de que la sesión se cerró corresponde a
    /// FR-032.
    /// </summary>
    public static string PasswordRestablecida(string email) =>
        $"Se generó una contraseña temporal y se envió a {email}. Vence en 24 horas. Si tenía una " +
        "sesión abierta, se cerró.";

    /// <summary>
    /// El restablecimiento ya quedó registrado: esto informa el fallo del envío, no una operación
    /// revertida (FR-021).
    /// </summary>
    public static string PasswordRestablecidaSinEnvio(string email) =>
        $"La contraseña se restableció, pero no pudimos enviar el correo a {email}. Verificá la " +
        "dirección o volvé a intentar el envío.";
}
