using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Caja;

// Dentro del espacio de nombres: `GT.IntegrationTests.Caja` tapa al tipo `Caja` si el alias va arriba.
using CajaEntidad = GT.Domain.Caja.Caja;

/// <summary>
/// Datos de prueba del Módulo 11, cargados directamente en la base para no depender de la API que el propio
/// test está verificando.
///
/// Cada test crea <b>sus propios</b> empleados: la caja es personal, así que dos tests de la misma clase no
/// se pisan la caja abierta aunque compartan la base.
/// </summary>
public static class DatosDePruebaCaja
{
    public const string Password = "Caja.12345678";

    private static int _contador;

    /// <summary>Un empleado de *Administración de la empresa* con su cliente ya autenticado.</summary>
    public static Task<(Usuario Usuario, HttpClient Cliente)> EmpleadoAsync(this AplicacionDePrueba app) =>
        app.ConRolAsync(CodigosRol.Administracion);

    public static async Task<(Usuario Usuario, HttpClient Cliente)> ConRolAsync(
        this AplicacionDePrueba app,
        string rol)
    {
        var usuario = await app.UsuarioAsync(rol);

        return (usuario, await app.CrearClienteAutenticadoAsync(usuario.Username, Password));
    }

    /// <summary>Un usuario con nombre único, sin iniciar sesión: para los escenarios armados en la base.</summary>
    public static Task<Usuario> UsuarioAsync(this AplicacionDePrueba app, string rol = CodigosRol.Administracion) =>
        app.CrearUsuarioConRolViajesAsync($"caja_{rol}_{Interlocked.Increment(ref _contador)}", Password, rol);

    // ── Referencias ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Una factura en el estado pedido, con lo que la base exige para ese estado.</summary>
    public static async Task<FacturaCliente> FacturaAsync(
        this AplicacionDePrueba app,
        EstadoFactura estado = EstadoFactura.Pendiente)
    {
        var cliente = await app.CrearClienteAsync();

        return await app.CrearFacturaAsync(
            cliente.Id,
            estado: estado,
            fechaCobro: estado is EstadoFactura.Pagada ? FechaHoyArgentina.Hoy() : null,
            motivoAnulacion: estado is EstadoFactura.Anulada ? "Anulada en la prueba." : null);
    }

    /// <summary>
    /// <paramref name="cantidad"/> órdenes de pago sobre una liquidación pendiente, insertadas directo: el caso
    /// del tope de 50 necesita más de las que un pago real dejaría cargar. Devueltas en orden de creación, así
    /// que la primera es la más vieja.
    /// </summary>
    public static async Task<IReadOnlyList<OrdenDePago>> OrdenesDePagoAsync(this AplicacionDePrueba app, int cantidad)
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);

        return await app.ConAlcanceAsync(async contexto =>
        {
            var administrador = await DatosDePruebaLiquidaciones.AdministradorAsync(contexto);
            var ordenes = new List<OrdenDePago>();

            for (var i = 0; i < cantidad; i++)
            {
                var orden = new OrdenDePago
                {
                    LiquidacionId = liquidacion.Id,
                    FechaPago = FechaHoyArgentina.Hoy(),
                    Importe = 1m,
                    UsuarioId = administrador.Id,
                    RegistradaEn = DateTime.UtcNow,
                };

                contexto.OrdenesDePago.Add(orden);

                // De a una: el número sale de la secuencia en el orden de inserción.
                await contexto.SaveChangesAsync();
                ordenes.Add(orden);
            }

            return (IReadOnlyList<OrdenDePago>)ordenes;
        });
    }

    public static async Task<OrdenDePago> OrdenDePagoAsync(this AplicacionDePrueba app) =>
        (await app.OrdenesDePagoAsync(1))[0];

    public static Task MarcarFacturaPagadaAsync(this AplicacionDePrueba app, int facturaId) =>
        app.EnLaBaseAsync(contexto => contexto.Facturas
            .Where(factura => factura.Id == facturaId)
            .ExecuteUpdateAsync(cambio => cambio
                .SetProperty(factura => factura.Estado, EstadoFactura.Pagada)
                .SetProperty(factura => factura.FechaCobro, FechaHoyArgentina.Hoy())));

    // ── Cajas y movimientos directo en la base ──────────────────────────────────────────────────

    /// <summary>Una caja en cualquier estado e instante, saltando la apertura.</summary>
    public static Task<CajaEntidad> CrearCajaAsync(
        this AplicacionDePrueba app,
        int usuarioId,
        EstadoCaja estado = EstadoCaja.Abierta,
        decimal saldoInicial = 10_000m,
        DateTime? fechaApertura = null,
        decimal? saldoFinal = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var apertura = fechaApertura ?? DateTime.UtcNow.AddHours(-1);

            var caja = new CajaEntidad
            {
                SaldoInicial = saldoInicial,
                FechaApertura = apertura,
                UsuarioResponsableId = usuarioId,
                Estado = estado,
                FechaCierre = estado is EstadoCaja.Cerrada ? apertura.AddHours(8) : null,
                SaldoFinal = estado is EstadoCaja.Cerrada ? saldoFinal ?? saldoInicial : null,
            };

            contexto.Cajas.Add(caja);
            await contexto.SaveChangesAsync();

            return caja;
        });

    public static Task<MovimientoDeCaja> CrearMovimientoAsync(
        this AplicacionDePrueba app,
        int cajaId,
        int usuarioId,
        TipoMovimientoCaja tipo = TipoMovimientoCaja.Ingreso,
        decimal importe = 1_000m,
        DateTime? fecha = null,
        string concepto = "Movimiento de prueba") =>
        app.ConAlcanceAsync(async contexto =>
        {
            var movimiento = new MovimientoDeCaja
            {
                CajaId = cajaId,
                Tipo = tipo,
                Importe = importe,
                Concepto = concepto,
                UsuarioId = usuarioId,
                Fecha = fecha ?? DateTime.UtcNow,
            };

            contexto.MovimientosDeCaja.Add(movimiento);
            await contexto.SaveChangesAsync();

            return movimiento;
        });

    /// <summary>Cierra en la base, con el saldo que corresponde, sin pasar por el cierre.</summary>
    public static Task CerrarEnLaBaseAsync(this AplicacionDePrueba app, int cajaId, decimal saldoFinal = 0m) =>
        app.EnLaBaseAsync(contexto => contexto.Cajas
            .Where(caja => caja.Id == cajaId)
            .ExecuteUpdateAsync(cambio => cambio
                .SetProperty(caja => caja.Estado, EstadoCaja.Cerrada)
                .SetProperty(caja => caja.FechaCierre, DateTime.UtcNow)
                .SetProperty(caja => caja.SaldoFinal, saldoFinal)));

    public static Task<CajaEntidad?> RecargarCajaAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Cajas
            .Include(caja => caja.Movimientos)
            .AsNoTracking()
            .FirstOrDefaultAsync(caja => caja.Id == id));

    public static Task<int> ContarCajasDeAsync(this AplicacionDePrueba app, int usuarioId) =>
        app.ConAlcanceAsync(contexto => contexto.Cajas.CountAsync(caja => caja.UsuarioResponsableId == usuarioId));

    public static Task<int> ContarMovimientosDeAsync(this AplicacionDePrueba app, int cajaId) =>
        app.ConAlcanceAsync(contexto => contexto.MovimientosDeCaja.CountAsync(movimiento => movimiento.CajaId == cajaId));

    // ── Peticiones ──────────────────────────────────────────────────────────────────────────────

    public static Task<HttpResponseMessage> AbrirCajaAsync(this HttpClient cliente, decimal? saldoInicial) =>
        cliente.PostAsJsonAsync("/api/caja", new { saldoInicial });

    public static Task<HttpResponseMessage> RegistrarMovimientoAsync(
        this HttpClient cliente,
        int cajaId,
        string? tipo = "ingreso",
        decimal? importe = 1_000m,
        string? concepto = "Cobro flete",
        int? facturaId = null,
        int? ordenDePagoId = null) =>
        cliente.PostAsJsonAsync(
            $"/api/caja/{cajaId}/movimientos",
            new { tipo, importe, concepto, facturaId, ordenDePagoId });

    public static Task<HttpResponseMessage> CerrarCajaAsync(
        this HttpClient cliente,
        int cajaId,
        bool? confirmado,
        decimal? saldoFinalConfirmado) =>
        cliente.PostAsJsonAsync($"/api/caja/{cajaId}/cierre", new { confirmado, saldoFinalConfirmado });

    public static async Task<CajaDetalleLeida> DetalleAsync(this HttpClient cliente, int cajaId) =>
        (await cliente.GetFromJsonAsync<CajaDetalleLeida>($"/api/caja/{cajaId}"))!;

    public static async Task<ResumenLeido> ResumenAsync(this HttpClient cliente, int cajaId) =>
        (await cliente.GetFromJsonAsync<ResumenLeido>($"/api/caja/{cajaId}/cierre"))!;

    /// <summary>Recorre todas las páginas de una consulta: las filas de otros tests pueden empujar las propias.</summary>
    public static async Task<List<T>> TodasLasPaginasAsync<T>(this HttpClient cliente, string consulta)
    {
        var filas = new List<T>();
        var separador = consulta.Contains('?') ? '&' : '?';

        for (var pagina = 1; ; pagina++)
        {
            var leida = (await cliente.GetFromJsonAsync<PaginaLeida<T>>($"{consulta}{separador}pagina={pagina}"))!;
            filas.AddRange(leida.Items);

            if (pagina * leida.TamanioPagina >= leida.Total)
            {
                return filas;
            }
        }
    }
}

// ── Lo que devuelve el backend, tal como lo fija contracts/README.md ────────────────────────────

public record UsuarioResumenLeido(int Id, string Nombre);

public record CajaListadoLeida(
    int Id,
    UsuarioResumenLeido Responsable,
    DateTime FechaApertura,
    string Estado,
    DateTime? FechaCierre,
    decimal? SaldoFinal);

public record CajaDetalleLeida(
    int Id,
    UsuarioResumenLeido Responsable,
    DateTime FechaApertura,
    string Estado,
    DateTime? FechaCierre,
    decimal? SaldoFinal,
    decimal SaldoInicial,
    decimal TotalIngresos,
    decimal TotalEgresos,
    decimal SaldoActual,
    bool PuedeOperar);

public record MovimientoLeido(
    int Id,
    int CajaId,
    DateTime Fecha,
    string Tipo,
    decimal Importe,
    string Concepto,
    UsuarioResumenLeido Responsable,
    string? Referencia);

public record ResumenLeido(
    decimal SaldoInicial,
    decimal TotalIngresos,
    decimal TotalEgresos,
    List<MovimientoLeido> Movimientos,
    decimal SaldoFinal);

public record OpcionLeida(int Id, string Texto);

public record PaginaLeida<T>(List<T> Items, int Total, int Pagina, int TamanioPagina);

/// <summary>Los cuerpos de error del contrato en uno: los campos que un rechazo no lleva llegan nulos.</summary>
public record ErrorCajaLeido(
    string Codigo,
    string Mensaje,
    string? Campo,
    decimal? SaldoInicial,
    decimal? SaldoFinal,
    List<MovimientoLeido>? Movimientos);
