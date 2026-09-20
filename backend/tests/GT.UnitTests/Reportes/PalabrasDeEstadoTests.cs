using GT.Application.Caja;
using GT.Application.Choferes;
using GT.Application.Facturacion;
using GT.Application.Flota;
using GT.Application.Viajes;
using GT.Domain.Caja;
using GT.Domain.Choferes;
using GT.Domain.Viajes;

namespace GT.UnitTests.Reportes;

/// <summary>
/// Las palabras de los estados que van a los reportes, fijadas una por una (FR-010, data-model §2.5).
///
/// **Por qué existe este test.** Las clases <c>NombresDeEstado*</c> del backend no traducen a palabras:
/// devuelven el código camelCase del JSON, que es para lo que existen (convención [003]). Las palabras
/// visibles viven sólo en los mapas de TypeScript, y un reporte que se arma en C# no puede llamarlos.
/// Así que cada módulo ganó un <c>EnPantalla</c>, que es una **copia declarada** del mapa de su
/// pantalla, y esto es lo que la cierra: si alguien cambia una palabra en un lado y no en el otro, el
/// reporte deja de decir lo que dice la pantalla y **no falla nada** — sólo se descubre abriendo un
/// archivo (research §14).
///
/// Es lo más lejos que llega entre dos lenguajes el precedente de [003]: no se puede comparar
/// automáticamente C# contra TypeScript, pero sí dejar las dos listas fijadas y señalándose.
/// </summary>
public class PalabrasDeEstadoTests
{
    // ── Viajes: NOMBRES_DE_ESTADO de servicioViajes.ts ──────────────────────────────────────────

    [Theory]
    [InlineData(EstadoViaje.Pendiente, "Pendiente")]
    [InlineData(EstadoViaje.EnCurso, "En curso")]
    [InlineData(EstadoViaje.Rendido, "Rendido")]
    [InlineData(EstadoViaje.Anulado, "Anulado")]
    [InlineData(EstadoViaje.Facturado, "Facturado")]
    public void EstadoDeViaje_EnPantalla_EsLaPalabraDeLaPantalla(EstadoViaje estado, string esperada) =>
        Assert.Equal(esperada, NombresDeEstadoViaje.EnPantalla(estado));

    [Fact]
    public void EstadoDeViaje_EnPantalla_NoEsElCodigoDelJson() =>
        Assert.NotEqual(
            NombresDeEstadoViaje.EnJson(EstadoViaje.EnCurso),
            NombresDeEstadoViaje.EnPantalla(EstadoViaje.EnCurso));

    // ── Choferes: TEXTO_ESTADO_DOCUMENTO de choferes/servicios/estados.ts ───────────────────────

    [Theory]
    [InlineData(DocumentacionEstado.Vigente, "Al día")]
    [InlineData(DocumentacionEstado.ProximaAvencer, "Próxima a vencer")]
    [InlineData(DocumentacionEstado.Vencida, "Vencida")]
    public void DocumentoDeChofer_EnPantalla_EsLaPalabraDelPanel(
        DocumentacionEstado estado,
        string esperada) =>
        Assert.Equal(esperada, NombresDeEstado.EnPantalla(estado));

    /// <summary>El reporte recibe el DTO ya armado, con el código del JSON: tiene que poder volver.</summary>
    [Theory]
    [InlineData(DocumentacionEstado.Vigente)]
    [InlineData(DocumentacionEstado.ProximaAvencer)]
    [InlineData(DocumentacionEstado.Vencida)]
    public void DocumentoDeChofer_LeerDocumento_DeshaceElCodigoDelJson(DocumentacionEstado estado) =>
        Assert.Equal(estado, NombresDeEstado.LeerDocumento(NombresDeEstado.DelDocumento(estado)));

    // ── Flota: TEXTO_ESTADO_DOCUMENTO de flota/servicios/estados.ts ─────────────────────────────

    /// <summary>
    /// **Flota dice <c>Vigente</c> donde Choferes dice <c>Al día</c>**, y no es un descuido: son los dos
    /// mapas que existen hoy en las dos pantallas, y el reporte tiene que decir lo que dice la suya.
    /// </summary>
    [Theory]
    [InlineData(DocumentacionEstado.Vigente, "Vigente")]
    [InlineData(DocumentacionEstado.ProximaAvencer, "Próxima a vencer")]
    [InlineData(DocumentacionEstado.Vencida, "Vencida")]
    public void DocumentoDeVehiculo_EnPantalla_EsLaPalabraDelPanel(
        DocumentacionEstado estado,
        string esperada) =>
        Assert.Equal(esperada, NombresDeEstadoFlota.EnPantalla(estado));

    // ── Caja: NOMBRES_DE_TIPO de caja/servicios/servicioCaja.ts ────────────────────────────────

    [Theory]
    [InlineData(TipoMovimientoCaja.Ingreso, "Ingreso")]
    [InlineData(TipoMovimientoCaja.Egreso, "Egreso")]
    public void TipoDeMovimiento_EnPantalla_LlevaMayusculaInicial(
        TipoMovimientoCaja tipo,
        string esperada) =>
        Assert.Equal(esperada, NombresDeEstadoCaja.EnPantalla(tipo));

    [Fact]
    public void TipoDeMovimiento_EnPantalla_NoEsElCodigoDelJson() =>
        Assert.NotEqual(
            NombresDeEstadoCaja.EnJson(TipoMovimientoCaja.Ingreso),
            NombresDeEstadoCaja.EnPantalla(TipoMovimientoCaja.Ingreso));

    // ── Facturación: situacion(dias) de facturacion/servicios/api.ts ───────────────────────────

    [Theory]
    [InlineData(-3, "Vencida hace 3 días")]
    [InlineData(-1, "Vencida hace 1 día")]
    [InlineData(0, "Vence hoy")]
    [InlineData(1, "Vence en 1 día")]
    [InlineData(5, "Vence en 5 días")]
    public void Situacion_TieneLasTresRamasDeLaPantalla(int dias, string esperada) =>
        Assert.Equal(esperada, SituacionDeVencimiento.Para(dias));

    /// <summary>El cero tiene su propio texto: <c>Vence en 0 días</c> no es lo que nadie diría.</summary>
    [Fact]
    public void Situacion_EnCero_NoDiceCeroDias() =>
        Assert.DoesNotContain("0", SituacionDeVencimiento.Para(0));
}
