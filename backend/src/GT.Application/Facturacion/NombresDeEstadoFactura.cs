using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Traducción de las cuatro enumeraciones del módulo entre el enum del dominio, el JSON y la pantalla.
///
/// El JSON usa <b>camelCase</b> —<c>facturaA</c>, <c>cuentaCorriente</c>, no <c>FacturaA</c>— igual
/// que los enums de los Módulos 3, 4 y 5 (convención [003]).
/// </summary>
public static class NombresDeEstadoFactura
{
    // ── Estado visible: lo que se ve y lo que se filtra (FR-041, FR-058a) ───────────────────────

    public static string EnJson(EstadoFacturaVisible estado) => estado switch
    {
        EstadoFacturaVisible.Pendiente => "pendiente",
        EstadoFacturaVisible.Vencida => "vencida",
        EstadoFacturaVisible.Pagada => "pagada",
        EstadoFacturaVisible.Anulada => "anulada",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    /// <summary>
    /// Lee el filtro de estado de la query.
    ///
    /// Un valor desconocido devuelve <c>null</c> y el filtro se ignora, en vez de romper: filtrar de
    /// más no es un error, y el listado responde su vista por defecto —todas, incluidas las
    /// anuladas— (convención [003], FR-058).
    /// </summary>
    public static EstadoFacturaVisible? LeerEstado(string? valor) => valor switch
    {
        "pendiente" => EstadoFacturaVisible.Pendiente,
        "vencida" => EstadoFacturaVisible.Vencida,
        "pagada" => EstadoFacturaVisible.Pagada,
        "anulada" => EstadoFacturaVisible.Anulada,
        _ => null,
    };

    /// <summary>
    /// Cómo se nombra el estado <b>dentro de una oración</b>, con la minúscula que pide el texto:
    /// "Mostrando sólo las facturas vencidas" (contracts/README §Listado).
    /// </summary>
    public static string EnTexto(EstadoFacturaVisible estado) => estado switch
    {
        EstadoFacturaVisible.Pendiente => "pendientes",
        EstadoFacturaVisible.Vencida => "vencidas",
        EstadoFacturaVisible.Pagada => "pagadas",
        EstadoFacturaVisible.Anulada => "anuladas",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    // ── Estado guardado: sólo aparece en el historial (FR-045) ──────────────────────────────────

    /// <summary>
    /// El estado guardado tal como sale en una línea del historial. <c>null</c> se conserva:
    /// significa <b>Alta</b> cuando es el anterior, y <b>Corrección de datos</b> cuando es el nuevo
    /// (FR-037).
    /// </summary>
    public static string? EnJson(EstadoFactura? estado) => estado switch
    {
        null => null,
        EstadoFactura.Pendiente => "pendiente",
        EstadoFactura.Pagada => "pagada",
        EstadoFactura.Anulada => "anulada",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    // ── Tipo de comprobante (FR-008) ────────────────────────────────────────────────────────────

    public static string EnJson(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "facturaA",
        TipoComprobante.FacturaB => "facturaB",
        TipoComprobante.FacturaC => "facturaC",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoComprobante? LeerTipoComprobante(string? valor) => valor switch
    {
        "facturaA" => TipoComprobante.FacturaA,
        "facturaB" => TipoComprobante.FacturaB,
        "facturaC" => TipoComprobante.FacturaC,
        _ => null,
    };

    /// <summary>Como sale impreso en el documento y en la pantalla: <c>Factura A</c>.</summary>
    public static string EnTexto(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "Factura A",
        TipoComprobante.FacturaB => "Factura B",
        TipoComprobante.FacturaC => "Factura C",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>La letra sola, para el recuadro del documento (FR-031i).</summary>
    public static string LetraDe(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "A",
        TipoComprobante.FacturaB => "B",
        TipoComprobante.FacturaC => "C",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// Código de comprobante que va debajo de la letra en el recuadro: <c>001</c>, <c>006</c> y
    /// <c>011</c>, con los tres dígitos que fija FR-031i.
    /// </summary>
    public static string CodigoDe(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "001",
        TipoComprobante.FacturaB => "006",
        TipoComprobante.FacturaC => "011",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// El título del bloque de identificación: <c>FACTURA A</c> (FR-031i). Es
    /// <see cref="EnTexto(TipoComprobante)"/> en mayúsculas, y se escribe aparte para que cambiar cómo
    /// se lee en pantalla no cambie en silencio cómo sale impreso.
    /// </summary>
    public static string TituloDe(TipoComprobante tipo) => $"FACTURA {LetraDe(tipo)}";

    // ── Tipo de facturación (FR-009) ────────────────────────────────────────────────────────────

    public static string EnJson(TipoFacturacion tipo) => tipo switch
    {
        TipoFacturacion.Original => "original",
        TipoFacturacion.Refacturacion => "refacturacion",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static TipoFacturacion? LeerTipoFacturacion(string? valor) => valor switch
    {
        "original" => TipoFacturacion.Original,
        "refacturacion" => TipoFacturacion.Refacturacion,
        _ => null,
    };

    public static string EnTexto(TipoFacturacion tipo) => tipo switch
    {
        TipoFacturacion.Original => "Original",
        TipoFacturacion.Refacturacion => "Refacturación",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    // ── Condición de venta (FR-009a) ────────────────────────────────────────────────────────────

    public static string EnJson(CondicionDeVenta condicion) => condicion switch
    {
        CondicionDeVenta.Contado => "contado",
        CondicionDeVenta.CuentaCorriente => "cuentaCorriente",
        CondicionDeVenta.Tarjeta => "tarjeta",
        CondicionDeVenta.Cheque => "cheque",
        _ => throw new ArgumentOutOfRangeException(nameof(condicion), condicion, null),
    };

    public static CondicionDeVenta? LeerCondicionDeVenta(string? valor) => valor switch
    {
        "contado" => CondicionDeVenta.Contado,
        "cuentaCorriente" => CondicionDeVenta.CuentaCorriente,
        "tarjeta" => CondicionDeVenta.Tarjeta,
        "cheque" => CondicionDeVenta.Cheque,
        _ => null,
    };

    public static string EnTexto(CondicionDeVenta condicion) => condicion switch
    {
        CondicionDeVenta.Contado => "Contado",
        CondicionDeVenta.CuentaCorriente => "Cuenta Corriente",
        CondicionDeVenta.Tarjeta => "Tarjeta de Débito / Crédito",
        CondicionDeVenta.Cheque => "Cheque",
        _ => throw new ArgumentOutOfRangeException(nameof(condicion), condicion, null),
    };
}
