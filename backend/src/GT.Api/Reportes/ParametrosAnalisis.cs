using System.Globalization;
using GT.Application.Reportes.Analisis;

namespace GT.Api.Reportes;

internal static class ParametrosAnalisis
{
    public static string? Texto(IQueryCollection query, string campo) => string.IsNullOrWhiteSpace(query[campo]) ? null : query[campo].ToString().Trim();
    public static int? Entero(IQueryCollection query, string campo)
    {
        var texto = Texto(query, campo);
        if (texto is null) return null;
        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor)) return valor;
        throw new ReglaAnalisisException("filtro_invalido", "Ingresá un número entero válido.", campo);
    }
    public static bool? Booleano(IQueryCollection query, string campo)
    {
        var texto = Texto(query, campo);
        if (texto is null) return null;
        if (bool.TryParse(texto, out var valor)) return valor;
        throw new ReglaAnalisisException("filtro_invalido", "Elegí una opción válida.", campo);
    }
    public static PeriodoAnalisis Periodo(IQueryCollection query)
    {
        DateOnly Leer(string campo)
        {
            var texto = Texto(query, campo);
            if (texto is null) throw new ReglaAnalisisException("rango_requerido", "Elegí las fechas desde y hasta.", campo);
            if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valor)) return valor;
            throw new ReglaAnalisisException("rango_invalido", "Ingresá una fecha válida.", campo);
        }
        var desde = Leer("desde"); var hasta = Leer("hasta");
        if (desde > hasta) throw new ReglaAnalisisException("rango_invalido", "Desde no puede ser posterior a Hasta.", "hasta");
        return new(desde, hasta);
    }
    public static FiltroViajesAnalisis Viajes(IQueryCollection q) => new(Periodo(q), Entero(q, "clienteId"), Entero(q, "transportistaId"), Texto(q, "estado"), Texto(q, "busqueda"), Entero(q, "pagina") ?? 1, Entero(q, "tamanioPagina") ?? 20);
}
