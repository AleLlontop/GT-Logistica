using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

/// <summary>Modificación de un transportista. Conservar el propio CUIT no genera conflicto (FR-003).</summary>
public class ModificarTransportista(IRepositorioTransportistas repositorio)
{
    public async Task<ResultadoTransportista> EjecutarAsync(
        int id,
        TransportistaRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorTransportista.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTransportista(ErrorTransportista.DatosInvalidos, null, invalido);
        }

        var transportista = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (transportista is null)
        {
            return new ResultadoTransportista(ErrorTransportista.NoEncontrado, null);
        }

        var cuitNormalizado = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuit!);

        if (!ValidadorCuit.EsValido(cuitNormalizado))
        {
            return new ResultadoTransportista(ErrorTransportista.DatosInvalidos, null, "cuit");
        }

        // La unicidad excluye al propio registro (FR-003).
        if (await repositorio.ExisteCuitAsync(cuitNormalizado, id, cancelacion))
        {
            return new ResultadoTransportista(ErrorTransportista.CuitDuplicado, null, "cuit");
        }

        transportista.Nombre = peticion.Nombre!.Trim();
        transportista.Cuit = cuitNormalizado;
        transportista.Tipo = Enum.Parse<TipoPersona>(peticion.Tipo!, true);
        transportista.Telefono = peticion.Telefono!.Trim();
        transportista.Email = peticion.Email!.Trim();

        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (CuitDuplicadoException)
        {
            return new ResultadoTransportista(ErrorTransportista.CuitDuplicado, null, "cuit");
        }

        var fila = await repositorio.ObtenerConDependenciasActivasAsync(id, cancelacion);

        return new ResultadoTransportista(
            ErrorTransportista.Ninguno,
            TransportistaDto.Desde(fila!));
    }
}

/// <summary>
/// Baja lógica de un transportista (FR-010, y desde el Módulo 4 también FR-008d).
///
/// Se rechaza si tiene al menos un chofer <b>activo</b> <b>o</b> al menos un vehículo <b>activo</b>,
/// informando <b>las dos cantidades</b>: dejarlo dar de baja dejaría dependientes activos colgando de
/// un transportista inactivo, que es lo mismo que FR-008 —y FR-008a en la flota— no admiten al darlos
/// de alta. La baja procede si todos están inactivos o si no tiene ninguno.
///
/// <b>La asimetría con los catálogos es deliberada</b> y no hay que "arreglarla": acá se miran sólo
/// los dependientes <i>activos</i>, mientras que el tipo de vehículo (FR-010 del Módulo 4) y el de
/// documentación (FR-017b) se rechazan por dependientes cualesquiera. Un vehículo dado de baja sigue
/// mostrando su tipo y un documento histórico sigue necesitando los días de aviso del suyo, pero un
/// transportista inactivo no le hace falta a nadie que ya esté de baja (research §8).
///
/// G&amp;T Logística S.A. no recibe trato especial: se le aplica la misma regla.
/// </summary>
public class DarDeBajaTransportista(IRepositorioTransportistas repositorio)
{
    public async Task<ResultadoTransportista> EjecutarAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var fila = await repositorio.ObtenerConDependenciasActivasAsync(id, cancelacion);
        if (fila is null)
        {
            return new ResultadoTransportista(ErrorTransportista.NoEncontrado, null);
        }

        if (fila.ChoferesActivos > 0 || fila.VehiculosActivos > 0)
        {
            return new ResultadoTransportista(ErrorTransportista.ConChoferes, null)
            {
                CantidadChoferes = fila.ChoferesActivos,
                CantidadVehiculos = fila.VehiculosActivos,
            };
        }

        var transportista = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        transportista!.Activo = false;

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoTransportista(
            ErrorTransportista.Ninguno,
            TransportistaDto.Desde(transportista));
    }
}
