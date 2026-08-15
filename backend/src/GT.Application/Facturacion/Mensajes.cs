using System.Text.Json.Serialization;
using GT.Application.Autenticacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Códigos de error del módulo. El frontend decide con el código y muestra el mensaje tal cual, sin
/// interpretarlo. Son exactamente los que fijan <c>contracts/facturacion-api.yaml</c> y
/// <c>contracts/README.md</c>.
///
/// <b>La regla de códigos HTTP</b> (research §11, convención [005]): <c>400</c> cuando el problema
/// está en lo que se tipeó —campos, formatos, duplicados, datos faltantes de otra entidad—;
/// <c>409</c> cuando está en el estado de algo que se comparte o que cambió —viaje ya facturado,
/// anulada ya reemplazada, transición no permitida, factura inmutable, confirmación pendiente—.
/// </summary>
public static class CodigosErrorFacturas
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string NoEncontrada = "no_encontrado";

    // ── Empresa emisora (400) ───────────────────────────────────────────────────────────────────
    public const string CuitInvalido = "cuit_invalido";
    public const string EmailInvalido = "email_invalido";
    public const string ArchivoNoAdmitido = "archivo_no_admitido";
    public const string ArchivoNoGuardado = "archivo_no_guardado";

    // ── Emisión (400): falta o está mal algo de lo que se tipeó o se eligió ─────────────────────
    public const string EmpresaEmisoraIncompleta = "empresa_emisora_incompleta";
    public const string ClienteInexistente = "cliente_inexistente";
    public const string ClienteInactivo = "cliente_inactivo";
    public const string ClienteSinDomicilio = "cliente_sin_domicilio";
    public const string ViajeSinRemito = "viaje_sin_remito";
    public const string NumeroDuplicado = "numero_duplicado";
    public const string NumeroInvalido = "numero_invalido";
    public const string SinViajesSeleccionados = "sin_viajes_seleccionados";
    public const string RefacturacionSinReemplazada = "refacturacion_sin_reemplazada";
    public const string OriginalConReemplazada = "original_con_reemplazada";
    public const string VencimientoPagoAnterior = "vencimiento_pago_anterior";
    public const string CaeVencimientoAnterior = "cae_vencimiento_anterior";
    public const string CaeRequerido = "cae_requerido";
    public const string FechaCobroAnterior = "fecha_cobro_anterior";
    public const string MotivoRequerido = "motivo_requerido";
    public const string RangoDeFechasRequerido = "rango_de_fechas_requerido";

    // ── De acá para abajo, 409: el estado de algo compartido o que cambió ───────────────────────
    public const string ViajeYaFacturado = "viaje_ya_facturado";
    public const string AnuladaYaReemplazada = "anulada_ya_reemplazada";
    public const string TransicionNoPermitida = "transicion_no_permitida";
    public const string FacturaAnuladaInmutable = "factura_anulada_inmutable";
    public const string FacturaCobrada = "factura_cobrada";

    /// <summary>
    /// Las dos confirmaciones de FR-032. El primer intento responde así <b>sin haber creado nada</b>
    /// y el segundo lleva <c>confirmado: true</c>.
    /// </summary>
    public const string EmisionRequiereConfirmacion = "emision_requiere_confirmacion";

    /// <summary>Módulo 5, FR-055a: rendir exige el remito porque sale impreso en la factura.</summary>
    public const string RemitoRequerido = "remito_requerido";
}

/// <summary>
/// Qué hay que confirmar antes de emitir (FR-032). Viaja en el cuerpo del <c>409</c> para que la
/// pantalla elija el diálogo sin tener que interpretar el texto.
/// </summary>
public enum MotivoConfirmacion
{
    /// <summary>Un viaje incluido tiene importe en cero y no suma al neto.</summary>
    ViajeEnCero,

    /// <summary>La fecha de facturación es anterior a la de algún viaje incluido.</summary>
    FechaAnteriorAViaje,
}

/// <summary>Por qué un viaje no se puede facturar o produce un rechazo.</summary>
public enum MotivoViajeEnConflicto
{
    SinRemito,
    YaFacturado,
    ImporteEnCero,
    PosteriorAlaFactura,
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Son exactamente los que fija <c>contracts/README.md</c>, palabra por palabra. Ninguno expone
/// detalles técnicos, códigos de error ni nombres de campos internos.
/// </summary>
public static class MensajesFacturas
{
    // ── Errores comunes ─────────────────────────────────────────────────────────────────────────
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrada = "No encontramos lo que buscabas.";

    // ── Empresa emisora ─────────────────────────────────────────────────────────────────────────
    public const string SinConfigurar =
        "La empresa emisora todavía no está configurada. Completá al menos la razón social, el CUIT, " +
        "el domicilio y la condición de IVA para poder emitir facturas.";

    public const string CuitInvalido =
        "El CUIT tiene que tener once dígitos y un dígito verificador válido.";

    public const string EmailInvalido = "Escribí un email con formato válido.";

    /// <summary><paramref name="campo"/> llega en minúscula: "Completá el domicilio para poder guardar."</summary>
    public static string ObligatorioVacio(string campo) => $"Completá {campo} para poder guardar.";

    public const string EmpresaEmisoraGuardada = "Los datos de la empresa emisora quedaron guardados.";

    public const string SinLogo =
        "Todavía no hay un logo cargado. Es opcional: las facturas se emiten igual.";

    public const string AyudaDelLogo = "JPG o PNG, hasta 10 MB.";

    public const string LogoNoAdmitido =
        "Ese archivo no es una imagen JPG ni PNG. La configuración quedó sin cambios.";

    public const string ArchivoNoGuardado =
        "No pudimos guardar el archivo. La configuración quedó sin cambios; volvé a intentar.";

    // ── Emisión ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nombra <b>qué</b> falta configurar, no sólo que falta algo: saber que la empresa está
    /// incompleta sin saber qué campo no ayuda a resolverlo (FR-006, precedente [004]).
    /// </summary>
    public static string EmpresaEmisoraIncompleta(IEnumerable<string> faltantes) =>
        $"Falta configurar la empresa emisora: {string.Join(", ", faltantes)}. Cargalos en Empresa " +
        "emisora para poder emitir.";

    public const string ClienteInexistente = "Elegí un cliente activo.";

    public static string ClienteInactivo(string razonSocial) =>
        $"{razonSocial} está dado de baja en el padrón. Dalo de alta de nuevo para poder facturarle.";

    public static string ClienteSinDomicilio(string razonSocial) =>
        $"A {razonSocial} le falta el domicilio, que sale impreso en la factura. Cargalo en el padrón " +
        "de clientes del Módulo de viajes y volvé a intentar.";

    public static string ViajeSinRemito(int numeroDeViaje) =>
        $"El viaje N° {numeroDeViaje} no tiene número de remito y el remito sale impreso en el " +
        "detalle. No se puede facturar.";

    /// <summary>Nombra la factura que ya usa el número: sin eso, no se sabe qué otro número elegir.</summary>
    public static string NumeroDuplicado(string numero, string fecha, string cliente) =>
        $"El número {numero} ya lo usa la factura del {fecha} de {cliente}. Cargá otro número.";

    public const string NumeroInvalido = "El número tiene que tener el formato 0000-00000000.";

    public const string SinViajesSeleccionados = "Elegí al menos un viaje para facturar.";

    public const string RefacturacionSinReemplazada =
        "Elegí qué factura anulada reemplaza esta Refacturación.";

    public const string OriginalConReemplazada =
        "Una factura Original no reemplaza a ninguna otra. Elegí Refacturación o quitá la referencia.";

    public const string VencimientoPagoAnterior =
        "El vencimiento de pago no puede ser anterior a la fecha de facturación.";

    public const string CaeVencimientoAnterior =
        "El vencimiento del CAE no puede ser anterior a la fecha de facturación.";

    public static string ViajeYaFacturado(int numeroDeViaje, string numeroDeFactura) =>
        $"El viaje N° {numeroDeViaje} ya fue facturado en el comprobante {numeroDeFactura}. " +
        "Actualizá la lista y volvé a intentar.";

    public static string AnuladaYaReemplazada(string anulada, string refacturacion) =>
        $"La factura {anulada} ya fue reemplazada por la Refacturación {refacturacion}. Elegí otra.";

    // ── Las dos confirmaciones previas de FR-032 ────────────────────────────────────────────────
    // Llegan como 409 **sin haber creado nada**. Se reintenta con `confirmado: true`.

    public static string ConfirmarViajeEnCero(int numeroDeViaje) =>
        $"El viaje N° {numeroDeViaje} tiene importe $ 0,00 y no suma al neto. Una vez emitida, la " +
        "factura no cambia de importes: sólo se corrige anulándola.";

    public static string ConfirmarFechaAnteriorAViaje(
        int numeroDeViaje,
        string fechaDelViaje,
        string fechaDeFacturacion) =>
        $"El viaje N° {numeroDeViaje} es del {fechaDelViaje}, posterior a la fecha de facturación " +
        $"{fechaDeFacturacion}. Suele indicar un error de carga de fechas.";

    public static string FacturaEmitida(string numero, int cantidadDeViajes) =>
        $"Se emitió la factura {numero}. Sus {cantidadDeViajes} viajes quedaron en estado facturado.";

    // ── Corrección ──────────────────────────────────────────────────────────────────────────────
    public const string CaeRequerido = "Una factura emitida no puede quedarse sin CAE.";

    public const string CaeVencimientoRequerido =
        "Una factura emitida no puede quedarse sin vencimiento del CAE.";

    public const string FacturaAnuladaInmutable = "Una factura anulada no se puede corregir.";

    public const string FacturaCorregida =
        "Se guardaron los cambios y se regeneró el documento de la factura.";

    // ── Cobro ───────────────────────────────────────────────────────────────────────────────────
    public const string FechaCobroAnterior =
        "La fecha de cobro no puede ser anterior a la fecha de facturación.";

    public static string TransicionNoPermitida(string numero, string estadoActual) =>
        $"La factura {numero} está {estadoActual} y no admite ese cambio de estado.";

    // ── Anulación ───────────────────────────────────────────────────────────────────────────────
    public const string MotivoRequerido = "Escribí el motivo de la anulación.";

    /// <summary>Sin ofrecer ni sugerir revertir el cobro: no existe ninguna acción que lo haga (FR-043a).</summary>
    public static string FacturaCobrada(string numero, string fechaCobro) =>
        $"La factura {numero} está cobrada desde el {fechaCobro} y no se puede anular.";

    // ── Reportes ────────────────────────────────────────────────────────────────────────────────
    public const string RangoDeFechasRequerido = "Elegí un rango de fechas para ver los totales.";

    // ── Módulo 5: el único cambio de comportamiento sobre una operación existente (FR-055a) ─────
    public const string RemitoRequerido =
        "Cargá el número de remito antes de rendir el viaje: sale impreso en el detalle de la factura.";

    // ── Textos del documento generado (FR-031) ──────────────────────────────────────────────────

    /// <summary>Va impresa en el documento, no se estampa al servirlo (FR-031c, FR-031d).</summary>
    public const string LeyendaNoEsComprobanteFiscal =
        "Documento no válido como comprobante fiscal. La validez la da el CAE.";

    public const string LeyendaAnulada = "FACTURA ANULADA";
}

/// <summary>
/// Rechazo por el estado de algo compartido o que cambió, con todo lo que hace falta para resolverlo
/// <b>en el cuerpo además de en el mensaje</b> (precedente [004]).
///
/// Saber que hay un conflicto sin saber con qué no ayuda a resolverlo: la pantalla no debería tener
/// que extraer el número de la factura en conflicto del texto del mensaje.
/// </summary>
public record ErrorDeBloqueoFactura(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    /// <summary>
    /// La factura que ya usa el número, la que ya incluye el viaje, o la Refacturación que ya
    /// reemplaza a la anulada.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FacturaResumen? FacturaEnConflicto { get; init; }

    /// <summary>Los viajes que producen el rechazo, nombrados uno por uno.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ViajeEnConflicto>? Viajes { get; init; }

    /// <summary>Qué hay que confirmar (FR-032), en camelCase igual que el resto de los enums.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MotivoConfirmacion { get; init; }

    /// <summary>Desde cuándo está cobrada, en el rechazo de anular una <c>pagada</c> (FR-043a).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FechaCobro { get; init; }
}

/// <summary>
/// Rechazo por datos faltantes que enumera <b>cuáles</b> faltan (FR-006, FR-011a).
/// </summary>
public record ErrorConFaltantes(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Faltantes { get; init; }
}
