using GT.Domain.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>El listado de cajas de todos los responsables (FR-026), con su orden total (convención [003]).</summary>
public class ConsultaCajasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_Listado_TraeCajasDeTodos_ConCierreYSaldoFinalSoloEnLasCerradas()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var responsableA = await app.UsuarioAsync();
        var responsableB = await app.UsuarioAsync();

        var abierta = await app.CrearCajaAsync(responsableA.Id);
        var cerrada = await app.CrearCajaAsync(responsableB.Id, EstadoCaja.Cerrada, 5_000m, saldoFinal: 7_500m);

        var filas = await cliente.TodasLasPaginasAsync<CajaListadoLeida>("/api/caja");

        var filaAbierta = Assert.Single(filas, fila => fila.Id == abierta.Id);
        Assert.Equal("abierta", filaAbierta.Estado);
        Assert.Equal(responsableA.Username, filaAbierta.Responsable.Nombre);
        Assert.Null(filaAbierta.FechaCierre);
        Assert.Null(filaAbierta.SaldoFinal);

        var filaCerrada = Assert.Single(filas, fila => fila.Id == cerrada.Id);
        Assert.Equal("cerrada", filaCerrada.Estado);
        Assert.Equal(responsableB.Username, filaCerrada.Responsable.Nombre);
        Assert.NotNull(filaCerrada.FechaCierre);
        Assert.Equal(7_500m, filaCerrada.SaldoFinal);
    }

    [Fact]
    public async Task Con_25Cajas_ElOrdenEsTotal_SinRepetidosNiFaltantes()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        // Todas con la misma apertura: sólo el `Id` desempata.
        var apertura = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var creadas = new List<int>();

        for (var i = 0; i < 25; i++)
        {
            var usuario = await app.UsuarioAsync();
            creadas.Add((await app.CrearCajaAsync(usuario.Id, EstadoCaja.Cerrada, fechaApertura: apertura)).Id);
        }

        var filas = await cliente.TodasLasPaginasAsync<CajaListadoLeida>("/api/caja");
        var ids = filas.Select(fila => fila.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(creadas, id => Assert.Contains(id, ids));

        var propias = filas.Where(fila => creadas.Contains(fila.Id)).Select(fila => fila.Id).ToList();
        Assert.Equal(creadas.OrderByDescending(id => id), propias);

        // Entre todas, más reciente primero.
        Assert.Equal(
            filas.OrderByDescending(fila => fila.FechaApertura).ThenByDescending(fila => fila.Id).Select(fila => fila.Id),
            ids);
    }
}
