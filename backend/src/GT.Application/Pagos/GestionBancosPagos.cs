using GT.Domain.Pagos;

namespace GT.Application.Pagos;

public class GestionBancosPagos(IRepositorioPagos repositorio)
{
    public static string ValidarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) throw new ReglaPagoException("nombre_requerido", "Escribí el nombre del banco.", campo: "nombre");
        return ReglasDePagos.NormalizarNombreBanco(nombre)
            ?? throw new ReglaPagoException("longitud_excedida", "No puede superar 200 caracteres después de quitar los espacios exteriores.", campo: "nombre");
    }
    public Task<BancoDto> CrearAsync(BancoEntrada entrada, CancellationToken ct) =>
        repositorio.CrearBancoAsync(entrada with { Nombre = ValidarNombre(entrada.Nombre) }, ct);
    public Task<BancoDto?> EditarAsync(int id, BancoEntrada entrada, CancellationToken ct) =>
        repositorio.EditarBancoAsync(id, entrada with { Nombre = ValidarNombre(entrada.Nombre) }, ct);
}
