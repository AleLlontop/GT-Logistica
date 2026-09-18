using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Caja;

/// <summary>Registrar ingresos y egresos sobre la caja abierta propia (US2, FR-006 a FR-015).</summary>
public class MovimientosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Un_IngresoConFactura_YUnEgresoConOrden_QuedanConFechaResponsableYReferencia()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);
        var factura = await app.FacturaAsync();
        var orden = await app.OrdenDePagoAsync();

        var ingreso = await cliente.RegistrarMovimientoAsync(
            cajaId, "ingreso", 3_500m, "  cobro flete Cliente SA  ", facturaId: factura.Id);
        var egreso = await cliente.RegistrarMovimientoAsync(
            cajaId, "egreso", 2_000m, "pago de peaje", ordenDePagoId: orden.Id);

        Assert.Equal(HttpStatusCode.Created, ingreso.StatusCode);
        Assert.Equal(HttpStatusCode.Created, egreso.StatusCode);

        var leidoIngreso = (await ingreso.Content.ReadFromJsonAsync<MovimientoLeido>())!;
        Assert.Equal("ingreso", leidoIngreso.Tipo);
        Assert.Equal(3_500m, leidoIngreso.Importe);
        Assert.Equal("cobro flete Cliente SA", leidoIngreso.Concepto);
        Assert.Equal(usuario.Id, leidoIngreso.Responsable.Id);
        Assert.Equal($"Factura {factura.NumeroComprobante} · {factura.ClienteRazonSocial}", leidoIngreso.Referencia);

        var leidoEgreso = (await egreso.Content.ReadFromJsonAsync<MovimientoLeido>())!;
        Assert.Equal("egreso", leidoEgreso.Tipo);
        Assert.Equal(NumerosVisibles.OrdenDePago(orden.Numero), leidoEgreso.Referencia);

        Assert.Matches("\"fecha\":\"[^\"]+Z\"", await ingreso.Content.ReadAsStringAsync());

        var detalle = await cliente.DetalleAsync(cajaId);
        Assert.Equal(3_500m, detalle.TotalIngresos);
        Assert.Equal(2_000m, detalle.TotalEgresos);
        Assert.Equal(11_500m, detalle.SaldoActual);
    }

    [Theory]
    [InlineData("ingreso")]
    [InlineData("egreso")]
    public async Task Un_MovimientoSinReferencia_SeRegistra(string tipo)
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);

        var respuesta = await cliente.RegistrarMovimientoAsync(cajaId, tipo);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Null((await respuesta.Content.ReadFromJsonAsync<MovimientoLeido>())!.Referencia);
    }

    public static TheoryData<string?, string?, string?, string, string> DatosInvalidos => new()
    {
        { null, "1000", "Cobro", "tipo", "Elegí si es un ingreso o un egreso." },
        { "transferencia", "1000", "Cobro", "tipo", "Elegí si es un ingreso o un egreso." },
        { "ingreso", null, "Cobro", "importe", "Escribí un importe mayor que cero." },
        { "ingreso", "0", "Cobro", "importe", "Escribí un importe mayor que cero." },
        { "ingreso", "-200", "Cobro", "importe", "Escribí un importe mayor que cero." },
        { "ingreso", "150.555", "Cobro", "importe", "Escribí el importe con hasta dos decimales." },
        { "ingreso", "1000", null, "concepto", "Escribí el concepto del movimiento." },
        { "ingreso", "1000", "", "concepto", "Escribí el concepto del movimiento." },
        { "ingreso", "1000", "   ", "concepto", "Escribí el concepto del movimiento." },
        { "ingreso", "1000", new string('a', 201), "concepto", "El concepto puede tener hasta 200 caracteres." },
    };

    [Theory]
    [MemberData(nameof(DatosInvalidos))]
    public async Task Un_DatoInvalido_Responde400ConSuCampo_SinCrearFilas(
        string? tipo,
        string? importe,
        string? concepto,
        string campo,
        string mensaje)
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);

        var respuesta = await cliente.RegistrarMovimientoAsync(
            cajaId,
            tipo,
            importe is null ? null : decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture),
            concepto);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("datos_invalidos", error.Codigo);
        Assert.Equal(campo, error.Campo);
        Assert.Equal(mensaje, error.Mensaje);
        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }

    [Fact]
    public async Task Una_ReferenciaQueNoCorrespondeAlTipo_Responde400_InvocandoDirecto()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);
        var factura = await app.FacturaAsync();
        var orden = await app.OrdenDePagoAsync();

        await AfirmarReferenciaInvalidaAsync(
            await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", ordenDePagoId: orden.Id),
            "ordenDePagoId",
            "Una orden de pago sólo puede asociarse a un egreso.");

        await AfirmarReferenciaInvalidaAsync(
            await cliente.RegistrarMovimientoAsync(cajaId, "egreso", facturaId: factura.Id),
            "facturaId",
            "Una factura sólo puede asociarse a un ingreso.");

        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }

    [Theory]
    [InlineData(EstadoFactura.Pagada)]
    [InlineData(EstadoFactura.Anulada)]
    public async Task Una_FacturaQueNoEstaPendiente_Responde400(EstadoFactura estado)
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);
        var factura = await app.FacturaAsync(estado);

        await AfirmarReferenciaInvalidaAsync(
            await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", facturaId: factura.Id),
            "facturaId",
            "La factura elegida ya no está pendiente de cobro.");

        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }

    [Fact]
    public async Task Una_FacturaOUnaOrdenInexistente_Responde400()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);

        await AfirmarReferenciaInvalidaAsync(
            await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", facturaId: int.MaxValue),
            "facturaId",
            "La factura elegida ya no está pendiente de cobro.");

        await AfirmarReferenciaInvalidaAsync(
            await cliente.RegistrarMovimientoAsync(cajaId, "egreso", ordenDePagoId: int.MaxValue),
            "ordenDePagoId",
            "La orden de pago elegida no existe.");

        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }

    [Fact]
    public async Task Sobre_UnaCajaCerrada_Responde409CajaCerrada()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, EstadoCaja.Cerrada);

        var respuesta = await cliente.RegistrarMovimientoAsync(caja.Id);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("caja_cerrada", error.Codigo);
        Assert.Equal("Esta caja ya está cerrada y no admite nuevos movimientos.", error.Mensaje);
        Assert.Equal(0, await app.ContarMovimientosDeAsync(caja.Id));
    }

    [Fact]
    public async Task Sobre_LaCajaDeOtroEmpleado_Responde409CajaAjena()
    {
        var (_, duenio) = await app.EmpleadoAsync();
        var (_, otro) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(duenio);

        var respuesta = await otro.RegistrarMovimientoAsync(cajaId);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("caja_ajena", error.Codigo);
        Assert.Equal(
            "Esta caja es de otro empleado. Sólo quien la abrió puede registrar movimientos y cerrarla.",
            error.Mensaje);
        Assert.Equal(0, await app.ContarMovimientosDeAsync(cajaId));
    }

    [Fact]
    public async Task Sobre_UnaCajaInexistente_Responde404()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.RegistrarMovimientoAsync(int.MaxValue);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_EgresoMayorQueElSaldo_SeRegistra_YElSaldoQuedaNegativo()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente, 1_000m);

        var respuesta = await cliente.RegistrarMovimientoAsync(cajaId, "egreso", 1_500m);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.Equal(-500m, (await cliente.DetalleAsync(cajaId)).SaldoActual);
    }

    [Fact]
    public async Task Las_Referencias_QuedanIntactas_YUnaFacturaSeReferenciaDosVeces()
    {
        var (_, cliente) = await app.EmpleadoAsync();
        var cajaId = await AbrirAsync(cliente);
        var factura = await app.FacturaAsync();
        var orden = await app.OrdenDePagoAsync();

        Assert.Equal(
            HttpStatusCode.Created,
            (await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", facturaId: factura.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await cliente.RegistrarMovimientoAsync(cajaId, "ingreso", facturaId: factura.Id)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await cliente.RegistrarMovimientoAsync(cajaId, "egreso", ordenDePagoId: orden.Id)).StatusCode);

        // La referencia es sólo informativa (FR-012): ni la factura se cobra ni la orden cambia.
        var facturaReleida = await app.ConAlcanceAsync(contexto =>
            contexto.Facturas.AsNoTracking().FirstAsync(f => f.Id == factura.Id));
        Assert.Equal(EstadoFactura.Pendiente, facturaReleida.Estado);
        Assert.Null(facturaReleida.FechaCobro);

        var ordenReleida = await app.ConAlcanceAsync(contexto =>
            contexto.OrdenesDePago.AsNoTracking().FirstAsync(o => o.Id == orden.Id));
        Assert.Equal(orden.Importe, ordenReleida.Importe);
    }

    [Fact]
    public async Task El_ListadoDeLaCaja_TraeLosMovimientosPorFechaDescendente_ConTodasLasColumnas()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);
        var factura = await app.FacturaAsync();

        var primero = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: DateTime.UtcNow.AddMinutes(-30));
        var segundo = await app.CrearMovimientoAsync(
            caja.Id, usuario.Id, TipoMovimientoCaja.Egreso, 500m, DateTime.UtcNow.AddMinutes(-10), "Peaje");

        // Uno con referencia, por la API.
        var tercero = (await (await cliente.RegistrarMovimientoAsync(caja.Id, "ingreso", facturaId: factura.Id))
            .Content.ReadFromJsonAsync<MovimientoLeido>())!;

        var filas = await cliente.TodasLasPaginasAsync<MovimientoLeido>($"/api/caja/{caja.Id}/movimientos");

        Assert.Equal([tercero.Id, segundo.Id, primero.Id], filas.Select(fila => fila.Id));

        var peaje = filas[1];
        Assert.Equal("egreso", peaje.Tipo);
        Assert.Equal(500m, peaje.Importe);
        Assert.Equal("Peaje", peaje.Concepto);
        Assert.Equal(usuario.Username, peaje.Responsable.Nombre);
        Assert.Null(peaje.Referencia);
        Assert.NotNull(filas[0].Referencia);
    }

    private static async Task<int> AbrirAsync(HttpClient cliente, decimal saldoInicial = 10_000m) =>
        (await (await cliente.AbrirCajaAsync(saldoInicial)).Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Id;

    private static async Task AfirmarReferenciaInvalidaAsync(HttpResponseMessage respuesta, string campo, string mensaje)
    {
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("referencia_invalida", error.Codigo);
        Assert.Equal(campo, error.Campo);
        Assert.Equal(mensaje, error.Mensaje);
    }
}
