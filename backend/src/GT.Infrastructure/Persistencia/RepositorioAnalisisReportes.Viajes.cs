using GT.Application.Choferes;
using GT.Application.Reportes.Analisis;
using GT.Application.Viajes;
using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioAnalisisReportes
{
    public Task<PaginaDe<FilaViajeAnalisis>> TablaViajesAsync(FiltroViajesAnalisis filtro, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_,token)=>LeerTablaViajesAsync(filtro,token),ct);
    }

    private IQueryable<Viaje> ViajesFiltrados(FiltroViajesAnalisis filtro) => FiltrosViajesSql.Aplicar(db.Viajes.AsNoTracking(),
        new FiltrosDeViajes(filtro.ClienteId,filtro.TransportistaId,NombresDeEstadoViaje.Leer(filtro.Estado),
            filtro.Periodo.Desde,filtro.Periodo.Hasta,filtro.Busqueda,filtro.Pagina));

    private async Task<PaginaDe<FilaViajeAnalisis>> LeerTablaViajesAsync(FiltroViajesAnalisis filtro,CancellationToken ct)
    {
        var consulta=ViajesFiltrados(filtro);
        var total=await consulta.CountAsync(ct);
        var filas=await consulta.OrderByDescending(v=>v.Fecha).ThenByDescending(v=>v.Numero)
            .Skip((filtro.Pagina-1)*filtro.TamanioPagina).Take(filtro.TamanioPagina)
            .Select(v=>new FilaViajeAnalisis(v.Id,v.Numero,v.Fecha,
                new ReferenciaAnalisis(v.ClienteId,v.Cliente!.RazonSocial,v.Cliente.Activo),v.Origen,v.Destino,
                v.Chofer==null?null:new ReferenciaAnalisis(v.Chofer.Id,v.Chofer.Persona!.Apellido+", "+v.Chofer.Persona.Nombre,v.Chofer.Activo),
                v.Vehiculo==null?null:new ReferenciaAnalisis(v.Vehiculo.Id,v.Vehiculo.Patente,v.Vehiculo.Activo),
                v.Transportista==null?null:new ReferenciaAnalisis(v.Transportista.Id,v.Transportista.Nombre,v.Transportista.Activo),
                NombresDeEstadoViaje.EnJson(v.Estado),v.Importe)).ToArrayAsync(ct);
        return new(filas,total,filtro.Pagina,filtro.TamanioPagina);
    }
    public Task<AnalisisViajes> ViajesAsync(FiltroViajesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default)
    {
        ValidacionAnalisis.Filtro(filtro);
        return foto.LeerAsync((_, token) => LeerViajesAsync(filtro, contexto, token), ct);
    }

    private async Task<AnalisisViajes> LeerViajesAsync(FiltroViajesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var agregados = await LeerAgregadosViajesAsync(filtro, contexto, ct);
        var resumen = agregados.Resumen;
        var series = agregados.Series;
        var distribuciones = agregados.Distribuciones;
        return new(contexto, resumen, series, distribuciones, await LeerTablaViajesAsync(filtro, ct));
    }

    private async Task<(ResumenViajes Resumen, IReadOnlyList<MesViajes> Series, DistribucionesViajes Distribuciones)> LeerAgregadosViajesAsync(FiltroViajesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct)
    {
        var consulta = ViajesFiltrados(filtro);
        var estados = await consulta.GroupBy(v => v.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.LongCount(), Importe = g.Sum(v => v.Estado == EstadoViaje.Anulado ? 0m : v.Importe) }).ToArrayAsync(ct);
        var resumen = new ResumenViajes(estados.Sum(g => g.Cantidad),
            estados.Where(g => g.Estado == EstadoViaje.Rendido || g.Estado == EstadoViaje.Facturado).Sum(g => g.Cantidad),
            estados.Where(g => g.Estado == EstadoViaje.EnCurso).Sum(g => g.Cantidad),
            estados.Where(g => g.Estado == EstadoViaje.Pendiente).Sum(g => g.Cantidad),
            estados.Where(g => g.Estado == EstadoViaje.Anulado).Sum(g => g.Cantidad), estados.Sum(g => g.Importe));
        var meses = await consulta.GroupBy(v => new { v.Fecha.Year, v.Fecha.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Cantidad = g.LongCount(),
                Realizados = g.Sum(v => v.Estado == EstadoViaje.Rendido || v.Estado == EstadoViaje.Facturado ? 1L : 0L),
                Pendientes = g.Sum(v => v.Estado == EstadoViaje.EnCurso ? 1L : 0L) }).ToArrayAsync(ct);
        var series = new List<MesViajes>();
        var mes = filtro.Periodo.MesLiquidadoDesde;
        while (true)
        {
            var dato = meses.SingleOrDefault(g => g.Year == mes.Year && g.Month == mes.Month);
            series.Add(new(mes.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture), dato?.Cantidad ?? 0, dato?.Realizados ?? 0, dato?.Pendientes ?? 0));
            if (mes == filtro.Periodo.MesLiquidadoHasta) break;
            mes = mes.AddMonths(1);
        }
        var clientes = consulta.GroupBy(v => new { v.ClienteId, v.Cliente!.RazonSocial })
            .Select(g => new GrupoImporteSql { Id = g.Key.ClienteId, Nombre = g.Key.RazonSocial, Cantidad = g.LongCount(), Importe = g.Sum(v => v.Estado == EstadoViaje.Anulado ? 0m : v.Importe) });
        var transportistas = consulta.GroupBy(v => new { v.TransportistaId, Nombre = v.Transportista == null ? "Sin asignar" : v.Transportista.Nombre })
            .Select(g => new GrupoImporteSql { Id = g.Key.TransportistaId, Nombre = g.Key.Nombre, Cantidad = g.LongCount(), Importe = g.Sum(v => v.Estado == EstadoViaje.Anulado ? 0m : v.Importe) });
        var distribuciones = new DistribucionesViajes(
            new(estados.OrderByDescending(g => g.Cantidad).ThenBy(g => g.Estado).Select(g => new GrupoCantidad(NombresDeEstadoViaje.EnJson(g.Estado), NombresDeEstadoViaje.EnPantalla(g.Estado), g.Cantidad)).ToArray(), null, estados.Length),
            await DistribuirImportesAsync(clientes, ct), await DistribuirImportesAsync(transportistas, ct));
        return (resumen, series, distribuciones);
    }

    private static DistribucionImporte Distribuir(IEnumerable<GrupoImporte> grupos)
    {
        var ordenados = grupos.OrderByDescending(g => g.Importe).ThenBy(g => g.Id, StringComparer.Ordinal).ToArray();
        var resto = ordenados.Skip(10).ToArray();
        return new(ordenados.Take(10).ToArray(), resto.Length == 0 ? null : new("otros", "Otros", resto.Sum(g => g.Cantidad), resto.Sum(g => g.Importe)), ordenados.Length);
    }
}
