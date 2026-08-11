using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// FR-018 y SC-013: un viaje <c>rendido</c> es inmutable <b>para todos los roles</b>, incluido el
/// Administrador del sistema (US4 esc. 8 y 9).
///
/// Se prueban <b>los cinco caminos de escritura</b> —editar, asignar, poner en curso, rendir de nuevo
/// y anular— y los cinco también con la cuenta de administrador. No hay camino de corrección en esta
/// versión, y eso simplifica el módulo entero: los cinco casos de uso consultan el estado antes de
/// tocar nada.
/// </summary>
public class ViajeRendidoInmutableTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordDePrueba = "Viajes.1234";

    [Fact]
    public async Task Los_CincoCaminosDeEscritura_SeRechazan()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        await VerificarLosCincoCaminosAsync(cliente, "viaje_rendido_inmutable");
    }

    /// <summary>
    /// US4 esc. 9: el Administrador del sistema tampoco puede. No es una restricción de permisos sino
    /// una del estado, y por eso vale para todos.
    /// </summary>
    [Fact]
    public async Task El_AdministradorDelSistema_TampocoPuede()
    {
        var usuario = await app.CrearUsuarioConRolViajesAsync(
            "admin-rendido",
            PasswordDePrueba,
            CodigosRol.AdministradorSistema);

        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, PasswordDePrueba);

        await VerificarLosCincoCaminosAsync(cliente, "viaje_rendido_inmutable");
    }

    /// <summary>Un viaje anulado tampoco se modifica, con su propio código (FR-017, US6 esc. 7).</summary>
    [Fact]
    public async Task Un_ViajeAnulado_TampocoSeModifica()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        await VerificarLosCincoCaminosAsync(
            cliente,
            "viaje_anulado_inmutable",
            EstadoViaje.Anulado);
    }

    private async Task VerificarLosCincoCaminosAsync(
        HttpClient cliente,
        string codigoEsperado,
        EstadoViaje estado = EstadoViaje.Rendido)
    {
        var escenario = await app.ArmarEscenarioAsync();
        var otro = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            estado: estado,
            asignado: true,
            importe: 240_000m);

        // 1. Editar.
        var edicion = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = escenario.ClienteId,
            fecha = viaje.Fecha.ToString("yyyy-MM-dd"),
            origen = "Otro origen",
            destino = "Otro destino",
        });

        await VerificarRechazo(edicion, codigoEsperado);

        // 2. Asignar.
        var asignacion = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = otro.ChoferId, vehiculoId = otro.VehiculoId });

        // La asignación sobre un anulado tiene mensaje propio: no se puede reasignar (FR-020).
        await VerificarRechazo(
            asignacion,
            estado is EstadoViaje.Rendido ? codigoEsperado : "asignacion_no_permitida");

        // 3. Poner en curso.
        await VerificarRechazo(
            await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null),
            codigoEsperado);

        // 4. Rendir de nuevo.
        await VerificarRechazo(
            await cliente.PostAsJsonAsync(
                $"/api/viajes/{viaje.Id}/rendicion",
                new { confirmado = true }),
            codigoEsperado);

        // 5. Anular.
        await VerificarRechazo(
            await cliente.PostAsJsonAsync(
                $"/api/viajes/{viaje.Id}/anulacion",
                new { motivo = "Un motivo cualquiera." }),
            codigoEsperado);

        // Y después de los cinco intentos, el viaje quedó exactamente como estaba.
        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(estado, despues!.Estado);
        Assert.Equal("Rosario", despues.Origen);
        Assert.Equal(escenario.ChoferId, despues.ChoferId);
    }

    private static async Task VerificarRechazo(HttpResponseMessage respuesta, string codigoEsperado)
    {
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal(codigoEsperado, error!.Codigo);
    }
}
