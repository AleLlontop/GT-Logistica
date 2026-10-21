using GT.Application.Facturacion;
using GT.Application.Reportes.Analisis;

namespace GT.Application.Reportes.Fuentes;

public static class FuenteAnalisisFacturacion
{
    public static ReporteTabular Crear(AnalisisFacturacion datos, string filtros, string autor) => new(
        new("Facturación — documentos por fecha de factura", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        [ColumnaDeReporte.Texto("Número"), ColumnaDeReporte.Fecha("Fecha de factura"), ColumnaDeReporte.Texto("Cliente histórico"),
         ColumnaDeReporte.Texto("Estado"), ColumnaDeReporte.Importe("Neto"), ColumnaDeReporte.Importe("IVA"),
         ColumnaDeReporte.Importe("Total"), ColumnaDeReporte.Fecha("Fecha de cobro")],
        datos.Detalle.Items.Select(f => (IReadOnlyList<CeldaDeReporte>)[
            CeldaDeReporte.Texto(f.NumeroComprobante), CeldaDeReporte.Fecha(f.Fecha), CeldaDeReporte.Texto(f.ClienteRazonSocial),
            CeldaDeReporte.Texto(EstadoFacturaAnalisis.EnPantalla(NombresDeEstadoFactura.LeerEstado(f.Estado)!.Value)),
            CeldaDeReporte.Importe(f.Neto), CeldaDeReporte.Importe(f.Iva), CeldaDeReporte.Importe(f.Total), CeldaDeReporte.Fecha(f.FechaCobro)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de facturas", datos.Resumen.Cantidad),
         TotalDeReporte.Cantidad("Anuladas (sin contribución financiera)", datos.Resumen.CantidadAnuladas),
         new("Neto vigente", datos.Detalle.Items.Where(f => f.Estado != "anulada").Sum(f => f.Neto)),
         new("IVA vigente", datos.Detalle.Items.Where(f => f.Estado != "anulada").Sum(f => f.Iva)),
         new("Facturado vigente", datos.Resumen.Facturado),
         new("Cobrado vigente de estos documentos", datos.Resumen.CobradoVigenteDocumental),
         new("Pendiente vigente de estos documentos", datos.Resumen.PendienteVigenteDocumental)]);
}

public static class FuenteAnalisisCobros
{
    public static ReporteTabular Crear(AnalisisCobros datos, string filtros, string autor) => new(
        new("Cobros por fecha efectiva de cobro", filtros, datos.Detalle.Items.Count, datos.Contexto.ConsultadoEn.UtcDateTime, autor),
        [ColumnaDeReporte.Texto("Número"), ColumnaDeReporte.Texto("Cliente histórico"), ColumnaDeReporte.Fecha("Fecha de factura"),
         ColumnaDeReporte.Fecha("Fecha de cobro"), ColumnaDeReporte.Importe("Importe cobrado")],
        datos.Detalle.Items.Select(f => (IReadOnlyList<CeldaDeReporte>)[CeldaDeReporte.Texto(f.NumeroComprobante),
            CeldaDeReporte.Texto(f.ClienteRazonSocial), CeldaDeReporte.Fecha(f.FechaFactura), CeldaDeReporte.Fecha(f.FechaCobro),
            CeldaDeReporte.Importe(f.ImporteCobrado)]).ToArray(),
        [TotalDeReporte.Cantidad("Cantidad de cobros", datos.Resumen.CantidadCobros), new("Cobrado en el período", datos.Resumen.ImporteCobrado)]);
}
