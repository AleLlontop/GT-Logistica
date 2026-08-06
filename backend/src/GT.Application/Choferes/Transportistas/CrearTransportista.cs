using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public class CrearTransportista(IRepositorioTransportistas repositorio)
{
    public async Task<ResultadoTransportista> EjecutarAsync(
        TransportistaRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorTransportista.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoTransportista(ErrorTransportista.DatosInvalidos, null, invalido);
        }

        var cuitNormalizado = NormalizadorDocumentoNumerico.Normalizar(peticion.Cuit!);

        if (!ValidadorCuit.EsValido(cuitNormalizado))
        {
            return new ResultadoTransportista(ErrorTransportista.DatosInvalidos, null, "cuit");
        }

        if (await repositorio.ExisteCuitAsync(cuitNormalizado, null, cancelacion))
        {
            return new ResultadoTransportista(ErrorTransportista.CuitDuplicado, null, "cuit");
        }

        var transportista = new Transportista
        {
            Nombre = peticion.Nombre!.Trim(),
            Cuit = cuitNormalizado,
            Tipo = Enum.Parse<TipoPersona>(peticion.Tipo!, true),
            Telefono = peticion.Telefono!.Trim(),
            Email = peticion.Email!.Trim(),
            Activo = true
        };

        await repositorio.AgregarAsync(transportista, cancelacion);

        // La consulta de arriba cierra la ventana normal; el índice único cierra la carrera entre
        // dos altas simultáneas del mismo CUIT, que ninguna consulta previa puede evitar (FR-003).
        try
        {
            await repositorio.GuardarCambiosAsync(cancelacion);
        }
        catch (CuitDuplicadoException)
        {
            return new ResultadoTransportista(ErrorTransportista.CuitDuplicado, null, "cuit");
        }

        // Recién creado: todavía no puede tener ningún chofer asignado.
        return new ResultadoTransportista(
            ErrorTransportista.Ninguno,
            TransportistaDto.Desde(transportista, choferesActivos: 0));
    }
}

/// <summary>
/// Violación del índice único del CUIT detectada al guardar. Existe para no filtrar tipos de EF Core
/// ni de SqlClient hacia la capa de aplicación, igual que <c>DniDuplicadoException</c> del Módulo 2.
/// </summary>
public class CuitDuplicadoException(Exception interna)
    : Exception("Ese CUIT ya está registrado para otro transportista.", interna);
