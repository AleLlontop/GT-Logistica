using GT.Application.Usuarios.Personas;
using GT.Domain.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>Motivo por el que una edición no se pudo guardar.</summary>
public enum ErrorEdicion
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    UsernameDuplicado,
    EmailDuplicado,
    PersonaInexistente,
    PersonaYaVinculada,
    UltimoAdministrador,
}

public record ResultadoEdicion(ErrorEdicion Error, UsuarioDetalle? Usuario, string? Campo = null)
{
    public bool Exitoso => Error is ErrorEdicion.Ninguno;

    public string? UsernameQueTieneLaPersona { get; init; }
}

/// <summary>
/// Edición de un usuario (User Story 3).
///
/// Dos cosas que la distinguen del alta:
///
/// - Las comparaciones de unicidad **excluyen al propio usuario** (FR-015): conservar su propio
///   username o email no puede leerse como un conflicto.
/// - Sacar la cuenta de <c>activo</c> puede dejar al sistema sin administradores, así que pasa por
///   <see cref="ProteccionUltimoAdministrador"/> (FR-019).
///
/// No toca la contraseña: para eso está el restablecimiento (FR-014).
/// </summary>
public class ModificarUsuario(
    IRepositorioEscrituraUsuarios repositorio,
    IRepositorioGestionUsuarios gestion,
    IRepositorioPersonas personas)
{
    public async Task<ResultadoEdicion> EjecutarAsync(
        int idUsuario,
        ModificarUsuarioRequest peticion,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerParaEditarAsync(idUsuario, cancelacion);

        if (usuario is null)
        {
            return new ResultadoEdicion(ErrorEdicion.NoEncontrado, null);
        }

        var username = (peticion.Username ?? string.Empty).Trim();
        var email = (peticion.Email ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            return new ResultadoEdicion(ErrorEdicion.DatosInvalidos, null, "username");
        }

        if (!ValidadorEmail.EsValido(email))
        {
            return new ResultadoEdicion(ErrorEdicion.DatosInvalidos, null, "email");
        }

        var estado = EstadoUsuarioTexto.Interpretar(peticion.Estado);

        if (estado is null)
        {
            return new ResultadoEdicion(ErrorEdicion.DatosInvalidos, null, "estado");
        }

        var usernameNormalizado = NormalizadorUsername.Normalizar(username);
        var emailNormalizado = NormalizadorEmail.Normalizar(email);

        // FR-015: la comparación excluye al propio usuario.
        if (await gestion.ExisteUsernameAsync(usernameNormalizado, idUsuario, cancelacion))
        {
            return new ResultadoEdicion(ErrorEdicion.UsernameDuplicado, null, "username");
        }

        if (await gestion.ExisteEmailAsync(emailNormalizado, idUsuario, cancelacion))
        {
            return new ResultadoEdicion(ErrorEdicion.EmailDuplicado, null, "email");
        }

        // ── FR-019: no dejar al sistema sin administradores activos ────────────────────────────
        var saleDeActivo = usuario.Estado == EstadoUsuario.Activo && estado != EstadoUsuario.Activo;

        if (saleDeActivo)
        {
            var esAdministrador = usuario.Roles.Any(rol =>
                rol.Codigo == CodigosRol.AdministradorSistema);

            var restantes = await repositorio.ContarAdministradoresActivosExcluyendoAsync(
                idUsuario,
                cancelacion);

            var permitido = ProteccionUltimoAdministrador.SePuedeEjecutar(
                esAdministrador,
                restantes,
                OperacionSobreAdministrador.CambiarEstado);

            if (!permitido)
            {
                return new ResultadoEdicion(ErrorEdicion.UltimoAdministrador, null, "estado");
            }
        }

        // ── FR-008 y FR-023: la persona nueva tiene que estar disponible ───────────────────────
        var cambiaLaPersona = peticion.PersonaId != usuario.PersonaId;
        Domain.Personas.Persona? personaNueva = null;

        if (cambiaLaPersona && peticion.PersonaId is { } personaId)
        {
            personaNueva = await personas.ObtenerPorIdAsync(personaId, cancelacion);

            if (personaNueva is null || !personaNueva.Activa)
            {
                return new ResultadoEdicion(ErrorEdicion.PersonaInexistente, null, "personaId");
            }

            var yaLaTiene = await personas.UsernameDelUsuarioVinculadoAsync(personaId, cancelacion);

            if (yaLaTiene is not null)
            {
                return new ResultadoEdicion(ErrorEdicion.PersonaYaVinculada, null, "personaId")
                {
                    UsernameQueTieneLaPersona = yaLaTiene,
                };
            }
        }

        usuario.Username = username;
        usuario.UsernameNormalizado = usernameNormalizado;
        usuario.Email = email;
        usuario.EmailNormalizado = emailNormalizado;
        usuario.Estado = estado.Value;
        // `null` desasocia la persona y la libera para otro usuario: es la única forma de liberarla
        // (FR-008). Se actualiza también la navegación para que la respuesta refleje el cambio y no
        // el estado anterior.
        usuario.PersonaId = peticion.PersonaId;

        if (cambiaLaPersona)
        {
            usuario.Persona = personaNueva;
        }

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoEdicion(ErrorEdicion.Ninguno, UsuarioDetalle.Desde(usuario));
    }
}
