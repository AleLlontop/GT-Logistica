using GT.Domain.Facturacion;

// La entidad se referencia con alias porque el módulo tiene una **subcarpeta** `EmpresaEmisora/` —así
// lo fija el plan §Project Structure—, y ese espacio de nombres oculta al tipo del mismo nombre. El
// alias deja claro cuál de los dos se está usando en vez de arrastrar un `GT.Domain.Facturacion.`
// completo en cada mención.
using Entidad = GT.Domain.Facturacion.EmpresaEmisora;

namespace GT.Application.Facturacion;

// ── Empresa emisora ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Lo que llega del formulario de empresa emisora (FR-002).
///
/// <b>No incluye el logo</b>: tiene sus tres recursos propios —subir, ver, quitar— porque es un
/// archivo y no un campo de texto, y así guardar un teléfono no puede borrarlo en silencio.
/// </summary>
public record EmpresaEmisoraRequest(
    string? RazonSocial,
    string? Cuit,
    string? Domicilio,
    string? CondicionIva,
    string? IngresosBrutos,
    DateOnly? InicioActividades,
    string? PuntoDeVenta,
    string? Cbu,
    string? Telefono,
    string? Email);

/// <summary>El logo cargado, o <c>null</c> si no hay ninguno (FR-003, FR-004).</summary>
public record LogoDto(string Nombre, string Url);

/// <summary>
/// Lo que devuelven el <c>GET</c> y el <c>PUT</c> de la configuración.
/// </summary>
/// <param name="Configurada">
/// <c>false</c> mientras falte alguno de los cuatro obligatorios, incluida la situación en que nunca
/// se guardó nada. La pantalla muestra el formulario vacío con el mensaje explícito, nunca una
/// pantalla en blanco (US1 esc. 1).
/// </param>
/// <param name="Faltantes">
/// Los obligatorios que faltan, por nombre. Con la fila ausente son los cuatro (FR-006).
/// </param>
public record EmpresaEmisoraDto(
    bool Configurada,
    IReadOnlyList<string> Faltantes,
    string? RazonSocial,
    string? Cuit,
    string? Domicilio,
    string? CondicionIva,
    string? IngresosBrutos,
    DateOnly? InicioActividades,
    string? PuntoDeVenta,
    string? Cbu,
    string? Telefono,
    string? Email,
    LogoDto? Logo)
{
    /// <summary>Ruta del logo. Es endpoint autorizado, nunca una URL pública (Principio V).</summary>
    public const string UrlDelLogo = "/api/facturacion/empresa-emisora/logo";

    /// <summary>La respuesta cuando la fila todavía no existe: faltan los cuatro obligatorios.</summary>
    public static EmpresaEmisoraDto SinConfigurar() => new(
        Configurada: false,
        Faltantes: Entidad.TodosLosObligatorios(),
        RazonSocial: null,
        Cuit: null,
        Domicilio: null,
        CondicionIva: null,
        IngresosBrutos: null,
        InicioActividades: null,
        PuntoDeVenta: null,
        Cbu: null,
        Telefono: null,
        Email: null,
        Logo: null);

    public static EmpresaEmisoraDto Desde(Entidad empresa)
    {
        var faltantes = empresa.ObligatoriosFaltantes();

        return new EmpresaEmisoraDto(
            faltantes.Count == 0,
            faltantes,
            empresa.RazonSocial,
            empresa.Cuit,
            empresa.Domicilio,
            empresa.CondicionIva,
            empresa.IngresosBrutos,
            empresa.InicioActividades,
            empresa.PuntoDeVenta,
            empresa.Cbu,
            empresa.Telefono,
            empresa.Email,
            empresa.LogoRuta is null
                ? null
                : new LogoDto(empresa.LogoNombreOriginal ?? "logo", UrlDelLogo));
    }
}

// ── Armado de la factura ────────────────────────────────────────────────────────────────────────

/// <summary>
/// Un viaje que se ofrece para incluir en la factura (FR-019, FR-019a).
/// </summary>
/// <param name="PuedeFacturarse">
/// <c>false</c> <b>sólo</b> por falta de remito, hoy. Los que no cumplen las otras condiciones no
/// llegan a esta lista: se filtran en la consulta.
/// </param>
/// <param name="MotivoNoFacturable">
/// Por qué no se puede, para que la pantalla muestre la palabra que lo explica al lado de la casilla
/// deshabilitada. Un listado no oculta filas en silencio ni las ofrece sin decir lo que sabe de ellas
/// (convención [003]).
/// </param>
public record ViajeFacturable(
    int Id,
    int Numero,
    string Fecha,
    string? NumeroRemito,
    string Origen,
    string Destino,
    decimal Importe,
    bool PuedeFacturarse,
    string? MotivoNoFacturable);

/// <summary>
/// Lo que llega del alta de factura, y lo mismo que recibe la vista previa (FR-024, research §9).
///
/// <b>No lleva <c>neto</c>, <c>iva</c> ni <c>total</c>, y eso es el requisito, no una omisión.</b> Los
/// calcula el servidor a partir de los viajes que encuentra en la base, así que no hay forma de
/// mandarlos ni desde la pantalla ni invocando la acción directamente: no están en el contrato de
/// entrada. Tampoco lleva la alícuota, que sale del tipo de comprobante (FR-023).
/// </summary>
/// <param name="Confirmado">
/// Sólo hace falta después de un <c>409 emision_requiere_confirmacion</c> (FR-032). Sin motivo que
/// confirmar se ignora.
/// </param>
public record EmisionRequest(
    int? ClienteId,
    string? TipoComprobante,
    string? TipoFacturacion,
    string? CondicionDeVenta,
    int? Mes,
    int? Anio,
    DateOnly? Fecha,
    string? NumeroComprobante,
    string? Detalle,
    string? Cae,
    DateOnly? CaeVencimiento,
    DateOnly? VencimientoPago,
    int? FacturaReemplazadaId,
    IReadOnlyList<int>? ViajeIds,
    bool? Confirmado);

/// <summary>
/// Lo que llega de la corrección (FR-035).
///
/// <b>Cuatro campos y ninguno más.</b> El cliente, los viajes y los importes no están acá, así que no
/// hay nada que ignorar: no se pueden mandar (FR-036). Tampoco el estado ni la fecha de cobro, que
/// tienen su recurso propio (FR-044, research §15.5).
/// </summary>
public record CorreccionRequest(
    string? Detalle,
    string? Cae,
    DateOnly? CaeVencimiento,
    DateOnly? VencimientoPago);

public record CobroRequest(DateOnly? FechaCobro);

public record AnulacionFacturaRequest(string? Motivo);

// ── Facturas ────────────────────────────────────────────────────────────────────────────────────

/// <summary>Lo mínimo para nombrar una factura desde otra pantalla o desde un mensaje de error.</summary>
public record FacturaResumen(int Id, string NumeroComprobante, string Fecha, string Estado);

/// <summary>Un viaje nombrado dentro de un rechazo (contracts §ViajeEnConflicto).</summary>
public record ViajeEnConflicto(int Id, int Numero, string Motivo);

/// <summary>El cliente de la factura: la copia congelada <b>más</b> la referencia al padrón.</summary>
/// <param name="RazonSocial">
/// La <b>congelada en la factura</b>, no la del padrón (FR-034a). Una corrección posterior en el
/// padrón no cambia lo que dice una factura ya emitida.
/// </param>
/// <param name="Activo">
/// Del padrón, no de la factura. <c>false</c> se muestra con la palabra <c>Inactivo</c> al lado, nunca
/// sólo con un color (FR-011, US3 esc. 9).
/// </param>
public record ClienteDeFactura(
    int Id,
    string RazonSocial,
    string Cuit,
    string Domicilio,
    bool Activo);

/// <summary>Los diez datos del emisor congelados al emitir (FR-034). El logo no se congela.</summary>
public record EmisorDeFactura(
    string RazonSocial,
    string Cuit,
    string Domicilio,
    string CondicionIva,
    string? IngresosBrutos,
    DateOnly? InicioActividades,
    string? PuntoDeVenta,
    string? Cbu,
    string? Telefono,
    string? Email);

/// <summary>Un viaje incluido en la factura, tal como sale en la ficha y en el documento.</summary>
public record ViajeDeFactura(
    int Id,
    int Numero,
    string Fecha,
    string? NumeroRemito,
    string Origen,
    string Destino,
    decimal Importe);

/// <summary>Fila del listado: las ocho columnas de FR-057.</summary>
public record FacturaListado(
    int Id,
    string NumeroComprobante,
    string Fecha,
    ClienteResumido Cliente,
    string TipoComprobante,
    int Mes,
    int Anio,
    decimal Total,
    string Estado,
    string VencimientoPago,
    string? MotivoAnulacion,
    string? FechaCobro);

/// <summary>Lo que el listado necesita del cliente: la copia congelada y si sigue activo.</summary>
public record ClienteResumido(int Id, string RazonSocial, bool Activo);

/// <summary>Una línea del historial (FR-045, FR-037).</summary>
/// <param name="EstadoAnterior"><c>null</c> en la emisión y en las correcciones.</param>
/// <param name="EstadoNuevo">
/// <c>null</c> <b>sólo</b> en una corrección, que la pantalla lee como <c>Corrección de datos</c>.
/// </param>
/// <param name="OcurridoEn">
/// Instante UTC con la <c>Z</c> que lo declara, garantizada para todo el sistema por la conversión de
/// <c>GtDbContext</c> (convención [002]).
/// </param>
public record EntradaDeHistorial(
    string? EstadoAnterior,
    string? EstadoNuevo,
    string Usuario,
    DateTime OcurridoEn);

/// <summary>Ficha completa (FR-060).</summary>
/// <param name="Alicuota">
/// Derivada del tipo de comprobante, no almacenada (research §5). Va en la respuesta porque la
/// pantalla la muestra al lado del IVA —<c>IVA (21%)</c>— y calcularla en TypeScript sería escribir
/// la regla dos veces.
/// </param>
/// <param name="ReemplazaA">A qué factura anulada reemplaza esta Refacturación (FR-050).</param>
/// <param name="ReemplazadaPor">
/// Qué Refacturación reemplazó a esta anulada. Se resuelve por consulta sobre
/// <c>FacturaReemplazadaId</c>, no por una columna espejo (FR-050).
/// </param>
public record FacturaDetalle(
    int Id,
    string NumeroComprobante,
    string Fecha,
    string TipoComprobante,
    string TipoFacturacion,
    string CondicionDeVenta,
    int Mes,
    int Anio,
    string? Detalle,
    EmisorDeFactura Emisor,
    ClienteDeFactura Cliente,
    IReadOnlyList<ViajeDeFactura> Viajes,
    decimal Neto,
    decimal Iva,
    decimal Alicuota,
    decimal Total,
    string Cae,
    string CaeVencimiento,
    string VencimientoPago,
    string Estado,
    string? FechaCobro,
    string? MotivoAnulacion,
    FacturaResumen? ReemplazaA,
    FacturaResumen? ReemplazadaPor,
    string DocumentoUrl,
    IReadOnlyList<EntradaDeHistorial> Historial);

// ── Reportes ────────────────────────────────────────────────────────────────────────────────────

/// <summary>Una fila del panel de vencimientos (FR-063).</summary>
/// <param name="Dias">
/// Negativo = días de atraso. Positivo o cero = días de plazo. La pantalla lo dice con la palabra que
/// lo explica y no sólo con un color (FR-065).
/// </param>
public record FilaDeVencimiento(
    int Id,
    string NumeroComprobante,
    string Cliente,
    decimal Total,
    string VencimientoPago,
    int Dias);

/// <summary>Una fila del cuadro de totales por cliente (FR-061).</summary>
/// <param name="Cantidad">Facturas <b>no anuladas</b> del rango (FR-062).</param>
/// <param name="Pendiente"><c>facturado − cobrado</c>.</param>
public record TotalPorCliente(
    int ClienteId,
    string RazonSocial,
    int Cantidad,
    decimal Facturado,
    decimal Cobrado,
    decimal Pendiente);

// ── Resultado de las operaciones ────────────────────────────────────────────────────────────────

/// <summary>
/// Los rechazos posibles de las operaciones sobre una factura.
///
/// Los primeros son problemas de <b>lo que se tipeó</b> y salen como <c>400</c>; los que están debajo
/// de la línea son problemas del <b>estado</b> de algo compartido o que cambió, y salen como
/// <c>409</c> (research §11). <c>RespuestasDeFactura</c> hace esa traducción en un solo lugar.
/// </summary>
public enum ErrorFactura
{
    Ninguno,
    NoEncontrada,
    DatosInvalidos,
    CuitInvalido,
    EmailInvalido,
    ArchivoNoAdmitido,
    ArchivoNoGuardado,
    EmpresaEmisoraIncompleta,
    ClienteInexistente,
    ClienteInactivo,
    ClienteSinDomicilio,
    ViajeSinRemito,
    NumeroDuplicado,
    NumeroInvalido,
    SinViajesSeleccionados,
    RefacturacionSinReemplazada,
    OriginalConReemplazada,
    VencimientoPagoAnterior,
    CaeVencimientoAnterior,
    CaeRequerido,
    FechaCobroAnterior,
    MotivoRequerido,
    RangoDeFechasRequerido,

    // ── De acá para abajo, 409 ──────────────────────────────────────────────────────────────────
    ViajeYaFacturado,
    AnuladaYaReemplazada,
    TransicionNoPermitida,
    FacturaAnuladaInmutable,
    FacturaCobrada,
    EmisionRequiereConfirmacion,
}

/// <summary>
/// Lo que devuelve cualquier operación sobre una factura: el resultado, o el rechazo con todo lo que
/// el mensaje necesita nombrar.
/// </summary>
/// <param name="Faltantes">
/// Los datos que faltan, cuando el rechazo es por eso (FR-006, FR-011a). Viajan en el cuerpo además
/// de en el mensaje.
/// </param>
/// <param name="ViajesEnConflicto">
/// Los viajes que producen el rechazo, nombrados uno por uno: saber que hay un problema sin saber en
/// cuál de los ocho viajes elegidos no ayuda a resolverlo.
/// </param>
public record ResultadoFactura(
    ErrorFactura Error,
    FacturaDetalle? Factura = null,
    string? Campo = null,
    IReadOnlyList<string>? Faltantes = null,
    FacturaResumen? FacturaEnConflicto = null,
    IReadOnlyList<ViajeEnConflicto>? ViajesEnConflicto = null,
    MotivoConfirmacion? MotivoConfirmacion = null,
    string? Mensaje = null)
{
    public bool Exitoso => Error is ErrorFactura.Ninguno;

    public static ResultadoFactura Exito(FacturaDetalle factura) =>
        new(ErrorFactura.Ninguno, factura);
}

/// <summary>
/// Resultado de las operaciones sobre la empresa emisora, que devuelven la configuración y no una
/// factura. Va aparte para no llenar <see cref="ResultadoFactura"/> de campos que no le sirven.
/// </summary>
public record ResultadoEmpresaEmisora(
    ErrorFactura Error,
    EmpresaEmisoraDto? Empresa = null,
    string? Campo = null,
    string? Mensaje = null)
{
    public bool Exitoso => Error is ErrorFactura.Ninguno;
}
