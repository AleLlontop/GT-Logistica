using System.Net.Http.Json;
using GT.Domain.Flota;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// SC-006, el criterio de éxito central del módulo: el filtro <c>disponible</c> devuelve <b>0%</b> de
/// unidades con documentación <c>vencida</c> o <c>sinDocumentacion</c>, y el <b>100%</b> de las
/// excluidas por esa causa aparece en el panel de vencimientos.
///
/// Lo garantiza el predicado de la consulta, no un filtrado posterior que alguien podría olvidar
/// (research §5). Los dos valores operativos son complementarios dentro de los activos: todo vehículo
/// activo cae en exactamente uno.
/// </summary>
public class FiltroDisponibleTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_FiltroDisponible_NoDevuelveNingunaUnidadConPapelesEnFalta()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de SC-006");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de SC-006");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de SC-006",
            diasAvisoVencimiento: 30);

        // Guardado como disponible y con todo al día: es el único que puede salir a la ruta.
        var enCondiciones = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(enCondiciones.Id, tipoDocumento.Id, 300);

        // Guardado como disponible, pero con el seguro vencido: no puede figurar disponible (FR-015).
        var conSeguroVencido = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(conSeguroVencido.Id, tipoDocumento.Id, -1);

        // Guardado como disponible, pero sin ningún papel: tampoco (FR-013).
        var sinPapeles = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        // Y uno parado por reparación, con la documentación al día: la columna guardada lo saca.
        var enElTaller = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.FueraDeServicio);
        await app.CrearDocumentoVehiculoAsync(enElTaller.Id, tipoDocumento.Id, 300);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var disponibles = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=disponible");

        // El 0% que exige SC-006, verificado por lo que hay y por lo que no hay.
        var unico = Assert.Single(disponibles!.Items);
        Assert.Equal(enCondiciones.Id, unico.Id);

        Assert.DoesNotContain(
            disponibles.Items,
            v => v.EstadoDocumentacion is "vencida" or "sinDocumentacion");

        // Los tres excluidos caen del otro lado: los dos valores son complementarios (research §5).
        var fueraDeServicio = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=fueraDeServicio");

        Assert.Equal(3, fueraDeServicio!.Total);
        Assert.Contains(fueraDeServicio.Items, v => v.Id == conSeguroVencido.Id);
        Assert.Contains(fueraDeServicio.Items, v => v.Id == sinPapeles.Id);
        Assert.Contains(fueraDeServicio.Items, v => v.Id == enElTaller.Id);
    }

    /// <summary>
    /// La otra mitad de SC-006: el 100% de las unidades excluidas <b>por documentación</b> figura en
    /// el panel de vencimientos, para que quien opera sepa qué renovar.
    ///
    /// La que está en el taller con los papeles al día no figura, y es correcto: no hay nada que
    /// renovar (FR-035).
    /// </summary>
    [Fact]
    public async Task Las_ExcluidasPorDocumentacion_FiguranEnElPanel()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del panel de SC-006");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del panel de SC-006");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del panel de SC-006");

        var conSeguroVencido = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(conSeguroVencido.Id, tipoDocumento.Id, -3);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");

        Assert.Contains(panel!, alerta => alerta.VehiculoId == conSeguroVencido.Id);
    }

    /// <summary>
    /// El borde de FR-014: <c>proximaAvencer</c> <b>no</b> saca la unidad de circulación. El papel
    /// todavía vale; el panel avisa, pero la unidad sigue disponible.
    /// </summary>
    [Fact]
    public async Task Una_UnidadProximaAvencer_SigueSiendoDisponible()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del aviso");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del aviso");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del aviso",
            diasAvisoVencimiento: 30);

        var porVencer = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(porVencer.Id, tipoDocumento.Id, 10);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var disponibles = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=disponible");

        var fila = Assert.Single(disponibles!.Items);
        Assert.Equal(porVencer.Id, fila.Id);
        Assert.Equal("proximaAvencer", fila.EstadoDocumentacion);

        // Y aun así el panel la muestra: avisar no es inhabilitar.
        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");
        Assert.Contains(panel!, alerta => alerta.VehiculoId == porVencer.Id);
    }
}
