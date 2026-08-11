using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Cuerpo de la asignación. <b>Los dos campos son obligatorios</b>: no hay asignación parcial, así
/// que un viaje tiene chofer y vehículo o no tiene ninguno de los dos (FR-019b).
/// </summary>
public record AsignacionRequest(int? ChoferId, int? VehiculoId);

/// <summary>
/// Asignación y reasignación del chofer y el vehículo de un viaje (FR-019 a FR-029).
///
/// <b>Es el control que justifica el módulo</b>: que no salga a la ruta una unidad sin documentación
/// en regla a la fecha del viaje.
///
/// Cinco cosas que decide este caso de uso, en este orden:
///
/// <list type="number">
///   <item>El viaje admite asignación: sólo <c>pendiente</c> y <c>en curso</c> (FR-020).</item>
///   <item>Las dos unidades existen y son elegibles: chofer activo, vehículo activo y con estado
///   operativo guardado <c>disponible</c> (FR-021).</item>
///   <item><b>La habilitación por documentación, contra la fecha del viaje</b> y no contra hoy: un
///   documento vencido bloquea y no se guarda nada; uno próximo a vencer advierte y <b>sí</b> se
///   guarda; ninguno cargado habilita (FR-022, FR-023, FR-024, SC-014).</item>
///   <item><b>Si el viaje ya está <c>en curso</c>, la ocupación</b>: reasignarle una unidad que está
///   en otro viaje andando se rechaza. Reasignar un <c>pendiente</c> no verifica nada, porque un
///   pendiente no ocupa a nadie (FR-026a, FR-027).</item>
///   <item>El <b>transportista del chofer</b> queda registrado en el viaje (FR-028). No se compara
///   con el del vehículo: el transportista del viaje sale siempre del chofer (FR-029).</item>
/// </list>
/// </summary>
public class AsignarChoferYVehiculo(IRepositorioViajes viajes, TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        int id,
        AsignacionRequest peticion,
        CancellationToken cancelacion = default)
    {
        var viaje = await viajes.ObtenerParaModificarAsync(id, cancelacion);

        if (viaje is null)
        {
            return new ResultadoViaje(ErrorViaje.NoEncontrado);
        }

        // Uno de los cinco caminos de escritura que consultan el estado antes de tocar nada. Los dos
        // terminales tienen mensajes distintos: `rendido` es inmutable, `anulado` tampoco se reasigna
        // (FR-018, FR-020).
        if (EstadoTerminal.Rechazo(viaje) is { } terminal)
        {
            return terminal with
            {
                Error = viaje.Estado is EstadoViaje.Rendido
                    ? ErrorViaje.ViajeRendidoInmutable
                    : ErrorViaje.AsignacionNoPermitida,
                EstadoActual = NombresDeEstadoViaje.EnTexto(viaje.Estado),
            };
        }

        // FR-019b: los dos juntos. Un cuerpo con uno solo se rechaza antes de mirar nada más, para
        // que no quede ningún viaje con chofer y sin vehículo, ni al revés.
        if (peticion.ChoferId is null or <= 0)
        {
            return new ResultadoViaje(ErrorViaje.ChoferInexistente, Campo: "choferId");
        }

        if (peticion.VehiculoId is null or <= 0)
        {
            return new ResultadoViaje(ErrorViaje.VehiculoInexistente, Campo: "vehiculoId");
        }

        var chofer = await viajes.ObtenerChoferParaAsignarAsync(peticion.ChoferId.Value, cancelacion);

        if (chofer is null || !chofer.Activo)
        {
            return new ResultadoViaje(ErrorViaje.ChoferInexistente, Campo: "choferId");
        }

        var vehiculo = await viajes.ObtenerVehiculoParaAsignarAsync(
            peticion.VehiculoId.Value,
            cancelacion);

        // El mismo criterio que la lista de asignables, verificado en el servidor: la restricción no
        // vive sólo en el desplegable (FR-021).
        if (vehiculo is null || !vehiculo.Activo ||
            vehiculo.EstadoOperativo != VehiculoEstado.Disponible)
        {
            return new ResultadoViaje(ErrorViaje.VehiculoInexistente, Campo: "vehiculoId");
        }

        var nombreDelChofer = chofer.Persona?.NombreCompleto ?? $"Chofer {chofer.Id}";

        // ── La habilitación, contra la fecha del viaje ─────────────────────────────────────────
        // Chofer y vehículo se evalúan por separado y el primer bloqueo corta: el mensaje nombra una
        // unidad y un documento, y encadenar los dos motivos daría un texto que nadie lee entero.
        var veredictoChofer = EvaluadorHabilitacion.ParaChofer(chofer.Documentacion, viaje.Fecha);

        if (veredictoChofer.Bloquea)
        {
            return Bloqueo(viaje, nombreDelChofer, veredictoChofer.DocumentoQueDecide!);
        }

        var veredictoVehiculo = EvaluadorHabilitacion.ParaVehiculo(
            vehiculo.Documentacion,
            viaje.Fecha);

        if (veredictoVehiculo.Bloquea)
        {
            return Bloqueo(viaje, vehiculo.Patente, veredictoVehiculo.DocumentoQueDecide!);
        }

        // ── FR-026a: la ocupación, sólo si el viaje ya está en curso ───────────────────────────
        if (viaje.Estado is EstadoViaje.EnCurso &&
            await OcupacionAsync(viaje, chofer, nombreDelChofer, vehiculo, cancelacion) is { } ocupada)
        {
            return ocupada;
        }

        viaje.ChoferId = chofer.Id;
        viaje.VehiculoId = vehiculo.Id;

        // FR-028: el transportista **del chofer**, en el momento de asignarlo. Reasignar el chofer lo
        // vuelve a escribir con el del nuevo; que el chofer cambie de transportista después no mueve
        // este viaje. Y **no** se compara con el del vehículo: sale siempre del chofer (FR-029).
        viaje.TransportistaId = chofer.TransportistaId;

        try
        {
            await viajes.GuardarCambiosAsync(cancelacion);
        }
        catch (UnidadOcupadaException excepcion)
        {
            // La carrera que la consulta previa no alcanza a cerrar: dos operadores reasignando la
            // misma unidad a dos viajes en curso en el mismo milisegundo (SC-005).
            return await OcupacionAsync(viaje, chofer, nombreDelChofer, vehiculo, cancelacion)
                ?? new ResultadoViaje(
                    excepcion.EsDelChofer ? ErrorViaje.ChoferOcupado : ErrorViaje.VehiculoOcupado,
                    NumeroDelViaje: viaje.Numero,
                    Unidad: excepcion.EsDelChofer ? nombreDelChofer : vehiculo.Patente);
        }

        var momento = MomentoDeLectura.Desde(reloj);
        var ficha = await viajes.ObtenerFichaAsync(id, cancelacion);

        return new ResultadoViaje(
            ErrorViaje.Ninguno,
            ViajeDetalle.Desde(ficha!, momento),
            // FR-023, FR-015a: la advertencia llega **con** el resultado porque la asignación ya se
            // guardó, y reasignar es reversible mientras el viaje no esté rendido ni anulado.
            AdvertenciasDeAsignacion(veredictoChofer, nombreDelChofer, veredictoVehiculo, vehiculo),
            NumeroDelViaje: viaje.Numero);
    }

    private async Task<ResultadoViaje?> OcupacionAsync(
        Viaje viaje,
        Chofer chofer,
        string nombreDelChofer,
        Vehiculo vehiculo,
        CancellationToken cancelacion)
    {
        if (await viajes.ViajeEnCursoDelChoferAsync(chofer.Id, viaje.Id, cancelacion) is { } conChofer)
        {
            return new ResultadoViaje(
                ErrorViaje.ChoferOcupado,
                NumeroDelViaje: viaje.Numero,
                NumeroDeViajeRelacionado: conChofer.Numero,
                Unidad: nombreDelChofer);
        }

        if (await viajes.ViajeEnCursoDelVehiculoAsync(vehiculo.Id, viaje.Id, cancelacion)
            is { } conVehiculo)
        {
            return new ResultadoViaje(
                ErrorViaje.VehiculoOcupado,
                NumeroDelViaje: viaje.Numero,
                NumeroDeViajeRelacionado: conVehiculo.Numero,
                Unidad: vehiculo.Patente);
        }

        return null;
    }

    /// <summary>
    /// FR-022: el rechazo nombra la unidad, el documento y <b>la fecha del viaje</b>. Sin los tres,
    /// quien opera sabe que no puede pero no qué resolver — y sin la fecha, alguien que carga un
    /// viaje retroactivo no entiende por qué un papel "vencido" sí valía la semana pasada.
    /// </summary>
    private static ResultadoViaje Bloqueo(Viaje viaje, string unidad, DocumentoEvaluado documento) =>
        new(
            ErrorViaje.DocumentacionVencida,
            NumeroDelViaje: viaje.Numero,
            Unidad: unidad,
            Documento: documento.Tipo,
            NumeroDocumento: documento.Numero,
            FechaDeReferencia: FormatoDeFecha.Corta(viaje.Fecha));

    private static IReadOnlyList<Advertencia> AdvertenciasDeAsignacion(
        VeredictoHabilitacion veredictoChofer,
        string nombreDelChofer,
        VeredictoHabilitacion veredictoVehiculo,
        Vehiculo vehiculo)
    {
        var advertencias = new List<Advertencia>();

        // Una por unidad: si a los dos les falta poco para vencer, las dos se informan. Nombrar sólo
        // una dejaría a la otra sin resolver.
        if (veredictoChofer.Advierte)
        {
            advertencias.Add(Advertir(veredictoChofer.DocumentoQueDecide!, nombreDelChofer));
        }

        if (veredictoVehiculo.Advierte)
        {
            advertencias.Add(Advertir(veredictoVehiculo.DocumentoQueDecide!, vehiculo.Patente));
        }

        return advertencias;
    }

    private static Advertencia Advertir(DocumentoEvaluado documento, string unidad) => new(
        CodigosErrorViajes.DocumentacionProximaAvencer,
        MensajesViajes.DocumentacionProximaAvencer(
            documento.Tipo,
            unidad,
            FormatoDeFecha.Corta(documento.FechaVencimiento)));
}
