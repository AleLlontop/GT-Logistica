using GT.Application.Facturacion.EmpresaEmisora;

namespace GT.Application.Facturacion;

/// <summary>
/// El documento tal como va a quedar, <b>antes</b> de confirmar la emisión (FR-033).
///
/// <b>No crea la factura, no guarda ningún archivo y no registra nada</b>: arma la entidad en memoria,
/// la pasa por el mismo mapeo y el mismo armador que usa la emisión, y devuelve los bytes. Una vista
/// previa abandonada no deja rastro (US2 esc. 33).
///
/// <b>Es el mismo armador y sobre la misma entrada</b>, y ahí está todo el punto de FR-033: si esta
/// pantalla dibujara una maqueta parecida en HTML, las dos se separarían sin que nadie lo note y
/// revisar la vista previa dejaría de servir para algo. La igualdad byte a byte con el archivo que se
/// guarda al emitir la verifica <c>VistaPreviaTests</c> (SC-007b, research §2).
///
/// Aplica <b>las mismas validaciones de datos</b> que la emisión —empresa emisora configurada, cliente
/// con domicilio, viajes con remito— porque un documento que no se puede emitir tampoco se puede
/// previsualizar honestamente. <b>No</b> aplica las confirmaciones de FR-032: no hay nada irreversible
/// que confirmar todavía.
/// </summary>
public class VistaPreviaFactura(
    PreparadorDeFactura preparador,
    IArmadorDocumentoFactura armador,
    GestionarLogo logo)
{
    public record Documento(byte[] Contenido);

    public async Task<(ResultadoFactura? Rechazo, Documento? Pdf)> EjecutarAsync(
        EmisionRequest peticion,
        CancellationToken cancelacion = default)
    {
        var (rechazo, listo) = await preparador.PrepararAsync(
            peticion,
            esVistaPrevia: true,
            cancelacion);

        if (rechazo is not null)
        {
            return (rechazo, null);
        }

        // El logo vigente de la configuración, no uno congelado: es la única excepción declarada al
        // congelamiento de FR-034 (research §5).
        var datos = DatosDelDocumento.Desde(
            listo!.Factura,
            await logo.ParaElDocumentoAsync(cancelacion));

        // Renderiza a memoria y devuelve. Nada toca el disco: eso es lo que hace que FR-033 se cumpla al
        // pie de la letra y no por disciplina.
        return (null, new Documento(armador.Armar(datos)));
    }
}
