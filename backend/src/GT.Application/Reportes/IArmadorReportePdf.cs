namespace GT.Application.Reportes;

/// <summary>
/// La frontera con la biblioteca de PDF. La capa de aplicación habla con esta interfaz y el dominio
/// no sabe que existe un PDF, igual que con <c>IArmadorDocumentoFactura</c> del Módulo 6.
///
/// Recibe el <see cref="ReporteTabular"/> —el mismo objeto que consume el armador de Excel— y
/// devuelve los bytes.
/// </summary>
public interface IArmadorReportePdf
{
    byte[] Armar(ReporteTabular reporte);
}
