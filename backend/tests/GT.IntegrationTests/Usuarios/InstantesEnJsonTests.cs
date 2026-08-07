using System.Text.Json;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// Los instantes salen del API declarando que son UTC.
///
/// Nació de un defecto encontrado recorriendo el quickstart del Módulo 2: el último acceso mostraba
/// las 17:07 cuando en Argentina eran las 14:07 — exactamente las tres horas de UTC−3. La causa era
/// que EF Core devuelve los `datetime2` con `Kind = Unspecified` y `System.Text.Json` los escribía
/// sin la `Z`, así que el frontend recibía una hora sin zona y la tomaba como local.
///
/// Se comprueba sobre el JSON crudo a propósito: deserializar contra un `DateTime` de C# haría pasar
/// el test igual, porque el error no está en el valor sino en cómo se declara. La `Z` es el dato.
/// </summary>
public class InstantesEnJsonTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task UltimoAccesoYFechaAlta_DeclaranSuZonaHoraria()
    {
        // Autenticarse deja escrito el último acceso del administrador, así que después de esto las
        // dos propiedades tienen valor.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var json = await cliente.GetStringAsync("/api/usuarios");

        var administrador = JsonDocument.Parse(json).RootElement
            .EnumerateArray()
            .Single(usuario =>
                usuario.GetProperty("username").GetString()
                    == SembradorInicial.UsernameAdministrador);

        var ultimoAcceso = administrador.GetProperty("ultimoAcceso").GetString();
        var fechaAlta = administrador.GetProperty("fechaAlta").GetString();

        Assert.EndsWith("Z", ultimoAcceso);
        Assert.EndsWith("Z", fechaAlta);
    }

    /// <summary>
    /// El instante que viaja tiene que ser el mismo que quedó guardado. Sin esto, marcar todo como
    /// UTC podría tapar el desfase corriendo el valor en lugar de rotularlo bien.
    /// </summary>
    [Fact]
    public async Task ElInstanteQueViaja_EsElMismoQueQuedoGuardado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var json = await cliente.GetStringAsync("/api/usuarios");
        var guardado = await app.ObtenerAdministradorAsync();

        var informado = JsonDocument.Parse(json).RootElement
            .EnumerateArray()
            .Single(usuario =>
                usuario.GetProperty("username").GetString()
                    == SembradorInicial.UsernameAdministrador)
            .GetProperty("ultimoAcceso")
            .GetDateTimeOffset();

        Assert.NotNull(guardado.UltimoAcceso);
        Assert.Equal(DateTimeKind.Utc, guardado.UltimoAcceso!.Value.Kind);
        Assert.Equal(
            guardado.UltimoAcceso!.Value,
            informado.UtcDateTime,
            TimeSpan.FromSeconds(1));
    }
}
