using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Los tres índices únicos filtrados, ejercitados contra la base real (research §2, §15).
///
/// <b>Es el test que protege contra un reordenamiento futuro de <c>EstadoViaje</c>.</b> Los filtros
/// llevan los valores <c>1</c> y <c>3</c> escritos a mano en SQL, y cambiar el orden del enum no
/// falla al compilar: dejaría el remito siendo único entre los rendidos y dos viajes podrían
/// compartir chofer sin que nada avisara.
///
/// Va en la fase foundational y no dentro de una historia porque es la garantía de la que dependen
/// SC-003 y SC-005, y descubrir tarde que un índice está mal escrito obliga a rehacer la migración.
///
/// Se inserta directamente contra la base, sin pasar por los casos de uso: lo que se verifica es el
/// índice, no la consulta previa que da el mensaje bueno.
/// </summary>
public class IndicesFiltradosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    // ── FR-014: el remito es único entre los NO anulados ────────────────────────────────────────

    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.EnCurso)]
    [InlineData(EstadoViaje.Rendido)]
    public async Task El_Remito_NoSeRepiteEntreNoAnulados(EstadoViaje estado)
    {
        var cliente = await app.CrearClienteAsync();
        var remito = $"R-{Guid.NewGuid():N}"[..20];

        await app.CrearViajeAsync(cliente.Id, estado: estado, numeroRemito: remito);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Pendiente, numeroRemito: remito));
    }

    [Fact]
    public async Task El_RemitoDeUnViajeAnulado_VuelveAEstarLibre()
    {
        // Es el caso de US2 esc. 9: se anuló el viaje y el remito de papel se reusa en otro.
        var cliente = await app.CrearClienteAsync();
        var remito = $"R-{Guid.NewGuid():N}"[..20];

        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Anulado, numeroRemito: remito);

        var segundo = await app.CrearViajeAsync(
            cliente.Id,
            estado: EstadoViaje.Pendiente,
            numeroRemito: remito);

        Assert.Equal(remito, segundo.NumeroRemito);
    }

    [Fact]
    public async Task Dos_ViajesAnulados_PuedenCompartirRemito()
    {
        var cliente = await app.CrearClienteAsync();
        var remito = $"R-{Guid.NewGuid():N}"[..20];

        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Anulado, numeroRemito: remito);
        var segundo = await app.CrearViajeAsync(
            cliente.Id,
            estado: EstadoViaje.Anulado,
            numeroRemito: remito);

        Assert.Equal(remito, segundo.NumeroRemito);
    }

    [Fact]
    public async Task Varios_ViajesSinRemito_Conviven()
    {
        // El índice filtra por `NumeroRemito IS NOT NULL`: un viaje sin remito no ocupa nada, y el
        // remito es opcional (FR-014).
        var cliente = await app.CrearClienteAsync();

        await app.CrearViajeAsync(cliente.Id);
        await app.CrearViajeAsync(cliente.Id);
        var tercero = await app.CrearViajeAsync(cliente.Id);

        Assert.Null(tercero.NumeroRemito);
    }

    // ── FR-026: un chofer y un vehículo, en un solo viaje `en curso` a la vez ───────────────────

    [Fact]
    public async Task Un_Chofer_NoPuedeEstarEnDosViajesEnCurso()
    {
        var (clienteId, choferId, vehiculoId, otroVehiculoId) = await ArmarEscenarioAsync();

        await app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: choferId,
            vehiculoId: vehiculoId);

        await Assert.ThrowsAsync<DbUpdateException>(() => app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: choferId,
            vehiculoId: otroVehiculoId));
    }

    [Fact]
    public async Task Un_Vehiculo_NoPuedeEstarEnDosViajesEnCurso()
    {
        var (clienteId, choferId, vehiculoId, _) = await ArmarEscenarioAsync();
        var otroChofer = await app.CrearChoferCompletoAsync(semilla: SemillaUnica());

        await app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: choferId,
            vehiculoId: vehiculoId);

        await Assert.ThrowsAsync<DbUpdateException>(() => app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: otroChofer.Id,
            vehiculoId: vehiculoId));
    }

    /// <summary>
    /// Los tres estados que <b>no</b> ocupan. Es lo que hace verdadero a FR-027: dos viajes
    /// pendientes con el mismo chofer y la misma fecha se aceptan, porque un pendiente todavía no
    /// compromete a nadie (US3 esc. 12).
    /// </summary>
    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.Rendido)]
    [InlineData(EstadoViaje.Anulado)]
    public async Task La_Unidad_SeRepiteLibrementeFueraDeEnCurso(EstadoViaje estado)
    {
        var (clienteId, choferId, vehiculoId, _) = await ArmarEscenarioAsync();

        await app.CrearViajeAsync(
            clienteId,
            estado: estado,
            choferId: choferId,
            vehiculoId: vehiculoId);

        var segundo = await app.CrearViajeAsync(
            clienteId,
            estado: estado,
            choferId: choferId,
            vehiculoId: vehiculoId);

        Assert.Equal(choferId, segundo.ChoferId);
        Assert.Equal(vehiculoId, segundo.VehiculoId);
    }

    [Fact]
    public async Task Al_RendirElPrimero_LaUnidadQuedaLibreParaOtroViajeEnCurso()
    {
        // FR-037: liberar es dejar de ocupar. El índice deja de alcanzar al viaje rendido por su solo
        // cambio de estado, sin que nadie borre la asignación.
        var (clienteId, choferId, vehiculoId, _) = await ArmarEscenarioAsync();

        var primero = await app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: choferId,
            vehiculoId: vehiculoId);

        await app.EnLaBaseAsync(async contexto =>
        {
            var viaje = await contexto.Viajes.FirstAsync(v => v.Id == primero.Id);
            viaje.Estado = EstadoViaje.Rendido;
            await contexto.SaveChangesAsync();
        });

        var segundo = await app.CrearViajeAsync(
            clienteId,
            estado: EstadoViaje.EnCurso,
            choferId: choferId,
            vehiculoId: vehiculoId);

        Assert.Equal(EstadoViaje.EnCurso, segundo.Estado);

        // La asignación del rendido se conserva: liberar nunca borra el dato (FR-037).
        var rendido = await app.RecargarViajeAsync(primero.Id);
        Assert.Equal(choferId, rendido!.ChoferId);
        Assert.Equal(vehiculoId, rendido.VehiculoId);
    }

    [Fact]
    public async Task Varios_ViajesEnCursoSinAsignar_Conviven()
    {
        // Los dos índices filtran por `IS NOT NULL`. Un viaje en curso sin asignar no es un estado
        // que el módulo permita alcanzar por la API (FR-025), pero el índice no puede depender de eso.
        var cliente = await app.CrearClienteAsync();

        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.EnCurso);
        var segundo = await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.EnCurso);

        Assert.Null(segundo.ChoferId);
    }

    // ── FR-011: el número no se repite nunca ───────────────────────────────────────────────────

    [Fact]
    public async Task El_Numero_EsUnicoYLoGeneraLaSecuencia()
    {
        var cliente = await app.CrearClienteAsync();

        var primero = await app.CrearViajeAsync(cliente.Id);
        var segundo = await app.CrearViajeAsync(cliente.Id);

        // Si alguno saliera con 0, la propiedad `Numero` quedó declarada mal y EF la está mandando en
        // el INSERT en vez de dejar que se aplique el DEFAULT de la columna (tasks §trampa 2).
        Assert.True(primero.Numero > 0);
        Assert.Equal(primero.Numero + 1, segundo.Numero);
    }

    private static int _semilla = 60_000_000;

    private static int SemillaUnica() => Interlocked.Increment(ref _semilla);

    private async Task<(int ClienteId, int ChoferId, int VehiculoId, int OtroVehiculoId)>
        ArmarEscenarioAsync()
    {
        var cliente = await app.CrearClienteAsync();
        var transportista = await app.CrearTransportistaAsync();
        var chofer = await app.CrearChoferCompletoAsync(
            semilla: SemillaUnica(),
            transportistaId: transportista.Id);

        var tipo = await app.CrearTipoVehiculoAsync();

        var vehiculo = await app.CrearVehiculoAsync(
            tipo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        var otroVehiculo = await app.CrearVehiculoAsync(
            tipo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);

        return (cliente.Id, chofer.Id, vehiculo.Id, otroVehiculo.Id);
    }
}
