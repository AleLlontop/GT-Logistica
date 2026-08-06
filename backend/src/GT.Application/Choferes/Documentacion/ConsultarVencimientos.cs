using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

/// <summary>Una fila del panel: qué chofer, de qué transportista, y qué documento lo pone en falta.</summary>
public record AlertaVencimiento(
    int ChoferId,
    string Apellido,
    string Nombre,
    TransportistaResumen Transportista,
    DocumentoDto Documento);

/// <summary>
/// Panel de vencimientos (FR-021).
///
/// Es la vista que responde, al entrar al módulo, qué choferes necesitan renovar algo. No hay nada
/// que ejecutar ni ningún proceso nocturno: el estado se calcula al consultar, así que un documento
/// entra al panel solo, el día que le toca (FR-019).
///
/// Dos exclusiones deliberadas, las dos de FR-021:
/// <list type="bullet">
///   <item><b>Los choferes inactivos no aparecen</b>, cualquiera sea el estado de su documentación:
///   ya no salen a la ruta, así que nadie va a renovar esos papeles.</item>
///   <item><b>Los documentos históricos no alertan</b>: sólo se evalúa el vigente de cada tipo, el
///   de vencimiento más lejano. Cargar una renovación saca la alerta sin que nadie borre ni edite el
///   documento anterior (FR-020a, SC-010).</item>
/// </list>
/// </summary>
public class ConsultarVencimientos(IRepositorioDocumentacion repositorio)
{
    public async Task<List<AlertaVencimiento>> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var hoy = FechaHoyArgentina.Hoy();
        var candidatos = await repositorio.ConsultarVigentesDeChoferesActivosAsync(cancelacion);

        return candidatos
            .Where(documento => CalculadorEstadoDocumento.Calcular(
                documento.FechaVencimiento,
                documento.Tipo!.DiasAvisoVencimiento,
                hoy) is not DocumentacionEstado.Vigente)
            // Ordenado por urgencia: primero lo vencido hace más tiempo. El Id desempata para que
            // dos documentos con la misma fecha no cambien de lugar entre dos consultas iguales.
            .OrderBy(documento => documento.FechaVencimiento)
            .ThenBy(documento => documento.Id)
            .Select(documento => new AlertaVencimiento(
                documento.ChoferId,
                documento.Chofer!.Persona!.Apellido,
                documento.Chofer.Persona.Nombre,
                new TransportistaResumen(
                    documento.Chofer.Transportista!.Id,
                    documento.Chofer.Transportista.Nombre),
                DocumentoDto.Desde(documento, esVigenteDelTipo: true, hoy)))
            .ToList();
    }
}
