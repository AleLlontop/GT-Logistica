using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// Caso límite de la spec: dos responsables de sistemas crean el mismo username al mismo tiempo.
///
/// La unicidad se garantiza con un índice único en la base, no sólo con la validación previa
/// (FR-002, FR-003, research §3). Este test es la razón por la que la validación previa no alcanza:
/// entre el SELECT y el INSERT hay una ventana en la que las dos peticiones creen que el username
/// está libre.
///
/// Lo importante no es sólo que no se creen dos usuarios, sino que quien pierde reciba el error de
/// duplicado en lenguaje llano y no una excepción técnica.
/// </summary>
public class UnicidadConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task DosAltasSimultaneas_DelMismoUsername_CreanUnaSola()
    {
        var clienteA = await app.CrearClienteAutenticadoAsync();
        var clienteB = await app.CrearClienteAutenticadoAsync();

        const string username = "carrera.username";

        var alta = (string email) => new
        {
            username,
            email,
            password = DatosDePrueba.PasswordValida,
            estado = "activo",
            roles = new[] { CodigosRol.Trafico },
            personaId = (int?)null,
        };

        var enParalelo = await Task.WhenAll(
            clienteA.PostAsJsonAsync("/api/usuarios", alta("carrera.a@gt.com.ar")),
            clienteB.PostAsJsonAsync("/api/usuarios", alta("carrera.b@gt.com.ar")));

        var creadas = enParalelo.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rechazadas = enParalelo.Where(r => r.StatusCode == HttpStatusCode.BadRequest).ToList();

        Assert.Equal(1, creadas);
        Assert.Single(rechazadas);

        // Quien llega segundo recibe el error de duplicado, no un 500.
        var error = await rechazadas[0].Content.ReadFromJsonAsync<RespuestaError>();

        Assert.NotNull(error);
        Assert.Equal("username_duplicado", error.Codigo);

        var enLaBase = await app.ConAlcanceAsync(contexto => contexto.Usuarios
            .CountAsync(usuario => usuario.UsernameNormalizado == username.ToUpperInvariant()));

        Assert.Equal(1, enLaBase);
    }

    [Fact]
    public async Task DosAltasSimultaneas_DelMismoEmail_CreanUnaSola()
    {
        var clienteA = await app.CrearClienteAutenticadoAsync();
        var clienteB = await app.CrearClienteAutenticadoAsync();

        const string email = "carrera.email@gt.com.ar";

        var alta = (string username) => new
        {
            username,
            email,
            password = DatosDePrueba.PasswordValida,
            estado = "activo",
            roles = new[] { CodigosRol.Trafico },
            personaId = (int?)null,
        };

        var enParalelo = await Task.WhenAll(
            clienteA.PostAsJsonAsync("/api/usuarios", alta("carrera.mail.a")),
            clienteB.PostAsJsonAsync("/api/usuarios", alta("carrera.mail.b")));

        Assert.Equal(1, enParalelo.Count(r => r.StatusCode == HttpStatusCode.Created));

        var rechazada = enParalelo.Single(r => r.StatusCode == HttpStatusCode.BadRequest);
        var error = await rechazada.Content.ReadFromJsonAsync<RespuestaError>();

        Assert.NotNull(error);
        Assert.Equal("email_duplicado", error.Codigo);
    }

    [Fact]
    public async Task DosAltasSimultaneas_DeLaMismaPersona_VinculanAUnaSola()
    {
        // El índice único filtrado de PersonaId es lo que sostiene FR-008 bajo concurrencia.
        var clienteA = await app.CrearClienteAutenticadoAsync();
        var clienteB = await app.CrearClienteAutenticadoAsync();

        var persona = await app.CrearPersonaAsync("31999888");

        var alta = (string username) => new
        {
            username,
            email = $"{username}@gt.com.ar",
            password = DatosDePrueba.PasswordValida,
            estado = "activo",
            roles = new[] { CodigosRol.Trafico },
            personaId = (int?)persona.Id,
        };

        var enParalelo = await Task.WhenAll(
            clienteA.PostAsJsonAsync("/api/usuarios", alta("carrera.persona.a")),
            clienteB.PostAsJsonAsync("/api/usuarios", alta("carrera.persona.b")));

        Assert.Equal(1, enParalelo.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(1, enParalelo.Count(r => r.StatusCode == HttpStatusCode.BadRequest));

        var vinculados = await app.ConAlcanceAsync(contexto => contexto.Usuarios
            .CountAsync(usuario => usuario.PersonaId == persona.Id));

        Assert.Equal(1, vinculados);
    }

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
