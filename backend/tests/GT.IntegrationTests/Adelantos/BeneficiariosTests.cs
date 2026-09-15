using System.Net;
using System.Net.Http.Json;
using GT.Domain.Personas;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Usuarios;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// El desplegable de beneficiarios (FR-002, FR-002a, FR-004) sobre un padrón con cada caso, y la prueba de
/// que <b>es la misma regla</b> que valida el registro (research §1).
/// </summary>
public class BeneficiariosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private sealed record Padron(
        Persona PropioGomez,
        Persona PropioAlvarez,
        Persona PropioConFichaDeBaja,
        Persona Externo,
        Persona EmpleadaTorres,
        Persona EmpleadaRuiz,
        Persona EmpleadoDeBaja,
        Persona TipoChoferSinFicha,
        Persona TipoEmpleadoConFichaPropia)
    {
        public Persona[] Todas =>
        [
            PropioGomez, PropioAlvarez, PropioConFichaDeBaja, Externo, EmpleadaTorres, EmpleadaRuiz,
            EmpleadoDeBaja, TipoChoferSinFicha, TipoEmpleadoConFichaPropia,
        ];

        /// <summary>Las filas de la respuesta que son de este padrón, en el orden en que llegaron.</summary>
        public int[] Propias(BeneficiariosLeidos respuesta) =>
            [.. respuesta.Personas.Select(persona => persona.Id).Where(id => Todas.Any(propia => propia.Id == id))];
    }

    private async Task<Padron> PadronAsync()
    {
        await app.ConfigurarEmpresaEmisoraAsync();
        var propio = await app.TransportistaPropioAsync();
        var externo = await app.TransportistaExternoAsync();

        return new Padron(
            await app.ChoferAsync(propio.Id, "Gomez", "Carlos"),
            await app.ChoferAsync(propio.Id, "Alvarez", "Beto"),
            await app.ChoferAsync(propio.Id, "Castro", "Dario", fichaActiva: false),
            await app.ChoferAsync(externo.Id, "Diaz", "Luis"),
            await app.EmpleadoAsync("Torres", "Ana"),
            await app.EmpleadoAsync("Ruiz", "Marta"),
            await app.EmpleadoAsync("Sosa", "Pedro", activa: false),
            await app.CrearPersonaAsync(
                dni: DatosDePruebaViajes.SemillaUnica().ToString(),
                nombre: "Eva",
                apellido: "Luna",
                tipo: TipoIntegrante.Chofer),
            await app.ChoferAsync(propio.Id, "Molina", "Iris", tipo: TipoIntegrante.Empleado));
    }

    [Fact]
    public async Task Con_TipoChofer_TraeLosPropiosActivos_YAQuienTieneFichaPropiaAunqueFigureComoEmpleado()
    {
        var padron = await PadronAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.BeneficiariosAsync("chofer");

        Assert.True(respuesta.EmpresaEmisoraConfigurada);

        // Por apellido: Alvarez, Gomez, Molina.
        Assert.Equal(
            [padron.PropioAlvarez.Id, padron.PropioGomez.Id, padron.TipoEmpleadoConFichaPropia.Id],
            padron.Propias(respuesta));

        var alvarez = respuesta.Personas.Single(persona => persona.Id == padron.PropioAlvarez.Id);
        Assert.Equal(("Alvarez", "Beto", padron.PropioAlvarez.Dni), (alvarez.Apellido, alvarez.Nombre, alvarez.Dni));
    }

    [Fact]
    public async Task Con_TipoEmpleado_TraeSoloLosEmpleadosActivosSinFicha()
    {
        var padron = await PadronAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.BeneficiariosAsync("empleado");

        // Por apellido: Ruiz, Torres.
        Assert.Equal([padron.EmpleadaRuiz.Id, padron.EmpleadaTorres.Id], padron.Propias(respuesta));
    }

    [Fact]
    public async Task A_IgualApellidoYNombre_OrdenaPorId()
    {
        var primero = await app.EmpleadoAsync("Homonimo", "Igual");
        var segundo = await app.EmpleadoAsync("Homonimo", "Igual");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.BeneficiariosAsync("empleado");

        Assert.Equal(
            [primero.Id, segundo.Id],
            respuesta.Personas.Select(persona => persona.Id).Where(id => id == primero.Id || id == segundo.Id));
    }

    [Fact]
    public async Task Sin_EmpresaEmisora_NoOfreceChoferes_PeroSiEmpleados()
    {
        var padron = await PadronAsync();
        await app.SinEmpresaEmisoraAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var choferes = await cliente.BeneficiariosAsync("chofer");
        Assert.False(choferes.EmpresaEmisoraConfigurada);
        Assert.Empty(choferes.Personas);

        var empleados = await cliente.BeneficiariosAsync("empleado");
        Assert.False(empleados.EmpresaEmisoraConfigurada);
        Assert.Equal([padron.EmpleadaRuiz.Id, padron.EmpleadaTorres.Id], padron.Propias(empleados));
    }

    [Theory]
    [InlineData("/api/adelantos/beneficiarios")]
    [InlineData("/api/adelantos/beneficiarios?tipo=gerente")]
    public async Task Sin_TipoOConUnoDesconocido_Responde400(string ruta)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
        Assert.Equal("datos_invalidos", error.Codigo);
        Assert.Equal("tipo", error.Campo);
    }

    /// <summary>
    /// Lo que el desplegable ofrece y lo que el registro acepta no pueden separarse: para cada persona del
    /// padrón y cada tipo, aparece en <c>/beneficiarios</c> si y sólo si el registro responde <c>201</c>.
    /// </summary>
    [Fact]
    public async Task Es_LaMismaRegla_QueValidaElRegistro()
    {
        var padron = await PadronAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        foreach (var tipo in new[] { "chofer", "empleado" })
        {
            var ofrecidas = padron.Propias(await cliente.BeneficiariosAsync(tipo));

            foreach (var persona in padron.Todas)
            {
                var registro = await cliente.RegistrarAsync(tipo, persona.Id);

                Assert.True(
                    ofrecidas.Contains(persona.Id) == (registro.StatusCode == HttpStatusCode.Created),
                    $"{persona.Apellido} como {tipo}: ofrecida {ofrecidas.Contains(persona.Id)}, registro {(int)registro.StatusCode}");
            }
        }
    }
}
