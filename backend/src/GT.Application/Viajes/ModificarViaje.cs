using GT.Application.Viajes.Clientes;
using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Corrección de los datos de un viaje (FR-017, FR-018, FR-022a).
///
/// <b>El cuerpo no acepta número, estado, chofer ni vehículo</b> —no están en
/// <see cref="ViajeRequest"/>—, así que corregir un destino no puede avanzar el viaje ni cambiar quién
/// lo maneja, y la asignación de un viaje editado queda intacta (FR-011, FR-019a, FR-034).
///
/// <b>Cambiar la fecha revalida la asignación contra la fecha nueva</b> (FR-022a). No es una
/// validación más: es lo que hace que SC-004 sea cierto. Sin ella hay dos reglas —una para asignar y
/// otra para editar— y un agujero entre las dos por el que un viaje queda guardado con documentación
/// vencida a su propia fecha. Cuando el rechazo cae, aborta <b>el <c>PUT</c> entero</b>: no se guarda
/// la fecha ni ninguno de los otros campos del mismo cuerpo.
/// </summary>
public class ModificarViaje(
    IRepositorioViajes viajes,
    IRepositorioClientes clientes,
    TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        int id,
        ViajeRequest peticion,
        CancellationToken cancelacion = default)
    {
        var viaje = await viajes.ObtenerParaModificarAsync(id, cancelacion);

        if (viaje is null)
        {
            return new ResultadoViaje(ErrorViaje.NoEncontrado);
        }

        // Uno de los cinco caminos de escritura que consultan el estado antes de tocar nada (FR-018).
        if (EstadoTerminal.Rechazo(viaje) is { } terminal)
        {
            return terminal;
        }

        if (ValidadorViaje.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoViaje(invalido.Error, Campo: invalido.Campo);
        }

        var cliente = await clientes.ObtenerPorIdAsync(peticion.ClienteId!.Value, cancelacion);

        if (cliente is null || !cliente.Activo)
        {
            return new ResultadoViaje(ErrorViaje.ClienteInexistente, Campo: "clienteId");
        }

        var remito = Normalizar(peticion.NumeroRemito);

        if (remito is not null &&
            await viajes.ObtenerPorRemitoAsync(remito, id, cancelacion) is { } dueño)
        {
            return new ResultadoViaje(
                ErrorViaje.RemitoDuplicado,
                Campo: "numeroRemito",
                NumeroDeViajeRelacionado: dueño.Numero);
        }

        var fechaNueva = peticion.Fecha!.Value;

        // FR-022a. Se evalúa **antes** de escribir un solo campo: el rechazo tiene que dejar el viaje
        // exactamente como estaba.
        if (fechaNueva != viaje.Fecha &&
            BloqueoALaFechaNueva(viaje, fechaNueva) is { } bloqueo)
        {
            return bloqueo;
        }

        viaje.ClienteId = cliente.Id;
        viaje.Fecha = fechaNueva;
        viaje.Origen = peticion.Origen!.Trim();
        viaje.Destino = peticion.Destino!.Trim();
        viaje.NumeroRemito = remito;
        viaje.DetalleCarga = Normalizar(peticion.DetalleCarga);
        viaje.Importe = peticion.Importe ?? 0m;

        try
        {
            await viajes.GuardarCambiosAsync(cancelacion);
        }
        catch (RemitoDuplicadoException)
        {
            var otro = remito is null
                ? null
                : await viajes.ObtenerPorRemitoAsync(remito, id, cancelacion);

            return new ResultadoViaje(
                ErrorViaje.RemitoDuplicado,
                Campo: "numeroRemito",
                NumeroDeViajeRelacionado: otro?.Numero);
        }

        viaje.Cliente = cliente;

        var momento = MomentoDeLectura.Desde(reloj);
        var ficha = await viajes.ObtenerFichaAsync(id, cancelacion);

        return new ResultadoViaje(
            ErrorViaje.Ninguno,
            ViajeDetalle.Desde(ficha ?? viaje, momento),
            AdvertenciasDeViaje.Al(viaje, momento),
            NumeroDelViaje: viaje.Numero);
    }

    /// <summary>
    /// El rechazo si mover el viaje a la fecha nueva dejaría bloqueada su asignación, o <c>null</c> si
    /// no hay problema —incluido el caso normal de un viaje todavía sin asignar—.
    ///
    /// Reutiliza el mismo evaluador que la asignación, con otra fecha: la regla es una sola, escrita
    /// en un solo lugar (FR-022a, SC-004).
    /// </summary>
    private static ResultadoViaje? BloqueoALaFechaNueva(Viaje viaje, DateOnly fechaNueva)
    {
        if (viaje.Chofer is { } chofer)
        {
            var veredicto = EvaluadorHabilitacion.ParaChofer(chofer.Documentacion, fechaNueva);

            if (veredicto.Bloquea)
            {
                return RechazoPorFecha(
                    fechaNueva,
                    chofer.Persona?.NombreCompleto ?? $"Chofer {chofer.Id}",
                    veredicto.DocumentoQueDecide!);
            }
        }

        if (viaje.Vehiculo is { } vehiculo)
        {
            var veredicto = EvaluadorHabilitacion.ParaVehiculo(vehiculo.Documentacion, fechaNueva);

            if (veredicto.Bloquea)
            {
                return RechazoPorFecha(fechaNueva, vehiculo.Patente, veredicto.DocumentoQueDecide!);
            }
        }

        return null;
    }

    private static ResultadoViaje RechazoPorFecha(
        DateOnly fechaNueva,
        string unidad,
        DocumentoEvaluado documento) =>
        new(
            ErrorViaje.FechaBloqueaAsignacion,
            Campo: "fecha",
            Unidad: unidad,
            Documento: documento.Tipo,
            NumeroDocumento: documento.Numero,
            FechaDeReferencia: FormatoDeFecha.Corta(fechaNueva));

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// El rechazo de los <b>cinco caminos de escritura</b> sobre un viaje que ya está cerrado: editar,
/// asignar, poner en curso, rendir y anular (FR-018, SC-013).
///
/// Vale para <b>todos los roles</b>, incluido el Administrador del sistema: no hay camino de
/// corrección en esta versión, y eso simplifica el módulo entero.
/// </summary>
public static class EstadoTerminal
{
    public static ResultadoViaje? Rechazo(Viaje viaje) => viaje.Estado switch
    {
        EstadoViaje.Rendido => new ResultadoViaje(
            ErrorViaje.ViajeRendidoInmutable,
            NumeroDelViaje: viaje.Numero),

        EstadoViaje.Anulado => new ResultadoViaje(
            ErrorViaje.ViajeAnuladoInmutable,
            NumeroDelViaje: viaje.Numero),

        _ => null,
    };
}

/// <summary>
/// Cómo se escribe una fecha <b>dentro de un mensaje</b>: <c>10/08/2026</c>, como se lee en Argentina
/// (Principio II). En el JSON viaja siempre en <c>yyyy-MM-dd</c>; esto es sólo para los textos que el
/// backend arma.
/// </summary>
public static class FormatoDeFecha
{
    public static string Corta(DateOnly fecha) => fecha.ToString("dd/MM/yyyy");
}
