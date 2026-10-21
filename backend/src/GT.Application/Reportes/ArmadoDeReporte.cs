namespace GT.Application.Reportes;

/// <summary>
/// El paso común a los cinco reportes: verificar el tope, poner el encabezado, elegir el armador
/// según el formato y nombrar el archivo.
///
/// Existe para que las cinco fuentes se ocupen de **lo único que las distingue** —qué consulta usan,
/// qué columnas tienen y qué totalizan— y no de repetir cinco veces el mismo cierre.
///
/// Es también lo que garantiza que el **instante del encabezado y el del nombre del archivo sean el
/// mismo**: se lee una vez del <c>TimeProvider</c> registrado y se usa para los dos.
/// </summary>
public class ArmadoDeReporte(
    IArmadorReportePdf pdf,
    IArmadorReporteExcel excel,
    OpcionesDeReporte opciones,
    TimeProvider reloj)
{
    /// <summary>
    /// <c>true</c> si el filtro dejó **más** filas que el tope. Con filas iguales al tope el reporte se
    /// entrega, porque FR-016 rechaza "más de" (research §4).
    /// </summary>
    public bool SuperaElTope(int filas) => opciones.Supera(filas);

    /// <summary>El <c>409</c> con sus dos números y el mensaje ya armado por el backend.</summary>
    public ResultadoReporte TopeSuperado(int filas) =>
        ResultadoReporte.TopeSuperado(filas, opciones.TopeDeFilas);

    /// <summary>Cuántas filas pedirle a la consulta paginada: las del tope, en una sola vuelta.</summary>
    public int TamanioParaTraerTodo => opciones.TopeDeFilas;

    /// <summary>Día del reporte en Argentina para evaluar los vencimientos con su mismo reloj.</summary>
    public DateOnly Fecha => GT.Domain.Choferes.FechaHoyArgentina.Desde(reloj.GetUtcNow());

    public ResultadoReporte Entregar(
        TipoDeReporte tipo,
        FormatoDeReporte formato,
        string generadoPor,
        string titulo,
        string filtros,
        IReadOnlyList<ColumnaDeReporte> columnas,
        IReadOnlyList<IReadOnlyList<CeldaDeReporte>> filas,
        IReadOnlyList<TotalDeReporte>? totales = null)
    {
        return Entregar(tipo, formato, generadoPor, titulo, filtros, columnas, filas,
            reloj.GetUtcNow(), totales);
    }

    /// <summary>La foto analítica ya capturó el reloj antes de consultar; no volver a leerlo al renderizar.</summary>
    public ResultadoReporte Entregar(
        TipoDeReporte tipo,
        FormatoDeReporte formato,
        string generadoPor,
        string titulo,
        string filtros,
        IReadOnlyList<ColumnaDeReporte> columnas,
        IReadOnlyList<IReadOnlyList<CeldaDeReporte>> filas,
        DateTimeOffset instanteCapturado,
        IReadOnlyList<TotalDeReporte>? totales = null)
    {
        var generadoEn = instanteCapturado.UtcDateTime;

        var reporte = new ReporteTabular(
            new EncabezadoDeReporte(titulo, filtros, filas.Count, generadoEn, generadoPor),
            columnas,
            filas,
            totales ?? []);

        return Entregar(tipo, formato, reporte);
    }

    /// <summary>Dibuja el modelo ya materializado; no abre transacciones ni consulta reloj o datos.</summary>
    public ResultadoReporte Entregar(TipoDeReporte tipo, FormatoDeReporte formato, ReporteTabular reporte)
    {
        var contenido = formato is FormatoDeReporte.Pdf
            ? pdf.Armar(reporte)
            : excel.Armar(reporte);

        return ResultadoReporte.Entregado(
            contenido,
            NombreDeArchivoDeReporte.Para(tipo, formato, reporte.Encabezado.GeneradoEn),
            formato);
    }
}
