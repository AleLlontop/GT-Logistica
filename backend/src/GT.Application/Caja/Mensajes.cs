namespace GT.Application.Caja;

/// <summary>
/// Códigos de error del módulo, exactamente los de <c>contracts/README.md</c>. El frontend decide con el
/// código y muestra el mensaje tal cual.
///
/// <b>La regla de códigos HTTP</b> (convención [005]): <c>400</c> cuando el problema está en lo que se tipeó
/// o se eligió; <c>409</c> cuando está en el estado de la caja —abierta, cerrada, de otro— y en las dos
/// respuestas del cierre que traen el resumen.
/// </summary>
public static class CodigosErrorCaja
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string ReferenciaInvalida = "referencia_invalida";
    public const string RangoInvalido = "rango_invalido";
    public const string CajaNoEncontrada = "caja_no_encontrada";

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    public const string CajaYaAbierta = "caja_ya_abierta";
    public const string CajaCerrada = "caja_cerrada";
    public const string CajaAjena = "caja_ajena";
    public const string ConfirmacionRequerida = "confirmacion_requerida";
    public const string CierreDesactualizado = "cierre_desactualizado";
}

/// <summary>Textos que se muestran tal cual, en español rioplatense (Principio II, contracts/README §Textos).</summary>
public static class MensajesCaja
{
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrada = "No encontramos esa caja.";

    // ── Apertura ────────────────────────────────────────────────────────────────────────────────
    public const string SaldoInicialRequerido = "Escribí el saldo inicial.";

    public const string SaldoInicialNegativo = "El saldo inicial no puede ser negativo.";

    public const string CajaYaAbierta = "Ya tenés una caja abierta. Cerrala antes de abrir otra.";

    // ── Movimientos ─────────────────────────────────────────────────────────────────────────────
    public const string TipoRequerido = "Elegí si es un ingreso o un egreso.";

    public const string ImporteRequerido = "Escribí un importe mayor que cero.";

    /// <summary>También para un saldo inicial con más de dos decimales.</summary>
    public const string ImporteMalEscrito = "Escribí el importe con hasta dos decimales.";

    public const string ConceptoRequerido = "Escribí el concepto del movimiento.";

    public const string ConceptoDemasiadoLargo = "El concepto puede tener hasta 200 caracteres.";

    public const string OrdenDePagoEnIngreso = "Una orden de pago sólo puede asociarse a un egreso.";

    public const string FacturaEnEgreso = "Una factura sólo puede asociarse a un ingreso.";

    public const string FacturaNoPendiente = "La factura elegida ya no está pendiente de cobro.";

    public const string OrdenDePagoInexistente = "La orden de pago elegida no existe.";

    public const string CajaCerrada = "Esta caja ya está cerrada y no admite nuevos movimientos.";

    /// <summary>La caja es personal también en el servidor (FR-035, tasks.md decisión 1).</summary>
    public const string CajaAjena =
        "Esta caja es de otro empleado. Sólo quien la abrió puede registrar movimientos y cerrarla.";

    // ── Cierre ──────────────────────────────────────────────────────────────────────────────────
    public const string ConfirmacionRequerida = "Revisá el resumen y confirmá el cierre.";

    public const string CierreDesactualizado =
        "El resumen cambió desde que lo viste. Revisalo antes de confirmar el cierre.";

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────
    public const string RangoInvalido = "La fecha desde es posterior a la hasta.";
}
