using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La edición de un viaje (FR-017, FR-019a, FR-022a, FR-034, SC-004; US2 esc. 13 y 14, US3 esc. 15).
///
/// <b>El test que sostiene SC-004</b> es el de la fecha: mover un viaje asignado a un día en que la
/// documentación está vencida no puede guardar <b>nada</b>, ni la fecha ni los demás campos del mismo
/// <c>PUT</c>. Sin esa revalidación hay dos reglas —una para asignar y otra para editar— y un agujero
/// entre las dos.
/// </summary>
public class ModificarViajeTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static int _semilla = 70_000_000;

    /// <summary>FR-017: la edición aplica las mismas validaciones que el alta.</summary>
    [Fact]
    public async Task La_Edicion_AplicaLasMismasValidacionesQueElAlta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = padron.Id,
            fecha = "2026-08-11",
            origen = "Rosario",
            destino = "Córdoba",
            importe = -5,
        });

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("importe_negativo", error!.Codigo);
    }

    /// <summary>
    /// US3 esc. 15: el cuerpo no acepta <c>numero</c>, <c>estado</c>, <c>choferId</c> ni
    /// <c>vehiculoId</c>. No es que los ignore: no están en el contrato de entrada (FR-019a, FR-034).
    /// Corregir un destino no puede avanzar el viaje ni cambiar quién lo maneja.
    /// </summary>
    [Fact]
    public async Task El_Cuerpo_NoPuedeTocarLaAsignacionNiElEstado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await ArmarEscenarioAsignadoAsync();

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{escenario.ViajeId}", new
        {
            clienteId = escenario.ClienteId,
            fecha = escenario.Fecha.ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Santa Fe",
            estado = "enCurso",
            choferId = (int?)null,
            vehiculoId = (int?)null,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(escenario.ViajeId);

        Assert.Equal("Santa Fe", despues!.Destino);
        Assert.Equal(EstadoViaje.Pendiente, despues.Estado);
        Assert.Equal(escenario.ChoferId, despues.ChoferId);
        Assert.Equal(escenario.VehiculoId, despues.VehiculoId);
    }

    /// <summary>
    /// FR-022a y SC-004: al mover la fecha a un día en que la documentación está vencida,
    /// <b>no se guarda nada</b>. El test cambia además el destino en el mismo cuerpo, justamente para
    /// verificar que el rechazo aborta el <c>PUT</c> entero y no sólo el campo fecha.
    /// </summary>
    [Fact]
    public async Task Mover_LaFechaAUnDiaConDocumentacionVencida_NoGuardaNada()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await ArmarEscenarioAsignadoAsync(diasHastaVencimiento: 10);

        // La licencia vence en 10 días; el viaje se quiere mover a dentro de 40.
        var fechaNueva = FechaHoyArgentina.Hoy().AddDays(40);

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{escenario.ViajeId}", new
        {
            clienteId = escenario.ClienteId,
            fecha = fechaNueva.ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Mendoza",
        });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("fecha_bloquea_asignacion", error!.Codigo);

        // Qué unidad y qué documento lo impiden, en el cuerpo además de en el texto (SC-004).
        Assert.NotNull(error.UnidadQueBloquea);
        Assert.NotNull(error.DocumentoQueBloquea);

        // Y no se guardó nada: ni la fecha ni el destino del mismo cuerpo.
        var despues = await app.RecargarViajeAsync(escenario.ViajeId);

        Assert.Equal(escenario.Fecha, despues!.Fecha);
        Assert.Equal("Córdoba", despues.Destino);
    }

    /// <summary>
    /// El espejo: si a la fecha nueva la documentación sigue vigente, la edición procede normalmente.
    /// </summary>
    [Fact]
    public async Task Mover_LaFechaAUnDiaConDocumentacionVigente_Procede()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await ArmarEscenarioAsignadoAsync(diasHastaVencimiento: 400);

        var fechaNueva = FechaHoyArgentina.Hoy().AddDays(40);

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{escenario.ViajeId}", new
        {
            clienteId = escenario.ClienteId,
            fecha = fechaNueva.ToString("yyyy-MM-dd"),
            origen = "Rosario",
            destino = "Mendoza",
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(escenario.ViajeId);
        Assert.Equal(fechaNueva, despues!.Fecha);
    }

    /// <summary>
    /// Un viaje sin asignar no tiene documentación que revalidar: la fecha se mueve libremente
    /// (FR-022a, que sólo aplica cuando hay unidades asignadas).
    /// </summary>
    [Fact]
    public async Task Un_ViajeSinAsignar_MueveLaFechaLibremente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id);

        var respuesta = await cliente.PutAsJsonAsync($"/api/viajes/{viaje.Id}", new
        {
            clienteId = padron.Id,
            fecha = "2030-01-15",
            origen = "Rosario",
            destino = "Córdoba",
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private record EscenarioAsignado(
        int ClienteId,
        int ViajeId,
        int ChoferId,
        int VehiculoId,
        DateOnly Fecha);

    /// <summary>
    /// Un viaje pendiente con chofer y vehículo asignados, y una licencia del chofer que vence en
    /// <paramref name="diasHastaVencimiento"/> días contados desde hoy.
    /// </summary>
    private async Task<EscenarioAsignado> ArmarEscenarioAsignadoAsync(int diasHastaVencimiento = 400)
    {
        var padron = await app.CrearClienteAsync();
        var transportista = await app.CrearTransportistaAsync();

        var chofer = await app.CrearChoferCompletoAsync(
            semilla: Interlocked.Increment(ref _semilla),
            transportistaId: transportista.Id);

        var tipo = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: 0);
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento);

        var tipoVehiculo = await app.CrearTipoVehiculoAsync();

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        var fecha = FechaHoyArgentina.Hoy();

        var viaje = await app.CrearViajeAsync(
            padron.Id,
            fecha: fecha,
            choferId: chofer.Id,
            vehiculoId: vehiculo.Id,
            transportistaId: transportista.Id);

        return new EscenarioAsignado(padron.Id, viaje.Id, chofer.Id, vehiculo.Id, fecha);
    }
}
