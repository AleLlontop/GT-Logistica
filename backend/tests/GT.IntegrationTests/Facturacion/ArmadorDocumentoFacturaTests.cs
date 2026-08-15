using GT.Application.Facturacion;
using GT.Domain.Facturacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// El test de humo del generador de PDF, y <b>el que detecta la falta de <c>libfontconfig1</c> en CI</b>
/// (research §1, §15.3).
///
/// Es el único punto del módulo que falla en producción sin fallar antes: el backend compila, restaura,
/// arranca y sirve todo, y revienta recién al emitir la primera factura. Compilar bien no prueba nada
/// sobre una dependencia con requisitos nativos, así que hay que ejercitarla de verdad.
///
/// <b>Resuelve el armador del contenedor de la aplicación en vez de instanciarlo</b>, y no es un
/// detalle: <c>QuestPDF.Settings.License</c> es una configuración global que fija <c>Program.cs</c>.
/// Instanciando la clase a mano el test pasaría con la licencia sin declarar y la aplicación real
/// fallaría igual — que es justo el escenario que este test existe para descartar.
///
/// Es también el primer paso del recorrido de <c>quickstart.md</c>.
/// </summary>
public class ArmadorDocumentoFacturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>La firma de un PDF. Un archivo que no empieza así no es un PDF, tenga el tamaño que tenga.</summary>
    private static readonly byte[] FirmaPdf = "%PDF"u8.ToArray();

    private IArmadorDocumentoFactura Armador()
    {
        using var alcance = app.Services.CreateScope();

        return alcance.ServiceProvider.GetRequiredService<IArmadorDocumentoFactura>();
    }

    [Fact]
    public void GeneraUnPdfDeVerdadAPartirDeUnaFacturaEnMemoria()
    {
        // La entidad no existe en la base: es exactamente la entrada de la vista previa (research §2).
        var factura = DatosDePruebaFacturas.FacturaEnMemoria();

        var pdf = Armador().Armar(DatosDelDocumento.Desde(factura, logo: null));

        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 1024, "Un PDF de una factura con detalle no puede pesar menos de 1 KB.");
        Assert.Equal(FirmaPdf, pdf[..FirmaPdf.Length]);
    }

    /// <summary>
    /// FR-031j: la disposición es la misma para los tres tipos, así que los tres tienen que armarse.
    /// Una <c>Factura C</c> con IVA en cero no es un caso degenerado que el armador pueda saltear.
    /// </summary>
    [Theory]
    [InlineData(TipoComprobante.FacturaA)]
    [InlineData(TipoComprobante.FacturaB)]
    [InlineData(TipoComprobante.FacturaC)]
    public void ArmaLosTresTiposDeComprobante(TipoComprobante tipo)
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria(tipo: tipo);

        var pdf = Armador().Armar(DatosDelDocumento.Desde(factura, logo: null));

        Assert.NotEmpty(pdf);
    }

    /// <summary>
    /// FR-031g: sin logo el bloque del emisor se acomoda a su ausencia. El caso con logo va aparte
    /// porque es el que ejercita la decodificación de la imagen, que también es motor nativo.
    /// </summary>
    [Fact]
    public void ArmaConLogoCargado()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria();
        var logo = new LogoDelDocumento(PngDeUnPixel());

        var pdf = Armador().Armar(DatosDelDocumento.Desde(factura, logo));

        Assert.NotEmpty(pdf);
    }

    /// <summary>
    /// Las tres omisiones condicionales del documento —sin CBU, sin detalle, sin ingresos brutos—
    /// tienen que producir un documento válido y no un hueco ni una excepción (FR-031, bloques 6 y 9).
    /// </summary>
    [Fact]
    public void ArmaSinCbuYSinObservaciones()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria(cbu: null, detalle: null);

        var pdf = Armador().Armar(DatosDelDocumento.Desde(factura, logo: null));

        Assert.NotEmpty(pdf);
    }

    /// <summary>FR-031d: la leyenda y el motivo salen impresos en el documento regenerado al anular.</summary>
    [Fact]
    public void ArmaElDocumentoDeUnaFacturaAnuladaConSuMotivo()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria(
            estado: EstadoFactura.Anulada,
            motivoAnulacion: "Se facturó al cliente equivocado.");

        var datos = DatosDelDocumento.Desde(factura, logo: null);

        Assert.Equal("FACTURA ANULADA", datos.LeyendaAnulada);
        Assert.Equal("Se facturó al cliente equivocado.", datos.MotivoAnulacion);
        Assert.NotEmpty(Armador().Armar(datos));
    }

    /// <summary>
    /// FR-031e: una fila por viaje, nunca una única fila consolidada. Es lo que hace que la factura se
    /// explique por los viajes que la componen.
    /// </summary>
    [Fact]
    public void ElDetalleLlevaUnaFilaPorViaje()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria(viajes:
        [
            DatosDePruebaFacturas.ViajeEnMemoria(1041, 30_000m),
            DatosDePruebaFacturas.ViajeEnMemoria(1042, 30_000m),
            DatosDePruebaFacturas.ViajeEnMemoria(1043, 22_644.63m),
        ]);

        var datos = DatosDelDocumento.Desde(factura, logo: null);

        Assert.Equal(3, datos.Detalle.Count);
        Assert.Equal(["1041", "1042", "1043"], datos.Detalle.Select(fila => fila.Codigo));

        // El importe del viaje sale igual en `Precio unit.` y en `Importe`: la cantidad es 1 y no hay
        // bonificación, así que por definición coinciden (FR-031e).
        Assert.All(datos.Detalle, fila => Assert.Equal(fila.PrecioUnitario, fila.Importe));
    }

    /// <summary>
    /// Una factura con muchos viajes obliga al detalle a cortarse entre páginas repitiendo el
    /// encabezado (FR-031e). Acá lo que se verifica es que se arme sin caerse; que el encabezado se
    /// repita lo resuelve <c>table.Header</c> de la biblioteca.
    /// </summary>
    [Fact]
    public void ArmaUnaFacturaDeMuchosViajesEnVariasPaginas()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria(viajes:
            [.. Enumerable.Range(1, 60).Select(n => DatosDePruebaFacturas.ViajeEnMemoria(n, 12_500m))]);

        var pdf = Armador().Armar(DatosDelDocumento.Desde(factura, logo: null));

        Assert.NotEmpty(pdf);
    }

    /// <summary>
    /// <b>El documento es una función de la factura: dos armados de la misma entrada dan los mismos
    /// bytes.</b>
    ///
    /// Es lo que hace verificable a SC-007b. Sin fijar los metadatos, QuestPDF estampa el instante de
    /// generación y la comparación byte a byte entre la vista previa y el archivo guardado pasa o falla
    /// según si los dos cayeron en el mismo segundo — lo que la volvía una prueba que dependía del reloj.
    /// </summary>
    [Fact]
    public void Dos_ArmadosDeLaMismaFactura_DanLosMismosBytes()
    {
        var factura = DatosDePruebaFacturas.FacturaEnMemoria();
        var armador = Armador();

        var primero = armador.Armar(DatosDelDocumento.Desde(factura, logo: null));
        Thread.Sleep(1100);
        var segundo = armador.Armar(DatosDelDocumento.Desde(factura, logo: null));

        Assert.Equal(primero, segundo);
    }

    /// <summary>Un PNG válido de 1×1 píxel, para no depender de un archivo del repositorio.</summary>
    private static byte[] PngDeUnPixel() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");
}
