using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Traducción de las enumeraciones del módulo al JSON, en <b>camelCase</b> (convención [003]), y al
/// español de las oraciones que arma el servidor.
///
/// La declaración del filtro —"Mostrando sólo los adelantos pendientes"— la arma la pantalla, como en los
/// listados de facturas y liquidaciones.
/// </summary>
public static class NombresDeEstadoAdelanto
{
    public static string EnJson(EstadoAdelanto estado) => estado switch
    {
        EstadoAdelanto.Pendiente => "pendiente",
        EstadoAdelanto.Aprobado => "aprobado",
        EstadoAdelanto.Rechazado => "rechazado",
        EstadoAdelanto.Anulado => "anulado",
        _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
    };

    /// <summary>
    /// Lee el filtro de estado de la query. <b>Tolerante</b>: un valor desconocido devuelve <c>null</c> y el
    /// filtro se ignora, así el listado responde su vista por defecto, todos incluidos.
    /// </summary>
    public static EstadoAdelanto? LeerEstado(string? valor) => valor switch
    {
        "pendiente" => EstadoAdelanto.Pendiente,
        "aprobado" => EstadoAdelanto.Aprobado,
        "rechazado" => EstadoAdelanto.Rechazado,
        "anulado" => EstadoAdelanto.Anulado,
        _ => null,
    };

    public static string EnJson(OperacionDeAdelanto operacion) => operacion switch
    {
        OperacionDeAdelanto.Registro => "registro",
        OperacionDeAdelanto.Aprobacion => "aprobacion",
        OperacionDeAdelanto.Rechazo => "rechazo",
        OperacionDeAdelanto.Anulacion => "anulacion",
        _ => throw new ArgumentOutOfRangeException(nameof(operacion), operacion, null),
    };

    public static string EnJson(TipoBeneficiario tipo) => tipo switch
    {
        TipoBeneficiario.Chofer => "chofer",
        TipoBeneficiario.Empleado => "empleado",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>
    /// Lee el tipo de persona. <b>Estricto</b>, a diferencia del estado: el tipo es obligatorio, así que un
    /// valor desconocido devuelve <c>null</c> y quien llama lo rechaza como dato inválido.
    /// </summary>
    public static TipoBeneficiario? LeerTipo(string? valor) => valor switch
    {
        "chofer" => TipoBeneficiario.Chofer,
        "empleado" => TipoBeneficiario.Empleado,
        _ => null,
    };

    public static string EnJson(MotivoNoElegible motivo) => motivo switch
    {
        MotivoNoElegible.Inexistente => "inexistente",
        MotivoNoElegible.Inactiva => "inactiva",
        MotivoNoElegible.EmpresaEmisoraNoConfigurada => "empresaEmisoraNoConfigurada",
        MotivoNoElegible.TipoDistinto => "tipoDistinto",
        MotivoNoElegible.ChoferExterno => "choferExterno",
        _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, null),
    };

    /// <summary>Para las oraciones: <c>Este adelanto ya está aprobado</c>.</summary>
    public static string EnOracion(EstadoAdelanto estado) => EnJson(estado);

    /// <summary>Para las oraciones: <c>ya no figura como chofer</c>.</summary>
    public static string EnOracion(TipoBeneficiario tipo) => EnJson(tipo);

    /// <summary>Para mostrar como dato: <c>Chofer</c>, <c>Empleado</c>.</summary>
    public static string EnTexto(TipoBeneficiario tipo) => tipo switch
    {
        TipoBeneficiario.Chofer => "Chofer",
        _ => "Empleado",
    };
}
