using GT.Application.Reportes.Analisis;

namespace GT.Application.Reportes.Fuentes;

public static class FuenteAnalisisLiquidaciones
{
    // Copia declarada del mapa NOMBRES_DE_ESTADO de Liquidaciones/servicios/servicioLiquidaciones.ts ([012]).
    public static string EstadoVisible(string codigo) => codigo switch
    {
        "pendiente" => "Pendiente", "pagada" => "Pagada", "anulada" => "Anulada",
        _ => throw new ArgumentOutOfRangeException(nameof(codigo)),
    };

    public static ReporteTabular Crear(AnalisisLiquidaciones datos, string filtros, string autor) => new(
        new("Liquidaciones por período liquidado", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        [ColumnaDeReporte.Texto("Número LQ"), ColumnaDeReporte.Texto("Período liquidado"), ColumnaDeReporte.Texto("Transportista"),
         ColumnaDeReporte.Texto("Estado"), ColumnaDeReporte.Importe("Importe total"), ColumnaDeReporte.Importe("Pagado vigente"), ColumnaDeReporte.Importe("Pendiente vigente")],
        datos.Detalle.Items.Select(l => (IReadOnlyList<CeldaDeReporte>)[CeldaDeReporte.Texto(l.Numero), CeldaDeReporte.Texto(l.Periodo),
            CeldaDeReporte.Texto(l.Transportista.Nombre), CeldaDeReporte.Texto(EstadoVisible(l.Estado)),
            CeldaDeReporte.Importe(l.ImporteTotal), CeldaDeReporte.Importe(l.PagadoVigente), CeldaDeReporte.Importe(l.PendienteVigente)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de liquidaciones", datos.Resumen.Cantidad),
         TotalDeReporte.Cantidad("Anuladas (sin deuda vigente)", datos.Resumen.CantidadAnuladas),
         new("Importe liquidado vigente", datos.Resumen.ImporteLiquidado),
         new("Pagado vigente de estos documentos", datos.Resumen.PagadoVigenteDocumental),
         new("Pendiente vigente de estos documentos", datos.Resumen.PendienteVigenteDocumental)]);
}

public static class FuenteAnalisisPagos
{
    public static ReporteTabular Crear(AnalisisPagos datos, string filtros, string autor) => new(
        new("Pagos por fecha efectiva de pago", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        [ColumnaDeReporte.Texto("OP") with { AnchoRelativoPdf = 0.8f },
         ColumnaDeReporte.Texto("Liquidación") with { AnchoRelativoPdf = 1.1f },
         ColumnaDeReporte.Texto("Período liquidado") with { AnchoRelativoPdf = 1.1f },
         ColumnaDeReporte.Texto("Transportista") with { AnchoRelativoPdf = 1.8f },
         ColumnaDeReporte.Fecha("Fecha de pago") with { AnchoRelativoPdf = 1.3f },
         ColumnaDeReporte.Importe("Importe") with { AnchoRelativoPdf = 1.7f },
         ColumnaDeReporte.Texto("Origen") with { AnchoRelativoPdf = 0.8f },
         ColumnaDeReporte.Texto("OPP de origen") with { AnchoRelativoPdf = 1f },
         ColumnaDeReporte.Texto("Vigencia") with { AnchoRelativoPdf = 1f },
         ColumnaDeReporte.Texto("Revertida el (Argentina)") with { AnchoRelativoPdf = 1.7f },
         ColumnaDeReporte.Texto("Revertida por") with { AnchoRelativoPdf = 1.5f }],
        datos.Detalle.Items.Select(o => (IReadOnlyList<CeldaDeReporte>)[CeldaDeReporte.Texto(o.Numero), CeldaDeReporte.Texto(o.LiquidacionNumero),
            CeldaDeReporte.Texto(o.PeriodoLiquidado), CeldaDeReporte.Texto(o.Transportista.Nombre), CeldaDeReporte.Fecha(o.FechaPago),
            CeldaDeReporte.Importe(o.Importe), CeldaDeReporte.Texto(o.Origen == "manual" ? "Manual" : "OPP"), CeldaDeReporte.Texto(o.OppNumero),
            CeldaDeReporte.Texto(o.Vigente ? "Vigente" : "Revertida"),
            CeldaDeReporte.Texto(o.RevertidaEn is { } fecha ? EncabezadoDeReporte.EnArgentina(fecha).ToString("dd/MM/yyyy HH:mm") : null),
            CeldaDeReporte.Texto(o.RevertidaPor?.Nombre)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de pagos vigentes", datos.Resumen.CantidadPagosVigentes),
         new("Pagado vigente en el período", datos.Resumen.ImportePagadoVigente),
         TotalDeReporte.Cantidad("Cantidad de pagos revertidos", datos.Resumen.CantidadRevertidos),
         new("Importe revertido histórico (excluido del vigente)", datos.Resumen.ImporteRevertidoHistorico)]);
}
