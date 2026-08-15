using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Los dos cambios de comportamiento que el Módulo 6 introduce en el Módulo 5 (FR-052, FR-055, FR-055a).
///
/// <b>Un viaje <c>facturado</c> es inmutable para todos los roles</b>, con el mismo alcance que ya regía
/// para <c>rendido</c>. Y eso se logró <b>sin tocar ninguno de los cinco caminos de escritura</b>: los
/// cinco ya consultaban <c>EstadoTerminal.Rechazo</c>, al que se le agregó un caso (research §8.3).
/// </summary>
public class ViajeFacturadoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDePrueba = "Prueba.1234";

    // ── FR-055a: el remito pasa a ser obligatorio para rendir ────────────────────────────────────

    /// <summary>
    /// <b>El único cambio de comportamiento sobre una operación existente del Módulo 5.</b> Rendir sin
    /// remito se rechaza con <c>400</c> marcando el campo; con remito procede igual que antes.
    ///
    /// Sigue siendo opcional en <c>pendiente</c> y en <c>en curso</c>: lo que cambia es la puerta de
    /// salida, no el alta.
    /// </summary>
    [Fact]
    public async Task Rendir_SinRemito_SeRechazaYConRemitoProcede()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        // Sin remito, explícitamente: el default del helper trae uno.
        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            asignado: true,
            importe: 120_000m,
            numeroRemito: "");

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Viajes
                .Where(v => v.Id == viaje.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(v => v.NumeroRemito, (string?)null));
        });

        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        var sinRemito = await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null);

        Assert.Equal(HttpStatusCode.BadRequest, sinRemito.StatusCode);

        var error = await sinRemito.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("remito_requerido", error!.Codigo);
        Assert.Equal("numeroRemito", error.Campo);
        Assert.Equal(
            "Cargá el número de remito antes de rendir el viaje: sale impreso en el detalle de la " +
            "factura.",
            error.Mensaje);

        // El viaje quedó exactamente como estaba: el rechazo no lo movió.
        Assert.Equal(EstadoViaje.EnCurso, (await app.RecargarViajeAsync(viaje.Id))!.Estado);

        // Con el remito cargado, rinde.
        var enCurso = await app.RecargarViajeAsync(viaje.Id);

        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = enCurso!.ClienteId,
            fecha = enCurso.Fecha.ToString("yyyy-MM-dd"),
            origen = enCurso.Origen,
            destino = enCurso.Destino,
            numeroRemito = ArmadoDeEscenarios.RemitoUnico(),
            importe = enCurso.Importe,
        });

        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);

        var conRemito = await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null);

        Assert.Equal(HttpStatusCode.OK, conRemito.StatusCode);
        Assert.Equal(EstadoViaje.Rendido, (await app.RecargarViajeAsync(viaje.Id))!.Estado);
    }

    // ── FR-052: los cinco caminos de escritura, cerrados para todos los roles ────────────────────

    /// <summary>
    /// SC-013: un viaje <c>facturado</c> rechaza <b>los cinco caminos de escritura</b> del Módulo 5
    /// —editar, asignar, poner en curso, rendir y anular— <b>para todos los roles</b>, incluido
    /// Administrador del sistema.
    ///
    /// Se prueba con los dos roles que tienen <c>viajes.gestionar</c>: si alguno pudiera, la regla no
    /// sería "para todos los roles" sino "para casi todos".
    /// </summary>
    [Theory]
    [InlineData(CodigosRol.AdministradorSistema)]
    [InlineData(CodigosRol.Trafico)]
    public async Task Un_ViajeFacturado_RechazaLosCincoCaminosDeEscritura(string codigoRol)
    {
        var (viajeId, escenario) = await ViajeFacturadoAsync();

        var cliente = codigoRol == CodigosRol.AdministradorSistema
            ? await app.CrearClienteAutenticadoAsync()
            : await ClienteConRolAsync(codigoRol);

        var viaje = await app.RecargarViajeAsync(viajeId);

        // 1. Editar.
        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{viajeId}", new
        {
            clienteId = viaje!.ClienteId,
            fecha = viaje.Fecha.ToString("yyyy-MM-dd"),
            origen = "Otro origen",
            destino = viaje.Destino,
            numeroRemito = viaje.NumeroRemito,
            importe = 999_999m,
        });

        await AssertRechazadoAsync(edicion, viaje.Numero);

        // 2. Asignar. **Rechaza con su código propio y no con el genérico**, y está bien: la asignación
        // ya traducía el estado terminal a `asignacion_no_permitida` desde el Módulo 5, para que el
        // mensaje hable de reasignar en vez de modificar. El Módulo 6 no tocó ese camino —research §8.3
        // fija que los cinco quedan cerrados sin modificar ninguno—, así que hereda esa traducción. Lo
        // que FR-052 exige es que rechace, y rechaza nombrando el estado.
        var asignacion = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viajeId}/asignacion",
            new { choferId = escenario.ChoferId, vehiculoId = escenario.VehiculoId });

        Assert.Equal(HttpStatusCode.Conflict, asignacion.StatusCode);

        var errorDeAsignacion = await asignacion.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("asignacion_no_permitida", errorDeAsignacion!.Codigo);
        Assert.Equal(
            $"El viaje {viaje.Numero} está facturado y no se puede reasignar.",
            errorDeAsignacion.Mensaje);

        // 3. Poner en curso.
        await AssertRechazadoAsync(
            await cliente.PostAsync($"/api/viajes/{viajeId}/en-curso", null),
            viaje.Numero);

        // 4. Rendir.
        await AssertRechazadoAsync(
            await cliente.PostAsync($"/api/viajes/{viajeId}/rendicion", null),
            viaje.Numero);

        // 5. Anular.
        var anulacion = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viajeId}/anulacion",
            new { motivo = "Intento de anular un viaje facturado." });

        await AssertRechazadoAsync(anulacion, viaje.Numero);

        // Nada cambió, en ninguno de los cinco intentos.
        var despues = await app.RecargarViajeAsync(viajeId);

        Assert.Equal(EstadoViaje.Facturado, despues!.Estado);
        Assert.Equal(viaje.Origen, despues.Origen);
        Assert.Equal(viaje.Importe, despues.Importe);
        Assert.NotNull(despues.FacturaId);
    }

    // ── FR-055: el estado y la factura en el listado y en la ficha ───────────────────────────────

    /// <summary>
    /// FR-055: la ficha y la fila muestran el <b>número y la fecha</b> de la factura del viaje. Sale de
    /// la navegación por <c>FacturaId</c>, no de columnas copiadas al viaje.
    /// </summary>
    [Fact]
    public async Task La_FichaYElListado_MuestranLaFacturaDelViaje()
    {
        var (viajeId, _) = await ViajeFacturadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleConFacturaLeido>(
            $"/api/viajes/{viajeId}");

        Assert.Equal("facturado", ficha!.Estado);
        Assert.NotNull(ficha.Factura);
        Assert.False(string.IsNullOrWhiteSpace(ficha.Factura!.Numero));
        Assert.False(string.IsNullOrWhiteSpace(ficha.Factura.Fecha));

        // El listado filtrado por `facturado` lo trae con su factura (FR-055).
        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesConFacturaLeida>(
            "/api/viajes?estado=facturado");

        var fila = Assert.Single(pagina!.Items, item => item.Id == viajeId);

        Assert.Equal("facturado", fila.Estado);
        Assert.Equal(ficha.Factura.Numero, fila.Factura!.Numero);
    }

    /// <summary>
    /// El filtro de estado acepta <c>facturado</c>, y el listado sin filtro <b>sí</b> los incluye: la
    /// exclusión por defecto del Módulo 5 es sólo de los anulados (FR-044, FR-055).
    /// </summary>
    [Fact]
    public async Task El_ListadoSinFiltro_IncluyeLosFacturados()
    {
        var (viajeId, _) = await ViajeFacturadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesConFacturaLeida>(
            "/api/viajes?pagina=1");

        Assert.Contains(pagina!.Items, item => item.Id == viajeId);
    }

    /// <summary>Un viaje sin facturar trae <c>factura: null</c>, no un objeto vacío.</summary>
    [Fact]
    public async Task Un_ViajeSinFacturar_TraeLaFacturaEnNulo()
    {
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, estado: EstadoViaje.Rendido);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleConFacturaLeido>(
            $"/api/viajes/{viaje.Id}");

        Assert.Equal("rendido", ficha!.Estado);
        Assert.Null(ficha.Factura);
    }

    // ── Ayudas ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Un viaje que llegó a <c>facturado</c> <b>por el camino real</b>: se emite la factura desde el
    /// endpoint. Marcarlo a mano en la base saltearía justo lo que interesa verificar.
    /// </summary>
    private async Task<(int ViajeId, EscenarioDeAsignacion Escenario)> ViajeFacturadoAsync()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var escenario = await app.ArmarEscenarioAsync();

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Clientes
                .Where(c => c.Id == escenario.ClienteId)
                .ExecuteUpdateAsync(cambio =>
                    cambio.SetProperty(c => c.Direccion, "Ruta 9 km 312, Rosario"));
        });

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            estado: EstadoViaje.Rendido,
            importe: 150_000m);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var emision = await cliente.PostAsJsonAsync("/api/facturas", new
        {
            clienteId = escenario.ClienteId,
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
            viajeIds = new[] { viaje.Id },
        });

        emision.EnsureSuccessStatusCode();

        return (viaje.Id, escenario);
    }

    private static async Task AssertRechazadoAsync(HttpResponseMessage respuesta, int numero)
    {
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("viaje_facturado_inmutable", error!.Codigo);
        Assert.Equal(
            $"El viaje {numero} está facturado y no se puede modificar. Anulá la factura si " +
            "necesitás corregirlo.",
            error.Mensaje);
    }

    private async Task<HttpClient> ClienteConRolAsync(string codigoRol)
    {
        var username = $"gestiona-{codigoRol}-{Guid.NewGuid():N}"[..20];

        await app.CrearUsuarioConRolViajesAsync(username, PasswordDePrueba, codigoRol);

        return await app.CrearClienteAutenticadoAsync(username, PasswordDePrueba);
    }
}

// ── Lo que devuelve el Módulo 5 después de FR-055 ───────────────────────────────────────────────

public record FacturaDelViajeLeida(int Id, string Numero, string Fecha);

public record ViajeDetalleConFacturaLeido(
    int Id,
    int Numero,
    string Estado,
    string? NumeroRemito,
    FacturaDelViajeLeida? Factura);

public record ViajeConFacturaLeido(int Id, int Numero, string Estado, FacturaDelViajeLeida? Factura);

public record PaginaDeViajesConFacturaLeida(
    List<ViajeConFacturaLeido> Items,
    int Total,
    int Pagina,
    int TamanioPagina);
