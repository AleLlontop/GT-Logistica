using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Ver el detalle de un adelanto (User Story 3, FR-019, FR-020, FR-036).
///
/// <b>Nace en verde, y se declara</b>: el detalle ya lo implementan el repositorio, el caso de uso y el
/// endpoint de la historia 1. Lo que protege es que siga cumpliendo FR-019 y FR-020.
/// </summary>
public class DetalleAdelantoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Muestra_LaPersona_ElTipo_YLosDatosDelAdelanto()
    {
        var juan = await app.ChoferPropioAsync("Pérez", "Juan");
        var adelanto = await app.CrearAdelantoAsync(
            juan.Id,
            tipo: TipoBeneficiario.Chofer,
            importe: 150_000m,
            fecha: new DateOnly(2026, 9, 14),
            motivo: "Gastos médicos");

        var detalle = await LeerAsync(adelanto.Id);

        Assert.Equal(new PersonaResumenLeida(juan.Id, "Pérez", "Juan", juan.Dni), detalle.Persona);
        Assert.Equal("chofer", detalle.Tipo);
        Assert.Equal("2026-09-14", detalle.Fecha);
        Assert.Equal("Gastos médicos", detalle.Motivo);
        Assert.Equal(150_000m, detalle.Importe);
        Assert.Equal("pendiente", detalle.Estado);
        Assert.Null(detalle.MotivoRechazo);
        Assert.Null(detalle.MotivoAnulacion);
    }

    [Fact]
    public async Task Un_Rechazado_TraeSuMotivo_EnElDetalleYEnSuEntrada()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(
            persona.Id,
            EstadoAdelanto.Rechazado,
            motivoDeCierre: "Ya tiene un adelanto pendiente del mes anterior.");

        var detalle = await LeerAsync(adelanto.Id);

        Assert.Equal("rechazado", detalle.Estado);
        Assert.Equal("Ya tiene un adelanto pendiente del mes anterior.", detalle.MotivoRechazo);
        Assert.Null(detalle.MotivoAnulacion);
        Assert.Equal(
            [("registro", null), ("rechazo", "Ya tiene un adelanto pendiente del mes anterior.")],
            detalle.Historial.Select(entrada => (entrada.Operacion, entrada.Motivo)));
    }

    [Fact]
    public async Task Un_RegistradoAprobadoYAnulado_TraeLasTresOperacionesEnOrden_ConUsuarioEInstante()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(
            persona.Id,
            EstadoAdelanto.Anulado,
            motivoDeCierre: "Cargado sobre la persona equivocada.");

        var detalle = await LeerAsync(adelanto.Id);

        Assert.Equal("anulado", detalle.Estado);
        Assert.Equal("Cargado sobre la persona equivocada.", detalle.MotivoAnulacion);
        Assert.Null(detalle.MotivoRechazo);

        Assert.Equal(
            [("registro", null), ("aprobacion", null), ("anulacion", "Cargado sobre la persona equivocada.")],
            detalle.Historial.Select(entrada => (entrada.Operacion, entrada.Motivo)));

        Assert.All(detalle.Historial, entrada =>
        {
            Assert.Equal("admin", entrada.Usuario);
            Assert.Equal(DateTimeKind.Utc, entrada.OcurridoEn.Kind);
        });

        Assert.Equal(
            detalle.Historial.Select(entrada => entrada.OcurridoEn).Order(),
            detalle.Historial.Select(entrada => entrada.OcurridoEn));
    }

    [Fact]
    public async Task El_Apellido_EsElVigenteDelPadron()
    {
        var persona = await app.EmpleadoAsync("Pérez", "Juan");
        var adelanto = await app.CrearAdelantoAsync(persona.Id);

        await app.CorregirApellidoAsync(persona.Id, "Pérez Gil");

        Assert.Equal("Pérez Gil", (await LeerAsync(adelanto.Id)).Persona.Apellido);
    }

    [Fact]
    public async Task El_Tipo_SigueSiendoChofer_DespuesDeDarDeBajaLaFicha()
    {
        var juan = await app.ChoferPropioAsync();
        var adelanto = await app.CrearAdelantoAsync(juan.Id, tipo: TipoBeneficiario.Chofer);

        await app.DarDeBajaFichaAsync(juan.Id);

        Assert.Equal("chofer", (await LeerAsync(adelanto.Id)).Tipo);
    }

    [Theory]
    [InlineData(EstadoAdelanto.Pendiente, true, false)]
    [InlineData(EstadoAdelanto.Aprobado, false, true)]
    [InlineData(EstadoAdelanto.Rechazado, false, false)]
    [InlineData(EstadoAdelanto.Anulado, false, false)]
    public async Task Que_SePuedeHacer_SaleDelEstado(EstadoAdelanto estado, bool puedeResolverse, bool puedeAnularse)
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id, estado);

        var detalle = await LeerAsync(adelanto.Id);

        Assert.Equal(puedeResolverse, detalle.PuedeResolverse);
        Assert.Equal(puedeAnularse, detalle.PuedeAnularse);
    }

    [Fact]
    public async Task Un_Inexistente_Responde404()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/adelantos/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal(
            "adelanto_no_encontrado",
            (await respuesta.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!.Codigo);
    }

    private async Task<AdelantoDetalleLeido> LeerAsync(int id)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        return (await cliente.GetFromJsonAsync<AdelantoDetalleLeido>($"/api/adelantos/{id}"))!;
    }
}
