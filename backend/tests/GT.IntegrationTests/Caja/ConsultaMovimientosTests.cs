using System.Net;
using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// La consulta de movimientos por rango y por caja (US4, FR-023 a FR-025). Los escenarios con fechas pasadas
/// se arman directo en la base.
///
/// Los tests de la clase comparten la base, así que los filtros por rango se combinan con la caja propia del
/// test —o se cuentan sólo las filas propias— para no ver los movimientos de otro.
/// </summary>
public class ConsultaMovimientosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>Un instante de Argentina (UTC−3) como UTC.</summary>
    private static DateTime EnArgentina(int mes, int dia, int hora, int minuto = 0) =>
        new DateTimeOffset(2026, mes, dia, hora, minuto, 0, TimeSpan.FromHours(-3)).UtcDateTime;

    [Fact]
    public async Task Sin_Filtros_TraeTodos_ConLasSeisColumnas()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);
        var movimiento = await app.CrearMovimientoAsync(
            caja.Id, usuario.Id, TipoMovimientoCaja.Egreso, 250m, concepto: "Peaje");

        var filas = await cliente.TodasLasPaginasAsync<MovimientoLeido>("/api/movimientos-caja");

        var fila = Assert.Single(filas, f => f.Id == movimiento.Id);
        Assert.Equal("egreso", fila.Tipo);
        Assert.Equal(250m, fila.Importe);
        Assert.Equal("Peaje", fila.Concepto);
        Assert.Equal(usuario.Username, fila.Responsable.Nombre);
        Assert.Null(fila.Referencia);
    }

    [Fact]
    public async Task Por_Rango_TraeSoloLosDeEsosDias_ConCadaExtremoOpcional()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, fechaApertura: EnArgentina(9, 14, 8));

        var del14 = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 14, 12));
        var del15 = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 15, 12));
        var del16 = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 16, 12));

        Assert.Equal(
            [del16.Id, del15.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?desde=2026-09-15&hasta=2026-09-16&cajaId={caja.Id}"));

        Assert.Equal(
            [del16.Id, del15.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?desde=2026-09-15&cajaId={caja.Id}"));

        Assert.Equal(
            [del15.Id, del14.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?hasta=2026-09-15&cajaId={caja.Id}"));
    }

    [Fact]
    public async Task Los_DiasSonDeArgentina_EnLosDosExtremos()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, fechaApertura: EnArgentina(9, 14, 8));

        // 16/09 23:30 en Argentina es 17/09 02:30 UTC; 15/09 00:30 en Argentina es 15/09 03:30 UTC.
        var tarde = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 16, 23, 30));
        var temprano = await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 15, 0, 30));

        // Y los bordes de afuera: 14/09 23:59 y 17/09 00:00 en Argentina.
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 14, 23, 59));
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(9, 17, 0, 0));

        Assert.Equal(
            [tarde.Id, temprano.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?desde=2026-09-15&hasta=2026-09-16&cajaId={caja.Id}"));
    }

    [Fact]
    public async Task Un_RangoInvertido_Responde400()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.GetAsync("/api/movimientos-caja?desde=2026-09-16&hasta=2026-09-15");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorCajaLeido>())!;
        Assert.Equal("rango_invalido", error.Codigo);
        Assert.Equal("La fecha desde es posterior a la hasta.", error.Mensaje);
    }

    [Fact]
    public async Task Por_Caja_TraeSoloEsaCaja_YCombinadoConElRangoTambien()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var otro = await app.UsuarioAsync();

        var cerrada = await app.CrearCajaAsync(usuario.Id, EstadoCaja.Cerrada, fechaApertura: EnArgentina(9, 10, 8));
        var abierta = await app.CrearCajaAsync(otro.Id, fechaApertura: EnArgentina(9, 11, 8));

        var deCerrada10 = await app.CrearMovimientoAsync(cerrada.Id, usuario.Id, fecha: EnArgentina(9, 10, 9));
        var deCerrada11 = await app.CrearMovimientoAsync(cerrada.Id, usuario.Id, fecha: EnArgentina(9, 11, 9));
        await app.CrearMovimientoAsync(abierta.Id, otro.Id, fecha: EnArgentina(9, 11, 10));

        Assert.Equal(
            [deCerrada11.Id, deCerrada10.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?cajaId={cerrada.Id}"));

        Assert.Equal(
            [deCerrada11.Id],
            await IdsAsync(cliente, $"/api/movimientos-caja?cajaId={cerrada.Id}&desde=2026-09-11&hasta=2026-09-11"));
    }

    [Fact]
    public async Task Un_PeriodoSinMovimientos_DevuelveTotalCero()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var pagina = (await cliente.GetFromJsonAsync<PaginaLeida<MovimientoLeido>>(
            "/api/movimientos-caja?desde=1990-01-01&hasta=1990-01-31"))!;

        Assert.Equal(0, pagina.Total);
        Assert.Empty(pagina.Items);
    }

    [Fact]
    public async Task El_OrdenEsTotal_SinRepetidosNiFaltantesEntrePaginas()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        // Todos en el mismo instante: sólo el `Id` desempata.
        var instante = DateTime.UtcNow.AddMinutes(-5);
        var creados = new List<int>();

        for (var i = 0; i < 25; i++)
        {
            creados.Add((await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: instante)).Id);
        }

        var ids = await IdsAsync(cliente, $"/api/movimientos-caja?cajaId={caja.Id}");

        Assert.Equal(creados.OrderByDescending(id => id), ids);
    }

    private static async Task<List<int>> IdsAsync(HttpClient cliente, string consulta) =>
        [.. (await cliente.TodasLasPaginasAsync<MovimientoLeido>(consulta)).Select(fila => fila.Id)];
}
