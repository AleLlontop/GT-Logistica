using System.Text.Json.Serialization;
using GT.Application.Autenticacion;

namespace GT.Application.Flota;

/// <summary>
/// Códigos de error del módulo. El frontend decide con el código y muestra el mensaje tal cual, sin
/// interpretarlo. Son exactamente los que fijan <c>contracts/flota-api.yaml</c> y
/// <c>contracts/README.md</c>.
/// </summary>
public static class CodigosErrorFlota
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string NoEncontrado = "no_encontrado";

    // ── Padrón de flota ─────────────────────────────────────────────────────────────────────────
    public const string PatenteDuplicada = "patente_duplicada";

    /// <summary>
    /// Distinto de <see cref="PatenteDuplicada"/> a propósito (FR-008f): sin esa distinción, quien
    /// intenta recargar una unidad que volvió recibe "ya está registrada" y no la encuentra, porque
    /// no aparece en el listado por defecto.
    /// </summary>
    public const string PatenteDeVehiculoDadoDeBaja = "patente_de_vehiculo_dado_de_baja";

    public const string PatenteInvalida = "patente_invalida";
    public const string TipoVehiculoInexistente = "tipo_vehiculo_inexistente";
    public const string TransportistaInexistente = "transportista_inexistente";
    public const string DisponibleConDocumentacionVencida = "disponible_con_documentacion_vencida";
    public const string DisponibleSinDocumentacion = "disponible_sin_documentacion";
    public const string TransportistaInactivoAlReactivar = "transportista_inactivo_al_reactivar";
    public const string TipoInactivoAlReactivar = "tipo_inactivo_al_reactivar";

    // ── Catálogo de tipos de vehículo ───────────────────────────────────────────────────────────
    public const string NombreDuplicado = "nombre_duplicado";
    public const string TipoVehiculoEnUso = "tipo_vehiculo_en_uso";

    // ── Documentación ───────────────────────────────────────────────────────────────────────────
    public const string TipoInexistente = "tipo_inexistente";
    public const string VencimientoAnteriorAEmision = "vencimiento_anterior_a_emision";
    public const string ArchivoNoAdmitido = "archivo_no_admitido";
    public const string ArchivoNoGuardado = "archivo_no_guardado";
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Son exactamente los que fija <c>contracts/README.md</c>. Ninguno expone detalles técnicos, códigos
/// de error ni nombres de campos internos.
/// </summary>
public static class MensajesFlota
{
    // ── Errores ────────────────────────────────────────────────────────────────────────────────
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrado = "No encontramos lo que buscabas.";

    public const string PatenteDuplicada = "Esa patente ya está registrada en la flota.";

    public const string PatenteDeVehiculoDadoDeBaja =
        "Esa patente pertenece a una unidad dada de baja. Reactivala desde su ficha en vez de " +
        "registrarla de nuevo.";

    public const string PatenteInvalida = "La patente tiene que tener el formato ABC123 o AB123CD.";

    public const string TipoVehiculoInexistente = "Elegí un tipo de vehículo activo.";

    public const string TransportistaInexistente = "Elegí un transportista activo.";

    public const string SinTiposDeVehiculo =
        "Todavía no hay ningún tipo de vehículo cargado. Pedile al administrador que cargue al menos " +
        "uno antes de registrar unidades.";

    public const string SinTransportistas =
        "Todavía no hay ningún transportista cargado. Registrá al menos uno antes de registrar " +
        "unidades.";

    /// <summary>Nombra el documento que lo impide: sin eso, el operador no sabe qué resolver (FR-014a).</summary>
    public static string DisponibleConDocumentacionVencida(string documento) =>
        $"No podés dejar la unidad disponible: {documento} está vencido.";

    public const string DisponibleSinDocumentacion =
        "No podés dejar la unidad disponible: todavía no tiene documentación cargada.";

    /// <summary>Dice cuántos vehículos usan el tipo, para que se sepa qué resolver (FR-010, SC-008).</summary>
    public static string TipoVehiculoEnUso(int cantidad) =>
        $"No se puede dar de baja: {cantidad} vehículo(s) usan este tipo.";

    public const string NombreDuplicado = "Ya existe un tipo con ese nombre.";

    public const string VencimientoAnteriorAEmision =
        "La fecha de vencimiento tiene que ser posterior a la de emisión.";

    public const string TipoInexistente =
        "El tipo de documentación seleccionado ya no está disponible. Actualizá la lista y volvé a " +
        "elegir.";

    public const string ArchivoNoAdmitido =
        "El archivo tiene que ser PDF, JPG o PNG y pesar menos de 10 MB.";

    /// <summary>
    /// FR-029: la carga es todo o nada. El mensaje dice las dos cosas que le importan a quien opera
    /// —que no se modificó nada y que no tiene que volver a tipear—.
    /// </summary>
    public const string ArchivoNoGuardado =
        "No se pudo guardar el archivo. El documento no se modificó; volvé a intentar.";

    public const string TransportistaInactivoAlReactivar =
        "El transportista de esta unidad está dado de baja. Elegí uno activo para reactivarla.";

    public const string TipoInactivoAlReactivar =
        "El tipo de esta unidad está dado de baja. Elegí uno activo para reactivarla.";

    // ── Confirmaciones de operación ────────────────────────────────────────────────────────────
    public static string VehiculoRegistrado(string patente) =>
        $"La unidad {patente} quedó registrada en la flota.";

    public static string VehiculoModificado(string patente) =>
        $"Los datos de la unidad {patente} quedaron actualizados.";

    public static string VehiculoDadoDeBaja(string patente) =>
        $"La unidad {patente} quedó dada de baja. Su documentación se conserva.";

    public static string VehiculoReactivado(string patente) => $"La unidad {patente} volvió a la flota.";

    public static string TipoVehiculoCreado(string nombre) =>
        $"El tipo {nombre} quedó disponible para registrar vehículos.";

    public static string TipoVehiculoDadoDeBaja(string nombre) =>
        $"El tipo {nombre} quedó inactivo. Deja de ofrecerse al registrar vehículos.";

    public static string TipoVehiculoReactivado(string nombre) =>
        $"El tipo {nombre} volvió a estar activo. Se ofrece de nuevo al registrar vehículos.";

    // ── Estados vacíos ─────────────────────────────────────────────────────────────────────────
    public const string SinVehiculos =
        "Todavía no hay unidades registradas. Registrá la primera para empezar.";

    public const string SinCoincidencias = "Ningún vehículo coincide con los filtros aplicados.";

    public const string VehiculoSinDocumentacion =
        "Esta unidad todavía no tiene documentación cargada. Mientras no la tenga, no puede quedar " +
        "disponible.";

    public const string SinVencimientos = "No hay vencimientos pendientes.";

    public const string CatalogoDeTiposVacio =
        "Todavía no hay tipos de vehículo cargados. Cargá el primero para poder registrar unidades.";
}

/// <summary>
/// Rechazo por dependencias, con el número a la vista (SC-008).
///
/// Los rechazos por dependencias <b>dicen cuántas son</b>: saber que hay dependientes sin saber
/// cuántos no ayuda a resolverlo. Las tres cantidades son anulables porque cada rechazo informa las
/// suyas —el tipo de vehículo informa vehículos, el transportista informa choferes y vehículos, el
/// tipo de documentación informa documentos— y las que no corresponden no viajan en el JSON.
/// </summary>
public record ErrorConDependencias(string Codigo, string Mensaje, string? Campo = null)
    : ErrorResponse(Codigo, Mensaje, Campo)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadVehiculos { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadChoferes { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CantidadDocumentos { get; init; }
}
