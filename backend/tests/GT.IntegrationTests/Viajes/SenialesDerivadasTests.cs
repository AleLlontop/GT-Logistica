using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Las dos señales derivadas del listado (FR-016, FR-039).
///
/// <b>El test central es el de la comparación</b>: <c>demorado</c> se resuelve con una subconsulta en
/// SQL en el listado y con <c>Viaje.EstaDemorado</c> en C# en la ficha. Son dos escrituras de la
/// misma regla, y la convención [003] pide un test que las compare sobre el mismo dato — es
/// exactamente el mismo precedente por el que el Módulo 4 compara su estado de documentación.
///
/// La demora no se puede provocar a mano —haría falta esperar cinco días—, así que el instante del
/// pase a <c>en curso</c> se retrasa contra la base (plan §Principio IV, quickstart paso 22).
/// </summary>
public class SenialesDerivadasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_Demorado_DelListadoCoincideConLaReglaEnCSharp()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        // Se retrasa la línea del historial: el viaje arrancó hace más de cinco días.
        await RetrasarElArranqueAsync(viaje.Id, dias: 8);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={escenario.ClienteId}");

        var fila = Assert.Single(pagina!.Items);

        // Lo que devolvió la subconsulta en SQL…
        Assert.True(fila.Demorado);

        // …y lo que devuelve la regla en C# sobre el mismo instante del mismo historial.
        var historial = await app.HistorialDeAsync(viaje.Id);

        var enCursoDesde = historial
            .Where(linea => linea.EstadoNuevo == EstadoViaje.EnCurso)
            .Max(linea => (DateTime?)linea.OcurridoEn);

        Assert.Equal(
            Viaje.EstaDemorado(enCursoDesde, DateTime.UtcNow),
            fila.Demorado);

        // Y la ficha, que usa la regla en C#, dice lo mismo que el listado.
        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>($"/api/viajes/{viaje.Id}");
        Assert.Equal(fila.Demorado, ficha!.Demorado);
    }

    [Fact]
    public async Task Un_ViajeQueArrancoHoy_NoEstaDemorado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={escenario.ClienteId}");

        Assert.False(Assert.Single(pagina!.Items).Demorado);
    }

    /// <summary>
    /// La demora es una señal, no un quinto estado: el viaje sigue <c>enCurso</c> y el sistema no le
    /// cambia el estado a nadie por sí solo (FR-039).
    /// </summary>
    [Fact]
    public async Task La_Demora_NoLeCambiaElEstadoAlViaje()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);
        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        await RetrasarElArranqueAsync(viaje.Id, dias: 30);

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={escenario.ClienteId}");

        var fila = Assert.Single(pagina!.Items);

        Assert.True(fila.Demorado);
        Assert.Equal("enCurso", fila.Estado);

        var guardado = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.EnCurso, guardado!.Estado);
    }

    /// <summary>Un viaje que nunca arrancó no tiene de qué contar: la subconsulta devuelve `false`.</summary>
    [Fact]
    public async Task Un_ViajePendienteViejo_NoEstaDemorado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, fecha: FechaHoyArgentina.Hoy().AddMonths(-6));

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}");

        Assert.False(Assert.Single(pagina!.Items).Demorado);
    }

    /// <summary>FR-016: se calcula contra el día en curso en Argentina, no contra el del servidor.</summary>
    [Fact]
    public async Task El_EsRetroactivo_SeCalculaContraElDiaEnCursoEnArgentina()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var ayer = await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(-1));
        var deHoy = await app.CrearViajeAsync(padron.Id, fecha: hoy);
        var manana = await app.CrearViajeAsync(padron.Id, fecha: hoy.AddDays(1));

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}");

        var porId = pagina!.Items.ToDictionary(fila => fila.Id);

        Assert.True(porId[ayer.Id].EsRetroactivo);
        Assert.False(porId[deHoy.Id].EsRetroactivo);
        Assert.False(porId[manana.Id].EsRetroactivo);
    }

    private Task RetrasarElArranqueAsync(int viajeId, int dias) =>
        app.EnLaBaseAsync(async contexto =>
        {
            var linea = await contexto.CambiosDeEstadoViaje
                .FirstAsync(cambio =>
                    cambio.ViajeId == viajeId && cambio.EstadoNuevo == EstadoViaje.EnCurso);

            await contexto.CambiosDeEstadoViaje
                .Where(cambio => cambio.Id == linea.Id)
                .ExecuteUpdateAsync(fila => fila.SetProperty(
                    cambio => cambio.OcurridoEn,
                    linea.OcurridoEn.AddDays(-dias)));
        });
}
