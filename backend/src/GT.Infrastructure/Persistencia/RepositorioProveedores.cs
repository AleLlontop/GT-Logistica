using System.Linq.Expressions;
using GT.Application.Choferes;
using GT.Application.Proveedores;
using GT.Domain.Choferes;
using GT.Domain.Proveedores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioProveedores(GtDbContext contexto) : IRepositorioProveedores
{
    // Una única proyección efectiva; identidad/contacto de Fletero nunca se copian al proveedor.
    private static readonly Expression<Func<Proveedor, ProveedorDetalle>> Lectura = p => new()
    {
        Id = p.Id, RazonSocial = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Nombre : p.RazonSocial!,
        NombreComercial = p.NombreComercial, Cuit = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Cuit : p.Cuit!,
        Tipo = p.Tipo == TipoProveedor.Fletero ? "fletero" : "proveedorNormal", Estado = p.Activo ? "activo" : "inactivo",
        Telefono = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Telefono : p.Telefono,
        Email = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Email : p.Email,
        Direccion = p.Direccion, TransportistaId = p.TransportistaId, Version = p.Version,
    };

    public Task<ProveedorDetalle?> ObtenerAsync(int id, CancellationToken cancelacion) =>
        contexto.Proveedores.AsNoTracking().Where(p => p.Id == id).Select(Lectura).SingleOrDefaultAsync(cancelacion);

    public async Task<PaginaDe<ProveedorResumen>> ConsultarAsync(FiltrosProveedor filtros, CancellationToken cancelacion)
    {
        ReglasProveedor.ValidarFiltros(filtros);
        var consulta = contexto.Proveedores.AsNoTracking().Select(Lectura);
        if (!string.IsNullOrWhiteSpace(filtros.Q))
        {
            var patron = Patron(filtros.Q);
            consulta = consulta.Where(p => EF.Functions.Like(p.RazonSocial, patron, "\\") ||
                EF.Functions.Like(p.NombreComercial!, patron, "\\") || EF.Functions.Like(p.Cuit, patron, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(filtros.RazonSocial))
        {
            var patron = Patron(filtros.RazonSocial);
            consulta = consulta.Where(p => EF.Functions.Like(p.RazonSocial, patron, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(filtros.NombreComercial))
        {
            var patron = Patron(filtros.NombreComercial);
            consulta = consulta.Where(p => EF.Functions.Like(p.NombreComercial!, patron, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(filtros.Cuit))
        {
            var patron = Patron(filtros.Cuit);
            consulta = consulta.Where(p => EF.Functions.Like(p.Cuit, patron, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(filtros.Tipo)) consulta = consulta.Where(p => p.Tipo == filtros.Tipo);
        if (!string.IsNullOrWhiteSpace(filtros.Estado)) consulta = consulta.Where(p => p.Estado == filtros.Estado);
        var total = await consulta.CountAsync(cancelacion);
        var items = await consulta.OrderBy(p => p.RazonSocial).ThenBy(p => p.Id)
            .Skip((filtros.Pagina - 1) * 20).Take(20)
            .Select(p => new ProveedorResumen(p.Id, p.RazonSocial, p.NombreComercial, p.Cuit, p.Tipo, p.Estado)).ToListAsync(cancelacion);
        return new(items, total, filtros.Pagina, 20);
    }

    public async Task<PaginaDe<TransportistaExterno>> ExternosAsync(string? q, int pagina, CancellationToken cancelacion)
    {
        ReglasProveedor.ValidarFiltros(new(Pagina: pagina));
        var cuitEmisor = await contexto.EmpresaEmisora.AsNoTracking().Select(e => e.Cuit).SingleOrDefaultAsync(cancelacion);
        if (string.IsNullOrWhiteSpace(cuitEmisor)) throw SinEmisor();
        var propio = NormalizadorDocumentoNumerico.Normalizar(cuitEmisor);
        var consulta = contexto.Transportistas.AsNoTracking().Where(t => t.Cuit != propio);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var patron = Patron(q);
            consulta = consulta.Where(t => EF.Functions.Like(t.Nombre, patron, "\\") || EF.Functions.Like(t.Cuit, patron, "\\"));
        }
        var total = await consulta.CountAsync(cancelacion);
        var items = await consulta.OrderBy(t => t.Nombre).ThenBy(t => t.Id).Skip((pagina - 1) * 20).Take(20)
            .Select(t => new TransportistaExterno(t.Id, t.Nombre, t.Cuit)).ToListAsync(cancelacion);
        return new(items, total, pagina, 20);
    }

    public async Task<ProveedorDetalle> GuardarAsync(int? id, ProveedorPeticion datos, CancellationToken cancelacion)
    {
        datos = ReglasProveedor.Normalizar(datos, id.HasValue);
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await new CoordinadorIdentidadProveedor(contexto).AdquirirAsync(async () =>
        {
            var cuits = new List<string>();
            if (id.HasValue)
            {
                var anterior = await contexto.Proveedores.AsNoTracking().Where(p => p.Id == id).Select(Lectura).SingleOrDefaultAsync(cancelacion)
                    ?? throw ProveedorException.NoEncontrado();
                cuits.Add(anterior.Cuit);
            }
            if (datos.Tipo == "fletero")
            {
                var cuit = await contexto.Transportistas.AsNoTracking().Where(t => t.Id == datos.TransportistaId).Select(t => t.Cuit).SingleOrDefaultAsync(cancelacion);
                if (cuit is null) throw new ProveedorException("datos_invalidos", "No encontramos ese transportista.", "transportistaId");
                cuits.Add(cuit);
            }
            else cuits.Add(datos.Cuit!);
            return cuits;
        }, cancelacion);

        if (id.HasValue)
        {
            var filas = await contexto.Proveedores.Where(p => p.Id == id && p.Version == datos.Version)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Version, p => p.Version), cancelacion);
            if (filas != 1)
            {
                if (!await contexto.Proveedores.AnyAsync(p => p.Id == id, cancelacion)) throw ProveedorException.NoEncontrado();
                throw ProveedorException.Cambio();
            }
        }
        string cuitEfectivo;
        if (datos.Tipo == "fletero")
        {
            await contexto.Transportistas.Where(t => t.Id == datos.TransportistaId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Cuit, t => t.Cuit), cancelacion);
            var externo = await contexto.Transportistas.AsNoTracking().SingleAsync(t => t.Id == datos.TransportistaId, cancelacion);
            var emisor = await contexto.EmpresaEmisora.FromSqlRaw("SELECT * FROM [EmpresaEmisora] WITH (HOLDLOCK)")
                .AsNoTracking().SingleOrDefaultAsync(cancelacion);
            if (emisor is null || string.IsNullOrWhiteSpace(emisor.Cuit)) throw SinEmisor();
            if (externo.Cuit == NormalizadorDocumentoNumerico.Normalizar(emisor.Cuit))
                throw new ProveedorException("datos_invalidos", "Elegí un transportista externo a la empresa emisora.", "transportistaId");
            cuitEfectivo = NormalizadorDocumentoNumerico.Normalizar(externo.Cuit);
        }
        else cuitEfectivo = datos.Cuit!;

        if (await contexto.Proveedores.Where(p => p.Id != (id ?? 0))
            .AnyAsync(p => (p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Cuit : p.Cuit) == cuitEfectivo, cancelacion))
            throw ProveedorException.Duplicado();

        var proveedor = id.HasValue
            ? await contexto.Proveedores.AsNoTracking().SingleAsync(p => p.Id == id, cancelacion)
            : new Proveedor();
        proveedor.Tipo = datos.Tipo == "fletero" ? TipoProveedor.Fletero : TipoProveedor.ProveedorNormal;
        proveedor.RazonSocial = datos.RazonSocial; proveedor.Cuit = datos.Cuit;
        proveedor.Telefono = datos.Telefono; proveedor.Email = datos.Email;
        proveedor.NombreComercial = datos.NombreComercial; proveedor.Direccion = datos.Direccion;
        proveedor.TransportistaId = datos.TransportistaId;
        // ExecuteUpdate ya serializó la fila y verificó Version. No se pisa Activo.
        if (id.HasValue)
        {
            proveedor.Version++;
            // Reemplazar cualquier lectura rastreada anterior al lock por la fila recién leída.
            var rastreada = contexto.ChangeTracker.Entries<Proveedor>().SingleOrDefault(e => e.Entity.Id == id);
            if (rastreada is not null) rastreada.State = EntityState.Detached;
            contexto.Proveedores.Update(proveedor);
            contexto.Entry(proveedor).Property(p => p.Version).OriginalValue = datos.Version!.Value;
        }
        else contexto.Proveedores.Add(proveedor);
        try { await contexto.SaveChangesAsync(cancelacion); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 } sql &&
            (sql.Message.Contains("IX_Proveedores_Cuit") || sql.Message.Contains("IX_Proveedores_TransportistaId")))
        { throw ProveedorException.Duplicado(); }
        await transaccion.CommitAsync(cancelacion);
        return (await ObtenerAsync(proveedor.Id, cancelacion))!;
    }

    public async Task<ProveedorDetalle> CambiarEstadoAsync(int id, long version, bool activo, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        var filas = await contexto.Proveedores.Where(p => p.Id == id && p.Version == version && p.Activo != activo)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Activo, activo).SetProperty(p => p.Version, p => p.Version + 1), cancelacion);
        if (filas == 0)
        {
            var actual = await ObtenerAsync(id, cancelacion) ?? throw ProveedorException.NoEncontrado();
            if (!activo || actual.Estado != "activo") throw ProveedorException.Cambio();
        }
        await transaccion.CommitAsync(cancelacion);
        return (await ObtenerAsync(id, cancelacion))!;
    }
    private static string Patron(string valor) => "%" + valor.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";
    private static ProveedorException SinEmisor() => new("emisor_sin_configurar", "Configurá la empresa emisora antes de elegir un Fletero.", "transportistaId", TipoErrorProveedor.Conflicto);
}
