namespace GT.Application.Reportes;

/// <summary>Cuál de los cinco. De acá sale el prefijo del nombre del archivo (FR-001, FR-012).</summary>
public enum TipoDeReporte
{
    Viajes,
    VencimientosChoferes,
    VencimientosFlota,
    VencimientosFacturas,
    MovimientosCaja,
}

/// <summary>
/// El nombre del archivo (FR-012). Función pura de reporte, formato e instante, con test propio.
///
/// <code>
/// viajes-2026-09-20-1432.pdf
/// vencimientos-choferes-2026-09-20-1432.xlsx
/// movimientos-caja-2026-09-20-1432.pdf
/// </code>
///
/// **Fecha y hora de Argentina.** Sin la hora, dos reportes del mismo listado el mismo día colisionan
/// en la carpeta, que es justo lo que FR-012 quiere evitar.
///
/// **Sólo ASCII, minúsculas y guiones**: así viaja en el <c>filename</c> simple del
/// <c>Content-Disposition</c> y el frontend lo extrae con una expresión regular de una línea, sin
/// recomponerlo en TypeScript (research §8, convención [009]).
/// </summary>
public static class NombreDeArchivoDeReporte
{
    public static string Prefijo(TipoDeReporte reporte) => reporte switch
    {
        TipoDeReporte.Viajes => "viajes",
        TipoDeReporte.VencimientosChoferes => "vencimientos-choferes",
        TipoDeReporte.VencimientosFlota => "vencimientos-flota",
        TipoDeReporte.VencimientosFacturas => "vencimientos-facturas",
        TipoDeReporte.MovimientosCaja => "movimientos-caja",
        _ => throw new ArgumentOutOfRangeException(nameof(reporte), reporte, null),
    };

    /// <param name="generadoEn">Instante UTC. Se convierte a hora de Argentina acá adentro.</param>
    public static string Para(TipoDeReporte reporte, FormatoDeReporte formato, DateTime generadoEn)
    {
        var enArgentina = EncabezadoDeReporte.EnArgentina(generadoEn);

        return $"{Prefijo(reporte)}-{enArgentina:yyyy-MM-dd-HHmm}{FormatosDeReporte.Extension(formato)}";
    }
}
