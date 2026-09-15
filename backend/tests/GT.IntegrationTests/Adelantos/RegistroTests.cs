using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Adelantos;

/// <summary>Registrar un adelanto (User Story 1, FR-001 a FR-012).</summary>
public class RegistroTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Registra_Pendiente_ConElTipoElegido_ElMotivoRecortado_YUnaEntradaDeRegistro()
    {
        var juan = await app.ChoferPropioAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.RegistrarAsync("chofer", juan.Id, motivo: "  gastos médicos  ");

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var detalle = (await respuesta.Content.ReadFromJsonAsync<AdelantoDetalleLeido>())!;
        Assert.Equal($"/api/adelantos/{detalle.Id}", respuesta.Headers.Location!.OriginalString);
        Assert.Equal("pendiente", detalle.Estado);
        Assert.Equal("chofer", detalle.Tipo);
        Assert.Equal("gastos médicos", detalle.Motivo);
        Assert.Equal(150_000m, detalle.Importe);
        Assert.Equal(DatosDePruebaAdelantos.Hoy, detalle.Fecha);
        Assert.Equal(juan.Id, detalle.Persona.Id);

        var entrada = Assert.Single(detalle.Historial);
        Assert.Equal("registro", entrada.Operacion);
        Assert.Equal("admin", entrada.Usuario);
        Assert.Equal(DateTimeKind.Utc, entrada.OcurridoEn.Kind);
        Assert.Null(entrada.Motivo);

        Assert.True(detalle.PuedeResolverse);
        Assert.False(detalle.PuedeAnularse);
    }

    [Fact]
    public async Task La_FechaDeHoyYLaDelPiso_SeAceptan_YUnDiaAfueraDeCadaLado_NoSeRegistra()
    {
        var persona = await app.EmpleadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        var hoy = DatosDePruebaAdelantos.HoyEnArgentina;
        var piso = ReglasDeAdelanto.PrimeraFechaAdmitida(hoy);

        Assert.Equal(HttpStatusCode.Created, (await cliente.RegistrarAsync("empleado", persona.Id, hoy)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await cliente.RegistrarAsync("empleado", persona.Id, piso)).StatusCode);

        foreach (var fuera in new[] { hoy.AddDays(1), piso.AddDays(-1) })
        {
            var respuesta = await cliente.RegistrarAsync("empleado", persona.Id, fuera);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("fecha_fuera_de_rango", error.Codigo);
            Assert.Equal("fecha", error.Campo);
            Assert.Equal(piso.ToString("yyyy-MM-dd"), error.Desde);
            Assert.Equal(hoy.ToString("yyyy-MM-dd"), error.Hasta);
            Assert.Equal($"La fecha tiene que estar entre el {piso:dd/MM/yyyy} y hoy.", error.Mensaje);
        }

        Assert.Equal(2, await app.ContarAdelantosDeAsync(persona.Id));
    }

    [Fact]
    public async Task Cada_CampoFaltanteOMalFormado_Responde400ConSuCampo_YNoRegistra()
    {
        var persona = await app.EmpleadoAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();
        var id = persona.Id;
        var hoy = DatosDePruebaAdelantos.Hoy;

        (object Cuerpo, string Campo)[] casos =
        [
            (new { personaId = id, fecha = hoy, motivo = "Gastos", importe = 1m }, "tipo"),
            (new { tipo = "gerente", personaId = id, fecha = hoy, motivo = "Gastos", importe = 1m }, "tipo"),
            (new { tipo = "empleado", fecha = hoy, motivo = "Gastos", importe = 1m }, "personaId"),
            (new { tipo = "empleado", personaId = id, motivo = "Gastos", importe = 1m }, "fecha"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "", importe = 1m }, "motivo"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "   ", importe = 1m }, "motivo"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = new string('a', 201), importe = 1m }, "motivo"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "Gastos" }, "importe"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "Gastos", importe = 0m }, "importe"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "Gastos", importe = -20_000m }, "importe"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "Gastos", importe = 150_000.555m }, "importe"),
            (new { tipo = "empleado", personaId = id, fecha = hoy, motivo = "Gastos", importe = 10_000_000_000_000_000m }, "importe"),
        ];

        foreach (var (cuerpo, campo) in casos)
        {
            var respuesta = await cliente.PostAsJsonAsync("/api/adelantos", cuerpo);

            Assert.True(respuesta.StatusCode == HttpStatusCode.BadRequest, $"{campo}: {(int)respuesta.StatusCode}");

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("datos_invalidos", error.Codigo);
            Assert.Equal(campo, error.Campo);
        }

        Assert.Equal(0, await app.ContarAdelantosDeAsync(persona.Id));
    }

    [Fact]
    public async Task Con_TipoChoferYSinEmpresaEmisora_SeRechaza_YNoRegistra()
    {
        var juan = await app.ChoferPropioAsync();
        await app.SinEmpresaEmisoraAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.RegistrarAsync("chofer", juan.Id);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(
            "empresa_emisora_no_configurada",
            (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);
        Assert.Equal(0, await app.ContarAdelantosDeAsync(juan.Id));
    }

    /// <summary>US1 esc. 10 y 11: invocando la acción directamente, con personas que la pantalla no ofrecería.</summary>
    [Fact]
    public async Task Una_PersonaNoElegible_SeRechazaDiciendoElMotivo_YNoRegistra()
    {
        var chofer = await app.ChoferPropioAsync("Pérez", "Juan");
        var fichaDeBaja = await app.ChoferPropioAsync("Gómez", "Carlos", fichaActiva: false);
        var externo = await app.ChoferExternoAsync("Díaz", "Luis");
        var empleado = await app.EmpleadoAsync("Torres", "Ana");
        var dadoDeBaja = await app.EmpleadoAsync("Sosa", "Pedro");
        await app.DarDeBajaPersonaAsync(dadoDeBaja.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        (string Tipo, int PersonaId, string Motivo, string Mensaje)[] casos =
        [
            ("empleado", int.MaxValue, "inexistente", "La persona elegida ya no está en el padrón. Elegí otra."),
            ("empleado", dadoDeBaja.Id, "inactiva", "Sosa, Pedro se dio de baja y ya no puede recibir adelantos. Elegí otra persona."),
            ("chofer", fichaDeBaja.Id, "inactiva", "Gómez, Carlos se dio de baja y ya no puede recibir adelantos. Elegí otra persona."),
            ("empleado", chofer.Id, "tipoDistinto", "Pérez, Juan ya no figura como empleado. Elegí el tipo de persona otra vez para ver a quiénes se puede elegir."),
            ("chofer", empleado.Id, "tipoDistinto", "Torres, Ana ya no figura como chofer. Elegí el tipo de persona otra vez para ver a quiénes se puede elegir."),
            ("chofer", externo.Id, "choferExterno", "Díaz, Luis maneja para un transportista externo, y los adelantos son sólo para choferes de G&T Logística. Elegí otra persona."),
        ];

        foreach (var (tipo, personaId, motivo, mensaje) in casos)
        {
            var respuesta = await cliente.RegistrarAsync(tipo, personaId);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

            var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("beneficiario_no_elegible", error.Codigo);
            Assert.Equal(motivo, error.Motivo);
            Assert.Equal(mensaje, error.Mensaje);
        }

        foreach (var persona in new[] { chofer, fichaDeBaja, externo, empleado, dadoDeBaja })
        {
            Assert.Equal(0, await app.ContarAdelantosDeAsync(persona.Id));
        }
    }

    /// <summary>FR-012: sin tope ni aviso. FR-037: registrar no toca a la persona ni a su ficha.</summary>
    [Fact]
    public async Task Dos_AdelantosDeLaMismaPersonaEnElMes_SeRegistran_YLaPersonaQuedaIntacta()
    {
        var juan = await app.ChoferPropioAsync();
        var personaAntes = (await app.RecargarPersonaAsync(juan.Id))!;
        var fichaAntes = await app.RecargarFichaAsync(juan.Id);
        var cliente = await app.CrearClienteAutenticadoAsync();

        Assert.Equal(HttpStatusCode.Created, (await cliente.RegistrarAsync("chofer", juan.Id, importe: 150_000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await cliente.RegistrarAsync("chofer", juan.Id, importe: 50_000m)).StatusCode);

        Assert.Equal(2, await app.ContarAdelantosDeAsync(juan.Id));

        var personaDespues = (await app.RecargarPersonaAsync(juan.Id))!;
        var fichaDespues = await app.RecargarFichaAsync(juan.Id);

        Assert.Equal(
            (personaAntes.Apellido, personaAntes.Nombre, personaAntes.Dni, personaAntes.Tipo, personaAntes.Activa),
            (personaDespues.Apellido, personaDespues.Nombre, personaDespues.Dni, personaDespues.Tipo, personaDespues.Activa));
        Assert.Equal(
            (fichaAntes.Activo, fichaAntes.TransportistaId, fichaAntes.Cuil),
            (fichaDespues.Activo, fichaDespues.TransportistaId, fichaDespues.Cuil));
    }
}
