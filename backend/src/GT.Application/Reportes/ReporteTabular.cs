namespace GT.Application.Reportes;

/// <summary>
/// El modelo neutro que las cinco fuentes producen y los dos armadores consumen (research §2).
///
/// Existe **uno solo**, y ésa es la decisión: la alternativa —un armador por reporte y por formato—
/// son diez clases dibujando la misma tabla. Pero lo que de verdad la decide es la convención [006]:
/// <i>cuando una misma información se produce por dos caminos, los dos llaman al mismo armador sobre
/// la misma entrada</i>. Acá los dos caminos son los dos <b>formatos</b>, y con un modelo intermedio
/// único el PDF y el Excel del mismo reporte <b>no pueden</b> traer filas distintas, en otro orden o
/// con otro total: leen el mismo objeto. La igualdad es estructural y no hace falta un test que
/// compare los dos archivos.
/// </summary>
public record ReporteTabular(
    EncabezadoDeReporte Encabezado,
    IReadOnlyList<ColumnaDeReporte> Columnas,
    IReadOnlyList<IReadOnlyList<CeldaDeReporte>> Filas,
    IReadOnlyList<TotalDeReporte> Totales)
{
    public static ReporteTabular Sin<T>(
        EncabezadoDeReporte encabezado,
        IReadOnlyList<ColumnaDeReporte> columnas,
        IReadOnlyList<IReadOnlyList<CeldaDeReporte>> filas) =>
        new(encabezado, columnas, filas, []);
}

/// <summary>
/// Las cinco piezas de FR-006, en el orden en el que los dos armadores las dibujan: título, filtros
/// en palabras, filas incluidas, instante de generación y quién lo generó.
/// </summary>
/// <param name="GeneradoEn">
/// Instante UTC, del <c>TimeProvider</c> registrado. **Se muestra en hora de Argentina.**
///
/// Que el reloj entre al archivo es deliberado y contrario a la regla del documento de la factura
/// (convención [006]): una factura es un comprobante que no cambia y un reporte es la foto de un
/// listado que sí cambia, así que a qué momento corresponde la foto es parte del dato (research §7).
/// La consecuencia es que **no se escribe ningún test de igualdad byte a byte sobre un reporte**:
/// sería un test que pasa o falla según el reloj.
/// </param>
public record EncabezadoDeReporte(
    string Titulo,
    string Filtros,
    int CantidadDeFilas,
    DateTime GeneradoEn,
    string GeneradoPor)
{
    /// <summary>Desplazamiento fijo de Argentina, el mismo que usa <c>FechaHoyArgentina</c>.</summary>
    private static readonly TimeSpan DesplazamientoArgentina = TimeSpan.FromHours(-3);

    /// <summary>El instante de generación en hora de Argentina.</summary>
    public DateTime GeneradoEnArgentina => EnArgentina(GeneradoEn);

    /// <summary>
    /// Un instante UTC en hora de Argentina, con el desplazamiento fijo que el sistema ya usa.
    ///
    /// Vive acá y no en cada quien que lo necesita —el encabezado la usa para la línea de generación y
    /// <c>NombreDeArchivoDeReporte</c> para la hora del nombre— porque dos desplazamientos escritos en
    /// dos lugares se pueden separar.
    /// </summary>
    public static DateTime EnArgentina(DateTime instanteUtc) =>
        new DateTimeOffset(DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc))
            .ToOffset(DesplazamientoArgentina)
            .DateTime;

    public string LineaDeFilas => $"Filas incluidas: {CantidadDeFilas:N0}";

    public string LineaDeGeneracion =>
        $"Generado el {GeneradoEnArgentina:dd/MM/yyyy} a las {GeneradoEnArgentina:HH:mm}";

    public string LineaDeAutor => $"Generado por {GeneradoPor}";
}

/// <summary>Qué clase de dato lleva una columna. Decide alineación y formato en cada armador.</summary>
public enum TipoDeColumna
{
    Texto,
    Fecha,
    Importe,
    Entero,
}

/// <param name="Nombre">El encabezado, <b>idéntico al de la pantalla</b> (FR-008).</param>
public record ColumnaDeReporte(string Nombre, TipoDeColumna Tipo)
{
    public static ColumnaDeReporte Texto(string nombre) => new(nombre, TipoDeColumna.Texto);

    public static ColumnaDeReporte Fecha(string nombre) => new(nombre, TipoDeColumna.Fecha);

    public static ColumnaDeReporte Importe(string nombre) => new(nombre, TipoDeColumna.Importe);

    public static ColumnaDeReporte Entero(string nombre) => new(nombre, TipoDeColumna.Entero);
}

/// <summary>
/// Una celda: <b>el valor y su tipo, nunca texto ya formateado</b>.
///
/// Es la pieza que hace convivir FR-010 con FR-011. El PDF formatea el <c>decimal</c> como
/// <c>$ 1.240.000,00</c> y el Excel lo escribe como número con formato <c>#,##0.00</c>; si la celda
/// llegara como <c>"$ 1.240.000,00"</c>, la planilla no podría sumarlo y SC-007 no se cumpliría.
///
/// **Un valor nulo es una celda vacía, no un texto de relleno**: un viaje sin chofer ni vehículo deja
/// las dos celdas en blanco, como la pantalla. No se escribe "Sin asignar" ni un guion.
/// </summary>
public record CeldaDeReporte
{
    private CeldaDeReporte(TipoDeColumna tipo)
    {
        Tipo = tipo;
    }

    public TipoDeColumna Tipo { get; private init; }

    public string? ValorTexto { get; private init; }

    public DateOnly? ValorFecha { get; private init; }

    public decimal? ValorImporte { get; private init; }

    public int? ValorEntero { get; private init; }

    /// <summary>Sin dato: la celda queda vacía en los dos formatos.</summary>
    public bool EstaVacia => Tipo switch
    {
        TipoDeColumna.Texto => string.IsNullOrEmpty(ValorTexto),
        TipoDeColumna.Fecha => ValorFecha is null,
        TipoDeColumna.Importe => ValorImporte is null,
        TipoDeColumna.Entero => ValorEntero is null,
        _ => true,
    };

    public static CeldaDeReporte Texto(string? valor) =>
        new(TipoDeColumna.Texto) { ValorTexto = valor };

    public static CeldaDeReporte Fecha(DateOnly? valor) =>
        new(TipoDeColumna.Fecha) { ValorFecha = valor };

    public static CeldaDeReporte Importe(decimal? valor) =>
        new(TipoDeColumna.Importe) { ValorImporte = valor };

    public static CeldaDeReporte Entero(int? valor) =>
        new(TipoDeColumna.Entero) { ValorEntero = valor };
}

/// <summary>
/// Una línea del pie de la tabla. **El Excel la escribe como número**, igual que las celdas de
/// importe, para que SC-004 se pueda comprobar con la calculadora de la planilla.
/// </summary>
public record TotalDeReporte(string Etiqueta, decimal Importe);
