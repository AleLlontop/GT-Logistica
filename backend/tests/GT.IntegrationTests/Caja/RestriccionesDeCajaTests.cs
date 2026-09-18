using GT.Domain.Caja;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia.Configuraciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Caja;

using CajaEntidad = GT.Domain.Caja.Caja;

/// <summary>
/// Las garantías que viven en la base, verificadas fila por fila y <b>sin pasar por los casos de uso</b>:
/// con la validación en el medio, un <c>CHECK</c> mal escrito pasaría inadvertido.
///
/// Cada fila inválida viola <b>una sola</b> restricción, porque con dos SQL Server nombra una cualquiera
/// (convención [009]). Existe porque los <c>CHECK</c> y el filtro del índice llevan valores de enum escritos a
/// mano (data-model §Enumeraciones).
/// </summary>
public class RestriccionesDeCajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    // ── Cajas ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Una_CajaAbierta_YUnaCerrada_Validas_SeAceptan()
    {
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Abierta, null, null));
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Cerrada, DateTime.UtcNow, 10_000m));
    }

    [Fact]
    public async Task Un_SaldoInicialNegativo_SeRechaza()
    {
        var error = await InsertarCajaAsync(EstadoCaja.Abierta, null, null, saldoInicial: -1m);

        Assert.NotNull(error);
        Assert.Contains(CajaConfiguracion.CheckSaldoInicial, error, StringComparison.Ordinal);
    }

    public static TheoryData<EstadoCaja, bool, bool> CierresInconsistentes => new()
    {
        { EstadoCaja.Abierta, true, false }, // abierta con fecha de cierre
        { EstadoCaja.Abierta, false, true }, // abierta con saldo final
        { EstadoCaja.Cerrada, false, true }, // cerrada sin fecha de cierre
        { EstadoCaja.Cerrada, true, false }, // cerrada sin saldo final
    };

    [Theory]
    [MemberData(nameof(CierresInconsistentes))]
    public async Task Un_CierreAMedioHacer_SeRechaza(EstadoCaja estado, bool conFechaCierre, bool conSaldoFinal)
    {
        var error = await InsertarCajaAsync(
            estado,
            conFechaCierre ? DateTime.UtcNow : null,
            conSaldoFinal ? 10_000m : null);

        Assert.NotNull(error);
        Assert.Contains(CajaConfiguracion.CheckCierreConsistente, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_Indice_RechazaUnaSegundaAbiertaDelMismoUsuario()
    {
        var usuario = await app.UsuarioAsync();

        Assert.Null(await InsertarCajaAsync(EstadoCaja.Abierta, null, null, usuario.Id));

        var error = await InsertarCajaAsync(EstadoCaja.Abierta, null, null, usuario.Id);

        Assert.NotNull(error);
        Assert.Contains(CajaConfiguracion.IndiceResponsableAbierta, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_Indice_AceptaUnaAbiertaMasCualquierCantidadDeCerradasDelMismoUsuario()
    {
        var usuario = await app.UsuarioAsync();

        Assert.Null(await InsertarCajaAsync(EstadoCaja.Cerrada, DateTime.UtcNow, 1m, usuario.Id));
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Cerrada, DateTime.UtcNow, 2m, usuario.Id));
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Abierta, null, null, usuario.Id));
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Cerrada, DateTime.UtcNow, 3m, usuario.Id));
    }

    [Fact]
    public async Task El_Indice_AceptaDosAbiertasDeUsuariosDistintos()
    {
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Abierta, null, null, (await app.UsuarioAsync()).Id));
        Assert.Null(await InsertarCajaAsync(EstadoCaja.Abierta, null, null, (await app.UsuarioAsync()).Id));
    }

    // ── Movimientos ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Un_ImporteEnCero_SeRechaza()
    {
        var error = await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, importe: 0m);

        Assert.NotNull(error);
        Assert.Contains(MovimientoDeCajaConfiguracion.CheckImporte, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_ConceptoDeSoloEspacios_SeRechaza()
    {
        var error = await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, concepto: "   ");

        Assert.NotNull(error);
        Assert.Contains(MovimientoDeCajaConfiguracion.CheckConcepto, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_IngresoConOrdenDePago_SeRechaza()
    {
        var orden = await app.OrdenDePagoAsync();

        var error = await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, ordenDePagoId: orden.Id);

        Assert.NotNull(error);
        Assert.Contains(MovimientoDeCajaConfiguracion.CheckReferencia, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_EgresoConFactura_SeRechaza()
    {
        var factura = await app.FacturaAsync();

        var error = await InsertarMovimientoAsync(TipoMovimientoCaja.Egreso, facturaId: factura.Id);

        Assert.NotNull(error);
        Assert.Contains(MovimientoDeCajaConfiguracion.CheckReferencia, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cada_Tipo_ConSuReferencia_OSinNinguna_SeAcepta()
    {
        var factura = await app.FacturaAsync();
        var orden = await app.OrdenDePagoAsync();

        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, facturaId: factura.Id));
        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Egreso, ordenDePagoId: orden.Id));
        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso));
        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Egreso));
    }

    [Fact]
    public async Task La_MismaFactura_EnDosMovimientos_SeAcepta()
    {
        var factura = await app.FacturaAsync(EstadoFactura.Pendiente);

        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, facturaId: factura.Id));
        Assert.Null(await InsertarMovimientoAsync(TipoMovimientoCaja.Ingreso, facturaId: factura.Id));
    }

    // ── Piezas ──────────────────────────────────────────────────────────────────────────────────

    private async Task<string?> InsertarCajaAsync(
        EstadoCaja estado,
        DateTime? fechaCierre,
        decimal? saldoFinal,
        int? usuarioId = null,
        decimal saldoInicial = 10_000m)
    {
        var responsable = usuarioId ?? (await app.UsuarioAsync()).Id;

        return await IntentarAsync(contexto => contexto.Cajas.Add(new CajaEntidad
        {
            SaldoInicial = saldoInicial,
            FechaApertura = DateTime.UtcNow,
            UsuarioResponsableId = responsable,
            Estado = estado,
            FechaCierre = fechaCierre,
            SaldoFinal = saldoFinal,
        }));
    }

    private async Task<string?> InsertarMovimientoAsync(
        TipoMovimientoCaja tipo,
        decimal importe = 1_000m,
        string concepto = "Cobro flete",
        int? facturaId = null,
        int? ordenDePagoId = null)
    {
        var usuario = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        return await IntentarAsync(contexto => contexto.MovimientosDeCaja.Add(new MovimientoDeCaja
        {
            CajaId = caja.Id,
            Tipo = tipo,
            Importe = importe,
            Concepto = concepto,
            UsuarioId = usuario.Id,
            Fecha = DateTime.UtcNow,
            FacturaId = facturaId,
            OrdenDePagoId = ordenDePagoId,
        }));
    }

    private async Task<string?> IntentarAsync(Action<GT.Infrastructure.Persistencia.GtDbContext> agregar)
    {
        try
        {
            await app.EnLaBaseAsync(async contexto =>
            {
                agregar(contexto);
                await contexto.SaveChangesAsync();
            });

            return null;
        }
        catch (DbUpdateException excepcion)
        {
            return excepcion.InnerException?.Message ?? excepcion.Message;
        }
    }
}
