namespace GT.Application.Reportes.Analisis;

/// <summary>Consulta una foto completa y la dibuja después de cerrar Snapshot, reutilizando los motores012.</summary>
public sealed class EmitirAnalisisReporte(IExportacionAnalisis fuentes, ArmadoDeReporte armado, TimeProvider reloj)
{
    public async Task<ResultadoReporte> EjecutarAsync(FiltroTemporalAnalisis filtro, FormatoDeReporte formato, string autor, CancellationToken ct = default)
    {
        var instante = reloj.GetUtcNow();
        var (tipo, modelo) = filtro switch
        {
            FiltroViajesAnalisis f => (TipoDeReporte.Viajes, await fuentes.ExportarViajesAsync(f, instante, autor, ct)),
            FiltroFacturacionAnalisis f => (TipoDeReporte.Facturacion, await fuentes.ExportarFacturacionAsync(f, instante, autor, ct)),
            FiltroCobrosAnalisis f => (TipoDeReporte.Cobros, await fuentes.ExportarCobrosAsync(f, instante, autor, ct)),
            FiltroLiquidacionesAnalisis f => (TipoDeReporte.Liquidaciones, await fuentes.ExportarLiquidacionesAsync(f, instante, autor, ct)),
            FiltroPagosAnalisis f => (TipoDeReporte.Pagos, await fuentes.ExportarPagosAsync(f, instante, autor, ct)),
            FiltroCajaAnalisis f => (TipoDeReporte.MovimientosCaja, await fuentes.ExportarCajaAsync(f, instante, autor, ct)),
            FiltroFlotaAnalisis f => (TipoDeReporte.UtilizacionFlota, await fuentes.ExportarFlotaAsync(f, instante, autor, ct)),
            _ => throw new ReglaAnalisisException("filtro_invalido", "Elegí un reporte válido."),
        };
        ct.ThrowIfCancellationRequested();
        return armado.Entregar(tipo, formato, modelo);
    }

    public async Task<ResultadoReporte> VencimientosAsync(SeccionReporte panel, FormatoDeReporte formato, string autor, CancellationToken ct = default)
    {
        var tipo = panel switch
        {
            SeccionReporte.VencimientosChoferes => TipoDeReporte.VencimientosChoferes,
            SeccionReporte.VencimientosFlota => TipoDeReporte.VencimientosFlota,
            SeccionReporte.VencimientosFacturas => TipoDeReporte.VencimientosFacturas,
            _ => throw new ReglaAnalisisException("filtro_invalido", "Elegí un panel de vencimientos válido."),
        };
        var modelo = await fuentes.ExportarVencimientosAsync(panel, reloj.GetUtcNow(), autor, ct);
        ct.ThrowIfCancellationRequested();
        return armado.Entregar(tipo, formato, modelo);
    }
}
