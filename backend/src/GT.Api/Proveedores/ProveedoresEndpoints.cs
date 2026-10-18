using GT.Api.Autorizacion;
using GT.Application.Proveedores;
using GT.Domain.Usuarios;

namespace GT.Api.Proveedores;

public static class ProveedoresEndpoints
{
    public static void MapearProveedores(this IEndpointRouteBuilder rutas)
    {
        rutas.MapGet("/api/proveedores/{id:int}/liquidaciones-disponibles",async(int id,ConsultarLiquidacionesDisponibles consultar,CancellationToken ct)=>
        {
            try { return RespuestasProveedor.Json(await consultar.EjecutarAsync(id,ct)); }
            catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        }).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ProveedoresFacturasCargar));
        rutas.MapGet("/api/proveedores/busqueda",async(string? q,bool? soloActivos,int? pagina,IRepositorioProveedores repo,CancellationToken ct)=>
        {
            try { return Results.Ok(await repo.ConsultarAsync(new(Q:q,Estado:(soloActivos??false)?"activo":null,Pagina:pagina??1),ct)); }
            catch(ProveedorException error) { return RespuestasProveedor.Error(error); }
        }).RequireAuthorization(PoliticasAutorizacion.BuscarProveedores);
        var grupo = rutas.MapGroup("/api/proveedores")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ProveedoresGestionar));
        grupo.MapGet("/", (string? q, string? razonSocial, string? nombreComercial, string? cuit, string? tipo, string? estado,
            int? pagina, IRepositorioProveedores repo, CancellationToken ct) => Ejecutar(async () =>
                Results.Ok(await repo.ConsultarAsync(new(q, razonSocial, nombreComercial, cuit, tipo, estado, pagina ?? 1), ct))));
        grupo.MapGet("/transportistas-externos", (string? q, int? pagina, IRepositorioProveedores repo, CancellationToken ct) =>
            Ejecutar(async () => Results.Ok(await repo.ExternosAsync(q, pagina ?? 1, ct))));
        grupo.MapGet("/{id:int}", (int id, IRepositorioProveedores repo, CancellationToken ct) => Ejecutar(async () =>
            Results.Ok(await repo.ObtenerAsync(id, ct) ?? throw ProveedorException.NoEncontrado())));
        grupo.MapPost("/", (ProveedorPeticion datos, GestionarProveedores gestionar, CancellationToken ct) => Ejecutar(async () =>
        {
            var creado = await gestionar.GuardarAsync(null, datos, ct);
            return Results.Created($"/api/proveedores/{creado.Id}", creado);
        }));
        grupo.MapPut("/{id:int}", (int id, ProveedorPeticion datos, GestionarProveedores gestionar, CancellationToken ct) =>
            Ejecutar(async () => Results.Ok(await gestionar.GuardarAsync(id, datos, ct))));
        grupo.MapPost("/{id:int}/baja", (int id, VersionProveedor datos, GestionarProveedores gestionar, CancellationToken ct) =>
            Ejecutar(async () => Results.Ok(await gestionar.CambiarEstadoAsync(id, datos.Version, false, ct))));
        grupo.MapPost("/{id:int}/alta", (int id, VersionProveedor datos, GestionarProveedores gestionar, CancellationToken ct) =>
            Ejecutar(async () => Results.Ok(await gestionar.CambiarEstadoAsync(id, datos.Version, true, ct))));
    }

    private static async Task<IResult> Ejecutar(Func<Task<IResult>> operacion)
    {
        try { return await operacion(); }
        catch (ProveedorException error)
        {
            return Results.Json(new { codigo = error.Codigo, mensaje = error.Message, campo = error.Campo }, statusCode: error.Tipo switch
            { TipoErrorProveedor.Conflicto => 409, TipoErrorProveedor.NoEncontrado => 404, _ => 400 });
        }
    }
}
