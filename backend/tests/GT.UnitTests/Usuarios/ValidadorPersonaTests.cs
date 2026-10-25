using GT.Application.Usuarios.Personas;

namespace GT.UnitTests.Usuarios;

/// <summary>
/// FR-026a: formato de nombre, apellido y teléfono. Los mismos casos están en
/// <c>frontend/src/modules/usuarios/personas/servicios/validacion.test.ts</c>, que copia la regla
/// para decirla antes de enviar: si uno cambia, el otro también.
/// </summary>
public class ValidadorPersonaTests
{
    private static PersonaRequest Peticion(
        string nombre = "Juan",
        string apellido = "Pérez",
        string telefono = "221-555-0101") =>
        new(nombre, apellido, "30123456", "empleado", telefono, "juan@gt.com.ar", new DateOnly(1990, 5, 17));

    [Theory]
    [InlineData("Juan")]
    [InlineData("María José")]
    [InlineData("Ñuñez")]
    [InlineData("O'Connor")]
    [InlineData("D’Angelo")]
    [InlineData("Pérez-Gómez")]
    [InlineData("J.")]
    [InlineData("Zoë")]
    public void Acepta_NombresValidos(string nombre)
    {
        Assert.Null(ValidadorPersona.PrimerCampoInvalido(Peticion(nombre: nombre)));
        Assert.Null(ValidadorPersona.PrimerCampoInvalido(Peticion(apellido: nombre)));
    }

    [Theory]
    [InlineData("Juan3")]
    [InlineData("Juan 2do")]
    [InlineData("Juan_Pérez")]
    [InlineData("Juan@")]
    [InlineData("...")]
    [InlineData("- '")]
    public void Rechaza_NombresInvalidos(string nombre)
    {
        Assert.Equal("nombre", ValidadorPersona.PrimerCampoInvalido(Peticion(nombre: nombre)));
        Assert.Equal("apellido", ValidadorPersona.PrimerCampoInvalido(Peticion(apellido: nombre)));
    }

    [Fact]
    public void Admite100Caracteres_YRechaza101_SinContarLosEspaciosDeLosBordes()
    {
        Assert.Null(ValidadorPersona.PrimerCampoInvalido(Peticion(nombre: $"  {new string('a', 100)}  ")));
        Assert.Equal("nombre", ValidadorPersona.PrimerCampoInvalido(Peticion(nombre: new string('a', 101))));
    }

    [Theory]
    [InlineData("221-555-0101")]
    [InlineData("+54 9 (221) 555.0101")]
    [InlineData("455512")]
    [InlineData("12345678901234567890")]
    public void Acepta_TelefonosValidos(string telefono)
    {
        Assert.Null(ValidadorPersona.PrimerCampoInvalido(Peticion(telefono: telefono)));
    }

    [Theory]
    [InlineData("45551")]
    [InlineData("123456789012345678901")]
    [InlineData("221-ABC-0101")]
    [InlineData("4555-12/13")]
    [InlineData("11-5555-5555 int. 23")]
    [InlineData("11111111111111111111-----------")]
    public void Rechaza_TelefonosInvalidos(string telefono)
    {
        Assert.Equal("telefono", ValidadorPersona.PrimerCampoInvalido(Peticion(telefono: telefono)));
    }
}
