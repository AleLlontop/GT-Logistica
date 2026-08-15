using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;

namespace GT.Application.Facturacion;

/// <summary>
/// La operación central del módulo: emitir una factura agrupando viajes rendidos (FR-014, FR-054).
///
/// <b>O pasa todo o no pasa nada.</b> Se crea la factura, se marcan sus viajes como <c>facturado</c> y
/// se genera y guarda el documento en PDF, en una sola operación. Si el documento no se puede generar,
/// la emisión se rechaza entera: no se crea la factura, los viajes quedan en <c>rendido</c> y el número
/// de comprobante queda libre (FR-031, SC-007a).
///
/// <b>El orden entre disco y base sigue la convención [003], con una vuelta de tuerca</b> (research §6):
///
/// <list type="number">
///   <item>Se valida todo y se piden las confirmaciones. Nada se toca hasta acá.</item>
///   <item>Se arma el PDF y se escribe en disco <b>antes</b> de abrir la transacción. No necesita el
///   <c>Id</c> de la factura —el número lo tipea el usuario—, así que nada obliga a escribirlo
///   después.</item>
///   <item>La transacción inserta la factura con la ruta ya puesta, marca los viajes y confirma.</item>
///   <item>Si algo falla, se borra el archivo recién escrito.</item>
/// </list>
///
/// Deja como único estado roto posible un archivo huérfano en el volumen —invisible para quien opera—
/// y nunca una factura que dice tener documento sin tenerlo.
/// </summary>
public class EmitirFactura(
    IRepositorioFacturas facturas,
    PreparadorDeFactura preparador,
    IArmadorDocumentoFactura armador,
    GestionarLogo logo,
    IAlmacenDeArchivos almacen,
    ConsultarFichaFactura fichas,
    TimeProvider reloj)
{
    public async Task<ResultadoFactura> EjecutarAsync(
        EmisionRequest peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var (rechazo, listo) = await preparador.PrepararAsync(
            peticion,
            esVistaPrevia: false,
            cancelacion);

        if (rechazo is not null)
        {
            return rechazo;
        }

        var (factura, viajes) = listo!;

        // ── Las dos confirmaciones previas de FR-032 ─────────────────────────────────────────────
        // Viven en el backend y no en la pantalla, por el criterio de la convención [005]: **la emisión
        // no se deshace**. Una vez emitida, la factura no cambia de importes y sólo se corrige
        // anulándola. El primer intento responde 409 **sin crear nada** y el segundo lleva
        // `confirmado: true` (research §11).
        if (peticion.Confirmado != true &&
            ConfirmacionPendiente(factura, viajes) is { } confirmacion)
        {
            return confirmacion;
        }

        // ── El documento, antes de la transacción ─────────────────────────────────────────────────
        var datos = DatosDelDocumento.Desde(factura, await logo.ParaElDocumentoAsync(cancelacion));

        byte[] contenido;

        try
        {
            contenido = armador.Armar(datos);
        }
        catch (DocumentoNoGeneradoException)
        {
            // FR-031: la emisión se rechaza entera. Nada se creó todavía, así que no hay nada que
            // deshacer y el número queda libre.
            return new ResultadoFactura(
                ErrorFactura.DatosInvalidos,
                Mensaje: "No pudimos generar el documento de la factura. No se emitió nada; volvé a " +
                    "intentar en unos minutos.");
        }

        string ruta;

        try
        {
            await using var flujo = new MemoryStream(contenido);
            ruta = await almacen.GuardarAsync(flujo, cancelacion);
        }
        catch (ArchivoNoGuardadoException)
        {
            return new ResultadoFactura(
                ErrorFactura.ArchivoNoGuardado,
                Mensaje: "No pudimos guardar el documento de la factura. No se emitió nada; volvé a " +
                    "intentar en unos minutos.");
        }

        factura.DocumentoRuta = ruta;

        // ── La transacción ───────────────────────────────────────────────────────────────────────
        var viajeIds = viajes.Select(viaje => viaje.Id).ToList();
        var ahora = reloj.GetUtcNow().UtcDateTime;

        try
        {
            var resultado = await facturas.EmitirAsync(
                factura,
                viajeIds,
                usuarioId,
                ahora,
                cancelacion);

            if (!resultado.Exitoso)
            {
                // El `UPDATE` condicional afectó menos filas que las pedidas: otro operador se adelantó
                // con alguno de los viajes. La transacción ya se deshizo (research §4).
                await BorrarAsync(ruta);

                return RechazoPorViajesTomados(resultado.ViajesTomados);
            }
        }
        catch (NumeroDuplicadoException)
        {
            // La carrera por el número: la consulta previa dejó pasar a las dos emisiones y el índice
            // único filtrado cortó la segunda (FR-027, convención [003]).
            await BorrarAsync(ruta);

            var dueña = await facturas.ObtenerPorNumeroAsync(factura.NumeroComprobante, cancelacion);

            return new ResultadoFactura(
                ErrorFactura.NumeroDuplicado,
                Campo: "numeroComprobante",
                FacturaEnConflicto: dueña is null ? null : PreparadorDeFactura.ResumenDe(dueña),
                Mensaje: dueña is null
                    ? MensajesFacturas.DatosInvalidos
                    : MensajesFacturas.NumeroDuplicado(
                        dueña.NumeroComprobante,
                        FormatoDeDocumento.Fecha(dueña.Fecha),
                        dueña.ClienteRazonSocial));
        }
        catch (AnuladaYaReemplazadaException)
        {
            // La carrera por la refacturación: dos Refacturaciones simultáneas de la misma anulada
            // (FR-049a).
            await BorrarAsync(ruta);

            var otra = await facturas.ObtenerQueLaReemplazaAsync(
                factura.FacturaReemplazadaId!.Value,
                cancelacion);

            return new ResultadoFactura(
                ErrorFactura.AnuladaYaReemplazada,
                Campo: "facturaReemplazadaId",
                FacturaEnConflicto: otra is null ? null : PreparadorDeFactura.ResumenDe(otra),
                Mensaje: MensajesFacturas.AnuladaYaReemplazada(
                    "anulada",
                    otra?.NumeroComprobante ?? "otra Refacturación"));
        }
        catch
        {
            // Cualquier otra falla de la transacción: el archivo ya está escrito y la factura no llegó a
            // existir. Se compensa borrándolo, que es lo que deja el estado roto aceptable en vez del
            // prohibido (convención [003]).
            await BorrarAsync(ruta);

            throw;
        }

        var ficha = await fichas.EjecutarAsync(factura.Id, cancelacion);

        return new ResultadoFactura(
            ErrorFactura.Ninguno,
            ficha,
            Mensaje: MensajesFacturas.FacturaEmitida(factura.NumeroComprobante, viajeIds.Count));
    }

    /// <summary>
    /// Las dos situaciones de FR-032, en el orden en que la pantalla las muestra.
    ///
    /// El rechazo <b>nombra el viaje puntual</b> —qué viaje tiene importe cero, qué viaje es posterior a
    /// la fecha— y no dice "hay un viaje": con ocho viajes elegidos, la diferencia es entre poder
    /// revisarlo y tener que buscarlo.
    /// </summary>
    private static ResultadoFactura? ConfirmacionPendiente(
        FacturaCliente factura,
        IReadOnlyList<Viaje> viajes)
    {
        if (viajes.FirstOrDefault(viaje => viaje.Importe == 0m) is { } enCero)
        {
            return new ResultadoFactura(
                ErrorFactura.EmisionRequiereConfirmacion,
                MotivoConfirmacion: MotivoConfirmacion.ViajeEnCero,
                ViajesEnConflicto:
                [
                    new ViajeEnConflicto(
                        enCero.Id,
                        enCero.Numero,
                        nameof(MotivoViajeEnConflicto.ImporteEnCero).EnCamelCase()),
                ],
                Mensaje: MensajesFacturas.ConfirmarViajeEnCero(enCero.Numero));
        }

        if (viajes.FirstOrDefault(viaje => viaje.Fecha > factura.Fecha) is { } posterior)
        {
            return new ResultadoFactura(
                ErrorFactura.EmisionRequiereConfirmacion,
                MotivoConfirmacion: MotivoConfirmacion.FechaAnteriorAViaje,
                ViajesEnConflicto:
                [
                    new ViajeEnConflicto(
                        posterior.Id,
                        posterior.Numero,
                        nameof(MotivoViajeEnConflicto.PosteriorAlaFactura).EnCamelCase()),
                ],
                Mensaje: MensajesFacturas.ConfirmarFechaAnteriorAViaje(
                    posterior.Numero,
                    FormatoDeDocumento.Fecha(posterior.Fecha),
                    FormatoDeDocumento.Fecha(factura.Fecha)));
        }

        return null;
    }

    private static ResultadoFactura RechazoPorViajesTomados(IReadOnlyList<ViajeTomado> tomados)
    {
        var primero = tomados.Count > 0 ? tomados[0] : null;

        return new ResultadoFactura(
            ErrorFactura.ViajeYaFacturado,
            ViajesEnConflicto: [.. tomados.Select(viaje => new ViajeEnConflicto(
                viaje.Id,
                viaje.Numero,
                nameof(MotivoViajeEnConflicto.YaFacturado).EnCamelCase()))],
            Mensaje: primero is null
                ? MensajesFacturas.DatosInvalidos
                : MensajesFacturas.ViajeYaFacturado(
                    primero.Numero,
                    primero.NumeroDeFactura ?? "otro comprobante"));
    }

    /// <summary>
    /// Borra el PDF huérfano. <c>CancellationToken.None</c> a propósito: si la petición se canceló, la
    /// compensación tiene que correr igual — si no, el archivo queda para siempre.
    /// </summary>
    private Task BorrarAsync(string ruta) => almacen.BorrarAsync(ruta, CancellationToken.None);
}
