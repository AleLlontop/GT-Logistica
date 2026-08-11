using GT.Domain.Choferes;

namespace GT.Application.Viajes.Clientes;

/// <summary>
/// Corrección de los datos de un cliente (FR-003, FR-007).
///
/// Aplica <b>las mismas validaciones que el alta</b>, con la única diferencia de que la comparación
/// de CUIT excluye al propio cliente: conservar el suyo no genera conflicto (US1 esc. 5).
///
/// <b>No acepta <c>activo</c> en el cuerpo</b>: dar de baja y dar de alta son recursos propios, así
/// que corregir una razón social no puede reactivar en silencio a alguien que estaba dado de baja
/// (FR-007, precedente [004]).
/// </summary>
public class ModificarCliente(IRepositorioClientes clientes)
{
    public async Task<ResultadoCliente> EjecutarAsync(
        int id,
        ClienteRequest peticion,
        CancellationToken cancelacion = default)
    {
        var cliente = await clientes.ObtenerParaModificarAsync(id, cancelacion);

        if (cliente is null)
        {
            return new ResultadoCliente(ErrorCliente.NoEncontrado);
        }

        if (ValidadorCliente.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoCliente(invalido.Error, Campo: invalido.Campo);
        }

        var cuit = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuit);

        if (!ValidadorCuit.EsValido(cuit))
        {
            return new ResultadoCliente(ErrorCliente.CuitInvalido, Campo: "cuit");
        }

        var otroDueño = await clientes.ObtenerPorCuitAsync(cuit, id, cancelacion);

        if (otroDueño is not null)
        {
            return otroDueño.Activo
                ? new ResultadoCliente(ErrorCliente.CuitDuplicado, Campo: "cuit")
                : new ResultadoCliente(ErrorCliente.CuitDeClienteDadoDeBaja, Campo: "cuit");
        }

        cliente.RazonSocial = peticion.RazonSocial!.Trim();
        cliente.Cuit = cuit;
        cliente.Telefono = peticion.Telefono!.Trim();
        cliente.Email = peticion.Email!.Trim();
        cliente.Direccion = string.IsNullOrWhiteSpace(peticion.Direccion)
            ? null
            : peticion.Direccion.Trim();

        try
        {
            await clientes.GuardarCambiosAsync(cancelacion);
        }
        catch (CuitDeClienteDuplicadoException)
        {
            return new ResultadoCliente(ErrorCliente.CuitDuplicado, Campo: "cuit");
        }

        return new ResultadoCliente(ErrorCliente.Ninguno, ClienteDto.Desde(cliente));
    }
}
