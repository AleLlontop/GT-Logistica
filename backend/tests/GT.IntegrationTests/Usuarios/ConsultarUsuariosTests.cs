using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 2: consulta de usuarios.
///
/// Cubre FR-011 (columnas y filtros combinables, con coincidencia parcial en username y email),
/// FR-012 (sin resultados es una respuesta legítima) y FR-013 (el detalle nunca muestra la
/// contraseña).
/// </summary>
public class ConsultarUsuariosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Lista_DevuelveLasSeisColumnasDeFR011()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("columnas.test", CodigosRol.Gerencia);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>("/api/usuarios");

        var usuario = Assert.Single(usuarios!.Where(u => u.Username == "columnas.test"));

        Assert.Equal("columnas.test@gt.com.ar", usuario.Email);
        Assert.Equal("activo", usuario.Estado);
        Assert.Single(usuario.Roles);
        Assert.NotEqual(default, usuario.FechaAlta);
        // Nunca ingresó: la pantalla lo muestra como "Nunca ingresó" (contracts/README.md).
        Assert.Null(usuario.UltimoAcceso);
    }

    // Cada caso crea su propio usuario: la fixture se comparte entre los casos de la teoría, así que
    // repetir el username chocaría contra el índice único de FR-002 y el fallo no diría nada sobre
    // el filtro.
    [Theory]
    [InlineData("pere", "jperez.minusculas")]
    [InlineData("PERE", "jperez.mayusculas")]
    [InlineData("Pere", "jperez.mezcladas")]
    public async Task Filtra_PorUsername_ConCoincidenciaParcialYSinDistinguirMayusculas(
        string fragmento,
        string username)
    {
        // FR-011: escribir "pere" tiene que traer a "jperez", esté escrito como esté.
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync(username);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>(
            $"/api/usuarios?username={fragmento}");

        Assert.Contains(usuarios!, usuario => usuario.Username == username);
    }

    [Fact]
    public async Task Filtra_PorUsername_EnCualquierPosicionDelTexto()
    {
        // No es "empieza con": el fragmento puede estar en el medio.
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("mjuarez.medio");

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>("/api/usuarios?username=juarez");

        Assert.Contains(usuarios!, usuario => usuario.Username == "mjuarez.medio");
    }

    [Fact]
    public async Task Filtra_PorEmail_ConCoincidenciaParcial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("mail.parcial");

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>(
            "/api/usuarios?email=MAIL.PARCIAL@GT");

        Assert.Contains(usuarios!, usuario => usuario.Username == "mail.parcial");
    }

    [Fact]
    public async Task Filtra_PorRol_ConIgualdadExacta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("solo.trafico", CodigosRol.Trafico);
        await app.CrearUsuarioAsync("solo.gerencia", CodigosRol.Gerencia);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>(
            $"/api/usuarios?rol={CodigosRol.Trafico}");

        Assert.Contains(usuarios!, usuario => usuario.Username == "solo.trafico");
        Assert.DoesNotContain(usuarios!, usuario => usuario.Username == "solo.gerencia");
    }

    [Fact]
    public async Task Filtra_PorEstado_ConIgualdadExacta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("esta.activo", estado: EstadoUsuario.Activo);
        await app.CrearUsuarioAsync("esta.bloqueado", estado: EstadoUsuario.Bloqueado);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>("/api/usuarios?estado=bloqueado");

        Assert.Contains(usuarios!, usuario => usuario.Username == "esta.bloqueado");
        Assert.DoesNotContain(usuarios!, usuario => usuario.Username == "esta.activo");
    }

    [Fact]
    public async Task Combina_LosCuatroFiltros_ConY()
    {
        // FR-011: el resultado cumple TODAS las condiciones, no cualquiera de ellas.
        var cliente = await app.CrearClienteAutenticadoAsync();

        await app.CrearUsuarioAsync("combina.si", CodigosRol.Trafico, EstadoUsuario.Inactivo);
        // Mismo fragmento de username y mismo rol, pero otro estado: no tiene que aparecer.
        await app.CrearUsuarioAsync("combina.no", CodigosRol.Trafico, EstadoUsuario.Activo);

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>(
            $"/api/usuarios?username=combina&email=combina&rol={CodigosRol.Trafico}&estado=inactivo");

        Assert.Contains(usuarios!, usuario => usuario.Username == "combina.si");
        Assert.DoesNotContain(usuarios!, usuario => usuario.Username == "combina.no");
    }

    [Fact]
    public async Task Devuelve_ListaVacia_CuandoNingunUsuarioCoincide()
    {
        // FR-012: sin resultados es una respuesta legítima (200 con lista vacía), no un error. El
        // mensaje explícito lo pone la pantalla.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/usuarios?username=no.existe.nadie.asi");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var usuarios = await respuesta.Content.ReadFromJsonAsync<List<UsuarioLeido>>();

        Assert.Empty(usuarios!);
    }

    [Fact]
    public async Task Detalle_IncluyeLaPersonaAsociada()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("28777666", "Marta", "Gómez");
        var usuario = await app.CrearUsuarioAsync("con.persona", personaId: persona.Id);

        var detalle = await cliente.GetFromJsonAsync<UsuarioDetalleLeido>($"/api/usuarios/{usuario.Id}");

        Assert.NotNull(detalle!.Persona);
        Assert.Equal("Marta", detalle.Persona.Nombre);
        Assert.Equal("28777666", detalle.Persona.Dni);
    }

    [Fact]
    public async Task Detalle_DevuelvePersonaEnNull_CuandoElUsuarioNoTieneNinguna()
    {
        // Es un caso válido y habitual, no una excepción (FR-008).
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("sin.persona.detalle");

        var detalle = await cliente.GetFromJsonAsync<UsuarioDetalleLeido>($"/api/usuarios/{usuario.Id}");

        Assert.Null(detalle!.Persona);
    }

    [Fact]
    public async Task Detalle_NoDevuelveLaContraseñaEnNingunaCircunstancia()
    {
        // FR-013.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("sin.clave.detalle");

        var respuesta = await cliente.GetAsync($"/api/usuarios/{usuario.Id}");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DatosDePrueba.PasswordValida, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detalle_DevuelveNoEncontrado_CuandoElUsuarioNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/usuarios/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();

        Assert.Equal("no_encontrado", error!.Codigo);
    }

    [Fact]
    public async Task Rechaza_LaConsulta_ParaUnUsuarioSinElPermisoDeGestion()
    {
        // FR-007.
        var cliente = await app.CrearClienteComoAsync("gerencia.curiosa", CodigosRol.Gerencia);

        var respuesta = await cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private record UsuarioLeido(
        int Id,
        string Username,
        string Email,
        string Estado,
        IReadOnlyList<RolLeido> Roles,
        DateTime FechaAlta,
        DateTime? UltimoAcceso);

    private record UsuarioDetalleLeido(
        int Id,
        string Username,
        string Estado,
        PersonaLeida? Persona);

    private record PersonaLeida(int Id, string Nombre, string Apellido, string Dni, string Tipo);

    private record RolLeido(string Codigo, string Nombre);

    private record RespuestaError(string Codigo, string Mensaje);
}
