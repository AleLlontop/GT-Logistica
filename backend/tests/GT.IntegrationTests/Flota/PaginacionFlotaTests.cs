using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-032 y US4 esc. 9: 20 filas por página, con el total de coincidencias sobre <b>toda</b> la flota
/// y un orden total.
///
/// El orden por <c>Patente, Id</c> es lo que impide que dos consultas idénticas devuelvan resultados
/// distintos o que una fila aparezca en dos páginas (convención [003], research §9).
/// </summary>
public class PaginacionFlotaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const int Vehiculos = 25;

    [Fact]
    public async Task Veinticinco_Vehiculos_DanVeinteYCinco_ConElTotalEnVeinticinco()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la paginación");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la paginación");

        for (var i = 0; i < Vehiculos; i++)
        {
            await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        }

        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&pagina=1");

        var segunda = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&pagina=2");

        Assert.Equal(20, primera!.Items.Count);
        Assert.Equal(5, segunda!.Items.Count);

        // El total cuenta las coincidencias completas, no las de la página (FR-032).
        Assert.Equal(Vehiculos, primera.Total);
        Assert.Equal(Vehiculos, segunda.Total);
        Assert.Equal(20, primera.TamanioPagina);

        // Ninguna fila aparece en dos páginas.
        var idsPrimera = primera.Items.Select(v => v.Id).ToHashSet();
        Assert.DoesNotContain(segunda.Items, v => idsPrimera.Contains(v.Id));

        // Y entre las dos está toda la flota, sin faltantes.
        Assert.Equal(Vehiculos, idsPrimera.Count + segunda.Items.Count);
    }

    /// <summary>
    /// El orden es estable entre dos consultas iguales: es lo que la convención [003] pide con el
    /// criterio total, y sin él una fila podría intercambiarse entre páginas.
    /// </summary>
    [Fact]
    public async Task El_Orden_EsElMismoEntreDosConsultasIguales()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del orden estable");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del orden estable");

        for (var i = 0; i < 5; i++)
        {
            await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        }

        var cliente = await app.CrearClienteAutenticadoAsync();
        var ruta = $"/api/flota/vehiculos?transportistaId={transportista.Id}";

        var primera = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(ruta);
        var segunda = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(ruta);

        Assert.Equal(
            primera!.Items.Select(v => v.Id),
            segunda!.Items.Select(v => v.Id));

        // Y ordena por patente, que es el criterio que la pantalla muestra.
        Assert.Equal(
            primera.Items.Select(v => v.Patente).Order(StringComparer.Ordinal),
            primera.Items.Select(v => v.Patente));
    }

    /// <summary>
    /// Una página fuera de rango no es un error: devuelve la lista vacía con el total real, para que
    /// la pantalla pueda decir cuántas coincidencias hay y volver a la primera.
    /// </summary>
    [Fact]
    public async Task Una_PaginaFueraDeRango_DevuelveVacioConElTotalReal()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la página vacía");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la página vacía");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&pagina=99");

        Assert.Empty(pagina!.Items);
        Assert.Equal(1, pagina.Total);
    }

    /// <summary>
    /// Los filtros se aplican sobre toda la flota <b>antes</b> de paginar, así que el total refleja
    /// los filtros y no el padrón entero (FR-032).
    /// </summary>
    [Fact]
    public async Task El_Total_RespetaLosFiltros()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del total filtrado");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del total filtrado");

        await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);
        await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var activos = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        var dadosDeBaja = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=dadoDeBaja");

        Assert.Equal(1, activos!.Total);
        Assert.Equal(2, dadosDeBaja!.Total);
    }
}
