using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El transportista registrado en el viaje (FR-028, FR-029, SC-010; US3 esc. 9 y 10).
///
/// <b>Es una referencia al padrón, no una copia del nombre</b>, y esa distinción es todo el
/// requisito: el viaje no se mueve si el chofer cambia de transportista después —el trabajo lo hizo
/// el de entonces— pero sí muestra la razón social corregida, porque los datos se leen del padrón.
/// </summary>
public class TransportistaDelViajeTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Al_Asignar_QuedaRegistradoElTransportistaDelChofer()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(escenario.TransportistaId, despues!.TransportistaId);
    }

    /// <summary>
    /// US3 esc. 9: si después el chofer cambia de transportista, el viaje <b>no se mueve</b>. El
    /// trabajo lo hizo el de entonces, y moverlo reescribiría la historia (SC-010).
    /// </summary>
    [Fact]
    public async Task Si_ElChoferCambiaDeTransportista_ElViajeNoSeMueve()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        var otro = await app.CrearTransportistaAsync(nombre: "Transporte Nuevo");

        await app.EnLaBaseAsync(async contexto =>
        {
            var chofer = await contexto.Choferes.FirstAsync(c => c.Id == escenario.ChoferId);
            chofer.TransportistaId = otro.Id;
            await contexto.SaveChangesAsync();
        });

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(escenario.TransportistaId, despues!.TransportistaId);
        Assert.NotEqual(otro.Id, despues.TransportistaId);
    }

    /// <summary>
    /// US3 esc. 10: si le corrigen la razón social al transportista, el viaje <b>muestra la
    /// corregida</b>. Guardar el nombre en vez de la referencia habría congelado un error de tipeo.
    /// </summary>
    [Fact]
    public async Task Si_LeCorrigenLaRazonSocial_ElViajeMuestraLaCorregida()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, escenario);

        await app.EnLaBaseAsync(async contexto =>
        {
            var transportista = await contexto.Transportistas
                .FirstAsync(t => t.Id == escenario.TransportistaId);

            transportista.Nombre = "Transporte Sur S.R.L.";
            await contexto.SaveChangesAsync();
        });

        var ficha = await cliente.GetFromJsonAsync<ViajeDetalleLeido>($"/api/viajes/{viaje.Id}");

        Assert.Equal("Transporte Sur S.R.L.", ficha!.Transportista!.Nombre);
    }

    /// <summary>Reasignar el chofer vuelve a escribir el transportista, con el del nuevo (FR-028).</summary>
    [Fact]
    public async Task Reasignar_ElChofer_VuelveATomarSuTransportista()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var primero = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(primero);

        await BloqueoPorDocumentacionTests.Asignar(cliente, viaje.Id, primero);

        var segundo = await app.ArmarEscenarioAsync();

        await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = segundo.ChoferId, vehiculoId = primero.VehiculoId });

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(segundo.TransportistaId, despues!.TransportistaId);
    }

    /// <summary>
    /// FR-029: <b>no</b> se compara el transportista del vehículo con el del chofer. Un chofer de un
    /// transportista puede manejar una unidad de otro, y el transportista del viaje sale del chofer.
    /// </summary>
    [Fact]
    public async Task No_SeComparaElTransportistaDelVehiculoConElDelChofer()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var delChofer = await app.ArmarEscenarioAsync();
        var delVehiculo = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(delChofer);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = delChofer.ChoferId, vehiculoId = delVehiculo.VehiculoId });

        Assert.Equal(System.Net.HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(delChofer.TransportistaId, despues!.TransportistaId);
        Assert.Equal(delVehiculo.VehiculoId, despues.VehiculoId);
    }
}
