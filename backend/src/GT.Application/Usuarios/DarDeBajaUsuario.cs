using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>Motivo por el que una baja no se pudo ejecutar.</summary>
public enum ErrorBaja
{
    Ninguno,
    NoEncontrado,
    UltimoAdministrador,
}

public record ResultadoBaja(ErrorBaja Error, string Username)
{
    public bool Exitoso => Error is ErrorBaja.Ninguno;
}

/// <summary>
/// Baja lógica de un usuario (User Story 5).
///
/// No borra: cambia el estado a <c>inactivo</c> y el registro sigue existiendo y visible en el
/// listado (FR-006). La cuenta deja de poder autenticarse en el acto, y si tenía una sesión abierta
/// se corta en su siguiente operación — eso ya lo resuelve el <c>RevalidadorSesion</c> del Módulo 1,
/// sin código nuevo (research §7).
///
/// La confirmación explícita que pide FR-017 ocurre en la pantalla, antes de llamar acá.
/// </summary>
public class DarDeBajaUsuario(IRepositorioEscrituraUsuarios repositorio)
{
    public async Task<ResultadoBaja> EjecutarAsync(
        int idUsuario,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerParaEditarAsync(idUsuario, cancelacion);

        if (usuario is null)
        {
            return new ResultadoBaja(ErrorBaja.NoEncontrado, string.Empty);
        }

        // FR-019: es el tercero de los tres caminos que pueden dejar al sistema sin administradores.
        var esAdministradorActivo =
            usuario.Estado == EstadoUsuario.Activo &&
            usuario.Roles.Any(rol => rol.Codigo == CodigosRol.AdministradorSistema);

        if (esAdministradorActivo)
        {
            var restantes = await repositorio.ContarAdministradoresActivosExcluyendoAsync(
                idUsuario,
                cancelacion);

            var permitido = ProteccionUltimoAdministrador.SePuedeEjecutar(
                restantes,
                OperacionSobreAdministrador.DarDeBaja);

            if (!permitido)
            {
                return new ResultadoBaja(ErrorBaja.UltimoAdministrador, usuario.Username);
            }
        }

        usuario.Estado = EstadoUsuario.Inactivo;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoBaja(ErrorBaja.Ninguno, usuario.Username);
    }
}
