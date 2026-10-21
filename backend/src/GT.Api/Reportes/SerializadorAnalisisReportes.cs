using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GT.Application.Reportes.Analisis;
using GT.Application.Reportes;

namespace GT.Api.Reportes;

/// <summary>Opciones exclusivas de015: no modificar el JSON monetario ni los errores de módulos anteriores.</summary>
public static class SerializadorAnalisisReportes
{
    public static readonly JsonSerializerOptions Opciones = CrearOpciones();
    private static JsonSerializerOptions CrearOpciones()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new ImporteAnalisisJsonConverter());
        opciones.Converters.Add(new InstanteAnalisisJsonConverter());
        opciones.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return opciones;
    }
    public static IResult Json<T>(T valor) => Results.Json(valor, Opciones);
    public static IResult Error(string codigo, string mensaje, int estado, string? campo = null) =>
        Results.Json(new ErrorAnalisis(codigo, mensaje, campo is null ? null : new Dictionary<string, string[]> { [campo] = [mensaje] }), Opciones, statusCode: estado);
    public static IResult Error(ReglaAnalisisException error) => Error(error.Codigo, error.Message, 400, error.Campo);
    public static async Task<IResult> EjecutarAsync(Func<Task<IResult>> operacion)
    {
        try { return await operacion(); }
        catch (PrecisionMonetariaXlsxException) { return RespuestasDeReporte.PrecisionMonetariaXlsx(); }
        catch (TopeExportacionAnalisisException error) { return RespuestasDeReporte.TopeSuperado(error.Filas, error.Tope); }
        catch (ReporteNoGeneradoException) { return RespuestasDeReporte.NoGenerado(); }
        catch (ReglaAnalisisException error) { return Error(error); }
        catch (AnalisisNoDisponibleException error) { return Error(AnalisisNoDisponibleException.Codigo, error.Message, 503); }
        catch (OverflowException) { return Error("analisis_fuera_de_rango", "Los importes del análisis exceden el rango disponible.", 422); }
        catch (Exception error) when (error is not OperationCanceledException)
        { return Error("analisis_no_generado", "No se pudo obtener el análisis. Conservá los filtros y volvé a intentar.", 500); }
    }

}

public record ErrorAnalisis(string Codigo, string Mensaje, IReadOnlyDictionary<string, string[]>? Errores = null);

/// <summary>Decimal firmado y agregado sin el tope del formulario013; jamás redondear para serializar.</summary>
public sealed partial class ImporteAnalisisJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("El importe debe ser una cadena decimal canónica.");
        var texto = reader.GetString();
        if (texto is null || texto.Length > 34 || !Canonico().IsMatch(texto) ||
            !decimal.TryParse(texto, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor) ||
            Formatear(valor) != texto)
            throw new JsonException("El importe debe conservar exactamente sus dos decimales.");
        return valor;
    }
    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(Formatear(value));
    private static string Formatear(decimal valor)
    {
        if (decimal.Round(valor, 2) != valor) throw new JsonException("El importe no tiene escala monetaria de dos decimales.");
        return (valor == 0m ? 0m : valor).ToString("F2", CultureInfo.InvariantCulture);
    }
    [GeneratedRegex(@"^-?(0|[1-9][0-9]*)\.[0-9]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Canonico();
}

public sealed class InstanteAnalisisJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetDateTimeOffset().ToUniversalTime();
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.UtcDateTime);
}
