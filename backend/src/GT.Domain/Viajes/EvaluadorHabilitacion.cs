using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Domain.Viajes;

/// <summary>
/// Un documento ya reducido a lo que la habilitación necesita mirar. Existe para que la regla sirva
/// igual al chofer y al vehículo, que en los Módulos 3 y 4 son tablas distintas a propósito.
/// </summary>
public record DocumentoEvaluado(string Tipo, string Numero, DateOnly FechaVencimiento);

/// <param name="DocumentoQueDecide">
/// El documento que produjo el veredicto: el vencido en <see cref="HabilitacionAsignacion.Bloqueado"/>
/// y el próximo a vencer en <see cref="HabilitacionAsignacion.ConAdvertencia"/>. <c>null</c> cuando
/// está habilitado.
///
/// Se devuelve porque el mensaje lo nombra —tipo, número y fecha—: sin eso, quien opera sabe que no
/// puede pero no qué resolver (FR-022, FR-023).
/// </param>
public record VeredictoHabilitacion(
    HabilitacionAsignacion Habilitacion,
    DocumentoEvaluado? DocumentoQueDecide = null)
{
    public bool Bloquea => Habilitacion is HabilitacionAsignacion.Bloqueado;

    public bool Advierte => Habilitacion is HabilitacionAsignacion.ConAdvertencia;
}

/// <summary>
/// Veredicto de habilitación de una unidad para un viaje (FR-022 a FR-024).
///
/// <b>Lo único nuevo acá es el paso 3.</b> Los dos primeros los resuelven los Módulos 3 y 4 tal como
/// están, sin tocarles una línea, y eso es posible porque sus calculadores ya reciben la fecha de
/// referencia por parámetro en vez de leer el reloj (plan §Enfoque técnico 1):
///
/// <list type="number">
///   <item>De cada tipo, el documento vigente: <c>CalculadorEstadoChofer.VigentesDeCadaTipo</c> y
///   <c>CalculadorEstadoVehiculo.VigentesDeCadaTipo</c>.</item>
///   <item>El estado de cada uno con <c>CalculadorEstadoDocumento.Calcular</c>, pasándole
///   <b>la fecha del viaje</b> donde los otros módulos pasan <c>FechaHoyArgentina.Hoy()</c>.</item>
///   <item>Traducir esos estados a los tres valores de <see cref="HabilitacionAsignacion"/>.</item>
/// </list>
///
/// Evaluar contra la fecha del viaje y no contra hoy es lo que hace verdadera la carga retroactiva: un
/// viaje de la semana pasada se asigna con la documentación que estaba vigente ese día, aunque hoy
/// esté vencida, y un viaje del mes que viene se rechaza si el papel vence antes (SC-014).
///
/// <b>Ningún documento cargado es <c>habilitado</c></b>, no bloqueado (FR-024). Contradice al Módulo 4
/// —donde una unidad sin documentación no puede quedar <c>disponible</c>— y está bien: son dos
/// preguntas distintas. Allá se pregunta si la unidad está en condiciones; acá, si hay algo cargado
/// que <b>prohíba</b> este viaje. Además la lista de asignables ya filtró por el estado operativo
/// guardado, así que el Módulo 4 ya dijo lo suyo antes (research §3).
/// </summary>
public static class EvaluadorHabilitacion
{
    /// <param name="documentos">Todos los documentos del chofer, vigentes e históricos.</param>
    /// <param name="fechaDelViaje">La fecha contra la que se evalúa. No es "hoy".</param>
    public static VeredictoHabilitacion ParaChofer(
        IEnumerable<Documentacion> documentos,
        DateOnly fechaDelViaje) =>
        Evaluar(
            CalculadorEstadoChofer
                .VigentesDeCadaTipo(documentos)
                .Select(documento => (
                    Documento: new DocumentoEvaluado(
                        NombreDelTipo(documento.Tipo, documento.Id),
                        documento.Numero,
                        documento.FechaVencimiento),
                    DiasAviso: DiasAvisoDelTipo(documento.Tipo, documento.Id))),
            fechaDelViaje);

    /// <param name="documentos">Todos los documentos del vehículo, vigentes e históricos.</param>
    /// <param name="fechaDelViaje">La fecha contra la que se evalúa. No es "hoy".</param>
    public static VeredictoHabilitacion ParaVehiculo(
        IEnumerable<DocumentacionVehiculo> documentos,
        DateOnly fechaDelViaje) =>
        Evaluar(
            CalculadorEstadoVehiculo
                .VigentesDeCadaTipo(documentos)
                .Select(documento => (
                    Documento: new DocumentoEvaluado(
                        NombreDelTipo(documento.Tipo, documento.Id),
                        documento.Numero,
                        documento.FechaVencimiento),
                    DiasAviso: DiasAvisoDelTipo(documento.Tipo, documento.Id))),
            fechaDelViaje);

    private static VeredictoHabilitacion Evaluar(
        IEnumerable<(DocumentoEvaluado Documento, int DiasAviso)> vigentes,
        DateOnly fechaDelViaje)
    {
        var evaluados = vigentes
            .Select(vigente => (
                vigente.Documento,
                Estado: CalculadorEstadoDocumento.Calcular(
                    vigente.Documento.FechaVencimiento,
                    vigente.DiasAviso,
                    fechaDelViaje)))
            .ToList();

        // El que decide es el que vence primero, con desempate por nombre de tipo: sin criterio, dos
        // consultas idénticas nombrarían documentos distintos en el mismo mensaje.
        var vencidos = evaluados
            .Where(evaluado => evaluado.Estado is DocumentacionEstado.Vencida)
            .OrderBy(evaluado => evaluado.Documento.FechaVencimiento)
            .ThenBy(evaluado => evaluado.Documento.Tipo, StringComparer.Ordinal)
            .ToList();

        if (vencidos.Count > 0)
        {
            return new VeredictoHabilitacion(
                HabilitacionAsignacion.Bloqueado,
                vencidos[0].Documento);
        }

        var porVencer = evaluados
            .Where(evaluado => evaluado.Estado is DocumentacionEstado.ProximaAvencer)
            .OrderBy(evaluado => evaluado.Documento.FechaVencimiento)
            .ThenBy(evaluado => evaluado.Documento.Tipo, StringComparer.Ordinal)
            .ToList();

        if (porVencer.Count > 0)
        {
            return new VeredictoHabilitacion(
                HabilitacionAsignacion.ConAdvertencia,
                porVencer[0].Documento);
        }

        // Cubre los dos casos de FR-024 con la misma respuesta: todos vigentes, y ninguno cargado.
        return new VeredictoHabilitacion(HabilitacionAsignacion.Habilitado);
    }

    private static string NombreDelTipo(DocumentacionTipo? tipo, int documentoId) =>
        tipo?.Nombre ?? throw new InvalidOperationException(
            $"El documento {documentoId} llegó sin su tipo cargado, y sin el nombre el rechazo no " +
            "puede decir qué documento lo impide.");

    private static int DiasAvisoDelTipo(DocumentacionTipo? tipo, int documentoId) =>
        tipo?.DiasAvisoVencimiento ?? throw new InvalidOperationException(
            $"El documento {documentoId} llegó sin su tipo cargado, y sin los días de aviso no se " +
            "puede calcular su estado.");
}
