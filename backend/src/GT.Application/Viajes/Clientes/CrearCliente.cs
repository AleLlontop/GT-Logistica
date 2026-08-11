using GT.Domain.Choferes;
using GT.Domain.Viajes;

namespace GT.Application.Viajes.Clientes;

/// <summary>
/// Alta de un cliente en el padrón (FR-001 a FR-004, FR-007).
///
/// <b>El orden de los pasos importa</b>, igual que con la patente del Módulo 4: el CUIT se normaliza
/// primero y recién después se valida y se compara. Si se validara antes de normalizar,
/// <c>30-71234567-8</c> sería rechazado por formato en vez de aceptado; si se comparara antes,
/// <c>30-71234567-8</c> y <c>30712345678</c> convivirían como dos clientes distintos (FR-004).
///
/// <b>Reutiliza <c>ValidadorCuit</c> y <c>NormalizadorDocumentoNumerico</c> del Módulo 3 sin
/// modificarlos</b>: son reglas sobre once dígitos, no saben de transportistas (research §13).
/// </summary>
public class CrearCliente(IRepositorioClientes clientes)
{
    public async Task<ResultadoCliente> EjecutarAsync(
        ClienteRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorCliente.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoCliente(invalido.Error, Campo: invalido.Campo);
        }

        var cuit = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuit);

        if (!ValidadorCuit.EsValido(cuit))
        {
            return new ResultadoCliente(ErrorCliente.CuitInvalido, Campo: "cuit");
        }

        if (await CuitOcupadoAsync(cuit, null, cancelacion) is { } ocupado)
        {
            return ocupado;
        }

        var cliente = new Cliente
        {
            RazonSocial = peticion.RazonSocial!.Trim(),
            Cuit = cuit,
            Telefono = peticion.Telefono!.Trim(),
            Email = peticion.Email!.Trim(),
            Direccion = string.IsNullOrWhiteSpace(peticion.Direccion)
                ? null
                : peticion.Direccion.Trim(),
            Activo = true,
        };

        await clientes.AgregarAsync(cliente, cancelacion);

        try
        {
            await clientes.GuardarCambiosAsync(cancelacion);
        }
        catch (CuitDeClienteDuplicadoException)
        {
            // Dos altas simultáneas del mismo CUIT: la consulta previa las dejó pasar a las dos y el
            // índice cortó la segunda. Se vuelve a mirar quién quedó dueño para responder con el
            // rechazo que corresponde, que no es el mismo si el dueño está dado de baja.
            return await CuitOcupadoAsync(cuit, null, cancelacion)
                ?? new ResultadoCliente(ErrorCliente.CuitDuplicado, Campo: "cuit");
        }

        return new ResultadoCliente(ErrorCliente.Ninguno, ClienteDto.Desde(cliente));
    }

    /// <summary>
    /// El rechazo que corresponde si el CUIT ya está tomado, o <c>null</c> si está libre.
    ///
    /// Distingue los dos casos (FR-007): sin esa distinción, quien intenta registrar de nuevo a un
    /// cliente que volvió recibe "ya pertenece a otro" y no lo encuentra, porque un cliente dado de
    /// baja no aparece en el listado por defecto.
    /// </summary>
    private async Task<ResultadoCliente?> CuitOcupadoAsync(
        string cuit,
        int? idAExcluir,
        CancellationToken cancelacion)
    {
        var dueño = await clientes.ObtenerPorCuitAsync(cuit, idAExcluir, cancelacion);

        if (dueño is null)
        {
            return null;
        }

        return dueño.Activo
            ? new ResultadoCliente(ErrorCliente.CuitDuplicado, Campo: "cuit")
            : new ResultadoCliente(ErrorCliente.CuitDeClienteDadoDeBaja, Campo: "cuit");
    }
}

/// <summary>
/// Las validaciones de campo que comparten el alta y la edición (FR-002, FR-017): "la edición aplica
/// las mismas validaciones que el alta" es un requisito, y una sola escritura es lo que lo garantiza.
/// </summary>
public static class ValidadorCliente
{
    public static (ErrorCliente Error, string Campo)? PrimerCampoInvalido(ClienteRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.RazonSocial) ||
            peticion.RazonSocial.Trim().Length > 100)
        {
            return (ErrorCliente.DatosInvalidos, "razonSocial");
        }

        if (string.IsNullOrWhiteSpace(peticion.Cuit))
        {
            return (ErrorCliente.DatosInvalidos, "cuit");
        }

        if (string.IsNullOrWhiteSpace(peticion.Telefono) || peticion.Telefono.Trim().Length > 30)
        {
            return (ErrorCliente.DatosInvalidos, "telefono");
        }

        // El email tiene código propio: "revisá los campos marcados" no explica que el problema es el
        // formato y no que esté vacío (contracts/README.md).
        if (string.IsNullOrWhiteSpace(peticion.Email) || peticion.Email.Trim().Length > 254)
        {
            return (ErrorCliente.DatosInvalidos, "email");
        }

        if (!peticion.Email.Contains('@'))
        {
            return (ErrorCliente.EmailInvalido, "email");
        }

        if (peticion.Direccion is { } direccion && direccion.Trim().Length > 200)
        {
            return (ErrorCliente.DatosInvalidos, "direccion");
        }

        return null;
    }
}
