using GT.Application.Autenticacion;
using GT.Application.Facturacion;

namespace GT.Api.Facturacion;

/// <summary>
/// Traducción de un <see cref="ResultadoFactura"/> fallido a su respuesta HTTP.
///
/// Vive en un solo lugar y no repetida en los cinco grupos de endpoints del módulo, porque la regla de
/// códigos que aplica es una sola y transversal (research §11, convención [005]):
///
/// <list type="bullet">
///   <item><b><c>400</c></b> cuando el problema está en <b>lo que se tipeó</b>: campos, formatos,
///   duplicados, datos faltantes de otra entidad.</item>
///   <item><b><c>409</c></b> cuando está en el <b>estado</b> de algo que se comparte o que cambió:
///   viaje ya facturado, anulada ya reemplazada, transición no permitida, factura inmutable,
///   <b>confirmación pendiente</b>.</item>
/// </list>
///
/// Con eso el frontend sabe, sin leer el código del backend, si tiene que marcar un campo o abrir un
/// diálogo.
/// </summary>
public static class RespuestasDeFactura
{
    public static IResult NoEncontrada() => Results.NotFound(
        new ErrorResponse(CodigosErrorFacturas.NoEncontrada, MensajesFacturas.NoEncontrada));

    public static IResult TraducirFallo(ResultadoFactura resultado) => resultado.Error switch
    {
        ErrorFactura.NoEncontrada => NoEncontrada(),

        // ── 400: el problema está en lo que se tipeó o se eligió ────────────────────────────────

        // Nombra los cuatro obligatorios que faltan, en el cuerpo además de en el mensaje: saber que
        // la empresa está incompleta sin saber qué campo no ayuda a resolverlo (FR-006).
        ErrorFactura.EmpresaEmisoraIncompleta => Results.BadRequest(new ErrorConFaltantes(
            CodigosErrorFacturas.EmpresaEmisoraIncompleta,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos,
            resultado.Campo)
        {
            Faltantes = resultado.Faltantes,
        }),

        ErrorFactura.CuitInvalido => Invalido(
            CodigosErrorFacturas.CuitInvalido, MensajesFacturas.CuitInvalido, resultado),

        ErrorFactura.EmailInvalido => Invalido(
            CodigosErrorFacturas.EmailInvalido, MensajesFacturas.EmailInvalido, resultado),

        ErrorFactura.ArchivoNoAdmitido => Invalido(
            CodigosErrorFacturas.ArchivoNoAdmitido, MensajesFacturas.LogoNoAdmitido, resultado),

        // 500 y no 400: el archivo era válido y el problema fue del sistema, no de lo que cargaron.
        // Es el mismo criterio del Módulo 4.
        ErrorFactura.ArchivoNoGuardado => Results.Json(
            new ErrorResponse(
                CodigosErrorFacturas.ArchivoNoGuardado,
                MensajesFacturas.ArchivoNoGuardado,
                resultado.Campo),
            statusCode: StatusCodes.Status500InternalServerError),

        ErrorFactura.ClienteInexistente => Invalido(
            CodigosErrorFacturas.ClienteInexistente, MensajesFacturas.ClienteInexistente, resultado),

        ErrorFactura.ClienteInactivo => Invalido(
            CodigosErrorFacturas.ClienteInactivo, resultado.Mensaje, resultado),

        ErrorFactura.ClienteSinDomicilio => Invalido(
            CodigosErrorFacturas.ClienteSinDomicilio, resultado.Mensaje, resultado),

        // Nombra los viajes uno por uno: con ocho viajes elegidos, saber que "uno" no tiene remito no
        // alcanza para arreglarlo (convención [004]).
        ErrorFactura.ViajeSinRemito => Results.BadRequest(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.ViajeSinRemito,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos,
            resultado.Campo)
        {
            Viajes = resultado.ViajesEnConflicto,
        }),

        // 400 y no 409: un número repetido es un duplicado, como el remito del Módulo 5, y se corrige
        // tipeando otro (research §11).
        ErrorFactura.NumeroDuplicado => Results.BadRequest(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.NumeroDuplicado,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos,
            resultado.Campo)
        {
            FacturaEnConflicto = resultado.FacturaEnConflicto,
        }),

        ErrorFactura.NumeroInvalido => Invalido(
            CodigosErrorFacturas.NumeroInvalido, MensajesFacturas.NumeroInvalido, resultado),

        ErrorFactura.SinViajesSeleccionados => Invalido(
            CodigosErrorFacturas.SinViajesSeleccionados,
            MensajesFacturas.SinViajesSeleccionados,
            resultado),

        ErrorFactura.RefacturacionSinReemplazada => Invalido(
            CodigosErrorFacturas.RefacturacionSinReemplazada,
            MensajesFacturas.RefacturacionSinReemplazada,
            resultado),

        ErrorFactura.OriginalConReemplazada => Invalido(
            CodigosErrorFacturas.OriginalConReemplazada,
            MensajesFacturas.OriginalConReemplazada,
            resultado),

        ErrorFactura.VencimientoPagoAnterior => Invalido(
            CodigosErrorFacturas.VencimientoPagoAnterior,
            MensajesFacturas.VencimientoPagoAnterior,
            resultado),

        ErrorFactura.CaeVencimientoAnterior => Invalido(
            CodigosErrorFacturas.CaeVencimientoAnterior,
            MensajesFacturas.CaeVencimientoAnterior,
            resultado),

        ErrorFactura.CaeRequerido => Invalido(
            CodigosErrorFacturas.CaeRequerido, resultado.Mensaje, resultado),

        ErrorFactura.FechaCobroAnterior => Invalido(
            CodigosErrorFacturas.FechaCobroAnterior, MensajesFacturas.FechaCobroAnterior, resultado),

        ErrorFactura.MotivoRequerido => Invalido(
            CodigosErrorFacturas.MotivoRequerido, MensajesFacturas.MotivoRequerido, resultado),

        ErrorFactura.RangoDeFechasRequerido => Invalido(
            CodigosErrorFacturas.RangoDeFechasRequerido,
            MensajesFacturas.RangoDeFechasRequerido,
            resultado),

        // ── 409: el problema está en el estado de algo compartido o que cambió ──────────────────

        ErrorFactura.ViajeYaFacturado => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.ViajeYaFacturado,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos)
        {
            Viajes = resultado.ViajesEnConflicto,
            FacturaEnConflicto = resultado.FacturaEnConflicto,
        }),

        ErrorFactura.AnuladaYaReemplazada => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.AnuladaYaReemplazada,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos)
        {
            FacturaEnConflicto = resultado.FacturaEnConflicto,
        }),

        ErrorFactura.TransicionNoPermitida => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.TransicionNoPermitida,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos)),

        ErrorFactura.FacturaAnuladaInmutable => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.FacturaAnuladaInmutable,
            MensajesFacturas.FacturaAnuladaInmutable)),

        // Informa desde qué fecha está cobrada, **sin ofrecer ni sugerir revertir el cobro**: no
        // existe ninguna acción que lo haga (FR-043a).
        ErrorFactura.FacturaCobrada => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.FacturaCobrada,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos)
        {
            FechaCobro = resultado.FacturaEnConflicto?.Fecha,
        }),

        // No cambió nada: el primer intento sólo pide confirmación (FR-032). El segundo lleva
        // `confirmado: true` en el cuerpo.
        ErrorFactura.EmisionRequiereConfirmacion => Conflicto(new ErrorDeBloqueoFactura(
            CodigosErrorFacturas.EmisionRequiereConfirmacion,
            resultado.Mensaje ?? MensajesFacturas.DatosInvalidos)
        {
            MotivoConfirmacion = resultado.MotivoConfirmacion switch
            {
                Application.Facturacion.MotivoConfirmacion.ViajeEnCero => "viajeEnCero",
                Application.Facturacion.MotivoConfirmacion.FechaAnteriorAViaje => "fechaAnteriorAViaje",
                _ => null,
            },
            Viajes = resultado.ViajesEnConflicto,
        }),

        // Cualquier error sin código propio se comunica como datos inválidos, con el campo marcado.
        // Nunca se cae: una respuesta de error no puede convertirse en un 500.
        //
        // **Usa el mensaje del resultado cuando lo trae**, y no el genérico: los cuatro obligatorios de
        // la empresa emisora llegan por acá con su texto propio —`Completá el domicilio para poder
        // guardar.`— y pisarlo con "Revisá los campos marcados" perdería justamente el dato que hace
        // útil al rechazo (contracts/README §Empresa emisora).
        _ => Invalido(CodigosErrorFacturas.DatosInvalidos, resultado.Mensaje, resultado),
    };

    /// <summary>La misma traducción para las operaciones que devuelven la configuración del emisor.</summary>
    public static IResult TraducirFallo(ResultadoEmpresaEmisora resultado) =>
        TraducirFallo(new ResultadoFactura(
            resultado.Error,
            Campo: resultado.Campo,
            Mensaje: resultado.Mensaje));

    private static IResult Invalido(string codigo, string? mensaje, ResultadoFactura resultado) =>
        Results.BadRequest(new ErrorResponse(
            codigo,
            mensaje ?? MensajesFacturas.DatosInvalidos,
            resultado.Campo));

    private static IResult Conflicto(ErrorDeBloqueoFactura cuerpo) =>
        Results.Json(cuerpo, statusCode: StatusCodes.Status409Conflict);
}
