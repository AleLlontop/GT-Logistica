using GT.Application.Viajes;
using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Predicado de listado005 compartido por análisis015; SQL antes de contar/paginar.</summary>
internal static class FiltrosViajesSql
{
    private const string ColacionSinAcentos = "Latin1_General_CI_AI";
    public static IQueryable<Viaje> Aplicar(IQueryable<Viaje> viajes, FiltrosDeViajes filtros)
    {
        var consulta = viajes;

        if (filtros.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(viaje => viaje.ClienteId == clienteId);
        }

        if (filtros.TransportistaId is { } transportistaId)
        {
            consulta = consulta.Where(viaje => viaje.TransportistaId == transportistaId);
        }

        // **Predicado único**, no un filtrado posterior: sin filtro de estado los anulados no
        // aparecen, y con el filtro `anulado` aparecen sólo ellos. Escrito así, la exclusión es una
        // garantía de la consulta y no algo que alguien pueda olvidar más adelante (FR-044).
        consulta = filtros.Estado is { } estado
            ? consulta.Where(viaje => viaje.Estado == estado)
            : consulta.Where(viaje => viaje.Estado != EstadoViaje.Anulado);

        if (filtros.Desde is { } desde)
        {
            consulta = consulta.Where(viaje => viaje.Fecha >= desde);
        }

        if (filtros.Hasta is { } hasta)
        {
            consulta = consulta.Where(viaje => viaje.Fecha <= hasta);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
        {
            var patron = $"%{filtros.Busqueda.Trim()}%";

            consulta = consulta.Where(viaje =>
                EF.Functions.Like(EF.Functions.Collate(viaje.Origen, ColacionSinAcentos), patron) ||
                EF.Functions.Like(EF.Functions.Collate(viaje.Destino, ColacionSinAcentos), patron) ||
                EF.Functions.Like(
                    EF.Functions.Collate(viaje.Cliente!.RazonSocial, ColacionSinAcentos),
                    patron));
        }

        return consulta;
    }
}
