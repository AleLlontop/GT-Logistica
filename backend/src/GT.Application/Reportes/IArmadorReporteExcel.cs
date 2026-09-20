namespace GT.Application.Reportes;

/// <summary>
/// La frontera con la biblioteca de planillas. La única implementación conoce ClosedXML; nada más del
/// sistema la ve.
///
/// Recibe el mismo <see cref="ReporteTabular"/> que el armador de PDF: por eso los dos archivos del
/// mismo reporte no pueden traer filas distintas (research §2).
/// </summary>
public interface IArmadorReporteExcel
{
    byte[] Armar(ReporteTabular reporte);
}
