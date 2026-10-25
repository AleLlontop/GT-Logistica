using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Adelantos;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
using GT.Domain.Usuarios;

namespace GT.IntegrationTests.Empleados;

/// <summary>
/// Casos que completan la cobertura transversal del módulo: permisos, bloqueo por caja abierta y
/// aislamiento de los historiales por Persona/Usuario.
/// </summary>
public class EmpleadosHistoriasYPermisosTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    private static string NuevoDni() => Random.Shared.Next(70_000_000, 79_999_999).ToString();

    private static object Alta(string dni) => new
    {
        nombre = "Ana",
        apellido = "Pérez",
        dni,
        tipo = "empleado",
        telefono = "221-555-0101",
        email = $"empleado-{dni}@gt.com.ar",
        fechaNacimiento = "1990-05-17",
    };

    private static async Task<PersonaLeida> CrearEmpleadoAsync(HttpClient cliente)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/empleados", Alta(NuevoDni()));
        var contenido = await respuesta.Content.ReadAsStringAsync();
        Assert.True(
            respuesta.StatusCode == HttpStatusCode.Created,
            $"El alta de empleado devolvió {(int)respuesta.StatusCode}: {contenido}");

        var persona = await respuesta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(persona);
        return persona;
    }

    [Fact]
    public async Task GerenciaPuedeConsultarPeroNoGestionarEmpleados()
    {
        var cliente = await app.CrearClienteComoAsync(
            $"g_emp_{Guid.NewGuid():N}",
            CodigosRol.Gerencia);

        var consulta = await cliente.GetAsync("/api/empleados");
        var alta = await cliente.PostAsJsonAsync("/api/empleados", Alta(NuevoDni()));

        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, alta.StatusCode);
    }

    [Fact]
    public async Task BajaConCajaAbierta_LaRechazaElVinculoConElUsuario_YConservaEmpleadoActivo()
    {
        // Una caja tiene siempre un usuario responsable, así que el empleado con caja abierta está
        // vinculado a un usuario y la baja la rechaza FR-028 del Módulo 2, antes que la caja.
        var administrador = await app.CrearClienteAutenticadoAsync();
        var empleado = await CrearEmpleadoAsync(administrador);

        // La caja referencia al usuario, no a Persona. Se crea la relación que necesita el escenario.
        var usuario = await app.CrearUsuarioAsync(
            $"u_box_{Guid.NewGuid():N}",
            CodigosRol.Administracion,
            personaId: empleado.Id);
        await app.CrearCajaAsync(usuario.Id);

        var baja = await administrador.DeleteAsync($"/api/empleados/{empleado.Id}");
        var error = await baja.Content.ReadFromJsonAsync<ErrorLeido>();
        var ficha = await administrador.GetFromJsonAsync<PersonaLeida>($"/api/empleados/{empleado.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, baja.StatusCode);
        Assert.Equal("persona_vinculada", error?.Codigo);
        Assert.NotNull(ficha);
        Assert.True(ficha.Activa);
    }

    [Fact]
    public async Task HistorialDeAdelantos_SoloDevuelveLosDelEmpleadoConsultado()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();
        var empleadoObjetivo = await CrearEmpleadoAsync(administrador);
        var otroEmpleado = await CrearEmpleadoAsync(administrador);

        var adelantoObjetivo = await app.CrearAdelantoAsync(empleadoObjetivo.Id);
        _ = await app.CrearAdelantoAsync(otroEmpleado.Id);

        var historial = await administrador.GetFromJsonAsync<List<AdelantoLeido>>(
            $"/api/empleados/{empleadoObjetivo.Id}/adelantos");

        Assert.NotNull(historial);
        Assert.Contains(historial, adelanto => adelanto.Id == adelantoObjetivo.Id);
        Assert.All(historial, adelanto => Assert.Equal(empleadoObjetivo.Id, adelanto.Persona.Id));
    }

    [Fact]
    public async Task HistorialDeCajas_SoloDevuelveLasDelUsuarioVinculadoAlEmpleado()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();
        var empleadoObjetivo = await CrearEmpleadoAsync(administrador);
        var otroEmpleado = await CrearEmpleadoAsync(administrador);

        var usuarioObjetivo = await app.CrearUsuarioAsync(
            $"u_box_t_{Guid.NewGuid():N}", CodigosRol.Administracion,
            personaId: empleadoObjetivo.Id);
        var otroUsuario = await app.CrearUsuarioAsync(
            $"u_box_o_{Guid.NewGuid():N}", CodigosRol.Administracion,
            personaId: otroEmpleado.Id);

        var cajaObjetivo = await app.CrearCajaAsync(usuarioObjetivo.Id);
        _ = await app.CrearCajaAsync(otroUsuario.Id);

        var historial = await administrador.GetFromJsonAsync<List<CajaLeida>>(
            $"/api/empleados/{empleadoObjetivo.Id}/cajas");

        Assert.NotNull(historial);
        Assert.Contains(historial, caja => caja.Id == cajaObjetivo.Id);
        Assert.All(historial, caja => Assert.Equal(usuarioObjetivo.Id, caja.Responsable.Id));
    }

    private sealed record PersonaLeida(
        int Id,
        string Nombre,
        string Apellido,
        string Dni,
        string Tipo,
        string Telefono,
        string Email,
        bool Activa);

    private sealed record ErrorLeido(string Codigo, string Mensaje);

    private sealed record PersonaResumenLeido(int Id, string Apellido, string Nombre, string Dni);

    private sealed record AdelantoLeido(int Id, PersonaResumenLeido Persona);

    private sealed record ResponsableLeido(int Id, string Nombre);

    private sealed record CajaLeida(
        int Id,
        ResponsableLeido Responsable,
        string FechaApertura,
        string Estado,
        string? FechaCierre,
        decimal? SaldoFinal);
}
