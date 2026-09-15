using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>Las personas del filtro del listado (FR-014, US2 esc. 12).</summary>
public class PersonasConAdelantosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Ofrece_ALasPersonasConAdelantos_AunqueEstenDeBaja_YNoALasQueNoTienen()
    {
        var chofer = await app.ChoferPropioAsync("Gómez", "Carlos");
        await app.CrearAdelantoAsync(chofer.Id, EstadoAdelanto.Rechazado, tipo: TipoBeneficiario.Chofer);
        await app.DarDeBajaFichaAsync(chofer.Id);
        await app.DarDeBajaPersonaAsync(chofer.Id);

        var empleadaSinAdelantos = await app.EmpleadoAsync("Ruiz", "Marta");
        var externo = await app.ChoferExternoAsync("Díaz", "Luis");

        var ids = await IdsAsync();

        Assert.Contains(chofer.Id, ids);
        Assert.DoesNotContain(empleadaSinAdelantos.Id, ids);
        Assert.DoesNotContain(externo.Id, ids);
    }

    [Fact]
    public async Task Ordena_PorApellido_Nombre_EId()
    {
        var zeta = await app.EmpleadoAsync("Zeta", "Ana");
        var alfaBeto = await app.EmpleadoAsync("Alfa", "Beto");
        var alfaAna = await app.EmpleadoAsync("Alfa", "Ana");
        int[] propios = [zeta.Id, alfaBeto.Id, alfaAna.Id];

        foreach (var id in propios)
        {
            await app.CrearAdelantoAsync(id);
        }

        Assert.Equal([alfaAna.Id, alfaBeto.Id, zeta.Id], (await IdsAsync()).Where(propios.Contains));
    }

    private async Task<List<int>> IdsAsync()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var personas = await cliente.GetFromJsonAsync<List<PersonaResumenLeida>>("/api/adelantos/personas");

        return [.. personas!.Select(persona => persona.Id)];
    }
}
