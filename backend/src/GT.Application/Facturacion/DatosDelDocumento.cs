using System.Globalization;
using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// El logo vigente de la configuración, ya leído a memoria, o nada.
///
/// <b>No se congela en la factura</b>, y es la única excepción declarada al congelamiento de FR-034:
/// congelar los datos de texto alcanza para lo que la regla protege, y guardar una copia del archivo
/// por comprobante agregaría un archivo por factura sin ningún caso de uso que lo pida (research §5).
///
/// La consecuencia está aceptada y escrita: si se cambia el logo y después se corrige el CAE de una
/// factura vieja, el documento regenerado sale con el logo nuevo. Es el único dato del comprobante que
/// puede cambiar, y es una imagen, no un dato fiscal.
/// </summary>
public record LogoDelDocumento(byte[] Contenido);

/// <summary>Una fila de la tabla de detalle: las nueve columnas de FR-031e.</summary>
/// <param name="Codigo">El número del viaje.</param>
/// <param name="ProductoServicio">Origen, destino y número de remito del viaje.</param>
/// <param name="Importe">
/// El importe del viaje, que sale igual en <c>Precio unit.</c> y en <c>Importe</c>: la cantidad es
/// siempre <c>1</c> y no hay bonificación, así que por definición coinciden (FR-031e).
/// </param>
/// <param name="Subtotal">
/// El importe del viaje más su IVA. <b>Es informativo</b>: la suma de los subtotales por fila puede
/// diferir del total en centavos por redondeo, y manda el pie (FR-031f).
/// </param>
public record FilaDeDetalle(
    string Codigo,
    string ProductoServicio,
    string Cantidad,
    string UnidadDeMedida,
    string PrecioUnitario,
    string PorcentajeBonificacion,
    string Importe,
    string PorcentajeIva,
    string Subtotal);

/// <summary>
/// Todo lo que el documento imprime, ya formateado y en el orden de los nueve bloques de FR-031.
///
/// <b>Es el mapeo único desde la entidad</b> (research §2): lo produce
/// <see cref="Desde"/> y nada más, así que la vista previa y el archivo guardado no pueden decir cosas
/// distintas. Los importes ya vienen con el formato de moneda del sistema y las fechas con el de
/// fecha, para que el armador no tenga que decidir nada: sólo dibuja.
/// </summary>
public record DatosDelDocumento(
    // 1. Banda de ejemplar
    string BandaDeEjemplar,

    // 2. Bloque del emisor
    LogoDelDocumento? Logo,
    string EmisorRazonSocial,
    string EmisorCondicionIva,
    string EmisorDomicilio,

    // 3. Recuadro de letra
    string Letra,
    string CodigoDeComprobante,

    // 4. Bloque de identificación
    string Titulo,
    string NumeroComprobante,
    string FechaDeEmision,
    string Periodo,
    string EmisorCuit,
    string EmisorIngresosBrutos,
    string EmisorInicioActividades,

    // 5. Banda de vencimiento de pago
    string VencimientoPago,

    // 6. Banda de CBU — `null` la omite entera (FR-031)
    string? EmisorCbu,

    // 7. Bloque del cliente
    string ClienteRazonSocial,
    string ClienteCuit,
    string ClienteDomicilio,
    string ClienteCondicionIva,
    string CondicionDeVenta,
    string ClienteRemito,

    // 8. Tabla de detalle
    IReadOnlyList<FilaDeDetalle> Detalle,

    // 9. Pie de importes — `Observaciones` en `null` lo omite entero, rótulo incluido (FR-031)
    string? Observaciones,
    string Neto,
    string EtiquetaIva,
    string Iva,
    string Total,
    string Cae,
    string CaeVencimiento,

    // Leyendas (FR-031c, FR-031d)
    string LeyendaNoFiscal,
    string? LeyendaAnulada,
    string? MotivoAnulacion)
{
    /// <summary>
    /// La condición de IVA del cliente es <b>texto fijo</b> y no un campo del padrón ni un dato que se
    /// elija al emitir: todos los clientes de la empresa son empresas (FR-031h).
    /// </summary>
    public const string CondicionIvaDelCliente = "Responsable Inscripto";

    /// <summary>Qué se imprime donde no hay dato. Un guion, nunca un hueco que parezca un error.</summary>
    private const string SinDato = "—";

    /// <summary>
    /// El mapeo único, desde la entidad y no desde un DTO por camino.
    ///
    /// <b>Recibe la <c>FacturaCliente</c> exista o no en la base</b>: la vista previa le pasa una
    /// construida en memoria y la emisión le pasa la que acaba de persistir. Con la entidad como
    /// entrada única, el mapeo es uno solo y SC-007b se verifica con un test que compara byte a byte
    /// los dos PDF de la misma factura (research §2).
    /// </summary>
    /// <param name="logo">El vigente de la configuración, no uno congelado (research §5).</param>
    public static DatosDelDocumento Desde(FacturaCliente factura, LogoDelDocumento? logo)
    {
        var alicuota = factura.Alicuota;
        var estaAnulada = factura.Estado is EstadoFactura.Anulada;

        return new DatosDelDocumento(
            BandaDeEjemplar: factura.TipoFacturacion is TipoFacturacion.Refacturacion
                ? "REFACTURACIÓN"
                : "ORIGINAL",

            Logo: logo,
            EmisorRazonSocial: factura.EmisorRazonSocial,
            EmisorCondicionIva: factura.EmisorCondicionIva,
            EmisorDomicilio: factura.EmisorDomicilio,

            Letra: NombresDeEstadoFactura.LetraDe(factura.TipoComprobante),
            CodigoDeComprobante: NombresDeEstadoFactura.CodigoDe(factura.TipoComprobante),

            Titulo: NombresDeEstadoFactura.TituloDe(factura.TipoComprobante),
            NumeroComprobante: factura.NumeroComprobante,
            FechaDeEmision: FormatoDeDocumento.Fecha(factura.Fecha),

            // El período va acá, en el bloque de identificación, en formato `MM/AAAA`
            // (spec §Clarifications, CHK001).
            Periodo: PeriodoDe(factura),

            EmisorCuit: FormatoDeDocumento.Cuit(factura.EmisorCuit),
            EmisorIngresosBrutos: factura.EmisorIngresosBrutos ?? SinDato,
            EmisorInicioActividades: factura.EmisorInicioActividades is { } inicio
                ? FormatoDeDocumento.Fecha(inicio)
                : SinDato,

            VencimientoPago: FormatoDeDocumento.Fecha(factura.VencimientoPago),

            // Vacío ⇒ la banda no sale. Mismo criterio que el bloque de Observaciones (FR-031).
            EmisorCbu: string.IsNullOrWhiteSpace(factura.EmisorCbu) ? null : factura.EmisorCbu,

            ClienteRazonSocial: factura.ClienteRazonSocial,
            ClienteCuit: FormatoDeDocumento.Cuit(factura.ClienteCuit),
            ClienteDomicilio: factura.ClienteDomicilio,
            ClienteCondicionIva: CondicionIvaDelCliente,
            CondicionDeVenta: NombresDeEstadoFactura.EnTexto(factura.CondicionDeVenta),

            // Vacío a propósito: cada viaje lleva su propio remito en su fila del detalle (FR-031h).
            ClienteRemito: string.Empty,

            Detalle: [.. factura.Viajes
                .OrderBy(viaje => viaje.Fecha)
                .ThenBy(viaje => viaje.Numero)
                .Select(viaje => new FilaDeDetalle(
                    Codigo: viaje.Numero.ToString(CultureInfo.InvariantCulture),
                    ProductoServicio: ProductoServicioDe(viaje),
                    Cantidad: "1",
                    UnidadDeMedida: "UNIDAD",
                    PrecioUnitario: FormatoDeDocumento.Pesos(viaje.Importe),
                    PorcentajeBonificacion: FormatoDeDocumento.Porcentaje(0m),
                    Importe: FormatoDeDocumento.Pesos(viaje.Importe),
                    PorcentajeIva: FormatoDeDocumento.Porcentaje(alicuota * 100m),
                    // Informativo: su suma puede diferir del total por centavos, y manda el pie
                    // (FR-031f).
                    Subtotal: FormatoDeDocumento.Pesos(
                        viaje.Importe + Math.Round(
                            viaje.Importe * alicuota, 2, MidpointRounding.AwayFromZero))))],

            Observaciones: string.IsNullOrWhiteSpace(factura.Detalle) ? null : factura.Detalle,
            Neto: FormatoDeDocumento.Pesos(factura.Neto),
            EtiquetaIva: $"IVA ({FormatoDeDocumento.Porcentaje(alicuota * 100m)})",
            Iva: FormatoDeDocumento.Pesos(factura.Iva),
            Total: FormatoDeDocumento.Pesos(factura.Total),
            Cae: factura.Cae,
            CaeVencimiento: FormatoDeDocumento.Fecha(factura.CaeVencimiento),

            LeyendaNoFiscal: MensajesFacturas.LeyendaNoEsComprobanteFiscal,
            LeyendaAnulada: estaAnulada ? MensajesFacturas.LeyendaAnulada : null,
            MotivoAnulacion: estaAnulada ? factura.MotivoAnulacion : null);
    }

    /// <summary>El período facturado en <c>MM/AAAA</c> (FR-031, bloque 4).</summary>
    private static string PeriodoDe(FacturaCliente factura) =>
        $"{factura.PeriodoMes:00}/{factura.PeriodoAnio:0000}";

    /// <summary>
    /// Origen, destino y remito de un viaje en una sola celda (FR-031e).
    ///
    /// El remito va entre paréntesis y no como columna aparte porque la tabla ya tiene sus nueve
    /// columnas fijas. Un viaje sin remito no llega acá: la emisión lo rechaza (FR-019a).
    /// </summary>
    private static string ProductoServicioDe(Domain.Viajes.Viaje viaje)
    {
        var recorrido = $"Viaje {viaje.Origen} → {viaje.Destino}";

        return string.IsNullOrWhiteSpace(viaje.NumeroRemito)
            ? recorrido
            : $"{recorrido} (Remito {viaje.NumeroRemito})";
    }
}

/// <summary>
/// Cómo se escribe cada cosa <b>dentro del documento</b>: el formato de moneda y de fecha del resto
/// del sistema (FR-031, Principio II).
///
/// Vive acá y no en el armador porque es una decisión de contenido y no de dibujo: el armador recibe
/// texto ya formateado y sólo lo ubica. Es también lo que hace que el documento y la pantalla escriban
/// los mismos números de la misma forma — `$ 1.240.000,00` y `12/08/2026` —, que es lo que pide SC-007a.
/// </summary>
public static class FormatoDeDocumento
{
    /// <summary>
    /// Cultura argentina: punto de miles y coma decimal. Es la misma que usa <c>compartido/moneda</c>
    /// en el frontend; escribirlo distinto en el documento haría que la factura impresa y la pantalla
    /// se leyeran distinto.
    /// </summary>
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    /// <summary>Un importe como se lee acá: <c>$ 1.240.000,00</c>, siempre con dos decimales.</summary>
    public static string Pesos(decimal importe) =>
        "$ " + importe.ToString("N2", Argentina);

    /// <summary>Un porcentaje: <c>21,00 %</c>, <c>0,00 %</c> en una Factura C (FR-031j).</summary>
    public static string Porcentaje(decimal valor) =>
        valor.ToString("N2", Argentina) + " %";

    /// <summary>Una fecha como se lee acá: <c>12/08/2026</c>.</summary>
    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Argentina);

    /// <summary>
    /// Un CUIT con los guiones que lleva impreso: <c>30-71234567-8</c>. Se guarda normalizado a once
    /// dígitos y se muestra separado, que es como se lee en un comprobante.
    /// </summary>
    public static string Cuit(string cuitNormalizado) =>
        cuitNormalizado.Length == 11
            ? $"{cuitNormalizado[..2]}-{cuitNormalizado[2..10]}-{cuitNormalizado[10..]}"
            : cuitNormalizado;
}
