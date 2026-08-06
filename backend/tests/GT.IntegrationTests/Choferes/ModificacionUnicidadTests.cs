using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// La unicidad al modificar <b>excluye al propio registro</b> (FR-003, FR-007).
///
/// Es el error clásico de reusar la validación del alta en la modificación: guardar un formulario
/// sin cambiarle nada fallaría diciendo que el CUIL ya existe, y existe porque es el suyo.
/// </summary>
public class ModificacionUnicidadTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object ModificacionChofer(string dni, string cuil, int transportistaId) => new
    {
        nombre = "Ramona",
        apellido = "Gómez",
        dni,
        cuil,
        fechaNacimiento = "1990-05-17",
        telefono = "11-5555-5555",
        email = "ramona@gt.com.ar",
        transportistaId,
    };

    private static object ModificacionTransportista(string nombre, string cuit) => new
    {
        nombre,
        cuit,
        tipo = "juridica",
        telefono = "11-5555-5555",
        email = "info@gt.com.ar",
    };

    [Fact]
    public async Task Un_ChoferPuedeGuardarSuPropioCuilYSuPropioDni()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Unicidad propia");
        var chofer = await app.CrearChoferCompletoAsync(18111222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            ModificacionChofer("18111222", DatosDePrueba.CuilValidoPara(18111222), transportista.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_ElCuilDeOtroChofer()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "CUIL ajeno");

        var chofer = await app.CrearChoferCompletoAsync(18211222, transportistaId: transportista.Id);
        await app.CrearChoferCompletoAsync(18311222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            ModificacionChofer("18211222", DatosDePrueba.CuilValidoPara(18311222), transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("cuil_duplicado", error!.Codigo);
        Assert.Equal("cuil", error.Campo);
    }

    [Fact]
    public async Task Rechaza_ElDniDeOtraPersona()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "DNI ajeno");

        var chofer = await app.CrearChoferCompletoAsync(18411222, transportistaId: transportista.Id);
        await app.CrearPersonaAsync(dni: "18511222");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            ModificacionChofer("18511222", DatosDePrueba.CuilValidoPara(18411222), transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("dni_duplicado", error!.Codigo);
    }

    [Fact]
    public async Task Un_TransportistaPuedeGuardarSuPropioCuit()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "CUIT propio", cuit: "30780000003");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/transportistas/{transportista.Id}",
            ModificacionTransportista("CUIT propio renombrado", "30-78000000-3"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var leido = await respuesta.Content.ReadFromJsonAsync<TransportistaLeido>();
        Assert.Equal("CUIT propio renombrado", leido!.Nombre);
        Assert.Equal("30780000003", leido.Cuit);
    }

    [Fact]
    public async Task Rechaza_ElCuitDeOtroTransportista()
    {
        var uno = await app.CrearTransportistaAsync(nombre: "CUIT uno", cuit: "30790000001");
        await app.CrearTransportistaAsync(nombre: "CUIT dos", cuit: "30800000005");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/transportistas/{uno.Id}",
            ModificacionTransportista("CUIT uno", "30800000005"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("cuit_duplicado", error!.Codigo);
    }

    private record TransportistaLeido(
        int Id,
        string Nombre,
        string Cuit,
        string Tipo,
        string Telefono,
        string Email,
        bool Activo,
        int ChoferesActivos);
}
