using GT.Application.Facturacion;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GT.Infrastructure.Documentos;

/// <summary>
/// La <b>única clase del sistema que conoce QuestPDF</b> (plan §Structure Decision). La capa de
/// aplicación habla con <see cref="IArmadorDocumentoFactura"/> y el dominio no sabe que existe un PDF.
///
/// Dibuja los nueve bloques de FR-031 en su orden, y <b>la misma disposición para los tres tipos de
/// comprobante</b>: lo único que cambia entre una A, una B y una C es la letra, el código, el título y
/// el valor de la alícuota (FR-031j). No hay un pie distinto según el tipo — una `Factura C` muestra
/// `IVA (0,00 %)` y `$ 0,00`, que no es un error ni una factura incompleta (FR-023).
///
/// <b>Acá no se decide ningún contenido</b>: todo llega ya formateado en <see cref="DatosDelDocumento"/>,
/// que es el mapeo único desde la entidad. Esta clase sólo ubica texto en la hoja. Es lo que hace que
/// la vista previa y el archivo guardado no puedan decir cosas distintas (research §2).
///
/// <b>Requisito nativo</b>: el motor de texto necesita <c>libfontconfig1</c> y <c>libfreetype6</c>.
/// Están en <c>backend/Dockerfile</c>; sin ellos el backend arranca perfecto y falla acá, al emitir la
/// primera factura. Lo cubre <c>ArmadorDocumentoFacturaTests</c>, que genera un PDF de verdad en CI.
/// </summary>
public class ArmadorDocumentoFacturaQuestPdf : IArmadorDocumentoFactura
{
    private const float TamanioBase = 9f;
    private static readonly Color GrisSuave = Colors.Grey.Lighten3;
    private static readonly Color GrisBorde = Colors.Grey.Medium;

    public byte[] Armar(DatosDelDocumento datos)
    {
        try
        {
            return Document
                .Create(documento => documento.Page(hoja => Componer(hoja, datos)))
                .WithMetadata(MetadatosDe(datos))
                .GeneratePdf();
        }
        catch (Exception excepcion)
        {
            // Se traduce a la excepción de la capa de aplicación para que los casos de uso puedan
            // decidir sin conocer la biblioteca: la emisión se rechaza entera, la corrección no se
            // guarda y la anulación no queda aplicada a medias (FR-031, FR-031b).
            throw new DocumentoNoGeneradoException(
                "No se pudo generar el documento de la factura.",
                excepcion);
        }
    }

    /// <summary>
    /// Los metadatos del PDF, <b>derivados de la factura y de nada más</b>.
    ///
    /// <b>Las dos fechas se fijan y no se leen del reloj, y no es un detalle</b>: QuestPDF estampa por
    /// defecto el instante de generación, así que dos armados de la misma factura difieren en los bytes de
    /// esa fecha. Eso convierte a SC-007b —"la vista previa y el documento guardado coinciden byte a
    /// byte"— en algo que sólo se cumple si los dos caen en el mismo segundo, y el test que lo verifica
    /// pasaría o fallaría según el reloj.
    ///
    /// Con la fecha de facturación en su lugar, <b>el documento es una función de la factura</b>: el mismo
    /// comprobante produce siempre los mismos bytes. Es también lo que hace verificable la regeneración de
    /// FR-031b — si el archivo cambió, cambió porque cambió un dato, no porque se rearmó.
    ///
    /// Lo descubrió el recorrido manual del <c>quickstart.md</c>, cuando la comparación byte a byte falló
    /// por un dígito de segundos.
    /// </summary>
    private static DocumentMetadata MetadatosDe(DatosDelDocumento datos)
    {
        var fecha = DateTime.TryParseExact(
            datos.FechaDeEmision,
            "dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var emitida)
            ? emitida
            : DateTime.UnixEpoch;

        return new DocumentMetadata
        {
            Title = $"{datos.Titulo} {datos.NumeroComprobante}",
            Author = datos.EmisorRazonSocial,
            Subject = $"Período {datos.Periodo}",
            Creator = "Sistema Integral de Gestión — G&T Logística",
            Producer = "Sistema Integral de Gestión — G&T Logística",
            CreationDate = fecha,
            ModifiedDate = fecha,
        };
    }

    private static void Componer(PageDescriptor hoja, DatosDelDocumento datos)
    {
        // A4 vertical: es un comprobante pensado para imprimirse y mandarse al cliente.
        hoja.Size(PageSizes.A4);
        hoja.Margin(1.2f, Unit.Centimetre);
        hoja.DefaultTextStyle(estilo => estilo.FontSize(TamanioBase).FontFamily(Fonts.Calibri));

        hoja.Content().Column(cuerpo =>
        {
            cuerpo.Spacing(6);

            // Cuando la factura está anulada, lo primero que se ve es que lo está: el documento no
            // puede circular como si estuviera vigente (FR-031d). Va impresa acá, en el armador, y no
            // se estampa al servir el archivo — el documento se arma en un solo lugar.
            if (datos.LeyendaAnulada is { } leyenda)
            {
                cuerpo.Item().Element(contenedor => BandaDeAnulacion(contenedor, leyenda, datos));
            }

            // 1. Banda de ejemplar.
            cuerpo.Item().AlignCenter().Text(datos.BandaDeEjemplar).Bold().FontSize(TamanioBase + 1);

            // 2, 3 y 4. Emisor · recuadro de letra · identificación, en un solo renglón de tres.
            cuerpo.Item().Element(contenedor => Encabezado(contenedor, datos));

            // 5. Banda de vencimiento de pago, a todo el ancho.
            cuerpo.Item().Element(contenedor =>
                Banda(contenedor, "Vencimiento de pago", datos.VencimientoPago));

            // 6. Banda de CBU. Se omite entera cuando el CBU congelado está vacío (FR-031).
            if (datos.EmisorCbu is { } cbu)
            {
                cuerpo.Item().Element(contenedor => Banda(contenedor, "CBU", cbu));
            }

            // 7. Bloque del cliente.
            cuerpo.Item().Element(contenedor => BloqueDelCliente(contenedor, datos));

            // 8. Tabla de detalle: una fila por viaje, nunca una fila consolidada (FR-031e).
            cuerpo.Item().Element(contenedor => TablaDeDetalle(contenedor, datos));

            // 9. Pie de importes, con las Observaciones a su izquierda.
            cuerpo.Item().Element(contenedor => PieDeImportes(contenedor, datos));

            // FR-031c: el documento no es el comprobante fiscal, y lo dice él mismo.
            cuerpo.Item().PaddingTop(4).Text(datos.LeyendaNoFiscal)
                .FontSize(TamanioBase - 1.5f)
                .Italic()
                .FontColor(Colors.Grey.Darken1);
        });

        // Con muchos viajes el detalle sigue de largo; el pie numera las hojas para que quien recibe
        // el comprobante impreso sepa si le llegó completo (FR-031e).
        hoja.Footer().AlignCenter().Text(texto =>
        {
            texto.DefaultTextStyle(estilo => estilo.FontSize(TamanioBase - 2).FontColor(GrisBorde));
            texto.Span("Página ");
            texto.CurrentPageNumber();
            texto.Span(" de ");
            texto.TotalPages();
        });
    }

    private static void BandaDeAnulacion(
        IContainer contenedor,
        string leyenda,
        DatosDelDocumento datos) =>
        contenedor
            .Border(1)
            .BorderColor(Colors.Red.Darken2)
            .Background(Colors.Red.Lighten4)
            .Padding(6)
            .Column(columna =>
            {
                columna.Item().AlignCenter().Text(leyenda)
                    .Bold()
                    .FontSize(TamanioBase + 4)
                    .FontColor(Colors.Red.Darken3);

                // El motivo va junto a la leyenda y no en una nota al pie: quien mira el documento
                // tiene que enterarse de por qué se anuló en el mismo golpe de vista (FR-031d).
                if (!string.IsNullOrWhiteSpace(datos.MotivoAnulacion))
                {
                    columna.Item().AlignCenter().Text($"Motivo: {datos.MotivoAnulacion}")
                        .FontColor(Colors.Red.Darken3);
                }
            });

    /// <summary>Los bloques 2, 3 y 4 comparten renglón, como en un comprobante argentino impreso.</summary>
    private static void Encabezado(IContainer contenedor, DatosDelDocumento datos) =>
        contenedor.Border(1).BorderColor(GrisBorde).Row(renglon =>
        {
            // 2. Bloque del emisor. Sin logo se arma igual y sin dejar hueco (FR-031g): la imagen
            // simplemente no se agrega, y la columna se acomoda sola.
            renglon.RelativeItem(4).Padding(8).Column(emisor =>
            {
                emisor.Spacing(2);

                if (datos.Logo is { } logo)
                {
                    emisor.Item().Height(38).AlignLeft().Image(logo.Contenido).FitHeight();
                }

                emisor.Item().Text(datos.EmisorRazonSocial).Bold().FontSize(TamanioBase + 3);
                emisor.Item().Text(datos.EmisorCondicionIva);
                emisor.Item().Text(datos.EmisorDomicilio);
            });

            // 3. Recuadro de letra: la letra grande con su código numérico debajo (FR-031i).
            renglon.ConstantItem(56).BorderLeft(1).BorderRight(1).BorderColor(GrisBorde)
                .Padding(6)
                .Column(recuadro =>
                {
                    recuadro.Item().AlignCenter().Text(datos.Letra).Bold().FontSize(26);
                    recuadro.Item().AlignCenter().Text($"COD. {datos.CodigoDeComprobante}")
                        .FontSize(TamanioBase - 1.5f);
                });

            // 4. Bloque de identificación, con el período en MM/AAAA.
            renglon.RelativeItem(4).Padding(8).Column(identificacion =>
            {
                identificacion.Spacing(2);

                identificacion.Item().Text(datos.Titulo).Bold().FontSize(TamanioBase + 3);
                identificacion.Item().Text($"N° {datos.NumeroComprobante}").Bold();

                Dato(identificacion, "Fecha de emisión", datos.FechaDeEmision);
                Dato(identificacion, "Período facturado", datos.Periodo);
                Dato(identificacion, "CUIT", datos.EmisorCuit);
                Dato(identificacion, "Ingresos Brutos", datos.EmisorIngresosBrutos);
                Dato(identificacion, "Inicio de actividades", datos.EmisorInicioActividades);
            });
        });

    private static void Banda(IContainer contenedor, string rotulo, string valor) =>
        contenedor
            .Border(1)
            .BorderColor(GrisBorde)
            .Background(GrisSuave)
            .PaddingVertical(4)
            .PaddingHorizontal(8)
            .Text(texto =>
            {
                texto.Span($"{rotulo}: ").Bold();
                texto.Span(valor);
            });

    private static void BloqueDelCliente(IContainer contenedor, DatosDelDocumento datos) =>
        contenedor.Border(1).BorderColor(GrisBorde).Padding(8).Row(renglon =>
        {
            renglon.RelativeItem().Column(columna =>
            {
                columna.Spacing(2);
                Dato(columna, "Cliente", datos.ClienteRazonSocial);
                Dato(columna, "CUIT", datos.ClienteCuit);
                Dato(columna, "Domicilio", datos.ClienteDomicilio);
            });

            renglon.RelativeItem().Column(columna =>
            {
                columna.Spacing(2);

                // Texto fijo: no es un campo del padrón ni algo que se elija al emitir, porque todos
                // los clientes de la empresa son empresas (FR-031h).
                Dato(columna, "Condición de IVA", datos.ClienteCondicionIva);

                Dato(columna, "Condición de venta", datos.CondicionDeVenta);

                // Vacío a propósito: cada viaje lleva su propio remito en su fila del detalle
                // (FR-031h). El rótulo sale igual porque es parte del formulario del comprobante.
                Dato(columna, "Remito", datos.ClienteRemito);
            });
        });

    /// <summary>
    /// Las nueve columnas de FR-031e, con una fila por viaje.
    ///
    /// El encabezado va en <c>table.Header</c>, que QuestPDF <b>repite en cada página</b>: una factura
    /// de muchos viajes sigue de largo y las hojas siguientes no quedan con columnas sin nombre.
    /// </summary>
    private static void TablaDeDetalle(IContainer contenedor, DatosDelDocumento datos) =>
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                columnas.ConstantColumn(38);    // Código
                columnas.RelativeColumn(4);     // Producto / Servicio
                columnas.ConstantColumn(38);    // Cantidad
                columnas.ConstantColumn(48);    // U. Medida
                columnas.RelativeColumn(1.4f);  // Precio unit.
                columnas.ConstantColumn(44);    // % Bonif.
                columnas.RelativeColumn(1.4f);  // Importe
                columnas.ConstantColumn(48);    // % IVA
                columnas.RelativeColumn(1.4f);  // Subtotal
            });

            tabla.Header(encabezado =>
            {
                Encabezado(encabezado, "Código");
                Encabezado(encabezado, "Producto / Servicio", alineadoALaIzquierda: true);
                Encabezado(encabezado, "Cantidad");
                Encabezado(encabezado, "U. Medida");
                Encabezado(encabezado, "Precio unit.");
                Encabezado(encabezado, "% Bonif.");
                Encabezado(encabezado, "Importe");

                // Sale siempre, también en una Factura B y en una C: la disposición no cambia entre
                // tipos (FR-031j).
                Encabezado(encabezado, "% IVA");

                Encabezado(encabezado, "Subtotal");

                static void Encabezado(
                    TableCellDescriptor celda,
                    string texto,
                    bool alineadoALaIzquierda = false)
                {
                    var contenedor = celda.Cell()
                        .Border(1)
                        .BorderColor(GrisBorde)
                        .Background(GrisSuave)
                        .Padding(4);

                    (alineadoALaIzquierda ? contenedor : contenedor.AlignCenter())
                        .Text(texto)
                        .Bold()
                        .FontSize(TamanioBase - 1);
                }
            });

            foreach (var fila in datos.Detalle)
            {
                Celda(tabla, fila.Codigo, centrada: true);
                Celda(tabla, fila.ProductoServicio);
                Celda(tabla, fila.Cantidad, centrada: true);
                Celda(tabla, fila.UnidadDeMedida, centrada: true);
                Celda(tabla, fila.PrecioUnitario, aLaDerecha: true);
                Celda(tabla, fila.PorcentajeBonificacion, aLaDerecha: true);
                Celda(tabla, fila.Importe, aLaDerecha: true);
                Celda(tabla, fila.PorcentajeIva, aLaDerecha: true);
                Celda(tabla, fila.Subtotal, aLaDerecha: true);
            }

            static void Celda(
                TableDescriptor tabla,
                string texto,
                bool centrada = false,
                bool aLaDerecha = false)
            {
                var contenedor = tabla.Cell().Border(1).BorderColor(GrisBorde).Padding(4);

                if (centrada) contenedor = contenedor.AlignCenter();
                else if (aLaDerecha) contenedor = contenedor.AlignRight();

                contenedor.Text(texto).FontSize(TamanioBase - 0.5f);
            }
        });

    private static void PieDeImportes(IContainer contenedor, DatosDelDocumento datos) =>
        contenedor.Row(renglon =>
        {
            // Las Observaciones se omiten enteras —rótulo incluido— cuando el detalle está vacío,
            // con el mismo criterio que la banda de CBU (FR-031, bloque 9).
            renglon.RelativeItem(3).Column(izquierda =>
            {
                if (datos.Observaciones is not { } observaciones)
                {
                    return;
                }

                izquierda.Item().Border(1).BorderColor(GrisBorde).Padding(8).Column(bloque =>
                {
                    bloque.Spacing(2);
                    bloque.Item().Text("Observaciones").Bold();
                    bloque.Item().Text(observaciones);
                });
            });

            renglon.ConstantItem(12);

            renglon.RelativeItem(2).Border(1).BorderColor(GrisBorde).Padding(8).Column(derecha =>
            {
                derecha.Spacing(3);

                // Los tres importes del pie son los que mandan: si por redondeo la suma de los
                // subtotales por fila difiere del total, la diferencia es de las filas, no de acá
                // (FR-031f).
                Importe(derecha, "Neto", datos.Neto);
                Importe(derecha, datos.EtiquetaIva, datos.Iva);

                derecha.Item().PaddingTop(2).BorderTop(1).BorderColor(GrisBorde).PaddingTop(4)
                    .Row(total =>
                    {
                        total.RelativeItem().Text("TOTAL").Bold().FontSize(TamanioBase + 2);
                        total.RelativeItem().AlignRight().Text(datos.Total)
                            .Bold()
                            .FontSize(TamanioBase + 2);
                    });

                derecha.Item().PaddingTop(6).Column(cae =>
                {
                    cae.Spacing(2);
                    Dato(cae, "CAE", datos.Cae);
                    Dato(cae, "Vencimiento del CAE", datos.CaeVencimiento);
                });
            });

            static void Importe(ColumnDescriptor columna, string rotulo, string valor) =>
                columna.Item().Row(renglon =>
                {
                    renglon.RelativeItem().Text(rotulo);
                    renglon.RelativeItem().AlignRight().Text(valor);
                });
        });

    /// <summary>Un par rótulo-valor, que es la forma en que se lee casi todo el comprobante.</summary>
    private static void Dato(ColumnDescriptor columna, string rotulo, string valor) =>
        columna.Item().Text(texto =>
        {
            texto.Span($"{rotulo}: ").Bold();
            texto.Span(valor);
        });
}
