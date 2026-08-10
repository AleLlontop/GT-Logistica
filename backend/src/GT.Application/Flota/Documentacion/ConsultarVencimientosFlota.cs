using GT.Domain.Choferes;

namespace GT.Application.Flota.Documentacion;

/// <summary>
/// Panel de vencimientos de la flota (FR-035).
///
/// Es la vista que responde, al entrar al módulo, qué unidades necesitan renovar algo antes de quedar
/// inhabilitadas para circular. No hay nada que ejecutar ni ningún proceso nocturno: el estado se
/// calcula al consultar, así que un documento entra al panel solo, el día que le toca (FR-022,
/// SC-005).
///
/// <b>Separado del panel de choferes a propósito</b>: unificar los dos sería más lindo y es alcance
/// fantasma —la spec no lo pide, y las dos vistas responden preguntas distintas: quién no puede
/// manejar y qué unidad no puede salir— (research §10).
///
/// Tres exclusiones deliberadas:
/// <list type="bullet">
///   <item><b>Los vehículos dados de baja no aparecen</b>, cualquiera sea el estado de sus papeles:
///   ya no forman parte de la flota operativa y nadie va a renovarlos. Al reactivar la unidad vuelve
///   a alertar sola, sin recargar nada (FR-008e).</item>
///   <item><b>Los documentos históricos no alertan</b>: sólo el vigente de cada tipo. Cargar una
///   renovación saca la alerta sin que nadie borre ni edite el documento anterior (FR-024,
///   SC-010).</item>
///   <item><b>Los documentos vigentes no aparecen</b>: el panel es de lo que hay que resolver.</item>
/// </list>
///
/// Todo vehículo que el filtro <c>disponible</c> dejó afuera por documentación vencida o ausente
/// figura acá, y eso no es coincidencia: las dos consultas miran el mismo vigente de cada tipo
/// (FR-015, SC-006).
/// </summary>
public class ConsultarVencimientosFlota(IRepositorioDocumentacionVehiculo repositorio)
{
    public async Task<List<AlertaVencimientoFlota>> EjecutarAsync(CancellationToken cancelacion = default)
    {
        var hoy = FechaHoyArgentina.Hoy();
        var candidatos = await repositorio.ConsultarVigentesDeVehiculosActivosAsync(cancelacion);

        return candidatos
            .Where(documento => CalculadorEstadoDocumento.Calcular(
                documento.FechaVencimiento,
                documento.Tipo!.DiasAvisoVencimiento,
                hoy) is not DocumentacionEstado.Vigente)
            // Ordenado por urgencia: primero lo vencido hace más tiempo. El Id desempata para que dos
            // documentos con la misma fecha no cambien de lugar entre dos consultas iguales.
            .OrderBy(documento => documento.FechaVencimiento)
            .ThenBy(documento => documento.Id)
            .Select(documento => new AlertaVencimientoFlota(
                documento.VehiculoId,
                documento.Vehiculo!.Patente,
                new Resumen(
                    documento.Vehiculo.Transportista!.Id,
                    documento.Vehiculo.Transportista.Nombre),
                DocumentoVehiculoDto.Desde(documento, esVigenteDelTipo: true, hoy)))
            .ToList();
    }
}
