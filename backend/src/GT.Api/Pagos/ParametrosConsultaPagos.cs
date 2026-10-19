using System.Globalization;
using GT.Application.Pagos;
using GT.Domain.Pagos;

namespace GT.Api.Pagos;

internal static class ParametrosConsultaPagos
{
    public static DateOnly? Fecha(IQueryCollection query, string campo)
    {
        var texto = query[campo].ToString();
        if (texto.Length == 0) return null;
        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)) return fecha;
        throw new ReglaPagoException("fecha_invalida", "Ingresá una fecha válida.", campo: campo);
    }
    public static int? Entero(IQueryCollection query, string campo)
    {
        var texto = query[campo].ToString();
        if (texto.Length == 0) return null;
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)) return numero;
        throw new ReglaPagoException("parametro_invalido", "Ingresá un número entero válido.", campo: campo);
    }
    public static EstadoOrdenPagoProveedor? EstadoOrden(IQueryCollection query) => query["estado"].ToString() switch
    {
        "" => null, "pendiente" => EstadoOrdenPagoProveedor.Pendiente, "anulada" => EstadoOrdenPagoProveedor.Anulada,
        _ => throw new ReglaPagoException("estado_invalido", "Elegí Pendiente o Anulada.", campo: "estado"),
    };
    public static EstadoCheque? EstadoDelCheque(IQueryCollection query) => query["estado"].ToString() switch
    {
        "" => null, "pendiente" => EstadoCheque.Pendiente, "entregado" => EstadoCheque.Entregado,
        "depositado" => EstadoCheque.Depositado, "cobrado" => EstadoCheque.Cobrado,
        "rechazado" => EstadoCheque.Rechazado, "anulado" => EstadoCheque.Anulado,
        _ => throw new ReglaPagoException("estado_invalido", "Elegí un estado de cheque válido.", campo: "estado"),
    };

}
