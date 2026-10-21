using GT.Domain.Facturacion;

namespace GT.Application.Reportes.Analisis;

/// <summary>Palabras copiadas del mapa de Facturación/servicios/api.ts; código JSON conserva su mapa original.</summary>
public static class EstadoFacturaAnalisis
{
    public static string EnPantalla(EstadoFacturaVisible estado) => estado switch
    {
        EstadoFacturaVisible.Pendiente => "Pendiente",
        EstadoFacturaVisible.Vencida => "Vencida",
        EstadoFacturaVisible.Pagada => "Pagada",
        EstadoFacturaVisible.Anulada => "Anulada",
        _ => throw new ArgumentOutOfRangeException(nameof(estado)),
    };
}
