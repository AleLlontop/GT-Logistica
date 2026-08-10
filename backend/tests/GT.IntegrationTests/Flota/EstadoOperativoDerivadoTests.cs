using System.Net.Http.Json;
using GT.Domain.Flota;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-014 y US4 esc. 11 (quickstart paso 12): una unidad guardada como <c>disponible</c> con el
/// seguro vencido se lista como <c>fueraDeServicio</c> <b>sin que nadie la edite</b>, y vuelve a
/// <c>disponible</c> al cargar la renovación.
///
/// Es la prueba de fondo de haber elegido derivar el estado al leer en vez de guardarlo: con una
/// columna sobrescrita, esto exigiría un proceso nocturno que la mantuviera al día y otro que la
/// revirtiera al renovar (research §4).
/// </summary>
public class EstadoOperativoDerivadoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Guardada_ComoDisponible_ConElSeguroVencido_SeListaFueraDeServicio()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del derivado");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del derivado");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del derivado");

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        var vencido = await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -1);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal("fueraDeServicio", fila.Estado);
        Assert.Equal("vencida", fila.EstadoDocumentacion);

        // La columna guardada **no se tocó**: nadie editó nada.
        var enLaBase = await app.RecargarVehiculoAsync(vehiculo.Id);
        Assert.Equal(VehiculoEstado.Disponible, enLaBase!.EstadoOperativo);

        // Se carga la renovación y la unidad vuelve sola, sin que nadie la edite (SC-010).
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 365);

        var despues = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        Assert.Equal("disponible", Assert.Single(despues!.Items).Estado);
        Assert.NotEqual(0, vencido.Id);
    }

    /// <summary>
    /// La ficha devuelve el estado <b>dos veces</b>: el derivado para mostrar y el guardado para
    /// poblar el formulario de edición. Con uno solo, editar una unidad parada por papeles vencidos
    /// le pisaría en silencio el motivo real a quien opera (plan §Reevaluación post-diseño).
    /// </summary>
    [Fact]
    public async Task La_Ficha_DevuelveElDerivadoYElGuardado()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de los dos estados");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de los dos estados");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de los dos estados");

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -20);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculo.Id}");

        Assert.Equal("fueraDeServicio", ficha!.Estado);
        Assert.Equal("disponible", ficha.EstadoOperativoGuardado);
    }

    /// <summary>
    /// El caso que justifica conservar la columna guardada: un camión en el taller sigue fuera de
    /// servicio aunque tenga toda la documentación al día. Sin ella, renovar el seguro marcaría
    /// disponible una unidad rota (research §4).
    /// </summary>
    [Fact]
    public async Task Una_UnidadEnElTaller_SigueFueraDeServicio_ConLaDocumentacionAlDia()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del taller");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del taller");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Seguro del taller");

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.FueraDeServicio);

        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, 400);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculo.Id}");

        Assert.Equal("enRegla", ficha!.EstadoDocumentacion);
        Assert.Equal("fueraDeServicio", ficha.Estado);
        Assert.Equal("fueraDeServicio", ficha.EstadoOperativoGuardado);
    }
}
