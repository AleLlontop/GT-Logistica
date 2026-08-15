using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Facturacion;

/// <summary>
/// Los tres permisos del módulo, verificados endpoint por endpoint (FR-066 a FR-068, SC-014).
///
/// <b>Ocultar el botón es una cortesía; la restricción es ésta.</b> Cada test invoca la acción a mano con
/// una cuenta que no la tiene y verifica el <c>403</c>: si esto pasara sólo por la pantalla, cualquiera con
/// una consola del navegador podría anular una factura (FR-068).
///
/// Es el módulo con la autorización más granular del sistema —tres permisos— y no agregó una línea de
/// maquinaria: el <c>PermisoHandler</c> y el catálogo de menú del Módulo 1 los absorbieron sin cambios
/// (research §7).
/// </summary>
public class PermisosFacturacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Password = "Permisos.1234";

    private async Task<HttpClient> ClienteConAsync(string username, params string[] permisos)
    {
        await app.CrearUsuarioConPermisosAsync(username, Password, permisos);

        return await app.CrearClienteAutenticadoAsync(username, Password);
    }

    /// <summary>Una factura para tener sobre qué invocar las escrituras.</summary>
    private async Task<int> UnaFacturaAsync()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var padron = await app.CrearClienteAsync();
        var factura = await app.CrearFacturaAsync(padron.Id);

        return factura.Id;
    }

    private static object CuerpoDeEmision(int clienteId)
    {
        var hoy = FechaHoyArgentina.Hoy();

        return new
        {
            clienteId,
            tipoComprobante = "facturaA",
            tipoFacturacion = "original",
            condicionDeVenta = "cuentaCorriente",
            mes = hoy.Month,
            anio = hoy.Year,
            fecha = hoy.ToString("yyyy-MM-dd"),
            numeroComprobante = DatosDePruebaFacturas.NumeroUnico(),
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            viajeIds = new[] { 1 },
        };
    }

    /// <summary>
    /// FR-066 a FR-068: <c>facturacion.consultar</c> a secas recibe <c>403</c> en emitir, corregir, cobrar
    /// y anular. <b>Las cuatro escrituras, una por una</b>: verificar sólo una dejaría las otras tres sin
    /// cubrir, y son las que dan miedo.
    /// </summary>
    [Fact]
    public async Task Solo_Consultar_Recibe403EnLasCuatroEscrituras()
    {
        var facturaId = await UnaFacturaAsync();
        var padron = await app.CrearClienteAsync();

        var cliente = await ClienteConAsync(
            "solo_consulta_facturas",
            CodigosPermiso.FacturacionConsultar);

        var hoy = FechaHoyArgentina.Hoy();

        // 1. Emitir.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync("/api/facturas", CuerpoDeEmision(padron.Id))).StatusCode);

        // 2. Corregir.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.PutAsJsonAsync($"/api/facturas/{facturaId}", new
            {
                cae = "75123456789012",
                caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
                vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
            })).StatusCode);

        // 3. Cobrar.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync(
                $"/api/facturas/{facturaId}/cobro",
                new { fechaCobro = hoy.ToString("yyyy-MM-dd") })).StatusCode);

        // 4. Anular.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync(
                $"/api/facturas/{facturaId}/anulacion",
                new { motivo = "Intento sin permiso." })).StatusCode);
    }

    /// <summary>
    /// Y <b>sí</b> puede leer todo: el listado, la ficha, el documento, el panel y los totales. Eso es lo
    /// que hace útil al permiso de consulta — mirar la cobranza no exige poder facturar (FR-066).
    /// </summary>
    [Fact]
    public async Task Solo_Consultar_PuedeLeerLasCincoLecturas()
    {
        var facturaId = await UnaFacturaAsync();

        var cliente = await ClienteConAsync(
            "solo_lee_facturas",
            CodigosPermiso.FacturacionConsultar);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/facturas")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await cliente.GetAsync($"/api/facturas/{facturaId}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await cliente.GetAsync("/api/facturas/vencimientos")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await cliente.GetAsync(
                "/api/facturas/totales?desde=2026-01-01&hasta=2026-12-31")).StatusCode);

        // El documento: la factura de prueba apunta a un archivo que no existe en el volumen, así que
        // responde 404 — lo que importa es que **no** sea 403.
        var documento = await cliente.GetAsync($"/api/facturas/{facturaId}/documento");
        Assert.NotEqual(HttpStatusCode.Forbidden, documento.StatusCode);
    }

    /// <summary>
    /// FR-067: <c>facturacion.gestionar</c> <b>sin</b> <c>facturacion.anular</c> recibe <c>403</c>
    /// <b>sólo en anular</b>. Es el tercer nivel de granularidad del sistema, y lo que este test protege es
    /// que sea exactamente eso: un permiso menos, no un rol distinto.
    /// </summary>
    [Fact]
    public async Task Gestionar_SinAnular_Recibe403SoloEnAnular()
    {
        var facturaId = await UnaFacturaAsync();

        var cliente = await ClienteConAsync(
            "gestiona_sin_anular",
            CodigosPermiso.FacturacionGestionar,
            CodigosPermiso.FacturacionConsultar);

        var hoy = FechaHoyArgentina.Hoy();

        // Corregir: sí puede.
        var correccion = await cliente.PutAsJsonAsync($"/api/facturas/{facturaId}", new
        {
            cae = "75123456789012",
            caeVencimiento = hoy.AddDays(10).ToString("yyyy-MM-dd"),
            vencimientoPago = hoy.AddDays(30).ToString("yyyy-MM-dd"),
        });

        Assert.NotEqual(HttpStatusCode.Forbidden, correccion.StatusCode);

        // Cobrar: sí puede.
        var cobro = await cliente.PostAsJsonAsync(
            $"/api/facturas/{facturaId}/cobro",
            new { fechaCobro = hoy.ToString("yyyy-MM-dd") });

        Assert.NotEqual(HttpStatusCode.Forbidden, cobro.StatusCode);

        // Anular: **no**.
        var anulacion = await cliente.PostAsJsonAsync(
            $"/api/facturas/{facturaId}/anulacion",
            new { motivo = "Intento sin el permiso de anular." });

        Assert.Equal(HttpStatusCode.Forbidden, anulacion.StatusCode);
    }

    /// <summary>
    /// La configuración de la empresa emisora exige <c>facturacion.gestionar</c>: no es una pantalla de
    /// lectura para nadie (FR-067).
    /// </summary>
    [Fact]
    public async Task La_EmpresaEmisora_SeEditaSoloConGestionar()
    {
        var soloConsulta = await ClienteConAsync(
            "consulta_emisora_permisos",
            CodigosPermiso.FacturacionConsultar);

        var respuesta = await soloConsulta.PutAsJsonAsync("/api/facturacion/empresa-emisora", new
        {
            razonSocial = "Intento",
            cuit = "30712345671",
            domicilio = "Sin permiso",
            condicionIva = "IVA Responsable Inscripto",
        });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /// <summary>
    /// Los insumos del alta —facturables, vista previa y anuladas sin reemplazo— exigen
    /// <c>facturacion.gestionar</c>: no son pantallas de lectura, son partes del alta (FR-067).
    /// </summary>
    [Fact]
    public async Task Los_InsumosDelAlta_ExigenGestionar()
    {
        var padron = await app.CrearClienteAsync();

        var soloConsulta = await ClienteConAsync(
            "consulta_insumos",
            CodigosPermiso.FacturacionConsultar);

        var hoy = FechaHoyArgentina.Hoy();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await soloConsulta.GetAsync(
                $"/api/facturas/facturables?clienteId={padron.Id}&mes={hoy.Month}&anio={hoy.Year}"))
                .StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await soloConsulta.GetAsync(
                $"/api/facturas/anuladas-sin-reemplazo?clienteId={padron.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await soloConsulta.PostAsJsonAsync(
                "/api/facturas/vista-previa",
                CuerpoDeEmision(padron.Id))).StatusCode);
    }

    /// <summary>
    /// Sin ningún permiso del módulo, <b>ni las lecturas responden</b>. Se usa un rol con sólo
    /// <c>viajes.consultar</c>, que es lo que tiene Tráfico sobre este módulo (research §7).
    /// </summary>
    [Fact]
    public async Task Sin_NingunPermisoDelModulo_NadaResponde()
    {
        var facturaId = await UnaFacturaAsync();

        var cliente = await ClienteConAsync("sin_permisos_facturacion", CodigosPermiso.ViajesConsultar);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/facturas")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.GetAsync($"/api/facturas/{facturaId}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await cliente.GetAsync("/api/facturas/vencimientos")).StatusCode);
    }

    /// <summary>
    /// El menú devuelve las entradas que corresponden a cada permiso, resueltas por el servidor (FR-066).
    ///
    /// <b>Es <c>CatalogoOpcionesMenu</c> del Módulo 1 absorbiendo tres permisos nuevos sin una línea de
    /// código propio</b>: la confirmación de que autorizar por permiso y nunca por rol escala
    /// (research §7).
    /// </summary>
    [Fact]
    public async Task El_Menu_DevuelveLasEntradasQueCorrespondenACadaPermiso()
    {
        var soloConsulta = await ClienteConAsync(
            "menu_consulta_facturacion",
            CodigosPermiso.FacturacionConsultar);

        var deConsulta = await LeerMenuAsync(soloConsulta);

        // Las tres de lectura, y no la de configuración.
        Assert.Contains("facturas", deConsulta);
        Assert.Contains("vencimientos-facturas", deConsulta);
        Assert.Contains("totales-facturados", deConsulta);
        Assert.DoesNotContain("empresa-emisora", deConsulta);

        var soloGestion = await ClienteConAsync(
            "menu_gestion_facturacion",
            CodigosPermiso.FacturacionGestionar);

        var deGestion = await LeerMenuAsync(soloGestion);

        // La de configuración, y ninguna de lectura: los permisos no son niveles ordenados, y quien
        // gestiona tiene los dos porque el sembrador se los da por separado (FR-066).
        Assert.Contains("empresa-emisora", deGestion);
        Assert.DoesNotContain("facturas", deGestion);

        var sinNada = await ClienteConAsync("menu_sin_facturacion", CodigosPermiso.ViajesConsultar);
        var vacio = await LeerMenuAsync(sinNada);

        Assert.DoesNotContain("facturas", vacio);
        Assert.DoesNotContain("empresa-emisora", vacio);
        Assert.DoesNotContain("vencimientos-facturas", vacio);
        Assert.DoesNotContain("totales-facturados", vacio);
    }

    /// <summary>
    /// El reparto que sembró el Módulo 6 (FR-066, research §7): el administrador tiene los tres,
    /// Administración gestiona y consulta, Gerencia sólo consulta, y <b>Tráfico ninguno</b> — facturar es
    /// tarea administrativa.
    /// </summary>
    [Fact]
    public async Task El_RepartoPorRol_EsElDeResearch7()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var roles = await cliente.GetFromJsonAsync<List<RolConPermisosLeido>>("/api/roles");

        var permisosDe = (string codigo) => roles!
            .Single(rol => rol.Codigo == codigo)
            .PermisosPorModulo
            .Where(modulo => modulo.Modulo == "Facturación")
            .SelectMany(modulo => modulo.Permisos.Select(permiso => permiso.Codigo))
            .ToHashSet();

        var administrador = permisosDe(CodigosRol.AdministradorSistema);
        Assert.Contains(CodigosPermiso.FacturacionGestionar, administrador);
        Assert.Contains(CodigosPermiso.FacturacionConsultar, administrador);
        Assert.Contains(CodigosPermiso.FacturacionAnular, administrador);

        var administracion = permisosDe(CodigosRol.Administracion);
        Assert.Contains(CodigosPermiso.FacturacionGestionar, administracion);
        Assert.Contains(CodigosPermiso.FacturacionConsultar, administracion);
        Assert.DoesNotContain(CodigosPermiso.FacturacionAnular, administracion);

        var gerencia = permisosDe(CodigosRol.Gerencia);
        Assert.Equal([CodigosPermiso.FacturacionConsultar], gerencia);

        // Tráfico no recibe ninguno: es el primer permiso de escritura del sistema que no le llega.
        Assert.Empty(permisosDe(CodigosRol.Trafico));
    }

    private static async Task<List<string>> LeerMenuAsync(HttpClient cliente)
    {
        var sesion = await cliente.GetFromJsonAsync<SesionLeida>("/api/auth/sesion");

        return [.. sesion!.OpcionesMenu.Select(opcion => opcion.Codigo)];
    }
}

/// <summary>Lo justo de un rol para verificar el reparto de permisos que este archivo mira.</summary>
public record RolConPermisosLeido(
    string Codigo,
    string Nombre,
    List<ModuloDePermisosLeido> PermisosPorModulo);

public record ModuloDePermisosLeido(string Modulo, List<PermisoLeidoDeFacturacion> Permisos);

public record PermisoLeidoDeFacturacion(string Codigo, string Descripcion);
