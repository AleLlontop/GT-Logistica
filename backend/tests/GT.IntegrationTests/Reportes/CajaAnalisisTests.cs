using GT.Application.Pagos;
using GT.Application.Reportes.Analisis;
using GT.Domain.Caja;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class CajaAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task CuatroBordesArgentina_SerieYMovimientoNetoNegativo_ComparteProyeccionLegacy()
    {
        var usuario = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: new(2026,10,1,2,59,0,DateTimeKind.Utc));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 1m, fecha: new(2026,10,1,3,30,0,DateTimeKind.Utc));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, tipo: TipoMovimientoCaja.Egreso, importe: 3m, fecha: new(2026,11,1,2,30,0,DateTimeKind.Utc));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: new(2026,11,1,3,0,0,DateTimeKind.Utc));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var repo = new RepositorioAnalisisReportes(db, scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026,10,1), new(2026,10,31));
        var contexto = ContextoAnalisis.DesdeInstante(new(2026,11,2,12,0,0,TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaMovimientoArgentina);
        var datos = await repo.CajaAsync(new(periodo, caja.Id), contexto);
        Assert.Equal(2, datos.Resumen.Cantidad);
        Assert.Equal(1m, datos.Resumen.Ingresos);
        Assert.Equal(3m, datos.Resumen.Egresos);
        Assert.Equal(-2m, datos.Resumen.Neto);
        Assert.Equal(-2m, Assert.Single(datos.Series).Neto);
        Assert.Equal("2026-10", datos.Series[0].Periodo);
        var intervalo = periodo.ParaInstantesArgentina();
        var legacy = await new RepositorioCaja(db).ConsultarMovimientosAsync(intervalo.DesdeInclusivo, intervalo.HastaExclusivo, caja.Id, 1);
        Assert.Equal(legacy.Items.Select(m => m.Id), datos.Detalle.Items.Select(m => m.Id));
        Assert.All(datos.Detalle.Items, m => Assert.Equal(usuario.Username, m.Responsable.Nombre));
    }

    [Fact]
    public async Task EgresoYCompensacionEnMesesDistintos_ReferenciaOppNoOp()
    {
        var pago = await EscenarioAnulacion.CrearAsync(app, "efectivo");
        using(var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(pago.OrdenId, new(Guid.NewGuid(), "Compensación noviembre", true), pago.UsuarioId, false,
                new(2026,11,1), new(2026,11,1,12,0,0,DateTimeKind.Utc));
        using var lectura = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(lectura.ServiceProvider.GetRequiredService<GtDbContext>(), lectura.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        foreach(var mes in new[]{10,11})
        {
            var periodo = new PeriodoAnalisis(new(2026,mes,1), new(2026,mes,DateTime.DaysInMonth(2026,mes)));
            var contexto = ContextoAnalisis.DesdeInstante(new(2026,11,2,12,0,0,TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaMovimientoArgentina);
            var datos = await repo.CajaAsync(new(periodo,pago.CajaId), contexto);
            var fila = Assert.Single(datos.Detalle.Items);
            Assert.Equal(pago.OrdenId, fila.OrdenPagoProveedorId);
            Assert.Null(fila.OrdenDePagoId);
            Assert.StartsWith("OPP-", fila.Referencia);
            Assert.Equal(mes == 10 ? -121m : 121m, datos.Resumen.Neto);
        }
    }
}
