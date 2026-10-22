using System.Globalization;
using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Domain.Caja;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<AnalisisCaja> CajaAsync(FiltroCajaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerCajaAsync(filtro, contexto, token), ct);
    }

    private IQueryable<MovimientoDeCaja> MovimientosFiltrados(FiltroCajaAnalisis filtro)
    {
        var intervalo = filtro.Periodo.ParaInstantesArgentina();
        return RepositorioCaja.FiltrarMovimientos(db.MovimientosDeCaja.AsNoTracking(), intervalo.DesdeInclusivo, intervalo.HastaExclusivo, filtro.CajaId);
    }

    private async Task<AnalisisCaja> LeerCajaAsync(FiltroCajaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosCajaAsync(filtro, contexto, ct);
        var consulta = MovimientosFiltrados(filtro);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        var filas = await consulta.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).Skip((filtro.Pagina - 1) * filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(RepositorioCaja.ProyeccionDeMovimiento).ToArrayAsync(ct);
        var ids = filas.Select(m => m.Id).ToArray();
        // Una consulta por página, independiente de su tamaño; sin cambiar la proyección/contrato legacy.
        var referencias = await db.MovimientosDeCaja.AsNoTracking().Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.OrdenDePagoId, Responsable = m.Caja!.UsuarioResponsable!.Username, m.Caja.FechaApertura }).ToDictionaryAsync(m => m.Id, ct);
        var detalle = new PaginaDe<FilaMovimientoAnalisis>(filas.Select(f =>
        {
            var fila = RepositorioCaja.ALista(f); var extra = referencias[f.Id];
            return new FilaMovimientoAnalisis(f.Id, f.CajaId, NombreCajaAnalisis.Desde(extra.Responsable, extra.FechaApertura), f.Fecha, fila.Tipo, f.Importe,
                f.Concepto, new(f.UsuarioId, f.Usuario), fila.Referencia, extra.OrdenDePagoId, f.OrdenPagoProveedorId);
        }).ToArray(), checked((int)resumen.Cantidad), filtro.Pagina, filtro.TamanioPagina);
        return new(contexto, resumen, series, distribuciones, detalle);
        }

    private async Task<(ResumenCajaAnalisis Resumen, IReadOnlyList<MesCajaAnalisis> Series, DistribucionesCaja Distribuciones)> LeerAgregadosCajaAsync(FiltroCajaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = MovimientosFiltrados(filtro);
        var tipos = await consulta.GroupBy(m => m.Tipo).Select(g => new { Tipo = g.Key, Cantidad = g.LongCount(), Importe = g.Sum(m => m.Importe) }).ToArrayAsync(ct);
        var ingresos = tipos.Where(g => g.Tipo == TipoMovimientoCaja.Ingreso).Sum(g => g.Importe);
        var egresos = tipos.Where(g => g.Tipo == TipoMovimientoCaja.Egreso).Sum(g => g.Importe);
        var resumen = new ResumenCajaAnalisis(tipos.Sum(g => g.Cantidad), ingresos, egresos, ingresos - egresos);
        var meses = await consulta.GroupBy(m => new { m.Fecha.AddHours(-3).Year, m.Fecha.AddHours(-3).Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Ingresos = g.Sum(m => m.Tipo == TipoMovimientoCaja.Ingreso ? m.Importe : 0m), Egresos = g.Sum(m => m.Tipo == TipoMovimientoCaja.Egreso ? m.Importe : 0m) }).ToArrayAsync(ct);
        var series = new List<MesCajaAnalisis>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            var entrada = dato?.Ingresos ?? 0m; var salida = dato?.Egresos ?? 0m;
            series.Add(new(mes.ToString("yyyy-MM", CultureInfo.InvariantCulture), entrada, salida, entrada - salida));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var distribuciones = new DistribucionesCaja(Distribuir(tipos.Select(g => new GrupoImporte(NombresDeEstadoCaja.EnJson(g.Tipo), NombresDeEstadoCaja.EnPantalla(g.Tipo), g.Cantidad, g.Importe))));
        return (resumen, series, distribuciones);
    }
}
