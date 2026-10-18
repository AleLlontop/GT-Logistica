using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GT.Domain.Proveedores;

namespace GT.Api.Proveedores;

/// <summary>Se registra sólo en las respuestas de 013; ningún contrato anterior cambia.</summary>
public partial class ImporteDecimalJsonConverter : JsonConverter<decimal>
{
    public static bool IntentarLeer(string? texto,out decimal importe)
    {
        importe=0;
        return texto is not null && texto.Length<=19 && Canonico().IsMatch(texto) &&
            decimal.TryParse(texto,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out importe) && importe<=ReglasFacturaProveedor.Maximo;
    }
    public override decimal Read(ref Utf8JsonReader lector,Type tipo,JsonSerializerOptions opciones)
    {
        if(lector.TokenType!=JsonTokenType.String || !IntentarLeer(lector.GetString(),out var importe))
            throw new JsonException("El importe debe ser una cadena decimal canónica con exactamente dos decimales.");
        return importe;
    }
    public override void Write(Utf8JsonWriter escritor,decimal valor,JsonSerializerOptions opciones)=>
        escritor.WriteStringValue(valor.ToString("F2",CultureInfo.InvariantCulture));
    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.[0-9]{2}$")]
    private static partial Regex Canonico();
}
