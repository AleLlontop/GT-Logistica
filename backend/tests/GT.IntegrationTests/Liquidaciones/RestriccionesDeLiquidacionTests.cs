using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia.Configuraciones;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Las garantías que viven en la base, verificadas fila por fila y <b>sin pasar por los casos de uso</b>:
/// con la validación en el medio, un <c>CHECK</c> mal escrito pasaría inadvertido.
///
/// <b>Por qué existe</b>: <c>CK_Liquidaciones_Estado</c> y el filtro del índice llevan valores de enum
/// escritos a mano. Reordenar el enum no falla al compilar (research §12.1).
/// </summary>
public class RestriccionesDeLiquidacionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(EstadoLiquidacion.Pendiente, 0, false)]
    [InlineData(EstadoLiquidacion.Pendiente, 40_000, false)]
    [InlineData(EstadoLiquidacion.Pagada, 100_000, false)]
    [InlineData(EstadoLiquidacion.Anulada, 0, true)]
    public async Task El_CheckDelEstado_AceptaCadaRamaValida(EstadoLiquidacion estado, int pagado, bool conMotivo)
    {
        var error = await IntentarInsertarAsync(estado, pagado, conMotivo ? "Período equivocado." : null);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(EstadoLiquidacion.Pagada, 40_000, false)] // pagada con saldo
    [InlineData(EstadoLiquidacion.Pendiente, 100_000, false)] // pendiente sin saldo
    [InlineData(EstadoLiquidacion.Anulada, 40_000, true)] // anulada con pagos
    [InlineData(EstadoLiquidacion.Anulada, 0, false)] // anulada sin motivo
    [InlineData(EstadoLiquidacion.Pendiente, 0, true)] // pendiente con motivo
    public async Task El_CheckDelEstado_RechazaCadaFilaInvalida(EstadoLiquidacion estado, int pagado, bool conMotivo)
    {
        var error = await IntentarInsertarAsync(estado, pagado, conMotivo ? "Período equivocado." : null);

        Assert.NotNull(error);
        Assert.Contains(LiquidacionConfiguracion.CheckEstado, error, StringComparison.Ordinal);
    }

    /// <summary>
    /// FR-043 queda cerrado por dos restricciones a la vez: una fila con lo pagado por encima del total
    /// viola también la del estado, y SQL Server nombra la primera que evalúa. Por eso lo que se afirma es
    /// el rechazo, y el <c>CHECK</c> propio de lo pagado se aísla con un valor negativo, que la del estado
    /// sí dejaría pasar.
    /// </summary>
    [Fact]
    public async Task Lo_Pagado_NoPuedeSuperarElTotal_NiSerNegativo()
    {
        Assert.NotNull(await IntentarInsertarAsync(EstadoLiquidacion.Pagada, 100_001, null));
        Assert.NotNull(await IntentarInsertarAsync(EstadoLiquidacion.Pendiente, 100_001, null));

        var negativo = await IntentarInsertarAsync(EstadoLiquidacion.Pendiente, -1, null);

        Assert.NotNull(negativo);
        Assert.Contains("CK_Liquidaciones_ImportePagado", negativo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_IndiceDeVigentes_RechazaDosVinculosVigentesDelMismoViaje()
    {
        var (primera, segunda, viaje) = await DosLiquidacionesYUnViajeAsync();

        await InsertarVinculoAsync(primera, viaje, vigente: true);

        var choque = await Assert.ThrowsAsync<DbUpdateException>(() =>
            InsertarVinculoAsync(segunda, viaje, vigente: true));

        Assert.Contains(
            LiquidacionViajeConfiguracion.IndiceViajeVigente,
            choque.InnerException!.Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Un viaje pasa por varias liquidaciones anuladas antes de quedar en una vigente: muchos no vigentes y
    /// uno vigente conviven (research §1). También prueba que <c>false</c> llega a la base y no lo pisa el
    /// <c>DEFAULT 1</c> de la columna.
    /// </summary>
    [Fact]
    public async Task El_IndiceDeVigentes_AceptaUnVigenteYVariosNoVigentes()
    {
        var (primera, segunda, viaje) = await DosLiquidacionesYUnViajeAsync();
        var (tercera, _, _) = await DosLiquidacionesYUnViajeAsync();

        await InsertarVinculoAsync(primera, viaje, vigente: false);
        await InsertarVinculoAsync(segunda, viaje, vigente: false);
        await InsertarVinculoAsync(tercera, viaje, vigente: true);

        var vigencias = await app.ConAlcanceAsync(contexto => contexto.LiquidacionViajes
            .Where(vinculo => vinculo.ViajeId == viaje)
            .Select(vinculo => vinculo.Vigente)
            .ToListAsync());

        Assert.Equal(2, vigencias.Count(vigente => !vigente));
        Assert.Equal(1, vigencias.Count(vigente => vigente));
    }

    /// <summary>La secuencia asigna el número: la entidad no lo pone y dos liquidaciones no lo repiten.</summary>
    [Fact]
    public async Task La_SecuenciaAsignaNumerosDistintos()
    {
        var (primera, segunda, _) = await DosLiquidacionesYUnViajeAsync();

        var numeros = await app.ConAlcanceAsync(contexto => contexto.Liquidaciones
            .Where(liquidacion => liquidacion.Id == primera || liquidacion.Id == segunda)
            .Select(liquidacion => liquidacion.Numero)
            .ToListAsync());

        Assert.All(numeros, numero => Assert.True(numero > 0));
        Assert.Equal(2, numeros.Distinct().Count());
    }

    private async Task<string?> IntentarInsertarAsync(EstadoLiquidacion estado, int pagado, string? motivo)
    {
        var transportista = await app.TransportistaExternoAsync();

        try
        {
            await app.EnLaBaseAsync(async contexto =>
            {
                contexto.Liquidaciones.Add(new Liquidacion
                {
                    TransportistaId = transportista.Id,
                    PeriodoMes = 7,
                    PeriodoAnio = 2026,
                    ImporteTotal = 100_000m,
                    ImportePagado = pagado,
                    Estado = estado,
                    MotivoAnulacion = motivo,
                });

                await contexto.SaveChangesAsync();
            });

            return null;
        }
        catch (DbUpdateException excepcion)
        {
            return excepcion.InnerException?.Message ?? excepcion.Message;
        }
    }

    private async Task<(int Primera, int Segunda, int ViajeId)> DosLiquidacionesYUnViajeAsync()
    {
        var cliente = await app.CrearClienteAsync();
        var transportista = await app.TransportistaExternoAsync();
        var viaje = await app.ViajeDeAsync(cliente.Id, transportista.Id, estado: EstadoViaje.Rendido);

        var ids = new List<int>();

        for (var i = 0; i < 2; i++)
        {
            ids.Add(await app.ConAlcanceAsync(async contexto =>
            {
                var liquidacion = new Liquidacion
                {
                    TransportistaId = transportista.Id,
                    PeriodoMes = 7,
                    PeriodoAnio = 2026,
                    ImporteTotal = 100_000m,
                };

                contexto.Liquidaciones.Add(liquidacion);
                await contexto.SaveChangesAsync();

                return liquidacion.Id;
            }));
        }

        return (ids[0], ids[1], viaje.Id);
    }

    private Task InsertarVinculoAsync(int liquidacionId, int viajeId, bool vigente) =>
        app.EnLaBaseAsync(async contexto =>
        {
            contexto.LiquidacionViajes.Add(new LiquidacionViaje
            {
                LiquidacionId = liquidacionId,
                ViajeId = viajeId,
                Vigente = vigente,
            });

            await contexto.SaveChangesAsync();
        });
}
