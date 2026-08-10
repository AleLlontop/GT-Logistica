using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// US6 esc. 4: cambiar los días de aviso de un tipo recalcula el estado de sus documentos
/// <b>sin actualizar ninguna fila</b>.
///
/// Es la prueba de fondo de haber elegido calcular el estado al leer en vez de guardarlo (research
/// §2). Con una columna almacenada, este cambio exigiría recorrer y actualizar todos los documentos
/// del tipo; acá no se toca ninguno.
/// </summary>
public class RecalculoPorDiasAvisoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Cambiar_LosDiasDeAviso_CambiaElEstado_SinTocarNingunaFila()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 14111222);
        var tipo = await app.CrearTipoDocumentacionAsync(
            nombre: "Tipo que cambia de aviso",
            diasAvisoVencimiento: 30);

        // Vence en 20 días: dentro de la ventana de 30, así que arranca próxima a vencer.
        var documento = await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: 20);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var antes = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.Equal("proximaAvencer", Assert.Single(antes!.Documentos).Estado);
        Assert.Equal("proximaAvencer", antes.EstadoDocumentacion);

        // Una foto de la fila antes de tocar el catálogo.
        var filaAntes = await app.RecargarDocumentoAsync(documento.Id);

        var cambio = await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo.Id}",
            new { nombre = tipo.Nombre, diasAvisoVencimiento = 10, ambito = "chofer" });
        cambio.EnsureSuccessStatusCode();

        // El mismo documento, sin haberlo tocado, ahora está al día.
        var despues = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.Equal("vigente", Assert.Single(despues!.Documentos).Estado);
        Assert.Equal("enRegla", despues.EstadoDocumentacion);

        // Y la fila es idéntica: no hay ninguna columna de estado que se haya actualizado.
        var filaDespues = await app.RecargarDocumentoAsync(documento.Id);

        Assert.Equal(filaAntes!.Numero, filaDespues!.Numero);
        Assert.Equal(filaAntes.FechaEmision, filaDespues.FechaEmision);
        Assert.Equal(filaAntes.FechaVencimiento, filaDespues.FechaVencimiento);
        Assert.Equal(filaAntes.ArchivoRuta, filaDespues.ArchivoRuta);
    }

    /// <summary>
    /// Y el cambio también se ve en el filtro del listado, que resuelve el estado en la base: no
    /// hay dos implementaciones de la regla que puedan quedar desalineadas.
    /// </summary>
    [Fact]
    public async Task El_ListadoTambienRecalcula()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Recálculo en el listado");
        var chofer = await app.CrearChoferCompletoAsync(14211222, transportistaId: transportista.Id);
        var tipo = await app.CrearTipoDocumentacionAsync(
            nombre: "Tipo del listado",
            diasAvisoVencimiento: 30);

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: 20);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var comoPorVencer = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&estadoDocumentacion=proximaAvencer");
        Assert.Equal(chofer.Id, Assert.Single(comoPorVencer!.Items).Id);

        await cliente.PutAsJsonAsync(
            $"/api/tipos-documentacion/{tipo.Id}",
            new { nombre = tipo.Nombre, diasAvisoVencimiento = 10, ambito = "chofer" });

        var yaNoEsta = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&estadoDocumentacion=proximaAvencer");
        Assert.Empty(yaNoEsta!.Items);

        var ahoraEnRegla = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&estadoDocumentacion=enRegla");
        Assert.Equal(chofer.Id, Assert.Single(ahoraEnRegla!.Items).Id);
    }

    /// <summary>
    /// La regla vive en un solo lugar conceptual, pero se ejecuta en dos —el dominio en C# y la
    /// consulta en SQL—. Este test compara las dos sobre el mismo dato, que es lo que evita que se
    /// separen sin que nadie se entere.
    /// </summary>
    [Fact]
    public async Task El_EstadoDelListado_CoincideConElDeLaFicha()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dominio contra SQL");
        var tipo = await app.CrearTipoDocumentacionAsync(
            nombre: "Tipo de contraste",
            diasAvisoVencimiento: 15);

        var cliente = await app.CrearClienteAutenticadoAsync();

        foreach (var (semilla, dias) in new[] { (14311222, 400), (14411222, 5), (14511222, 0), (14611222, -3) })
        {
            var chofer = await app.CrearChoferCompletoAsync(semilla, transportistaId: transportista.Id);
            await app.CrearDocumentoAsync(chofer.Id, tipo.Id, dias);

            var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

            var listado = await cliente.GetFromJsonAsync<PaginaLeida>(
                $"/api/choferes?transportistaId={transportista.Id}&dni={semilla:D8}");

            var fila = Assert.Single(listado!.Items);
            Assert.Equal(ficha!.EstadoDocumentacion, fila.EstadoDocumentacion);
        }
    }
}
