using System.Text.Json;
using GT.Api.Reportes;
using GT.Application.Autenticacion;
using GT.Application.Reportes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GT.UnitTests.Reportes;

/// <summary>
/// La traducción de un resultado a su respuesta HTTP: los <b>cuatro</b> cuerpos del contrato con su
/// texto exacto y su código, más las tres cabeceras del <c>200</c> por formato.
///
/// Es pura traducción: no necesita endpoint, ni base, ni aplicación levantada. Se ejecuta el
/// <c>IResult</c> contra un contexto vacío y se lee lo que escribió.
/// </summary>
public class RespuestasDeReporteTests
{
    private static readonly byte[] Contenido = [1, 2, 3, 4];

    // ── 400 formato_invalido ────────────────────────────────────────────────────────────────────

    /// <summary>Ausente, vacío y distinto de <c>pdf</c>/<c>excel</c>: los tres son el mismo rechazo.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("csv")]
    [InlineData("PDF")]
    public async Task FormatoInvalido_Es400ConSuCodigoYSuTexto(string? formato)
    {
        Assert.Null(FormatosDeReporte.Leer(formato));

        var (estado, cuerpo) = await EjecutarAsync(RespuestasDeReporte.FormatoInvalido());

        Assert.Equal(StatusCodes.Status400BadRequest, estado);
        Assert.Equal("formato_invalido", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(
            "El formato del reporte tiene que ser PDF o Excel.",
            cuerpo.GetProperty("mensaje").GetString());
    }

    // ── 403 sin_permiso ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El cuerpo que los cinco endpoints declaran como metadata y que el manejador de acceso denegado
    /// del Módulo 1 escribe. <b>Los dos permisos dan el mismo <c>403</c> y el mismo texto</b>:
    /// distinguirlos le diría a quien invoca la ruta a mano exactamente qué permiso le falta.
    /// </summary>
    [Fact]
    public void SinPermiso_TraeSuCodigoYSuTexto()
    {
        var cuerpo = RespuestasDeReporte.SinPermiso();

        Assert.Equal("sin_permiso", cuerpo.Codigo);
        Assert.Equal(
            "No tenés permiso para emitir reportes. Pedíselo a quien administra el sistema.",
            cuerpo.Mensaje);
    }

    // ── 409 tope_de_filas_superado ──────────────────────────────────────────────────────────────

    /// <summary>Lleva <c>filas</c> y <c>tope</c> <b>además</b> del mensaje ya armado.</summary>
    [Fact]
    public async Task TopeSuperado_Es409ConFilasTopeYMensaje()
    {
        var (estado, cuerpo) = await EjecutarAsync(RespuestasDeReporte.TopeSuperado(7412, 5000));

        Assert.Equal(StatusCodes.Status409Conflict, estado);
        Assert.Equal("tope_de_filas_superado", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(
            "El filtro dejó 7.412 filas y el tope de un reporte es 5.000. Acotá los filtros y volvé a intentar.",
            cuerpo.GetProperty("mensaje").GetString());
        Assert.Equal(7412, cuerpo.GetProperty("filas").GetInt32());
        Assert.Equal(5000, cuerpo.GetProperty("tope").GetInt32());
    }

    // ── 500 reporte_no_generado ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoGenerado_Es500ConSuCodigoYSuTexto()
    {
        var (estado, cuerpo) = await EjecutarAsync(RespuestasDeReporte.NoGenerado());

        Assert.Equal(StatusCodes.Status500InternalServerError, estado);
        Assert.Equal("reporte_no_generado", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(
            "No pudimos generar el reporte. Volvé a intentar en unos minutos.",
            cuerpo.GetProperty("mensaje").GetString());
    }

    // ── 200 y sus tres cabeceras ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(FormatoDeReporte.Pdf, "application/pdf", "inline", "viajes-2026-09-20-1432.pdf")]
    [InlineData(
        FormatoDeReporte.Excel,
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "attachment",
        "viajes-2026-09-20-1432.xlsx")]
    public async Task Entregado_TraeTipoDisposicionYNosniff(
        FormatoDeReporte formato,
        string tipoEsperado,
        string disposicionEsperada,
        string nombre)
    {
        var contexto = NuevoContexto();

        var resultado = RespuestasDeReporte.Traducir(
            contexto.Response,
            ResultadoReporte.Entregado(Contenido, nombre, formato));

        await resultado.ExecuteAsync(contexto);

        Assert.Equal(StatusCodes.Status200OK, contexto.Response.StatusCode);
        Assert.Equal(tipoEsperado, contexto.Response.ContentType);
        Assert.Equal(
            $"{disposicionEsperada}; filename=\"{nombre}\"",
            contexto.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("nosniff", contexto.Response.Headers.XContentTypeOptions.ToString());
    }

    /// <summary>La traducción también decide por el resultado, no sólo por quien la llama.</summary>
    [Fact]
    public async Task Traducir_ConTopeSuperado_Es409()
    {
        var contexto = NuevoContexto();

        var resultado = RespuestasDeReporte.Traducir(
            contexto.Response,
            ResultadoReporte.TopeSuperado(10, 3));

        await resultado.ExecuteAsync(contexto);

        Assert.Equal(StatusCodes.Status409Conflict, contexto.Response.StatusCode);
    }

    [Fact]
    public async Task Traducir_ConFormatoNoAdmitido_Es400()
    {
        var contexto = NuevoContexto();

        var resultado = RespuestasDeReporte.Traducir(
            contexto.Response,
            ResultadoReporte.FormatoNoAdmitido());

        await resultado.ExecuteAsync(contexto);

        Assert.Equal(StatusCodes.Status400BadRequest, contexto.Response.StatusCode);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    private static DefaultHttpContext NuevoContexto()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddOptions();

        return new DefaultHttpContext
        {
            RequestServices = servicios.BuildServiceProvider(),
            Response = { Body = new MemoryStream() },
        };
    }

    private static async Task<(int Estado, JsonElement Cuerpo)> EjecutarAsync(IResult resultado)
    {
        var contexto = NuevoContexto();

        await resultado.ExecuteAsync(contexto);

        contexto.Response.Body.Position = 0;

        using var lector = new StreamReader(contexto.Response.Body);
        var json = await lector.ReadToEndAsync();

        return (contexto.Response.StatusCode, JsonDocument.Parse(json).RootElement.Clone());
    }

    /// <summary>Para que el <c>using</c> de <see cref="ErrorResponse"/> no quede sin uso.</summary>
    [Fact]
    public void ElCuerpoDeErrorEsElDelSistema() =>
        Assert.IsAssignableFrom<ErrorResponse>(RespuestasDeReporte.SinPermiso());
}
