using GT.Domain.Viajes;

namespace GT.Domain.Facturacion;

/// <summary>
/// Entidad principal del módulo: lo que se le cobra a un cliente por un grupo de viajes rendidos de un
/// período (FR-007 a FR-014).
///
/// <b>Se llama <c>FacturaCliente</c> y no <c>Factura</c></b> porque el sistema va a tener después la
/// liquidación al transportista, que también es una factura; la tabla, en cambio, se llama
/// <c>Facturas</c>, que es como la nombra el negocio.
///
/// <b>Trece columnas son copias congeladas</b>: los diez datos del emisor (FR-034) y tres del cliente
/// (FR-034a). Una factura dice a quién se le facturó ese día, no quién es hoy: corregir un domicilio
/// en el padrón no cambia ninguna factura ya emitida (SC-007).
///
/// Del cliente se guardan <b>los dos</b>, copia y referencia, y ninguno reemplaza al otro: la copia es
/// lo que muestran la ficha, el listado y el documento; la referencia es lo que permite filtrar
/// (FR-058) y totalizar (FR-061). Del emisor se guarda <b>sólo la copia</b>: no hay
/// <c>EmpresaEmisoraId</c>, porque no hay nada que filtrar por emisor habiendo uno solo.
///
/// <b>Esta misma entidad es la entrada única del armador del documento</b> (research §2): la vista
/// previa la construye en memoria sin persistirla y la emisión la construye, la persiste, y las dos
/// renderizan con la misma función. Dos traducciones al mismo destino se separan sin que nadie lo
/// note, y entonces revisar la vista previa deja de servir para algo.
/// </summary>
public class FacturaCliente
{
    public int Id { get; set; }

    // ── Identificación y clasificación ──────────────────────────────────────────────────────────

    /// <summary>
    /// Formato <c>0014-00000003</c>: punto de venta y número correlativo (FR-027). Lo tipea quien
    /// emite, porque sale de AFIP/ARCA por fuera del sistema.
    ///
    /// <b>Único entre las no anuladas</b>, por índice único filtrado. Anular libera el número.
    /// </summary>
    public required string NumeroComprobante { get; set; }

    /// <summary>
    /// Fecha de facturación. Se propone hoy y se puede cambiar (FR-012). Es la fecha de corte de los
    /// totales (FR-061) y la que ordena el listado (FR-059).
    /// </summary>
    public required DateOnly Fecha { get; set; }

    public required TipoComprobante TipoComprobante { get; set; }

    public required TipoFacturacion TipoFacturacion { get; set; }

    public required CondicionDeVenta CondicionDeVenta { get; set; }

    /// <summary>Mes del período facturado, 1 a 12. La base lo acota con un <c>CHECK</c>.</summary>
    public required byte PeriodoMes { get; set; }

    /// <summary>
    /// Año del período. <b>Sin <c>CHECK</c> en la base</b>: la lista de años se amplía con el tiempo y
    /// una restricción obligaría a una migración cada vez. Se valida en la aplicación (FR-010).
    /// </summary>
    public required short PeriodoAnio { get; set; }

    /// <summary>
    /// Texto libre opcional (FR-013). <b>Es el único dato de texto que se puede corregir después</b>
    /// (FR-035), y sale impreso en el pie del documento bajo el rótulo <c>Observaciones</c>.
    /// </summary>
    public string? Detalle { get; set; }

    // ── Cliente: referencia y copia congelada (FR-034a) ─────────────────────────────────────────

    public required int ClienteId { get; set; }

    public Cliente? Cliente { get; set; }

    public required string ClienteRazonSocial { get; set; }

    public required string ClienteCuit { get; set; }

    /// <summary>
    /// <b>Es lo que vuelve obligatorio el domicilio para facturar</b> (FR-011a): la columna no admite
    /// nulo, así que un cliente sin domicilio no puede llegar acá. La validación de la capa de
    /// aplicación da el mensaje bueno; esta columna es la garantía.
    /// </summary>
    public required string ClienteDomicilio { get; set; }

    // ── Emisor: sólo copia congelada, diez columnas (FR-034) ────────────────────────────────────

    public required string EmisorRazonSocial { get; set; }

    public required string EmisorCuit { get; set; }

    public required string EmisorDomicilio { get; set; }

    public required string EmisorCondicionIva { get; set; }

    public string? EmisorIngresosBrutos { get; set; }

    public DateOnly? EmisorInicioActividades { get; set; }

    public string? EmisorPuntoDeVenta { get; set; }

    public string? EmisorCbu { get; set; }

    public string? EmisorTelefono { get; set; }

    public string? EmisorEmail { get; set; }

    // ── Importes (FR-022 a FR-025) ──────────────────────────────────────────────────────────────

    /// <summary>Suma exacta de los importes de los viajes incluidos (FR-022).</summary>
    public decimal Neto { get; set; }

    /// <summary>Neto × alícuota del tipo, con redondeo comercial a dos decimales (FR-023).</summary>
    public decimal Iva { get; set; }

    /// <summary>Neto + IVA. La base lo verifica con un <c>CHECK</c>.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// La alícuota <b>no es columna</b>: se deriva del tipo de comprobante, que sí está congelado
    /// (research §5). Agregarle una columna sería un campo que ninguna FR pide.
    /// </summary>
    public decimal Alicuota => AlicuotasIva.De(TipoComprobante);

    // ── CAE y vencimientos ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Código de autorización electrónica, obtenido en AFIP/ARCA por fuera del sistema. Obligatorio
    /// para dar por emitida la factura (FR-028) y <b>corregible pero nunca vaciable</b> (FR-035).
    /// </summary>
    public required string Cae { get; set; }

    /// <summary>No anterior a <see cref="Fecha"/> (FR-029).</summary>
    public required DateOnly CaeVencimiento { get; set; }

    /// <summary>
    /// No anterior a <see cref="Fecha"/> (FR-030). Se propone <c>Fecha + 30 días</c>.
    ///
    /// <b>Es el único plazo que mueve la factura a <c>vencida</c></b>: el vencimiento del CAE no
    /// influye en el estado de cobro, son dos plazos distintos (FR-041, US5 esc. 10).
    /// </summary>
    public required DateOnly VencimientoPago { get; set; }

    // ── Estado, cobro y anulación ───────────────────────────────────────────────────────────────

    /// <summary>Toda factura nace <c>pendiente</c> (FR-040).</summary>
    public EstadoFactura Estado { get; set; } = EstadoFactura.Pendiente;

    /// <summary>
    /// Obligatoria al registrar el cobro, no anterior a <see cref="Fecha"/> (FR-042).
    /// <b>Corregir el CAE no la toca</b> (US4 esc. 8).
    /// </summary>
    public DateOnly? FechaCobro { get; set; }

    /// <summary>
    /// Obligatorio al anular (FR-046). Visible en la ficha, en el listado filtrado por anuladas, e
    /// <b>impreso en el documento regenerado</b> (FR-031d).
    /// </summary>
    public string? MotivoAnulacion { get; set; }

    // ── Refacturación (FR-049, FR-049a, FR-050) ─────────────────────────────────────────────────

    /// <summary>
    /// A qué factura anulada reemplaza esta Refacturación. Obligatoria si
    /// <see cref="TipoFacturacion"/> es <c>Refacturacion</c> y prohibida si es <c>Original</c>.
    ///
    /// <b>No hay columna espejo <c>ReemplazadaPorId</c></b>: la otra dirección se resuelve con una
    /// consulta por <c>FacturaReemplazadaId == id</c>, y una columna que habría que mantener
    /// sincronizada puede discrepar del dato que ya está (FR-050).
    /// </summary>
    public int? FacturaReemplazadaId { get; set; }

    public FacturaCliente? FacturaReemplazada { get; set; }

    // ── Documento generado (FR-031, FR-031a) ────────────────────────────────────────────────────

    /// <summary>
    /// Ruta relativa dentro del volumen. <b>No admite nulo</b>: toda factura emitida tiene su
    /// documento, porque se genera en la misma operación que la crea (FR-054, SC-007a).
    ///
    /// La factura guarda la <b>referencia</b>, nunca el contenido. Se sirve por endpoint autorizado.
    /// </summary>
    public required string DocumentoRuta { get; set; }

    // ── Relaciones ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los viajes incluidos. La relación la sostiene <c>Viajes.FacturaId</c> y no una tabla
    /// intermedia: una columna escalar no puede apuntar a dos facturas, así que la exclusividad ya es
    /// estructural (research §4).
    /// </summary>
    public ICollection<Viaje> Viajes { get; } = [];

    /// <summary>Historial y correcciones, de la más vieja a la más nueva (FR-045, FR-037).</summary>
    public ICollection<CambioDeEstadoFactura> CambiosDeEstado { get; } = [];
}
