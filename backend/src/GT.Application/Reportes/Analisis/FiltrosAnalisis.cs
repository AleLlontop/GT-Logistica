namespace GT.Application.Reportes.Analisis;

public sealed class ReglaAnalisisException(string codigo, string mensaje, string? campo = null) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public string? Campo { get; } = campo;
}

public abstract record FiltroTemporalAnalisis(PeriodoAnalisis Periodo, int Pagina = 1, int TamanioPagina = 20);
public record FiltroViajesAnalisis(PeriodoAnalisis Periodo, int? ClienteId = null, int? TransportistaId = null,
    string? Estado = null, string? Busqueda = null, int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroFacturacionAnalisis(PeriodoAnalisis Periodo, int? ClienteId = null, string? Estado = null,
    string? Numero = null, int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroCobrosAnalisis(PeriodoAnalisis Periodo, int? ClienteId = null, string? Numero = null,
    int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroLiquidacionesAnalisis(PeriodoAnalisis Periodo, int? TransportistaId = null, string? Estado = null,
    int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroPagosAnalisis(PeriodoAnalisis Periodo, int? TransportistaId = null, string Vigencia = "todos",
    int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroCajaAnalisis(PeriodoAnalisis Periodo, int? CajaId = null,
    int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroFlotaAnalisis(PeriodoAnalisis Periodo, int? VehiculoId = null, int? TipoVehiculoId = null,
    int? TransportistaId = null, bool? Activo = null, string Orden = "cantidadDesc",
    int Pagina = 1, int TamanioPagina = 20) : FiltroTemporalAnalisis(Periodo, Pagina, TamanioPagina);
public record FiltroPaginaAnalisis(int Pagina = 1, int TamanioPagina = 20);
public enum SeccionReporte { Viajes, Facturacion, Liquidaciones, Caja, Flota, VencimientosChoferes, VencimientosFlota, VencimientosFacturas }
public enum RecursoAnalisis { Viajes, Facturacion, Cobros, Liquidaciones, Pagos, Caja, Flota }
public enum ReferenciaDeAnalisis { ClientesViajes, TransportistasViajes, ClientesFacturacion, TransportistasLiquidaciones, Cajas, Vehiculos, TiposVehiculo, TransportistasFlota }
public record FiltroReferenciaAnalisis(ReferenciaDeAnalisis Recurso, string? Busqueda = null, int Pagina = 1, int TamanioPagina = 20);
public record FiltroGrupoAnalisis(FiltroTemporalAnalisis Filtro, string Dimension, int Pagina = 1, int TamanioPagina = 20);

public static class ValidacionAnalisis
{
    public static void Pagina(int pagina, int tamanio)
    {
        if (pagina < 1 || tamanio is < 1 or > 200 || (long)(pagina - 1) * tamanio > int.MaxValue)
            throw new ReglaAnalisisException("filtro_invalido", "Elegí una página y un tamaño válidos (1 a 200 filas).", "pagina");
    }

    public static void Filtro(FiltroTemporalAnalisis filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.Periodo is null) throw new ReglaAnalisisException("rango_requerido", "Elegí el período de análisis.", "desde");
        Pagina(filtro.Pagina, filtro.TamanioPagina);
        static void Id(int? id, string campo)
        {
            if (id is <= 0) throw new ReglaAnalisisException("filtro_invalido", "Elegí una referencia válida.", campo);
        }
        static void Codigo(string? codigo, string campo, params string[] permitidos)
        {
            if (codigo is not null && !permitidos.Contains(codigo, StringComparer.Ordinal))
                throw new ReglaAnalisisException("filtro_invalido", "Elegí una opción válida.", campo);
        }
        switch (filtro)
        {
            case FiltroViajesAnalisis f:
                Id(f.ClienteId, "clienteId"); Id(f.TransportistaId, "transportistaId");
                Codigo(f.Estado, "estado", "pendiente", "enCurso", "rendido", "anulado", "facturado"); break;
            case FiltroFacturacionAnalisis f:
                Id(f.ClienteId, "clienteId"); Codigo(f.Estado, "estado", "pendiente", "vencida", "pagada", "anulada"); break;
            case FiltroCobrosAnalisis f: Id(f.ClienteId, "clienteId"); break;
            case FiltroLiquidacionesAnalisis f:
                Id(f.TransportistaId, "transportistaId"); Codigo(f.Estado, "estado", "pendiente", "pagada", "anulada"); break;
            case FiltroPagosAnalisis f:
                Id(f.TransportistaId, "transportistaId"); Codigo(f.Vigencia, "vigencia", "todos", "vigentes", "revertidos"); break;
            case FiltroCajaAnalisis f:
                Id(f.CajaId, "cajaId");
                if (f.Periodo.Hasta == DateOnly.MaxValue)
                    throw new ReglaAnalisisException("rango_invalido", "La fecha hasta excede el intervalo de instantes disponible.", "hasta");
                break;
            case FiltroFlotaAnalisis f:
                Id(f.VehiculoId, "vehiculoId"); Id(f.TipoVehiculoId, "tipoVehiculoId"); Id(f.TransportistaId, "transportistaId");
                Codigo(f.Orden, "orden", "cantidadDesc", "cantidadAsc"); break;
            default: throw new ReglaAnalisisException("filtro_invalido", "La perspectiva de análisis no es válida.");
        }
    }

    public static void Grupo(FiltroGrupoAnalisis grupo)
    {
        Filtro(grupo.Filtro); Pagina(grupo.Pagina, grupo.TamanioPagina);
        string[] dimensiones = grupo.Filtro switch
        {
            FiltroViajesAnalisis => ["estado", "cliente", "transportista"],
            FiltroFacturacionAnalisis => ["estado", "cliente"],
            FiltroCobrosAnalisis => ["cliente"],
            FiltroLiquidacionesAnalisis => ["estado", "transportista"],
            FiltroPagosAnalisis => ["transportista"],
            FiltroCajaAnalisis => ["tipo"],
            FiltroFlotaAnalisis => ["tipo", "transportista"],
            _ => [],
        };
        if (!dimensiones.Contains(grupo.Dimension, StringComparer.Ordinal))
            throw new ReglaAnalisisException("filtro_invalido", "La agrupación no corresponde a este reporte.", "dimension");
    }
}
