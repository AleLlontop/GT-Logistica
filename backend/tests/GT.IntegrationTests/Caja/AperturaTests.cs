using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>Abrir la caja (US1, FR-001 a FR-005), su detalle y la caja abierta propia.</summary>
public class AperturaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Abrir_Responde201_ConElUsuarioComoResponsable_YAbierta()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.AbrirCajaAsync(10_000m);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var caja = (await respuesta.Content.ReadFromJsonAsync<CajaDetalleLeida>())!;
        Assert.Equal($"/api/caja/{caja.Id}", respuesta.Headers.Location!.OriginalString);
        Assert.Equal("abierta", caja.Estado);
        Assert.Equal(10_000m, caja.SaldoInicial);
        Assert.Equal(usuario.Id, caja.Responsable.Id);
        Assert.Equal(usuario.Username, caja.Responsable.Nombre);
        Assert.True(caja.PuedeOperar);

        // El instante sale con la `Z` que lo declara UTC (convención [002]).
        var crudo = await (await cliente.GetAsync($"/api/caja/{caja.Id}")).Content.ReadAsStringAsync();
        Assert.Matches("\"fechaApertura\":\"[^\"]+Z\"", crudo);
    }

    [Fact]
    public async Task Un_SaldoInicialEnCero_SeAcepta()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        Assert.Equal(HttpStatusCode.Created, (await cliente.AbrirCajaAsync(0m)).StatusCode);
    }

    [Theory]
    [InlineData("-1000", "El saldo inicial no puede ser negativo.")]
    [InlineData("100.555", "Escribí el importe con hasta dos decimales.")]
    [InlineData(null, "Escribí el saldo inicial.")]
    public async Task Un_SaldoInicialInvalido_Responde400_SinCrearFilas(string? saldo, string mensaje)
    {
        var (usuario, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.AbrirCajaAsync(
            saldo is null ? null : decimal.Parse(saldo, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("datos_invalidos", error.Codigo);
        Assert.Equal("saldoInicial", error.Campo);
        Assert.Equal(mensaje, error.Mensaje);
        Assert.Equal(0, await app.ContarCajasDeAsync(usuario.Id));
    }

    [Fact]
    public async Task Una_SegundaApertura_Responde409_SinCrearNada()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        await cliente.AbrirCajaAsync(10_000m);

        var respuesta = await cliente.AbrirCajaAsync(5_000m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("caja_ya_abierta", error.Codigo);
        Assert.Equal("Ya tenés una caja abierta. Cerrala antes de abrir otra.", error.Mensaje);
        Assert.Equal(1, await app.ContarCajasDeAsync(usuario.Id));
    }

    [Fact]
    public async Task Otro_Empleado_AbreLaSuya()
    {
        var (_, primero) = await app.EmpleadoAsync();
        var (_, segundo) = await app.EmpleadoAsync();

        Assert.Equal(HttpStatusCode.Created, (await primero.AbrirCajaAsync(10_000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await segundo.AbrirCajaAsync(3_000m)).StatusCode);
    }

    [Fact]
    public async Task Despues_DeCerrarla_ElMismoEmpleadoAbreOtra()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var primera = (await (await cliente.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!;

        await app.CerrarEnLaBaseAsync(primera.Id, 10_000m);

        Assert.Equal(HttpStatusCode.Created, (await cliente.AbrirCajaAsync(2_000m)).StatusCode);
        Assert.Equal(2, await app.ContarCajasDeAsync(usuario.Id));
    }

    [Fact]
    public async Task Abierta_DevuelveLaPropia_O204SinNinguna()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.GetAsync("/api/caja/abierta")).StatusCode);

        var creada = (await (await cliente.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!;

        var abierta = await cliente.GetFromJsonAsync<CajaDetalleLeida>("/api/caja/abierta");
        Assert.Equal(creada.Id, abierta!.Id);
    }

    [Fact]
    public async Task El_Detalle_TraeLosDatos_ElSaldoActual_YPuedeOperarSoloParaElDuenio()
    {
        var (usuario, duenio) = await app.EmpleadoAsync();
        var (_, otro) = await app.EmpleadoAsync();
        var creada = (await (await duenio.AbrirCajaAsync(10_000m)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!;

        var propia = await duenio.DetalleAsync(creada.Id);
        Assert.Equal(usuario.Id, propia.Responsable.Id);
        Assert.Equal("abierta", propia.Estado);
        Assert.Equal(10_000m, propia.SaldoActual);
        Assert.Equal(0m, propia.TotalIngresos);
        Assert.Equal(0m, propia.TotalEgresos);
        Assert.Null(propia.FechaCierre);
        Assert.Null(propia.SaldoFinal);
        Assert.True(propia.PuedeOperar);

        Assert.False((await otro.DetalleAsync(creada.Id)).PuedeOperar);
    }

    [Fact]
    public async Task El_Detalle_DeUnaCajaInexistente_Responde404()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.GetAsync($"/api/caja/{int.MaxValue}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("caja_no_encontrada", (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);
    }

    [Fact]
    public async Task El_Detalle_DeUnaCerrada_TraeCierreYSaldoFinal_SinPoderOperar()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, EstadoCaja.Cerrada, 10_000m, saldoFinal: 10_000m);

        var detalle = await cliente.DetalleAsync(caja.Id);

        Assert.Equal("cerrada", detalle.Estado);
        Assert.NotNull(detalle.FechaCierre);
        Assert.Equal(10_000m, detalle.SaldoFinal);
        Assert.False(detalle.PuedeOperar);
    }
}
