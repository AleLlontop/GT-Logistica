using System.Net;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Autenticacion;

public class SesionTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// FR-009 y SC-006: si la cuenta deja de estar `activa` mientras tiene una sesión abierta, la
    /// sesión se corta en la operación siguiente — no sigue viva hasta vencer por su cuenta.
    ///
    /// Junto con <c>UsaRolesVigentesNoLosDelIngreso</c>, es lo que justifica la revalidación por
    /// petición: con un token autocontenido haría falta una lista de revocación para lograr esto.
    /// </summary>
    [Fact]
    public async Task CortaSesionSiLaCuentaSeDesactiva()
    {
        var usuario = await CrearUsuarioActivoAsync("empleado", "Empleado.1234");
        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, "Empleado.1234");

        // La sesión funciona.
        var antes = await cliente.GetAsync("/api/auth/sesion");
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);

        // Lo dan de baja desde fuera, con la sesión ya abierta.
        await CambiarEstadoAsync(usuario.Id, EstadoUsuario.Inactivo);

        // La operación siguiente se rechaza.
        var despues = await cliente.GetAsync("/api/auth/sesion");
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    /// <summary>FR-009: lo mismo vale para una cuenta bloqueada, no sólo para una dada de baja.</summary>
    [Fact]
    public async Task CortaSesionSiLaCuentaSeBloquea()
    {
        var usuario = await CrearUsuarioActivoAsync("bloqueable", "Bloqueable.1234");
        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, "Bloqueable.1234");

        await CambiarEstadoAsync(usuario.Id, EstadoUsuario.Bloqueado);

        var respuesta = await cliente.GetAsync("/api/auth/sesion");
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-013 y SC-005: cerrar sesión la invalida de inmediato. La cookie que el navegador todavía
    /// tenga guardada deja de autorizar cualquier operación, así que volver "atrás" no recupera el
    /// acceso.
    /// </summary>
    [Fact]
    public async Task CierreDeSesionInvalidaLaCookie()
    {
        var usuario = await CrearUsuarioActivoAsync("saliente", "Saliente.1234");
        var cliente = await app.CrearClienteAutenticadoAsync(usuario.Username, "Saliente.1234");

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/sesion")).StatusCode);

        var cierre = await cliente.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, cierre.StatusCode);

        // Ninguna respuesta protegida puede quedar en la caché del navegador (FR-013).
        Assert.True(cierre.Headers.CacheControl?.NoStore);

        var despues = await cliente.GetAsync("/api/auth/sesion");
        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    /// <summary>
    /// FR-013: cerrar sesión es idempotente. Llamarlo sin sesión abierta responde igual, para que un
    /// segundo clic o una pestaña vieja nunca dejen al usuario con un error en pantalla.
    /// </summary>
    [Fact]
    public async Task CierreDeSesion_EsIdempotente()
    {
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-014: un mismo usuario puede tener sesiones abiertas en más de un equipo a la vez. Cada
    /// cliente con su propio contenedor de cookies equivale a un navegador distinto.
    /// </summary>
    [Fact]
    public async Task PermiteSesionesSimultaneas()
    {
        var usuario = await CrearUsuarioActivoAsync("viajero", "Viajero.1234");

        var equipoUno = await app.CrearClienteAutenticadoAsync(usuario.Username, "Viajero.1234");
        var equipoDos = await app.CrearClienteAutenticadoAsync(usuario.Username, "Viajero.1234");

        Assert.Equal(HttpStatusCode.OK, (await equipoUno.GetAsync("/api/auth/sesion")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await equipoDos.GetAsync("/api/auth/sesion")).StatusCode);
    }

    private Task CambiarEstadoAsync(int idUsuario, EstadoUsuario estado) =>
        app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Usuarios
                .Where(usuario => usuario.Id == idUsuario)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(u => u.Estado, estado));
        });

    private Task<Usuario> CrearUsuarioActivoAsync(string username, string password) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();
            var rol = await contexto.Roles.FirstAsync(r => r.Codigo == CodigosRol.Gerencia);

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(password),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });
}
