namespace GT.Domain.Facturacion;

/// <summary>Los tres importes de la factura, ya calculados (FR-022, FR-023).</summary>
public record ImportesDeFactura(decimal Neto, decimal Iva, decimal Total);

/// <summary>
/// Cómo se calculan el neto, el IVA y el total de una factura (FR-022, FR-023, research §9).
///
/// <b>Una sola función y un solo lugar.</b> El alta del frontend muestra los importes en vivo mientras
/// se marcan viajes, pero <b>el valor que se guarda es siempre el que sale de acá</b>, calculado a
/// partir de los viajes que el servidor encontró en la base: FR-024 lo pide explícitamente, "ni desde
/// la pantalla ni invocando la acción directamente".
///
/// <b>Todo en <c>decimal</c>, nunca en punto flotante</b> (convención [005]): un total que alguien va a
/// comparar contra una planilla no puede acumular error de representación.
/// </summary>
public static class CalculadorImportes
{
    public static ImportesDeFactura Calcular(
        IEnumerable<decimal> importesDeViajes,
        TipoComprobante tipo)
    {
        // Suma exacta: los importes de los viajes ya vienen con dos decimales de la tabla `Viajes`, así
        // que no hay nada que redondear acá (FR-022).
        var neto = importesDeViajes.Sum();

        var iva = IvaSobre(neto, tipo);

        return new ImportesDeFactura(neto, iva, neto + iva);
    }

    /// <summary>
    /// El IVA sobre un importe, con <b>redondeo comercial</b> —la mitad para arriba— a dos decimales
    /// (spec §Assumptions).
    ///
    /// <c>MidpointRounding.AwayFromZero</c> y no el valor por defecto de .NET, que es
    /// <c>ToEven</c> —redondeo bancario—: con <c>ToEven</c>, <c>0,125</c> daría <c>0,12</c> y una
    /// planilla que redondea a la mitad para arriba daría <c>0,13</c>. La diferencia es de un centavo y
    /// aparece justo cuando alguien está comparando los dos números.
    /// </summary>
    public static decimal IvaSobre(decimal neto, TipoComprobante tipo) =>
        Math.Round(neto * AlicuotasIva.De(tipo), 2, MidpointRounding.AwayFromZero);
}
