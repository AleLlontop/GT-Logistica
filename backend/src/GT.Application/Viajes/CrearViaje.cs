using GT.Application.Viajes.Clientes;
using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Alta de un viaje (FR-012, FR-013, FR-015, FR-016, FR-032, FR-035).
///
/// Tres cosas que pasan acá y en ningún otro lado:
///
/// <list type="bullet">
///   <item>El viaje <b>nace <c>pendiente</c></b> (FR-032), y el estado no llega del cuerpo: no está
///   en el contrato de entrada.</item>
///   <item>Se escribe <b>la primera línea del historial</b> —<c>null → pendiente</c>—, en la misma
///   operación que el viaje. Es la única con <c>estadoAnterior</c> vacío: antes del alta no había
///   estado (FR-035).</item>
///   <item>El <b>número lo genera la base</b>. Este código no lo asigna ni podría: la propiedad tiene
///   <c>private set</c> y la columna un <c>DEFAULT</c> sobre la secuencia (FR-011).</item>
/// </list>
///
/// Las dos advertencias —origen igual a destino y carga retroactiva— <b>llegan con el resultado</b> y
/// no frenan el guardado: las dos se corrigen editando, así que no hay ningún paso extra que dar
/// (FR-015, FR-015a, FR-016).
/// </summary>
public class CrearViaje(
    IRepositorioViajes viajes,
    IRepositorioClientes clientes,
    TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        ViajeRequest peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (ValidadorViaje.PrimerCampoInvalido(peticion) is { } invalido)
        {
            return new ResultadoViaje(invalido.Error, Campo: invalido.Campo);
        }

        var cliente = await clientes.ObtenerPorIdAsync(peticion.ClienteId!.Value, cancelacion);

        // Tiene que estar **activo**: un cliente dado de baja deja de ofrecerse al registrar viajes,
        // y el servidor lo verifica igual aunque la pantalla no lo ofrezca (FR-008, FR-012).
        if (cliente is null || !cliente.Activo)
        {
            return new ResultadoViaje(ErrorViaje.ClienteInexistente, Campo: "clienteId");
        }

        var remito = Normalizar(peticion.NumeroRemito);

        if (remito is not null &&
            await viajes.ObtenerPorRemitoAsync(remito, cancelacion: cancelacion) is { } dueño)
        {
            return new ResultadoViaje(
                ErrorViaje.RemitoDuplicado,
                Campo: "numeroRemito",
                NumeroDeViajeRelacionado: dueño.Numero);
        }

        var momento = MomentoDeLectura.Desde(reloj);

        var viaje = new Viaje
        {
            ClienteId = cliente.Id,
            Fecha = peticion.Fecha!.Value,
            Origen = peticion.Origen!.Trim(),
            Destino = peticion.Destino!.Trim(),
            NumeroRemito = remito,
            DetalleCarga = Normalizar(peticion.DetalleCarga),
            Importe = peticion.Importe ?? 0m,
            Estado = EstadoViaje.Pendiente,
        };

        // El viaje y su primera línea de historial se agregan juntos y se guardan en un solo
        // `SaveChanges`, que EF envuelve en una transacción: no hay estado posible con el viaje
        // creado y el historial vacío (FR-035).
        await viajes.AgregarAsync(viaje, cancelacion);

        viaje.CambiosDeEstado.Add(new CambioDeEstadoViaje
        {
            ViajeId = viaje.Id,
            EstadoAnterior = null,
            EstadoNuevo = EstadoViaje.Pendiente,
            UsuarioId = usuarioId,
            OcurridoEn = momento.Ahora,
        });

        try
        {
            await viajes.GuardarCambiosAsync(cancelacion);
        }
        catch (RemitoDuplicadoException)
        {
            // Dos altas simultáneas con el mismo remito: la consulta previa las dejó pasar a las dos
            // y el índice único filtrado cortó la segunda (SC-003).
            var ganador = remito is null
                ? null
                : await viajes.ObtenerPorRemitoAsync(remito, cancelacion: cancelacion);

            return new ResultadoViaje(
                ErrorViaje.RemitoDuplicado,
                Campo: "numeroRemito",
                NumeroDeViajeRelacionado: ganador?.Numero);
        }

        viaje.Cliente = cliente;

        return new ResultadoViaje(
            ErrorViaje.Ninguno,
            ViajeDetalle.Desde(viaje, momento),
            AdvertenciasDeViaje.Al(viaje, momento),
            NumeroDelViaje: viaje.Numero);
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Las validaciones de campo que comparten el alta y la edición: "la edición aplica las mismas
/// validaciones que el alta" es un requisito (FR-017), y una sola escritura es lo que lo garantiza.
/// </summary>
public static class ValidadorViaje
{
    public static (ErrorViaje Error, string Campo)? PrimerCampoInvalido(ViajeRequest peticion)
    {
        if (peticion.ClienteId is null or <= 0)
        {
            return (ErrorViaje.DatosInvalidos, "clienteId");
        }

        // Sin límite de antigüedad ni de anticipación: el pasado es carga retroactiva y el futuro es
        // un viaje planificado, los dos válidos (FR-016).
        if (peticion.Fecha is null)
        {
            return (ErrorViaje.DatosInvalidos, "fecha");
        }

        if (string.IsNullOrWhiteSpace(peticion.Origen) || peticion.Origen.Trim().Length > 100)
        {
            return (ErrorViaje.DatosInvalidos, "origen");
        }

        if (string.IsNullOrWhiteSpace(peticion.Destino) || peticion.Destino.Trim().Length > 100)
        {
            return (ErrorViaje.DatosInvalidos, "destino");
        }

        if (peticion.NumeroRemito is { } remito && remito.Trim().Length > 50)
        {
            return (ErrorViaje.DatosInvalidos, "numeroRemito");
        }

        if (peticion.DetalleCarga is { } detalle && detalle.Trim().Length > 500)
        {
            return (ErrorViaje.DatosInvalidos, "detalleCarga");
        }

        // El cero es válido —viaje sin cargo o con importe todavía sin definir— y el negativo tiene
        // código propio, porque "revisá los campos marcados" no explica cuál es el problema (FR-013).
        if (peticion.Importe is { } importe && importe < 0m)
        {
            return (ErrorViaje.ImporteNegativo, "importe");
        }

        return null;
    }
}

/// <summary>
/// Las advertencias que acompañan al resultado del alta y de la edición (FR-015, FR-015a, FR-016).
///
/// Ninguna frena el guardado: las dos se corrigen editando el viaje, y el criterio para advertir con
/// el resultado en vez de exigir confirmación previa es la <b>reversibilidad</b>, no la gravedad
/// (research §5).
/// </summary>
public static class AdvertenciasDeViaje
{
    public static IReadOnlyList<Advertencia> Al(Viaje viaje, MomentoDeLectura momento)
    {
        var advertencias = new List<Advertencia>();

        // Sin distinguir mayúsculas: "Rosario" y "rosario" son la misma localidad para esta pregunta.
        if (string.Equals(viaje.Origen, viaje.Destino, StringComparison.OrdinalIgnoreCase))
        {
            advertencias.Add(new Advertencia(
                CodigosErrorViajes.OrigenIgualADestino,
                MensajesViajes.OrigenIgualADestino));
        }

        if (viaje.Fecha < momento.Hoy)
        {
            advertencias.Add(new Advertencia(
                CodigosErrorViajes.CargaRetroactiva,
                MensajesViajes.CargaRetroactiva));
        }

        return advertencias;
    }
}
