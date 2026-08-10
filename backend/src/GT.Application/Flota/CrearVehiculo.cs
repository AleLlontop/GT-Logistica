using GT.Application.Choferes.Transportistas;
using GT.Application.Flota.TiposVehiculo;
using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota;

/// <summary>
/// Alta de una unidad en el padrón de flota (FR-001 a FR-006, FR-008a).
///
/// <b>El orden de los pasos importa</b> (research §6): la patente se normaliza primero y recién
/// después se valida el formato y se compara. Si se validara antes de normalizar, <c>AB-123-CD</c>
/// sería rechazada por formato en vez de aceptada; si se comparara antes, <c>ab 123 cd</c> y
/// <c>AB123CD</c> convivirían como dos unidades distintas.
///
/// <b>El alta sólo admite <c>fueraDeServicio</c></b>, y no es evidente al leer FR-012: sale de cruzar
/// FR-013 con FR-014a. Una unidad recién registrada no tiene documentos, así que su estado general es
/// <c>sinDocumentacion</c> y <c>disponible</c> queda rechazado (US2 esc. 8).
/// </summary>
public class CrearVehiculo(
    IRepositorioVehiculos vehiculos,
    IRepositorioTiposVehiculo tipos,
    IRepositorioTransportistas transportistas)
{
    public async Task<ResultadoVehiculo> EjecutarAsync(
        VehiculoRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorVehiculo.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoVehiculo(ErrorVehiculo.DatosInvalidos, Campo: invalido);
        }

        var patente = NormalizadorPatente.Normalizar(peticion.Patente!);

        if (!ValidadorPatente.EsValida(patente))
        {
            return new ResultadoVehiculo(ErrorVehiculo.PatenteInvalida, Campo: "patente");
        }

        var tipo = await tipos.ObtenerPorIdAsync(peticion.TipoVehiculoId!.Value, cancelacion);
        if (tipo is null || !tipo.Activo)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.TipoVehiculoInexistente,
                Campo: "tipoVehiculoId");
        }

        var transportista = await transportistas.ObtenerPorIdAsync(
            peticion.TransportistaId!.Value,
            cancelacion);

        if (transportista is null || !transportista.Activo)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.TransportistaInexistente,
                Campo: "transportistaId");
        }

        // FR-013 y FR-014a: la unidad todavía no tiene ningún documento, así que su estado general es
        // `sinDocumentacion` y no puede quedar disponible. El formulario ya lo dice de entrada, pero
        // el servidor lo verifica igual: la autorización y las reglas se evalúan acá, no en pantalla.
        var estado = NombresDeEstadoFlota.LeerEstadoOperativo(peticion.EstadoOperativo)!.Value;
        if (estado is VehiculoEstado.Disponible)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.DisponibleSinDocumentacion,
                Campo: "estadoOperativo");
        }

        if (await PatenteOcupadaAsync(patente, cancelacion) is { } ocupada)
        {
            return ocupada;
        }

        var vehiculo = new Vehiculo
        {
            Patente = patente,
            Marca = peticion.Marca!.Trim(),
            Modelo = peticion.Modelo!.Trim(),
            TipoVehiculoId = tipo.Id,
            TransportistaId = transportista.Id,
            EstadoOperativo = estado,
            Activo = true,
        };

        await vehiculos.AgregarAsync(vehiculo, cancelacion);

        try
        {
            await vehiculos.GuardarCambiosAsync(cancelacion);
        }
        catch (PatenteDuplicadaException)
        {
            // Dos altas simultáneas de la misma patente: la consulta previa las dejó pasar a las dos
            // y el índice cortó la segunda. Se vuelve a mirar quién quedó dueño para responder con el
            // mensaje que corresponde (research §6).
            return await PatenteOcupadaAsync(patente, cancelacion)
                ?? new ResultadoVehiculo(ErrorVehiculo.PatenteDuplicada, Campo: "patente");
        }

        vehiculo.Tipo = tipo;
        vehiculo.Transportista = transportista;

        return new ResultadoVehiculo(
            ErrorVehiculo.Ninguno,
            VehiculoDetalle.Desde(vehiculo, FechaHoyArgentina.Hoy()));
    }

    /// <summary>
    /// El rechazo que corresponde si la patente ya está tomada, o <c>null</c> si está libre.
    ///
    /// Distingue los dos casos (FR-008f): sin esa distinción, quien intenta recargar una unidad que
    /// volvió recibe "ya está registrada" y no la encuentra, porque un vehículo dado de baja no
    /// aparece en el listado por defecto.
    /// </summary>
    private async Task<ResultadoVehiculo?> PatenteOcupadaAsync(
        string patente,
        CancellationToken cancelacion)
    {
        var dueña = await vehiculos.ObtenerPorPatenteAsync(patente, cancelacion: cancelacion);

        if (dueña is null)
        {
            return null;
        }

        return dueña.Activo
            ? new ResultadoVehiculo(ErrorVehiculo.PatenteDuplicada, Campo: "patente")
            : new ResultadoVehiculo(ErrorVehiculo.PatenteDeVehiculoDadoDeBaja, Campo: "patente");
    }
}

public static class ValidadorVehiculo
{
    public static string? PrimerCampoInvalido(VehiculoRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Patente)) return "patente";
        if (string.IsNullOrWhiteSpace(peticion.Marca)) return "marca";
        if (peticion.Marca.Trim().Length > 50) return "marca";
        if (string.IsNullOrWhiteSpace(peticion.Modelo)) return "modelo";
        if (peticion.Modelo.Trim().Length > 50) return "modelo";
        if (peticion.TipoVehiculoId is null or <= 0) return "tipoVehiculoId";
        if (peticion.TransportistaId is null or <= 0) return "transportistaId";
        if (NombresDeEstadoFlota.LeerEstadoOperativo(peticion.EstadoOperativo) is null)
        {
            return "estadoOperativo";
        }

        return null;
    }
}
