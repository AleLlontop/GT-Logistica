using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La búsqueda por texto (FR-042, US5 esc. 3 y 4).
///
/// <b>Sin distinguir mayúsculas ni acentos</b>, sobre origen, destino y razón social del cliente. Lo
/// resuelve una colación explícita en la consulta —<c>Latin1_General_CI_AI</c>— y no un recorrido en
/// memoria: quien busca "cordoba" en el teclado del galpón tiene que encontrar "Córdoba".
/// </summary>
public class BusquedaViajesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Encuentra_SinAcentosYSinDistinguirMayusculas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, origen: "Rosario", destino: "Córdoba");

        foreach (var texto in new[] { "cordoba", "CÓRDOBA", "CORDOBA", "córdoba", "cÓrDoBa" })
        {
            var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
                $"/api/viajes?clienteId={padron.Id}&busqueda={Uri.EscapeDataString(texto)}");

            Assert.Equal(1, pagina!.Total);
        }
    }

    [Fact]
    public async Task Busca_SobreOrigenDestinoYRazonSocial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync(razonSocial: "Distribuidora del Litoral");

        var porOrigen = await app.CrearViajeAsync(padron.Id, origen: "Paraná", destino: "Salta");
        var porDestino = await app.CrearViajeAsync(padron.Id, origen: "Salta", destino: "Paraná");

        var resultado = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=parana");

        Assert.Equal(2, resultado!.Total);
        Assert.Contains(resultado.Items, fila => fila.Id == porOrigen.Id);
        Assert.Contains(resultado.Items, fila => fila.Id == porDestino.Id);

        // Y por razón social del cliente, que es el tercer campo de FR-042.
        var porCliente = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=litoral");

        Assert.Equal(2, porCliente!.Total);
    }

    [Fact]
    public async Task La_Busqueda_EsParcial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, origen: "San Miguel de Tucumán", destino: "Salta");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=tucuman");

        Assert.Equal(1, pagina!.Total);
    }

    /// <summary>US5 esc. 4: la búsqueda se combina con los filtros, no los reemplaza.</summary>
    [Fact]
    public async Task Se_CombinaConLosFiltros()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var otro = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, origen: "Rosario", destino: "Córdoba");
        await app.CrearViajeAsync(otro.Id, origen: "Rosario", destino: "Córdoba");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=cordoba");

        Assert.Equal(1, pagina!.Total);
    }

    [Fact]
    public async Task Sin_Coincidencias_DevuelveLaPaginaVacia()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();

        await app.CrearViajeAsync(padron.Id, origen: "Rosario", destino: "Córdoba");

        var pagina = await cliente.GetFromJsonAsync<PaginaDeViajesLeida>(
            $"/api/viajes?clienteId={padron.Id}&busqueda=ushuaia");

        Assert.Equal(0, pagina!.Total);
        Assert.Empty(pagina.Items);
    }
}
