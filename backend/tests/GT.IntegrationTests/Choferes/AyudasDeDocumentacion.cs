using System.Net.Http.Headers;
using GT.Application.Choferes.Documentacion;
using GT.Domain.Choferes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Choferes;

/// <summary>Un almacén que nunca escribe, para ejercitar el camino de FR-015e.</summary>
public class AlmacenQueSiempreFalla : IAlmacenDeArchivos
{
    public Task<string> GuardarAsync(Stream contenido, CancellationToken cancelacion = default) =>
        throw new ArchivoNoGuardadoException("El volumen no está disponible.");

    public Task<Stream?> AbrirAsync(string rutaRelativa, CancellationToken cancelacion = default) =>
        Task.FromResult<Stream?>(null);

    public Task BorrarAsync(string rutaRelativa, CancellationToken cancelacion = default) =>
        Task.CompletedTask;
}

public static class AyudasDeDocumentacion
{
    /// <summary>Un PDF mínimo: lo que importa es que empiece con la firma que el validador busca.</summary>
    public static byte[] Pdf(string marca = "uno") =>
        [.."%PDF-1.7\n% documento de prueba "u8, ..System.Text.Encoding.UTF8.GetBytes(marca)];

    /// <summary>Un archivo que dice ser PDF por su nombre y no lo es (FR-015a).</summary>
    public static byte[] NoEsUnPdf() => [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    /// <summary>
    /// Arma el <c>multipart/form-data</c> del formulario de documento. Las fechas van relativas a
    /// hoy en hora de Argentina, que es contra lo que se calcula el estado (FR-017a).
    /// </summary>
    public static MultipartFormDataContent Formulario(
        int tipoId,
        int diasHastaVencimiento,
        string numero = "ABC-123",
        byte[]? archivo = null,
        string nombreDeArchivo = "escaneo.pdf",
        DateOnly? fechaEmision = null,
        DateOnly? fechaVencimiento = null)
    {
        var hoy = FechaHoyArgentina.Hoy();
        var emision = fechaEmision ?? hoy.AddYears(-1);
        var vencimiento = fechaVencimiento ?? hoy.AddDays(diasHastaVencimiento);

        var contenido = new MultipartFormDataContent
        {
            { new StringContent(tipoId.ToString()), "documentacionTipoId" },
            { new StringContent(numero), "numero" },
            { new StringContent(emision.ToString("yyyy-MM-dd")), "fechaEmision" },
            { new StringContent(vencimiento.ToString("yyyy-MM-dd")), "fechaVencimiento" },
        };

        if (archivo is not null)
        {
            var parte = new ByteArrayContent(archivo);
            parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            contenido.Add(parte, "archivo", nombreDeArchivo);
        }

        return contenido;
    }

    /// <summary>
    /// Una vista de la aplicación con el almacén de archivos siempre fallando. Comparte la misma
    /// base que la fixture original, así que lo preparado antes sigue estando.
    /// </summary>
    public static WebApplicationFactory<Program> ConAlmacenQueFalla(this AplicacionDePrueba app) =>
        app.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
                servicios.AddSingleton<IAlmacenDeArchivos, AlmacenQueSiempreFalla>()));
}

/// <summary>Lo que devuelve el backend por cada documento (`contracts/choferes-api.yaml`).</summary>
public record DocumentoLeido(
    int Id,
    TipoLeido Tipo,
    string Numero,
    string FechaEmision,
    string FechaVencimiento,
    string Estado,
    bool EsVigenteDelTipo,
    int DiasHastaVencimiento,
    bool TieneArchivo,
    string? ArchivoNombre);

public record TipoLeido(int Id, string Nombre);

public record ErrorLeido(string Codigo, string Mensaje, string? Campo);
