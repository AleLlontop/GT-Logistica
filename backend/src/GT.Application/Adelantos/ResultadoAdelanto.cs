using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Los rechazos posibles, uno por código de <see cref="CodigosErrorAdelantos"/>. Los de arriba salen como
/// <c>400</c> y los de abajo de la línea como <c>409</c>: <c>RespuestasDeAdelanto</c> hace esa traducción
/// en un solo lugar.
/// </summary>
public enum ErrorAdelanto
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    FechaFueraDeRango,
    EmpresaEmisoraNoConfigurada,
    BeneficiarioNoElegible,
    MotivoRequerido,
    RangoInvalido,

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    AdelantoNoResoluble,
    AdelantoNoAnulable,
    RechazoRequiereConfirmacion,
    AnulacionRequiereConfirmacion,
}

/// <summary>
/// Lo que devuelve cualquier escritura del módulo: el detalle releído, o el rechazo con todo lo que su
/// cuerpo de error necesita.
/// </summary>
/// <param name="Motivo">Con <see cref="ErrorAdelanto.BeneficiarioNoElegible"/>.</param>
/// <param name="Estado">Con <c>adelanto_no_resoluble</c> y <c>adelanto_no_anulable</c>: el estado en que está.</param>
/// <param name="Desde">Con <c>fecha_fuera_de_rango</c>: el primer día del mes anterior.</param>
/// <param name="Hasta">Con <c>fecha_fuera_de_rango</c>: el día en curso.</param>
public record ResultadoAdelanto(
    ErrorAdelanto Error,
    AdelantoDetalle? Adelanto = null,
    string? Campo = null,
    string? Mensaje = null,
    MotivoNoElegible? Motivo = null,
    EstadoAdelanto? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null)
{
    public bool Exitoso => Error is ErrorAdelanto.Ninguno;

    public static ResultadoAdelanto Exito(AdelantoDetalle detalle) => new(ErrorAdelanto.Ninguno, detalle);

    public static ResultadoAdelanto Rechazo(ErrorAdelanto error, string mensaje, string? campo = null) =>
        new(error, Campo: campo, Mensaje: mensaje);

    public static ResultadoAdelanto NoEncontrado() =>
        Rechazo(ErrorAdelanto.NoEncontrado, MensajesAdelantos.NoEncontrado);

    public static ResultadoAdelanto NoResoluble(EstadoAdelanto estado) =>
        new(ErrorAdelanto.AdelantoNoResoluble, Mensaje: MensajesAdelantos.NoResoluble(estado), Estado: estado);

    public static ResultadoAdelanto NoAnulable(EstadoAdelanto estado) =>
        new(ErrorAdelanto.AdelantoNoAnulable, Mensaje: MensajesAdelantos.NoAnulable(estado), Estado: estado);
}

/// <summary>El listado, o el rechazo del rango invertido, que no llega a consultar (FR-015).</summary>
public record ResultadoConsultaAdelantos(PaginaDeAdelantos? Pagina, ResultadoAdelanto? Rechazo);
