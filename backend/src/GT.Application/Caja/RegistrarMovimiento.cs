using GT.Domain.Caja;

namespace GT.Application.Caja;

/// <summary>
/// Registra un ingreso o un egreso sobre la caja abierta del usuario en sesión (FR-006 a FR-015).
///
/// Valida en orden —tipo, importe, concepto, referencia según el tipo, existencia de la orden— y recién ahí
/// escribe. Que la caja siga abierta, sea propia y la factura siga pendiente lo decide la transacción con el
/// lock tomado (research §2): la consulta previa no alcanzaría contra un cierre simultáneo.
/// </summary>
public class RegistrarMovimiento(IRepositorioCaja cajas, TimeProvider reloj)
{
    public async Task<ResultadoCaja> EjecutarAsync(
        int cajaId,
        RegistrarMovimientoRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (NombresDeEstadoCaja.LeerTipo(peticion?.Tipo) is not { } tipo)
        {
            return ResultadoCaja.Invalido(MensajesCaja.TipoRequerido, "tipo");
        }

        if (peticion!.Importe is not { } importe || importe <= 0m)
        {
            return ResultadoCaja.Invalido(MensajesCaja.ImporteRequerido, "importe");
        }

        if (!ReglasDeCaja.ImporteValido(importe))
        {
            return ResultadoCaja.Invalido(MensajesCaja.ImporteMalEscrito, "importe");
        }

        // Recortado antes de medirlo y de guardarlo: sólo espacios es vacío (FR-009).
        var concepto = peticion.Concepto?.Trim();

        if (string.IsNullOrEmpty(concepto))
        {
            return ResultadoCaja.Invalido(MensajesCaja.ConceptoRequerido, "concepto");
        }

        if (!ReglasDeCaja.ConceptoValido(concepto))
        {
            return ResultadoCaja.Invalido(MensajesCaja.ConceptoDemasiadoLargo, "concepto");
        }

        switch (ReglasDeCaja.ReferenciaAdmitida(tipo, peticion.FacturaId, peticion.OrdenDePagoId))
        {
            case ReferenciaNoAdmitida.OrdenDePagoEnIngreso:
                return Referencia(MensajesCaja.OrdenDePagoEnIngreso, "ordenDePagoId");

            case ReferenciaNoAdmitida.FacturaEnEgreso:
                return Referencia(MensajesCaja.FacturaEnEgreso, "facturaId");
        }

        // Sin tope ni estado: una orden fuera de las 50 del desplegable se acepta si existe (research §4).
        if (peticion.OrdenDePagoId is { } ordenId && !await cajas.ExisteOrdenDePagoAsync(ordenId, cancelacion))
        {
            return Referencia(MensajesCaja.OrdenDePagoInexistente, "ordenDePagoId");
        }

        var registrado = await cajas.RegistrarMovimientoAsync(
            new MovimientoDeCaja
            {
                CajaId = cajaId,
                Tipo = tipo,
                Importe = importe,
                Concepto = concepto,
                UsuarioId = usuarioId,
                Fecha = reloj.GetUtcNow().UtcDateTime,
                FacturaId = peticion.FacturaId,
                OrdenDePagoId = peticion.OrdenDePagoId,
            },
            cancelacion);

        if (registrado.NoOperable is { } motivo)
        {
            return ResultadoCaja.NoOperable(motivo);
        }

        // Una factura inexistente se rechaza igual que una cobrada: en los dos casos no está pendiente.
        if (registrado.FacturaNoPendiente)
        {
            return Referencia(MensajesCaja.FacturaNoPendiente, "facturaId");
        }

        return ResultadoCaja.Exito((await cajas.ObtenerMovimientoAsync(registrado.Id!.Value, cancelacion))!);
    }

    private static ResultadoCaja Referencia(string mensaje, string campo) =>
        ResultadoCaja.Rechazo(ErrorCaja.ReferenciaInvalida, mensaje, campo);
}
