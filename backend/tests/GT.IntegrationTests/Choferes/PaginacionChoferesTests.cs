using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Paginación del listado (FR-030, research §9).
///
/// El caso que este test protege es el que no se descubre con pocos datos: sin un orden
/// <b>total</b>, dos choferes homónimos pueden intercambiarse entre páginas y aparecer duplicados o
/// desaparecer. Por eso el orden termina en <c>Id</c> y por eso acá se cargan homónimos a propósito.
/// </summary>
public class PaginacionChoferesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const int Choferes = 25;

    /// <param name="rango">
    /// Todos los tests de la clase comparten la misma base, así que cada uno arma su padrón en su
    /// propio rango de DNI: repetirlos chocaría contra el índice único del padrón de personas.
    /// </param>
    private async Task<int> PrepararPadronAsync(int rango)
    {
        var transportista = await app.CrearTransportistaAsync(nombre: $"Padrón paginado {rango}");

        for (var i = 0; i < Choferes; i++)
        {
            var semilla = 12000000 + rango * 100 + i;
            var persona = await app.CrearPersonaAsync(
                dni: $"{semilla:D8}",
                // Todos homónimos: es lo que hace que el desempate por Id importe.
                nombre: "Ana",
                apellido: "González");

            await app.CrearChoferAsync(
                persona.Id,
                transportista.Id,
                cuil: DatosDePrueba.CuilValidoPara(semilla));
        }

        return transportista.Id;
    }

    [Fact]
    public async Task Devuelve_VeinteYCinco_ConElTotalCompleto()
    {
        var transportistaId = await PrepararPadronAsync(1);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}");

        Assert.Equal(20, primera!.Items.Count);
        Assert.Equal(Choferes, primera.Total);
        Assert.Equal(1, primera.Pagina);
        Assert.Equal(20, primera.TamanioPagina);

        var segunda = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}&pagina=2");

        Assert.Equal(5, segunda!.Items.Count);
        Assert.Equal(Choferes, segunda.Total);
        Assert.Equal(2, segunda.Pagina);
    }

    [Fact]
    public async Task Ninguna_FilaApareceEnDosPaginas()
    {
        var transportistaId = await PrepararPadronAsync(2);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var primera = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}");
        var segunda = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}&pagina=2");

        var todos = primera!.Items.Concat(segunda!.Items).Select(chofer => chofer.Id).ToList();

        Assert.Equal(Choferes, todos.Count);
        Assert.Equal(Choferes, todos.Distinct().Count());
    }

    /// <summary>Dos consultas idénticas devuelven lo mismo en el mismo orden.</summary>
    [Fact]
    public async Task El_OrdenSeRepiteEntreConsultasIguales()
    {
        var transportistaId = await PrepararPadronAsync(3);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var una = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}");
        var otra = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}");

        Assert.Equal(
            una!.Items.Select(chofer => chofer.Id),
            otra!.Items.Select(chofer => chofer.Id));
    }

    /// <summary>Una página fuera de rango no es un error: items vacío con el total real.</summary>
    [Fact]
    public async Task Una_PaginaFueraDeRango_DevuelveVacioConElTotalReal()
    {
        var transportistaId = await PrepararPadronAsync(4);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportistaId}&pagina=99");

        Assert.Empty(pagina!.Items);
        Assert.Equal(Choferes, pagina.Total);
    }
}
