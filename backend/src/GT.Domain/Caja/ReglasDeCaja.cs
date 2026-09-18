namespace GT.Domain.Caja;

/// <summary>
/// Las reglas puras del módulo.
///
/// <b>Ninguna lee el reloj</b> (convención [005]): los instantes los fija el caso de uso.
/// </summary>
public static class ReglasDeCaja
{
    /// <summary>El largo de <c>Concepto</c>, que es también el de la columna (FR-009).</summary>
    public const int LargoMaximoDelConcepto = 200;

    /// <summary>
    /// El mayor importe que entra en <c>decimal(18,2)</c>. Uno más grande se rechaza como formato incorrecto
    /// y no llega a la base como un <c>500</c>.
    /// </summary>
    public const decimal ImporteMaximo = 9_999_999_999_999_999.99m;

    /// <summary>
    /// RN6: saldo inicial + ingresos − egresos. Puede dar negativo y no falla: un egreso mayor que el saldo
    /// se registra igual (FR-015).
    /// </summary>
    public static decimal SaldoFinal(decimal saldoInicial, decimal totalIngresos, decimal totalEgresos) =>
        saldoInicial + totalIngresos - totalEgresos;

    /// <summary>RN4, FR-002: mayor o igual que cero y con a lo sumo dos decimales.</summary>
    public static bool SaldoInicialValido(decimal importe) =>
        importe >= 0m && TieneADosDecimales(importe) && importe <= ImporteMaximo;

    /// <summary>RN3, FR-008: mayor que cero y con a lo sumo dos decimales.</summary>
    public static bool ImporteValido(decimal importe) =>
        importe > 0m && TieneADosDecimales(importe) && importe <= ImporteMaximo;

    /// <summary>
    /// RN5, FR-009: presente, no vacío después de recortar, y hasta <see cref="LargoMaximoDelConcepto"/>
    /// caracteres ya recortado.
    /// </summary>
    public static bool ConceptoValido(string? concepto) =>
        concepto?.Trim() is { Length: > 0 and <= LargoMaximoDelConcepto };

    /// <summary>
    /// RN8: la referencia sigue al tipo. <c>null</c> si se admite; si no, qué referencia sobra.
    /// </summary>
    public static ReferenciaNoAdmitida? ReferenciaAdmitida(
        TipoMovimientoCaja tipo,
        int? facturaId,
        int? ordenDePagoId) => tipo switch
    {
        TipoMovimientoCaja.Ingreso when ordenDePagoId is not null => ReferenciaNoAdmitida.OrdenDePagoEnIngreso,
        TipoMovimientoCaja.Egreso when facturaId is not null => ReferenciaNoAdmitida.FacturaEnEgreso,
        _ => null,
    };

    private static bool TieneADosDecimales(decimal importe) => decimal.Round(importe, 2) == importe;
}

/// <summary>Por qué una referencia no corresponde al tipo del movimiento (RN8).</summary>
public enum ReferenciaNoAdmitida
{
    /// <summary>Un ingreso que trae una orden de pago.</summary>
    OrdenDePagoEnIngreso,

    /// <summary>Un egreso que trae una factura.</summary>
    FacturaEnEgreso,
}
