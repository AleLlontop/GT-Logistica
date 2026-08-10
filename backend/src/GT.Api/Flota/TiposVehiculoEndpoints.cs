using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Flota;
using GT.Application.Flota.TiposVehiculo;
using GT.Domain.Usuarios;

namespace GT.Api.Flota;

/// <summary>
/// Catálogo de tipos de vehículo (FR-009 a FR-011).
///
/// <b>Es el único grupo del módulo con permiso propio</b>: escribir exige
/// <c>flota.tipos.gestionar</c>, que sólo tiene el Administrador del sistema. Leer alcanza con
/// <c>flota.gestionar</c>, porque el formulario de vehículo consume esta lista para su selector
/// (FR-039, research §7).
///
/// El catálogo arranca vacío y no se precarga por migración: el primer tipo lo carga quien opera.
/// </summary>
public static class TiposVehiculoEndpoints
{
    public static void MapearTiposVehiculo(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/flota/tipos-vehiculo");

        grupo.MapGet("/", ListarAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FlotaGestionar));

        var administracion = PoliticasAutorizacion.Para(CodigosPermiso.FlotaTiposGestionar);

        grupo.MapPost("/", CrearAsync).RequireAuthorization(administracion);
        grupo.MapPut("/{id:int}", ModificarAsync).RequireAuthorization(administracion);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync).RequireAuthorization(administracion);

        // Recurso aparte y no un campo del PUT, igual que la reactivación de vehículo: así modificar
        // el nombre nunca cambia de paso el estado del tipo (FR-009).
        grupo.MapPost("/{id:int}/reactivacion", ReactivarAsync).RequireAuthorization(administracion);
    }

    /// <param name="soloActivos">
    /// Anulable a propósito: pedir el catálogo sin el parámetro tiene que tomar el valor por defecto
    /// del contrato en vez de fallar al enlazar (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        bool? soloActivos,
        GestionTiposVehiculo gestion,
        CancellationToken cancelacion) =>
        // Una lista vacía es una respuesta legítima: es el estado de toda instalación nueva, y la
        // pantalla lo dice con un mensaje explícito (FR-036, US1 esc. 1).
        Results.Ok(await gestion.ConsultarAsync(soloActivos ?? false, cancelacion));

    private static async Task<IResult> CrearAsync(
        TipoVehiculoRequest peticion,
        GestionTiposVehiculo gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.CrearAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/flota/tipos-vehiculo/{resultado.Tipo!.Id}", resultado.Tipo)
            : TraducirFallo(resultado);
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        TipoVehiculoRequest peticion,
        GestionTiposVehiculo gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.ModificarAsync(id, peticion, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Tipo) : TraducirFallo(resultado);
    }

    private static async Task<IResult> DarDeBajaAsync(
        int id,
        GestionTiposVehiculo gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.DarDeBajaAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    private static async Task<IResult> ReactivarAsync(
        int id,
        GestionTiposVehiculo gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.ReactivarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Tipo) : TraducirFallo(resultado);
    }

    private static IResult NoEncontrado() => Results.NotFound(
        new ErrorResponse(CodigosErrorFlota.NoEncontrado, MensajesFlota.NoEncontrado));

    private static IResult TraducirFallo(ResultadoTipoVehiculo resultado) => resultado.Error switch
    {
        ErrorTipoVehiculo.NoEncontrado => NoEncontrado(),

        ErrorTipoVehiculo.NombreDuplicado => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.NombreDuplicado,
            MensajesFlota.NombreDuplicado,
            resultado.Campo)),

        // 409 y no 400: lo que mandaron es válido; lo que impide la baja es el estado del catálogo, y
        // el cuerpo dice cuántos vehículos son (FR-010, SC-008).
        ErrorTipoVehiculo.ConVehiculos => Results.Json(
            new ErrorConDependencias(
                CodigosErrorFlota.TipoVehiculoEnUso,
                MensajesFlota.TipoVehiculoEnUso(resultado.CantidadVehiculos ?? 0),
                resultado.Campo)
            {
                CantidadVehiculos = resultado.CantidadVehiculos,
            },
            statusCode: StatusCodes.Status409Conflict),

        _ => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.DatosInvalidos,
            MensajesFlota.DatosInvalidos,
            resultado.Campo)),
    };
}
