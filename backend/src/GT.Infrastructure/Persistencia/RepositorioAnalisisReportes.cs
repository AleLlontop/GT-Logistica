using GT.Application.Reportes.Analisis;

namespace GT.Infrastructure.Persistencia;

/// <summary>Proyecciones analíticas materializadas sobre la base operativa, sin entidades nuevas.</summary>
public partial class RepositorioAnalisisReportes : IConsultaViajesAnalisis, IConsultaFacturacionAnalisis, IConsultaLiquidacionesAnalisis, IConsultaCajaAnalisis, IConsultaFlotaAnalisis, IConsultaVencimientosAnalisis, IConsultaDashboardAnalisis, IExportacionAnalisis
{
    private readonly GtDbContext db;
    private readonly CoordinadorFotoAnalisis foto;
    private readonly GT.Application.Reportes.OpcionesDeReporte opciones;
    public RepositorioAnalisisReportes(GtDbContext db, CoordinadorFotoAnalisis foto,
        GT.Application.Reportes.OpcionesDeReporte? opciones = null)
        => (this.db, this.foto, this.opciones) = (db, foto, opciones ?? GT.Application.Reportes.OpcionesDeReporte.PorDefecto);
}
