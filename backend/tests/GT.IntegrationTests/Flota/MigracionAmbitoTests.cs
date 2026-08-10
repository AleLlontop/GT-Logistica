using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-017c: la migración del ámbito no rompe nada de lo ya cargado (quickstart paso 2).
///
/// La columna se crea con valor por defecto <c>Chofer</c> en la misma sentencia, así que <b>todos</b>
/// los tipos preexistentes quedan con ámbito chofer y ningún documento existente cambia de
/// comportamiento. No hace falta ninguna corrección manual.
///
/// Es un test del <i>resultado</i> de la migración, no de su ejecución: la aplicación de prueba corre
/// las migraciones al levantar, así que lo que se verifica es el estado en el que quedó la base.
/// </summary>
public class MigracionAmbitoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// Un tipo insertado <b>sin</b> declarar el ámbito —como los que existían antes del Módulo 4—
    /// queda en chofer por el valor por defecto de la columna.
    /// </summary>
    [Fact]
    public async Task Los_TiposPreexistentes_QuedanEnAmbitoChofer()
    {
        var id = await app.ConAlcanceAsync(async contexto =>
        {
            // Se escribe por SQL directo, salteando el modelo, para reproducir una fila cargada antes
            // de que la columna existiera: la migración es la que le pone el valor.
            await contexto.Database.ExecuteSqlRawAsync(
                "INSERT INTO DocumentacionTipos (Nombre, DiasAvisoVencimiento, Activo) " +
                "VALUES ('Tipo anterior al Módulo 4', 30, 1)");

            return await contexto.DocumentacionTipos
                .Where(tipo => tipo.Nombre == "Tipo anterior al Módulo 4")
                .Select(tipo => tipo.Id)
                .FirstAsync();
        });

        var tipo = await app.ConAlcanceAsync(contexto => contexto.DocumentacionTipos
            .AsNoTracking()
            .FirstAsync(t => t.Id == id));

        Assert.Equal(DocumentacionAmbito.Chofer, tipo.Ambito);
    }

    /// <summary>
    /// Y lo que importa de verdad: un documento de chofer cargado con ese tipo <b>sigue calculando su
    /// estado igual</b>. El ámbito no entra en la regla de vencimientos (FR-017c).
    /// </summary>
    [Fact]
    public async Task Ningun_DocumentoDeChofer_CambiaDeEstadoPorLaMigracion()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 41111222);
        var tipo = await app.CrearTipoDocumentacionAsync(
            nombre: "Licencia migrada",
            diasAvisoVencimiento: 30);

        // Uno al día y uno dentro de la ventana de aviso: los dos bordes que la regla distingue.
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: 20);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<ChoferConEstado>($"/api/choferes/{chofer.Id}");

        Assert.Equal("proximaAvencer", ficha!.EstadoDocumentacion);
        Assert.Equal("proximaAvencer", Assert.Single(ficha.Documentos).Estado);
    }

    /// <summary>
    /// El catálogo del Módulo 3 sigue devolviendo sus tipos, ahora con el ámbito a la vista. Sin el
    /// filtro se ven los dos ámbitos, que es lo que muestra la pantalla de mantenimiento (FR-017a).
    /// </summary>
    [Fact]
    public async Task El_CatalogoSigueDevolviendoLosDeChofer_ConSuAmbito()
    {
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Psicofísico con ámbito");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var catalogo = await cliente.GetFromJsonAsync<List<TipoDocumentacionLeido>>(
            "/api/tipos-documentacion");

        var fila = Assert.Single(catalogo!, t => t.Id == tipo.Id);
        Assert.Equal("chofer", fila.Ambito);
    }

    private record ChoferConEstado(string EstadoDocumentacion, List<DocumentoConEstado> Documentos);

    private record DocumentoConEstado(int Id, string Estado);

    private record TipoDocumentacionLeido(
        int Id,
        string Nombre,
        int DiasAvisoVencimiento,
        string Ambito,
        bool Activo,
        int DocumentosAsociados);
}
