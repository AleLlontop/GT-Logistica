using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota;

/// <summary>Un identificador con su nombre, para tipo y transportista (<c>Resumen</c> del contrato).</summary>
public record Resumen(int Id, string Nombre);

public enum ErrorVehiculo
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    PatenteInvalida,
    PatenteDuplicada,

    /// <summary>
    /// La patente está ocupada por una unidad <b>dada de baja</b> (FR-008f). Se distingue de
    /// <see cref="PatenteDuplicada"/> porque quien intenta recargarla no la encuentra en el listado
    /// por defecto y necesita que se le diga que la reactive (research §6).
    /// </summary>
    PatenteDeVehiculoDadoDeBaja,

    TipoVehiculoInexistente,
    TransportistaInexistente,

    /// <summary>FR-014a: se quiso dejar disponible una unidad con un documento vencido.</summary>
    DisponibleConDocumentacionVencida,

    /// <summary>FR-013, FR-014a: se quiso dejar disponible una unidad sin ningún documento.</summary>
    DisponibleSinDocumentacion,

    /// <summary>FR-008e: el transportista de la unidad está inactivo y no vino un reemplazo activo.</summary>
    TransportistaInactivoAlReactivar,

    /// <summary>FR-008e: el tipo de la unidad está inactivo y no vino un reemplazo activo.</summary>
    TipoInactivoAlReactivar,
}

/// <param name="DocumentoQueImpide">
/// Nombre del documento vencido que impide dejar la unidad disponible. El mensaje lo nombra: sin eso,
/// quien opera sabe que no puede pero no qué resolver (FR-014a).
/// </param>
public record ResultadoVehiculo(
    ErrorVehiculo Error,
    VehiculoDetalle? Vehiculo = null,
    string? Campo = null,
    string? DocumentoQueImpide = null)
{
    public bool Exitoso => Error is ErrorVehiculo.Ninguno;
}

/// <summary>
/// Lo que llega del formulario de vehículo.
///
/// <paramref name="EstadoOperativo"/> es lo que <b>elige</b> el operador y se guarda tal cual. Lo que
/// el listado y la ficha devuelven puede no ser lo mismo: ése es el valor derivado (FR-012, FR-014).
/// </summary>
public record VehiculoRequest(
    string? Patente,
    string? Marca,
    string? Modelo,
    int? TipoVehiculoId,
    int? TransportistaId,
    string? EstadoOperativo);

/// <summary>
/// Cuerpo <b>opcional</b> de la reactivación. Sólo hace falta si el transportista o el tipo de la
/// unidad fueron dados de baja mientras estuvo afuera (FR-008e, research §11).
/// </summary>
public record ReactivacionRequest(int? TransportistaId, int? TipoVehiculoId);

/// <summary>Fila del listado: exactamente las siete columnas de <c>contracts/README.md</c>.</summary>
/// <param name="Estado">
/// El estado operativo <b>derivado</b>, no el guardado: una unidad guardada como disponible cuyo
/// seguro venció figura como <c>fueraDeServicio</c> (FR-014).
/// </param>
public record VehiculoListado(
    int Id,
    string Patente,
    string Marca,
    string Modelo,
    Resumen Tipo,
    Resumen Transportista,
    bool Activo,
    string Estado,
    string EstadoDocumentacion);

/// <summary>
/// Ficha completa de una unidad.
///
/// <b>Devuelve el estado operativo dos veces, y es deliberado</b> (plan §Reevaluación post-diseño):
/// <see cref="VehiculoListado.Estado"/> es el derivado, para mostrar, y
/// <paramref name="EstadoOperativoGuardado"/> es el que eligió el operador, para poblar el formulario
/// de edición. Con uno solo, editar una unidad parada por papeles vencidos le pisaría en silencio el
/// motivo real a quien opera.
/// </summary>
public record VehiculoDetalle(
    int Id,
    string Patente,
    string Marca,
    string Modelo,
    Resumen Tipo,
    Resumen Transportista,
    bool Activo,
    string Estado,
    string EstadoDocumentacion,
    string EstadoOperativoGuardado,
    IReadOnlyList<DocumentoVehiculoDto> Documentos)
    : VehiculoListado(Id, Patente, Marca, Modelo, Tipo, Transportista, Activo, Estado, EstadoDocumentacion)
{
    public static VehiculoDetalle Desde(Vehiculo vehiculo, DateOnly hoy)
    {
        var tipo = vehiculo.Tipo
            ?? throw new InvalidOperationException(
                $"El vehículo {vehiculo.Id} llegó sin su tipo cargado.");

        var transportista = vehiculo.Transportista
            ?? throw new InvalidOperationException(
                $"El vehículo {vehiculo.Id} llegó sin su transportista cargado.");

        var estadoDocumentacion = CalculadorEstadoVehiculo.Calcular(vehiculo.Documentacion, hoy);

        var vigentes = CalculadorEstadoVehiculo
            .VigentesDeCadaTipo(vehiculo.Documentacion)
            .Select(documento => documento.Id)
            .ToHashSet();

        // Agrupados por tipo y, dentro de cada tipo, por vencimiento descendente: el vigente primero
        // y sus renovaciones anteriores debajo (contracts/README.md).
        var documentos = vehiculo.Documentacion
            .OrderBy(documento => documento.Tipo?.Nombre)
            .ThenByDescending(documento => documento.FechaVencimiento)
            .ThenByDescending(documento => documento.Id)
            .Select(documento => DocumentoVehiculoDto.Desde(
                documento,
                vigentes.Contains(documento.Id),
                hoy))
            .ToList();

        return new VehiculoDetalle(
            vehiculo.Id,
            vehiculo.Patente,
            vehiculo.Marca,
            vehiculo.Modelo,
            new Resumen(tipo.Id, tipo.Nombre),
            new Resumen(transportista.Id, transportista.Nombre),
            vehiculo.Activo,
            NombresDeEstadoFlota.DelVehiculo(
                CalculadorEstadoOperativo.Derivar(vehiculo.EstadoOperativo, estadoDocumentacion)),
            NombresDeEstadoFlota.DeLaDocumentacion(estadoDocumentacion),
            NombresDeEstadoFlota.DelVehiculo(vehiculo.EstadoOperativo),
            documentos);
    }
}

/// <summary>
/// Un documento del vehículo tal como lo devuelve el contrato. El <c>estado</c> y
/// <c>diasHastaVencimiento</c> se calculan al leer, nunca se guardan (FR-019, FR-021).
/// </summary>
public record DocumentoVehiculoDto(
    int Id,
    int VehiculoId,
    Resumen Tipo,
    string Numero,
    string FechaEmision,
    string FechaVencimiento,
    string Estado,
    bool EsVigenteDelTipo,
    int DiasHastaVencimiento,
    bool TieneArchivo,
    string? ArchivoNombre)
{
    /// <param name="esVigenteDelTipo">
    /// Si es el documento que manda para su tipo. Lo decide quien tiene a la vista los demás
    /// documentos del vehículo, no el documento solo (FR-024).
    /// </param>
    public static DocumentoVehiculoDto Desde(
        DocumentacionVehiculo documento,
        bool esVigenteDelTipo,
        DateOnly hoy)
    {
        var tipo = documento.Tipo
            ?? throw new InvalidOperationException(
                $"El documento {documento.Id} llegó sin su tipo cargado, y sin los días de aviso no " +
                "se puede calcular su estado.");

        return new DocumentoVehiculoDto(
            documento.Id,
            documento.VehiculoId,
            new Resumen(tipo.Id, tipo.Nombre),
            documento.Numero,
            documento.FechaEmision.ToString("yyyy-MM-dd"),
            documento.FechaVencimiento.ToString("yyyy-MM-dd"),
            NombresDeEstadoFlota.DelDocumento(
                CalculadorEstadoDocumento.Calcular(
                    documento.FechaVencimiento,
                    tipo.DiasAvisoVencimiento,
                    hoy)),
            esVigenteDelTipo,
            CalculadorEstadoDocumento.DiasHastaVencimiento(documento.FechaVencimiento, hoy),
            documento.TieneArchivo,
            documento.ArchivoNombre);
    }
}

public enum ErrorDocumentoVehiculo
{
    Ninguno,
    NoEncontrado,

    /// <summary>El documento no existe, o el vehículo al que se le quiso cargar tampoco.</summary>
    VehiculoNoEncontrado,

    DatosInvalidos,

    /// <summary>
    /// El tipo no existe, está inactivo, o es de ámbito <b>chofer</b>: este módulo no ofrece esos y
    /// tampoco los acepta si alguien manda el identificador a mano (FR-017a, US3 esc. 12).
    /// </summary>
    TipoInexistente,

    VencimientoAnteriorAEmision,
    ArchivoNoAdmitido,
    ArchivoNoGuardado,
}

public record ResultadoDocumentoVehiculo(
    ErrorDocumentoVehiculo Error,
    DocumentoVehiculoDto? Documento = null,
    string? Campo = null)
{
    public bool Exitoso => Error is ErrorDocumentoVehiculo.Ninguno;
}

/// <summary>Una fila del panel: qué unidad, de qué transportista, y qué documento la pone en falta.</summary>
public record AlertaVencimientoFlota(
    int VehiculoId,
    string Patente,
    Resumen Transportista,
    DocumentoVehiculoDto Documento);

/// <summary>
/// Filtro de estado del listado: un control único con <b>tres valores excluyentes</b> (FR-030a).
///
/// Combina el estado operativo derivado con el estado de alta. Los dos valores operativos son
/// complementarios dentro de los activos —todo vehículo activo cae en exactamente uno—, y por eso
/// <see cref="Disponible"/> nunca puede devolver una unidad con documentación vencida o ausente: lo
/// garantiza el predicado de la consulta, no un chequeo posterior (FR-015, SC-006, research §5).
/// </summary>
public enum FiltroEstadoVehiculo
{
    Disponible,
    FueraDeServicio,
    DadoDeBaja,
}

/// <summary>
/// Los cuatro filtros del listado, combinables entre sí (FR-030). Se aplican sobre toda la flota
/// <b>antes</b> de paginar (FR-032).
/// </summary>
/// <param name="Estado">
/// <c>null</c> significa <b>sólo los activos</b>, no "todos": el listado responde por la operación
/// del día, y quien quiera ver los dados de baja lo pide explícitamente (FR-031).
/// </param>
public record FiltrosDeFlota(
    int? TransportistaId = null,
    int? TipoVehiculoId = null,
    FiltroEstadoVehiculo? Estado = null,
    EstadoDocumentacionVehiculo? EstadoDocumentacion = null,
    int Pagina = 1);

/// <summary>
/// Los enums del dominio viajan en el JSON con la misma grafía que fija el contrato
/// (<c>fueraDeServicio</c>, <c>enRegla</c>, <c>proximaAvencer</c>, …), que es camelCase y no
/// PascalCase (convención [003] de <c>CLAUDE.md</c>).
/// </summary>
public static class NombresDeEstadoFlota
{
    public static string DelVehiculo(VehiculoEstado estado) => EnCamelCase(estado.ToString());

    public static string DeLaDocumentacion(EstadoDocumentacionVehiculo estado) =>
        EnCamelCase(estado.ToString());

    public static string DelDocumento(DocumentacionEstado estado) => EnCamelCase(estado.ToString());

    public static string DelFiltro(FiltroEstadoVehiculo filtro) => EnCamelCase(filtro.ToString());

    /// <summary>
    /// El estado operativo que llega del formulario. Un valor desconocido devuelve <c>null</c> y el
    /// campo se marca como inválido, en vez de caer en un valor por defecto que nadie eligió.
    /// </summary>
    public static VehiculoEstado? LeerEstadoOperativo(string? valor) => valor switch
    {
        "disponible" => VehiculoEstado.Disponible,
        "fueraDeServicio" => VehiculoEstado.FueraDeServicio,
        _ => null,
    };

    /// <summary>
    /// El filtro de estado del listado. Un valor desconocido se ignora en vez de romper: filtrar de
    /// más no es un error, y devuelve el listado por defecto —sólo los activos— (FR-031).
    /// </summary>
    public static FiltroEstadoVehiculo? LeerFiltroEstado(string? valor) => valor switch
    {
        "disponible" => FiltroEstadoVehiculo.Disponible,
        "fueraDeServicio" => FiltroEstadoVehiculo.FueraDeServicio,
        "dadoDeBaja" => FiltroEstadoVehiculo.DadoDeBaja,
        _ => null,
    };

    public static EstadoDocumentacionVehiculo? LeerEstadoDocumentacion(string? valor) => valor switch
    {
        "enRegla" => EstadoDocumentacionVehiculo.EnRegla,
        "proximaAvencer" => EstadoDocumentacionVehiculo.ProximaAvencer,
        "vencida" => EstadoDocumentacionVehiculo.Vencida,
        "sinDocumentacion" => EstadoDocumentacionVehiculo.SinDocumentacion,
        _ => null,
    };

    private static string EnCamelCase(string nombre) => char.ToLowerInvariant(nombre[0]) + nombre[1..];
}
