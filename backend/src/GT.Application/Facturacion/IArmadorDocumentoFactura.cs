namespace GT.Application.Facturacion;

/// <summary>
/// La frontera con la biblioteca que genera el PDF.
///
/// Existe para que la capa de aplicación y el dominio no conozcan QuestPDF: la única clase del sistema
/// que la conoce es <c>GT.Infrastructure/Documentos/ArmadorDocumentoFacturaQuestPdf</c> (research §1).
///
/// <b>Un solo armador, invocado por los dos caminos, sobre la misma entrada</b> (FR-033, research §2):
/// la vista previa arma la entidad <c>FacturaCliente</c> que todavía no existe, la mapea con
/// <see cref="DatosDelDocumento.Desde"/> y renderiza; la emisión arma la misma entidad, la persiste, y
/// mapea y renderiza con la misma función. Dos traducciones al mismo destino se separan sin que nadie
/// lo note, y entonces revisar la vista previa deja de servir para algo.
/// </summary>
public interface IArmadorDocumentoFactura
{
    /// <summary>
    /// Renderiza el documento <b>a memoria</b>. Que devuelva bytes y no escriba nada es lo que hace
    /// posible FR-033 al pie de la letra: la vista previa produce el documento y no lo guarda; la
    /// emisión llama a lo mismo y sí escribe el resultado.
    /// </summary>
    /// <exception cref="DocumentoNoGeneradoException">
    /// El documento no se pudo armar. Quien llama decide qué hacer: la emisión se rechaza entera, la
    /// corrección no se guarda y la anulación no queda aplicada a medias (FR-031, FR-031b).
    /// </exception>
    byte[] Armar(DatosDelDocumento datos);
}

/// <summary>
/// El documento no se pudo generar. Es lo que traduce cualquier falla del motor de PDF a algo que la
/// capa de aplicación pueda tratar.
///
/// <b>El caso realista en producción es la falta de <c>libfontconfig1</c></b> en la imagen: el backend
/// compila, arranca y sirve todo, y revienta recién al emitir la primera factura. Va acompañado de un
/// test de integración que genera un PDF de verdad, para que la falta se note en CI (research §1, §15.3).
/// </summary>
public class DocumentoNoGeneradoException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);
