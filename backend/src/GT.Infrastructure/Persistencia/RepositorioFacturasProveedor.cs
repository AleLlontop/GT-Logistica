using GT.Application.Proveedores;
using GT.Domain.Proveedores;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Application.Liquidaciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioFacturasProveedor(GtDbContext contexto) : IRepositorioFacturasProveedor
{
    public async Task<GT.Application.Choferes.PaginaDe<FacturaProveedorResumen>> ConsultarAsync(FiltrosFacturaProveedor filtros, CancellationToken cancelacion)
    {
        ConsultarFacturasProveedor.Validar(filtros);
        var consulta = contexto.FacturasProveedor.AsNoTracking().AsQueryable();
        if (filtros.Desde is { } desde) consulta = consulta.Where(f => f.Fecha >= desde);
        if (filtros.Hasta is { } hasta) consulta = consulta.Where(f => f.Fecha <= hasta);
        if (filtros.ProveedorId is { } proveedor) consulta = consulta.Where(f => f.ProveedorId == proveedor);
        if (filtros.Estado == "pendiente") consulta = consulta.Where(f => f.Estado == EstadoFacturaProveedor.Pendiente);
        if (filtros.Estado == "pagada") consulta = consulta.Where(f => f.Estado == EstadoFacturaProveedor.Pagada);
        var total = await consulta.CountAsync(cancelacion);
        const int tamanio = GT.Application.Choferes.PaginaDe<FacturaProveedorResumen>.TamanioPorDefecto;
        var salto = ((long)filtros.Pagina - 1) * tamanio;
        if (salto > int.MaxValue) return new([], total, filtros.Pagina, tamanio);
        var filas = await consulta.OrderByDescending(f => f.Fecha).ThenByDescending(f => f.Id)
            .Skip((int)salto).Take(tamanio)
            .Select(f => new FacturaProveedorResumen(f.Id, f.Numero, f.Fecha, f.ProveedorId,
                f.ProveedorRazonSocial, f.ProveedorCuit, f.ImporteTotal, f.Estado == EstadoFacturaProveedor.Pagada ? "pagada" : "pendiente")).ToListAsync(cancelacion);
        return new(filas, total, filtros.Pagina, tamanio);
    }
    public async Task<IReadOnlyList<LiquidacionDisponibleProveedor>> ConsultarLiquidacionesDisponiblesAsync(int proveedorId, CancellationToken cancelacion)
    {
        var proveedor = await contexto.Proveedores.AsNoTracking().SingleOrDefaultAsync(p => p.Id == proveedorId, cancelacion)
            ?? throw ProveedorException.NoEncontrado();
        if (!proveedor.Activo) throw Inactivo();
        if (proveedor.Tipo == TipoProveedor.ProveedorNormal) return [];
        var transportista = await contexto.Transportistas.AsNoTracking().SingleAsync(t => t.Id == proveedor.TransportistaId, cancelacion);
        var emisor = await contexto.EmpresaEmisora.AsNoTracking().Select(e => e.Cuit).SingleOrDefaultAsync(cancelacion);
        ValidarExterno(transportista.Cuit, emisor);
        var filas = await contexto.Liquidaciones.AsNoTracking()
            .Where(l => l.TransportistaId == proveedor.TransportistaId && l.Estado == EstadoLiquidacion.Pendiente &&
                !contexto.FacturasProveedor.Any(f => f.LiquidacionId == l.Id))
            .OrderByDescending(l => l.Numero).ThenByDescending(l => l.Id)
            .Select(l => new { l.Id, l.Numero, l.PeriodoMes, l.PeriodoAnio, l.ImporteTotal }).ToListAsync(cancelacion);
        return filas.Select(l => new LiquidacionDisponibleProveedor(l.Id, NumerosVisibles.Liquidacion(l.Numero),
            Periodo(l.PeriodoMes, l.PeriodoAnio), l.ImporteTotal)).ToArray();
    }
    public async Task<int> RegistrarAsync(FacturaProveedor factura,CancellationToken cancelacion)
    {
        var intentoCommit=false;
        try
        {
            await using var transaccion=await contexto.Database.BeginTransactionAsync(cancelacion);
            await new CoordinadorIdentidadProveedor(contexto).AdquirirAsync(async()=>
            {
                var cuit=await contexto.Proveedores.AsNoTracking().Where(p=>p.Id==factura.ProveedorId)
                    .Select(p=>p.Tipo==TipoProveedor.Fletero?p.Transportista!.Cuit:p.Cuit).SingleOrDefaultAsync(cancelacion)
                    ?? throw ProveedorException.NoEncontrado();
                return new[]{cuit};
            },cancelacion);
            var filas=await contexto.Proveedores.Where(p=>p.Id==factura.ProveedorId && p.Activo)
                .ExecuteUpdateAsync(s=>s.SetProperty(p=>p.Activo,p=>p.Activo),cancelacion);
            if(filas!=1) throw Inactivo();
            var proveedor=await contexto.Proveedores.AsNoTracking().SingleAsync(p=>p.Id==factura.ProveedorId,cancelacion);
            factura.TipoProveedorAlRegistrar=proveedor.Tipo;
            if(proveedor.Tipo==TipoProveedor.Fletero)
            {
                if(factura.LiquidacionId is null or <=0) throw new ProveedorException("datos_invalidos","Elegí una liquidación Pendiente disponible del Fletero.","liquidacionId");
                // Mismo orden del padrón: coordinador/CUIT → proveedor → transportista → empresa → liquidación.
                await contexto.Transportistas.Where(t=>t.Id==proveedor.TransportistaId)
                    .ExecuteUpdateAsync(s=>s.SetProperty(t=>t.Cuit,t=>t.Cuit),cancelacion);
                var transportista=await contexto.Transportistas.AsNoTracking().SingleAsync(t=>t.Id==proveedor.TransportistaId,cancelacion);
                var emisor=await contexto.EmpresaEmisora.FromSqlRaw("SELECT * FROM [EmpresaEmisora] WITH (HOLDLOCK)")
                    .AsNoTracking().SingleOrDefaultAsync(cancelacion);
                ValidarExterno(transportista.Cuit,emisor?.Cuit);
                // Comparte el lock de editar/anular/pagar del Módulo 9, sin modificar sus reglas.
                var tomada=await contexto.Liquidaciones.Where(l=>l.Id==factura.LiquidacionId &&
                    l.TransportistaId==proveedor.TransportistaId && l.Estado==EstadoLiquidacion.Pendiente)
                    .ExecuteUpdateAsync(s=>s.SetProperty(l=>l.Estado,l=>l.Estado),cancelacion);
                if(tomada!=1) throw NoDisponible();
                var liquidacion=await contexto.Liquidaciones.AsNoTracking().SingleAsync(l=>l.Id==factura.LiquidacionId,cancelacion);
                if(await contexto.FacturasProveedor.AnyAsync(f=>f.LiquidacionId==liquidacion.Id,cancelacion)) throw NoDisponible();
                if(factura.ImporteTotal!=liquidacion.ImporteTotal) throw new ImportesLiquidacionDistintosException(factura.ImporteTotal,liquidacion.ImporteTotal);
                factura.ImporteLiquidacionAlRegistrar=liquidacion.ImporteTotal;
                factura.ProveedorRazonSocial=transportista.Nombre; factura.ProveedorCuit=transportista.Cuit;
            }
            else
            {
                if(factura.LiquidacionId is not null) throw new ProveedorException("datos_invalidos","Un proveedor normal no lleva liquidación.","liquidacionId");
                factura.ImporteLiquidacionAlRegistrar=null;
                factura.ProveedorRazonSocial=proveedor.RazonSocial!; factura.ProveedorCuit=proveedor.Cuit!;
            }
            factura.Estado=EstadoFacturaProveedor.Pendiente;
            if(await contexto.FacturasProveedor.AnyAsync(f=>f.ProveedorId==factura.ProveedorId && f.Numero==factura.Numero,cancelacion)) throw Duplicada();
            contexto.FacturasProveedor.Add(factura);
            try { await contexto.SaveChangesAsync(cancelacion); }
            catch(DbUpdateException error) when(error.InnerException is SqlException {Number:2601 or 2627} sql && sql.Message.Contains("IX_FacturasProveedor_ProveedorId_Numero")) { throw Duplicada(); }
            catch(DbUpdateException error) when(error.InnerException is SqlException {Number:2601 or 2627} sql && sql.Message.Contains("IX_FacturasProveedor_LiquidacionId")) { throw NoDisponible(); }
            intentoCommit=true;
            await transaccion.CommitAsync(cancelacion);
        }
        catch(Exception error) when(intentoCommit) { throw new ConfirmacionFacturaInciertaException(factura.Id,error); }
        return factura.Id;
    }
    public Task<bool> EstaConfirmadaAsync(int id,string ruta,CancellationToken cancelacion)=>
        // Leer con locks aun si la base usa RCSI: no interpretar una foto anterior al commit como ausencia.
        contexto.FacturasProveedor.FromSqlInterpolated($"SELECT * FROM [FacturasProveedor] WITH (READCOMMITTEDLOCK) WHERE [Id]={id} AND [ArchivoRuta]={ruta}")
            .AsNoTracking().AnyAsync(cancelacion);
    public Task<PdfFacturaPreparado?> ObtenerPdfAsync(int id,CancellationToken cancelacion)=>
        contexto.FacturasProveedor.AsNoTracking().Where(f=>f.Id==id && f.ArchivoRuta!=null)
            .Select(f=>new PdfFacturaPreparado(f.ArchivoRuta!,f.ArchivoNombre!,f.ArchivoTipoContenido!)).SingleOrDefaultAsync(cancelacion);
    public async Task<DetalleFacturaProveedor?> ObtenerAsync(int id,CancellationToken cancelacion)
    {
        var f=await contexto.FacturasProveedor.AsNoTracking().Include(f=>f.Liquidacion).SingleOrDefaultAsync(f=>f.Id==id,cancelacion);
        if(f is null) return null;
        return new DetalleFacturaProveedor(
            f.Id,f.ProveedorId,f.ProveedorRazonSocial,f.ProveedorCuit,f.TipoProveedorAlRegistrar==TipoProveedor.Fletero?"fletero":"proveedorNormal",
            f.Numero,f.Fecha,f.Detalle,f.Neto,f.Iva,f.ImporteTotal,f.Cae,f.FechaVencimientoCae,f.Estado == EstadoFacturaProveedor.Pagada ? "pagada" : "pendiente",
            f.ArchivoNombre==null?null:new ArchivoFacturaProveedor(f.ArchivoNombre,"/api/proveedores/facturas/"+f.Id+"/archivo"),
            f.Liquidacion is null?null:new LiquidacionFacturaProveedorDetalle(f.Liquidacion.Id,NumerosVisibles.Liquidacion(f.Liquidacion.Numero),
                Periodo(f.Liquidacion.PeriodoMes,f.Liquidacion.PeriodoAnio),f.ImporteLiquidacionAlRegistrar!.Value,
                f.Liquidacion.ImporteTotal,NombresDeEstadoLiquidacion.EnJson(f.Liquidacion.Estado)));
    }
    private static string Periodo(byte mes,short anio)=>$"{mes:00}/{anio:0000}";
    private static ProveedorException Inactivo()=>new("proveedor_inactivo","El proveedor está inactivo. Elegí otro proveedor.","proveedorId",TipoErrorProveedor.Conflicto);
    private static ProveedorException NoDisponible()=>new("liquidacion_no_disponible","La liquidación ya no está Pendiente y disponible para este Fletero. Elegí otra liquidación.","liquidacionId",TipoErrorProveedor.Conflicto);
    private static void ValidarExterno(string cuit,string? emisor)
    {
        if(string.IsNullOrWhiteSpace(emisor)) throw new ProveedorException("emisor_sin_configurar","Configurá la empresa emisora antes de elegir un Fletero.","proveedorId",TipoErrorProveedor.Conflicto);
        if(cuit==NormalizadorDocumentoNumerico.Normalizar(emisor)) throw new ProveedorException("transportista_no_externo","El Transportista vinculado ya no es externo a la empresa emisora. Elegí otro proveedor.","proveedorId",TipoErrorProveedor.Conflicto);
    }
    private static ProveedorException Duplicada()=>new("numero_duplicado","Ese número de factura ya está registrado para este proveedor.","numero");
}
