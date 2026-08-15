using GT.Domain.Choferes;
using Entidad = GT.Domain.Facturacion.EmpresaEmisora;

namespace GT.Application.Facturacion.EmpresaEmisora;

/// <summary>
/// Guarda los datos del emisor (FR-002).
///
/// <b>Crea la fila la primera vez y la actualiza siempre después.</b> No hay alta ni baja: la
/// configuración es única para todo el sistema, y modelarla con un <c>POST</c> aparte obligaría a la
/// pantalla a saber si ya existe para elegir el método (research §12).
///
/// <b>El orden de los pasos importa</b>, igual que con el CUIT del cliente y la patente del vehículo:
/// se normaliza primero y recién después se valida. Al revés, <c>30-71234567-8</c> sería rechazado por
/// formato en vez de aceptado y guardado como <c>30712345670</c> (FR-002).
///
/// <b>No toca el logo</b>: tiene sus recursos propios, así que corregir un teléfono no puede borrarlo
/// en silencio. Es el mismo criterio con el que el Módulo 4 sacó el estado del <c>PUT</c> de edición
/// (precedente [004]).
/// </summary>
public class GuardarEmpresaEmisora(IRepositorioEmpresaEmisora empresas)
{
    public async Task<ResultadoEmpresaEmisora> EjecutarAsync(
        EmpresaEmisoraRequest peticion,
        CancellationToken cancelacion = default)
    {
        var cuit = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuit);

        if (ValidadorEmpresaEmisora.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoEmpresaEmisora(
                invalido.Error,
                Campo: invalido.Campo,
                Mensaje: invalido.Mensaje);
        }

        if (!ValidadorCuit.EsValido(cuit))
        {
            return new ResultadoEmpresaEmisora(ErrorFactura.CuitInvalido, Campo: "cuit");
        }

        var empresa = await empresas.ObtenerParaModificarAsync(cancelacion);
        var esNueva = empresa is null;

        empresa ??= new Entidad
        {
            Id = Entidad.IdUnico,
            RazonSocial = string.Empty,
            Cuit = string.Empty,
            Domicilio = string.Empty,
            CondicionIva = string.Empty,
        };

        // `Trim` al guardar en los diez campos (FR-002): lo que sale impreso en un comprobante no puede
        // arrastrar espacios de sobra, y limpiarlos al mostrar sería limpiarlos en cada pantalla.
        empresa.RazonSocial = peticion.RazonSocial!.Trim();
        empresa.Cuit = cuit;
        empresa.Domicilio = peticion.Domicilio!.Trim();
        empresa.CondicionIva = peticion.CondicionIva!.Trim();
        empresa.IngresosBrutos = Opcional(peticion.IngresosBrutos);
        empresa.InicioActividades = peticion.InicioActividades;
        empresa.PuntoDeVenta = Opcional(peticion.PuntoDeVenta);
        empresa.Cbu = Opcional(peticion.Cbu);
        empresa.Telefono = Opcional(peticion.Telefono);
        empresa.Email = Opcional(peticion.Email);

        if (esNueva)
        {
            await empresas.AgregarAsync(empresa, cancelacion);
        }

        await empresas.GuardarAsync(cancelacion);

        return new ResultadoEmpresaEmisora(
            ErrorFactura.Ninguno,
            EmpresaEmisoraDto.Desde(empresa),
            Mensaje: MensajesFacturas.EmpresaEmisoraGuardada);
    }

    /// <summary>
    /// Un opcional vacío se guarda como <c>null</c> y no como cadena vacía. La diferencia se ve en el
    /// documento: la banda de CBU se omite cuando el CBU está vacío, y <c>""</c> no es lo mismo que
    /// nada si en algún lado se compara contra <c>null</c> (FR-031).
    /// </summary>
    private static string? Opcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Las validaciones de campo de FR-002.
///
/// Los cuatro obligatorios se nombran uno por uno en el mensaje —<c>Completá el domicilio para poder
/// guardar.</c>— en vez de mandar un "revisá los campos marcados" genérico: son cuatro campos de un
/// formulario de diez, y decir cuál falta ahorra buscarlo (contracts/README §Empresa emisora).
/// </summary>
public static class ValidadorEmpresaEmisora
{
    public static (ErrorFactura Error, string Campo, string? Mensaje)? PrimerCampoInvalido(
        EmpresaEmisoraRequest peticion)
    {
        if (Falta(peticion.RazonSocial, 200))
        {
            return Obligatorio("razonSocial", Entidad.NombresDeCampo.RazonSocial);
        }

        if (string.IsNullOrWhiteSpace(peticion.Cuit))
        {
            return Obligatorio("cuit", Entidad.NombresDeCampo.Cuit);
        }

        if (Falta(peticion.Domicilio, 200))
        {
            return Obligatorio("domicilio", Entidad.NombresDeCampo.Domicilio);
        }

        if (Falta(peticion.CondicionIva, 100))
        {
            return Obligatorio("condicionIva", Entidad.NombresDeCampo.CondicionIva);
        }

        if (Excede(peticion.IngresosBrutos, 50))
        {
            return Invalido("ingresosBrutos");
        }

        // Cuatro dígitos: es el punto de venta que después arma el número de comprobante (FR-027).
        if (peticion.PuntoDeVenta is { } punto &&
            !string.IsNullOrWhiteSpace(punto) &&
            (punto.Trim().Length != 4 || !punto.Trim().All(char.IsAsciiDigit)))
        {
            return Invalido("puntoDeVenta");
        }

        if (Excede(peticion.Cbu, 22))
        {
            return Invalido("cbu");
        }

        if (Excede(peticion.Telefono, 50))
        {
            return Invalido("telefono");
        }

        if (Excede(peticion.Email, 254))
        {
            return Invalido("email");
        }

        // El email tiene código propio: "revisá los campos marcados" no explica que el problema es el
        // formato y no que esté vacío (contracts/README). Es opcional, así que vacío no es un error.
        if (!string.IsNullOrWhiteSpace(peticion.Email) && !peticion.Email.Contains('@'))
        {
            return (ErrorFactura.EmailInvalido, "email", MensajesFacturas.EmailInvalido);
        }

        return null;
    }

    private static bool Falta(string? valor, int maximo) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim().Length > maximo;

    private static bool Excede(string? valor, int maximo) =>
        valor is not null && valor.Trim().Length > maximo;

    private static (ErrorFactura, string, string?) Obligatorio(string campo, string nombreVisible) =>
        (ErrorFactura.DatosInvalidos, campo, MensajesFacturas.ObligatorioVacio(nombreVisible));

    private static (ErrorFactura, string, string?) Invalido(string campo) =>
        (ErrorFactura.DatosInvalidos, campo, MensajesFacturas.DatosInvalidos);
}
