using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
using GT.Domain.Usuarios;

namespace GT.IntegrationTests.Empleados;

public class EmpleadosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static string NuevoDni() => Random.Shared.Next(70000000, 79999999).ToString();

    private static object Alta(string dni, string tipo = "chofer") => new
    {
        nombre = "María",
        apellido = "Gómez",
        dni,
        tipo,
        telefono = "221-555-0101",
        email = $"empleado-{dni}@gt.com.ar",
        fechaNacimiento = "1990-05-17",
    };

    [Fact]
    public async Task AltaDeEmpleados_FuerzaTipoEmpleado_YListadoNoIncluyeChoferes()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dniEmpleado = NuevoDni();
        var dniChofer = NuevoDni();

        // Aunque el cliente intente mandar tipo=chofer, la API de empleados lo fuerza a empleado.
        var alta = await cliente.PostAsJsonAsync("/api/empleados", Alta(dniEmpleado, "chofer"));

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var creado = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(creado);
        Assert.Equal("empleado", creado.Tipo);

        // La ruta general de personas permite crear una persona de tipo chofer para probar el filtro.
        var altaChofer = await cliente.PostAsJsonAsync("/api/personas", Alta(dniChofer, "chofer"));
        Assert.Equal(HttpStatusCode.Created, altaChofer.StatusCode);

        var listado = await cliente.GetFromJsonAsync<List<PersonaLeida>>(
            $"/api/empleados?texto={dniEmpleado}");
        Assert.NotNull(listado);
        Assert.Contains(listado, persona => persona.Dni == dniEmpleado && persona.Tipo == "empleado");
        Assert.DoesNotContain(listado, persona => persona.Dni == dniChofer);
    }

    [Fact]
    public async Task RechazaDniDuplicadoEnElModuloDeEmpleados()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dni = NuevoDni();

        var primera = await cliente.PostAsJsonAsync("/api/empleados", Alta(dni, "empleado"));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);

        var segunda = await cliente.PostAsJsonAsync("/api/empleados", Alta(dni, "empleado"));
        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
    }

    [Fact]
    public async Task NoExponePersonasDeTipoChoferComoEmpleados()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var alta = await cliente.PostAsJsonAsync("/api/personas", Alta(NuevoDni(), "chofer"));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var chofer = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(chofer);

        var respuesta = await cliente.GetAsync($"/api/empleados/{chofer.Id}");
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task TraficoNoTienePermisoParaConsultarEmpleados()
    {
        var cliente = await app.CrearClienteComoAsync("trafico.empleados", GT.Domain.Usuarios.CodigosRol.Trafico);
        var respuesta = await cliente.GetAsync("/api/empleados");
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ModificaEmpleado_ConservandoElTipoEmpleado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dni = NuevoDni();

        var alta = await cliente.PostAsJsonAsync(
            "/api/empleados", Alta(dni, "empleado"));

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var empleado = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(empleado);

        var modificacion = new
        {
            nombre = "Laura",
            apellido = "Gómez",
            dni,
            tipo = "chofer",
            telefono = "221-555-0102",
            email = $"modificado-{dni}@gt.com.ar",
            fechaNacimiento = "1990-05-17"
        };

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/empleados/{empleado.Id}", modificacion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var actualizado = await respuesta.Content.ReadFromJsonAsync<PersonaLeida>();

        Assert.NotNull(actualizado);
        Assert.Equal("Laura", actualizado.Nombre);
        Assert.Equal("empleado", actualizado.Tipo);
    }

    [Fact]
    public async Task NoPermiteConsultarNiModificarUnChoferComoEmpleado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dni = NuevoDni();

        var alta = await cliente.PostAsJsonAsync(
            "/api/personas", Alta(dni, "chofer"));

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var chofer = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(chofer);

        var consulta = await cliente.GetAsync(
            $"/api/empleados/{chofer.Id}");

        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);

        var modificacion = await cliente.PutAsJsonAsync(
            $"/api/empleados/{chofer.Id}", Alta(dni, "empleado"));

        Assert.Equal(HttpStatusCode.NotFound, modificacion.StatusCode);
    }
    [Fact]
    public async Task Devuelve404ParaUnEmpleadoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        const int idInexistente = 999999;

        var consulta = await cliente.GetAsync(
            $"/api/empleados/{idInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);

        var modificacion = await cliente.PutAsJsonAsync(
            $"/api/empleados/{idInexistente}",
            Alta(NuevoDni(), "empleado"));

        Assert.Equal(HttpStatusCode.NotFound, modificacion.StatusCode);

        var baja = await cliente.DeleteAsync(
            $"/api/empleados/{idInexistente}");

        Assert.Equal(HttpStatusCode.NotFound, baja.StatusCode);
    }
    [Fact]
    public async Task DaDeBajaEmpleado_YDejaDeAparecerEnLasConsultas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsJsonAsync(
            "/api/empleados",
            Alta(NuevoDni(), "empleado"));

        var respuesta = await alta.Content.ReadAsStringAsync();

        Assert.True(
            alta.StatusCode == HttpStatusCode.Created,
            $"Alta devolvió {(int)alta.StatusCode} ({alta.StatusCode}). " +
            $"Respuesta: {respuesta}");

        var creado = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(creado);

        var baja = await cliente.DeleteAsync(
            $"/api/empleados/{creado.Id}");

        Assert.True(
            baja.StatusCode == HttpStatusCode.NoContent ||
            baja.StatusCode == HttpStatusCode.OK,
            $"La baja devolvió {(int)baja.StatusCode}: " +
            await baja.Content.ReadAsStringAsync());

        // La ficha devuelve la persona, pero debe indicar que está inactiva.
        var consulta = await cliente.GetAsync(
            $"/api/empleados/{creado.Id}");

        var cuerpoConsulta = await consulta.Content.ReadAsStringAsync();

        Assert.True(
            consulta.StatusCode == HttpStatusCode.OK,
            $"La consulta tras la baja devolvió " +
            $"{(int)consulta.StatusCode} ({consulta.StatusCode}). " +
            $"Respuesta: {cuerpoConsulta}");

        var ficha = await consulta.Content.ReadFromJsonAsync<PersonaLeida>();

        Assert.NotNull(ficha);
        Assert.False(ficha.Activa);

        // La persona dada de baja ya no debe aparecer en el listado.
        var listado = await cliente.GetFromJsonAsync<List<PersonaLeida>>(
            "/api/empleados");

        Assert.NotNull(listado);
        Assert.DoesNotContain(
            listado,
            empleado => empleado.Id == creado.Id);
    }
    [Fact]
    public async Task ConsultaHistoriales_DeEmpleadoSinMovimientos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsJsonAsync(
            "/api/empleados",
            Alta(NuevoDni(), "empleado"));

        var respuesta = await alta.Content.ReadAsStringAsync();

        Assert.True(
            alta.StatusCode == HttpStatusCode.Created,
            $"Alta devolvió {(int)alta.StatusCode} ({alta.StatusCode}). " +
            $"Respuesta: {respuesta}");

        var creado = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(creado);

        var adelantos = await cliente.GetAsync(
            $"/api/empleados/{creado.Id}/adelantos");

        var cajas = await cliente.GetAsync(
            $"/api/empleados/{creado.Id}/cajas");

        Assert.Equal(HttpStatusCode.OK, adelantos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cajas.StatusCode);
    }

    [Fact]
    public async Task ConsultaEmpleadoInexistente_Devuelve404EnFichaEHistoriales()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        const int idInexistente = 999999;

        var ficha = await cliente.GetAsync(
            $"/api/empleados/{idInexistente}");

        var adelantos = await cliente.GetAsync(
            $"/api/empleados/{idInexistente}/adelantos");

        var cajas = await cliente.GetAsync(
            $"/api/empleados/{idInexistente}/cajas");

        Assert.Equal(HttpStatusCode.NotFound, ficha.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, adelantos.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, cajas.StatusCode);
    }

    [Fact]
    public async Task VuelveARegistrarEmpleadoDadoDeBaja_SeRechazaYNoLoReactiva()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dni = NuevoDni();

        var alta = await cliente.PostAsJsonAsync(
            "/api/empleados",
            Alta(dni, "empleado"));

        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var original = await alta.Content.ReadFromJsonAsync<PersonaLeida>();
        Assert.NotNull(original);

        var baja = await cliente.DeleteAsync(
            $"/api/empleados/{original.Id}");

        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var reingreso = await cliente.PostAsJsonAsync(
            "/api/empleados",
            new
            {
                nombre = "Laura",
                apellido = "Pérez",
                dni,
                tipo = "empleado",
                telefono = "221-555-0102",
                email = $"reingreso-{dni}@gt.com.ar",
                fechaNacimiento = "1992-06-12",
            });

        // FR-022b del Módulo 2: el alta nunca reactiva; la persona se reactiva desde el padrón.
        Assert.Equal(HttpStatusCode.BadRequest, reingreso.StatusCode);

        var sinTocar = await cliente.GetFromJsonAsync<PersonaLeida>(
            $"/api/empleados/{original.Id}");

        Assert.NotNull(sinTocar);
        Assert.False(sinTocar.Activa);
        Assert.Equal(original.Nombre, sinTocar.Nombre);
    }

    [Theory]
    [InlineData("Juan3", "Gómez", "221-555-0101")]
    [InlineData("Juan", "Gómez2", "221-555-0101")]
    [InlineData("Juan", "Gómez", "221-ABC-0101")]
    public async Task RechazaNombresConNumerosYTelefonosConLetras(
        string nombre,
        string apellido,
        string telefono)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var dni = NuevoDni();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/empleados",
            new
            {
                nombre,
                apellido,
                dni,
                tipo = "empleado",
                telefono,
                email = $"validacion-{dni}@gt.com.ar",
                fechaNacimiento = "1990-05-17",
            });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private record PersonaLeida(
        int Id,
        string Nombre,
        string Apellido,
        string Dni,
        string Tipo,
        string Telefono,
        string Email,
        bool Activa);
}