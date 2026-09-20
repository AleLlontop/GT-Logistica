using GT.Api.Autorizacion;
using GT.Application.Reportes;
using GT.Application.Reportes.Fuentes;
using GT.Domain.Usuarios;

namespace GT.Api.Reportes;

/// <summary>
/// Los <b>cinco</b> endpoints de reporte del sistema, declarados juntos a propósito.
///
/// FR-001 dice "ninguna otra pantalla la ofrece en esta feature". Escrito así, la revisión
/// <b>cuenta cinco <c>MapGet</c></b> en un solo lugar en vez de buscarlos por cinco carpetas
/// (research §6).
///
/// <b>Por qué cinco y no uno genérico</b> (<c>/api/reportes/{cual}</c>): FR-015 exige un permiso de
/// lectura <b>distinto por reporte</b>, y las políticas de ASP.NET se declaran por endpoint. Uno
/// genérico tendría que resolver el permiso a mano adentro, que es lo que la convención [004]
/// prohíbe. Además cada reporte recibe los filtros de su pantalla, con <b>los mismos nombres</b> que
/// el listado ya usa: el reporte no inventa un enlace de parámetros propio.
///
/// <b>Sobre las restricciones de ruta</b> (convención [005]): las cinco rutas son <b>literales</b> y
/// conviven con rutas de identificador de otros grupos —<c>/api/viajes/{id:int}</c>,
/// <c>/api/facturas/{id:int}</c>, <c>/api/caja/{id:int}</c>—. Verificado: <b>las tres ya llevan su
/// <c>{id:int}</c></b> y no hay que agregar ninguna. Sin esa restricción, las literales quedarían
/// inalcanzables y no fallaría ni al compilar ni al arrancar: fallaría al pedirlas.
///
/// <b>El parseo de <c>?formato=pdf|excel</c> vive acá</b> y es común a los cinco: es lo único que
/// todos reciben.
/// </summary>
public static class ReportesEndpoints
{
    public static void MapearReportes(this IEndpointRouteBuilder rutas)
    {
        // Las cinco políticas combinadas: el permiso de lectura de la pantalla **más**
        // `reportes.emitir` (FR-015, research §5). Los dos dan el mismo `403` y el mismo texto:
        // distinguirlos le diría a quien invoca la ruta a mano exactamente qué permiso le falta, que
        // no ayuda a nadie que esté operando la aplicación.
        var viajes = PoliticasAutorizacion.ParaTodos(
            CodigosPermiso.ViajesConsultar, CodigosPermiso.ReportesEmitir);

        var vencimientosChoferes = PoliticasAutorizacion.ParaTodos(
            CodigosPermiso.ChoferesVencimientosConsultar, CodigosPermiso.ReportesEmitir);

        var vencimientosFlota = PoliticasAutorizacion.ParaTodos(
            CodigosPermiso.FlotaVencimientosConsultar, CodigosPermiso.ReportesEmitir);

        var vencimientosFacturas = PoliticasAutorizacion.ParaTodos(
            CodigosPermiso.FacturacionConsultar, CodigosPermiso.ReportesEmitir);

        var movimientosCaja = PoliticasAutorizacion.ParaTodos(
            CodigosPermiso.CajaConsultar, CodigosPermiso.ReportesEmitir);

        // ── 1 de 5 ──────────────────────────────────────────────────────────────────────────────
        rutas.MapGet("/api/viajes/reporte", ReporteDeViajesAsync)
            .RequireAuthorization(viajes)
            .ConMensajeDeSinPermiso();

        // ── 2 de 5 ──────────────────────────────────────────────────────────────────────────────
        rutas.MapGet("/api/vencimientos/reporte", ReporteDeVencimientosDeChoferesAsync)
            .RequireAuthorization(vencimientosChoferes)
            .ConMensajeDeSinPermiso();

        // ── 3 de 5 ──────────────────────────────────────────────────────────────────────────────
        rutas.MapGet("/api/flota/vencimientos/reporte", ReporteDeVencimientosDeFlotaAsync)
            .RequireAuthorization(vencimientosFlota)
            .ConMensajeDeSinPermiso();

        // ── 4 de 5 ──────────────────────────────────────────────────────────────────────────────
        rutas.MapGet("/api/facturas/vencimientos/reporte", ReporteDeVencimientosDeFacturasAsync)
            .RequireAuthorization(vencimientosFacturas)
            .ConMensajeDeSinPermiso();

        // ── 5 de 5. Con éste son cinco, y cinco es el número de FR-001. ─────────────────────────
        rutas.MapGet("/api/movimientos-caja/reporte", ReporteDeMovimientosDeCajaAsync)
            .RequireAuthorization(movimientosCaja)
            .ConMensajeDeSinPermiso();
    }

    /// <summary>El <c>403</c> de los cinco lleva el texto del contrato del módulo, no el genérico.</summary>
    private static RouteHandlerBuilder ConMensajeDeSinPermiso(this RouteHandlerBuilder ruta) =>
        ruta.WithMetadata(new MensajeDeSinPermiso(RespuestasDeReporte.SinPermiso()));

    // ── Los cinco handlers ──────────────────────────────────────────────────────────────────────
    // Los cinco hacen lo mismo: leen el formato, piden el reporte a su fuente y traducen el
    // resultado. Lo único propio de cada uno son sus filtros.

    private static Task<IResult> ReporteDeViajesAsync(
        HttpContext contexto,
        FuenteReporteViajes fuente,
        string? formato,
        int? clienteId,
        int? transportistaId,
        string? estado,
        DateOnly? desde,
        DateOnly? hasta,
        string? busqueda,
        CancellationToken cancelacion) =>
        EjecutarAsync(
            contexto,
            formato,
            elegido => fuente.EjecutarAsync(
                new FiltrosDelReporteDeViajes(
                    clienteId, transportistaId, estado, desde, hasta, busqueda),
                elegido,
                UsuarioDe(contexto),
                cancelacion),
            cancelacion);

    private static Task<IResult> ReporteDeVencimientosDeChoferesAsync(
        HttpContext contexto,
        FuenteReporteVencimientosChoferes fuente,
        string? formato,
        CancellationToken cancelacion) =>
        EjecutarAsync(
            contexto,
            formato,
            elegido => fuente.EjecutarAsync(elegido, UsuarioDe(contexto), cancelacion),
            cancelacion);

    private static Task<IResult> ReporteDeVencimientosDeFlotaAsync(
        HttpContext contexto,
        FuenteReporteVencimientosFlota fuente,
        string? formato,
        CancellationToken cancelacion) =>
        EjecutarAsync(
            contexto,
            formato,
            elegido => fuente.EjecutarAsync(elegido, UsuarioDe(contexto), cancelacion),
            cancelacion);

    private static Task<IResult> ReporteDeVencimientosDeFacturasAsync(
        HttpContext contexto,
        FuenteReporteVencimientosFacturas fuente,
        string? formato,
        CancellationToken cancelacion) =>
        EjecutarAsync(
            contexto,
            formato,
            elegido => fuente.EjecutarAsync(elegido, UsuarioDe(contexto), cancelacion),
            cancelacion);

    private static Task<IResult> ReporteDeMovimientosDeCajaAsync(
        HttpContext contexto,
        FuenteReporteMovimientosCaja fuente,
        string? formato,
        DateOnly? desde,
        DateOnly? hasta,
        int? cajaId,
        CancellationToken cancelacion) =>
        EjecutarAsync(
            contexto,
            formato,
            elegido => fuente.EjecutarAsync(
                new FiltrosDelReporteDeCaja(desde, hasta, cajaId),
                elegido,
                UsuarioDe(contexto),
                cancelacion),
            cancelacion);

    /// <summary>
    /// El esqueleto común: leer el formato, pedir el reporte y traducir. **El <c>500</c> se atrapa
    /// acá**, para que un fallo del motor sea el cuerpo del contrato y no una excepción sin manejar ni
    /// un <c>200</c> con cero bytes (FR-017).
    /// </summary>
    private static async Task<IResult> EjecutarAsync(
        HttpContext contexto,
        string? formato,
        Func<FormatoDeReporte, Task<ResultadoReporte>> pedir,
        CancellationToken cancelacion)
    {
        if (FormatosDeReporte.Leer(formato) is not { } elegido)
        {
            return RespuestasDeReporte.FormatoInvalido();
        }

        cancelacion.ThrowIfCancellationRequested();

        try
        {
            return RespuestasDeReporte.Traducir(contexto.Response, await pedir(elegido));
        }
        catch (ReporteNoGeneradoException)
        {
            return RespuestasDeReporte.NoGenerado();
        }
    }

    /// <summary>Quién lo generó, para la quinta pieza del encabezado (FR-006).</summary>
    private static string UsuarioDe(HttpContext contexto) =>
        contexto.User.Identity?.Name ?? string.Empty;
}
