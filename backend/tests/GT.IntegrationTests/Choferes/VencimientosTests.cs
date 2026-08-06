using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Panel de vencimientos (FR-021).
///
/// Entran sólo los documentos vigentes de cada tipo de los choferes activos. Las dos exclusiones son
/// deliberadas y cada una tiene su test: un chofer dado de baja no alerta aunque tenga todo vencido,
/// y una licencia vieja ya renovada tampoco.
/// </summary>
public class VencimientosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Entran_LosProximosAVencer_YLosVencidos_PeroNoLosQueEstanAlDia()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Panel base");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo del panel", diasAvisoVencimiento: 30);

        var alDia = await app.CrearChoferCompletoAsync(15111222, transportistaId: transportista.Id);
        var porVencer = await app.CrearChoferCompletoAsync(15211222, transportistaId: transportista.Id);
        var vencido = await app.CrearChoferCompletoAsync(15311222, transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(alDia.Id, tipo.Id, 300);
        await app.CrearDocumentoAsync(porVencer.Id, tipo.Id, 10);
        await app.CrearDocumentoAsync(vencido.Id, tipo.Id, -20);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var alertas = await cliente.GetFromJsonAsync<List<AlertaLeida>>("/api/vencimientos");

        var delTipo = alertas!.Where(alerta => alerta.Documento.Tipo.Id == tipo.Id).ToList();

        Assert.Equal(2, delTipo.Count);
        Assert.DoesNotContain(delTipo, alerta => alerta.ChoferId == alDia.Id);

        // Ordenado por urgencia: primero lo vencido hace más tiempo.
        Assert.Equal(vencido.Id, delTipo[0].ChoferId);
        Assert.Equal("vencida", delTipo[0].Documento.Estado);
        Assert.Equal(-20, delTipo[0].Documento.DiasHastaVencimiento);

        Assert.Equal(porVencer.Id, delTipo[1].ChoferId);
        Assert.Equal("proximaAvencer", delTipo[1].Documento.Estado);
        Assert.Equal(10, delTipo[1].Documento.DiasHastaVencimiento);
    }

    [Fact]
    public async Task Un_ChoferInactivo_NoAparece_AunqueTengaTodoVencido()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Panel con inactivo");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo del inactivo");

        var inactivo = await app.CrearChoferCompletoAsync(
            15411222,
            activo: false,
            transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(inactivo.Id, tipo.Id, -500);

        var cliente = await app.CrearClienteAutenticadoAsync();
        var alertas = await cliente.GetFromJsonAsync<List<AlertaLeida>>("/api/vencimientos");

        Assert.DoesNotContain(alertas!, alerta => alerta.ChoferId == inactivo.Id);
    }

    /// <summary>Un documento histórico no alerta: sólo cuenta el vigente de su tipo (FR-020a).</summary>
    [Fact]
    public async Task Un_DocumentoHistorico_NoAlerta()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Panel con historial");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo con historial");

        var chofer = await app.CrearChoferCompletoAsync(15511222, transportistaId: transportista.Id);

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -300, numero: "VIEJA");
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, 400, numero: "RENOVADA");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var alertas = await cliente.GetFromJsonAsync<List<AlertaLeida>>("/api/vencimientos");

        Assert.DoesNotContain(alertas!, alerta => alerta.ChoferId == chofer.Id);
    }

    /// <summary>Cada alerta trae con qué chofer y de qué transportista, para poder actuar.</summary>
    [Fact]
    public async Task Cada_AlertaTraeSuChoferYSuTransportista()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transporte del panel");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo con datos");

        var persona = await app.CrearPersonaAsync(
            dni: "15611222",
            nombre: "Ramona",
            apellido: "Gómez");
        var chofer = await app.CrearChoferAsync(
            persona.Id,
            transportista.Id,
            cuil: DatosDePrueba.CuilValidoPara(15611222));

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -7, numero: "LIC-999");

        var cliente = await app.CrearClienteAutenticadoAsync();
        var alertas = await cliente.GetFromJsonAsync<List<AlertaLeida>>("/api/vencimientos");

        var alerta = Assert.Single(alertas!, a => a.ChoferId == chofer.Id);

        Assert.Equal("Gómez", alerta.Apellido);
        Assert.Equal("Ramona", alerta.Nombre);
        Assert.Equal(transportista.Id, alerta.Transportista.Id);
        Assert.Equal("Transporte del panel", alerta.Transportista.Nombre);
        Assert.Equal("LIC-999", alerta.Documento.Numero);
        Assert.True(alerta.Documento.EsVigenteDelTipo);
    }

    public record AlertaLeida(
        int ChoferId,
        string Apellido,
        string Nombre,
        TipoLeido Transportista,
        DocumentoLeido Documento);
}
