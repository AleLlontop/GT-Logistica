using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El alta de un viaje (FR-013, FR-015, FR-016, FR-032, FR-035, FR-039; US2 esc. 1, 6, 7, 10, 11 y 12).
/// </summary>
public class CrearViajeTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>FR-032: todo viaje nace pendiente, y el estado no llega del cuerpo.</summary>
    [Fact]
    public async Task El_Viaje_NacePendiente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var sobre = await CrearAsync(cliente, padron.Id);

        Assert.Equal("pendiente", sobre.Viaje.Estado);
    }

    /// <summary>
    /// FR-035: el alta escribe <b>la primera línea del historial</b>, con <c>estadoAnterior</c> vacío
    /// —antes del alta no había estado— y el usuario de la sesión.
    /// </summary>
    [Fact]
    public async Task El_Alta_EscribeLaPrimeraLineaDelHistorial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var administrador = await app.ObtenerAdministradorAsync();

        var sobre = await CrearAsync(cliente, padron.Id);

        var historial = await app.HistorialDeAsync(sobre.Viaje.Id);
        var linea = Assert.Single(historial);

        Assert.Null(linea.EstadoAnterior);
        Assert.Equal(EstadoViaje.Pendiente, linea.EstadoNuevo);
        Assert.Equal(administrador.Id, linea.UsuarioId);
        Assert.Equal(DateTimeKind.Utc, linea.OcurridoEn.Kind);
    }

    /// <summary>Y la ficha lo devuelve, con el nombre del usuario y la `Z` de la convención [002].</summary>
    [Fact]
    public async Task La_Ficha_DevuelveLaLineaDelAlta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var sobre = await CrearAsync(cliente, padron.Id);

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>(
            $"/api/viajes/{sobre.Viaje.Id}");

        var linea = Assert.Single(ficha!.Historial);

        Assert.Null(linea.EstadoAnterior);
        Assert.Equal("pendiente", linea.EstadoNuevo);
        Assert.Equal("admin", linea.Usuario);
    }

    // ── FR-013: el importe ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task El_ImporteNegativo_SeRechazaConSuPropioCodigo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
            importe = -1,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("importe_negativo", error!.Codigo);
        Assert.Equal("importe", error.Campo);
    }

    /// <summary>El cero es válido: viaje sin cargo, o con el importe todavía sin definir.</summary>
    [Fact]
    public async Task El_ImporteEnCero_SeAcepta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var sobre = await CrearAsync(cliente, padron.Id, importe: 0m);

        Assert.Equal(0m, sobre.Viaje.Importe);
    }

    // ── FR-016: la fecha admite pasado y futuro ────────────────────────────────────────────────

    [Fact]
    public async Task La_Fecha_AdmitePasadoYFuturo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var pasado = await CrearAsync(cliente, padron.Id, fecha: hoy.AddMonths(-6));
        var futuro = await CrearAsync(cliente, padron.Id, fecha: hoy.AddMonths(3));

        Assert.Equal("pendiente", pasado.Viaje.Estado);
        Assert.Equal("pendiente", futuro.Viaje.Estado);
    }

    // ── FR-015a: las dos advertencias llegan con el resultado y no frenan el guardado ──────────

    [Fact]
    public async Task El_OrigenIgualAlDestino_AdvierteSinFrenar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Rosario",
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        Assert.Contains(sobre!.Advertencias, a => a.Codigo == "origen_igual_a_destino");

        // El viaje quedó guardado: la advertencia no es un error.
        Assert.NotNull(await app.RecargarViajeAsync(sobre.Viaje.Id));
    }

    [Fact]
    public async Task La_CargaRetroactiva_AdvierteSinFrenar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var sobre = await CrearAsync(cliente, padron.Id, fecha: FechaHoyArgentina.Hoy().AddDays(-3));

        Assert.Contains(sobre.Advertencias, a => a.Codigo == "carga_retroactiva");
        Assert.True(sobre.Viaje.EsRetroactivo);
    }

    [Fact]
    public async Task Un_ViajeDeHoy_NoAdvierteNada()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var sobre = await CrearAsync(cliente, padron.Id, fecha: FechaHoyArgentina.Hoy());

        Assert.Empty(sobre.Advertencias);
        Assert.False(sobre.Viaje.EsRetroactivo);
    }

    // ── FR-012: el cliente tiene que existir y estar activo ────────────────────────────────────

    [Fact]
    public async Task Un_ClienteInactivo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync(activo: false);

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
        });

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("cliente_inexistente", error!.Codigo);
        Assert.Equal("clienteId", error.Campo);
    }

    // ── El contrato exige las dos señales derivadas desde el primer listado ────────────────────

    /// <summary>
    /// <c>demorado</c> y <c>esRetroactivo</c> están declarados obligatorios en el esquema
    /// <c>Viaje</c>: el listado tiene que cumplir su contrato desde US2, no recién desde US5
    /// (FR-016, FR-039).
    /// </summary>
    [Fact]
    public async Task El_ListadoDevuelveLasDosSenialesDerivadasDesdeElPrimerViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        await CrearAsync(cliente, padron.Id, fecha: hoy.AddDays(-10));

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}");

        var fila = Assert.Single(pagina!.Items);

        // Un viaje que nunca arrancó no tiene línea de `en curso` en el historial: la subconsulta
        // devuelve `false` sin necesitar ningún caso especial.
        Assert.False(fila.Demorado);
        Assert.True(fila.EsRetroactivo);
    }

    private static async Task<RespuestaViajeLeida> CrearAsync(
        HttpClient cliente,
        int clienteId,
        DateOnly? fecha = null,
        decimal importe = 0m)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId,
            fecha = (fecha ?? FechaHoyArgentina.Hoy()).ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Córdoba",
            importe,
        });

        respuesta.EnsureSuccessStatusCode();

        return (await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>())!;
    }
}
