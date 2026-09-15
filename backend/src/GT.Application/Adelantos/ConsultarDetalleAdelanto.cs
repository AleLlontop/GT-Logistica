using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Detalle de un adelanto (FR-019, FR-020, FR-036). <b>Es también la relectura de las cuatro
/// escrituras</b>: la entidad con la que se escribió no refleja lo que hicieron los <c>UPDATE</c>
/// condicionales (convención [006]).
/// </summary>
public class ConsultarDetalleAdelanto(IRepositorioAdelantos adelantos)
{
    public async Task<AdelantoDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var adelanto = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        return adelanto is null ? null : Armar(adelanto);
    }

    public static PersonaResumen PersonaDe(Adelanto adelanto) =>
        new(adelanto.PersonaId, adelanto.Persona!.Apellido, adelanto.Persona.Nombre, adelanto.Persona.Dni);

    public static AdelantoDetalle Armar(Adelanto adelanto) =>
        new(
            adelanto.Id,
            adelanto.Fecha.ToString("yyyy-MM-dd"),

            // Del padrón vigente, no copiado: un apellido corregido después se ve acá (FR-020).
            PersonaDe(adelanto),

            // El guardado al registrar, aunque la persona haya dejado de ser chofer o empleado (FR-020).
            NombresDeEstadoAdelanto.EnJson(adelanto.TipoBeneficiario),
            adelanto.Motivo,
            adelanto.Importe,
            NombresDeEstadoAdelanto.EnJson(adelanto.Estado),
            adelanto.MotivoRechazo,
            adelanto.MotivoAnulacion,

            // De la más vieja a la más nueva. El motivo se lee de la columna: el historial no lo copia.
            [.. adelanto.Cambios
                .OrderBy(cambio => cambio.OcurridoEn)
                .ThenBy(cambio => cambio.Id)
                .Select(cambio => new EntradaDeHistorialAdelanto(
                    NombresDeEstadoAdelanto.EnJson(cambio.Operacion),
                    cambio.Usuario?.Username ?? $"Usuario {cambio.UsuarioId}",
                    cambio.OcurridoEn,
                    cambio.Operacion switch
                    {
                        OperacionDeAdelanto.Rechazo => adelanto.MotivoRechazo,
                        OperacionDeAdelanto.Anulacion => adelanto.MotivoAnulacion,
                        _ => null,
                    }))],

            ReglasDeAdelanto.MotivoNoResoluble(adelanto.Estado) is null,
            ReglasDeAdelanto.MotivoNoAnulable(adelanto.Estado) is null);
}
