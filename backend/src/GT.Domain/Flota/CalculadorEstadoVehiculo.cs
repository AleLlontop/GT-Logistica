using GT.Domain.Choferes;

namespace GT.Domain.Flota;

/// <summary>
/// Estado general de la documentación de un vehículo (FR-033).
///
/// Se calcula en dos pasos, igual que el del chofer:
/// <list type="number">
///   <item>Tomar el documento <b>vigente</b> de cada tipo: el de vencimiento más lejano, y con
///   empate, el de mayor <c>Id</c> (FR-024, research §12). Los demás son historial: se ven en la
///   ficha, no definen nada y no alertan.</item>
///   <item>Quedarse con el <b>peor</b> de esos estados, con la precedencia
///   vencida &gt; próxima a vencer &gt; en regla.</item>
/// </list>
///
/// La regla de cada documento no se reescribe: se reutiliza
/// <see cref="CalculadorEstadoDocumento"/> del Módulo 3 tal cual, porque recibe tres primitivos y no
/// sabe de choferes (research §2).
///
/// Dos cosas que no hace, y son deliberadas:
/// <list type="bullet">
///   <item>No compara contra ninguna lista de documentación obligatoria: ningún tipo lo es, y el
///   sistema no infiere que falte un documento que nunca se cargó (FR-034).</item>
///   <item>No mira el archivo adjunto: que un documento no tenga escaneo es un dato del documento,
///   no del vehículo, y no altera este estado (FR-016a).</item>
/// </list>
///
/// <b>Esta misma regla se traduce a SQL</b> en la consulta del listado, para poder filtrar por estado
/// sin traer las filas a memoria. Las dos escrituras van cubiertas por un test que las compara sobre
/// el mismo dato (convención [003] de <c>AGENTS.md</c>).
/// </summary>
public static class CalculadorEstadoVehiculo
{
    /// <param name="documentos">Todos los documentos del vehículo, vigentes e históricos.</param>
    /// <param name="hoy">Día en curso en Argentina (<see cref="FechaHoyArgentina"/>).</param>
    public static EstadoDocumentacionVehiculo Calcular(
        IEnumerable<DocumentacionVehiculo> documentos,
        DateOnly hoy)
    {
        var estados = VigentesDeCadaTipo(documentos)
            .Select(documento => CalculadorEstadoDocumento.Calcular(
                documento.FechaVencimiento,
                documento.Tipo?.DiasAvisoVencimiento
                    ?? throw new InvalidOperationException(
                        $"El documento {documento.Id} llegó sin su tipo cargado, y sin los días de " +
                        "aviso no se puede calcular su estado."),
                hoy))
            .ToList();

        if (estados.Count == 0)
        {
            return EstadoDocumentacionVehiculo.SinDocumentacion;
        }

        if (estados.Contains(DocumentacionEstado.Vencida))
        {
            return EstadoDocumentacionVehiculo.Vencida;
        }

        return estados.Contains(DocumentacionEstado.ProximaAvencer)
            ? EstadoDocumentacionVehiculo.ProximaAvencer
            : EstadoDocumentacionVehiculo.EnRegla;
    }

    /// <summary>
    /// De cada tipo, el documento que manda: el de vencimiento más lejano (FR-024). El desempate por
    /// <c>Id</c> mayor no es decorativo —dos documentos del mismo tipo con la misma fecha son un
    /// error de carga plausible, y sin criterio el resultado cambiaría entre dos consultas idénticas
    /// (research §12)—.
    /// </summary>
    public static IEnumerable<DocumentacionVehiculo> VigentesDeCadaTipo(
        IEnumerable<DocumentacionVehiculo> documentos) =>
        documentos
            .GroupBy(documento => documento.DocumentacionTipoId)
            .Select(delTipo => delTipo
                .OrderByDescending(documento => documento.FechaVencimiento)
                .ThenByDescending(documento => documento.Id)
                .First());
}
