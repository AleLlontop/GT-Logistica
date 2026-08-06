using GT.Infrastructure.Archivos;

namespace GT.UnitTests.Choferes;

/// <summary>
/// FR-015a: PDF, JPG o PNG de hasta 10 MB, reconocidos por la <b>firma</b> del archivo.
///
/// El caso que justifica toda la clase es el último: un archivo con extensión <c>.pdf</c> que no es
/// un PDF. Validar por extensión o por el <c>Content-Type</c> que declara el navegador lo dejaría
/// pasar, porque las dos cosas las controla quien sube (research §3).
/// </summary>
public class ValidadorArchivoTests
{
    private static byte[] Pdf() => [.."%PDF-1.7"u8];

    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void Acepta_UnPdf()
    {
        var resultado = ValidadorArchivo.Validar(Pdf(), 1024);

        Assert.True(resultado.EsValido);
        Assert.Equal("application/pdf", resultado.TipoContenido);
    }

    [Fact]
    public void Acepta_UnJpeg()
    {
        var resultado = ValidadorArchivo.Validar(Jpeg(), 1024);

        Assert.True(resultado.EsValido);
        Assert.Equal("image/jpeg", resultado.TipoContenido);
    }

    [Fact]
    public void Acepta_UnPng()
    {
        var resultado = ValidadorArchivo.Validar(Png(), 1024);

        Assert.True(resultado.EsValido);
        Assert.Equal("image/png", resultado.TipoContenido);
    }

    /// <summary>El borde: 10 MB exactos entran, un byte más no.</summary>
    [Fact]
    public void Acepta_ExactamenteDiezMegas()
    {
        var resultado = ValidadorArchivo.Validar(Pdf(), ValidadorArchivo.TamanioMaximoEnBytes);

        Assert.True(resultado.EsValido);
    }

    [Fact]
    public void Rechaza_MasDeDiezMegas()
    {
        var resultado = ValidadorArchivo.Validar(Pdf(), ValidadorArchivo.TamanioMaximoEnBytes + 1);

        Assert.False(resultado.EsValido);
    }

    [Fact]
    public void Rechaza_UnArchivoVacio()
    {
        var resultado = ValidadorArchivo.Validar([], 0);

        Assert.False(resultado.EsValido);
    }

    /// <summary>
    /// Un ejecutable renombrado a <c>.pdf</c>. El validador no ve el nombre: ve que los primeros
    /// bytes son <c>MZ</c> y no <c>%PDF</c>.
    /// </summary>
    [Fact]
    public void Rechaza_UnArchivoQueDiceSerPdfYNoLoEs()
    {
        byte[] ejecutable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        var resultado = ValidadorArchivo.Validar(ejecutable, 2048);

        Assert.False(resultado.EsValido);
        Assert.Null(resultado.TipoContenido);
    }

    [Fact]
    public void Rechaza_UnArchivoDeTextoPlano()
    {
        var resultado = ValidadorArchivo.Validar("hola, no soy un pdf"u8, 19);

        Assert.False(resultado.EsValido);
    }

    /// <summary>
    /// La versión sobre un flujo deja el cursor al principio: quien guarda después tiene que poder
    /// escribir el archivo completo, no lo que sobró de la validación.
    /// </summary>
    [Fact]
    public async Task ValidarAsync_DejaElFlujoAlPrincipio()
    {
        var contenido = Pdf();
        using var flujo = new MemoryStream(contenido);

        var resultado = await ValidadorArchivo.ValidarAsync(flujo, contenido.Length);

        Assert.True(resultado.EsValido);
        Assert.Equal(0, flujo.Position);

        using var copia = new MemoryStream();
        await flujo.CopyToAsync(copia);
        Assert.Equal(contenido, copia.ToArray());
    }

    /// <summary>Un archivo más corto que la firma más larga no rompe: simplemente no es de los tres.</summary>
    [Fact]
    public async Task ValidarAsync_ConUnArchivoMasCortoQueLaFirma()
    {
        using var flujo = new MemoryStream([0x89, 0x50]);

        var resultado = await ValidadorArchivo.ValidarAsync(flujo, 2);

        Assert.False(resultado.EsValido);
    }
}
