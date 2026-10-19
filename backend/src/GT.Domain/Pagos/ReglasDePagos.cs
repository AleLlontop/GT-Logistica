using GT.Domain.Proveedores;

namespace GT.Domain.Pagos;

/// <summary>Reglas puras de 014. Hoy es siempre el día argentino recibido, nunca el reloj global.</summary>
public static class ReglasDePagos
{
    // Misma capacidad decimal(18,2) que las facturas de 013; no se recalculan impuestos.
    public const decimal ImporteMaximo = ReglasFacturaProveedor.Maximo;
    public const int LongitudMaximaNombreONumero = 200;

    public static bool ImporteValido(decimal importe) =>
        importe > 0 && importe <= ImporteMaximo && decimal.Round(importe, 2) == importe;

    /// <summary>No redondea ni modifica valores. Un conjunto vacío, inválido o fuera de rango no suma.</summary>
    public static bool IntentarSumarImportes(IEnumerable<decimal> importes, out decimal total)
    {
        total = 0;
        var suma = 0m;
        var hayImportes = false;
        foreach (var importe in importes)
        {
            if (!ImporteValido(importe) || importe > ImporteMaximo - suma)
                return false;
            suma += importe;
            hayImportes = true;
        }
        if (!hayImportes) return false;
        total = suma;
        return true;
    }

    public static bool TotalesCoinciden(IEnumerable<decimal> facturas, IEnumerable<decimal> medios) =>
        IntentarSumarImportes(facturas, out var totalFacturas)
        && IntentarSumarImportes(medios, out var totalMedios)
        && totalFacturas == totalMedios;

    public static bool FechaOrdenValida(DateOnly? fecha, DateOnly hoyArgentina) =>
        fecha.HasValue && fecha.Value <= hoyArgentina;

    public static string? NormalizarDetalle(string? detalle) =>
        string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();

    /// <summary>Devuelve null si falta o excede 200 tras trim; jamás recorta contenido interno.</summary>
    public static string? NormalizarNumeroCheque(string? numero) => NormalizarTextoRequerido(numero);

    // La igualdad/uniquidad del nombre bancario corresponde a la base, no a esta normalización.
    public static string? NormalizarNombreBanco(string? nombre) => NormalizarTextoRequerido(nombre);

    private static string? NormalizarTextoRequerido(string? valor)
    {
        var normalizado = valor?.Trim();
        return string.IsNullOrEmpty(normalizado) || normalizado.Length > LongitudMaximaNombreONumero
            ? null : normalizado;
    }

    public static bool MediosPermitidos(TipoProveedor proveedor, IEnumerable<TipoMedioPagoProveedor> medios)
    {
        var hayMedios = false;
        foreach (var medio in medios)
        {
            if (proveedor == TipoProveedor.Fletero)
            {
                if (medio != TipoMedioPagoProveedor.Cheque) return false;
            }
            else if (proveedor != TipoProveedor.ProveedorNormal
                || medio is not (TipoMedioPagoProveedor.Cheque or TipoMedioPagoProveedor.Transferencia
                    or TipoMedioPagoProveedor.Efectivo)) return false;
            hayMedios = true;
        }
        return hayMedios;
    }

    /// <summary>BancoId corresponde a una referencia seleccionada; existencia/Activo se revalidan fuera del dominio.</summary>
    public static bool ChequeValido(TipoProveedor proveedor, int? bancoId, string? numero,
        DateOnly? emision, DateOnly? vencimiento, decimal importe, DateOnly hoyArgentina) =>
        bancoId is > 0 && NormalizarNumeroCheque(numero) is not null && ImporteValido(importe)
        && emision.HasValue && vencimiento.HasValue && emision.Value <= hoyArgentina
        && (proveedor switch
        {
            TipoProveedor.Fletero => vencimiento.Value > emision.Value,
            TipoProveedor.ProveedorNormal => vencimiento.Value >= emision.Value,
            _ => false,
        });

    public static bool CbuValido(string? cbu) =>
        cbu is { Length: 22 } && cbu.All(caracter => caracter is >= '0' and <= '9');

    public static bool TransferenciaValida(DateOnly? fecha, decimal importe, string? cbuOrigen,
        string? cbuDestino, string? comprobante, DateOnly hoyArgentina) =>
        fecha.HasValue && fecha.Value <= hoyArgentina && ImporteValido(importe)
        && CbuValido(cbuOrigen) && CbuValido(cbuDestino) && !string.IsNullOrWhiteSpace(comprobante);

    public static bool EfectivoValido(decimal importe) => ImporteValido(importe);

    public static bool MotivoAnulacionValido(string? motivo) => !string.IsNullOrWhiteSpace(motivo);

    public static bool TransicionChequePermitida(EstadoCheque anterior, EstadoCheque nuevo) =>
        (anterior, nuevo) is
            (EstadoCheque.Pendiente, EstadoCheque.Entregado or EstadoCheque.Anulado)
            or (EstadoCheque.Entregado, EstadoCheque.Depositado or EstadoCheque.Anulado)
            or (EstadoCheque.Depositado, EstadoCheque.Cobrado or EstadoCheque.Rechazado);
}
