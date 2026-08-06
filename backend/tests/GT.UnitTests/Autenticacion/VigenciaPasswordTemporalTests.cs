using GT.Application.Autenticacion;
using GT.Domain.Autenticacion;
using GT.Infrastructure.Seguridad;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;

namespace GT.UnitTests.Autenticacion;

/// <summary>Cubre FR-017: la contraseña temporal vale 24 horas desde que se generó.</summary>
public class VigenciaPasswordTemporalTests
{
    private static readonly DateTime Generada = new(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void UnaPasswordDefinitivaNoVence()
    {
        Assert.True(VigenciaPasswordTemporal.SigueVigente(null, Generada.AddYears(3)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(23)]
    public void SigueVigenteDentroDeLas24Horas(int horas)
    {
        Assert.True(VigenciaPasswordTemporal.SigueVigente(Generada, Generada.AddHours(horas)));
    }

    [Theory]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(72)]
    public void VenceALas24Horas(int horas)
    {
        Assert.False(VigenciaPasswordTemporal.SigueVigente(Generada, Generada.AddHours(horas)));
    }
}

/// <summary>
/// Cubre FR-021. Estos son los escenarios que no se pueden probar contra la aplicación sin esperar
/// minutos reales, así que se verifican acá con un reloj controlado.
/// </summary>
public class ContadorIntentosFallidosTests
{
    private const string Origen = "192.168.1.50";
    private const string Cuenta = "ADMIN";

    private readonly FakeTimeProvider _reloj = new(
        new DateTimeOffset(2026, 8, 5, 9, 0, 0, TimeSpan.Zero));

    private readonly ContadorIntentosFallidosEnMemoria _contador;

    public ContadorIntentosFallidosTests()
    {
        _contador = new ContadorIntentosFallidosEnMemoria(
            new MemoryCache(new MemoryCacheOptions()),
            _reloj);
    }

    [Fact]
    public void NoFrenaAntesDelQuintoFallo()
    {
        for (var intento = 0; intento < LimiteIntentos.FallosPermitidos - 1; intento++)
        {
            _contador.RegistrarFallo(Origen, Cuenta);
            Assert.Null(_contador.TiempoDeEspera(Origen, Cuenta));
        }
    }

    [Fact]
    public void FrenaAlQuintoFallo()
    {
        RegistrarFallos(LimiteIntentos.FallosPermitidos);

        Assert.NotNull(_contador.TiempoDeEspera(Origen, Cuenta));
    }

    [Fact]
    public void SeLevantaSolaAlCumplirseElMinuto()
    {
        RegistrarFallos(LimiteIntentos.FallosPermitidos);

        _reloj.Advance(LimiteIntentos.Espera + TimeSpan.FromSeconds(1));

        // Nadie tuvo que destrabar nada: la restricción se levanta sola (FR-021, FR-016).
        Assert.Null(_contador.TiempoDeEspera(Origen, Cuenta));
    }

    [Fact]
    public void NoAfectaAOtraCuentaDelMismoOrigen()
    {
        RegistrarFallos(LimiteIntentos.FallosPermitidos);

        // El caso de la oficina de G&T: una sola conexión a internet, varias personas.
        Assert.NotNull(_contador.TiempoDeEspera(Origen, Cuenta));
        Assert.Null(_contador.TiempoDeEspera(Origen, "OTRA_PERSONA"));
    }

    [Fact]
    public void LosFallosViejosNoSeAcumulanConLosNuevos()
    {
        RegistrarFallos(LimiteIntentos.FallosPermitidos - 1);

        _reloj.Advance(LimiteIntentos.Ventana + TimeSpan.FromMinutes(1));

        // Pasada la ventana, el contador arranca de cero: este fallo es el primero, no el quinto.
        _contador.RegistrarFallo(Origen, Cuenta);

        Assert.Null(_contador.TiempoDeEspera(Origen, Cuenta));
    }

    [Fact]
    public void UnIngresoExitosoBorraElContador()
    {
        RegistrarFallos(LimiteIntentos.FallosPermitidos - 1);

        _contador.RegistrarExito(Origen, Cuenta);
        _contador.RegistrarFallo(Origen, Cuenta);

        Assert.Null(_contador.TiempoDeEspera(Origen, Cuenta));
    }

    private void RegistrarFallos(int cantidad)
    {
        for (var intento = 0; intento < cantidad; intento++)
        {
            _contador.RegistrarFallo(Origen, Cuenta);
        }
    }
}
