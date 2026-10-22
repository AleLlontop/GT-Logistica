using GT.Application.Reportes.Analisis;

namespace GT.Application.Reportes.Fuentes;

public static class FuenteAnalisisFlota
{
    // Copia declarada de TEXTO_ESTADO_VEHICULO de Flota/servicios/estados.ts ([012]).
    public static string EstadoVisible(string codigo) => codigo switch
    {
        "disponible" => "Disponible", "fueraDeServicio" => "Fuera de servicio",
        _ => throw new ArgumentOutOfRangeException(nameof(codigo)),
    };
    public static ReporteTabular Crear(AnalisisFlota datos, string filtros, string autor) => new(
        new("Utilización de flota por viajes realizados", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        [ColumnaDeReporte.Texto("Patente"), ColumnaDeReporte.Texto("Tipo actual"), ColumnaDeReporte.Texto("Transportista/propietario actual"),
         ColumnaDeReporte.Texto("Condición actual"), ColumnaDeReporte.Texto("Estado guardado actual"),
         ColumnaDeReporte.Texto("Disponibilidad actual"), ColumnaDeReporte.Entero("Viajes realizados")],
        datos.Detalle.Items.Select(v => (IReadOnlyList<CeldaDeReporte>)[CeldaDeReporte.Texto(v.Patente), CeldaDeReporte.Texto(v.Tipo.Nombre),
            CeldaDeReporte.Texto(v.Transportista.Nombre), CeldaDeReporte.Texto(v.Activo ? "Activo" : "Dado de baja"),
            CeldaDeReporte.Texto(EstadoVisible(v.EstadoOperativoGuardado)), CeldaDeReporte.Texto(EstadoVisible(v.DisponibilidadActual)),
            CeldaDeReporte.Entero(checked((int)v.CantidadViajes))]).ToArray(),
        [TotalDeReporte.Cantidad("Unidades del conjunto filtrado", datos.Resumen.CantidadUnidades),
         TotalDeReporte.Cantidad("Viajes realizados de estas unidades", datos.Resumen.TotalViajesConVehiculo),
         TotalDeReporte.Cantidad("Viajes sin vehículo en el período (fuera del filtro de unidades)", datos.Resumen.SinVehiculoEnPeriodo)]);
}

/// <summary>Adaptadores: exactamente las columnas y filas de012, sin copiar estados ni urgencias.</summary>
public static class FuenteAnalisisVencimientos
{
    private static EncabezadoDeReporte Encabezado(string titulo, ContextoAnalisis contexto, int cantidad, string autor) =>
        new(titulo, $"Situación actual al {contexto.HoyArgentina:dd/MM/yyyy}; sin corte histórico por período", cantidad, contexto.ConsultadoEn.UtcDateTime, autor);

    public static ReporteTabular Crear(AnalisisVencimientosChoferes datos, string autor) => new(
        Encabezado(FuenteReporteVencimientosChoferes.Titulo, datos.Contexto, datos.Detalle.Items.Count, autor),
        FuenteReporteVencimientosChoferes.Columnas, datos.Detalle.Items.Select(FuenteReporteVencimientosChoferes.Fila).ToArray(), []);
    public static ReporteTabular Crear(AnalisisVencimientosFlota datos, string autor) => new(
        Encabezado(FuenteReporteVencimientosFlota.Titulo, datos.Contexto, datos.Detalle.Items.Count, autor),
        FuenteReporteVencimientosFlota.Columnas, datos.Detalle.Items.Select(FuenteReporteVencimientosFlota.Fila).ToArray(), []);
    public static ReporteTabular Crear(AnalisisVencimientosFacturas datos, string autor) => new(
        Encabezado(FuenteReporteVencimientosFacturas.Titulo, datos.Contexto, datos.Detalle.Items.Count, autor),
        FuenteReporteVencimientosFacturas.Columnas, datos.Detalle.Items.Select(FuenteReporteVencimientosFacturas.Fila).ToArray(),
        [new("Total de importes", datos.ImporteTotal)]);
}
