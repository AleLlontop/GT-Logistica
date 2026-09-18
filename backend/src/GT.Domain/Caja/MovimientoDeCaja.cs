using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.Domain.Usuarios;

namespace GT.Domain.Caja;

/// <summary>
/// Un ingreso o un egreso de efectivo sobre una caja abierta (FR-006 a FR-015).
///
/// <b>No se edita, no se anula y no se borra</b> (FR-014): no hay endpoint que lo haga, y por eso todo es
/// <c>init</c>.
///
/// <b>La referencia es sólo informativa</b> (FR-012): registrar un ingreso con una factura no la cobra, y
/// un egreso con una orden de pago no la toca. Ninguna de las dos lleva colección inversa: los Módulos 6 y
/// 9 no se modifican.
/// </summary>
public class MovimientoDeCaja
{
    public int Id { get; init; }

    public required int CajaId { get; init; }

    public Caja? Caja { get; init; }

    public required TipoMovimientoCaja Tipo { get; init; }

    /// <summary>Mayor que cero y con a lo sumo dos decimales (RN3, FR-008).</summary>
    public required decimal Importe { get; init; }

    /// <summary>Recortado antes de guardar; no vacío y hasta 200 caracteres (RN5, FR-009).</summary>
    public required string Concepto { get; init; }

    /// <summary>Quien lo cargó, que es el responsable de la caja (RN7, FR-035).</summary>
    public required int UsuarioId { get; init; }

    public Usuario? Usuario { get; init; }

    /// <summary>Instante UTC del servidor; nunca tipeado (FR-013).</summary>
    public required DateTime Fecha { get; init; }

    /// <summary>Sólo en un ingreso, y la factura tiene que estar pendiente al guardar (FR-011).</summary>
    public int? FacturaId { get; init; }

    public FacturaCliente? Factura { get; init; }

    /// <summary>Sólo en un egreso, sin condición de estado (research §4).</summary>
    public int? OrdenDePagoId { get; init; }

    public OrdenDePago? OrdenDePago { get; init; }
}
