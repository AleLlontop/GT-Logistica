using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Datos de prueba del Módulo 9, cargados directamente en la base para no depender de la API que el
/// propio test está verificando.
///
/// Cada test crea <b>sus propios</b> transportistas: los disponibles se filtran por transportista, así
/// que dos tests de la misma clase no se ven los viajes aunque compartan la base y el período.
/// </summary>
public static class DatosDePruebaLiquidaciones
{
    /// <summary>El CUIT con el que <c>ConfigurarEmpresaEmisoraAsync</c> del Módulo 6 carga la empresa.</summary>
    public const string CuitEmpresaEmisora = "30712345671";

    public const int Mes = 7;

    public const int Anio = 2026;

    public static readonly DateOnly FechaDelPeriodo = new(Anio, Mes, 10);

    /// <summary>
    /// G&amp;T Logística en el padrón, con el CUIT de la empresa emisora. El CUIT es único en el padrón,
    /// así que se reutiliza si otro test de la clase ya lo cargó.
    /// </summary>
    public static async Task<Transportista> TransportistaPropioAsync(this AplicacionDePrueba app)
    {
        var existente = await app.ConAlcanceAsync(contexto => contexto.Transportistas
            .AsNoTracking()
            .FirstOrDefaultAsync(transportista => transportista.Cuit == CuitEmpresaEmisora));

        return existente ?? await app.CrearTransportistaAsync("G&T Logística S.A.", CuitEmpresaEmisora);
    }

    public static Task<Transportista> TransportistaExternoAsync(
        this AplicacionDePrueba app,
        string nombre = "Transportes Díaz",
        bool activo = true) =>
        app.CrearTransportistaAsync(nombre, activo: activo, tipo: TipoPersona.Fisica);

    /// <summary>Empresa emisora, cliente y un fletero: el punto de partida de casi todo test.</summary>
    public static async Task<(int ClienteId, Transportista Fletero)> EscenarioBaseAsync(this AplicacionDePrueba app)
    {
        await app.ConfigurarEmpresaEmisoraAsync();

        var cliente = await app.CrearClienteAsync();
        var fletero = await app.TransportistaExternoAsync();

        return (cliente.Id, fletero);
    }

    /// <summary>
    /// Un viaje del transportista ya en el estado pedido, dentro de 07/2026 salvo que se pida otra fecha.
    /// El transportista es el <b>registrado en el viaje</b> (FR-005).
    /// </summary>
    public static Task<Viaje> ViajeDeAsync(
        this AplicacionDePrueba app,
        int clienteId,
        int transportistaId,
        decimal importe = 100_000m,
        EstadoViaje estado = EstadoViaje.Rendido,
        DateOnly? fecha = null) =>
        app.CrearViajeAsync(
            clienteId,
            fecha ?? FechaDelPeriodo,
            estado,
            importe: importe,
            transportistaId: transportistaId,
            motivoAnulacion: estado is EstadoViaje.Anulado ? "Anulado en la prueba." : null);

    public static Task SinEmpresaEmisoraAsync(this AplicacionDePrueba app) =>
        app.EnLaBaseAsync(contexto => contexto.EmpresaEmisora.ExecuteDeleteAsync());

    /// <summary>
    /// Una liquidación ya en el estado pedido, saltando la generación. Respeta lo que exige la base: una
    /// pagada con lo pagado igual al total, una anulada con motivo y vínculos no vigentes, y siempre la
    /// entrada <c>generacion</c>.
    /// </summary>
    public static Task<Liquidacion> CrearLiquidacionAsync(
        this AplicacionDePrueba app,
        int transportistaId,
        IReadOnlyList<Viaje> viajes,
        EstadoLiquidacion estado = EstadoLiquidacion.Pendiente,
        decimal importePagado = 0m,
        string? motivoAnulacion = null,
        DateTime? generadaEn = null,
        int mes = Mes,
        int anio = Anio) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var administrador = await AdministradorAsync(contexto);
            var total = viajes.Sum(viaje => viaje.Importe);
            var pagado = estado is EstadoLiquidacion.Pagada && importePagado == 0m ? total : importePagado;

            var liquidacion = new Liquidacion
            {
                TransportistaId = transportistaId,
                PeriodoMes = (byte)mes,
                PeriodoAnio = (short)anio,
                ImporteTotal = total,
                ImportePagado = pagado,
                Estado = estado,
                MotivoAnulacion = estado is EstadoLiquidacion.Anulada
                    ? motivoAnulacion ?? "Anulada en la prueba."
                    : null,
            };

            foreach (var viaje in viajes)
            {
                liquidacion.Viajes.Add(new LiquidacionViaje
                {
                    LiquidacionId = 0,
                    ViajeId = viaje.Id,
                    Vigente = estado is not EstadoLiquidacion.Anulada,
                });
            }

            liquidacion.Cambios.Add(new CambioDeLiquidacion
            {
                LiquidacionId = 0,
                Operacion = OperacionDeLiquidacion.Generacion,
                UsuarioId = administrador.Id,
                OcurridoEn = generadaEn ?? DateTime.UtcNow,
            });

            if (pagado > 0m)
            {
                liquidacion.OrdenesDePago.Add(new OrdenDePago
                {
                    LiquidacionId = 0,
                    FechaPago = FechaHoyArgentina.Hoy(),
                    Importe = pagado,
                    UsuarioId = administrador.Id,
                    RegistradaEn = DateTime.UtcNow,
                });
            }

            contexto.Liquidaciones.Add(liquidacion);
            await contexto.SaveChangesAsync();

            return liquidacion;
        });

    public static Task<Liquidacion?> RecargarLiquidacionAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Liquidaciones
            .Include(liquidacion => liquidacion.Viajes)
            .Include(liquidacion => liquidacion.OrdenesDePago)
            .Include(liquidacion => liquidacion.Cambios).ThenInclude(cambio => cambio.Viajes)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(liquidacion => liquidacion.Id == id));

    public static Task<int> ContarLiquidacionesDeAsync(this AplicacionDePrueba app, int transportistaId) =>
        app.ConAlcanceAsync(contexto => contexto.Liquidaciones
            .CountAsync(liquidacion => liquidacion.TransportistaId == transportistaId));

    public static Task<GT.Domain.Usuarios.Usuario> AdministradorAsync(GT.Infrastructure.Persistencia.GtDbContext contexto) =>
        contexto.Usuarios.FirstAsync(usuario =>
            usuario.UsernameNormalizado == SembradorInicial.UsernameAdministrador.ToUpperInvariant());

    // ── Cuerpos de las peticiones ───────────────────────────────────────────────────────────────

    public static object CuerpoDeGeneracion(int transportistaId, IEnumerable<int> viajeIds, int mes = Mes, int anio = Anio) =>
        new { transportistaId, mes, anio, viajeIds = viajeIds.ToArray() };

    public static string RutaDeDisponibles(int transportistaId, int mes = Mes, int anio = Anio) =>
        $"/api/liquidaciones/disponibles?transportistaId={transportistaId}&mes={mes}&anio={anio}";
}

// ── Lo que devuelve el backend, tal como lo fija contracts/liquidaciones-api.yaml ───────────────

public record TransportistaResumenLeido(int Id, string RazonSocial, string Cuit, bool Activo);

public record TransportistasLiquidablesLeidos(
    bool EmpresaEmisoraConfigurada,
    List<TransportistaResumenLeido> Transportistas);

public record ViajeDisponibleLeido(
    int Id,
    int Numero,
    string Fecha,
    string Origen,
    string Destino,
    string Estado,
    decimal Importe);

public record LiquidacionListadoLeida(
    int Id,
    string Numero,
    int Mes,
    int Anio,
    TransportistaResumenLeido Transportista,
    decimal ImporteTotal,
    decimal? RestaPagar,
    string Estado,
    string? MotivoAnulacion);

public record PaginaDeLiquidacionesLeida(
    List<LiquidacionListadoLeida> Items,
    int Total,
    int Pagina,
    int TamanioPagina);

public record ViajeLiquidadoLeido(int Id, int Numero, string Fecha, string Origen, string Destino, decimal Importe);

public record OrdenDePagoLeida(
    int Id,
    string Numero,
    string FechaPago,
    decimal Importe,
    string RegistradaPor,
    DateTime RegistradaEn);

public record CambioDeLiquidacionLeido(
    string Operacion,
    string Usuario,
    DateTime OcurridoEn,
    List<int> ViajesQuitados,
    List<int> ViajesAgregados);

public record LiquidacionDetalleLeida(
    int Id,
    string Numero,
    int Mes,
    int Anio,
    string FechaGeneracion,
    TransportistaResumenLeido Transportista,
    string Estado,
    string? MotivoAnulacion,
    decimal ImporteTotal,
    decimal ImportePagado,
    decimal? RestaPagar,
    int Version,
    List<ViajeLiquidadoLeido> Viajes,
    List<OrdenDePagoLeida> OrdenesDePago,
    List<CambioDeLiquidacionLeido> Historial,
    bool PuedeEditarse,
    bool PuedeAnularse,
    bool PuedeRegistrarPago);

public record ViajeEnConflictoLiquidacionLeido(int Id, int Numero, string Motivo, string? Liquidacion);

/// <summary>
/// Los cuatro cuerpos de error del contrato en uno: los campos que un rechazo no lleva llegan nulos.
/// </summary>
public record ErrorLiquidacionLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    List<ViajeEnConflictoLiquidacionLeido>? Viajes,
    string? Motivo,
    int? CantidadOrdenesDePago,
    decimal? ImportePagado,
    decimal? RestaPagar,
    string? Desde,
    string? Hasta,
    decimal? Importe,
    decimal? RestaPagarAntes,
    decimal? RestaPagarDespues,
    bool? QuedaPagada);
