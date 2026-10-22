using System.Text.Json;
using System.Text.Json.Serialization;
using GT.Api.Proveedores;
using GT.Application.Pagos;

namespace GT.Api.Pagos;

public class SerializadorPagos : ISerializadorPagos
{
    public static readonly JsonSerializerOptions Opciones = CrearOpciones();
    private static JsonSerializerOptions CrearOpciones()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
        };
        opciones.Converters.Add(new ImporteDecimalJsonConverter());
        opciones.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return opciones;
    }
    public string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);

    public static async Task<T> LeerAsync<T>(HttpRequest request, CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, Opciones, ct)
                ?? throw new ReglaPagoException("entrada_invalida", "Completá los datos de la operación.");
        }
        catch (JsonException)
        {
            throw new ReglaPagoException("entrada_invalida", "Revisá los campos, tipos de medio, fechas e importes enviados.");
        }
    }

    public static IResult Json<T>(T valor, int estado = 200) => Results.Json(valor, Opciones, statusCode: estado);
    public static IResult Error(ReglaPagoException e) => Json(new
    {
        codigo = e.Codigo, mensaje = e.Message, campo = e.Campo,
        facturasNoDisponibles = e.FacturasNoDisponibles, saldoDisponible = e.SaldoDisponible,
        importeEfectivo = e.ImporteEfectivo,
    }, e.EstadoHttp);

    public static async Task<IResult> EjecutarAsync(Func<Task<IResult>> operacion)
    {
        try { return await operacion(); }
        catch (ReglaPagoException e) { return Error(e); }
    }

    public static IResult Escritura(ResultadoEscrituraPago resultado, HttpResponse response)
    {
        if (resultado.Location is not null) response.Headers.Location = resultado.Location;
        return Results.Content(resultado.Json, "application/json", statusCode: resultado.EstadoHttp);
    }
}
