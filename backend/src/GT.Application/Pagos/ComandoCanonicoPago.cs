using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GT.Domain.Pagos;

namespace GT.Application.Pagos;

public record ComandoCanonicoPago(int Version, string Contenido, byte[] Hash);

/// <summary>Versión estable: propiedades fijas, facturas ordenadas y medios como multiconjunto.</summary>
public static class CanonicalizadorPago
{
    public static ComandoCanonicoPago Generar(GenerarOrdenEntrada entrada)
    {
        if (entrada.FacturaIds is null || entrada.FacturaIds.Length == 0 || entrada.FacturaIds.Distinct().Count() != entrada.FacturaIds.Length)
            throw new ReglaPagoException("facturas_invalidas", "Elegí facturas distintas para la orden.", campo: "facturaIds");
        if (entrada.Medios is null || entrada.Medios.Length == 0)
            throw new ReglaPagoException("medios_requeridos", "Agregá los medios de pago.", campo: "medios");
        var medios = entrada.Medios.Select((medio, indice) => SerializarMedio(medio, indice)).ToArray();
        Array.Sort(medios, (a, b) => a.AsSpan().SequenceCompareTo(b));
        return Crear(writer =>
        {
            writer.WriteNumber("versionCanonica", 1);
            writer.WriteString("operacion", "generar");
            writer.WriteNumber("proveedorId", entrada.ProveedorId);
            Fecha(writer, "fecha", entrada.Fecha);
            writer.WriteString("detalle", ReglasDePagos.NormalizarDetalle(entrada.Detalle));
            writer.WriteStartArray("facturaIds");
            foreach (var id in entrada.FacturaIds.Order()) writer.WriteNumberValue(id);
            writer.WriteEndArray();
            Dinero(writer, "importeTotalConfirmado", entrada.ImporteTotalConfirmado);
            writer.WriteStartArray("medios");
            foreach (var medio in medios) writer.WriteRawValue(medio);
            writer.WriteEndArray();
        });
    }

    public static ComandoCanonicoPago Anular(int oppId, AnularOrdenEntrada entrada) => Crear(writer =>
    {
        writer.WriteNumber("versionCanonica", 1);
        writer.WriteString("operacion", "anular");
        writer.WriteNumber("ordenPagoProveedorId", oppId);
        writer.WriteString("motivo", entrada.Motivo?.Trim());
    });

    private static ComandoCanonicoPago Crear(Action<Utf8JsonWriter> escribir)
    {
        var bytes = Json(escribir);
        return new(1, Encoding.UTF8.GetString(bytes), SHA256.HashData(bytes));
    }
    private static byte[] Json(Action<Utf8JsonWriter> escribir)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            escribir(writer);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }
    private static void Fecha(Utf8JsonWriter w, string nombre, DateOnly? fecha) =>
        w.WriteString(nombre, fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    private static void Dinero(Utf8JsonWriter w, string nombre, decimal importe)
    {
        if (importe < 0 || importe > ReglasDePagos.ImporteMaximo || decimal.Round(importe, 2) != importe)
            throw new ReglaPagoException("importe_invalido", "El importe debe conservar centavos y estar dentro de la capacidad.", campo: nombre);
        w.WriteString(nombre, importe.ToString("F2", CultureInfo.InvariantCulture));
    }
    private static byte[] SerializarMedio(MedioEntrada medio, int indice) => Json(w =>
    {
        switch (medio)
        {
            case ChequeEntrada cheque:
                w.WriteString("tipo", "cheque");
                Dinero(w, "importe", cheque.Importe);
                w.WriteNumber("bancoId", cheque.BancoId);
                if (cheque.NumeroCheque?.Trim().Length > 200)
                    throw new ReglaPagoException("longitud_excedida", "No puede superar 200 caracteres después de quitar los espacios exteriores",
                        campo: $"medios[{indice}].numeroCheque");
                var numero = ReglasDePagos.NormalizarNumeroCheque(cheque.NumeroCheque)
                    ?? throw new ReglaPagoException("numero_cheque_invalido", "Escribí un número de cheque de hasta 200 caracteres.", campo: $"medios[{indice}].numeroCheque");
                w.WriteString("numeroCheque", numero);
                Fecha(w, "fechaEmision", cheque.FechaEmision);
                Fecha(w, "fechaVencimiento", cheque.FechaVencimiento);
                w.WriteString("detalle", cheque.Detalle);
                break;
            case TransferenciaEntrada transferencia:
                w.WriteString("tipo", "transferencia");
                Dinero(w, "importe", transferencia.Importe);
                Fecha(w, "fecha", transferencia.Fecha);
                w.WriteString("cbuOrigen", transferencia.CbuOrigen);
                w.WriteString("cbuDestino", transferencia.CbuDestino);
                w.WriteString("numeroComprobante", transferencia.NumeroComprobante);
                w.WriteString("observaciones", transferencia.Observaciones);
                break;
            case EfectivoEntrada efectivo:
                w.WriteString("tipo", "efectivo");
                Dinero(w, "importe", efectivo.Importe);
                break;
            default:
                throw new ReglaPagoException("medio_invalido", "El medio de pago no está admitido.", campo: "medios");
        }
    });
}
