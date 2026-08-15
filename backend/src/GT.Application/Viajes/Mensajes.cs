using System.Text.Json.Serialization;
using GT.Application.Autenticacion;

namespace GT.Application.Viajes;

/// <summary>
/// Códigos de error del módulo. El frontend decide con el código y muestra el mensaje tal cual, sin
/// interpretarlo. Son exactamente los que fijan <c>contracts/viajes-api.yaml</c> y
/// <c>contracts/README.md</c>.
///
/// <b>La regla de códigos HTTP</b> (research §5): <c>400</c> cuando el problema está en lo que se
/// tipeó —campos, duplicados, dependencias—; <c>409</c> cuando está en el estado de algo que se
/// comparte o que cambió —unidad ocupada, transición no permitida, entidad inmutable, confirmación
/// pendiente—. Con eso el frontend sabe, sin leer el código del backend, si tiene que marcar un campo
/// o abrir un diálogo.
/// </summary>
public static class CodigosErrorViajes
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string NoEncontrado = "no_encontrado";

    // ── Padrón de clientes (400) ────────────────────────────────────────────────────────────────
    public const string CuitInvalido = "cuit_invalido";
    public const string CuitDuplicado = "cuit_duplicado";

    /// <summary>
    /// Distinto de <see cref="CuitDuplicado"/> a propósito (FR-007): sin esa distinción, quien
    /// intenta registrar de nuevo a un cliente que volvió recibe "ya pertenece a otro" y no lo
    /// encuentra, porque un cliente dado de baja no aparece en el listado por defecto.
    /// </summary>
    public const string CuitDeClienteDadoDeBaja = "cuit_de_cliente_dado_de_baja";

    public const string EmailInvalido = "email_invalido";
    public const string ClienteConViajes = "cliente_con_viajes";

    // ── Viajes (400) ────────────────────────────────────────────────────────────────────────────
    public const string ClienteInexistente = "cliente_inexistente";
    public const string RemitoDuplicado = "remito_duplicado";
    public const string ImporteNegativo = "importe_negativo";
    public const string MotivoRequerido = "motivo_requerido";
    public const string RangoDeFechasRequerido = "rango_de_fechas_requerido";

    /// <summary>Módulo 6, FR-055a.</summary>
    public const string RemitoRequerido = "remito_requerido";

    // ── Viajes (409): el estado cambió o es compartido ──────────────────────────────────────────
    public const string ViajeRendidoInmutable = "viaje_rendido_inmutable";
    public const string ViajeAnuladoInmutable = "viaje_anulado_inmutable";

    /// <summary>Módulo 6, FR-052.</summary>
    public const string ViajeFacturadoInmutable = "viaje_facturado_inmutable";

    public const string TransicionNoPermitida = "transicion_no_permitida";
    public const string FaltaAsignacion = "falta_asignacion";
    public const string UnidadDadaDeBaja = "unidad_dada_de_baja";
    public const string ChoferOcupado = "chofer_ocupado";
    public const string VehiculoOcupado = "vehiculo_ocupado";
    public const string RendicionRequiereConfirmacion = "rendicion_requiere_confirmacion";

    // ── Asignación ──────────────────────────────────────────────────────────────────────────────
    public const string ChoferInexistente = "chofer_inexistente";
    public const string VehiculoInexistente = "vehiculo_inexistente";
    public const string DocumentacionVencida = "documentacion_vencida";
    public const string AsignacionNoPermitida = "asignacion_no_permitida";
    public const string FechaBloqueaAsignacion = "fecha_bloquea_asignacion";

    // ── Advertencias que NO bloquean (FR-015a) ──────────────────────────────────────────────────
    // Viajan en `advertencias[]` junto con el resultado, nunca como error.
    public const string OrigenIgualADestino = "origen_igual_a_destino";
    public const string CargaRetroactiva = "carga_retroactiva";
    public const string DocumentacionProximaAvencer = "documentacion_proxima_a_vencer";
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Son exactamente los que fija <c>contracts/README.md</c>, palabra por palabra. Ninguno expone
/// detalles técnicos, códigos de error ni nombres de campos internos.
/// </summary>
public static class MensajesViajes
{
    // ── Errores comunes ─────────────────────────────────────────────────────────────────────────
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrado = "No encontramos lo que buscabas.";

    // ── Padrón de clientes ──────────────────────────────────────────────────────────────────────
    public const string CuitInvalido =
        "El CUIT tiene que tener once dígitos y un dígito verificador válido.";

    public const string CuitDuplicado = "Ese CUIT ya pertenece a otro cliente.";

    public const string CuitDeClienteDadoDeBaja =
        "Ese CUIT pertenece a un cliente dado de baja. Dalo de alta de nuevo desde el listado en vez " +
        "de registrarlo otra vez.";

    public const string EmailInvalido = "Escribí un email con formato válido.";

    /// <summary>
    /// Dice <b>cuántos</b> viajes lo impiden: saber que hay dependientes sin saber cuántos no ayuda a
    /// resolverlo (FR-006, SC-009, precedente [004]). Cuenta sólo los `pendiente` y `en curso`.
    /// </summary>
    public static string ClienteConViajes(int cantidad) =>
        $"No se puede dar de baja: {cantidad} viaje(s) pendiente(s) o en curso dependen de este cliente.";

    // ── Viajes ──────────────────────────────────────────────────────────────────────────────────
    public const string ClienteInexistente = "Elegí un cliente activo.";

    public static string RemitoDuplicado(int numeroDelViajeQueLoUsa) =>
        $"Ese número de remito ya está cargado en el viaje {numeroDelViajeQueLoUsa}.";

    public const string ImporteNegativo = "El importe no puede ser negativo.";

    public static string ViajeRendidoInmutable(int numero) =>
        $"El viaje {numero} está rendido y no se puede modificar.";

    public static string ViajeAnuladoInmutable(int numero) =>
        $"El viaje {numero} está anulado y no se puede modificar.";

    /// <summary>
    /// Módulo 6, FR-052. Dice <b>dónde</b> mirar para destrabarlo: sin la mención a la factura, quien
    /// opera sabe que no puede tocar el viaje y no sabe qué hacer al respecto.
    /// </summary>
    public static string ViajeFacturadoInmutable(int numero) =>
        $"El viaje {numero} está facturado y no se puede modificar. Anulá la factura si necesitás " +
        "corregirlo.";

    public static string TransicionNoPermitida(int numero, string estadoActual, string estadoPedido) =>
        $"No se puede pasar el viaje {numero} de {estadoActual} a {estadoPedido}.";

    public const string FaltaAsignacion =
        "Asigná chofer y vehículo antes de poner el viaje en curso.";

    public static string UnidadDadaDeBaja(string unidad) =>
        $"{unidad} está dado de baja. Reasigná el viaje antes de ponerlo en curso.";

    /// <summary>Nombra el viaje que lo ocupa (FR-026): sin eso no se sabe qué cerrar.</summary>
    public static string ChoferOcupado(string chofer, int numeroDelViajeQueLoOcupa) =>
        $"{chofer} ya está en el viaje {numeroDelViajeQueLoOcupa}. Cerralo antes de poner este en curso.";

    public static string VehiculoOcupado(string patente, int numeroDelViajeQueLoOcupa) =>
        $"{patente} ya está en el viaje {numeroDelViajeQueLoOcupa}. Cerralo antes de poner este en curso.";

    public const string RendicionRequiereConfirmacion =
        "El viaje va a quedar cerrado sin importe y después no se va a poder corregir. Confirmá para " +
        "rendirlo igual.";

    public const string MotivoRequerido = "Escribí el motivo de la anulación.";

    /// <summary>
    /// Módulo 6, FR-055a. Dice <b>por qué</b> hace falta: sin el motivo, la regla parece un requisito
    /// arbitrario que apareció de un día para el otro (contracts/README §Rendición de un viaje).
    /// </summary>
    public const string RemitoRequerido =
        "Cargá el número de remito antes de rendir el viaje: sale impreso en el detalle de la factura.";

    public const string RangoDeFechasRequerido = "Elegí un rango de fechas para ver los totales.";

    // ── Asignación ──────────────────────────────────────────────────────────────────────────────
    public const string ChoferInexistente = "Elegí un chofer activo.";

    public const string VehiculoInexistente = "Elegí un vehículo disponible.";

    /// <summary>
    /// Nombra la unidad, el tipo y número del documento, y <b>la fecha del viaje</b>: la evaluación
    /// corre contra esa fecha y no contra hoy, así que decir "está vencido" a secas confundiría a
    /// quien está cargando un viaje retroactivo (FR-022, SC-004, SC-014).
    /// </summary>
    public static string DocumentacionVencida(
        string unidad,
        string tipoDocumento,
        string numeroDocumento,
        string fechaDelViaje) =>
        $"No podés asignar {unidad}: {tipoDocumento} N° {numeroDocumento} está vencido al {fechaDelViaje}.";

    /// <summary>
    /// La observación que acompaña a una unidad en el desplegable: el mismo documento que el servidor
    /// va a nombrar al rechazarla, evaluado contra la misma fecha (FR-021, FR-022).
    ///
    /// Es corta a propósito —entra al lado de la patente en un <c>option</c>— y no repite la unidad,
    /// que ya está escrita ahí. El mensaje completo lo da el rechazo, cuando hay dónde leerlo.
    /// </summary>
    public static string ObservacionDocumentoVencido(string tipoDocumento, string fechaVencimiento) =>
        $"{tipoDocumento} vencido el {fechaVencimiento}";

    public static string AsignacionNoPermitida(int numero, string estado) =>
        $"El viaje {numero} está {estado} y no se puede reasignar.";

    public static string FechaBloqueaAsignacion(
        string fechaNueva,
        string tipoDocumento,
        string unidad) =>
        $"No se puede mover el viaje al {fechaNueva}: {tipoDocumento} de {unidad} está vencido a esa " +
        "fecha. Cambiá la unidad o elegí otra fecha.";

    // ── Advertencias que no bloquean (FR-015a) ──────────────────────────────────────────────────
    public const string OrigenIgualADestino =
        "El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien.";

    public const string CargaRetroactiva =
        "Estás cargando un viaje con fecha anterior a hoy. Queda registrado como carga retroactiva.";

    public static string DocumentacionProximaAvencer(
        string tipoDocumento,
        string unidad,
        string fechaVencimiento) =>
        $"Asignación guardada. Atención: {tipoDocumento} de {unidad} vence el {fechaVencimiento}.";

    // ── Confirmaciones de operación ────────────────────────────────────────────────────────────
    public static string ClienteRegistrado(string razonSocial) =>
        $"El cliente {razonSocial} quedó registrado y ya se puede elegir al cargar un viaje.";

    public static string ClienteModificado(string razonSocial) =>
        $"Los datos de {razonSocial} quedaron actualizados.";

    public static string ClienteDadoDeBaja(string razonSocial) =>
        $"{razonSocial} quedó dado de baja. Deja de ofrecerse al registrar viajes.";

    public static string ClienteDadoDeAlta(string razonSocial) =>
        $"{razonSocial} volvió al padrón. Se ofrece de nuevo al registrar viajes.";

    public static string ViajeRegistrado(int numero) =>
        $"El viaje {numero} quedó registrado como pendiente.";

    public static string ViajeModificado(int numero) =>
        $"Los datos del viaje {numero} quedaron actualizados.";

    public static string ViajeAsignado(int numero, string chofer, string patente) =>
        $"El viaje {numero} quedó asignado a {chofer} con {patente}.";

    public static string ViajeEnCurso(int numero) => $"El viaje {numero} está en curso.";

    public static string ViajeRendido(int numero) => $"El viaje {numero} quedó rendido.";

    public static string ViajeAnulado(int numero) => $"El viaje {numero} quedó anulado.";

    // ── Estados vacíos ─────────────────────────────────────────────────────────────────────────
    public const string PadronDeClientesVacio =
        "Todavía no hay clientes cargados. Registrá el primero para poder empezar a cargar viajes.";

    public const string SinClientesCoincidentes =
        "Ningún cliente coincide con los filtros aplicados.";

    public const string SinViajes = "Todavía no hay viajes registrados. Registrá el primero para empezar.";

    public const string SinViajesCoincidentes = "Ningún viaje coincide con los filtros aplicados.";

    public const string SinClientesActivos =
        "Todavía no hay clientes activos. Cargá al menos un cliente antes de registrar viajes.";

    public const string SinChoferesActivos =
        "Todavía no hay choferes activos. Cargá al menos uno en el módulo de Choferes.";

    public const string SinVehiculosDisponibles =
        "Todavía no hay vehículos disponibles. Revisá el módulo de Flota.";

    public const string SinViajesEnElPeriodo = "No hay viajes en el período elegido.";
}

/// <summary>
/// Rechazo de la baja de un cliente por viajes vivos (FR-006, SC-009).
///
/// La cantidad va <b>en el cuerpo además de en el mensaje</b>: es el precedente [004], y existe para
/// que la pantalla pueda usarla sin tener que leer el texto.
/// </summary>
public record ErrorConDependencias(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadViajes { get; init; }
}

/// <summary>
/// Rechazo por el estado de algo compartido o que cambió: unidad ocupada, o documentación vencida a
/// la fecha del viaje.
///
/// Los tres datos viajan en el cuerpo <b>además de</b> en el mensaje (FR-022, FR-026, SC-004): quien
/// opera necesita saber qué unidad y qué documento lo impiden, o qué viaje ocupa a la unidad, y la
/// pantalla no debería tener que extraerlo del texto.
/// </summary>
public record ErrorDeBloqueo(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    /// <summary>Número del viaje <c>en curso</c> que ocupa al chofer o al vehículo (FR-026).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ViajeQueOcupa { get; init; }

    /// <summary>Nombre del chofer o patente del vehículo cuya documentación bloquea (FR-022).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? UnidadQueBloquea { get; init; }

    /// <summary>Tipo y número del documento vencido a la fecha del viaje (FR-022, SC-004).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentoQueBloquea { get; init; }
}
