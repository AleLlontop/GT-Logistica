using GT.Domain.Viajes;

namespace GT.UnitTests.Viajes;

/// <summary>
/// Cubre FR-039: el borde exacto de los cinco días corridos.
///
/// <b>Es uno de los dos requisitos que no se pueden verificar a mano</b>: comprobarlo operando la app
/// exigiría poner un viaje en curso y esperar cinco días. La regla recibe el instante por parámetro
/// justamente para poder fijarlo acá (plan §Principio IV, quickstart paso 22).
/// </summary>
public class DemoraViajeTests
{
    private static readonly DateTime EnCursoDesde = new(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ALosCincoDiasExactos_TodaviaNoEstaDemorado()
    {
        // "Más de 5 días", no "5 o más": a las 120 horas justas el viaje todavía está en tiempo.
        var ahora = EnCursoDesde.AddDays(Viaje.DiasParaDemora);

        Assert.False(Viaje.EstaDemorado(EnCursoDesde, ahora));
    }

    [Fact]
    public void PasadosLosCincoDias_EstaDemorado()
    {
        var ahora = EnCursoDesde.AddDays(Viaje.DiasParaDemora).AddSeconds(1);

        Assert.True(Viaje.EstaDemorado(EnCursoDesde, ahora));
    }

    [Fact]
    public void ReciénPuestoEnCurso_NoEstaDemorado() =>
        Assert.False(Viaje.EstaDemorado(EnCursoDesde, EnCursoDesde));

    [Fact]
    public void UnViajeQueNuncaArranco_NoPuedeEstarDemorado()
    {
        // Sin fila de `pendiente → en curso` en el historial no hay instante del que contar, y un
        // viaje pendiente no se demora por más vieja que sea su fecha.
        Assert.False(Viaje.EstaDemorado(null, EnCursoDesde.AddYears(1)));
    }

    [Fact]
    public void UnMesEnCurso_SigueDemorado()
    {
        // El caso que FR-039 declara plausible: el viaje cuya fecha pasó hace meses y nunca se rindió.
        Assert.True(Viaje.EstaDemorado(EnCursoDesde, EnCursoDesde.AddMonths(1)));
    }

    [Fact]
    public void LaDemoraNoCambiaElEstadoGuardado()
    {
        // `demorado` es una señal derivada, no un quinto valor de EstadoViaje: el sistema no le cambia
        // el estado a ningún viaje por sí solo (FR-039, data-model §Demorado).
        var viaje = new Viaje
        {
            ClienteId = 1,
            Fecha = new DateOnly(2026, 8, 1),
            Origen = "Rosario",
            Destino = "Córdoba",
            Estado = EstadoViaje.EnCurso,
        };

        Assert.True(Viaje.EstaDemorado(EnCursoDesde, EnCursoDesde.AddDays(30)));
        Assert.Equal(EstadoViaje.EnCurso, viaje.Estado);
    }
}
