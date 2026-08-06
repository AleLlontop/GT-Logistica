using GT.Application.Usuarios.Personas;
using GT.Domain.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>
/// Acceso a usuarios que necesita la gestión del Módulo 2. Lo implementa la capa de infraestructura.
/// </summary>
public interface IRepositorioGestionUsuarios
{
    Task<bool> ExisteUsernameAsync(
        string usernameNormalizado,
        int? excluyendoUsuarioId = null,
        CancellationToken cancelacion = default);

    Task<bool> ExisteEmailAsync(
        string emailNormalizado,
        int? excluyendoUsuarioId = null,
        CancellationToken cancelacion = default);

    Task<IReadOnlyList<Rol>> ObtenerRolesPorCodigoAsync(
        IReadOnlyList<string> codigos,
        CancellationToken cancelacion = default);

    /// <summary>Agrega y guarda. Devuelve el usuario con su <c>Id</c> ya asignado.</summary>
    Task<ResultadoGuardado> AgregarAsync(Usuario usuario, CancellationToken cancelacion = default);
}

/// <summary>
/// Cómo terminó un guardado que pudo chocar contra un índice único.
///
/// La infraestructura traduce la violación del índice a uno de estos valores en vez de dejar
/// escapar una excepción técnica: es lo que permite que quien pierde una carrera de altas reciba el
/// mismo mensaje de duplicado que da la validación previa (research §3).
/// </summary>
public enum ResultadoGuardado
{
    Exitoso,
    UsernameDuplicado,
    EmailDuplicado,
    PersonaYaVinculada,
}

/// <summary>Motivo por el que un alta no se pudo completar.</summary>
public enum ErrorAlta
{
    Ninguno,
    DatosInvalidos,
    UsernameDuplicado,
    EmailDuplicado,
    SinRoles,
    RolInexistente,
    PersonaInexistente,
    PersonaYaVinculada,
}

public record ResultadoAlta(ErrorAlta Error, UsuarioDetalle? Usuario, string? Campo = null)
{
    public bool Exitoso => Error is ErrorAlta.Ninguno;

    /// <summary>Username del usuario que ya tiene la persona, para poder nombrarlo (FR-008).</summary>
    public string? UsernameQueTieneLaPersona { get; init; }
}

/// <summary>
/// Alta de usuarios (User Story 1).
///
/// El orden de las validaciones sigue el de la spec: primero el formato, después las reglas de
/// negocio. Y la unicidad se valida dos veces —acá y en la base— porque entre el SELECT y el INSERT
/// hay una ventana en la que dos altas simultáneas creen las dos que el username está libre
/// (FR-002, FR-003, research §3).
/// </summary>
public class CrearUsuario(
    IRepositorioGestionUsuarios repositorio,
    IRepositorioPersonas personas,
    IHasheadorPasswordApp hasheador,
    TimeProvider reloj)
{
    public const int LargoMinimoPassword = 8;

    public async Task<ResultadoAlta> EjecutarAsync(
        CrearUsuarioRequest peticion,
        CancellationToken cancelacion = default)
    {
        var username = (peticion.Username ?? string.Empty).Trim();
        var email = (peticion.Email ?? string.Empty).Trim();
        var password = peticion.Password ?? string.Empty;

        // ── Formato ────────────────────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(username))
        {
            return new ResultadoAlta(ErrorAlta.DatosInvalidos, null, "username");
        }

        if (!ValidadorEmail.EsValido(email))
        {
            return new ResultadoAlta(ErrorAlta.DatosInvalidos, null, "email");
        }

        // FR-004: el mínimo se controla también acá y no sólo en pantalla, porque el servidor no
        // puede confiar en que la petición venga del formulario.
        if (password.Length < LargoMinimoPassword)
        {
            return new ResultadoAlta(ErrorAlta.DatosInvalidos, null, "password");
        }

        var estado = EstadoUsuarioTexto.Interpretar(peticion.Estado);

        if (estado is null)
        {
            return new ResultadoAlta(ErrorAlta.DatosInvalidos, null, "estado");
        }

        // ── FR-001: al menos un rol ────────────────────────────────────────────────────────────
        var codigosRol = peticion.Roles ?? [];

        if (codigosRol.Count == 0)
        {
            return new ResultadoAlta(ErrorAlta.SinRoles, null, "roles");
        }

        var roles = await repositorio.ObtenerRolesPorCodigoAsync(codigosRol, cancelacion);

        if (roles.Count != codigosRol.Distinct().Count())
        {
            return new ResultadoAlta(ErrorAlta.RolInexistente, null, "roles");
        }

        // ── FR-020: normalizar antes de comparar ───────────────────────────────────────────────
        var usernameNormalizado = NormalizadorUsername.Normalizar(username);
        var emailNormalizado = NormalizadorEmail.Normalizar(email);

        if (await repositorio.ExisteUsernameAsync(usernameNormalizado, null, cancelacion))
        {
            return new ResultadoAlta(ErrorAlta.UsernameDuplicado, null, "username");
        }

        if (await repositorio.ExisteEmailAsync(emailNormalizado, null, cancelacion))
        {
            return new ResultadoAlta(ErrorAlta.EmailDuplicado, null, "email");
        }

        // ── FR-008 y FR-023: la persona tiene que existir, estar activa y estar libre ──────────
        Domain.Personas.Persona? persona = null;

        if (peticion.PersonaId is { } personaId)
        {
            persona = await personas.ObtenerPorIdAsync(personaId, cancelacion);

            if (persona is null || !persona.Activa)
            {
                return new ResultadoAlta(ErrorAlta.PersonaInexistente, null, "personaId");
            }

            var yaLaTiene = await personas.UsernameDelUsuarioVinculadoAsync(personaId, cancelacion);

            if (yaLaTiene is not null)
            {
                return new ResultadoAlta(ErrorAlta.PersonaYaVinculada, null, "personaId")
                {
                    UsernameQueTieneLaPersona = yaLaTiene,
                };
            }
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;

        var usuario = new Usuario
        {
            Username = username,
            UsernameNormalizado = usernameNormalizado,
            Email = email,
            EmailNormalizado = emailNormalizado,
            PasswordHash = hasheador.Hashear(password),
            Estado = estado.Value,
            FechaAlta = ahora,
            UltimoAcceso = null,
            PasswordTemporalGeneradaEn = null,
            // FR-032: toda operación que fija una contraseña deja esta marca, que es lo que corta
            // las sesiones emitidas antes.
            PasswordActualizadaEn = ahora,
            PersonaId = peticion.PersonaId,
            // Se asigna también la navegación, no sólo el identificador: sin esto la respuesta del
            // alta diría `persona: null` aunque la asociación quedó guardada, porque el DTO lee la
            // navegación y nadie la habría cargado.
            Persona = persona,
        };

        foreach (var rol in roles)
        {
            usuario.Roles.Add(rol);
        }

        // ── La base tiene la última palabra ────────────────────────────────────────────────────
        var guardado = await repositorio.AgregarAsync(usuario, cancelacion);

        return guardado switch
        {
            ResultadoGuardado.UsernameDuplicado =>
                new ResultadoAlta(ErrorAlta.UsernameDuplicado, null, "username"),
            ResultadoGuardado.EmailDuplicado =>
                new ResultadoAlta(ErrorAlta.EmailDuplicado, null, "email"),
            ResultadoGuardado.PersonaYaVinculada =>
                new ResultadoAlta(ErrorAlta.PersonaYaVinculada, null, "personaId"),
            _ => new ResultadoAlta(ErrorAlta.Ninguno, UsuarioDetalle.Desde(usuario)),
        };
    }
}

/// <summary>
/// Hasheo de contraseñas visto desde la capa de aplicación. Lo implementa la de infraestructura,
/// que es donde vive el hasheador real.
/// </summary>
public interface IHasheadorPasswordApp
{
    string Hashear(string password);
}

/// <summary>
/// Validación de formato de email (FR-003).
///
/// Deliberadamente laxa: comprueba que haya algo antes de una arroba, algo después, y un punto en el
/// dominio. No intenta implementar el RFC —eso es una fuente conocida de rechazar direcciones
/// válidas— porque la única forma de saber si un email existe es mandarle un correo.
/// </summary>
public static class ValidadorEmail
{
    public static bool EsValido(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return false;
        }

        var partes = email.Split('@');

        return partes.Length == 2 &&
               partes[0].Length > 0 &&
               partes[1].Contains('.') &&
               partes[1].Length >= 3 &&
               !email.Contains(' ');
    }
}
