using GT.Application.Choferes;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<PaginaDe<BancoDto>> BancosAsync(int pagina, CancellationToken ct = default)
    {
        ElegibilidadPagosSql.ValidarPagina(pagina);
        var consulta = contexto.Bancos.AsNoTracking();
        return new(await consulta.OrderBy(x => x.Nombre).ThenBy(x => x.Id).Skip((pagina - 1) * 20).Take(20)
            .Select(x => new BancoDto(x.Id, x.Nombre, x.Activo, x.Version)).ToListAsync(ct), await consulta.CountAsync(ct), pagina, 20);
    }
    public Task<BancoDto?> BancoAsync(int id, CancellationToken ct = default) => contexto.Bancos.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new BancoDto(x.Id, x.Nombre, x.Activo, x.Version)).SingleOrDefaultAsync(ct);

    public async Task<BancoDto> CrearBancoAsync(BancoEntrada entrada, CancellationToken ct = default)
    {
        var nombre = GestionBancosPagos.ValidarNombre(entrada.Nombre);
        if (await contexto.Bancos.AnyAsync(x => x.Nombre == nombre, ct)) throw Duplicado();
        var banco = new Banco { Nombre = nombre };
        contexto.Bancos.Add(banco);
        try { await contexto.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (BloqueosPagos.Traducir(e) is not null)
        {
            contexto.Entry(banco).State = EntityState.Detached;
            throw BloqueosPagos.Traducir(e)!;
        }
        return (await BancoAsync(banco.Id, ct))!;
    }
    public async Task<BancoDto?> EditarBancoAsync(int id, BancoEntrada entrada, CancellationToken ct = default)
    {
        var nombre = GestionBancosPagos.ValidarNombre(entrada.Nombre);
        if (await contexto.Bancos.AnyAsync(x => x.Id != id && x.Nombre == nombre, ct)) throw Duplicado();
        try
        {
            var n = await contexto.Bancos.Where(x => x.Id == id && x.Version == entrada.Version)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Nombre, nombre).SetProperty(x => x.Version, x => x.Version + 1), ct);
            if (n != 1 && await contexto.Bancos.AnyAsync(x => x.Id == id, ct))
                throw new ReglaPagoException("banco_modificado", "El banco cambió. Actualizá el detalle antes de guardar.", 409, "version");
            return n == 0 ? null : await BancoAsync(id, ct);
        }
        catch (Exception e) when (BloqueosPagos.Traducir(e) is not null) { throw BloqueosPagos.Traducir(e)!; }
    }
    public async Task<BancoDto?> ActivarBancoAsync(int id, bool activo, CancellationToken ct = default)
    {
        try
        {
            await contexto.Bancos.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, activo), ct);
            return await BancoAsync(id, ct);
        }
        catch (Exception e) when (BloqueosPagos.Traducir(e) is not null) { throw BloqueosPagos.Traducir(e)!; }
    }
    private static ReglaPagoException Duplicado() => new("nombre_banco_duplicado", "Ya existe un banco con ese nombre.", campo: "nombre");
}
