using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Usuarios;
using GT.Domain.Usuarios;

namespace GT.Api.Usuarios;

/// <summary>
/// Gestión de usuarios (Módulo 2).
///
/// Todo el grupo exige el permiso <c>usuarios.gestionar</c>, que sólo otorga el rol
/// <i>Administrador del sistema</i> (FR-007). La única pantalla del módulo que no lo exige es el
/// cambio de contraseña propia, que vive en su propio grupo.
/// </summary>
public static class UsuariosEndpoints
{
    public static void MapearUsuarios(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/usuarios")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.UsuariosGestionar));

        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:int}", ObtenerDetalleAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapPost("/{id:int}/restablecer-password", RestablecerPasswordAsync);
        grupo.MapPut("/{id:int}/roles", AsignarRolesAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
    }

    /// <summary>
    /// Da de baja lógica un usuario (FR-006): lo deja <c>inactivo</c> y el registro sigue existiendo.
    /// La confirmación explícita ocurre en la pantalla, antes de llegar acá (FR-017).
    /// </summary>
    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaUsuario darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorBaja.NoEncontrado
            ? Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado))
            : Results.BadRequest(new ErrorResponse(
                CodigosErrorUsuarios.UltimoAdministrador,
                MensajesUsuarios.UltimoAdministrador));
    }

    /// <summary>
    /// Reemplaza los roles de un usuario (FR-018). Los deja exactamente como se enviaron: se agregan
    /// los que faltan y se quitan los que no vinieron.
    /// </summary>
    private static async Task<IResult> AsignarRolesAsync(
        int id,
        AsignarRolesRequest peticion,
        AsignarRoles asignar,
        CancellationToken cancelacion)
    {
        var resultado = await asignar.EjecutarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Usuario);
        }

        if (resultado.Error is ErrorRoles.NoEncontrado)
        {
            return Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado));
        }

        var error = resultado.Error switch
        {
            ErrorRoles.SinRoles => new ErrorResponse(
                CodigosErrorUsuarios.SinRoles,
                MensajesUsuarios.SinRoles,
                "roles"),

            ErrorRoles.UltimoAdministrador => new ErrorResponse(
                CodigosErrorUsuarios.UltimoAdministrador,
                MensajesUsuarios.UltimoAdministrador,
                "roles"),

            // Un rol inexistente sólo puede llegar de una petición armada a mano: la pantalla ofrece
            // los cuatro del sistema y nada más.
            _ => new ErrorResponse(
                CodigosErrorUsuarios.DatosInvalidos,
                MensajesUsuarios.DatosInvalidos,
                "roles"),
        };

        return Results.BadRequest(error);
    }

    /// <summary>
    /// Modifica un usuario (FR-015, FR-019). No incluye contraseña: para eso está el
    /// restablecimiento (FR-014).
    /// </summary>
    private static async Task<IResult> ModificarAsync(
        int id,
        ModificarUsuarioRequest peticion,
        ModificarUsuario modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Usuario);
        }

        if (resultado.Error is ErrorEdicion.NoEncontrado)
        {
            return Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado));
        }

        return Results.BadRequest(TraducirErrorEdicion(resultado));
    }

    /// <summary>
    /// Restablece la contraseña y la envía por email (FR-009).
    ///
    /// No lleva cuerpo de petición: el responsable de sistemas no elige la contraseña ni la ve. Un
    /// envío fallido devuelve <c>200</c> con <c>enviado: false</c>, no un error: la operación se
    /// hizo igual (FR-021).
    /// </summary>
    private static async Task<IResult> RestablecerPasswordAsync(
        int id,
        RestablecerPassword restablecer,
        CancellationToken cancelacion)
    {
        var resultado = await restablecer.EjecutarAsync(id, cancelacion);

        if (!resultado.Encontrado)
        {
            return Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado));
        }

        var mensaje = resultado.Enviado
            ? MensajesUsuarios.PasswordRestablecida(resultado.Email)
            : MensajesUsuarios.PasswordRestablecidaSinEnvio(resultado.Email);

        return Results.Ok(new RestablecerPasswordResponse(resultado.Enviado, mensaje));
    }

    private static ErrorResponse TraducirErrorEdicion(ResultadoEdicion resultado) => resultado.Error switch
    {
        ErrorEdicion.UsernameDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.UsernameDuplicado,
            MensajesUsuarios.UsernameDuplicado,
            resultado.Campo),

        ErrorEdicion.EmailDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.EmailDuplicado,
            MensajesUsuarios.EmailDuplicado,
            resultado.Campo),

        ErrorEdicion.PersonaYaVinculada => new ErrorResponse(
            CodigosErrorUsuarios.PersonaYaVinculada,
            MensajesUsuarios.PersonaYaVinculada(resultado.UsernameQueTieneLaPersona ?? "otro"),
            resultado.Campo),

        ErrorEdicion.PersonaInexistente => new ErrorResponse(
            CodigosErrorUsuarios.PersonaInexistente,
            MensajesUsuarios.PersonaInexistente,
            resultado.Campo),

        ErrorEdicion.UltimoAdministrador => new ErrorResponse(
            CodigosErrorUsuarios.UltimoAdministrador,
            MensajesUsuarios.UltimoAdministrador,
            resultado.Campo),

        _ => new ErrorResponse(
            CodigosErrorUsuarios.DatosInvalidos,
            MensajesUsuarios.DatosInvalidos,
            resultado.Campo),
    };

    /// <summary>
    /// Lista usuarios con los cuatro filtros combinables de FR-011.
    ///
    /// Una lista vacía es una respuesta legítima, no un error: la pantalla la traduce al mensaje
    /// explícito de "sin resultados" (FR-012).
    /// </summary>
    private static async Task<IResult> ListarAsync(
        ConsultarUsuarios consultar,
        string? username,
        string? email,
        string? rol,
        string? estado,
        CancellationToken cancelacion)
    {
        var usuarios = await consultar.EjecutarAsync(username, email, rol, estado, cancelacion);

        return Results.Ok(usuarios);
    }

    /// <summary>
    /// Detalle completo, con la persona asociada si tiene una. Nunca incluye la contraseña (FR-013).
    /// </summary>
    private static async Task<IResult> ObtenerDetalleAsync(
        int id,
        ConsultarUsuarios consultar,
        CancellationToken cancelacion)
    {
        var usuario = await consultar.ObtenerDetalleAsync(id, cancelacion);

        return usuario is null
            ? Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado))
            : Results.Ok(usuario);
    }

    /// <summary>
    /// Crea un usuario (User Story 1). No envía ningún correo: el responsable de sistemas comunica
    /// las credenciales por su cuenta (FR-021).
    /// </summary>
    private static async Task<IResult> CrearAsync(
        CrearUsuarioRequest peticion,
        CrearUsuario crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Created($"/api/usuarios/{resultado.Usuario!.Id}", resultado.Usuario);
        }

        return Results.BadRequest(TraducirError(resultado));
    }

    /// <summary>
    /// Convierte el motivo del rechazo en el cuerpo de error del contrato, con el texto exacto de
    /// <c>contracts/README.md</c> y el campo que la pantalla tiene que marcar en rojo.
    /// </summary>
    private static ErrorResponse TraducirError(ResultadoAlta resultado) => resultado.Error switch
    {
        ErrorAlta.UsernameDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.UsernameDuplicado,
            MensajesUsuarios.UsernameDuplicado,
            resultado.Campo),

        ErrorAlta.EmailDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.EmailDuplicado,
            MensajesUsuarios.EmailDuplicado,
            resultado.Campo),

        ErrorAlta.SinRoles => new ErrorResponse(
            CodigosErrorUsuarios.SinRoles,
            MensajesUsuarios.SinRoles,
            resultado.Campo),

        ErrorAlta.PersonaYaVinculada => new ErrorResponse(
            CodigosErrorUsuarios.PersonaYaVinculada,
            MensajesUsuarios.PersonaYaVinculada(resultado.UsernameQueTieneLaPersona ?? "otro"),
            resultado.Campo),

        ErrorAlta.PersonaInexistente => new ErrorResponse(
            CodigosErrorUsuarios.PersonaInexistente,
            MensajesUsuarios.PersonaInexistente,
            resultado.Campo),

        // Un rol inexistente sólo puede llegar de una petición armada a mano: la pantalla ofrece
        // los cuatro del sistema y nada más. Se informa como dato inválido.
        _ => new ErrorResponse(
            CodigosErrorUsuarios.DatosInvalidos,
            MensajesUsuarios.DatosInvalidos,
            resultado.Campo),
    };
}
