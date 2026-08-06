using GT.Application.Autenticacion;
using Microsoft.Extensions.Caching.Memory;

namespace GT.Infrastructure.Seguridad;

/// <summary>
/// Implementación en memoria del límite de intentos fallidos (FR-021, research §4).
///
/// Es la pieza más chica que cumple el requisito. El limitador incorporado de ASP.NET Core no sirve
/// acá porque cuenta *todas* las peticiones y no sólo las fallidas: penalizaría a quien ingresa bien
/// varias veces seguidas.
///
/// Limitación aceptada: al vivir en memoria, el contador se reinicia si se reinicia el backend y no
/// se comparte entre instancias. Con una sola instancia y esperas de un minuto, el impacto es nulo.
/// Llevarlo a Redis sería generalizar antes de tener el segundo caso real (Principio I).
///
/// Las decisiones se toman con <see cref="TimeProvider"/> y no con el vencimiento del caché, para
/// que la lógica se pueda probar sin esperar minutos reales.
/// </summary>
public class ContadorIntentosFallidosEnMemoria(IMemoryCache cache, TimeProvider reloj)
    : IContadorIntentosFallidos
{
    private sealed class Registro
    {
        public int Fallos { get; set; }

        public DateTimeOffset VentanaDesde { get; set; }

        public DateTimeOffset? FrenadoHasta { get; set; }
    }

    public TimeSpan? TiempoDeEspera(string origen, string usernameNormalizado)
    {
        if (!cache.TryGetValue(Clave(origen, usernameNormalizado), out Registro? registro) ||
            registro?.FrenadoHasta is null)
        {
            return null;
        }

        var restante = registro.FrenadoHasta.Value - reloj.GetUtcNow();

        return restante > TimeSpan.Zero ? restante : null;
    }

    public void RegistrarFallo(string origen, string usernameNormalizado)
    {
        var ahora = reloj.GetUtcNow();
        var clave = Clave(origen, usernameNormalizado);

        var registro = cache.TryGetValue(clave, out Registro? existente) && existente is not null
            ? existente
            : new Registro { VentanaDesde = ahora };

        // La ventana de 5 minutos se reinicia sola: fallos viejos no se acumulan con los nuevos.
        if (ahora - registro.VentanaDesde > LimiteIntentos.Ventana)
        {
            registro.Fallos = 0;
            registro.VentanaDesde = ahora;
            registro.FrenadoHasta = null;
        }

        registro.Fallos++;

        if (registro.Fallos >= LimiteIntentos.FallosPermitidos)
        {
            registro.FrenadoHasta = ahora + LimiteIntentos.Espera;
            registro.Fallos = 0;
            registro.VentanaDesde = ahora;
        }

        Guardar(clave, registro);
    }

    public void RegistrarExito(string origen, string usernameNormalizado) =>
        cache.Remove(Clave(origen, usernameNormalizado));

    private void Guardar(string clave, Registro registro) =>
        cache.Set(clave, registro, new MemoryCacheEntryOptions
        {
            // Sólo acota la memoria; la lógica de vencimiento la decide el reloj, no el caché.
            AbsoluteExpirationRelativeToNow = LimiteIntentos.Ventana + LimiteIntentos.Espera,
        });

    /// <summary>Clave compuesta: el error de una persona no frena a las demás de la misma oficina.</summary>
    private static string Clave(string origen, string usernameNormalizado) =>
        $"intentos:{origen}|{usernameNormalizado}";
}
