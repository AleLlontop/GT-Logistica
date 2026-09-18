using GT.Domain.Caja;

namespace GT.Application.Caja;

using CajaEntidad = GT.Domain.Caja.Caja;

/// <summary>
/// Abre una caja con su saldo inicial, con el usuario en sesión como responsable (FR-001 a FR-005).
///
/// La consulta previa da el mensaje de CA2 y el índice único filtrado cierra la carrera del doble clic
/// (research §1): las dos terminan en el <b>mismo</b> rechazo.
/// </summary>
public class AbrirCaja(IRepositorioCaja cajas, ConsultarDetalleCaja detalles, TimeProvider reloj)
{
    public async Task<ResultadoCaja> EjecutarAsync(
        AbrirCajaRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (peticion?.SaldoInicial is not { } saldoInicial)
        {
            return ResultadoCaja.Invalido(MensajesCaja.SaldoInicialRequerido, "saldoInicial");
        }

        if (saldoInicial < 0m)
        {
            return ResultadoCaja.Invalido(MensajesCaja.SaldoInicialNegativo, "saldoInicial");
        }

        if (!ReglasDeCaja.SaldoInicialValido(saldoInicial))
        {
            return ResultadoCaja.Invalido(MensajesCaja.ImporteMalEscrito, "saldoInicial");
        }

        if (await cajas.ExisteCajaAbiertaAsync(usuarioId, cancelacion))
        {
            return YaAbierta();
        }

        int id;

        try
        {
            id = await cajas.AbrirAsync(
                new CajaEntidad
                {
                    SaldoInicial = saldoInicial,
                    FechaApertura = reloj.GetUtcNow().UtcDateTime,
                    UsuarioResponsableId = usuarioId,
                },
                cancelacion);
        }
        catch (CajaYaAbiertaException)
        {
            return YaAbierta();
        }

        return ResultadoCaja.Exito((await detalles.EjecutarAsync(id, usuarioId, cancelacion))!);
    }

    private static ResultadoCaja YaAbierta() =>
        ResultadoCaja.Rechazo(ErrorCaja.CajaYaAbierta, MensajesCaja.CajaYaAbierta);
}
