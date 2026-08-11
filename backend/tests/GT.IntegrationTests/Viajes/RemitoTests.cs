using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El número de remito: opcional, y único entre los <b>no anulados</b> (FR-014, SC-003, US2 esc. 8 y 9).
/// </summary>
public class RemitoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_Remito_EsOpcional()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
        });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();
        Assert.Null(sobre!.Viaje.NumeroRemito);
    }

    /// <summary>
    /// FR-014: el rechazo <b>nombra el número del viaje que ya lo usa</b>. Sin eso, quien carga sabe
    /// que el remito está tomado pero no dónde buscarlo.
    /// </summary>
    [Fact]
    public async Task El_RemitoDuplicado_NombraElViajeQueLoUsa()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var remito = RemitoUnico();

        var primera = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id, remito));
        var primero = await primera.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        var segunda = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id, remito));

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);

        var error = await segunda.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("remito_duplicado", error!.Codigo);
        Assert.Equal("numeroRemito", error.Campo);
        Assert.Contains($"viaje {primero!.Viaje.Numero}", error.Mensaje);
    }

    /// <summary>US2 esc. 9: el remito de un viaje anulado vuelve a estar libre.</summary>
    [Fact]
    public async Task El_RemitoDeUnViajeAnulado_VuelveAEstarLibre()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var remito = RemitoUnico();

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Anulado, numeroRemito: remito);

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id, remito));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    /// <summary>Y el de uno rendido no: sigue ocupado, porque el viaje se hizo (FR-014).</summary>
    [Fact]
    public async Task El_RemitoDeUnViajeRendido_SigueOcupado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var remito = RemitoUnico();

        await app.CrearViajeAsync(padron.Id, estado: EstadoViaje.Rendido, numeroRemito: remito);

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id, remito));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    /// <summary>La edición aplica la misma regla, excluyéndose a sí misma (FR-017).</summary>
    [Fact]
    public async Task La_Edicion_ConservaSuPropioRemitoSinConflicto()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var remito = RemitoUnico();

        var alta = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id, remito));
        var creado = await alta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{creado!.Viaje.Id}", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-11",
            origen = "Rosario",
            destino = "Santa Fe",
            numeroRemito = remito,
        });

        Assert.Equal(HttpStatusCode.OK, edicion.StatusCode);
    }

    private static int _contador;

    private static string RemitoUnico() => $"R-{Interlocked.Increment(ref _contador):D6}-{Guid.NewGuid():N}"[..18];

    private static object Cuerpo(int clienteId, string remito) => new
    {
        clienteId,
        fecha = "2026-08-10",
        origen = "Rosario",
        destino = "Córdoba",
        numeroRemito = remito,
    };
}
