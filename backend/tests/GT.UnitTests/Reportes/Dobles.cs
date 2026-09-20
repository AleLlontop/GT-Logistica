using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Choferes.Transportistas;
using GT.Application.Facturacion;
using GT.Application.Flota;
using GT.Application.Flota.Documentacion;
using GT.Application.Reportes;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;
using GT.Domain.Caja;
using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using CajaEntidad = GT.Domain.Caja.Caja;

namespace GT.UnitTests.Reportes;

/// <summary>
/// Dobles de prueba para los tests de las cinco fuentes.
///
/// <b>Cada doble responde sólo lo que su fuente le pide y el resto lanza.</b> No es rigidez: una
/// fuente que llamara a otra consulta estaría haciendo algo que su tarea no dice, y así el test lo
/// grita en vez de devolver un valor por defecto que parece bien.
///
/// El proyecto no usa biblioteca de dobles y esto no la introduce: son clases comunes.
/// </summary>
internal static class Dobles
{
    /// <summary>Un armado con reloj fijo y un armador de PDF que captura el reporte recibido.</summary>
    public static (ArmadoDeReporte Armado, ArmadorCapturador Capturador) Armado(
        int tope = OpcionesDeReporte.TopePorDefecto)
    {
        var capturador = new ArmadorCapturador();

        var armado = new ArmadoDeReporte(
            capturador,
            capturador,
            new OpcionesDeReporte(tope),
            new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
                new DateTimeOffset(2026, 9, 20, 17, 32, 0, TimeSpan.Zero)));

        return (armado, capturador);
    }
}

/// <summary>Guarda el <see cref="ReporteTabular"/> que recibe, para poder afirmar sobre él.</summary>
internal sealed class ArmadorCapturador : IArmadorReportePdf, IArmadorReporteExcel
{
    public ReporteTabular? Ultimo { get; private set; }

    /// <summary>La fila de la tabla, sin contar el encabezado.</summary>
    public IReadOnlyList<CeldaDeReporte> Fila(int indice) => Ultimo!.Filas[indice];

    public IReadOnlyList<string> NombresDeColumna =>
        [.. Ultimo!.Columnas.Select(columna => columna.Nombre)];

    public byte[] Armar(ReporteTabular reporte)
    {
        Ultimo = reporte;

        return [1];
    }
}

// ── Viajes ──────────────────────────────────────────────────────────────────────────────────────

internal sealed class RepositorioViajesDoble(PaginaDe<ViajeListado> pagina) : IRepositorioViajes
{
    public int? TamanioPedido { get; private set; }

    public FiltrosDeViajes? FiltrosPedidos { get; private set; }

    public Task<PaginaDe<ViajeListado>> ConsultarAsync(
        FiltrosDeViajes filtros,
        MomentoDeLectura momento,
        int? tamanioPagina = null,
        CancellationToken cancelacion = default)
    {
        FiltrosPedidos = filtros;
        TamanioPedido = tamanioPagina;

        return Task.FromResult(pagina);
    }

    public Task AgregarAsync(Viaje viaje, CancellationToken cancelacion = default) => NoLlamado();

    public Task<Viaje?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) => NoLlamado<Viaje?>();

    public Task<Viaje?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default) => NoLlamado<Viaje?>();

    public Task<Viaje?> ObtenerPorRemitoAsync(string numeroRemito, int? idAExcluir = null, CancellationToken cancelacion = default) => NoLlamado<Viaje?>();

    public Task<IReadOnlyList<Chofer>> ConsultarChoferesAsignablesAsync(CancellationToken cancelacion = default) => NoLlamado<IReadOnlyList<Chofer>>();

    public Task<IReadOnlyList<Domain.Flota.Vehiculo>> ConsultarVehiculosAsignablesAsync(CancellationToken cancelacion = default) => NoLlamado<IReadOnlyList<Domain.Flota.Vehiculo>>();

    public Task<Chofer?> ObtenerChoferParaAsignarAsync(int id, CancellationToken cancelacion = default) => NoLlamado<Chofer?>();

    public Task<Domain.Flota.Vehiculo?> ObtenerVehiculoParaAsignarAsync(int id, CancellationToken cancelacion = default) => NoLlamado<Domain.Flota.Vehiculo?>();

    public Task<Viaje?> ViajeEnCursoDelChoferAsync(int choferId, int viajeAExcluir, CancellationToken cancelacion = default) => NoLlamado<Viaje?>();

    public Task<Viaje?> ViajeEnCursoDelVehiculoAsync(int vehiculoId, int viajeAExcluir, CancellationToken cancelacion = default) => NoLlamado<Viaje?>();

    public Task RegistrarCambioDeEstadoAsync(Viaje viaje, EstadoViaje estadoNuevo, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default) => NoLlamado();

    public Task<TotalesDelPeriodo> ConsultarTotalesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default) => NoLlamado<TotalesDelPeriodo>();

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) => NoLlamado();

    private static Task NoLlamado() => throw new NotSupportedException(Doble.Mensaje);

    private static Task<T> NoLlamado<T>() => throw new NotSupportedException(Doble.Mensaje);
}

internal sealed class RepositorioClientesDoble(Dictionary<int, string> porId) : IRepositorioClientes
{
    public Task<Cliente?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) =>
        Task.FromResult<Cliente?>(porId.TryGetValue(id, out var razonSocial)
            ? new Cliente
            {
                RazonSocial = razonSocial,
                Cuit = "30712345678",
                Telefono = "",
                Email = "",
            }
            : null);

    public Task AgregarAsync(Cliente cliente, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<PaginaDe<Cliente>> ConsultarAsync(FiltrosDeClientes filtros, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<Cliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<Cliente?> ObtenerPorCuitAsync(string cuitNormalizado, int? idAExcluir = null, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<int> ContarViajesVivosAsync(int clienteId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);
}

internal sealed class RepositorioTransportistasDoble(Dictionary<int, string> porId) : IRepositorioTransportistas
{
    public Task<Transportista?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) =>
        Task.FromResult<Transportista?>(porId.TryGetValue(id, out var nombre)
            ? new Transportista
            {
                Nombre = nombre,
                Cuit = "30712345678",
                Tipo = TipoPersona.Juridica,
                Telefono = "",
                Email = "",
            }
            : null);

    public Task<bool> ExisteCuitAsync(string cuitNormalizado, int? idAExcluir, CancellationToken cancelacion) => throw new NotSupportedException(Doble.Mensaje);

    public Task AgregarAsync(Transportista transportista, CancellationToken cancelacion) => throw new NotSupportedException(Doble.Mensaje);

    public Task GuardarCambiosAsync(CancellationToken cancelacion) => throw new NotSupportedException(Doble.Mensaje);

    public Task<List<TransportistaConDependenciasActivas>> ConsultarAsync(string? textoBusqueda, string? cuitNormalizado, bool soloActivos, CancellationToken cancelacion) => throw new NotSupportedException(Doble.Mensaje);

    public Task<TransportistaConDependenciasActivas?> ObtenerConDependenciasActivasAsync(int id, CancellationToken cancelacion) => throw new NotSupportedException(Doble.Mensaje);
}

// ── Vencimientos de choferes ────────────────────────────────────────────────────────────────────

internal sealed class RepositorioDocumentacionDoble(List<Documentacion> vigentes) : IRepositorioDocumentacion
{
    public Task<List<Documentacion>> ConsultarVigentesDeChoferesActivosAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(vigentes);

    public Task<Documentacion?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<List<Documentacion>> ConsultarDelChoferAsync(int choferId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<bool> ExisteChoferAsync(int choferId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<DocumentacionTipo?> ObtenerTipoActivoAsync(int tipoId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task AgregarAsync(Documentacion documento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task EliminarAsync(Documentacion documento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);
}

// ── Vencimientos de flota ───────────────────────────────────────────────────────────────────────

internal sealed class RepositorioDocumentacionVehiculoDoble(List<DocumentacionVehiculo> vigentes)
    : IRepositorioDocumentacionVehiculo
{
    public Task<List<DocumentacionVehiculo>> ConsultarVigentesDeVehiculosActivosAsync(CancellationToken cancelacion = default) =>
        Task.FromResult(vigentes);

    public Task<DocumentacionVehiculo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<List<DocumentacionVehiculo>> ConsultarDelVehiculoAsync(int vehiculoId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<bool> ExisteVehiculoAsync(int vehiculoId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<DocumentacionTipo?> ObtenerTipoActivoDeVehiculoAsync(int tipoId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task AgregarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task EliminarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);
}

// ── Vencimientos de facturas ────────────────────────────────────────────────────────────────────

internal sealed class RepositorioFacturasDoble(IReadOnlyList<FilaDeVencimiento> vencimientos) : IRepositorioFacturas
{
    public Task<IReadOnlyList<FilaDeVencimiento>> ConsultarVencimientosAsync(DateOnly hoy, CancellationToken cancelacion = default) =>
        Task.FromResult(vencimientos);

    public Task<IReadOnlyList<Viaje>> ConsultarFacturablesAsync(int clienteId, int mes, int anio, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(IReadOnlyList<int> ids, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<FacturaCliente?> ObtenerPorNumeroAsync(string numeroComprobante, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<IReadOnlyList<FacturaCliente>> ConsultarAnuladasSinReemplazoAsync(int clienteId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<PaginaDe<FacturaListado>> ConsultarAsync(FiltrosDeFacturas filtros, DateOnly hoy, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<FacturaCliente?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<FacturaCliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<FacturaCliente?> ObtenerQueLaReemplazaAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<ResultadoDeEmision> EmitirAsync(FacturaCliente factura, IReadOnlyList<int> viajeIds, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task CorregirAsync(FacturaCliente factura, int usuarioId, DateTime ocurridoEn, Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task AnularAsync(FacturaCliente factura, string motivo, int usuarioId, DateTime ocurridoEn, Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task RegistrarCobroAsync(FacturaCliente factura, DateOnly fechaCobro, int usuarioId, DateTime ocurridoEn, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<IReadOnlyList<TotalPorCliente>> ConsultarTotalesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);
}

// ── Movimientos de caja ─────────────────────────────────────────────────────────────────────────

internal sealed class RepositorioCajaDoble(PaginaDe<MovimientoListado> pagina, CajaDetalle? detalle = null)
    : IRepositorioCaja
{
    public int? TamanioPedido { get; private set; }

    public Task<PaginaDe<MovimientoListado>> ConsultarMovimientosAsync(
        DateTime? desde,
        DateTime? hastaExcluido,
        int? cajaId,
        int paginaPedida,
        int? tamanioPagina = null,
        CancellationToken cancelacion = default)
    {
        TamanioPedido = tamanioPagina;

        return Task.FromResult(pagina);
    }

    public Task<CajaDetalle?> ObtenerDetalleAsync(int id, int usuarioId, CancellationToken cancelacion = default) =>
        Task.FromResult(detalle);

    public Task<CajaDetalle?> ObtenerCajaAbiertaDeAsync(int usuarioId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<PaginaDe<CajaListado>> ConsultarCajasAsync(int paginaPedida, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<MotivoNoOperable?> MotivoNoOperableAsync(int id, int usuarioId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<bool> ExisteCajaAbiertaAsync(int usuarioId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<int> AbrirAsync(CajaEntidad caja, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<MovimientoRegistrado> RegistrarMovimientoAsync(MovimientoDeCaja movimiento, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<MovimientoListado?> ObtenerMovimientoAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<PaginaDe<MovimientoListado>> ConsultarMovimientosDeCajaAsync(int cajaId, int paginaPedida, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<IReadOnlyList<OpcionDeReferencia>> ConsultarFacturasPendientesAsync(CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<IReadOnlyList<OpcionDeReferencia>> ConsultarOrdenesDePagoAsync(CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<bool> ExisteOrdenDePagoAsync(int id, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<ResumenDeCierre> ConsultarResumenAsync(int cajaId, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);

    public Task<CierreIntentado> CerrarAsync(int cajaId, int usuarioId, decimal saldoFinalConfirmado, DateTime ocurridoEn, CancellationToken cancelacion = default) => throw new NotSupportedException(Doble.Mensaje);
}

internal static class Doble
{
    public const string Mensaje =
        "La fuente del reporte llamó a una consulta que no le corresponde. Cada fuente usa la misma " +
        "consulta que alimenta su pantalla y ninguna otra (research §3).";
}
