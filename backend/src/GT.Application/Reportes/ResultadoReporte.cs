namespace GT.Application.Reportes;

/// <summary>
/// Lo que devuelve una fuente de reporte: el archivo entregado, o por qué no se entrega.
///
/// Los tres casos son los tres cuerpos que <c>RespuestasDeReporte</c> traduce a HTTP. El cuarto del
/// contrato —<c>500 reporte_no_generado</c>— no vive acá porque no es un resultado sino un fallo del
/// motor: llega como <see cref="ReporteNoGeneradoException"/>.
/// </summary>
/// <param name="Contenido">Los bytes del archivo. <c>null</c> cuando no se entregó nada.</param>
/// <param name="NombreDeArchivo">Ya armado por el backend y listo para el <c>Content-Disposition</c>.</param>
/// <param name="Filas">Con el tope superado, cuántas filas dejó el filtro.</param>
/// <param name="Tope">Con el tope superado, cuál es el tope.</param>
public record ResultadoReporte(
    byte[]? Contenido = null,
    string? NombreDeArchivo = null,
    FormatoDeReporte? Formato = null,
    bool FormatoInvalido = false,
    int? Filas = null,
    int? Tope = null)
{
    public bool SeEntrega => Contenido is not null;

    public bool SuperaElTope => Filas is not null;

    public static ResultadoReporte Entregado(
        byte[] contenido,
        string nombreDeArchivo,
        FormatoDeReporte formato) =>
        new(contenido, nombreDeArchivo, formato);

    public static ResultadoReporte FormatoNoAdmitido() => new(FormatoInvalido: true);

    public static ResultadoReporte TopeSuperado(int filas, int tope) => new(Filas: filas, Tope: tope);
}

/// <summary>
/// El motor de PDF o el de planilla falló al armar el archivo (FR-017).
///
/// Se envuelve acá y no se deja escapar sin manejar, para que la respuesta sea el <c>500</c> del
/// contrato con su texto en español y no una excepción cruda ni un <c>200</c> con cero bytes. **Un
/// rechazo no es un callejón sin salida**: la pantalla muestra el mensaje, deja la acción accionable
/// otra vez y no toca los filtros ni la página.
/// </summary>
public class ReporteNoGeneradoException(Exception interna)
    : Exception(MensajesDeReporte.ReporteNoGenerado, interna);
