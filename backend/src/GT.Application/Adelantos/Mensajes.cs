using GT.Application.Facturacion;
using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Códigos de error del módulo, exactamente los de <c>contracts/adelantos-api.yaml</c>. El frontend decide
/// con el código y muestra el mensaje tal cual.
///
/// <b>La regla de códigos HTTP</b> (research §6, convención [005]): <c>400</c> cuando el problema está en
/// lo que se tipeó o se eligió; <c>409</c> cuando está en el estado del adelanto, y en las dos
/// confirmaciones pendientes.
/// </summary>
public static class CodigosErrorAdelantos
{
    public const string DatosInvalidos = "datos_invalidos";
    public const string FechaFueraDeRango = "fecha_fuera_de_rango";
    public const string EmpresaEmisoraNoConfigurada = "empresa_emisora_no_configurada";

    /// <summary><c>400</c> aunque la causa sea una baja de otro usuario: se corrige eligiendo otra persona (research §6).</summary>
    public const string BeneficiarioNoElegible = "beneficiario_no_elegible";

    public const string MotivoRequerido = "motivo_requerido";
    public const string RangoInvalido = "rango_invalido";
    public const string AdelantoNoEncontrado = "adelanto_no_encontrado";

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    public const string AdelantoNoResoluble = "adelanto_no_resoluble";
    public const string AdelantoNoAnulable = "adelanto_no_anulable";
    public const string RechazoRequiereConfirmacion = "rechazo_requiere_confirmacion";
    public const string AnulacionRequiereConfirmacion = "anulacion_requiere_confirmacion";
}

/// <summary>
/// Textos que se muestran tal cual, en español rioplatense con voseo (Principio II). Son los que fija
/// <c>contracts/README.md</c>, con la persona como <c>Apellido, Nombre</c>, los importes en pesos y las
/// fechas <c>dd/MM/yyyy</c>.
/// </summary>
public static class MensajesAdelantos
{
    public const string DatosInvalidos = "Revisá los campos marcados.";

    public const string NoEncontrado = "No encontramos ese adelanto.";

    // ── Registro ────────────────────────────────────────────────────────────────────────────────
    public const string TipoRequerido = "Elegí si el adelanto es para un chofer o un empleado.";

    public const string PersonaRequerida = "Elegí la persona que recibe el adelanto.";

    public const string FechaRequerida = "Elegí la fecha en que se otorgó el adelanto.";

    public static string FechaFueraDeRango(DateOnly desde) =>
        $"La fecha tiene que estar entre el {FormatoDeDocumento.Fecha(desde)} y hoy.";

    public const string MotivoDelAdelantoRequerido = "Escribí el motivo del adelanto. Ejemplo: gastos médicos";

    public const string MotivoDelAdelantoDemasiadoLargo = "El motivo puede tener hasta 200 caracteres.";

    public const string ImporteRequerido = "Escribí un importe mayor que cero. Ejemplo: 150000,00";

    public const string ImporteMalEscrito = "Escribí el importe con hasta dos decimales. Ejemplo: 150000,50";

    public const string EmpresaEmisoraNoConfigurada =
        "No se pueden elegir choferes todavía. Falta configurar la empresa emisora: sin su CUIT el sistema " +
        "no distingue a los choferes de G&T Logística de los de transportistas externos. Configurala en " +
        "Empresa emisora y volvé. Para un empleado, elegí el tipo Empleado.";

    /// <param name="persona"><c>Apellido, Nombre</c>, o <c>null</c> si no existe.</param>
    public static string NoElegible(MotivoNoElegible motivo, string? persona, TipoBeneficiario tipo) => motivo switch
    {
        MotivoNoElegible.Inactiva =>
            $"{persona} se dio de baja y ya no puede recibir adelantos. Elegí otra persona.",
        MotivoNoElegible.TipoDistinto =>
            $"{persona} ya no figura como {NombresDeEstadoAdelanto.EnOracion(tipo)}. Elegí el tipo de persona " +
            "otra vez para ver a quiénes se puede elegir.",
        MotivoNoElegible.ChoferExterno =>
            $"{persona} maneja para un transportista externo, y los adelantos son sólo para choferes de " +
            "G&T Logística. Elegí otra persona.",
        MotivoNoElegible.EmpresaEmisoraNoConfigurada => EmpresaEmisoraNoConfigurada,
        _ => "La persona elegida ya no está en el padrón. Elegí otra.",
    };

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────
    public const string RangoInvalido = "La fecha desde es posterior a la hasta. Corregí una de las dos.";

    // ── Aprobación, rechazo y anulación ─────────────────────────────────────────────────────────
    public const string MotivoDelRechazoRequerido = "Escribí el motivo del rechazo: queda en el historial.";

    public const string MotivoDeLaAnulacionRequerido = "Escribí el motivo de la anulación: queda en el historial.";

    public const string MotivoDeCierreDemasiadoLargo = "El motivo puede tener hasta 500 caracteres.";

    public static string NoResoluble(EstadoAdelanto estado) =>
        $"Este adelanto ya está {NombresDeEstadoAdelanto.EnOracion(estado)}: no se puede aprobar ni rechazar.";

    public static string NoAnulable(EstadoAdelanto estado) => estado switch
    {
        EstadoAdelanto.Pendiente =>
            "Este adelanto está pendiente: sólo se anula un adelanto aprobado. Si está mal cargado, rechazalo.",
        EstadoAdelanto.Rechazado => "Este adelanto está rechazado: no se puede anular.",
        _ => "Este adelanto ya está anulado.",
    };

    public const string RechazoRequiereConfirmacion =
        "El adelanto queda rechazado y no se vuelve a presentar. Si hace falta, se registra uno nuevo. No se " +
        "puede deshacer.";

    public static string AnulacionRequiereConfirmacion(decimal importe, string persona) =>
        $"El adelanto de {FormatoDeDocumento.Pesos(importe)} para {persona} queda anulado y deja de sumar en " +
        "el total adelantado. No se puede deshacer.";
}
