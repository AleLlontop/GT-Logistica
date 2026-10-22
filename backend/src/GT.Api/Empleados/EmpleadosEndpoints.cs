using GT.Api.Autorizacion;
using GT.Application.Adelantos;
using GT.Application.Autenticacion;
using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Usuarios;
using GT.Application.Usuarios.Personas;
using GT.Domain.Usuarios;

namespace GT.Api.Empleados;

///<summary>
/// ABM de empleados no choferes. Reutiliza Persona y los mismos casos de uso de validación
/// que el padrón general. No crea una entidad duplicada.
///</summary>
public static class EmpleadosEndpoints
{
    public static void MapearEmpleados(this IEndpointRouteBuilder rutas)
    {
        var consultar = rutas.MapGroup("/api/empleados")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.EmpleadosConsultar));

        consultar.MapGet("/", ListarAsync);
        consultar.MapGet("/{id:int}", ObtenerAsync);
        consultar.MapGet("/{id:int}/adelantos", HistorialAdelantosAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.AdelantosConsultar));
        consultar.MapGet("/{id:int}/cajas", HistorialCajasAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.CajaConsultar));

        var gestionar = rutas.MapGroup("/api/empleados")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.EmpleadosGestionar));

        gestionar.MapPost("/", CrearAsync);
        gestionar.MapPut("/{id:int}", ModificarAsync);
        gestionar.MapDelete("/{id:int}", DarDeBajaAsync);
    }

    private static async Task<IResult> ListarAsync(
        string? texto,
        bool? soloActivas,
        ConsultarPersonas consultar,
        CancellationToken cancelacion)
    {
        var personas = await consultar.EjecutarAsync(texto, soloActivas ?? false, cancelacion);
        return Results.Ok(personas.Where(persona => persona.Tipo == "empleado" && persona.Activa));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarPersonas consultar,
        CancellationToken cancelacion)
    {
        var persona = await consultar.ObtenerAsync(id, cancelacion);
        return persona is { Tipo: "empleado" } ? Results.Ok(persona) : NoEncontrado();
    }

    private static async Task<IResult> CrearAsync(
        PersonaRequest peticion,
        CrearPersona crear,
        CancellationToken cancelacion)
    {
        // El tipo no lo elige el usuario del módulo: siempre se crea como empleado.
        var resultado = await crear.EjecutarAsync(ComoEmpleado(peticion), cancelacion);
        return resultado.Exitoso
            ? Results.Created($"/api/empleados/{resultado.Persona!.Id}", resultado.Persona)
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        PersonaRequest peticion,
        ConsultarPersonas consultar,
        ModificarPersona modificar,
        CancellationToken cancelacion)
    {
        var actual = await consultar.ObtenerAsync(id, cancelacion);
        if (actual is not { Tipo: "empleado" })
        {
            return NoEncontrado();
        }

        var resultado = await modificar.EjecutarAsync(id, ComoEmpleado(peticion), cancelacion);
        return resultado.Exitoso
            ? Results.Ok(resultado.Persona)
            : resultado.Error is ErrorPersona.NoEncontrada
                ? NoEncontrado()
                : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> DarDeBajaAsync(
        int id,
        ConsultarPersonas consultar,
        IRepositorioPersonas personas,
        IRepositorioCaja cajas,
        DarDeBajaPersona darDeBaja,
        CancellationToken cancelacion)
    {
        var actual = await consultar.ObtenerAsync(id, cancelacion);
        if (actual is not { Tipo: "empleado" })
        {
            return NoEncontrado();
        }

        var usuarioId = await personas.IdUsuarioVinculadoAsync(id, cancelacion);
        if (usuarioId is not null &&
            await cajas.ExisteCajaAbiertaAsync(usuarioId.Value, cancelacion))
        {
            return Results.Conflict(new ErrorResponse(
                "empleado_con_caja_abierta",
                "No podés dar de baja a este empleado porque tiene una caja abierta. Cerrala primero."));
        }

        // DarDeBajaPersona conserva las reglas del Módulo 2: una persona vinculada a cualquier usuario
        // no se da de baja hasta desasociarla. Esta ruta no evade esa restricción.
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);
        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorPersona.NoEncontrada
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> HistorialAdelantosAsync(
        int id,
        ConsultarPersonas consultarPersonas,
        ConsultarAdelantos consultarAdelantos,
        CancellationToken cancelacion)
    {
        var persona = await consultarPersonas.ObtenerAsync(id, cancelacion);
        if (persona is not { Tipo: "empleado" })
        {
            return NoEncontrado();
        }

        // El caso de uso existente filtra en SQL, pagina y calcula el total; acá sólo reunimos todas
        // las páginas para mostrar el historial completo dentro de la ficha.
        var items = new List<AdelantoListado>();
        var numeroPagina = 1;
        var total = 0;
        do
        {
            var resultado = await consultarAdelantos.EjecutarAsync(
                new FiltrosDeAdelantos(PersonaId: id, Pagina: numeroPagina), cancelacion);

            if (resultado.Rechazo is not null || resultado.Pagina is null)
            {
                return Results.BadRequest(new ErrorResponse(
                    "datos_invalidos", "No pudimos consultar el historial de adelantos."));
            }

            items.AddRange(resultado.Pagina.Items);
            total = resultado.Pagina.Total;
            numeroPagina++;
        } while (items.Count < total);

        return Results.Ok(items);
    }

    private static async Task<IResult> HistorialCajasAsync(
        int id,
        ConsultarPersonas consultarPersonas,
        IRepositorioPersonas personas,
        ConsultarCajas consultarCajas,
        CancellationToken cancelacion)
    {
        var empleado = await consultarPersonas.ObtenerAsync(id, cancelacion);
        if (empleado is not { Tipo: "empleado" })
        {
            return NoEncontrado();
        }

        var usuarioId = await personas.IdUsuarioVinculadoAsync(id, cancelacion);
        if (usuarioId is null)
        {
            return Results.Ok(Array.Empty<CajaListado>());
        }

        var cajasDelEmpleado = new List<CajaListado>();
        var pagina = 1;
        PaginaDe<CajaListado> resultado;
        do
        {
            resultado = await consultarCajas.EjecutarAsync(pagina++, cancelacion);
            cajasDelEmpleado.AddRange(resultado.Items.Where(caja => caja.Responsable.Id == usuarioId.Value));
        } while (resultado.Pagina * resultado.TamanioPagina < resultado.Total);

        return Results.Ok(cajasDelEmpleado);
    }

    private static PersonaRequest ComoEmpleado(PersonaRequest peticion) => new(
        peticion.Nombre,
        peticion.Apellido,
        peticion.Dni,
        "empleado",
        peticion.Telefono,
        peticion.Email,
        peticion.FechaNacimiento);

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorUsuarios.NoEncontrado,
        MensajesUsuarios.NoEncontrado));

    private static ErrorResponse TraducirError(ResultadoPersona resultado) => resultado.Error switch
    {
        ErrorPersona.DniDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.DniDuplicado,
            MensajesUsuarios.DniDuplicado,
            resultado.Campo),
        ErrorPersona.Vinculada => new ErrorResponse(
            CodigosErrorUsuarios.PersonaVinculada,
            MensajesUsuarios.PersonaVinculada(resultado.UsernameQueLaTiene ?? "otro"),
            resultado.Campo),
        ErrorPersona.EsChofer => new ErrorResponse(
            CodigosErrorUsuarios.PersonaEsChofer,
            MensajesUsuarios.PersonaEsChofer,
            resultado.Campo),
        _ => new ErrorResponse(
            CodigosErrorUsuarios.DatosInvalidos,
            MensajesUsuarios.DatosInvalidos,
            resultado.Campo),
    };
}