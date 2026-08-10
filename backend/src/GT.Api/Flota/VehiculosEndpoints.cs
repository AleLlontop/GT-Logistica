using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Flota;
using GT.Domain.Usuarios;

namespace GT.Api.Flota;

/// <summary>
/// Padrón de flota (FR-001 a FR-008f, FR-030 a FR-032, FR-038).
///
/// Todo el grupo exige <c>flota.gestionar</c>, que otorgan Tráfico y Administrador del sistema
/// (FR-039).
///
/// <b>El estado operativo se recibe, pero el que se devuelve puede no ser el mismo</b>: el operador
/// elige y el sistema guarda; lo que el listado y la ficha devuelven es el derivado (FR-014).
/// </summary>
public static class VehiculosEndpoints
{
    public static void MapearVehiculos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/flota/vehiculos")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FlotaGestionar));

        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:int}", ObtenerAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
        grupo.MapPost("/{id:int}/reactivacion", ReactivarAsync);
    }

    /// <param name="estado">
    /// Omitirlo <b>no</b> es lo mismo que pedir los tres: sin el parámetro se devuelven sólo los
    /// activos (FR-031). Un valor desconocido se ignora en vez de romper: filtrar de más no es un
    /// error.
    /// </param>
    /// <param name="pagina">
    /// Anulable a propósito, igual que los booleanos de query del proyecto: pedir el listado sin el
    /// parámetro tiene que tomar el valor por defecto en vez de fallar al enlazar (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        int? transportistaId,
        int? tipoVehiculoId,
        string? estado,
        string? estadoDocumentacion,
        int? pagina,
        ConsultarFlota consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeFlota(
            transportistaId,
            tipoVehiculoId,
            NombresDeEstadoFlota.LeerFiltroEstado(estado),
            NombresDeEstadoFlota.LeerEstadoDocumentacion(estadoDocumentacion),
            pagina ?? 1);

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarFichaVehiculo consultar,
        CancellationToken cancelacion)
    {
        var vehiculo = await consultar.EjecutarAsync(id, cancelacion);

        return vehiculo is not null ? Results.Ok(vehiculo) : NoEncontrado();
    }

    private static async Task<IResult> CrearAsync(
        VehiculoRequest peticion,
        CrearVehiculo crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/flota/vehiculos/{resultado.Vehiculo!.Id}", resultado.Vehiculo)
            : TraducirFallo(resultado);
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        VehiculoRequest peticion,
        ModificarVehiculo modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Vehiculo) : TraducirFallo(resultado);
    }

    /// <summary>
    /// Baja lógica. La confirmación previa la pide la pantalla, no el endpoint (FR-007, SC-009).
    /// </summary>
    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaVehiculo darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    /// <param name="peticion">
    /// Cuerpo <b>opcional</b>: sólo hace falta si el transportista o el tipo de la unidad fueron
    /// dados de baja mientras estuvo afuera (FR-008e, US6 esc. 11).
    /// </param>
    private static async Task<IResult> ReactivarAsync(
        int id,
        ReactivacionRequest? peticion,
        ReactivarVehiculo reactivar,
        CancellationToken cancelacion)
    {
        var resultado = await reactivar.EjecutarAsync(id, peticion, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    private static IResult NoEncontrado() => Results.NotFound(
        new ErrorResponse(CodigosErrorFlota.NoEncontrado, MensajesFlota.NoEncontrado));

    private static IResult TraducirFallo(ResultadoVehiculo resultado) => resultado.Error switch
    {
        ErrorVehiculo.NoEncontrado => NoEncontrado(),

        ErrorVehiculo.PatenteDuplicada => Error(
            CodigosErrorFlota.PatenteDuplicada, MensajesFlota.PatenteDuplicada, resultado.Campo),

        ErrorVehiculo.PatenteDeVehiculoDadoDeBaja => Error(
            CodigosErrorFlota.PatenteDeVehiculoDadoDeBaja,
            MensajesFlota.PatenteDeVehiculoDadoDeBaja,
            resultado.Campo),

        ErrorVehiculo.PatenteInvalida => Error(
            CodigosErrorFlota.PatenteInvalida, MensajesFlota.PatenteInvalida, resultado.Campo),

        ErrorVehiculo.TipoVehiculoInexistente => Error(
            CodigosErrorFlota.TipoVehiculoInexistente,
            MensajesFlota.TipoVehiculoInexistente,
            resultado.Campo),

        ErrorVehiculo.TransportistaInexistente => Error(
            CodigosErrorFlota.TransportistaInexistente,
            MensajesFlota.TransportistaInexistente,
            resultado.Campo),

        // El mensaje nombra el documento que lo impide: sin eso, quien opera sabe que no puede pero
        // no qué resolver (FR-014a).
        ErrorVehiculo.DisponibleConDocumentacionVencida => Error(
            CodigosErrorFlota.DisponibleConDocumentacionVencida,
            MensajesFlota.DisponibleConDocumentacionVencida(resultado.DocumentoQueImpide ?? "un documento"),
            resultado.Campo),

        ErrorVehiculo.DisponibleSinDocumentacion => Error(
            CodigosErrorFlota.DisponibleSinDocumentacion,
            MensajesFlota.DisponibleSinDocumentacion,
            resultado.Campo),

        ErrorVehiculo.TransportistaInactivoAlReactivar => Error(
            CodigosErrorFlota.TransportistaInactivoAlReactivar,
            MensajesFlota.TransportistaInactivoAlReactivar,
            resultado.Campo),

        ErrorVehiculo.TipoInactivoAlReactivar => Error(
            CodigosErrorFlota.TipoInactivoAlReactivar,
            MensajesFlota.TipoInactivoAlReactivar,
            resultado.Campo),

        // Cualquier error sin código propio se comunica como datos inválidos, con el campo marcado.
        // Nunca se cae: una respuesta de error no puede convertirse en un 500.
        _ => Error(CodigosErrorFlota.DatosInvalidos, MensajesFlota.DatosInvalidos, resultado.Campo),
    };

    private static IResult Error(string codigo, string mensaje, string? campo) =>
        Results.BadRequest(new ErrorResponse(codigo, mensaje, campo));
}
