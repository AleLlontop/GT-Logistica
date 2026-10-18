using System.Text.RegularExpressions;
using GT.Domain.Choferes;

namespace GT.Application.Proveedores;

public static partial class ReglasProveedor
{
    public static ProveedorPeticion Normalizar(ProveedorPeticion datos, bool edicion = false)
    {
        if (datos.Tipo is not ("fletero" or "proveedorNormal")) Invalido("tipo", "Elegí el tipo de proveedor.");
        if (edicion && datos.Version is not > 0) Invalido("version", "Volvé a abrir el proveedor antes de guardar.");
        var razon = Texto(datos.RazonSocial); var cuit = Texto(datos.Cuit);
        var telefono = Texto(datos.Telefono); var email = Texto(datos.Email);
        if (datos.Tipo == "fletero")
        {
            if (datos.TransportistaId is not > 0) Invalido("transportistaId", "Elegí un transportista externo.");
            if (razon is not null || cuit is not null || telefono is not null || email is not null)
                Invalido("transportistaId", "La identidad y el contacto del Fletero se mantienen en Transportistas.");
        }
        else
        {
            if (datos.TransportistaId is not null) Invalido("transportistaId", "Un proveedor normal no tiene transportista vinculado.");
            if (razon is null) Invalido("razonSocial", "Escribí la razón social.");
            cuit = NormalizadorDocumentoNumerico.Normalizar(cuit);
            if (!ValidadorCuit.EsValido(cuit)) Invalido("cuit", "Escribí un CUIT válido.");
            if (email is not null && !FormatoEmail().IsMatch(email)) Invalido("email", "Escribí un email válido, con formato nombre@dominio.");
        }
        Capacidad(razon, 200, "razonSocial"); Capacidad(telefono, 30, "telefono");
        Capacidad(email, 254, "email"); Capacidad(Texto(datos.Direccion), 200, "direccion");
        return datos with { RazonSocial = razon, Cuit = cuit, Telefono = telefono, Email = email,
            NombreComercial = Texto(datos.NombreComercial), Direccion = Texto(datos.Direccion) };
    }
    public static void ValidarFiltros(FiltrosProveedor filtros)
    {
        if (filtros.Pagina < 1 || filtros.Pagina > int.MaxValue / 20) Invalido("pagina", "La página no es válida.");
        if (!string.IsNullOrWhiteSpace(filtros.Tipo) && filtros.Tipo is not ("fletero" or "proveedorNormal")) Invalido("tipo", "El tipo no es válido.");
        if (!string.IsNullOrWhiteSpace(filtros.Estado) && filtros.Estado is not ("activo" or "inactivo")) Invalido("estado", "El estado no es válido.");
    }
    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    private static void Capacidad(string? valor, int maximo, string campo)
    {
        if (valor?.Length > maximo) Invalido(campo, $"Este campo admite hasta {maximo} caracteres.");
    }
    private static void Invalido(string campo, string mensaje) => throw new ProveedorException("datos_invalidos", mensaje, campo);
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex FormatoEmail();
}
