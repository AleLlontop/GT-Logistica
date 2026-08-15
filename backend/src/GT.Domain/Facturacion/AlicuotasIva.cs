namespace GT.Domain.Facturacion;

/// <summary>
/// Qué alícuota de IVA le corresponde a cada tipo de comprobante (FR-023).
///
/// <b>Están fijas en el código y no las configura ninguna pantalla</b> (spec §Assumptions). No es una
/// omisión: ninguna FR pide configurarlas, y una pantalla de alícuotas sería alcance que nadie pidió.
///
/// <b>La alícuota no se guarda en la factura.</b> Se deriva del tipo de comprobante, que sí está
/// congelado y es inmutable después de emitir (FR-036), así que el valor no puede cambiar por una
/// operación del negocio. El único escenario donde la derivación discreparía de lo impreso es un
/// cambio de estas constantes, que es un cambio de versión del sistema. Queda anotado como candidato
/// a columna si alguna vez las alícuotas se vuelven configurables (research §5).
/// </summary>
public static class AlicuotasIva
{
    public static decimal De(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => 0.21m,

        // Misma alícuota que la A: la diferencia entre A y B es a quién se le factura, no cuánto IVA
        // lleva (spec §Clarifications).
        TipoComprobante.FacturaB => 0.21m,

        // IVA cero. El total es igual al neto, y eso **no** es un error ni una factura incompleta
        // (FR-023, contracts/README §Bloque 3).
        TipoComprobante.FacturaC => 0.00m,

        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>La alícuota como porcentaje entero para los textos: <c>21</c>, <c>0</c>.</summary>
    public static decimal PorcentajeDe(TipoComprobante tipo) => De(tipo) * 100m;
}
