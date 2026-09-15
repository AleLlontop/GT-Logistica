using GT.Domain.Adelantos;
using GT.Infrastructure.Persistencia.Configuraciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Las garantías que viven en la base, verificadas fila por fila y <b>sin pasar por los casos de uso</b>:
/// con la validación en el medio, un <c>CHECK</c> mal escrito pasaría inadvertido.
///
/// Cada fila inválida viola <b>una sola</b> restricción, porque con dos SQL Server nombra una cualquiera
/// (convención [009]). Existe porque los <c>CHECK</c> llevan valores de enum escritos a mano y porque
/// <c>LEN(NULL) &gt; 0</c> deja pasar la fila si no va detrás de su <c>IS NOT NULL</c> (research §9.1, §9.2).
/// </summary>
public class RestriccionesDeAdelantoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(EstadoAdelanto.Pendiente, null, null)]
    [InlineData(EstadoAdelanto.Aprobado, null, null)]
    [InlineData(EstadoAdelanto.Rechazado, "Ya tiene otro.", null)]
    [InlineData(EstadoAdelanto.Anulado, null, "Persona equivocada.")]
    public async Task El_CheckDelEstado_AceptaCadaRamaValida(
        EstadoAdelanto estado,
        string? motivoRechazo,
        string? motivoAnulacion)
    {
        Assert.Null(await IntentarInsertarAsync(estado, motivoRechazo, motivoAnulacion));
    }

    [Theory]
    [InlineData(EstadoAdelanto.Pendiente, "Motivo.", null)] // pendiente con motivo de rechazo
    [InlineData(EstadoAdelanto.Aprobado, null, "Motivo.")] // aprobado con motivo de anulación
    [InlineData(EstadoAdelanto.Rechazado, null, null)] // rechazado sin motivo: el caso de `LEN(NULL)`
    [InlineData(EstadoAdelanto.Rechazado, "   ", null)] // rechazado con motivo de sólo espacios
    [InlineData(EstadoAdelanto.Anulado, null, null)] // anulado sin motivo
    [InlineData(EstadoAdelanto.Anulado, null, "   ")] // anulado con motivo de sólo espacios
    [InlineData(EstadoAdelanto.Anulado, "Motivo.", "Motivo.")] // anulado con los dos motivos
    public async Task El_CheckDelEstado_RechazaCadaFilaInvalida(
        EstadoAdelanto estado,
        string? motivoRechazo,
        string? motivoAnulacion)
    {
        var error = await IntentarInsertarAsync(estado, motivoRechazo, motivoAnulacion);

        Assert.NotNull(error);
        Assert.Contains(AdelantoConfiguracion.CheckEstado, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_TipoDeBeneficiarioDesconocido_SeRechaza()
    {
        var error = await IntentarInsertarAsync(EstadoAdelanto.Pendiente, null, null, tipo: (TipoBeneficiario)3);

        Assert.NotNull(error);
        Assert.Contains(AdelantoConfiguracion.CheckTipoBeneficiario, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_ImporteEnCero_SeRechaza()
    {
        var error = await IntentarInsertarAsync(EstadoAdelanto.Pendiente, null, null, importe: 0m);

        Assert.NotNull(error);
        Assert.Contains(AdelantoConfiguracion.CheckImporte, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_MotivoDeSoloEspacios_SeRechaza()
    {
        var error = await IntentarInsertarAsync(EstadoAdelanto.Pendiente, null, null, motivo: "   ");

        Assert.NotNull(error);
        Assert.Contains(AdelantoConfiguracion.CheckMotivo, error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_IndiceDeOperaciones_RechazaUnaSegundaAprobacionDelMismoAdelanto()
    {
        var persona = await app.EmpleadoAsync();
        var adelanto = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado);
        var administrador = await app.ObtenerAdministradorAsync();

        var choque = await Assert.ThrowsAsync<DbUpdateException>(() => app.EnLaBaseAsync(async contexto =>
        {
            contexto.CambiosDeAdelanto.Add(new CambioDeAdelanto
            {
                AdelantoId = adelanto.Id,
                Operacion = OperacionDeAdelanto.Aprobacion,
                UsuarioId = administrador.Id,
                OcurridoEn = DateTime.UtcNow,
            });

            await contexto.SaveChangesAsync();
        }));

        Assert.Contains(
            CambioDeAdelantoConfiguracion.IndiceOperacion,
            choque.InnerException!.Message,
            StringComparison.Ordinal);
    }

    private async Task<string?> IntentarInsertarAsync(
        EstadoAdelanto estado,
        string? motivoRechazo,
        string? motivoAnulacion,
        TipoBeneficiario tipo = TipoBeneficiario.Empleado,
        decimal importe = 150_000m,
        string motivo = "Gastos médicos")
    {
        var persona = await app.EmpleadoAsync();

        try
        {
            await app.EnLaBaseAsync(async contexto =>
            {
                contexto.Adelantos.Add(new Adelanto
                {
                    PersonaId = persona.Id,
                    TipoBeneficiario = tipo,
                    Fecha = DatosDePruebaAdelantos.HoyEnArgentina,
                    Motivo = motivo,
                    Importe = importe,
                    Estado = estado,
                    MotivoRechazo = motivoRechazo,
                    MotivoAnulacion = motivoAnulacion,
                });

                await contexto.SaveChangesAsync();
            });

            return null;
        }
        catch (DbUpdateException excepcion)
        {
            return excepcion.InnerException?.Message ?? excepcion.Message;
        }
    }
}
