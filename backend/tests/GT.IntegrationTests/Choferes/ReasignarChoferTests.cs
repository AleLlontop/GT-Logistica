using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// SC-009: reasignar un chofer de transportista <b>conserva su documentación</b>.
///
/// Sale del diseño —los documentos cuelgan del chofer, no del transportista— y por eso el test
/// existe: para que quede fijado y no se rompa sin que nadie se entere.
/// </summary>
public class ReasignarChoferTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Modificacion(
        string dni,
        string cuil,
        int transportistaId,
        string nombre = "Ramona",
        string apellido = "Gómez",
        string fechaNacimiento = "1990-05-17",
        string telefono = "11-5555-5555",
        string email = "ramona@gt.com.ar") => new
        {
            nombre,
            apellido,
            dni,
            cuil,
            fechaNacimiento,
            telefono,
            email,
            transportistaId,
        };

    [Fact]
    public async Task Reasignar_ConservaLaDocumentacion()
    {
        var original = await app.CrearTransportistaAsync(nombre: "Transporte original");
        var destino = await app.CrearTransportistaAsync(nombre: "Transporte destino");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia reasignada");

        var chofer = await app.CrearChoferCompletoAsync(17111222, transportistaId: original.Id);
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 400, numero: "LIC-REASIGNADA");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            Modificacion("17111222", DatosDePrueba.CuilValidoPara(17111222), destino.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        // Cambió de transportista y la documentación sigue entera.
        var documento = Assert.Single(ficha!.Documentos);
        Assert.Equal("LIC-REASIGNADA", documento.Numero);
        Assert.Equal("enRegla", ficha.EstadoDocumentacion);

        var listado = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={destino.Id}");
        Assert.Equal(chofer.Id, Assert.Single(listado!.Items).Id);
    }

    /// <summary>Los datos personales se guardan en la persona del padrón, no en una copia.</summary>
    [Fact]
    public async Task Modificar_ActualizaLaPersonaDelPadron()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Datos personales");
        var chofer = await app.CrearChoferCompletoAsync(17211222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            Modificacion(
                "17211222",
                DatosDePrueba.CuilValidoPara(17211222),
                transportista.Id,
                apellido: "Gutiérrez",
                telefono: "11-4444-4444"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var persona = await app.RecargarPersonaAsync(chofer.PersonaId);

        Assert.Equal("Gutiérrez", persona!.Apellido);
        Assert.Equal("11-4444-4444", persona.Telefono);
    }

    [Fact]
    public async Task Rechaza_LaReasignacion_AUnTransportistaInactivo()
    {
        var original = await app.CrearTransportistaAsync(nombre: "Origen válido");
        var inactivo = await app.CrearTransportistaAsync(nombre: "Destino inactivo", activo: false);

        var chofer = await app.CrearChoferCompletoAsync(17311222, transportistaId: original.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/choferes/{chofer.Id}",
            Modificacion("17311222", DatosDePrueba.CuilValidoPara(17311222), inactivo.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlModificarUnChoferInexistente()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Para inexistente");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/choferes/999999",
            Modificacion("17411222", DatosDePrueba.CuilValidoPara(17411222), transportista.Id));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
