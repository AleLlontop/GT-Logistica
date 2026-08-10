using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-008d, US6 esc. 12 y SC-008: la baja de un transportista pasa a mirar <b>también su flota</b>, e
/// informa las dos cantidades.
///
/// Es el segundo —y último— cambio que este módulo hace sobre el Módulo 3. Sin él, un vehículo activo
/// podría quedar apuntando a un transportista inactivo, que es exactamente el estado que FR-008a
/// prohíbe crear desde el alta (research §8).
/// </summary>
public class BajaTransportistaConFlotaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_LaBaja_ConVehiculosActivos_AunqueNoTengaChoferes()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transportista con flota activa");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la flota que traba la baja");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("transportista_con_choferes", error!.Codigo);

        // Las dos cantidades **por separado**: son dos problemas distintos, que se resuelven en dos
        // pantallas distintas (SC-008).
        Assert.Equal(
            "No se puede dar de baja: 0 chofer(es) y 2 vehículo(s) activos dependen de este " +
            "transportista. Reasignalos o dalos de baja primero.",
            error.Mensaje);

        Assert.Equal(0, error.CantidadChoferes);
        Assert.Equal(2, error.CantidadVehiculos);
    }

    /// <summary>Con dependientes de los dos lados, el mensaje informa los dos números.</summary>
    [Fact]
    public async Task Informa_LasDosCantidades_CuandoHayChoferesYVehiculos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transportista con las dos cosas");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de las dos cosas");

        await app.CrearChoferCompletoAsync(51111222, transportistaId: transportista.Id);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();

        Assert.Equal(1, error!.CantidadChoferes);
        Assert.Equal(1, error.CantidadVehiculos);
        Assert.Contains("1 chofer(es) y 1 vehículo(s)", error.Mensaje);
    }

    /// <summary>
    /// La baja procede cuando choferes y vehículos están <b>todos inactivos</b>: es el caso límite
    /// explícito de la spec, y la asimetría deliberada con los catálogos (research §8).
    /// </summary>
    [Fact]
    public async Task Procede_CuandoChoferesYVehiculosEstanTodosInactivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transportista sin nada activo");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo sin nada activo");

        await app.CrearChoferCompletoAsync(
            51211222,
            activo: false,
            transportistaId: transportista.Id);

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>
    /// El listado de transportistas muestra la cantidad de vehículos activos junto a la de choferes:
    /// es lo que explica por qué algunos no se pueden dar de baja.
    /// </summary>
    [Fact]
    public async Task El_Listado_InformaLosVehiculosActivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transportista que informa flota");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo que se informa");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var listado = await cliente.GetFromJsonAsync<List<TransportistaLeido>>("/api/transportistas");

        var fila = Assert.Single(listado!, t => t.Id == transportista.Id);

        // Sólo los activos: son los que impiden la baja (FR-008d).
        Assert.Equal(1, fila.VehiculosActivos);
        Assert.Equal(0, fila.ChoferesActivos);
    }

    private record TransportistaLeido(
        int Id,
        string Nombre,
        bool Activo,
        int ChoferesActivos,
        int VehiculosActivos);
}
