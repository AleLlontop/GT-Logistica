using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>El resumen de cierre y su confirmación (US3, FR-016 a FR-022, research §3).</summary>
public class CierreTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_Resumen_TraeSaldoInicial_Movimientos_Totales_YSaldoFinal()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirConMovimientosAsync(cliente);

        var resumen = await cliente.ResumenAsync(cajaId);

        Assert.Equal(10_000m, resumen.SaldoInicial);
        Assert.Equal(3_500m, resumen.TotalIngresos);
        Assert.Equal(2_000m, resumen.TotalEgresos);
        Assert.Equal(11_500m, resumen.SaldoFinal);

        // En orden cronológico.
        Assert.Equal(["ingreso", "egreso"], resumen.Movimientos.Select(movimiento => movimiento.Tipo));
    }

    [Fact]
    public async Task Sin_Movimientos_ElSaldoFinalEsElInicial()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente, 10_000m);

        var resumen = await cliente.ResumenAsync(cajaId);

        Assert.Empty(resumen.Movimientos);
        Assert.Equal(10_000m, resumen.SaldoFinal);
    }

    [Fact]
    public async Task Los_MovimientosDeUnDiaAnterior_EntranEnElResumen()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, fechaApertura: DateTime.UtcNow.AddDays(-2));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 700m, fecha: DateTime.UtcNow.AddDays(-2));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 300m);

        var resumen = await cliente.ResumenAsync(caja.Id);

        Assert.Equal(2, resumen.Movimientos.Count);
        Assert.Equal(11_000m, resumen.SaldoFinal);
    }

    [Fact]
    public async Task Sin_Confirmado_Responde409ConElResumen_YLaCajaSigueAbierta()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirConMovimientosAsync(cliente);

        var respuesta = await cliente.CerrarCajaAsync(cajaId, confirmado: null, saldoFinalConfirmado: 11_500m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("confirmacion_requerida", error.Codigo);
        Assert.Equal("Revisá el resumen y confirmá el cierre.", error.Mensaje);
        Assert.Equal(10_000m, error.SaldoInicial);
        Assert.Equal(11_500m, error.SaldoFinal);
        Assert.Equal(2, error.Movimientos!.Count);

        Assert.Equal("abierta", (await cliente.DetalleAsync(cajaId)).Estado);
    }

    [Fact]
    public async Task Confirmado_ConElSaldoMostrado_LaDejaCerrada_ConFechaYSaldoFinal()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirConMovimientosAsync(cliente);

        var respuesta = await cliente.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 11_500m);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cerrada = (await respuesta.Content.ReadFromJsonAsync<CajaDetalleLeida>())!;
        Assert.Equal("cerrada", cerrada.Estado);
        Assert.Equal(11_500m, cerrada.SaldoFinal);
        Assert.NotNull(cerrada.FechaCierre);
        Assert.False(cerrada.PuedeOperar);
        Assert.Matches("\"fechaCierre\":\"[^\"]+Z\"", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Con_UnSaldoDistinto_Responde409ConElResumenNuevo_YNoCierra()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirConMovimientosAsync(cliente);
        var visto = await cliente.ResumenAsync(cajaId);

        // Un movimiento entre el resumen y la confirmación (US3 esc. 7).
        await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", 500m);

        var respuesta = await cliente.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: visto.SaldoFinal);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("cierre_desactualizado", error.Codigo);
        Assert.Equal("El resumen cambió desde que lo viste. Revisalo antes de confirmar el cierre.", error.Mensaje);
        Assert.Equal(12_000m, error.SaldoFinal);
        Assert.Equal(3, error.Movimientos!.Count);

        Assert.Equal("abierta", (await cliente.DetalleAsync(cajaId)).Estado);
    }

    [Fact]
    public async Task Cerrar_UnaCerrada_Responde409CajaCerrada()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente, 10_000m);
        await cliente.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m);

        var respuesta = await cliente.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("caja_cerrada", (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);
    }

    [Fact]
    public async Task Cerrar_LaDeOtroEmpleado_Responde409CajaAjena()
    {
        var (_, duenio) = await app.EmpleadoAsync();
        var (_, otro) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(duenio, 10_000m);

        var respuesta = await otro.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 10_000m);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("caja_ajena", (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);

        var resumen = await otro.GetAsync($"/api/caja/{cajaId}/cierre");
        Assert.Equal(HttpStatusCode.Conflict, resumen.StatusCode);
        Assert.Equal("caja_ajena", (await resumen.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);

        Assert.Equal("abierta", (await duenio.DetalleAsync(cajaId)).Estado);
    }

    [Fact]
    public async Task Despues_DeCerrar_NoAdmiteMovimientos_ElSaldoNoCambia_YSePuedeAbrirOtra()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirConMovimientosAsync(cliente);
        await cliente.CerrarCajaAsync(cajaId, confirmado: true, saldoFinalConfirmado: 11_500m);

        var movimiento = await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", 100m);
        Assert.Equal(HttpStatusCode.Conflict, movimiento.StatusCode);
        Assert.Equal("caja_cerrada", (await movimiento.Content.ReadFromJsonAsync<ErrorCajaLeido>())!.Codigo);

        var releida = (await app.RecargarCajaAsync(cajaId))!;
        Assert.Equal(EstadoCaja.Cerrada, releida.Estado);
        Assert.Equal(11_500m, releida.SaldoFinal);
        Assert.Equal(2, releida.Movimientos.Count);

        Assert.Equal(HttpStatusCode.Created, (await cliente.AbrirCajaAsync(11_500m)).StatusCode);
        Assert.Equal(2, await app.ContarCajasDeAsync(usuario.Id));
    }

    private static async Task<int> AbrirAsync(HttpClient cliente, decimal saldoInicial) =>
        (await (await cliente.AbrirCajaAsync(saldoInicial)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;

    /// <summary>$10.000 de inicio, un ingreso de $3.500 y un egreso de $2.000: saldo final $11.500 (CA6).</summary>
    private static async Task<int> AbrirConMovimientosAsync(HttpClient cliente)
    {
        var cajaId = await AbrirAsync(cliente, 10_000m);

        await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", 3_500m, "Cobro flete");
        await cliente.RegistrarMovimientoAsync(cajaId, "egreso", 2_000m, "Combustible");

        return cajaId;
    }
}
