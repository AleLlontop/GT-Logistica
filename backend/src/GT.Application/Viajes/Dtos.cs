namespace GT.Application.Viajes;

/// <summary>
/// Un identificador con su nombre y si sigue activo en su padrón (<c>Resumen</c> del contrato).
///
/// <b>Lleva <paramref name="Activo"/> y el del Módulo 4 no</b>: acá un viaje puede referenciar a un
/// chofer, un vehículo o un cliente dados de baja después, y el listado tiene que mostrarlos
/// igual —con la palabra <c>(inactivo)</c> al lado, nunca sólo con un color— en vez de ocultarlos
/// (FR-008, FR-030, FR-049).
/// </summary>
public record Resumen(int Id, string Nombre, bool Activo);

/// <summary>
/// Una advertencia que <b>no</b> frenó la operación (FR-015a).
///
/// Viaja en <see cref="RespuestaViaje.Advertencias"/> junto con el resultado y nunca como error. El
/// criterio de qué advierte con el resultado y qué exige confirmación previa no es la gravedad sino
/// la <b>reversibilidad</b>: estas tres se corrigen editando, así que llegan con el guardado hecho
/// (research §5).
/// </summary>
public record Advertencia(string Codigo, string Mensaje);

/// <summary>
/// Sobre de las <b>tres</b> operaciones que pueden advertir: alta, edición y asignación. El resto de
/// los endpoints devuelve el recurso pelado.
///
/// La advertencia va acá y no adentro del viaje porque no es un dato del viaje: es un dato de
/// <b>esta operación</b>. Guardada en el recurso, reaparecería en cada consulta posterior de la
/// ficha (research §5).
/// </summary>
public record RespuestaViaje(ViajeDetalle Viaje, IReadOnlyList<Advertencia> Advertencias);

// ── Padrón de clientes ──────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lo que llega del formulario de cliente. <b>No incluye <c>activo</c></b>: dar de baja y dar de alta
/// son recursos propios, así que corregir una razón social no puede reactivar en silencio a alguien
/// que estaba dado de baja (FR-007, precedente [004]).
/// </summary>
public record ClienteRequest(
    string? RazonSocial,
    string? Cuit,
    string? Telefono,
    string? Email,
    string? Direccion);

public record ClienteDto(
    int Id,
    string RazonSocial,
    string Cuit,
    string Telefono,
    string Email,
    string? Direccion,
    bool Activo)
{
    public static ClienteDto Desde(Domain.Viajes.Cliente cliente) => new(
        cliente.Id,
        cliente.RazonSocial,
        cliente.Cuit,
        cliente.Telefono,
        cliente.Email,
        cliente.Direccion,
        cliente.Activo);
}

public enum ErrorCliente
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    CuitInvalido,
    CuitDuplicado,

    /// <summary>
    /// El CUIT está ocupado por un cliente <b>dado de baja</b> (FR-007). Se distingue de
    /// <see cref="CuitDuplicado"/> porque quien lo intenta no lo encuentra en el listado por defecto y
    /// necesita que se le diga que lo dé de alta de nuevo, no que lo busque (research §13).
    /// </summary>
    CuitDeClienteDadoDeBaja,

    EmailInvalido,

    /// <summary>FR-006: tiene viajes <c>pendiente</c> o <c>en curso</c>. Informa cuántos.</summary>
    ConViajes,
}

public record ResultadoCliente(
    ErrorCliente Error,
    ClienteDto? Cliente = null,
    string? Campo = null,
    int? CantidadViajes = null)
{
    public bool Exitoso => Error is ErrorCliente.Ninguno;
}

// ── Viajes ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lo que llega del formulario de viaje.
///
/// <b>No incluye número, estado, chofer ni vehículo.</b> No es que los ignore: no están en el contrato
/// de entrada, y esa es la diferencia. El número lo genera el sistema (FR-011), el estado tiene sus
/// tres recursos propios (FR-034) y la asignación el suyo (FR-019a). Así, corregir un destino no
/// puede avanzar un viaje ni cambiar quién lo maneja.
/// </summary>
public record ViajeRequest(
    int? ClienteId,
    DateOnly? Fecha,
    string? Origen,
    string? Destino,
    string? NumeroRemito,
    string? DetalleCarga,
    decimal? Importe);

/// <summary>
/// Fila del listado: las diez columnas de FR-040 más las dos señales derivadas.
/// </summary>
/// <param name="Transportista">
/// El registrado <b>en el viaje</b> al asignar el chofer, no el actual del chofer (FR-028, SC-010).
/// <c>null</c> mientras no haya chofer asignado.
/// </param>
/// <param name="Demorado">
/// Derivado al leer, nunca guardado (FR-039). El viaje sigue estando <c>enCurso</c>: demorado no es
/// un quinto estado.
/// </param>
/// <param name="EsRetroactivo">
/// Derivado al leer: la fecha del viaje es anterior al día en curso en Argentina (FR-016).
/// </param>
public record ViajeListado(
    int Id,
    int Numero,
    string Fecha,
    Resumen Cliente,
    string Origen,
    string Destino,
    Resumen? Chofer,
    Resumen? Vehiculo,
    Resumen? Transportista,
    string Estado,
    decimal Importe,
    bool Demorado,
    bool EsRetroactivo,
    string? MotivoAnulacion);

/// <summary>Ficha completa (FR-045), con el historial de cambios de estado.</summary>
public record ViajeDetalle(
    int Id,
    int Numero,
    string Fecha,
    Resumen Cliente,
    string Origen,
    string Destino,
    Resumen? Chofer,
    Resumen? Vehiculo,
    Resumen? Transportista,
    string Estado,
    decimal Importe,
    bool Demorado,
    bool EsRetroactivo,
    string? MotivoAnulacion,
    string? NumeroRemito,
    string? DetalleCarga,
    IReadOnlyList<CambioDeEstadoDto> Historial)
    : ViajeListado(
        Id, Numero, Fecha, Cliente, Origen, Destino, Chofer, Vehiculo, Transportista, Estado,
        Importe, Demorado, EsRetroactivo, MotivoAnulacion)
{
    /// <summary>
    /// Arma la ficha a partir del viaje ya cargado con sus relaciones y su historial.
    ///
    /// <b>Acá <c>demorado</c> se calcula con la regla en C#</b> —<c>Viaje.EstaDemorado</c> sobre el
    /// instante que sale del historial— mientras el listado lo resuelve con una subconsulta en SQL.
    /// Son dos escrituras de la misma regla, y por eso va un test que las compara sobre el mismo dato
    /// (convención [003], FR-039).
    /// </summary>
    public static ViajeDetalle Desde(Domain.Viajes.Viaje viaje, MomentoDeLectura momento)
    {
        var cliente = viaje.Cliente
            ?? throw new InvalidOperationException($"El viaje {viaje.Id} llegó sin su cliente cargado.");

        var enCursoDesde = viaje.CambiosDeEstado
            .Where(cambio => cambio.EstadoNuevo == Domain.Viajes.EstadoViaje.EnCurso)
            .Select(cambio => (DateTime?)cambio.OcurridoEn)
            .Max();

        return new ViajeDetalle(
            viaje.Id,
            viaje.Numero,
            viaje.Fecha.ToString("yyyy-MM-dd"),
            new Resumen(cliente.Id, cliente.RazonSocial, cliente.Activo),
            viaje.Origen,
            viaje.Destino,
            viaje.Chofer is { } chofer
                ? new Resumen(
                    chofer.Id,
                    chofer.Persona?.NombreCompleto ?? $"Chofer {chofer.Id}",
                    chofer.Activo)
                : null,
            viaje.Vehiculo is { } vehiculo
                ? new Resumen(vehiculo.Id, vehiculo.Patente, vehiculo.Activo)
                : null,
            viaje.Transportista is { } transportista
                ? new Resumen(transportista.Id, transportista.Nombre, transportista.Activo)
                : null,
            NombresDeEstadoViaje.EnJson(viaje.Estado),
            viaje.Importe,
            Domain.Viajes.Viaje.EstaDemorado(enCursoDesde, momento.Ahora),
            viaje.Fecha < momento.Hoy,
            viaje.MotivoAnulacion,
            viaje.NumeroRemito,
            viaje.DetalleCarga,
            // De la más vieja a la más nueva, empezando por el alta (FR-035, FR-045).
            [.. viaje.CambiosDeEstado
                .OrderBy(cambio => cambio.OcurridoEn)
                .ThenBy(cambio => cambio.Id)
                .Select(cambio => new CambioDeEstadoDto(
                    NombresDeEstadoViaje.EnJson(cambio.EstadoAnterior),
                    NombresDeEstadoViaje.EnJson(cambio.EstadoNuevo),
                    cambio.Usuario?.Username ?? $"Usuario {cambio.UsuarioId}",
                    cambio.OcurridoEn))]);
    }
}

/// <summary>
/// Una línea del historial (FR-035).
/// </summary>
/// <param name="EstadoAnterior">
/// <c>null</c> <b>sólo</b> en el registro del alta: antes del alta no había estado. La pantalla lo
/// muestra como <c>Alta</c>.
/// </param>
/// <param name="OcurridoEn">
/// Instante en UTC con la <c>Z</c> que lo declara, garantizada para todo el sistema por la conversión
/// declarada una sola vez en <c>GtDbContext</c> (convención [002]). El frontend lo muestra en hora
/// local con <c>formatearInstante</c>.
/// </param>
public record CambioDeEstadoDto(
    string? EstadoAnterior,
    string EstadoNuevo,
    string Usuario,
    DateTime OcurridoEn);

/// <summary>
/// Los rechazos posibles de las operaciones sobre un viaje.
///
/// Los primeros son problemas de <b>lo que se tipeó</b> y salen como <c>400</c>; los que están debajo
/// de la línea son problemas del <b>estado</b> de algo compartido o que cambió, y salen como
/// <c>409</c> (research §5). El endpoint hace esa traducción en un solo lugar.
/// </summary>
public enum ErrorViaje
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    ClienteInexistente,
    RemitoDuplicado,
    ImporteNegativo,
    MotivoRequerido,
    ChoferInexistente,
    VehiculoInexistente,
    RangoDeFechasRequerido,

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    ViajeRendidoInmutable,
    ViajeAnuladoInmutable,
    TransicionNoPermitida,
    FaltaAsignacion,
    UnidadDadaDeBaja,
    ChoferOcupado,
    VehiculoOcupado,
    RendicionRequiereConfirmacion,
    DocumentacionVencida,
    AsignacionNoPermitida,
    FechaBloqueaAsignacion,
}

/// <summary>
/// Lo que devuelve cualquier operación sobre un viaje: el resultado, o el rechazo con todo lo que el
/// mensaje necesita nombrar.
/// </summary>
/// <param name="Advertencias">
/// Las que no frenaron la operación. Sólo las llenan las tres que pueden advertir —alta, edición y
/// asignación—; el resto devuelve la lista vacía (FR-015a).
/// </param>
/// <param name="NumeroDelViaje">
/// El del viaje sobre el que se operó, para los mensajes que lo nombran. Se conserva aunque la
/// operación falle, que es justo cuando hace falta.
/// </param>
/// <param name="NumeroDeViajeRelacionado">
/// El del <b>otro</b> viaje: el que ya usa el remito (FR-014) o el que ocupa a la unidad (FR-026).
/// </param>
/// <param name="Unidad">Nombre del chofer o patente del vehículo que bloquea (FR-022).</param>
/// <param name="Documento">Tipo del documento que bloquea; <paramref name="NumeroDocumento"/> lo completa.</param>
/// <param name="FechaDeReferencia">
/// La fecha contra la que se evaluó la documentación, ya formateada. Va en el mensaje porque la
/// evaluación corre contra la fecha del viaje y no contra hoy: decir "está vencido" a secas
/// confundiría a quien carga un viaje retroactivo (SC-014).
/// </param>
public record ResultadoViaje(
    ErrorViaje Error,
    ViajeDetalle? Viaje = null,
    IReadOnlyList<Advertencia>? Advertencias = null,
    string? Campo = null,
    int? NumeroDelViaje = null,
    int? NumeroDeViajeRelacionado = null,
    string? Unidad = null,
    string? Documento = null,
    string? NumeroDocumento = null,
    string? FechaDeReferencia = null,
    string? EstadoActual = null,
    string? EstadoPedido = null)
{
    public bool Exitoso => Error is ErrorViaje.Ninguno;

    /// <summary>El sobre <c>{ viaje, advertencias }</c> de las tres operaciones que advierten.</summary>
    public RespuestaViaje Sobre() => new(Viaje!, Advertencias ?? []);
}

/// <summary>Una fila de cualquiera de los dos cuadros de totales (FR-046).</summary>
public record TotalDelPeriodo(int Id, string Nombre, int CantidadViajes, decimal ImporteTotal);

/// <summary>Los dos cuadros de la pantalla de totales.</summary>
public record TotalesDelPeriodo(
    IReadOnlyList<TotalDelPeriodo> PorCliente,
    IReadOnlyList<TotalDelPeriodo> PorTransportista);

/// <summary>Las dos listas que alimentan los desplegables de la pantalla de asignación (FR-021).</summary>
public record Asignables(IReadOnlyList<Resumen> Choferes, IReadOnlyList<Resumen> Vehiculos);
