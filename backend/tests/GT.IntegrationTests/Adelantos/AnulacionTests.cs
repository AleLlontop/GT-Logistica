using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Adelantos;

/// <summary>Anular un adelanto aprobado (User Story 5, FR-027 a FR-031).</summary>
public class AnulacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Valida_EnOrden_Existencia_Estado_Motivo_YConfirmacion()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.EmpleadoAsync();

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await cliente.AnularAsync(999_999, "Motivo.", confirmado: true)).StatusCode);

        // El estado antes que el motivo.
        (EstadoAdelanto Estado, string Nombre, string Mensaje)[] noAnulables =
        [
            (EstadoAdelanto.Pendiente, "pendiente", "Este adelanto está pendiente: sólo se anula un adelanto aprobado. Si está mal cargado, rechazalo."),
            (EstadoAdelanto.Rechazado, "rechazado", "Este adelanto está rechazado: no se puede anular."),
            (EstadoAdelanto.Anulado, "anulado", "Este adelanto ya está anulado."),
        ];

        foreach (var (estado, nombre, mensaje) in noAnulables)
        {
            var adelanto = await app.CrearAdelantoAsync(persona.Id, estado);
            var respuesta = await cliente.AnularAsync(adelanto.Id, "", confirmado: null);

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("adelanto_no_anulable", error.Codigo);
            Assert.Equal(nombre, error.Estado);
            Assert.Equal(mensaje, error.Mensaje);
            Assert.Equal(estado, (await app.RecargarAdelantoAsync(adelanto.Id))!.Estado);
        }

        // El motivo antes que la confirmación.
        var aprobado = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado);

        foreach (var vacio in new[] { "", "   " })
        {
            var sinMotivo = await cliente.AnularAsync(aprobado.Id, vacio, confirmado: null);
            Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

            var error = (await sinMotivo.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("motivo_requerido", error.Codigo);
            Assert.Equal("Escribí el motivo de la anulación: queda en el historial.", error.Mensaje);
        }

        var largo = await cliente.AnularAsync(aprobado.Id, new string('a', 501), confirmado: null);
        Assert.Equal(HttpStatusCode.BadRequest, largo.StatusCode);
        Assert.Equal("datos_invalidos", (await largo.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);
    }

    /// <summary>US5 esc. 7: invocando la acción directamente, con motivo y sin confirmación.</summary>
    [Fact]
    public async Task Sin_Confirmado_Responde409_YNoCambiaNada()
    {
        var persona = await app.EmpleadoAsync("Pérez", "Juan");
        var adelanto = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado, 150_000m);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.AnularAsync(adelanto.Id, "Persona equivocada.", confirmado: false);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
        Assert.Equal("anulacion_requiere_confirmacion", error.Codigo);
        Assert.Equal(
            "El adelanto de $ 150.000,00 para Pérez, Juan queda anulado y deja de sumar en el total adelantado. No se puede deshacer.",
            error.Mensaje);

        var releido = (await app.RecargarAdelantoAsync(adelanto.Id))!;
        Assert.Equal(EstadoAdelanto.Aprobado, releido.Estado);
        Assert.Null(releido.MotivoAnulacion);
        Assert.Equal(2, releido.Cambios.Count);
    }

    [Fact]
    public async Task Con_Confirmado_LoAnula_SigueEnElListado_YDejaDeSumar()
    {
        var persona = await app.EmpleadoAsync();
        await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado, 100_000m);
        var anulable = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado, 50_000m);
        var cliente = await app.CrearClienteAutenticadoAsync();
        var consulta = $"/api/adelantos?personaId={persona.Id}";

        Assert.Equal(150_000m, (await cliente.GetFromJsonAsync<PaginaDeAdelantosLeida>(consulta))!.TotalAdelantado);

        var respuesta = await cliente.AnularAsync(anulable.Id, "  Cargado sobre la persona equivocada.  ", confirmado: true);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<AdelantoDetalleLeido>())!;
        Assert.Equal("anulado", detalle.Estado);
        Assert.Equal("Cargado sobre la persona equivocada.", detalle.MotivoAnulacion);
        Assert.Equal(
            [("registro", null), ("aprobacion", null), ("anulacion", "Cargado sobre la persona equivocada.")],
            detalle.Historial.Select(entrada => (entrada.Operacion, entrada.Motivo)));
        Assert.False(detalle.PuedeAnularse);

        var listado = (await cliente.GetFromJsonAsync<PaginaDeAdelantosLeida>(consulta))!;
        Assert.Equal(2, listado.Total);
        Assert.Contains(listado.Items, fila => fila.Id == anulable.Id && fila.Estado == "anulado");
        Assert.Equal(100_000m, listado.TotalAdelantado);

        var releida = (await app.RecargarPersonaAsync(persona.Id))!;
        Assert.Equal((persona.Apellido, persona.Activa), (releida.Apellido, releida.Activa));
    }
}
