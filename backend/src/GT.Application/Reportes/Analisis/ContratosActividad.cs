using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

public record ReferenciaAnalisis(int Id, string Nombre, bool? Activo = null);
public record GrupoCantidad(string? Id, string Nombre, long Cantidad);
public record GrupoImporte(string? Id, string Nombre, long Cantidad, decimal Importe);
public record DistribucionCantidad(IReadOnlyList<GrupoCantidad> Top, GrupoCantidad? Otros, int TotalCategorias);
public record DistribucionImporte(IReadOnlyList<GrupoImporte> Top, GrupoImporte? Otros, int TotalCategorias);

public record ResumenViajes(long Cantidad, long Realizados, long PendientesRendicion, long Pendientes, long Anulados, decimal ImporteTotal);
public record MesViajes(string Periodo, long Cantidad, long Realizados, long PendientesRendicion);
public record DistribucionesViajes(DistribucionCantidad Estado, DistribucionImporte Cliente, DistribucionImporte Transportista);
public record FilaViajeAnalisis(int Id, int Numero, DateOnly Fecha, ReferenciaAnalisis Cliente, string Origen,
    string Destino, ReferenciaAnalisis? Chofer, ReferenciaAnalisis? Vehiculo, ReferenciaAnalisis? Transportista,
    string Estado, decimal Importe);
public record AnalisisViajes(ContextoAnalisis Contexto, ResumenViajes Resumen, IReadOnlyList<MesViajes> Series,
    DistribucionesViajes Distribuciones, PaginaDe<FilaViajeAnalisis> Detalle);

public record ResumenFacturacion(long Cantidad, decimal Facturado, decimal CobradoVigenteDocumental,
    decimal PendienteVigenteDocumental, long CantidadAnuladas);
public record MesFacturacion(string Periodo, decimal Facturado, decimal CobradoVigenteDocumental, decimal PendienteVigenteDocumental);
public record DistribucionesFacturacion(DistribucionCantidad Estado, DistribucionImporte Cliente);
public record FilaFacturaAnalisis(int Id, string NumeroComprobante, DateOnly Fecha, int ClienteId,
    string ClienteRazonSocial, string Estado, decimal Neto, decimal Iva, decimal Total, DateOnly? FechaCobro);
public record AnalisisFacturacion(ContextoAnalisis Contexto, ResumenFacturacion Resumen, IReadOnlyList<MesFacturacion> Series,
    DistribucionesFacturacion Distribuciones, PaginaDe<FilaFacturaAnalisis> Detalle);

public record ResumenCobros(long CantidadCobros, decimal ImporteCobrado);
public record MesCobros(string Periodo, decimal ImporteCobrado);
public record DistribucionesCobros(DistribucionImporte Cliente);
public record FilaCobroAnalisis(int FacturaId, string NumeroComprobante, int ClienteId, string ClienteRazonSocial,
    DateOnly FechaFactura, DateOnly FechaCobro, decimal ImporteCobrado);
public record AnalisisCobros(ContextoAnalisis Contexto, ResumenCobros Resumen, IReadOnlyList<MesCobros> Series,
    DistribucionesCobros Distribuciones, PaginaDe<FilaCobroAnalisis> Detalle);

public enum EstadoSeccionAnalisis { Disponible, SinPermiso }
/// <summary>Una sección denegada no transporta datos; no se usa null como importe cero.</summary>
public sealed record SeccionAnalisis<T> where T : class
{
    public EstadoSeccionAnalisis Estado { get; }
    public T? Datos { get; }
    private SeccionAnalisis(EstadoSeccionAnalisis estado, T? datos) => (Estado, Datos) = (estado, datos);
    public static SeccionAnalisis<T> SinPermiso() => new(EstadoSeccionAnalisis.SinPermiso, null);
    public static SeccionAnalisis<T> Disponible(T datos) => new(EstadoSeccionAnalisis.Disponible, datos ?? throw new ArgumentNullException(nameof(datos)));
}

// El dashboard conserva sólo agregados, nunca páginas completas de entidades.
public record DashboardViajes(ResumenViajes Resumen, IReadOnlyList<MesViajes> Series, DistribucionesViajes Distribuciones);
public record DashboardFacturacion(ResumenFacturacion Documentos, IReadOnlyList<MesFacturacion> SerieDocumentos,
    ResumenCobros Cobros, IReadOnlyList<MesCobros> SerieCobros, DistribucionesFacturacion DistribucionesDocumentos,
    DistribucionesCobros DistribucionesCobros);
