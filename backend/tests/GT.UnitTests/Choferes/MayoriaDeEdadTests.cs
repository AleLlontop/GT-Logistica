using GT.Domain.Choferes;

namespace GT.UnitTests.Choferes;

/// <summary>
/// Cubre FR-011: no se registra un chofer menor de 18 años a la fecha del alta.
/// </summary>
public class MayoriaDeEdadTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 6);

    [Fact]
    public void EsMayor_CuandoCumpleDieciochoHoyMismo()
    {
        // El borde: cumplir 18 hoy alcanza. Por eso la edad se calcula por fecha cumplida y no
        // restando años.
        Assert.True(MayoriaDeEdad.EsMayor(new DateOnly(2008, 8, 6), Hoy));
    }

    [Fact]
    public void NoEsMayor_CuandoLosCumpleManana()
    {
        Assert.False(MayoriaDeEdad.EsMayor(new DateOnly(2008, 8, 7), Hoy));
    }

    [Fact]
    public void EsMayor_CuandoLosCumplioAyer()
    {
        Assert.True(MayoriaDeEdad.EsMayor(new DateOnly(2008, 8, 5), Hoy));
    }

    [Fact]
    public void EsMayor_ConUnaEdadHolgada()
    {
        Assert.True(MayoriaDeEdad.EsMayor(new DateOnly(1985, 3, 12), Hoy));
    }

    [Fact]
    public void NoEsMayor_ConDiecisieteAnios()
    {
        Assert.False(MayoriaDeEdad.EsMayor(new DateOnly(2009, 1, 1), Hoy));
    }

    [Fact]
    public void NacidoUnVeintinueveDeFebrero_EsMayorElPrimeroDeMarzo()
    {
        // 2008 fue bisiesto y 2026 no: el cumpleaños número 18 cae el 28 de febrero o el 1 de marzo
        // según el criterio. Con fecha cumplida, el 1 de marzo ya es mayor de edad.
        Assert.True(MayoriaDeEdad.EsMayor(new DateOnly(2008, 2, 29), new DateOnly(2026, 3, 1)));
    }
}
