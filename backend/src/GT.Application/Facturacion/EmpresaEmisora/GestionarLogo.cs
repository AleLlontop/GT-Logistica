using GT.Application.Choferes.Documentacion;

namespace GT.Application.Facturacion.EmpresaEmisora;

/// <summary>
/// El logo de la empresa emisora: subir, reemplazar, quitar y servir (FR-003, FR-004).
///
/// <b>Reutiliza el almacén y el validador del Módulo 3 sin modificarlos</b> (research §6, §13): uno
/// guarda un stream y el otro reconoce una firma, y ninguno sabe a qué entidad pertenece el archivo.
///
/// <b>Admite sólo JPG y PNG, y el que decide es este caso de uso</b>, no el validador. El validador
/// deduce el tipo de la <b>firma del archivo</b> —no de la extensión ni del <c>Content-Type</c>, que
/// los controla quien sube— y devuelve cuál es; acá se rechaza el PDF, que el validador acepta porque
/// los Módulos 3 y 4 lo necesitan. Un PDF válido renombrado a <c>.png</c> se rechaza igual, que es el
/// punto (FR-003).
///
/// <b>El orden entre disco y base sigue la convención [003]</b>: se escribe el archivo nuevo, se
/// confirma la fila, y recién después se borra el anterior. Así el único estado roto posible es un
/// archivo huérfano —invisible para quien opera— y nunca una fila que dice tener logo sin tenerlo.
/// </summary>
public class GestionarLogo(
    IRepositorioEmpresaEmisora empresas,
    IAlmacenDeArchivos almacen,
    IValidadorDeArchivo validador)
{
    /// <summary>Los dos tipos que admite el logo. El PDF no entra, aunque el validador lo reconozca.</summary>
    private static readonly string[] TiposAdmitidos = ["image/jpeg", "image/png"];

    public record LogoParaServir(Stream Contenido, string TipoContenido, string Nombre);

    /// <summary>
    /// Sube el logo o reemplaza al que había.
    ///
    /// No hay un caso de uso separado para reemplazar: subir sobre una configuración que ya tiene logo
    /// <b>es</b> reemplazar, y modelarlo como dos operaciones obligaría a la pantalla a saber cuál
    /// invocar (contracts/README §Logo).
    /// </summary>
    public async Task<ResultadoEmpresaEmisora> SubirAsync(
        ArchivoCargado? archivo,
        CancellationToken cancelacion = default)
    {
        if (archivo is null || archivo.TamanioEnBytes <= 0)
        {
            return new ResultadoEmpresaEmisora(
                ErrorFactura.ArchivoNoAdmitido,
                Campo: "archivo",
                Mensaje: MensajesFacturas.LogoNoAdmitido);
        }

        await using var contenido = archivo.Abrir();

        var validacion = await validador.ValidarAsync(contenido, archivo.TamanioEnBytes, cancelacion);

        // Dos rechazos con la misma respuesta: un archivo que no se reconoce, y uno que se reconoce
        // pero no es imagen. Para quien opera es lo mismo —"eso no es un JPG ni un PNG"— y el mensaje
        // dice qué formatos se aceptan (FR-003).
        if (!validacion.EsValido ||
            validacion.TipoContenido is null ||
            !TiposAdmitidos.Contains(validacion.TipoContenido))
        {
            return new ResultadoEmpresaEmisora(
                ErrorFactura.ArchivoNoAdmitido,
                Campo: "archivo",
                Mensaje: MensajesFacturas.LogoNoAdmitido);
        }

        var empresa = await empresas.ObtenerParaModificarAsync(cancelacion);

        // El logo no crea la configuración: sin los cuatro obligatorios no hay fila donde guardarlo, y
        // pedirle a la pantalla que cargue el logo antes de la razón social sería al revés de como se
        // usa (contracts/README §Empresa emisora).
        if (empresa is null)
        {
            return new ResultadoEmpresaEmisora(
                ErrorFactura.EmpresaEmisoraIncompleta,
                Campo: "archivo",
                Mensaje: MensajesFacturas.SinConfigurar);
        }

        string rutaNueva;

        try
        {
            rutaNueva = await almacen.GuardarAsync(contenido, cancelacion);
        }
        catch (ArchivoNoGuardadoException)
        {
            return new ResultadoEmpresaEmisora(ErrorFactura.ArchivoNoGuardado, Campo: "archivo");
        }

        var rutaAnterior = empresa.LogoRuta;

        empresa.LogoRuta = rutaNueva;
        empresa.LogoTipoContenido = validacion.TipoContenido;
        empresa.LogoNombreOriginal = archivo.Nombre;

        try
        {
            await empresas.GuardarAsync(cancelacion);
        }
        catch
        {
            // El archivo ya está escrito y la fila no llegó a apuntarlo: se compensa borrándolo. Deja
            // el estado roto aceptable en vez del prohibido (convención [003]).
            await almacen.BorrarAsync(rutaNueva, CancellationToken.None);

            throw;
        }

        // Recién después de confirmar. Al revés, una falla al guardar la fila dejaría la configuración
        // apuntando a un archivo que ya no existe.
        if (rutaAnterior is not null)
        {
            await almacen.BorrarAsync(rutaAnterior, cancelacion);
        }

        return new ResultadoEmpresaEmisora(
            ErrorFactura.Ninguno,
            EmpresaEmisoraDto.Desde(empresa),
            Mensaje: MensajesFacturas.EmpresaEmisoraGuardada);
    }

    /// <summary>
    /// Quita el logo. Las facturas se siguen emitiendo sin él (FR-004).
    ///
    /// <b>Idempotente y sin confirmación aparte</b>: quitar un logo que no está responde igual, y no
    /// destruye nada que no se pueda volver a subir. Es el mismo criterio con el que el Módulo 4 trató
    /// el alta de un vehículo (precedente [004]).
    /// </summary>
    public async Task QuitarAsync(CancellationToken cancelacion = default)
    {
        var empresa = await empresas.ObtenerParaModificarAsync(cancelacion);

        if (empresa?.LogoRuta is not { } ruta)
        {
            return;
        }

        empresa.LogoRuta = null;
        empresa.LogoTipoContenido = null;
        empresa.LogoNombreOriginal = null;

        await empresas.GuardarAsync(cancelacion);

        // Después de confirmar, igual que al reemplazar.
        await almacen.BorrarAsync(ruta, cancelacion);
    }

    /// <summary>
    /// El logo para servirlo <b>en línea</b>, o <c>null</c> si no hay ninguno o el archivo ya no está
    /// en el volumen. Las dos situaciones se comunican igual: <c>404</c>.
    /// </summary>
    public async Task<LogoParaServir?> ServirAsync(CancellationToken cancelacion = default)
    {
        var empresa = await empresas.ObtenerAsync(cancelacion);

        if (empresa?.LogoRuta is not { } ruta)
        {
            return null;
        }

        var contenido = await almacen.AbrirAsync(ruta, cancelacion);

        if (contenido is null)
        {
            return null;
        }

        return new LogoParaServir(
            contenido,
            empresa.LogoTipoContenido ?? "application/octet-stream",
            empresa.LogoNombreOriginal ?? "logo");
    }

    /// <summary>
    /// El logo leído a memoria para pasárselo al armador del documento, o <c>null</c>.
    ///
    /// Se lee <b>de la configuración vigente</b> y no de la factura: es la única excepción declarada al
    /// congelamiento de FR-034 (research §5).
    /// </summary>
    public async Task<LogoDelDocumento?> ParaElDocumentoAsync(CancellationToken cancelacion = default)
    {
        var empresa = await empresas.ObtenerAsync(cancelacion);

        if (empresa?.LogoRuta is not { } ruta)
        {
            return null;
        }

        await using var contenido = await almacen.AbrirAsync(ruta, cancelacion);

        if (contenido is null)
        {
            return null;
        }

        using var memoria = new MemoryStream();
        await contenido.CopyToAsync(memoria, cancelacion);

        return new LogoDelDocumento(memoria.ToArray());
    }
}
