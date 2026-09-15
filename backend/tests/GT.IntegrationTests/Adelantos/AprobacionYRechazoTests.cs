using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Adelantos;

/// <summary>Aprobar o rechazar un adelanto (User Story 4, FR-021 a FR-025).</summary>
public class AprobacionYRechazoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>FR-022: quien registró el adelanto puede aprobarlo.</summary>
    [Fact]
    public async Task Aprobar_UnPendienteRegistradoPorElMismoUsuario_LoDejaAprobado()
    {
        var persona = await app.EmpleadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        var registrado = (await (await cliente.RegistrarAsync("empleado", persona.Id)).Content
            .ReadFromJsonAsync<AdelantoDetalleLeido>())!;

        var respuesta = await cliente.AprobarAsync(registrado.Id);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<AdelantoDetalleLeido>())!;
        Assert.Equal("aprobado", detalle.Estado);
        Assert.Equal(["registro", "aprobacion"], detalle.Historial.Select(entrada => entrada.Operacion));
        Assert.All(detalle.Historial, entrada => Assert.Equal("admin", entrada.Usuario));
        Assert.False(detalle.PuedeResolverse);
        Assert.True(detalle.PuedeAnularse);
    }

    /// <summary>La regla de persona activa es del registro, no de la aprobación (spec §Assumptions).</summary>
    [Fact]
    public async Task Aprobar_UnPendienteDeAlguienDadoDeBajaDespues_SePuede()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id);
        await app.DarDeBajaPersonaAsync(persona.Id);
        var cliente = await app.CrearClienteAutenticadoAsync();

        Assert.Equal(HttpStatusCode.OK, (await cliente.AprobarAsync(adelanto.Id)).StatusCode);
        Assert.Equal(EstadoAdelanto.Aprobado, (await app.RecargarAdelantoAsync(adelanto.Id))!.Estado);
    }

    [Theory]
    [InlineData(EstadoAdelanto.Aprobado, "aprobado")]
    [InlineData(EstadoAdelanto.Rechazado, "rechazado")]
    [InlineData(EstadoAdelanto.Anulado, "anulado")]
    public async Task Aprobar_ORechazar_FueraDePendiente_Responde409ConElEstado_YNoCambiaNada(
        EstadoAdelanto estado,
        string nombre)
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id, estado);
        var cambiosAntes = adelanto.Cambios.Count;
        var cliente = await app.CrearClienteAutenticadoAsync();

        foreach (var respuesta in new[]
                 {
                     await cliente.AprobarAsync(adelanto.Id),
                     await cliente.RechazarAsync(adelanto.Id, "Otro motivo.", confirmado: true),
                 })
        {
            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("adelanto_no_resoluble", error.Codigo);
            Assert.Equal(nombre, error.Estado);
            Assert.Equal($"Este adelanto ya está {nombre}: no se puede aprobar ni rechazar.", error.Mensaje);
        }

        var releido = (await app.RecargarAdelantoAsync(adelanto.Id))!;
        Assert.Equal(estado, releido.Estado);
        Assert.Equal(cambiosAntes, releido.Cambios.Count);
    }

    [Fact]
    public async Task Rechazar_Valida_EnOrden_Existencia_Estado_Motivo_YConfirmacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.EmpleadoAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.RechazarAsync(999_999, "Motivo.", confirmado: true)).StatusCode);

        // El estado antes que el motivo: un aprobado sin motivo dice que está aprobado.
        var aprobado = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado);
        var noResoluble = await cliente.RechazarAsync(aprobado.Id, "", confirmado: null);
        Assert.Equal(HttpStatusCode.Conflict, noResoluble.StatusCode);
        Assert.Equal("adelanto_no_resoluble", (await noResoluble.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);

        // El motivo antes que la confirmación.
        var pendiente = await app.CrearAdelantoAsync(persona.Id);

        foreach (var vacio in new[] { "", "   " })
        {
            var sinMotivo = await cliente.RechazarAsync(pendiente.Id, vacio, confirmado: null);
            Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

            var error = (await sinMotivo.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("motivo_requerido", error.Codigo);
            Assert.Equal("Escribí el motivo del rechazo: queda en el historial.", error.Mensaje);
        }

        var largo = await cliente.RechazarAsync(pendiente.Id, new string('a', 501), confirmado: null);
        Assert.Equal(HttpStatusCode.BadRequest, largo.StatusCode);
        Assert.Equal("datos_invalidos", (await largo.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);

        Assert.Single((await app.RecargarAdelantoAsync(pendiente.Id))!.Cambios);
    }

    /// <summary>US4 esc. 8: invocando la acción directamente, con motivo y sin confirmación.</summary>
    [Fact]
    public async Task Rechazar_SinConfirmado_Responde409_YElAdelantoSiguePendienteSinEntradaNueva()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id);
        var cliente = await app.CrearClienteAutenticadoAsync();

        foreach (bool? confirmado in new bool?[] { null, false })
        {
            var respuesta = await cliente.RechazarAsync(adelanto.Id, "Ya tiene otro.", confirmado);

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("rechazo_requiere_confirmacion", error.Codigo);
            Assert.Null(error.Estado);
        }

        var releido = (await app.RecargarAdelantoAsync(adelanto.Id))!;
        Assert.Equal(EstadoAdelanto.Pendiente, releido.Estado);
        Assert.Null(releido.MotivoRechazo);
        Assert.Single(releido.Cambios);
    }

    [Fact]
    public async Task Rechazar_ConConfirmado_LoDejaRechazadoConElMotivoRecortado_YLaPersonaIntacta()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.RechazarAsync(
            adelanto.Id,
            "  Ya tiene un adelanto pendiente del mes anterior.  ",
            confirmado: true);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<AdelantoDetalleLeido>())!;
        Assert.Equal("rechazado", detalle.Estado);
        Assert.Equal("Ya tiene un adelanto pendiente del mes anterior.", detalle.MotivoRechazo);
        Assert.Equal(
            [("registro", null), ("rechazo", "Ya tiene un adelanto pendiente del mes anterior.")],
            detalle.Historial.Select(entrada => (entrada.Operacion, entrada.Motivo)));
        Assert.False(detalle.PuedeResolverse);
        Assert.False(detalle.PuedeAnularse);

        var releida = (await app.RecargarPersonaAsync(persona.Id))!;
        Assert.Equal((persona.Apellido, persona.Nombre, persona.Activa), (releida.Apellido, releida.Nombre, releida.Activa));
    }
}
