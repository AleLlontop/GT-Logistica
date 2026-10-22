using GT.Application.Reportes.Analisis;
using GT.Application.Viajes;
using GT.Application.Caja;

namespace GT.Application.Reportes.Fuentes;

/// <summary>Traducciones puras al modelo común; el repositorio entrega la foto completa, nunca una página visible.</summary>
public static class FuenteAnalisisViajes
{
    public static ReporteTabular Crear(AnalisisViajes datos, string filtros, string autor) => new(
        new("Análisis de viajes", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        // [015] La celda monetaria admite el máximo completo sin partir sus centavos en PDF.
        FuenteReporteViajes.Columnas.Select(c => c.Tipo switch { TipoDeColumna.Importe => c with { AnchoRelativoPdf = 3f }, TipoDeColumna.Fecha => c with { AnchoRelativoPdf = 1.4f }, _ => c }).ToArray(),
        datos.Detalle.Items.Select(v => (IReadOnlyList<CeldaDeReporte>)[
            CeldaDeReporte.Entero(v.Numero), CeldaDeReporte.Fecha(v.Fecha), CeldaDeReporte.Texto(v.Cliente.Nombre),
            CeldaDeReporte.Texto(v.Origen), CeldaDeReporte.Texto(v.Destino), CeldaDeReporte.Texto(v.Chofer?.Nombre),
            CeldaDeReporte.Texto(v.Vehiculo?.Nombre), CeldaDeReporte.Texto(v.Transportista?.Nombre),
            CeldaDeReporte.Texto(NombresDeEstadoViaje.EnPantalla(NombresDeEstadoViaje.Leer(v.Estado)!.Value)),
            CeldaDeReporte.Importe(v.Importe)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de viajes", datos.Resumen.Cantidad),
         TotalDeReporte.Cantidad("Realizados", datos.Resumen.Realizados),
         TotalDeReporte.Cantidad("Pendientes de rendición", datos.Resumen.PendientesRendicion),
         TotalDeReporte.Cantidad("Pendientes", datos.Resumen.Pendientes),
         TotalDeReporte.Cantidad("Anulados", datos.Resumen.Anulados),
         new("Importe vigente (excluye anulados)", datos.Resumen.ImporteTotal)]);
}

public static class FuenteAnalisisCaja
{
    public static ReporteTabular Crear(AnalisisCaja datos, string filtros, string autor) => new(
        new("Análisis de movimientos de caja", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        FuenteReporteMovimientosCaja.Columnas,
        datos.Detalle.Items.Select(m => (IReadOnlyList<CeldaDeReporte>)[
            CeldaDeReporte.Fecha(DateOnly.FromDateTime(EncabezadoDeReporte.EnArgentina(m.Fecha))),
            CeldaDeReporte.Texto(NombresDeEstadoCaja.EnPantalla(NombresDeEstadoCaja.LeerTipo(m.Tipo)!.Value)),
            CeldaDeReporte.Importe(m.Importe), CeldaDeReporte.Texto(m.Concepto),
            CeldaDeReporte.Texto(m.Responsable.Nombre), CeldaDeReporte.Texto(m.Referencia)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de movimientos", datos.Resumen.Cantidad),
         new("Ingresos", datos.Resumen.Ingresos), new("Egresos", datos.Resumen.Egresos),
         new("Movimiento neto", datos.Resumen.Neto)]);
}
