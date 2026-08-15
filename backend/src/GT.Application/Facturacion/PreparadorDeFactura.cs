using GT.Application.Facturacion.EmpresaEmisora;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;

namespace GT.Application.Facturacion;

/// <summary>
/// Valida el pedido y arma la <see cref="FacturaCliente"/> <b>en memoria</b>, con sus viajes cargados y
/// sus trece datos congelados.
///
/// <b>Existe porque la vista previa y la emisión tienen que armar exactamente la misma entidad</b>
/// (FR-033, research §2). Si cada camino la construyera por su cuenta, serían dos traducciones al mismo
/// destino que pueden diferir sin que nadie lo note — que es el problema que FR-033 quiere evitar, un
/// escalón más abajo. Con una sola preparación, revisar la vista previa sirve para algo.
///
/// La entidad que sale de acá <b>todavía no existe en la base</b> y no tiene <c>Id</c>: la vista previa
/// la renderiza y la descarta, y la emisión la persiste.
/// </summary>
public class PreparadorDeFactura(
    IRepositorioFacturas facturas,
    ConsultarEmpresaEmisora empresas,
    IRepositorioEmpresaEmisora repositorioEmpresas)
{
    /// <summary>Los años que el sistema acepta hoy (FR-010, spec §Assumptions).</summary>
    public static readonly int[] AniosValidos = [2025, 2026];

    /// <summary>La entidad lista para renderizar o para persistir, con los viajes que la componen.</summary>
    public record Preparacion(FacturaCliente Factura, IReadOnlyList<Viaje> Viajes);

    /// <param name="esVistaPrevia">
    /// Con <c>true</c> se saltean las dos verificaciones que son sobre el <b>estado persistido</b> y no
    /// sobre el contenido del documento: el número duplicado y la anulada ya reemplazada. Previsualizar
    /// no reserva el número, así que rechazar la vista previa por eso sería frenar antes de tiempo algo
    /// que quien opera todavía puede corregir (contracts/facturacion-api.yaml §vista-previa).
    /// </param>
    public async Task<(ResultadoFactura? Rechazo, Preparacion? Listo)> PrepararAsync(
        EmisionRequest peticion,
        bool esVistaPrevia,
        CancellationToken cancelacion = default)
    {
        if (PrimerCampoInvalido(peticion) is { } invalido)
        {
            return (Rechazo(invalido.Error, invalido.Campo, invalido.Mensaje), null);
        }

        // ── La empresa emisora, primero: sin ella no hay factura posible (FR-006) ────────────────
        var faltantes = await empresas.FaltantesAsync(cancelacion);

        if (faltantes.Count > 0)
        {
            return (new ResultadoFactura(
                ErrorFactura.EmpresaEmisoraIncompleta,
                Faltantes: faltantes,
                Mensaje: MensajesFacturas.EmpresaEmisoraIncompleta(faltantes)), null);
        }

        var emisor = (await repositorioEmpresas.ObtenerAsync(cancelacion))!;

        // ── El cliente (FR-011, FR-011a) ─────────────────────────────────────────────────────────
        var cliente = await facturas.ObtenerClienteAsync(peticion.ClienteId!.Value, cancelacion);

        if (cliente is null)
        {
            return (Rechazo(ErrorFactura.ClienteInexistente, "clienteId"), null);
        }

        if (!cliente.Activo)
        {
            return (new ResultadoFactura(
                ErrorFactura.ClienteInactivo,
                Campo: "clienteId",
                Mensaje: MensajesFacturas.ClienteInactivo(cliente.RazonSocial)), null);
        }

        // El domicilio sale impreso en el bloque del cliente del documento, así que sin él no hay nada
        // que imprimir. La columna tampoco admite nulo: la validación da el mensaje bueno y la columna
        // es la garantía (FR-011a).
        if (string.IsNullOrWhiteSpace(cliente.Direccion))
        {
            return (new ResultadoFactura(
                ErrorFactura.ClienteSinDomicilio,
                Campo: "clienteId",
                Faltantes: ["domicilio"],
                Mensaje: MensajesFacturas.ClienteSinDomicilio(cliente.RazonSocial)), null);
        }

        // ── Los viajes (FR-019a, FR-053) ─────────────────────────────────────────────────────────
        var viajeIds = peticion.ViajeIds!;
        var viajes = await facturas.ObtenerViajesAsync(viajeIds, cancelacion);

        if (viajes.Count != viajeIds.Count ||
            viajes.Any(viaje => viaje.ClienteId != cliente.Id))
        {
            // Un viaje que no existe o que es de otro cliente no llega desde la pantalla: la lista de
            // facturables ya filtró por cliente. Se rechaza igual, porque el endpoint se puede invocar
            // a mano (FR-024, SC-013).
            return (Rechazo(ErrorFactura.DatosInvalidos, "viajeIds"), null);
        }

        // Los sin remito se rechazan **nombrándolos uno por uno**: con ocho viajes elegidos, saber que
        // "uno" no tiene remito no alcanza para arreglarlo (FR-019a, convención [004]).
        var sinRemito = viajes
            .Where(viaje => string.IsNullOrWhiteSpace(viaje.NumeroRemito))
            .ToList();

        if (sinRemito.Count > 0)
        {
            return (new ResultadoFactura(
                ErrorFactura.ViajeSinRemito,
                Campo: "viajeIds",
                ViajesEnConflicto: [.. sinRemito.Select(viaje => new ViajeEnConflicto(
                    viaje.Id, viaje.Numero, nameof(MotivoViajeEnConflicto.SinRemito).EnCamelCase()))],
                Mensaje: MensajesFacturas.ViajeSinRemito(sinRemito[0].Numero)), null);
        }

        // 409 y no 400: el estado de algo compartido cambió entre que se armó la lista y se confirmó
        // (research §11). La lista de facturables ya los excluye, así que llegar acá significa que otro
        // operador se adelantó.
        var yaFacturados = viajes
            .Where(viaje => viaje.FacturaId is not null || viaje.Estado != EstadoViaje.Rendido)
            .ToList();

        if (yaFacturados.Count > 0)
        {
            var primero = yaFacturados[0];

            return (new ResultadoFactura(
                ErrorFactura.ViajeYaFacturado,
                ViajesEnConflicto: [.. yaFacturados.Select(viaje => new ViajeEnConflicto(
                    viaje.Id,
                    viaje.Numero,
                    nameof(MotivoViajeEnConflicto.YaFacturado).EnCamelCase()))],
                FacturaEnConflicto: primero.Factura is { } enConflicto
                    ? ResumenDe(enConflicto)
                    : null,
                Mensaje: MensajesFacturas.ViajeYaFacturado(
                    primero.Numero,
                    primero.Factura?.NumeroComprobante ?? "otro comprobante")), null);
        }

        // ── Refacturación (FR-049, FR-049a) ──────────────────────────────────────────────────────
        var tipoFacturacion = NombresDeEstadoFactura.LeerTipoFacturacion(peticion.TipoFacturacion)!.Value;
        FacturaCliente? reemplazada = null;

        if (tipoFacturacion is TipoFacturacion.Refacturacion)
        {
            if (peticion.FacturaReemplazadaId is not { } reemplazadaId)
            {
                return (Rechazo(ErrorFactura.RefacturacionSinReemplazada, "facturaReemplazadaId"), null);
            }

            reemplazada = await facturas.ObtenerFichaAsync(reemplazadaId, cancelacion);

            if (reemplazada is null ||
                reemplazada.ClienteId != cliente.Id ||
                reemplazada.Estado != EstadoFactura.Anulada)
            {
                return (Rechazo(ErrorFactura.DatosInvalidos, "facturaReemplazadaId"), null);
            }

            if (!esVistaPrevia &&
                await facturas.ObtenerQueLaReemplazaAsync(reemplazadaId, cancelacion) is { } otra)
            {
                return (new ResultadoFactura(
                    ErrorFactura.AnuladaYaReemplazada,
                    Campo: "facturaReemplazadaId",
                    FacturaEnConflicto: ResumenDe(otra),
                    Mensaje: MensajesFacturas.AnuladaYaReemplazada(
                        reemplazada.NumeroComprobante,
                        otra.NumeroComprobante)), null);
            }
        }
        else if (peticion.FacturaReemplazadaId is not null)
        {
            // Una Original no reemplaza a nadie. Se rechaza en vez de ignorar el campo: ignorarlo
            // guardaría una factura distinta de la que se pidió (FR-049).
            return (Rechazo(ErrorFactura.OriginalConReemplazada, "facturaReemplazadaId"), null);
        }

        // ── El número (FR-027) ───────────────────────────────────────────────────────────────────
        if (!esVistaPrevia &&
            await facturas.ObtenerPorNumeroAsync(peticion.NumeroComprobante!.Trim(), cancelacion)
                is { } dueña)
        {
            return (new ResultadoFactura(
                ErrorFactura.NumeroDuplicado,
                Campo: "numeroComprobante",
                FacturaEnConflicto: ResumenDe(dueña),
                Mensaje: MensajesFacturas.NumeroDuplicado(
                    dueña.NumeroComprobante,
                    FormatoDeDocumento.Fecha(dueña.Fecha),
                    dueña.ClienteRazonSocial)), null);
        }

        // ── El armado ────────────────────────────────────────────────────────────────────────────
        var tipoComprobante = NombresDeEstadoFactura.LeerTipoComprobante(peticion.TipoComprobante)!.Value;

        // **Los importes se calculan a partir de los viajes leídos de la base**, no del cuerpo: los
        // campos `neto`, `iva` y `total` no existen en `EmisionRequest`, así que no hay forma de
        // mandarlos ni desde la pantalla ni invocando la acción a mano (FR-024).
        var importes = CalculadorImportes.Calcular(
            viajes.Select(viaje => viaje.Importe),
            tipoComprobante);

        var factura = new FacturaCliente
        {
            NumeroComprobante = peticion.NumeroComprobante!.Trim(),
            Fecha = peticion.Fecha!.Value,
            TipoComprobante = tipoComprobante,
            TipoFacturacion = tipoFacturacion,
            CondicionDeVenta = NombresDeEstadoFactura
                .LeerCondicionDeVenta(peticion.CondicionDeVenta)!.Value,
            PeriodoMes = (byte)peticion.Mes!.Value,
            PeriodoAnio = (short)peticion.Anio!.Value,
            Detalle = string.IsNullOrWhiteSpace(peticion.Detalle) ? null : peticion.Detalle.Trim(),

            // Los tres del cliente: copia **y** referencia. Ninguno reemplaza al otro (FR-034a).
            ClienteId = cliente.Id,
            ClienteRazonSocial = cliente.RazonSocial,
            ClienteCuit = cliente.Cuit,
            ClienteDomicilio = cliente.Direccion!,

            // Los diez del emisor: sólo copia, nunca releídos de la configuración (FR-034).
            EmisorRazonSocial = emisor.RazonSocial,
            EmisorCuit = emisor.Cuit,
            EmisorDomicilio = emisor.Domicilio,
            EmisorCondicionIva = emisor.CondicionIva,
            EmisorIngresosBrutos = emisor.IngresosBrutos,
            EmisorInicioActividades = emisor.InicioActividades,
            EmisorPuntoDeVenta = emisor.PuntoDeVenta,
            EmisorCbu = emisor.Cbu,
            EmisorTelefono = emisor.Telefono,
            EmisorEmail = emisor.Email,

            Neto = importes.Neto,
            Iva = importes.Iva,
            Total = importes.Total,

            Cae = peticion.Cae!.Trim(),
            CaeVencimiento = peticion.CaeVencimiento!.Value,
            VencimientoPago = peticion.VencimientoPago!.Value,

            Estado = EstadoFactura.Pendiente,
            FacturaReemplazadaId = reemplazada?.Id,

            // Se completa al escribir el PDF, antes de abrir la transacción. La vista previa nunca lo
            // usa: renderiza y descarta (FR-033).
            DocumentoRuta = string.Empty,
        };

        // El detalle del documento sale de esta colección (FR-031e). La emisión la vacía antes de
        // persistir: el vínculo lo establece el `UPDATE` condicional de la transacción.
        foreach (var viaje in viajes)
        {
            factura.Viajes.Add(viaje);
        }

        return (null, new Preparacion(factura, viajes));
    }

    public static FacturaResumen ResumenDe(FacturaCliente factura) => new(
        factura.Id,
        factura.NumeroComprobante,
        factura.Fecha.ToString("yyyy-MM-dd"),
        NombresDeEstadoFactura.EnJson(factura.Estado switch
        {
            EstadoFactura.Pagada => EstadoFacturaVisible.Pagada,
            EstadoFactura.Anulada => EstadoFacturaVisible.Anulada,
            _ => EstadoFacturaVisible.Pendiente,
        }));

    private static ResultadoFactura Rechazo(
        ErrorFactura error,
        string? campo = null,
        string? mensaje = null) =>
        new(error, Campo: campo, Mensaje: mensaje);

    /// <summary>
    /// Las validaciones de campo, que <b>comparten la vista previa y la emisión</b>: "la vista previa
    /// aplica las mismas validaciones de datos que la emisión" es un requisito, y una sola escritura es
    /// lo que lo garantiza (contracts/facturacion-api.yaml §vista-previa).
    /// </summary>
    private static (ErrorFactura Error, string Campo, string? Mensaje)? PrimerCampoInvalido(
        EmisionRequest peticion)
    {
        if (peticion.ClienteId is null or <= 0) return Invalido("clienteId");

        if (NombresDeEstadoFactura.LeerTipoComprobante(peticion.TipoComprobante) is null)
        {
            return Invalido("tipoComprobante");
        }

        if (NombresDeEstadoFactura.LeerTipoFacturacion(peticion.TipoFacturacion) is null)
        {
            return Invalido("tipoFacturacion");
        }

        if (NombresDeEstadoFactura.LeerCondicionDeVenta(peticion.CondicionDeVenta) is null)
        {
            return Invalido("condicionDeVenta");
        }

        if (peticion.Mes is null or < 1 or > 12) return Invalido("mes");

        // El año se valida acá y **no con un `CHECK` en la base**: la lista se amplía con el tiempo y
        // una restricción de base obligaría a una migración cada vez (spec §Assumptions).
        if (peticion.Anio is not { } anio || !AniosValidos.Contains(anio)) return Invalido("anio");

        if (peticion.Fecha is null) return Invalido("fecha");

        if (!NumeroDeComprobante.EsValido(peticion.NumeroComprobante))
        {
            return (ErrorFactura.NumeroInvalido, "numeroComprobante", MensajesFacturas.NumeroInvalido);
        }

        if (peticion.Detalle is { } detalle && detalle.Trim().Length > 500) return Invalido("detalle");

        // El CAE es obligatorio para dar por emitida la factura: sin él no hay comprobante válido
        // (FR-028).
        if (string.IsNullOrWhiteSpace(peticion.Cae) || peticion.Cae.Trim().Length > 20)
        {
            return Invalido("cae");
        }

        if (peticion.CaeVencimiento is null) return Invalido("caeVencimiento");
        if (peticion.VencimientoPago is null) return Invalido("vencimientoPago");

        // FR-029 y FR-030: ninguno de los dos plazos puede ser anterior a la fecha de facturación. Son
        // dos plazos distintos y sólo el de pago mueve la factura a `vencida` (FR-041).
        if (peticion.CaeVencimiento < peticion.Fecha)
        {
            return (ErrorFactura.CaeVencimientoAnterior, "caeVencimiento", null);
        }

        if (peticion.VencimientoPago < peticion.Fecha)
        {
            return (ErrorFactura.VencimientoPagoAnterior, "vencimientoPago", null);
        }

        if (peticion.ViajeIds is null or { Count: 0 })
        {
            return (ErrorFactura.SinViajesSeleccionados, "viajeIds", null);
        }

        // Ids repetidos harían que el `UPDATE` condicional afectara menos filas que las pedidas y el
        // rechazo culparía a un viaje que en realidad estaba disponible.
        if (peticion.ViajeIds.Distinct().Count() != peticion.ViajeIds.Count)
        {
            return Invalido("viajeIds");
        }

        return null;
    }

    private static (ErrorFactura, string, string?) Invalido(string campo) =>
        (ErrorFactura.DatosInvalidos, campo, MensajesFacturas.DatosInvalidos);
}

internal static class TextoEnJson
{
    /// <summary>
    /// El nombre de un valor de enum como viaja en el JSON: <c>SinRemito</c> → <c>sinRemito</c>
    /// (convención [003]).
    /// </summary>
    public static string EnCamelCase(this string nombre) =>
        string.IsNullOrEmpty(nombre) ? nombre : char.ToLowerInvariant(nombre[0]) + nombre[1..];
}
