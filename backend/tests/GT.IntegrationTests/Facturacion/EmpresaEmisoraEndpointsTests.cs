using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// Los cinco endpoints de la empresa emisora, de punta a punta (US1, FR-001 a FR-004).
/// </summary>
public class EmpresaEmisoraEndpointsTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Ruta = "/api/facturacion/empresa-emisora";

    private static object Cuerpo(
        string razonSocial = "G&T Logística S.R.L.",
        string cuit = "30712345671",
        string domicilio = "Av. Pellegrini 1234, Rosario",
        string condicionIva = "IVA Responsable Inscripto",
        string? ingresosBrutos = "902-123456-7",
        string? puntoDeVenta = "0014",
        string? cbu = "0170099220000067797470",
        string? telefono = "0341-444-4444",
        string? email = "administracion@gtlogistica.com.ar") => new
        {
            razonSocial,
            cuit,
            domicilio,
            condicionIva,
            ingresosBrutos,
            inicioActividades = "2018-03-01",
            puntoDeVenta,
            cbu,
            telefono,
            email,
        };

    /// <summary>
    /// Cada test necesita partir de un estado conocido, y la tabla tiene una sola fila para toda la
    /// aplicación: no alcanza con crear datos nuevos como en los otros módulos.
    /// </summary>
    private Task VaciarConfiguracionAsync() => app.EnLaBaseAsync(async contexto =>
    {
        await contexto.EmpresaEmisora.ExecuteDeleteAsync();
    });

    // ── El GET sin fila (US1 esc. 1) ────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Responde <c>200</c>, no <c>404</c></b>: la ausencia de la fila no es un error sino el punto
    /// de partida del sistema recién instalado, y lo que la pantalla tiene que mostrar es el formulario
    /// vacío con el mensaje que dice qué falta (research §12).
    /// </summary>
    [Fact]
    public async Task ElGetSinFilaRespondeConfiguradaEnFalsoYLosCuatroFaltantes()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var leida = await respuesta.Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();

        Assert.NotNull(leida);
        Assert.False(leida!.Configurada);
        Assert.Equal(["razón social", "CUIT", "domicilio", "condición de IVA"], leida.Faltantes);
        Assert.Null(leida.RazonSocial);
        Assert.Null(leida.Logo);
    }

    // ── El PUT crea y después actualiza (FR-001) ────────────────────────────────────────────────

    [Fact]
    public async Task ElPutCreaLaFilaLaPrimeraVezYLaActualizaDespues()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var creacion = await cliente.PutAsJsonAsync(Ruta, Cuerpo());
        Assert.Equal(HttpStatusCode.OK, creacion.StatusCode);

        var creada = await creacion.Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();
        Assert.True(creada!.Configurada);
        Assert.Empty(creada.Faltantes);
        Assert.Equal("G&T Logística S.R.L.", creada.RazonSocial);

        var actualizacion = await cliente.PutAsJsonAsync(
            Ruta,
            Cuerpo(domicilio: "Bv. Oroño 500, Rosario"));

        Assert.Equal(HttpStatusCode.OK, actualizacion.StatusCode);

        var actualizada = await actualizacion.Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();
        Assert.Equal("Bv. Oroño 500, Rosario", actualizada!.Domicilio);

        // Sigue habiendo **una** fila: el segundo PUT actualizó, no insertó.
        var cantidad = await app.ConAlcanceAsync(contexto => contexto.EmpresaEmisora.CountAsync());
        Assert.Equal(1, cantidad);
    }

    /// <summary>
    /// FR-002: se guarda normalizado a sólo dígitos. Escribir <c>30-71234567-1</c> es válido, y lo que
    /// queda en la columna es lo que después sale impreso en el comprobante.
    /// </summary>
    [Fact]
    public async Task ElCuitConGuionesSeGuardaNormalizado()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(Ruta, Cuerpo(cuit: "30-71234567-1"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var leida = await respuesta.Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();
        Assert.Equal("30712345671", leida!.Cuit);
    }

    [Fact]
    public async Task ElCuitInvalidoSeRechazaConSuCodigo()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        // Once dígitos con el verificador equivocado: el que cierra para 3071234567 es el 1.
        var respuesta = await cliente.PutAsJsonAsync(Ruta, Cuerpo(cuit: "30712345670"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("cuit_invalido", error!.Codigo);
        Assert.Equal("cuit", error.Campo);
    }

    [Fact]
    public async Task ElObligatorioVacioSeRechazaNombrandoloYSinCrearNada()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(Ruta, Cuerpo(domicilio: "   "));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
        Assert.Equal("domicilio", error.Campo);
        Assert.Equal("Completá domicilio para poder guardar.", error.Mensaje);

        var cantidad = await app.ConAlcanceAsync(contexto => contexto.EmpresaEmisora.CountAsync());
        Assert.Equal(0, cantidad);
    }

    /// <summary>
    /// Los seis opcionales pueden quedar vacíos, y vacíos se guardan como nulos: la banda de CBU del
    /// documento se omite comparando contra nulo (FR-002, FR-031).
    /// </summary>
    [Fact]
    public async Task LosOpcionalesVaciosSeGuardanComoNulos()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        await cliente.PutAsJsonAsync(Ruta, new
        {
            razonSocial = "G&T Logística S.R.L.",
            cuit = "30712345671",
            domicilio = "Av. Pellegrini 1234",
            condicionIva = "IVA Responsable Inscripto",
            ingresosBrutos = "",
            puntoDeVenta = (string?)null,
            cbu = "   ",
            telefono = (string?)null,
            email = "",
        });

        var empresa = await app.ConAlcanceAsync(contexto =>
            contexto.EmpresaEmisora.AsNoTracking().FirstAsync());

        Assert.Null(empresa.IngresosBrutos);
        Assert.Null(empresa.PuntoDeVenta);
        Assert.Null(empresa.Cbu);
        Assert.Null(empresa.Telefono);
        Assert.Null(empresa.Email);
    }

    // ── El logo (FR-003, FR-004) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>El tipo se valida por la firma del archivo</b>, no por la extensión ni por el
    /// <c>Content-Type</c> declarado: las dos cosas las controla quien sube. Un PDF válido —que el
    /// validador compartido acepta, porque los Módulos 3 y 4 lo necesitan— se rechaza acá igual, porque
    /// el que decide qué tipos admite es este caso de uso (FR-003, research §6).
    /// </summary>
    [Fact]
    public async Task ElLogoRechazaUnPdfAunqueSeLlamePng()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PutAsJsonAsync(Ruta, Cuerpo());

        var respuesta = await SubirAsync(cliente, "%PDF-1.7 contenido"u8.ToArray(), "logo.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("archivo_no_admitido", error!.Codigo);
        Assert.Equal(
            "Ese archivo no es una imagen JPG ni PNG. La configuración quedó sin cambios.",
            error.Mensaje);

        var empresa = await app.ConAlcanceAsync(contexto =>
            contexto.EmpresaEmisora.AsNoTracking().FirstAsync());
        Assert.Null(empresa.LogoRuta);
    }

    /// <summary>
    /// Un PNG de verdad se acepta aunque el nombre diga otra cosa, porque lo que manda es la firma. Y
    /// se sirve **en línea** con el tipo deducido, no con el declarado (convención [003]).
    /// </summary>
    [Fact]
    public async Task ElLogoSeSubeSeSirveEnLineaYSeReemplaza()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PutAsJsonAsync(Ruta, Cuerpo());

        var subida = await SubirAsync(cliente, PngDeUnPixel(), "logo-gt.txt", "text/plain");
        Assert.Equal(HttpStatusCode.OK, subida.StatusCode);

        var leida = await subida.Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();
        Assert.NotNull(leida!.Logo);
        Assert.Equal("logo-gt.txt", leida.Logo!.Nombre);
        Assert.Equal("/api/facturacion/empresa-emisora/logo", leida.Logo.Url);

        var servido = await cliente.GetAsync($"{Ruta}/logo");
        Assert.Equal(HttpStatusCode.OK, servido.StatusCode);
        Assert.Equal("image/png", servido.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", servido.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(["nosniff"], servido.Headers.GetValues("X-Content-Type-Options"));

        var rutaAnterior = await RutaDelLogoAsync();

        // Reemplazar borra el archivo anterior **después** de confirmar la fila (convención [003]).
        var reemplazo = await SubirAsync(cliente, JpegDeUnPixel(), "nuevo.jpg", "image/jpeg");
        Assert.Equal(HttpStatusCode.OK, reemplazo.StatusCode);

        var rutaNueva = await RutaDelLogoAsync();
        Assert.NotEqual(rutaAnterior, rutaNueva);

        var servidoNuevo = await cliente.GetAsync($"{Ruta}/logo");
        Assert.Equal("image/jpeg", servidoNuevo.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// FR-004: quitar el logo deja la configuración completa y las facturas se siguen emitiendo. Es
    /// <b>idempotente</b> y no pide confirmación aparte (precedente [004]).
    /// </summary>
    [Fact]
    public async Task QuitarElLogoEsIdempotenteYDejaLaConfiguracionCompleta()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PutAsJsonAsync(Ruta, Cuerpo());
        await SubirAsync(cliente, PngDeUnPixel(), "logo.png", "image/png");

        var primera = await cliente.DeleteAsync($"{Ruta}/logo");
        Assert.Equal(HttpStatusCode.NoContent, primera.StatusCode);

        // Idempotente: quitar un logo que ya no está responde 204 igual.
        var segunda = await cliente.DeleteAsync($"{Ruta}/logo");
        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);

        var leida = await (await cliente.GetAsync(Ruta))
            .Content.ReadFromJsonAsync<EmpresaEmisoraLeida>();

        Assert.Null(leida!.Logo);
        Assert.True(leida.Configurada);

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync($"{Ruta}/logo")).StatusCode);
    }

    [Fact]
    public async Task ElLogoNoSePuedeSubirSinLaConfiguracionCargada()
    {
        await VaciarConfiguracionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await SubirAsync(cliente, PngDeUnPixel(), "logo.png", "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFacturaLeido>();
        Assert.Equal("empresa_emisora_incompleta", error!.Codigo);
    }

    // ── Los cinco endpoints y sus permisos (FR-066 a FR-068) ────────────────────────────────────

    /// <summary>
    /// Las cuatro escrituras exigen <c>facturacion.gestionar</c>. Quien sólo consulta las invoca a mano
    /// y recibe <c>403</c>: ocultar el botón es una cortesía, la restricción es ésta (FR-068, SC-014).
    /// </summary>
    [Fact]
    public async Task LasEscriturasExigenGestionarYQuienSoloConsultaRecibe403()
    {
        await VaciarConfiguracionAsync();
        var administrador = await app.CrearClienteAutenticadoAsync();
        await administrador.PutAsJsonAsync(Ruta, Cuerpo());

        const string password = "Consulta.1234";
        await app.CrearUsuarioConPermisosAsync(
            "solo_consulta_emisora",
            password,
            CodigosPermiso.FacturacionConsultar);

        var soloConsulta = await app.CrearClienteAutenticadoAsync("solo_consulta_emisora", password);

        // El GET sí: la ficha de una factura lo consume y es de lectura.
        Assert.Equal(HttpStatusCode.OK, (await soloConsulta.GetAsync(Ruta)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await soloConsulta.PutAsJsonAsync(Ruta, Cuerpo())).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SubirAsync(soloConsulta, PngDeUnPixel(), "logo.png", "image/png")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await soloConsulta.DeleteAsync($"{Ruta}/logo")).StatusCode);
    }

    /// <summary>
    /// Sin ninguno de los permisos de facturación, incluso el <c>GET</c> responde <c>403</c>. Se usa un
    /// rol de prueba con sólo <c>viajes.consultar</c>, que es lo que tiene Tráfico sobre este módulo.
    /// </summary>
    [Fact]
    public async Task SinPermisosDeFacturacionNiElGetResponde()
    {
        const string password = "Trafico.1234";
        await app.CrearUsuarioConPermisosAsync(
            "sin_facturacion",
            password,
            CodigosPermiso.ViajesConsultar);

        var cliente = await app.CrearClienteAutenticadoAsync("sin_facturacion", password);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync($"{Ruta}/logo")).StatusCode);
    }

    /// <summary>
    /// Las cuatro entradas de menú del módulo, resueltas por permiso por el servidor. Es el
    /// <c>CatalogoOpcionesMenu</c> del Módulo 1 absorbiendo tres permisos nuevos sin una línea de
    /// código propio (FR-066, research §7).
    /// </summary>
    [Fact]
    public async Task ElMenuTraeLaEntradaDeEmpresaEmisoraSoloConGestionar()
    {
        const string password = "Consulta.1234";
        await app.CrearUsuarioConPermisosAsync(
            "menu_solo_consulta",
            password,
            CodigosPermiso.FacturacionConsultar);

        var soloConsulta = await app.CrearClienteAutenticadoAsync("menu_solo_consulta", password);
        var menuDeConsulta = await LeerMenuAsync(soloConsulta);

        Assert.Contains("facturas", menuDeConsulta);
        Assert.Contains("vencimientos-facturas", menuDeConsulta);
        Assert.Contains("totales-facturados", menuDeConsulta);
        Assert.DoesNotContain("empresa-emisora", menuDeConsulta);

        var administrador = await app.CrearClienteAutenticadoAsync();
        Assert.Contains("empresa-emisora", await LeerMenuAsync(administrador));
    }

    private static async Task<List<string>> LeerMenuAsync(HttpClient cliente)
    {
        var sesion = await cliente.GetFromJsonAsync<SesionLeida>("/api/auth/sesion");

        return [.. sesion!.OpcionesMenu.Select(opcion => opcion.Codigo)];
    }

    private Task<string?> RutaDelLogoAsync() => app.ConAlcanceAsync(async contexto =>
        (await contexto.EmpresaEmisora.AsNoTracking().FirstAsync()).LogoRuta);

    private static Task<HttpResponseMessage> SubirAsync(
        HttpClient cliente,
        byte[] contenido,
        string nombre,
        string tipoDeclarado)
    {
        var formulario = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(contenido);

        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipoDeclarado);
        formulario.Add(archivo, "archivo", nombre);

        return cliente.PutAsync($"{Ruta}/logo", formulario);
    }

    private static byte[] PngDeUnPixel() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");

    /// <summary>
    /// Un archivo con la <b>firma de un JPEG</b>: <c>FF D8 FF E0</c> más la cabecera JFIF.
    ///
    /// Alcanza, y es exactamente lo que este test verifica: el sistema decide qué tipo es un archivo
    /// <b>por su firma</b> y no por la extensión ni por el <c>Content-Type</c> declarado (FR-003). Un
    /// JPEG completo no agregaría nada — el logo no se decodifica al subirlo, sólo se guarda.
    /// </summary>
    private static byte[] JpegDeUnPixel() =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];
}

/// <summary>Lo justo de la sesión para verificar el menú, que es lo que este test mira.</summary>
public record SesionLeida(string Username, List<OpcionMenuLeida> OpcionesMenu, List<string> Permisos);

public record OpcionMenuLeida(string Codigo, string Etiqueta, string Ruta);
