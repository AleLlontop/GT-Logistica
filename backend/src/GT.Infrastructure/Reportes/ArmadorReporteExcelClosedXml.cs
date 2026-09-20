using ClosedXML.Excel;
using GT.Application.Reportes;

namespace GT.Infrastructure.Reportes;

/// <summary>
/// Arma el <c>.xlsx</c> de cualquiera de los cinco reportes. Es la <b>única clase del sistema que
/// conoce ClosedXML</b>.
///
/// <b>Lo que decide esta clase es FR-011</b>: los importes y las fechas se asignan como
/// <c>decimal</c> y <c>DateTime</c> y se les fija el formato con <c>Style.NumberFormat.Format</c>;
/// el valor sigue siendo un número y la planilla lo ordena, lo filtra y lo suma sin conversión
/// (SC-007). Escribirlos como texto —<c>"$ 1.240.000,00"</c>— haría que la columna no se pudiera
/// sumar, que es justo lo que la spec pide poder hacer.
///
/// Los totales del pie también van como número, para que SC-004 se compruebe con la calculadora de la
/// planilla.
///
/// **Sin requisitos nativos**: ClosedXML es C# puro y el <c>Dockerfile</c> no cambia por ella
/// (research §1). Aun así lleva su test de integración, que reabre el archivo generado y verifica el
/// tipo de las celdas, resolviendo el servicio del contenedor (convención [006]).
/// </summary>
public class ArmadorReporteExcelClosedXml : IArmadorReporteExcel
{
    private const string FormatoDeImporte = "#,##0.00";
    private const string FormatoDeFecha = "dd/mm/yyyy";

    /// <summary>31 caracteres es el tope que Excel admite para el nombre de una hoja.</summary>
    private const int LargoMaximoDeHoja = 31;

    public byte[] Armar(ReporteTabular reporte)
    {
        try
        {
            using var libro = new XLWorkbook();
            var hoja = libro.AddWorksheet(NombreDeHoja(reporte.Encabezado.Titulo));

            var fila = EscribirEncabezado(hoja, reporte);

            fila = EscribirTabla(hoja, reporte, fila);

            EscribirTotales(hoja, reporte, fila);

            hoja.Columns().AdjustToContents();

            using var memoria = new MemoryStream();
            libro.SaveAs(memoria);

            return memoria.ToArray();
        }
        catch (Exception excepcion)
        {
            throw new ReporteNoGeneradoException(excepcion);
        }
    }

    /// <summary>Las cinco piezas de FR-006, en las primeras filas y en el mismo orden que el PDF.</summary>
    private static int EscribirEncabezado(IXLWorksheet hoja, ReporteTabular reporte)
    {
        var encabezado = reporte.Encabezado;

        hoja.Cell(1, 1).Value = encabezado.Titulo;
        hoja.Cell(1, 1).Style.Font.Bold = true;
        hoja.Cell(1, 1).Style.Font.FontSize = 14;

        hoja.Cell(2, 1).Value = encabezado.Filtros;
        hoja.Cell(3, 1).Value = encabezado.LineaDeFilas;
        hoja.Cell(4, 1).Value = $"{encabezado.LineaDeGeneracion} · {encabezado.LineaDeAutor}";

        // La fila 5 queda en blanco, para que la tabla arranque separada del encabezado.
        return 6;
    }

    private static int EscribirTabla(IXLWorksheet hoja, ReporteTabular reporte, int filaInicial)
    {
        for (var columna = 0; columna < reporte.Columnas.Count; columna++)
        {
            var celda = hoja.Cell(filaInicial, columna + 1);

            celda.Value = reporte.Columnas[columna].Nombre;
            celda.Style.Font.Bold = true;
            celda.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var fila = filaInicial + 1;

        foreach (var filaDelReporte in reporte.Filas)
        {
            for (var columna = 0; columna < reporte.Columnas.Count; columna++)
            {
                var valor = columna < filaDelReporte.Count
                    ? filaDelReporte[columna]
                    : CeldaDeReporte.Texto(null);

                Escribir(hoja.Cell(fila, columna + 1), valor);
            }

            fila++;
        }

        return fila;
    }

    /// <summary>
    /// El pie: la etiqueta en la penúltima columna y el importe <b>como número</b> en la última.
    /// </summary>
    private static void EscribirTotales(IXLWorksheet hoja, ReporteTabular reporte, int filaInicial)
    {
        if (reporte.Totales.Count == 0)
        {
            return;
        }

        var ultima = Math.Max(reporte.Columnas.Count, 2);
        var fila = filaInicial + 1;

        foreach (var total in reporte.Totales)
        {
            var etiqueta = hoja.Cell(fila, ultima - 1);
            etiqueta.Value = total.Etiqueta;
            etiqueta.Style.Font.Bold = true;
            etiqueta.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            var importe = hoja.Cell(fila, ultima);
            importe.Value = total.Importe;
            importe.Style.NumberFormat.Format = FormatoDeImporte;
            importe.Style.Font.Bold = true;

            fila++;
        }
    }

    /// <summary>
    /// **Acá está FR-011.** El importe se asigna como <c>decimal</c> y la fecha como <c>DateTime</c>,
    /// con su formato de número; nunca como texto.
    ///
    /// Una celda sin dato **se deja vacía**, no se rellena.
    /// </summary>
    private static void Escribir(IXLCell celda, CeldaDeReporte valor)
    {
        if (valor.EstaVacia)
        {
            return;
        }

        switch (valor.Tipo)
        {
            case TipoDeColumna.Texto:
                celda.Value = valor.ValorTexto;
                break;

            case TipoDeColumna.Fecha:
                celda.Value = valor.ValorFecha!.Value.ToDateTime(TimeOnly.MinValue);
                celda.Style.NumberFormat.Format = FormatoDeFecha;
                break;

            case TipoDeColumna.Importe:
                celda.Value = valor.ValorImporte!.Value;
                celda.Style.NumberFormat.Format = FormatoDeImporte;
                break;

            case TipoDeColumna.Entero:
                celda.Value = valor.ValorEntero!.Value;
                break;
        }
    }

    private static string NombreDeHoja(string titulo) =>
        titulo.Length <= LargoMaximoDeHoja ? titulo : titulo[..LargoMaximoDeHoja];
}
