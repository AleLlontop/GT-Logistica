using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El número de viaje (FR-011, SC-002, US2 esc. 5).
///
/// <b>El primer test es la trampa 2 de <c>tasks.md</c>.</b> Si <c>Viaje.Numero</c> se declarara
/// <c>required int</c>, el código estaría obligado a asignarlo, EF mandaría el <c>0</c> del
/// constructor en el <c>INSERT</c> y el <c>DEFAULT</c> de la columna no se aplicaría nunca: el primer
/// viaje del sistema saldría con número 0 y nadie lo notaría hasta ver la pantalla.
/// </summary>
public class NumeroDeViajeTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_AltaNuncaMandaElNumero_YLaSecuenciaLoGenera()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(padron.Id));
        var creado = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        // Un 0 acá significa que EF escribió la columna en vez de dejar que se aplique el DEFAULT.
        Assert.True(creado!.Viaje.Numero > 0);
    }

    [Fact]
    public async Task El_Numero_AvanzaDeAUno()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var primero = await CrearViajeAsync(cliente, padron.Id);
        var segundo = await CrearViajeAsync(cliente, padron.Id);
        var tercero = await CrearViajeAsync(cliente, padron.Id);

        Assert.Equal(primero.Numero + 1, segundo.Numero);
        Assert.Equal(segundo.Numero + 1, tercero.Numero);
    }

    /// <summary>
    /// US2 esc. 5: anular no devuelve el número a la secuencia. Es lo que garantiza que el 1041 sea
    /// siempre el mismo viaje y que la numeración no cuente dos historias distintas.
    /// </summary>
    [Fact]
    public async Task El_NumeroDeUnViajeAnulado_NoSeReutiliza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var anulado = await CrearViajeAsync(cliente, padron.Id);

        // Se anula contra la base y no por el endpoint a propósito: lo que este test verifica es la
        // secuencia, no la anulación —que tiene sus propios tests en `AnulacionTests`—.
        await app.EnLaBaseAsync(async contexto =>
        {
            var viaje = await contexto.Viajes.FindAsync(anulado.Id);
            viaje!.Estado = Domain.Viajes.EstadoViaje.Anulado;
            viaje.MotivoAnulacion = "El cliente canceló la carga.";
            await contexto.SaveChangesAsync();
        });

        var siguiente = await CrearViajeAsync(cliente, padron.Id);

        Assert.NotEqual(anulado.Numero, siguiente.Numero);
        Assert.True(siguiente.Numero > anulado.Numero);
    }

    /// <summary>
    /// FR-011: el número no está en el contrato de entrada, así que mandarlo no tiene efecto. No es
    /// que se ignore silenciosamente un campo válido: el cuerpo no lo tiene.
    /// </summary>
    [Fact]
    public async Task Ningun_CuerpoDePeticionPuedeFijarElNumero()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
            numero = 999_999,
        });

        var creado = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        Assert.NotEqual(999_999, creado!.Viaje.Numero);
    }

    /// <summary>Tampoco al editar: el número no es editable en ningún estado (FR-017).</summary>
    [Fact]
    public async Task La_Edicion_NoPuedeCambiarElNumero()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        var viaje = await CrearViajeAsync(cliente, padron.Id);

        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-11",
            origen = "Rosario",
            destino = "Santa Fe",
            numero = 42,
        });

        edicion.EnsureSuccessStatusCode();

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(viaje.Numero, despues!.Numero);
    }

    private static object Cuerpo(int clienteId) => new
    {
        clienteId,
        fecha = "2026-08-10",
        origen = "Rosario",
        destino = "Córdoba",
    };

    private static async Task<ViajeDetalleLeido> CrearViajeAsync(HttpClient cliente, int clienteId)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/viajes", Cuerpo(clienteId));
        respuesta.EnsureSuccessStatusCode();

        var sobre = await respuesta.Content.ReadFromJsonAsync<RespuestaViajeLeida>();

        return sobre!.Viaje;
    }
}
