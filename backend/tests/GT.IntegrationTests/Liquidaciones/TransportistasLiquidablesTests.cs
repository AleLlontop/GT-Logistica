using System.Net.Http.Json;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Externo es todo transportista cuyo CUIT no es el de la empresa emisora (FR-001, research §5).
/// </summary>
public class TransportistasLiquidablesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string Ruta = "/api/liquidaciones/transportistas";

    [Fact]
    public async Task Excluye_AlTransportistaConElCuitDeLaEmpresaEmisora_YOrdenaPorRazonSocial()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var propio = await app.TransportistaPropioAsync();
        var zeta = await app.TransportistaExternoAsync("Zeta Fletes");
        var alfa = await app.TransportistaExternoAsync("Alfa Cargas");

        var respuesta = await LeerAsync(Ruta);

        Assert.True(respuesta.EmpresaEmisoraConfigurada);
        Assert.DoesNotContain(respuesta.Transportistas, transportista => transportista.Id == propio.Id);

        var ids = respuesta.Transportistas.Select(transportista => transportista.Id).ToList();

        Assert.True(ids.IndexOf(alfa.Id) >= 0 && ids.IndexOf(alfa.Id) < ids.IndexOf(zeta.Id));
    }

    [Fact]
    public async Task Sin_IncluirInactivos_SoloActivos_YConElTambienLosDadosDeBaja()
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var activo = await app.TransportistaExternoAsync("Activo S.R.L.");
        var dadoDeBaja = await app.TransportistaExternoAsync("Dado de Baja S.A.", activo: false);

        var soloActivos = await LeerAsync(Ruta);
        Assert.Contains(soloActivos.Transportistas, transportista => transportista.Id == activo.Id);
        Assert.DoesNotContain(soloActivos.Transportistas, transportista => transportista.Id == dadoDeBaja.Id);

        var todos = await LeerAsync($"{Ruta}?incluirInactivos=true");
        var inactivo = Assert.Single(todos.Transportistas, transportista => transportista.Id == dadoDeBaja.Id);
        Assert.False(inactivo.Activo);
    }

    [Fact]
    public async Task Sin_EmpresaEmisora_DeclaraQueNoEstaConfigurada_YNoOfreceNinguno()
    {
        await app.TransportistaExternoAsync();
        await app.SinEmpresaEmisoraAsync();

        var respuesta = await LeerAsync(Ruta);

        Assert.False(respuesta.EmpresaEmisoraConfigurada);
        Assert.Empty(respuesta.Transportistas);
    }

    private async Task<TransportistasLiquidablesLeidos> LeerAsync(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        return (await cliente.GetFromJsonAsync<TransportistasLiquidablesLeidos>(ruta))!;
    }
}
