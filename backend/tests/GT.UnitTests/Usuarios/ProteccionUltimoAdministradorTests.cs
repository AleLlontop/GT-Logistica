using GT.Domain.Usuarios;

namespace GT.UnitTests.Usuarios;

/// <summary>
/// Cubre FR-019 y SC-005: nunca puede quedar el sistema sin un usuario activo con el rol
/// <i>Administrador del sistema</i>, por ninguno de los tres caminos que podrían romperlo.
/// </summary>
public class ProteccionUltimoAdministradorTests
{
    public static TheoryData<OperacionSobreAdministrador> LasTresOperaciones =>
    [
        OperacionSobreAdministrador.CambiarEstado,
        OperacionSobreAdministrador.QuitarRolAdministrador,
        OperacionSobreAdministrador.DarDeBaja,
    ];

    [Theory]
    [MemberData(nameof(LasTresOperaciones))]
    public void SeRechaza_CuandoNoQuedariaNingunAdministradorActivo(
        OperacionSobreAdministrador operacion)
    {
        // El caso que la spec quiere frenar: el afectado es el único administrador activo, así que
        // excluyéndolo no queda ninguno. Da igual si es la cuenta de otro o la propia de quien opera.
        Assert.False(ProteccionUltimoAdministrador.SePuedeEjecutar(0, operacion));
    }

    [Theory]
    [MemberData(nameof(LasTresOperaciones))]
    public void SePermite_CuandoQuedaAlMenosOtroAdministradorActivo(
        OperacionSobreAdministrador operacion)
    {
        Assert.True(ProteccionUltimoAdministrador.SePuedeEjecutar(1, operacion));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(37)]
    public void SePermite_CuandoQuedanVarios(int restantes)
    {
        Assert.True(ProteccionUltimoAdministrador.SePuedeEjecutar(
            restantes,
            OperacionSobreAdministrador.DarDeBaja));
    }

    [Theory]
    [MemberData(nameof(LasTresOperaciones))]
    public void NoAplica_CuandoElAfectadoNoEsAdministradorActivo(
        OperacionSobreAdministrador operacion)
    {
        // Dar de baja a alguien de Tráfico no puede quedar frenado por esta regla, ni siquiera si no
        // hubiera ningún otro administrador: su baja no cambia cuántos administradores activos hay.
        Assert.True(ProteccionUltimoAdministrador.SePuedeEjecutar(
            elAfectadoEsAdministradorActivo: false,
            administradoresActivosRestantes: 0,
            operacion));
    }

    [Theory]
    [MemberData(nameof(LasTresOperaciones))]
    public void SeRechaza_CuandoElAfectadoEsAdministradorActivo_YEsElUltimo(
        OperacionSobreAdministrador operacion)
    {
        Assert.False(ProteccionUltimoAdministrador.SePuedeEjecutar(
            elAfectadoEsAdministradorActivo: true,
            administradoresActivosRestantes: 0,
            operacion));
    }
}
