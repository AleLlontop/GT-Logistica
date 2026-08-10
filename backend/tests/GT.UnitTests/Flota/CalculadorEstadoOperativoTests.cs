using GT.Domain.Flota;

namespace GT.UnitTests.Flota;

/// <summary>
/// FR-014: el estado operativo que se muestra se deriva del guardado y del estado de la
/// documentación. La columna guardada no se toca nunca, y por eso la unidad vuelve sola a estar
/// disponible al renovar el papel vencido (research §4).
/// </summary>
public class CalculadorEstadoOperativoTests
{
    [Theory]
    [InlineData(EstadoDocumentacionVehiculo.Vencida)]
    [InlineData(EstadoDocumentacionVehiculo.SinDocumentacion)]
    public void GuardadoComoDisponible_ConPapelesEnFalta_SeMuestraFueraDeServicio(
        EstadoDocumentacionVehiculo documentacion)
    {
        Assert.Equal(
            VehiculoEstado.FueraDeServicio,
            CalculadorEstadoOperativo.Derivar(VehiculoEstado.Disponible, documentacion));
    }

    [Theory]
    [InlineData(EstadoDocumentacionVehiculo.EnRegla)]
    [InlineData(EstadoDocumentacionVehiculo.ProximaAvencer)]
    public void ConLaDocumentacionEnOrden_SeMuestraLoQueEligioElOperador(
        EstadoDocumentacionVehiculo documentacion)
    {
        Assert.Equal(
            VehiculoEstado.Disponible,
            CalculadorEstadoOperativo.Derivar(VehiculoEstado.Disponible, documentacion));
    }

    /// <summary>
    /// Próxima a vencer <b>no</b> saca la unidad de circulación: el papel todavía vale. Es lo que
    /// distingue el aviso de la inhabilitación (FR-014, FR-035).
    /// </summary>
    [Fact]
    public void ProximaAvencer_NoLaSacaDeServicio()
    {
        Assert.Equal(
            VehiculoEstado.Disponible,
            CalculadorEstadoOperativo.Derivar(
                VehiculoEstado.Disponible,
                EstadoDocumentacionVehiculo.ProximaAvencer));
    }

    /// <summary>
    /// El caso que justifica conservar la columna guardada: un camión en el taller sigue fuera de
    /// servicio aunque tenga toda la documentación al día. Sin ella, renovar el seguro marcaría
    /// disponible una unidad rota (research §4).
    /// </summary>
    [Theory]
    [InlineData(EstadoDocumentacionVehiculo.EnRegla)]
    [InlineData(EstadoDocumentacionVehiculo.ProximaAvencer)]
    [InlineData(EstadoDocumentacionVehiculo.Vencida)]
    [InlineData(EstadoDocumentacionVehiculo.SinDocumentacion)]
    public void GuardadoComoFueraDeServicio_SiempreSeMuestraFueraDeServicio(
        EstadoDocumentacionVehiculo documentacion)
    {
        Assert.Equal(
            VehiculoEstado.FueraDeServicio,
            CalculadorEstadoOperativo.Derivar(VehiculoEstado.FueraDeServicio, documentacion));
    }

    /// <summary>
    /// Al volver la documentación a <c>enRegla</c>, el derivado vuelve a <c>disponible</c> sin que
    /// nadie edite nada: el valor guardado nunca se sobrescribió (FR-014, US4 esc. 11).
    /// </summary>
    [Fact]
    public void AlRenovarElDocumento_ElDerivadoVuelveSolo()
    {
        const VehiculoEstado guardado = VehiculoEstado.Disponible;

        Assert.Equal(
            VehiculoEstado.FueraDeServicio,
            CalculadorEstadoOperativo.Derivar(guardado, EstadoDocumentacionVehiculo.Vencida));

        Assert.Equal(
            VehiculoEstado.Disponible,
            CalculadorEstadoOperativo.Derivar(guardado, EstadoDocumentacionVehiculo.EnRegla));
    }

    [Theory]
    [InlineData(EstadoDocumentacionVehiculo.Vencida, true)]
    [InlineData(EstadoDocumentacionVehiculo.SinDocumentacion, true)]
    [InlineData(EstadoDocumentacionVehiculo.EnRegla, false)]
    [InlineData(EstadoDocumentacionVehiculo.ProximaAvencer, false)]
    public void ImpideEstarDisponible_EsLaMismaCondicionQueValidaElFormulario(
        EstadoDocumentacionVehiculo documentacion,
        bool esperado)
    {
        // FR-014a y FR-014 comparten esta condición a propósito: una sola definición para que las
        // dos reglas no puedan separarse con el tiempo.
        Assert.Equal(esperado, CalculadorEstadoOperativo.ImpideEstarDisponible(documentacion));
    }
}
