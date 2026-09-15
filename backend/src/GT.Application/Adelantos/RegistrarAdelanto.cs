using GT.Domain.Adelantos;
using GT.Domain.Choferes;

namespace GT.Application.Adelantos;

/// <summary>
/// Registra un adelanto <c>pendiente</c> (FR-001 a FR-012).
///
/// Valida en el orden de data-model §Registrar —campos presentes, fecha, motivo, importe, elegibilidad— y
/// <b>la elegibilidad es la misma función</b> que arma el desplegable, aplicada a la persona pedida:
/// también rechaza una invocación directa con una persona que la pantalla nunca ofrecería (FR-009).
/// </summary>
public class RegistrarAdelanto(
    IRepositorioAdelantos adelantos,
    ConsultarDetalleAdelanto detalles,
    TimeProvider reloj)
{
    public const int LargoMaximoDelMotivo = 200;

    public async Task<ResultadoAdelanto> EjecutarAsync(
        RegistroRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (NombresDeEstadoAdelanto.LeerTipo(peticion?.Tipo) is not { } tipo)
        {
            return Invalido(MensajesAdelantos.TipoRequerido, "tipo");
        }

        if (peticion!.PersonaId is not { } personaId)
        {
            return Invalido(MensajesAdelantos.PersonaRequerida, "personaId");
        }

        if (peticion.Fecha is not { } fecha)
        {
            return Invalido(MensajesAdelantos.FechaRequerida, "fecha");
        }

        // El día en curso de Argentina: un adelanto cargado a las 22 del 30/09 es del 30/09 (research §9.6).
        var hoy = FechaHoyArgentina.Desde(reloj.GetUtcNow());

        if (!ReglasDeAdelanto.FechaAdmitida(fecha, hoy))
        {
            var desde = ReglasDeAdelanto.PrimeraFechaAdmitida(hoy);

            return new ResultadoAdelanto(
                ErrorAdelanto.FechaFueraDeRango,
                Campo: "fecha",
                Mensaje: MensajesAdelantos.FechaFueraDeRango(desde),
                Desde: desde,
                Hasta: hoy);
        }

        // Recortado antes de medirlo y de guardarlo: sólo espacios es vacío (research §9.7).
        var motivo = peticion.Motivo?.Trim();

        if (string.IsNullOrEmpty(motivo))
        {
            return Invalido(MensajesAdelantos.MotivoDelAdelantoRequerido, "motivo");
        }

        if (motivo.Length > LargoMaximoDelMotivo)
        {
            return Invalido(MensajesAdelantos.MotivoDelAdelantoDemasiadoLargo, "motivo");
        }

        if (peticion.Importe is not { } importe || importe <= 0m)
        {
            return Invalido(MensajesAdelantos.ImporteRequerido, "importe");
        }

        if (!ReglasDeAdelanto.ImporteValido(importe))
        {
            return Invalido(MensajesAdelantos.ImporteMalEscrito, "importe");
        }

        var cuitEmisora = await adelantos.ObtenerCuitEmpresaEmisoraAsync(cancelacion);
        var persona = await adelantos.ObtenerPersonaParaAdelantoAsync(personaId, cancelacion);

        if (ElegibilidadDeBeneficiario.Evaluar(tipo, persona, cuitEmisora) is { } noElegible)
        {
            return noElegible is MotivoNoElegible.EmpresaEmisoraNoConfigurada
                ? ResultadoAdelanto.Rechazo(
                    ErrorAdelanto.EmpresaEmisoraNoConfigurada,
                    MensajesAdelantos.EmpresaEmisoraNoConfigurada,
                    "tipo")
                : new ResultadoAdelanto(
                    ErrorAdelanto.BeneficiarioNoElegible,
                    Campo: "personaId",
                    Mensaje: MensajesAdelantos.NoElegible(
                        noElegible,
                        persona is null ? null : $"{persona.Apellido}, {persona.Nombre}",
                        tipo),
                    Motivo: noElegible);
        }

        var id = await adelantos.RegistrarAsync(
            new Adelanto
            {
                PersonaId = personaId,
                TipoBeneficiario = tipo,
                Fecha = fecha,
                Motivo = motivo,
                Importe = importe,
            },
            usuarioId,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        return ResultadoAdelanto.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }

    private static ResultadoAdelanto Invalido(string mensaje, string campo) =>
        ResultadoAdelanto.Rechazo(ErrorAdelanto.DatosInvalidos, mensaje, campo);
}
