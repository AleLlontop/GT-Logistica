using System.Net;
using System.Net.Http.Json;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>El detalle de una liquidación (User Story 3, FR-025 a FR-028, FR-033).</summary>
public class DetalleLiquidacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Una_Anulada_SigueListandoSusViajes_YNoTieneRestaPagar()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var a = await app.ViajeDeAsync(clienteId, fletero.Id, 60_000m);
        var b = await app.ViajeDeAsync(clienteId, fletero.Id, 40_000m);

        var liquidacion = await app.CrearLiquidacionAsync(
            fletero.Id,
            [a, b],
            EstadoLiquidacion.Anulada,
            motivoAnulacion: "El período no correspondía.");

        var detalle = await LeerAsync(liquidacion.Id);

        Assert.Equal("anulada", detalle.Estado);
        Assert.Equal("El período no correspondía.", detalle.MotivoAnulacion);
        Assert.Equal([a.Id, b.Id], detalle.Viajes.Select(viaje => viaje.Id).Order());
        Assert.Null(detalle.RestaPagar);
        Assert.Equal(100_000m, detalle.ImporteTotal);
    }

    [Fact]
    public async Task El_Historial_VaEnOrdenCronologico_ConLosViajesDeCadaEdicion()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var quitado = await app.ViajeDeAsync(clienteId, fletero.Id);
        var agregado = await app.ViajeDeAsync(clienteId, fletero.Id);

        var generadaEn = DateTime.UtcNow.AddHours(-2);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [quitado], generadaEn: generadaEn);

        await app.EnLaBaseAsync(async contexto =>
        {
            var administrador = await DatosDePruebaLiquidaciones.AdministradorAsync(contexto);

            var edicion = new CambioDeLiquidacion
            {
                LiquidacionId = liquidacion.Id,
                Operacion = OperacionDeLiquidacion.Edicion,
                UsuarioId = administrador.Id,
                OcurridoEn = generadaEn.AddHours(1),
            };

            edicion.Viajes.Add(new CambioDeLiquidacionViaje { CambioDeLiquidacionId = 0, ViajeId = quitado.Id, Agregado = false });
            edicion.Viajes.Add(new CambioDeLiquidacionViaje { CambioDeLiquidacionId = 0, ViajeId = agregado.Id, Agregado = true });

            contexto.CambiosDeLiquidacion.Add(edicion);
            await contexto.SaveChangesAsync();
        });

        var detalle = await LeerAsync(liquidacion.Id);

        Assert.Equal(["generacion", "edicion"], detalle.Historial.Select(entrada => entrada.Operacion));
        Assert.Empty(detalle.Historial[0].ViajesQuitados);
        Assert.Equal([quitado.Numero], detalle.Historial[1].ViajesQuitados);
        Assert.Equal([agregado.Numero], detalle.Historial[1].ViajesAgregados);
        Assert.Equal("admin", detalle.Historial[1].Usuario);
    }

    /// <summary>Research §12.8: una liquidación generada a las 02:30 UTC del 01/08 es del 31/07 en Argentina.</summary>
    [Fact]
    public async Task La_FechaDeGeneracion_EsLaDeArgentina()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);

        var liquidacion = await app.CrearLiquidacionAsync(
            fletero.Id,
            [viaje],
            generadaEn: new DateTime(2026, 8, 1, 2, 30, 0, DateTimeKind.Utc));

        Assert.Equal("2026-07-31", (await LeerAsync(liquidacion.Id)).FechaGeneracion);
    }

    /// <summary>FR-026: los datos del transportista se leen del padrón, no se copian.</summary>
    [Fact]
    public async Task Los_DatosDelTransportista_SonLosDelPadron()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);

        await app.EnLaBaseAsync(contexto => contexto.Transportistas
            .Where(transportista => transportista.Id == fletero.Id)
            .ExecuteUpdateAsync(cambio => cambio.SetProperty(transportista => transportista.Nombre, "Transportes Díaz e Hijos")));

        var detalle = await LeerAsync(liquidacion.Id);

        Assert.Equal("Transportes Díaz e Hijos", detalle.Transportista.RazonSocial);
        Assert.Equal(fletero.Cuit, detalle.Transportista.Cuit);
    }

    [Theory]
    [InlineData(EstadoLiquidacion.Pendiente, 0, true, true, true, true)]
    [InlineData(EstadoLiquidacion.Pendiente, 50_000, true, false, false, true)]
    [InlineData(EstadoLiquidacion.Pendiente, 0, false, false, true, true)]
    [InlineData(EstadoLiquidacion.Pagada, 0, true, false, false, false)]
    [InlineData(EstadoLiquidacion.Anulada, 0, true, false, false, false)]
    public async Task Las_TresBanderas_SiguenAlEstadoLosPagosYElTransportista(
        EstadoLiquidacion estado,
        int pagado,
        bool transportistaActivo,
        bool puedeEditarse,
        bool puedeAnularse,
        bool puedeRegistrarPago)
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje], estado, pagado);

        if (!transportistaActivo)
        {
            await app.EnLaBaseAsync(contexto => contexto.Transportistas
                .Where(transportista => transportista.Id == fletero.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(transportista => transportista.Activo, false)));
        }

        var detalle = await LeerAsync(liquidacion.Id);

        Assert.Equal(puedeEditarse, detalle.PuedeEditarse);
        Assert.Equal(puedeAnularse, detalle.PuedeAnularse);
        Assert.Equal(puedeRegistrarPago, detalle.PuedeRegistrarPago);
    }

    [Fact]
    public async Task Una_LiquidacionInexistente_Responde404()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/liquidaciones/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(
            "liquidacion_no_encontrada",
            (await respuesta.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
    }

    private async Task<LiquidacionDetalleLeida> LeerAsync(int id)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        return (await cliente.GetFromJsonAsync<LiquidacionDetalleLeida>($"/api/liquidaciones/{id}"))!;
    }
}
