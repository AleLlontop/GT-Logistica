using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// SC-010: cargar una renovación saca la alerta <b>sin tocar el documento anterior</b>.
///
/// Es el criterio de éxito que justifica toda la decisión de FR-020a. Con cualquier otro diseño,
/// quien opera tendría que acordarse de dar de baja o editar la licencia vieja para que el chofer
/// dejara de figurar en falta; acá alcanza con cargar la nueva.
/// </summary>
public class RenovacionSacaAlertaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Cargar_LaRenovacion_SacaLaAlerta_YDejaIntactoElAnterior()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Renovaciones");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia a renovar");
        var chofer = await app.CrearChoferCompletoAsync(16111222, transportistaId: transportista.Id);

        var vieja = await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -15, numero: "LIC-VIEJA");

        var cliente = await app.CrearClienteAutenticadoAsync();

        // Antes de renovar, el chofer alerta y figura vencido.
        var antes = await cliente.GetFromJsonAsync<List<VencimientosTests.AlertaLeida>>("/api/vencimientos");
        Assert.Contains(antes!, alerta => alerta.ChoferId == chofer.Id);

        var fichaAntes = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.Equal("vencida", fichaAntes!.EstadoDocumentacion);

        // Se carga la renovación por la API, como lo haría quien opera.
        var renovacion = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 400, numero: "LIC-NUEVA"));
        renovacion.EnsureSuccessStatusCode();

        // La alerta desaparece sola.
        var despues = await cliente.GetFromJsonAsync<List<VencimientosTests.AlertaLeida>>("/api/vencimientos");
        Assert.DoesNotContain(despues!, alerta => alerta.ChoferId == chofer.Id);

        var fichaDespues = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.Equal("enRegla", fichaDespues!.EstadoDocumentacion);

        // Y nadie tocó la licencia vieja: sigue en la ficha, con su estado real y marcada como
        // reemplazada.
        var enLaFicha = Assert.Single(fichaDespues.Documentos, documento => documento.Id == vieja.Id);

        Assert.Equal("LIC-VIEJA", enLaFicha.Numero);
        Assert.Equal("vencida", enLaFicha.Estado);
        Assert.False(enLaFicha.EsVigenteDelTipo);

        var filaSinTocar = await app.RecargarDocumentoAsync(vieja.Id);
        Assert.Equal(vieja.FechaVencimiento, filaSinTocar!.FechaVencimiento);
    }
}
