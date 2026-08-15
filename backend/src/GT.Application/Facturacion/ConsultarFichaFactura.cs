using GT.Domain.Facturacion;

namespace GT.Application.Facturacion;

/// <summary>
/// Ficha completa de una factura (FR-060).
///
/// Devuelve los datos del emisor y del cliente <b>tal como quedaron al emitirla</b>, el detalle de los
/// viajes incluidos, los tres importes con su alícuota, el CAE y los vencimientos, la fecha de cobro y
/// el motivo de anulación cuando corresponda, el historial completo y <b>las dos direcciones</b> de la
/// referencia de refacturación.
///
/// <b>La segunda dirección se resuelve con una consulta</b> —qué Refacturación reemplazó a esta
/// anulada— y no con una columna espejo que habría que mantener sincronizada y podría discrepar del
/// dato que ya está (FR-050).
/// </summary>
public class ConsultarFichaFactura(IRepositorioFacturas facturas, TimeProvider reloj)
{
    public async Task<FacturaDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var factura = await facturas.ObtenerFichaAsync(id, cancelacion);

        if (factura is null)
        {
            return null;
        }

        // Sólo se busca la otra dirección si esta factura está anulada: una vigente no puede haber sido
        // reemplazada, así que la consulta no tendría a quién encontrar (FR-050).
        var reemplazadaPor = factura.Estado is EstadoFactura.Anulada
            ? await facturas.ObtenerQueLaReemplazaAsync(id, cancelacion)
            : null;

        return Armar(factura, reemplazadaPor, Domain.Choferes.FechaHoyArgentina.Desde(reloj.GetUtcNow()));
    }

    /// <summary>
    /// El mapeo de la entidad a la ficha.
    ///
    /// <paramref name="hoy"/> llega por parámetro y no se lee del reloj acá adentro: es lo que permite
    /// probar en un test lo que a mano exigiría esperar a que venza una factura (convención [005]).
    /// </summary>
    public static FacturaDetalle Armar(
        FacturaCliente factura,
        FacturaCliente? reemplazadaPor,
        DateOnly hoy)
    {
        var cliente = factura.Cliente;

        return new FacturaDetalle(
            factura.Id,
            factura.NumeroComprobante,
            factura.Fecha.ToString("yyyy-MM-dd"),
            NombresDeEstadoFactura.EnJson(factura.TipoComprobante),
            NombresDeEstadoFactura.EnJson(factura.TipoFacturacion),
            NombresDeEstadoFactura.EnJson(factura.CondicionDeVenta),
            factura.PeriodoMes,
            factura.PeriodoAnio,
            factura.Detalle,

            // Los diez congelados. La pantalla los muestra con el aviso de que son los del día de la
            // emisión y que un cambio posterior no los altera (FR-034).
            new EmisorDeFactura(
                factura.EmisorRazonSocial,
                factura.EmisorCuit,
                factura.EmisorDomicilio,
                factura.EmisorCondicionIva,
                factura.EmisorIngresosBrutos,
                factura.EmisorInicioActividades,
                factura.EmisorPuntoDeVenta,
                factura.EmisorCbu,
                factura.EmisorTelefono,
                factura.EmisorEmail),

            // La copia congelada **más** la referencia: `activo` sale del padrón y es lo que permite
            // mostrar la palabra `Inactivo` al lado de una razón social que ya no cambia (FR-011,
            // FR-034a, US3 esc. 9).
            new ClienteDeFactura(
                factura.ClienteId,
                factura.ClienteRazonSocial,
                factura.ClienteCuit,
                factura.ClienteDomicilio,
                cliente?.Activo ?? true),

            [.. factura.Viajes
                .OrderBy(viaje => viaje.Fecha)
                .ThenBy(viaje => viaje.Numero)
                .Select(viaje => new ViajeDeFactura(
                    viaje.Id,
                    viaje.Numero,
                    viaje.Fecha.ToString("yyyy-MM-dd"),
                    viaje.NumeroRemito,
                    viaje.Origen,
                    viaje.Destino,
                    viaje.Importe))],

            factura.Neto,
            factura.Iva,

            // Derivada del tipo de comprobante, no almacenada. Va en la respuesta porque la pantalla la
            // muestra al lado del IVA —`IVA (21%)`— y calcularla en TypeScript sería escribir la regla
            // dos veces (research §5).
            AlicuotasIva.PorcentajeDe(factura.TipoComprobante),

            factura.Total,
            factura.Cae,
            factura.CaeVencimiento.ToString("yyyy-MM-dd"),
            factura.VencimientoPago.ToString("yyyy-MM-dd"),

            // El estado **derivado**, con la misma función pura que usa el filtro del listado (FR-041).
            NombresDeEstadoFactura.EnJson(
                DerivadorEstadoFactura.Derivar(factura.Estado, factura.VencimientoPago, hoy)),

            factura.FechaCobro?.ToString("yyyy-MM-dd"),
            factura.MotivoAnulacion,

            factura.FacturaReemplazada is { } reemplazada
                ? PreparadorDeFactura.ResumenDe(reemplazada)
                : null,

            reemplazadaPor is null ? null : PreparadorDeFactura.ResumenDe(reemplazadaPor),

            $"/api/facturas/{factura.Id}/documento",

            // De la más vieja a la más nueva, empezando por la emisión (FR-045). Una entrada con
            // `estadoNuevo` en nulo es una **corrección**, que la pantalla lee `Corrección de datos`
            // (FR-037).
            [.. factura.CambiosDeEstado
                .OrderBy(cambio => cambio.OcurridoEn)
                .ThenBy(cambio => cambio.Id)
                .Select(cambio => new EntradaDeHistorial(
                    NombresDeEstadoFactura.EnJson(cambio.EstadoAnterior),
                    NombresDeEstadoFactura.EnJson(cambio.EstadoNuevo),
                    cambio.Usuario?.Username ?? $"Usuario {cambio.UsuarioId}",
                    cambio.OcurridoEn))]);
    }
}
