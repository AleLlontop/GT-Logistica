using GT.Application.Choferes.Transportistas;
using GT.Application.Flota.TiposVehiculo;
using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota;

/// <summary>
/// Modificación de una unidad (FR-008c, FR-014a).
///
/// Cubre tres cosas en una: la corrección de datos, el cambio de estado operativo y la
/// <b>reasignación a otro transportista</b>, que no toca la documentación ya cargada (SC-003c). No
/// hace falta que la toque: los documentos cuelgan del vehículo, no del transportista.
///
/// La unicidad de la patente <b>excluye al propio vehículo</b>: conservar la suya no es un conflicto
/// (FR-002).
/// </summary>
public class ModificarVehiculo(
    IRepositorioVehiculos vehiculos,
    IRepositorioTiposVehiculo tipos,
    IRepositorioTransportistas transportistas)
{
    public async Task<ResultadoVehiculo> EjecutarAsync(
        int id,
        VehiculoRequest peticion,
        CancellationToken cancelacion = default)
    {
        if (ValidadorVehiculo.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoVehiculo(ErrorVehiculo.DatosInvalidos, Campo: invalido);
        }

        var vehiculo = await vehiculos.ObtenerParaModificarAsync(id, cancelacion);
        if (vehiculo is null)
        {
            return new ResultadoVehiculo(ErrorVehiculo.NoEncontrado);
        }

        var patente = NormalizadorPatente.Normalizar(peticion.Patente!);

        if (!ValidadorPatente.EsValida(patente))
        {
            return new ResultadoVehiculo(ErrorVehiculo.PatenteInvalida, Campo: "patente");
        }

        // Excluye al propio registro: conservar la propia patente tiene que poder guardarse (FR-002).
        var otra = await vehiculos.ObtenerPorPatenteAsync(patente, id, cancelacion);
        if (otra is not null)
        {
            return new ResultadoVehiculo(
                otra.Activo
                    ? ErrorVehiculo.PatenteDuplicada
                    : ErrorVehiculo.PatenteDeVehiculoDadoDeBaja,
                Campo: "patente");
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

        var estado = NombresDeEstadoFlota.LeerEstadoOperativo(peticion.EstadoOperativo)!.Value;
        var hoy = FechaHoyArgentina.Hoy();

        // FR-014a: dejar la unidad disponible se rechaza si la documentación está vencida o falta, y
        // el mensaje **nombra el documento que lo impide**. Es una validación de formulario y no la
        // derivación de FR-014: ésta explica el motivo en el momento, aquélla cubre el paso del
        // tiempo (research §4).
        if (estado is VehiculoEstado.Disponible &&
            RechazoPorDocumentacion(vehiculo, hoy) is { } rechazo)
        {
            return rechazo;
        }

        vehiculo.Patente = patente;
        vehiculo.Marca = peticion.Marca!.Trim();
        vehiculo.Modelo = peticion.Modelo!.Trim();
        vehiculo.TipoVehiculoId = tipo.Id;
        vehiculo.TransportistaId = transportista.Id;
        vehiculo.EstadoOperativo = estado;

        try
        {
            await vehiculos.GuardarCambiosAsync(cancelacion);
        }
        catch (PatenteDuplicadaException)
        {
            return new ResultadoVehiculo(ErrorVehiculo.PatenteDuplicada, Campo: "patente");
        }

        vehiculo.Tipo = tipo;
        vehiculo.Transportista = transportista;

        return new ResultadoVehiculo(ErrorVehiculo.Ninguno, VehiculoDetalle.Desde(vehiculo, hoy));
    }

    /// <summary>
    /// El rechazo de FR-014a, o <c>null</c> si la documentación permite dejar la unidad disponible.
    ///
    /// Se apoya en <see cref="CalculadorEstadoOperativo.ImpideEstarDisponible"/>, la misma condición
    /// que aplica la derivación al consultar: una sola definición para que las dos reglas no puedan
    /// separarse con el tiempo.
    /// </summary>
    private static ResultadoVehiculo? RechazoPorDocumentacion(Vehiculo vehiculo, DateOnly hoy)
    {
        var estadoDocumentacion = CalculadorEstadoVehiculo.Calcular(vehiculo.Documentacion, hoy);

        if (!CalculadorEstadoOperativo.ImpideEstarDisponible(estadoDocumentacion))
        {
            return null;
        }

        if (estadoDocumentacion is EstadoDocumentacionVehiculo.SinDocumentacion)
        {
            return new ResultadoVehiculo(
                ErrorVehiculo.DisponibleSinDocumentacion,
                Campo: "estadoOperativo");
        }

        var vencido = CalculadorEstadoVehiculo
            .VigentesDeCadaTipo(vehiculo.Documentacion)
            .Where(documento => CalculadorEstadoDocumento.Calcular(
                documento.FechaVencimiento,
                documento.Tipo!.DiasAvisoVencimiento,
                hoy) is DocumentacionEstado.Vencida)
            // El que venció hace más tiempo, para nombrar siempre el mismo entre dos consultas
            // iguales cuando hay más de uno.
            .OrderBy(documento => documento.FechaVencimiento)
            .ThenBy(documento => documento.Id)
            .First();

        return new ResultadoVehiculo(
            ErrorVehiculo.DisponibleConDocumentacionVencida,
            Campo: "estadoOperativo",
            DocumentoQueImpide: vencido.Tipo!.Nombre);
    }
}
