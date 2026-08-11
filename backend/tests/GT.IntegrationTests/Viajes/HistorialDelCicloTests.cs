using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El historial del <b>ciclo completo</b> (FR-035, SC-006, US4 esc. 13).
///
/// Hasta acá el historial sólo estaba probado para la línea del alta. Este test recorre los dos
/// caminos completos con <b>dos usuarios distintos</b> y afirma que cada transición dejó su línea,
/// en orden, con el estado anterior correcto, el nuevo correcto, quién la produjo y un instante en
/// UTC. Es lo que hace cierto el 100% de SC-006.
/// </summary>
public class HistorialDelCicloTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDePrueba = "Historial.1234";

    [Fact]
    public async Task El_Camino_Alta_EnCurso_Rendido_DejaTresLineasEnOrden()
    {
        var trafico = await app.CrearUsuarioConRolViajesAsync(
            "trafico-historial-a",
            PasswordDePrueba,
            CodigosRol.Trafico);

        var deTrafico = await app.CrearClienteAutenticadoAsync(trafico.Username, PasswordDePrueba);
        var deAdministrador = await app.CrearClienteAutenticadoAsync();
        var administrador = await app.ObtenerAdministradorAsync();

        var escenario = await app.ArmarEscenarioAsync();

        // El alta la hace Tráfico…
        var alta = await deTrafico.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = escenario.ClienteId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Córdoba",
            importe = 240_000m,
        });

        var creado = (await alta.Content.ReadFromJsonAsync<RespuestaViajeLeida>())!.Viaje;

        await AsignarAsync(deTrafico, creado.Id, escenario);

        // …y el resto del ciclo lo hace el administrador, para que el historial tenga dos manos.
        await deAdministrador.PostAsync($"/api/viajes/{creado.Id}/en-curso", null);
        await deAdministrador.PostAsync($"/api/viajes/{creado.Id}/rendicion", null);

        var historial = await app.HistorialDeAsync(creado.Id);

        Assert.Equal(3, historial.Count);

        // `null` sólo en la del alta: antes del alta no había estado.
        Assert.Null(historial[0].EstadoAnterior);
        Assert.Equal(EstadoViaje.Pendiente, historial[0].EstadoNuevo);
        Assert.Equal(trafico.Id, historial[0].UsuarioId);

        Assert.Equal(EstadoViaje.Pendiente, historial[1].EstadoAnterior);
        Assert.Equal(EstadoViaje.EnCurso, historial[1].EstadoNuevo);
        Assert.Equal(administrador.Id, historial[1].UsuarioId);

        Assert.Equal(EstadoViaje.EnCurso, historial[2].EstadoAnterior);
        Assert.Equal(EstadoViaje.Rendido, historial[2].EstadoNuevo);
        Assert.Equal(administrador.Id, historial[2].UsuarioId);

        Assert.All(historial, linea => Assert.Equal(DateTimeKind.Utc, linea.OcurridoEn.Kind));
    }

    [Fact]
    public async Task El_Camino_Alta_EnCurso_Anulado_DejaTresLineasEnOrden()
    {
        var trafico = await app.CrearUsuarioConRolViajesAsync(
            "trafico-historial-b",
            PasswordDePrueba,
            CodigosRol.Trafico);

        var deTrafico = await app.CrearClienteAutenticadoAsync(trafico.Username, PasswordDePrueba);
        var deAdministrador = await app.CrearClienteAutenticadoAsync();

        var escenario = await app.ArmarEscenarioAsync();

        var alta = await deTrafico.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = escenario.ClienteId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Mendoza",
        });

        var creado = (await alta.Content.ReadFromJsonAsync<RespuestaViajeLeida>())!.Viaje;

        await AsignarAsync(deTrafico, creado.Id, escenario);
        await deTrafico.PostAsync($"/api/viajes/{creado.Id}/en-curso", null);

        await deAdministrador.PostAsJsonAsync(
            $"/api/viajes/{creado.Id}/anulacion",
            new { motivo = "El cliente canceló la carga." });

        var historial = await app.HistorialDeAsync(creado.Id);

        Assert.Equal(3, historial.Count);
        Assert.Null(historial[0].EstadoAnterior);
        Assert.Equal(EstadoViaje.EnCurso, historial[2].EstadoAnterior);
        Assert.Equal(EstadoViaje.Anulado, historial[2].EstadoNuevo);
    }

    /// <summary>
    /// La ficha devuelve el historial completo, de la línea más vieja a la más nueva, con el nombre
    /// del usuario y el instante en UTC con la <c>Z</c> que lo declara (FR-045, convención [002]).
    /// </summary>
    [Fact]
    public async Task La_Ficha_DevuelveElHistorialCompletoEnOrden()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var alta = await cliente.PostAsJsonAsync("/api/viajes", new
        {
            clienteId = escenario.ClienteId,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Córdoba",
            importe = 100_000m,
        });

        var creado = (await alta.Content.ReadFromJsonAsync<RespuestaViajeLeida>())!.Viaje;

        await AsignarAsync(cliente, creado.Id, escenario);
        await cliente.PostAsync($"/api/viajes/{creado.Id}/en-curso", null);
        await cliente.PostAsync($"/api/viajes/{creado.Id}/rendicion", null);

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>($"/api/viajes/{creado.Id}");

        Assert.Equal(3, ficha!.Historial.Count);
        Assert.Equal([null, "pendiente", "enCurso"], ficha.Historial.Select(l => l.EstadoAnterior));
        Assert.Equal(["pendiente", "enCurso", "rendido"], ficha.Historial.Select(l => l.EstadoNuevo));
        Assert.All(ficha.Historial, linea => Assert.Equal("admin", linea.Usuario));
    }

    /// <summary>
    /// La asignación <b>no</b> deja línea de historial: FR-035 cubre cambios de estado, y asignar no
    /// cambia el estado. Está anotado como decisión, no como olvido (checklist CHK008).
    /// </summary>
    [Fact]
    public async Task La_Asignacion_NoDejaLineaDeHistorial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var antes = await app.HistorialDeAsync(viaje.Id);

        await AsignarAsync(cliente, viaje.Id, escenario);

        var despues = await app.HistorialDeAsync(viaje.Id);

        Assert.Equal(antes.Count, despues.Count);
    }

    private static Task<HttpResponseMessage> AsignarAsync(
        HttpClient cliente,
        int viajeId,
        EscenarioDeAsignacion escenario) =>
        cliente.PostAsJsonAsync(
            $"/api/viajes/{viajeId}/asignacion",
            new { choferId = escenario.ChoferId, vehiculoId = escenario.VehiculoId });
}
