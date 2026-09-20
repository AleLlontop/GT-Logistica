using GT.Application.Facturacion;
using GT.Application.Reportes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GT.Infrastructure.Reportes;

/// <summary>
/// Arma el PDF de cualquiera de los cinco reportes. Junto con
/// <see cref="ArmadorReporteExcelClosedXml"/>, son las <b>únicas dos clases que conocen una biblioteca
/// de documentos</b>: la capa de aplicación habla con las interfaces y el dominio no sabe que existe
/// un PDF.
///
/// <b>Una sola disposición para los cinco</b>: A4 <b>apaisado</b>, márgenes de 1 cm, encabezado
/// repetido en cada página y número de página al pie (research §10). El de viajes tiene diez columnas
/// y en vertical no entra; elegir la orientación por reporte serían cinco documentos distintos de
/// revisar donde un conjunto de reportes tiene que verse igual.
///
/// <b>El instante de generación entra al archivo, y también a los metadatos</b>. Es deliberado y
/// contrario a la regla del documento de la factura, que se fija a partir de sus datos para poder
/// compararse byte a byte: un reporte es la foto de un listado que cambia, y a qué momento corresponde
/// la foto es parte del dato (FR-006, research §7). La consecuencia es que **no se escribe ningún test
/// de igualdad byte a byte sobre un reporte**.
///
/// <b>Requisito nativo</b>: el motor de texto de QuestPDF necesita <c>libfontconfig1</c> y
/// <c>libfreetype6</c>, que <c>backend/Dockerfile</c> ya instala desde el Módulo 6. Lo cubre
/// <c>ArmadoresTests</c>, que genera un PDF de verdad resolviendo el servicio del contenedor.
/// </summary>
public class ArmadorReportePdfQuestPdf : IArmadorReportePdf
{
    private const float TamanioBase = 8f;
    private static readonly Color GrisSuave = Colors.Grey.Lighten3;
    private static readonly Color GrisBorde = Colors.Grey.Medium;

    public byte[] Armar(ReporteTabular reporte)
    {
        try
        {
            return Document
                .Create(documento => documento.Page(hoja => Componer(hoja, reporte)))
                .WithMetadata(MetadatosDe(reporte))
                .GeneratePdf();
        }
        catch (Exception excepcion)
        {
            // Traducido a la excepción de la capa de aplicación: el endpoint responde el `500` del
            // contrato con su texto en español, y no una excepción cruda ni un `200` de cero bytes.
            throw new ReporteNoGeneradoException(excepcion);
        }
    }

    /// <summary>
    /// <c>CreationDate</c> lleva el instante de generación, <b>a propósito</b>. Es lo contrario de lo
    /// que hace el documento de la factura, y el motivo está en el comentario de la clase.
    /// </summary>
    private static DocumentMetadata MetadatosDe(ReporteTabular reporte) => new()
    {
        Title = reporte.Encabezado.Titulo,
        Author = reporte.Encabezado.GeneradoPor,
        Subject = reporte.Encabezado.Filtros,
        Creator = "Sistema Integral de Gestión — G&T Logística",
        Producer = "Sistema Integral de Gestión — G&T Logística",
        CreationDate = reporte.Encabezado.GeneradoEnArgentina,
        ModifiedDate = reporte.Encabezado.GeneradoEnArgentina,
    };

    private static void Componer(PageDescriptor hoja, ReporteTabular reporte)
    {
        hoja.Size(PageSizes.A4.Landscape());
        hoja.Margin(1f, Unit.Centimetre);
        hoja.DefaultTextStyle(estilo => estilo.FontSize(TamanioBase).FontFamily(Fonts.Calibri));

        // El encabezado va en el `Header` y no en el cuerpo: así se repite en cada página, que es lo
        // que pide FR-006 para un reporte que puede tener miles de filas.
        hoja.Header().Element(contenedor => Encabezado(contenedor, reporte.Encabezado));

        hoja.Content().PaddingTop(8).Element(contenedor => Tabla(contenedor, reporte));

        hoja.Footer().AlignCenter().Text(texto =>
        {
            texto.DefaultTextStyle(estilo => estilo.FontSize(TamanioBase - 1).FontColor(GrisBorde));
            texto.Span("Página ");
            texto.CurrentPageNumber();
            texto.Span(" de ");
            texto.TotalPages();
        });
    }

    /// <summary>Las cinco piezas de FR-006, en su orden.</summary>
    private static void Encabezado(IContainer contenedor, EncabezadoDeReporte encabezado) =>
        contenedor
            .BorderBottom(1)
            .BorderColor(GrisBorde)
            .PaddingBottom(6)
            .Column(columna =>
            {
                columna.Spacing(2);

                columna.Item().Text(encabezado.Titulo).Bold().FontSize(TamanioBase + 5);

                columna.Item().Text(encabezado.Filtros).FontColor(Colors.Grey.Darken2);

                columna.Item().Text(encabezado.LineaDeFilas).FontColor(Colors.Grey.Darken2);

                columna.Item().Text(
                        $"{encabezado.LineaDeGeneracion} · {encabezado.LineaDeAutor}")
                    .FontSize(TamanioBase - 1)
                    .FontColor(Colors.Grey.Darken1);
            });

    private static void Tabla(IContainer contenedor, ReporteTabular reporte) =>
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(definicion =>
            {
                foreach (var columna in reporte.Columnas)
                {
                    // Las columnas de dato corto llevan ancho relativo menor; el texto se reparte el
                    // resto. Con cinco columnas sobra aire, que no es un problema (research §10).
                    definicion.RelativeColumn(columna.Tipo == TipoDeColumna.Texto ? 2f : 1.2f);
                }
            });

            // `Header` de la tabla, no de la página: QuestPDF lo repite en cada hoja al partir.
            tabla.Header(encabezado =>
            {
                foreach (var columna in reporte.Columnas)
                {
                    encabezado.Cell()
                        .Background(GrisSuave)
                        .Border(0.5f)
                        .BorderColor(GrisBorde)
                        .Padding(3)
                        .AlignedSegun(columna.Tipo)
                        .Text(columna.Nombre)
                        .Bold();
                }
            });

            foreach (var fila in reporte.Filas)
            {
                for (var indice = 0; indice < reporte.Columnas.Count; indice++)
                {
                    var tipo = reporte.Columnas[indice].Tipo;
                    var celda = indice < fila.Count ? fila[indice] : CeldaDeReporte.Texto(null);

                    tabla.Cell()
                        .Border(0.5f)
                        .BorderColor(Colors.Grey.Lighten1)
                        .Padding(3)
                        .AlignedSegun(tipo)
                        .Text(TextoDe(celda));
                }
            }

            // La fila de totales va al final de la tabla, en los dos formatos (research §10).
            foreach (var total in reporte.Totales)
            {
                tabla.Cell()
                    .ColumnSpan((uint)Math.Max(reporte.Columnas.Count - 1, 1))
                    .Background(GrisSuave)
                    .Border(0.5f)
                    .BorderColor(GrisBorde)
                    .Padding(3)
                    .AlignRight()
                    .Text(total.Etiqueta)
                    .Bold();

                tabla.Cell()
                    .Background(GrisSuave)
                    .Border(0.5f)
                    .BorderColor(GrisBorde)
                    .Padding(3)
                    .AlignRight()
                    .Text(FormatoDeDocumento.Pesos(total.Importe))
                    .Bold();
            }
        });

    /// <summary>
    /// El valor de la celda tal como se imprime. **Los importes van en pesos argentinos** y las fechas
    /// en <c>dd/MM/yyyy</c> (FR-010, Principio II).
    ///
    /// Reusa <c>FormatoDeDocumento</c> del Módulo 6, que es el único lugar del backend donde está
    /// escrito el formato de un peso: una segunda copia acá haría que el reporte y la factura se
    /// leyeran distinto el día que alguien toque una (convención [009]).
    ///
    /// **Una celda sin dato queda vacía**: ni guion, ni "Sin asignar" (spec §Edge Cases).
    /// </summary>
    private static string TextoDe(CeldaDeReporte celda) => celda switch
    {
        { EstaVacia: true } => string.Empty,
        { Tipo: TipoDeColumna.Texto } => celda.ValorTexto!,
        { Tipo: TipoDeColumna.Fecha } => FormatoDeDocumento.Fecha(celda.ValorFecha!.Value),
        { Tipo: TipoDeColumna.Importe } => FormatoDeDocumento.Pesos(celda.ValorImporte!.Value),
        { Tipo: TipoDeColumna.Entero } => celda.ValorEntero!.Value.ToString(),
        _ => string.Empty,
    };
}

internal static class AlineacionDeCelda
{
    /// <summary>Los importes y los enteros van a la derecha; el resto, a la izquierda (FR-010).</summary>
    public static IContainer AlignedSegun(this IContainer contenedor, TipoDeColumna tipo) =>
        tipo is TipoDeColumna.Importe or TipoDeColumna.Entero
            ? contenedor.AlignRight()
            : contenedor.AlignLeft();
}
