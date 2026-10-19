using System.Globalization;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;

namespace GT.UnitTests.Pagos;

public class ReglasDePagosTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private const string Cbu = "0000000000000000000001";
    private static decimal Importe(string valor) => decimal.Parse(valor, CultureInfo.InvariantCulture);
    private static FacturaFleteroParaPago Elegible => new(true, EstadoLiquidacion.Pendiente,
        false, 363m, 363m, 363m, 363m, true);

    [Theory]
    [InlineData("0.01")]
    [InlineData("363.12")]
    [InlineData("9999999999999999.99")]
    [InlineData("1.2300")]
    public void Importe_ConHastaDosDecimalesYDentroDeRango_EsValido(string valor) =>
        Assert.True(ReglasDePagos.ImporteValido(Importe(valor)));

    [Theory]
    [InlineData("0")]
    [InlineData("-0.01")]
    [InlineData("1.001")]
    [InlineData("10000000000000000")]
    [InlineData("79228162514264337593543950335")]
    public void Importe_NoPositivoConFraccionExtraOFueraDeRango_SeRechaza(string valor) =>
        Assert.False(ReglasDePagos.ImporteValido(Importe(valor)));

    [Theory]
    [InlineData("263.00", true)]
    [InlineData("262.99", false)]
    [InlineData("263.01", false)]
    public void Totales_ExigenIgualdadExactaSinTolerancia(string segundoMedio, bool esperado) =>
        Assert.Equal(esperado, ReglasDePagos.TotalesCoinciden([121m, 242m], [100m, Importe(segundoMedio)]));

    [Fact]
    public void Suma_AlMaximo_ConservaCadaCentavo()
    {
        Assert.True(ReglasDePagos.IntentarSumarImportes([ReglasDePagos.ImporteMaximo - .01m, .01m], out var total));
        Assert.Equal(9999999999999999.99m, total);
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("9999999999999999.99")]
    [InlineData("79228162514264337593543950335")]
    public void Suma_FueraDeCapacidad_NoDesbordaNiEntregaTotalParcial(string adicional)
    {
        Assert.False(ReglasDePagos.IntentarSumarImportes([ReglasDePagos.ImporteMaximo, Importe(adicional)], out var total));
        Assert.Equal(0m, total);
    }

    [Fact]
    public void Totales_NoAceptanConjuntosVaciosOImportesInvalidos()
    {
        Assert.False(ReglasDePagos.TotalesCoinciden([], []));
        Assert.False(ReglasDePagos.TotalesCoinciden([1m], []));
        Assert.False(ReglasDePagos.TotalesCoinciden([0m], [0m]));
        Assert.False(ReglasDePagos.TotalesCoinciden([1m, -1m], [1m]));
        Assert.False(ReglasDePagos.TotalesCoinciden([1.001m], [1.001m]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NumeroCheque_RequeridoNoBlanco(string? valor) =>
        Assert.Null(ReglasDePagos.NormalizarNumeroCheque(valor));

    [Theory]
    [InlineData("  000123  ", "000123")]
    [InlineData("001-000123", "001-000123")]
    [InlineData("001/000123", "001/000123")]
    [InlineData("  Ab  001/02-x  ", "Ab  001/02-x")]
    public void NumeroCheque_SoloTrimExteriorPreservaContenido(string entrada, string esperado) =>
        Assert.Equal(esperado, ReglasDePagos.NormalizarNumeroCheque(entrada));

    [Theory]
    [InlineData("000123", "123")]
    [InlineData("001-123", "001123")]
    [InlineData("A  B", "A B")]
    [InlineData("ab", "AB")]
    public void NumeroCheque_NoIntroduceEquivalencias(string primero, string segundo) =>
        Assert.NotEqual(ReglasDePagos.NormalizarNumeroCheque(primero), ReglasDePagos.NormalizarNumeroCheque(segundo));

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void NumeroCheque_LongitudSeEvaluaDespuesDeTrimSinTruncar(int longitud, bool valido)
    {
        var contenido = new string('a', longitud);
        Assert.Equal(valido ? contenido : null, ReglasDePagos.NormalizarNumeroCheque("  " + contenido + "  "));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Pago proveedor  ", "Pago proveedor")]
    [InlineData("  Pago  proveedor  ", "Pago  proveedor")]
    public void Detalle_OpcionalSinRellenoAutomatico(string? entrada, string? esperado) =>
        Assert.Equal(esperado, ReglasDePagos.NormalizarDetalle(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void NombreBanco_RequeridoSinDecidirComparacion(string? entrada) =>
        Assert.Null(ReglasDePagos.NormalizarNombreBanco(entrada));

    [Fact]
    public void NombreBanco_TrimYLimiteSinCambiarMayusculas()
    {
        Assert.Equal("Banco  Mixto", ReglasDePagos.NormalizarNombreBanco("  Banco  Mixto  "));
        Assert.Equal(new string('B', 200), ReglasDePagos.NormalizarNombreBanco(" " + new string('B', 200) + " "));
        Assert.Null(ReglasDePagos.NormalizarNombreBanco(new string('B', 201)));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void FechaOpp_UsaHoyArgentinoRecibido(int desplazamiento, bool esperado) =>
        Assert.Equal(esperado, ReglasDePagos.FechaOrdenValida(Hoy.AddDays(desplazamiento), Hoy));

    [Fact]
    public void FechaOpp_AusenteSeRechazaYNoTienePisoTemporal()
    {
        Assert.False(ReglasDePagos.FechaOrdenValida(null, Hoy));
        Assert.True(ReglasDePagos.FechaOrdenValida(DateOnly.MinValue, Hoy));
    }

    [Theory]
    [InlineData(TipoProveedor.ProveedorNormal, -1, -1, true)]
    [InlineData(TipoProveedor.ProveedorNormal, 0, 0, true)]
    [InlineData(TipoProveedor.ProveedorNormal, 1, 2, false)]
    [InlineData(TipoProveedor.ProveedorNormal, 0, -1, false)]
    [InlineData(TipoProveedor.Fletero, -1, 0, true)]
    [InlineData(TipoProveedor.Fletero, 0, 1, true)]
    [InlineData(TipoProveedor.Fletero, 0, 0, false)]
    [InlineData(TipoProveedor.Fletero, 0, -1, false)]
    [InlineData(TipoProveedor.Fletero, 1, 2, false)]
    public void Cheque_FechasSegunTipoProveedor(TipoProveedor tipo, int emision, int vencimiento, bool esperado) =>
        Assert.Equal(esperado, ReglasDePagos.ChequeValido(tipo, 1, "000123", Hoy.AddDays(emision), Hoy.AddDays(vencimiento), .01m, Hoy));

    [Fact]
    public void Cheque_RechazaReferenciaNumeroFechaOImporteInvalidos()
    {
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, null, "123", Hoy, Hoy, 1m, Hoy));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 0, "123", Hoy, Hoy, 1m, Hoy));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 1, " ", Hoy, Hoy, 1m, Hoy));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 1, "123", null, Hoy, 1m, Hoy));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 1, "123", Hoy, null, 1m, Hoy));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 1, "123", Hoy, Hoy, 0m, Hoy));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Transferencia_ValidaFechaPropia(int dias, bool esperado) =>
        Assert.Equal(esperado, ReglasDePagos.TransferenciaValida(Hoy.AddDays(dias), .01m, Cbu, Cbu, "0001", Hoy));

    [Theory]
    [InlineData(null)]
    [InlineData("000000000000000000001")]
    [InlineData("00000000000000000000001")]
    [InlineData("000000000000000000000A")]
    [InlineData("０００００００００００００００００００００１")]
    public void Transferencia_RechazaCbuInvalidoEnAmbosCampos(string? invalido)
    {
        Assert.False(ReglasDePagos.TransferenciaValida(Hoy, 1m, invalido, Cbu, "1", Hoy));
        Assert.False(ReglasDePagos.TransferenciaValida(Hoy, 1m, Cbu, invalido, "1", Hoy));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Transferencia_ComprobanteObligatorio(string? comprobante) =>
        Assert.False(ReglasDePagos.TransferenciaValida(Hoy, 1m, Cbu, Cbu, comprobante, Hoy));

    [Fact]
    public void Transferencia_ConservaCerosSinValidacionBancariaExterna()
    {
        Assert.True(ReglasDePagos.CbuValido(Cbu));
        Assert.Equal('0', Cbu[0]);
        Assert.False(ReglasDePagos.TransferenciaValida(null, 1m, Cbu, Cbu, "1", Hoy));
        Assert.False(ReglasDePagos.TransferenciaValida(Hoy, 0m, Cbu, Cbu, "1", Hoy));
        Assert.False(ReglasDePagos.TransferenciaValida(Hoy, 1.001m, Cbu, Cbu, "1", Hoy));
    }

    [Theory]
    [InlineData("0.01", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("1.001", false)]
    public void Efectivo_SoloValidaImporteSinConsultarCaja(string importe, bool valido) =>
        Assert.Equal(valido, ReglasDePagos.EfectivoValido(Importe(importe)));

    [Theory]
    [InlineData(TipoMedioPagoProveedor.Cheque)]
    [InlineData(TipoMedioPagoProveedor.Transferencia)]
    [InlineData(TipoMedioPagoProveedor.Efectivo)]
    public void Normal_AdmiteCadaMedioYCombinaciones(TipoMedioPagoProveedor medio)
    {
        Assert.True(ReglasDePagos.MediosPermitidos(TipoProveedor.ProveedorNormal, [medio]));
        Assert.True(ReglasDePagos.MediosPermitidos(TipoProveedor.ProveedorNormal, Enum.GetValues<TipoMedioPagoProveedor>()));
    }

    [Fact]
    public void Fletero_AdmiteUnoOVariosCheques()
    {
        Assert.True(ReglasDePagos.MediosPermitidos(TipoProveedor.Fletero, [TipoMedioPagoProveedor.Cheque]));
        Assert.True(ReglasDePagos.MediosPermitidos(TipoProveedor.Fletero, [TipoMedioPagoProveedor.Cheque, TipoMedioPagoProveedor.Cheque]));
    }

    [Theory]
    [InlineData(TipoMedioPagoProveedor.Efectivo)]
    [InlineData(TipoMedioPagoProveedor.Transferencia)]
    [InlineData((TipoMedioPagoProveedor)99)]
    public void Fletero_RechazaTodoElConjuntoAnteOtroMedio(TipoMedioPagoProveedor medio)
    {
        Assert.False(ReglasDePagos.MediosPermitidos(TipoProveedor.Fletero, [medio]));
        Assert.False(ReglasDePagos.MediosPermitidos(TipoProveedor.Fletero, [TipoMedioPagoProveedor.Cheque, medio]));
    }

    [Fact]
    public void Medios_NoAdmiteVaciosNiCodigosDesconocidos()
    {
        Assert.False(ReglasDePagos.MediosPermitidos(TipoProveedor.Fletero, []));
        Assert.False(ReglasDePagos.MediosPermitidos(TipoProveedor.ProveedorNormal, []));
        Assert.False(ReglasDePagos.MediosPermitidos(TipoProveedor.ProveedorNormal, [(TipoMedioPagoProveedor)99]));
        Assert.False(ReglasDePagos.ChequeValido((TipoProveedor)99, 1, "1", Hoy, Hoy, 1m, Hoy));
    }

    [Fact]
    public void Elegibilidad_FacturaNoPendiente() => Assert.Equal(MotivoFacturaFleteroNoElegible.FacturaNoPendiente,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { FacturaPendiente = false }));
    [Fact]
    public void Elegibilidad_LiquidacionAusente() => Assert.Equal(MotivoFacturaFleteroNoElegible.LiquidacionInexistente,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { EstadoLiquidacion = null }));
    [Theory]
    [InlineData(EstadoLiquidacion.Pagada)]
    [InlineData(EstadoLiquidacion.Anulada)]
    public void Elegibilidad_LiquidacionNoPendiente(EstadoLiquidacion estado) => Assert.Equal(MotivoFacturaFleteroNoElegible.LiquidacionNoPendiente,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { EstadoLiquidacion = estado }));
    [Fact]
    public void Elegibilidad_PagoVigenteIncompatible() => Assert.Equal(MotivoFacturaFleteroNoElegible.PagoVigenteIncompatible,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { TienePagoVigenteIncompatible = true }));
    [Fact]
    public void Elegibilidad_TotalModificado() => Assert.Equal(MotivoFacturaFleteroNoElegible.TotalModificado,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { TotalLiquidacionActual = 363.01m }));
    [Fact]
    public void Elegibilidad_SaldoDistinto() => Assert.Equal(MotivoFacturaFleteroNoElegible.SaldoDistinto,
        ElegibilidadFacturaFletero.Evaluar(Elegible with { SaldoLiquidacionActual = 362.99m }));
    [Fact]
    public void Elegibilidad_ConjuntoValido() => Assert.Null(ElegibilidadFacturaFletero.Evaluar(Elegible));
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Elegibilidad_ProveedorActivoOInactivoNoBloqueaDeuda(bool activo) =>
        Assert.Null(ElegibilidadFacturaFletero.Evaluar(Elegible with { ProveedorActivo = activo }));

    [Fact]
    public void CambioDeDia_UsaArgentinaAunqueUtcYaSeaManana()
    {
        var instanteUtc = new DateTimeOffset(2026, 10, 19, 1, 30, 0, TimeSpan.Zero);
        var hoyArgentina = DateOnly.FromDateTime(instanteUtc.ToOffset(TimeSpan.FromHours(-3)).DateTime);
        var mananaUtc = DateOnly.FromDateTime(instanteUtc.UtcDateTime);
        Assert.Equal(Hoy, hoyArgentina);
        Assert.False(ReglasDePagos.FechaOrdenValida(mananaUtc, hoyArgentina));
        Assert.False(ReglasDePagos.ChequeValido(TipoProveedor.ProveedorNormal, 1, "1", mananaUtc, mananaUtc, 1m, hoyArgentina));
        Assert.False(ReglasDePagos.TransferenciaValida(mananaUtc, 1m, Cbu, Cbu, "1", hoyArgentina));
        Assert.True(ReglasDePagos.FechaOrdenValida(Hoy.AddDays(-1), hoyArgentina));
        Assert.True(ReglasDePagos.TransferenciaValida(Hoy, 1m, Cbu, Cbu, "1", hoyArgentina));
    }

    public static IEnumerable<object[]> Transiciones()
    {
        var permitidas = new HashSet<(EstadoCheque, EstadoCheque)>
        {
            (EstadoCheque.Pendiente, EstadoCheque.Entregado), (EstadoCheque.Pendiente, EstadoCheque.Anulado),
            (EstadoCheque.Entregado, EstadoCheque.Depositado), (EstadoCheque.Entregado, EstadoCheque.Anulado),
            (EstadoCheque.Depositado, EstadoCheque.Cobrado), (EstadoCheque.Depositado, EstadoCheque.Rechazado),
        };
        foreach (var anterior in Enum.GetValues<EstadoCheque>())
        foreach (var nuevo in Enum.GetValues<EstadoCheque>())
            yield return [anterior, nuevo, permitidas.Contains((anterior, nuevo))];
    }

    [Theory]
    [MemberData(nameof(Transiciones))]
    public void Cheque_MatrizCompletaDeTransiciones(EstadoCheque anterior, EstadoCheque nuevo, bool permitido) =>
        Assert.Equal(permitido, ReglasDePagos.TransicionChequePermitida(anterior, nuevo));

    [Fact]
    public void Estados_SoloLosAprobadosYCodigosDesconocidosNoTransicionan()
    {
        Assert.Equal(new[] { "Pendiente", "Anulada" }, Enum.GetNames<EstadoOrdenPagoProveedor>());
        Assert.Equal(new[] { "Pendiente", "Entregado", "Depositado", "Cobrado", "Rechazado", "Anulado" }, Enum.GetNames<EstadoCheque>());
        Assert.False(ReglasDePagos.TransicionChequePermitida((EstadoCheque)99, EstadoCheque.Pendiente));
        Assert.False(ReglasDePagos.TransicionChequePermitida(EstadoCheque.Pendiente, (EstadoCheque)99));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData(" Corrección ", true)]
    public void MotivoAnulacion_Requerido(string? motivo, bool valido) =>
        Assert.Equal(valido, ReglasDePagos.MotivoAnulacionValido(motivo));
}
