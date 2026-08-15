using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// El punto de partida que comparten los tests de asignación y de ciclo de vida: un cliente, un
/// transportista, un chofer con su documentación y un vehículo disponible con la suya.
///
/// Se arma contra la base y no por la API para que cada test verifique sólo lo suyo: acá no se está
/// probando el alta de choferes ni la carga de documentación, que tienen sus propios tests en los
/// Módulos 3 y 4.
/// </summary>
public record EscenarioDeAsignacion(
    int ClienteId,
    int TransportistaId,
    int ChoferId,
    int VehiculoId,
    string Patente,
    string NombreDelChofer);

public static class ArmadoDeEscenarios
{
    /// <param name="diasDelDocumentoDelChofer">
    /// Días desde hoy hasta el vencimiento de su único documento. Negativo lo deja vencido;
    /// <c>null</c> lo deja <b>sin ningún documento</b>, que es un caso válido y habilita (FR-024).
    /// </param>
    /// <param name="diasAviso">
    /// Ventana de aviso del tipo. En cero no hay estado intermedio: el documento es vigente hasta su
    /// fecha inclusive y vencido al día siguiente.
    /// </param>
    public static async Task<EscenarioDeAsignacion> ArmarEscenarioAsync(
        this AplicacionDePrueba app,
        int? diasDelDocumentoDelChofer = 400,
        int? diasDelDocumentoDelVehiculo = 400,
        int diasAviso = 0,
        bool choferActivo = true,
        bool vehiculoActivo = true,
        VehiculoEstado estadoDelVehiculo = VehiculoEstado.Disponible,
        int? transportistaId = null)
    {
        var padron = await app.CrearClienteAsync();

        var idTransportista = transportistaId ?? (await app.CrearTransportistaAsync()).Id;

        var chofer = await app.CrearChoferCompletoAsync(
            semilla: DatosDePruebaViajes.SemillaUnica(),
            activo: choferActivo,
            transportistaId: idTransportista);

        if (diasDelDocumentoDelChofer is { } diasChofer)
        {
            var tipoChofer = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: diasAviso);
            await app.CrearDocumentoAsync(chofer.Id, tipoChofer.Id, diasChofer);
        }

        var tipoVehiculo = await app.CrearTipoVehiculoAsync();

        var vehiculo = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            idTransportista,
            estadoOperativo: estadoDelVehiculo,
            activo: vehiculoActivo);

        if (diasDelDocumentoDelVehiculo is { } diasVehiculo)
        {
            var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
                diasAvisoVencimiento: diasAviso);

            await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, diasVehiculo);
        }

        var nombre = await app.ConAlcanceAsync(async contexto =>
        {
            var persona = await contexto.Personas.FindAsync(chofer.PersonaId);
            return persona!.NombreCompleto;
        });

        return new EscenarioDeAsignacion(
            padron.Id,
            idTransportista,
            chofer.Id,
            vehiculo.Id,
            vehiculo.Patente,
            nombre);
    }

    /// <summary>
    /// Un viaje del escenario, en el estado y la fecha que el test necesite.
    /// </summary>
    /// <param name="numeroRemito">
    /// <b>Trae uno único por defecto, y hace falta desde el Módulo 6</b>: FR-055a volvió el remito
    /// obligatorio para rendir, así que un viaje del escenario sin remito no puede recorrer el ciclo de
    /// vida completo. Los tests que verifican la regla del remito pasan el valor explícitamente —o
    /// <c>null</c>— en vez de depender de este default.
    /// </param>
    public static Task<Viaje> CrearViajeDelEscenarioAsync(
        this AplicacionDePrueba app,
        EscenarioDeAsignacion escenario,
        DateOnly? fecha = null,
        EstadoViaje estado = EstadoViaje.Pendiente,
        bool asignado = false,
        decimal importe = 0m,
        string? numeroRemito = null) =>
        app.CrearViajeAsync(
            escenario.ClienteId,
            fecha: fecha ?? FechaHoyArgentina.Hoy(),
            estado: estado,
            importe: importe,
            numeroRemito: numeroRemito ?? RemitoUnico(),
            choferId: asignado ? escenario.ChoferId : null,
            vehiculoId: asignado ? escenario.VehiculoId : null,
            transportistaId: asignado ? escenario.TransportistaId : null);

    /// <summary>
    /// Un remito distinto en cada llamada: es único entre los viajes no anulados, así que dos viajes de
    /// prueba con el mismo número chocarían contra el índice (FR-014).
    /// </summary>
    public static string RemitoUnico() => $"R-{DatosDePruebaViajes.SemillaUnica()}";
}
