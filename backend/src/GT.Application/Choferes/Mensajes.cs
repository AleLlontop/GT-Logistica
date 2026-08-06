namespace GT.Application.Choferes;

/// <summary>
/// Códigos de error del módulo. El frontend decide con el código y muestra el mensaje tal cual, sin
/// interpretarlo. Son exactamente los que fija <c>contracts/choferes-api.yaml</c>.
/// </summary>
public static class CodigosErrorChoferes
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string CuitDuplicado = "cuit_duplicado";
    public const string CuilDuplicado = "cuil_duplicado";
    public const string DniDuplicado = "dni_duplicado";
    public const string TransportistaInexistente = "transportista_inexistente";
    public const string TransportistaConChoferes = "transportista_con_choferes";
    public const string MenorDeEdad = "menor_de_edad";
    public const string VencimientoAnteriorAEmision = "vencimiento_anterior_a_emision";
    public const string TipoDuplicado = "tipo_duplicado";
    public const string TipoInexistente = "tipo_inexistente";
    public const string TipoConDocumentos = "tipo_con_documentos";
    public const string ArchivoNoAdmitido = "archivo_no_admitido";
    public const string ArchivoNoGuardado = "archivo_no_guardado";
    public const string NoEncontrado = "no_encontrado";
}

/// <summary>
/// Textos que se muestran tal cual al usuario, en español rioplatense con voseo (Principio II).
///
/// Son exactamente los que fija <c>contracts/README.md</c>. Ninguno expone detalles técnicos,
/// códigos de error ni nombres de campos internos.
/// </summary>
public static class MensajesChoferes
{
    // ── Errores ────────────────────────────────────────────────────────────────────────────────
    public const string DatosInvalidos = "Revisá los campos marcados en rojo.";

    public const string CuitDuplicado = "Ese CUIT ya está registrado para otro transportista.";

    public const string CuilDuplicado = "Ese CUIL ya está registrado para otro chofer.";

    public const string DniDuplicado = "Esa persona ya está registrada como chofer.";

    public const string TransportistaInexistente =
        "El transportista seleccionado ya no está disponible. Actualizá la lista y volvé a elegir.";

    public const string MenorDeEdad = "Un chofer tiene que ser mayor de 18 años.";

    public const string VencimientoAnteriorAEmision =
        "La fecha de vencimiento tiene que ser posterior a la de emisión.";

    public const string TipoDuplicado = "Ya existe un tipo de documentación con ese nombre.";

    public const string TipoInexistente =
        "El tipo de documentación seleccionado ya no está disponible. Actualizá la lista y volvé a " +
        "elegir.";

    public const string ArchivoNoAdmitido =
        "El archivo tiene que ser un PDF, JPG o PNG de hasta 10 MB.";

    /// <summary>
    /// FR-015e: la carga es todo o nada. El mensaje dice las dos cosas que le importan a quien
    /// opera —que no se guardó nada y que no tiene que volver a tipear—.
    /// </summary>
    public const string ArchivoNoGuardado =
        "No pudimos guardar el archivo, así que no se guardó nada. Volvé a intentar; los datos que " +
        "cargaste se conservan.";

    public const string NoEncontrado =
        "Ese registro ya no existe. Puede que lo hayan eliminado desde otra sesión.";

    /// <summary>Dice cuántos choferes activos impiden la baja, para que se sepa qué resolver (FR-010).</summary>
    public static string TransportistaConChoferes(int cantidad) =>
        $"No se puede dar de baja: tiene {cantidad} chofer(es) activo(s). Reasignalos o dalos de " +
        "baja primero.";

    /// <summary>Dice cuántos documentos usan el tipo que se quiso dar de baja (FR-014).</summary>
    public static string TipoConDocumentos(int cantidad) =>
        $"No se puede dar de baja: hay {cantidad} documento(s) de ese tipo cargados.";

    // ── Confirmaciones de operación ────────────────────────────────────────────────────────────
    public const string CambiosGuardados = "Los cambios se guardaron correctamente.";

    public const string DocumentoCargado = "El documento se cargó correctamente.";

    public static string TransportistaRegistrado(string nombre) =>
        $"El transportista {nombre} se registró correctamente.";

    public static string ChoferRegistrado(string apellido, string nombre) =>
        $"El chofer {apellido}, {nombre} se registró correctamente.";

    /// <summary>Avisa que se reutilizó una persona ya cargada en vez de duplicar el padrón (FR-006).</summary>
    public static string ChoferRegistradoReutilizandoPersona(string apellido, string nombre) =>
        $"El chofer {apellido}, {nombre} se registró correctamente, reutilizando la persona que ya " +
        "estaba en el padrón.";

    public static string ChoferReasignado(string apellido, string nombre, string transportista) =>
        $"{apellido}, {nombre} ahora pertenece a {transportista}. Su documentación se conservó.";

    public static string ChoferDadoDeBaja(string apellido, string nombre) =>
        $"{apellido}, {nombre} quedó inactivo.";

    public static string QuedoInactivo(string nombre) => $"{nombre} quedó inactivo.";

    // ── Estados vacíos ─────────────────────────────────────────────────────────────────────────
    public const string SinTransportistas =
        "Todavía no hay transportistas cargados. Registrá el primero para poder asignarle choferes.";

    public const string SinCoincidenciasTransportistas =
        "No hay transportistas que coincidan con la búsqueda.";

    public const string SinChoferes = "Todavía no hay choferes registrados.";

    public const string SinCoincidenciasChoferes =
        "No hay choferes que coincidan con los filtros aplicados.";

    public const string ChoferSinDocumentacion = "Este chofer todavía no tiene documentación cargada.";

    public const string SinVencimientos = "No hay documentación próxima a vencer ni vencida.";

    public const string SinTiposDocumentacion =
        "Todavía no hay tipos de documentación. Cargá el primero para poder registrar documentos.";

    public const string SinTransportistasActivos =
        "No hay transportistas activos. Registrá uno desde la pantalla Transportistas.";

    public const string SinTiposActivos =
        "No hay tipos de documentación activos. Cargá uno desde la pantalla Tipos de documentación.";
}
