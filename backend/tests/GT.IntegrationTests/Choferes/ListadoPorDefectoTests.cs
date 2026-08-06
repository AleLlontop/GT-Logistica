using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Sin filtro de estado, el listado devuelve <b>sólo los activos</b> (FR-022).
///
/// No es lo mismo que "todos": un listado que oculta choferes sin decirlo se lee como un error de
/// datos, así que la pantalla muestra el filtro en <c>Activo</c> y quien quiera ver los dados de
/// baja lo cambia.
/// </summary>
public class ListadoPorDefectoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_Estado_DevuelveSoloActivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Activos por defecto");

        var activo = await app.CrearChoferCompletoAsync(13111222, transportistaId: transportista.Id);
        var inactivo = await app.CrearChoferCompletoAsync(
            13211222,
            activo: false,
            transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(activo.Id, fila.Id);
        Assert.True(fila.Activo);

        Assert.DoesNotContain(pagina.Items, chofer => chofer.Id == inactivo.Id);
        Assert.Equal(1, pagina.Total);
    }

    [Fact]
    public async Task Con_EstadoInactivo_DevuelveSoloLosDadosDeBaja()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Inactivos explícitos");

        var activo = await app.CrearChoferCompletoAsync(13311222, transportistaId: transportista.Id);
        var inactivo = await app.CrearChoferCompletoAsync(
            13411222,
            activo: false,
            transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&estado=inactivo");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(inactivo.Id, fila.Id);
        Assert.False(fila.Activo);

        Assert.DoesNotContain(pagina.Items, chofer => chofer.Id == activo.Id);
    }

    /// <summary>Los filtros de texto son parciales y no distinguen mayúsculas (FR-022).</summary>
    [Fact]
    public async Task Filtra_PorApellidoParcial_YPorDniParcial()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Filtros de texto");

        var persona = await app.CrearPersonaAsync(
            dni: "13511222",
            nombre: "Ramona",
            apellido: "Gutiérrez");
        var chofer = await app.CrearChoferAsync(
            persona.Id,
            transportista.Id,
            cuil: DatosDePrueba.CuilValidoPara(13511222));

        await app.CrearChoferCompletoAsync(13611222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var porApellido = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&apellido=utiérr");
        Assert.Equal(chofer.Id, Assert.Single(porApellido!.Items).Id);

        var porDni = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&dni=35112");
        Assert.Equal(chofer.Id, Assert.Single(porDni!.Items).Id);
    }

    /// <summary>El DNI se normaliza antes de buscar, así que con puntos encuentra igual (FR-025).</summary>
    [Fact]
    public async Task Filtra_PorDni_AunqueSeEscribaConPuntos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "DNI con puntos");
        var chofer = await app.CrearChoferCompletoAsync(13711222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&dni=13.711.222");

        Assert.Equal(chofer.Id, Assert.Single(pagina!.Items).Id);
    }
}
