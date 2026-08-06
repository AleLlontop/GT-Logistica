using GT.Infrastructure.Seguridad;

namespace GT.UnitTests.Usuarios;

/// <summary>Cubre FR-009 y las tres decisiones de research §2 sobre la contraseña temporal.</summary>
public class GeneradorPasswordTemporalTests
{
    private readonly GeneradorPasswordTemporal _generador = new();

    [Fact]
    public void Genera_UnaPasswordDe12Caracteres_QueSuperaElMinimoDeFR004()
    {
        var password = _generador.Generar();

        Assert.Equal(GeneradorPasswordTemporal.Largo, password.Length);
        Assert.True(password.Length >= 8);
    }

    [Theory]
    [InlineData('l')]
    [InlineData('1')]
    [InlineData('O')]
    [InlineData('0')]
    public void NoUsa_CaracteresQueSeConfundenAlLeerlos(char ambiguo)
    {
        // Alguien va a tener que tipear esto leyéndolo de un mail.
        var muchas = string.Concat(Enumerable.Range(0, 200).Select(_ => _generador.Generar()));

        Assert.DoesNotContain(ambiguo, muchas);
    }

    [Fact]
    public void Genera_UnaPasswordDistintaCadaVez()
    {
        var generadas = Enumerable.Range(0, 500).Select(_ => _generador.Generar()).ToList();

        Assert.Equal(generadas.Count, generadas.Distinct().Count());
    }

    [Fact]
    public void Usa_LetrasYNumeros_EnElConjuntoGenerado()
    {
        // Sobre una muestra grande tienen que aparecer las tres familias del alfabeto; en una sola
        // contraseña de 12 caracteres podría faltar alguna por azar.
        var muchas = string.Concat(Enumerable.Range(0, 200).Select(_ => _generador.Generar()));

        Assert.Contains(muchas, char.IsUpper);
        Assert.Contains(muchas, char.IsLower);
        Assert.Contains(muchas, char.IsDigit);
    }
}
