using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Facturacion;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia.Configuraciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Persistencia de las facturas, con las tres transacciones que tienen que ser atómicas adentro
/// (data-model §Transacciones).
/// </summary>
/// <param name="almacen">
/// Hace falta para <b>borrar</b> el documento reemplazado después de confirmar una corrección o una
/// anulación (research §6). Escribir el nuevo lo hace la capa de aplicación y llega por parámetro, para
/// que la persistencia no tenga que conocer el armador; borrar el viejo, en cambio, es parte del final
/// de la transacción y tiene que pasar exactamente después del <c>commit</c>.
/// </param>
public class RepositorioFacturas(GtDbContext contexto, IAlmacenDeArchivos almacen)
    : IRepositorioFacturas
{
    // ── Armado ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Las tres condiciones de FR-015 a FR-018 <b>a la vez</b>, en la consulta: del cliente, en estado
    /// <c>rendido</c>, con fecha dentro del mes y año, y sin factura vigente.
    ///
    /// <c>Estado == Rendido</c> ya implica <c>FacturaId == null</c> —al facturar el viaje pasa a
    /// <c>facturado</c>—, pero las dos condiciones van escritas igual: son dos hechos distintos y la
    /// que sostiene FR-017 es la segunda. Si algún día un viaje pudiera quedar <c>rendido</c> con
    /// factura, esta consulta seguiría siendo correcta.
    ///
    /// <b>Los que no tienen remito vienen igual</b>: la pantalla los muestra con la casilla
    /// deshabilitada y la palabra que lo explica (FR-019a, convención [003]).
    /// </summary>
    public async Task<IReadOnlyList<Viaje>> ConsultarFacturablesAsync(
        int clienteId,
        int mes,
        int anio,
        CancellationToken cancelacion = default) =>
        await contexto.Viajes
            .Where(viaje =>
                viaje.ClienteId == clienteId &&
                viaje.Estado == EstadoViaje.Rendido &&
                viaje.FacturaId == null &&
                viaje.Fecha.Month == mes &&
                viaje.Fecha.Year == anio)
            .OrderBy(viaje => viaje.Fecha)
            .ThenBy(viaje => viaje.Numero)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    /// <summary>
    /// Los viajes elegidos, <b>sin rastrear</b>. Es a propósito: se usan para calcular los importes y
    /// para armar el detalle del documento, y la transacción los marca con un <c>UPDATE</c> condicional
    /// que no pasa por el rastreador. Rastrearlos acá dejaría dos caminos escribiendo el mismo dato.
    /// </summary>
    public async Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(
        IReadOnlyList<int> ids,
        CancellationToken cancelacion = default) =>
        await contexto.Viajes
            .Where(viaje => ids.Contains(viaje.Id))
            .Include(viaje => viaje.Factura)
            .OrderBy(viaje => viaje.Fecha)
            .ThenBy(viaje => viaje.Numero)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken cancelacion = default) =>
        contexto.Clientes
            .AsNoTracking()
            .FirstOrDefaultAsync(cliente => cliente.Id == clienteId, cancelacion);

    /// <summary>
    /// La factura <b>no anulada</b> que ya usa ese número. El filtro tiene que coincidir con el del
    /// índice único: si acá se buscara entre todas, una anulada con el mismo número produciría un
    /// rechazo que el índice no habría producido (FR-027).
    /// </summary>
    public Task<FacturaCliente?> ObtenerPorNumeroAsync(
        string numeroComprobante,
        CancellationToken cancelacion = default) =>
        contexto.Facturas
            .Where(factura =>
                factura.NumeroComprobante == numeroComprobante &&
                factura.Estado != EstadoFactura.Anulada)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

    /// <summary>
    /// FR-049, FR-049a: las anuladas de ese cliente que todavía nadie refacturó. La segunda condición
    /// es una subconsulta de no existencia sobre <c>FacturaReemplazadaId</c>, que es la misma columna
    /// que sostiene el índice único.
    /// </summary>
    public async Task<IReadOnlyList<FacturaCliente>> ConsultarAnuladasSinReemplazoAsync(
        int clienteId,
        CancellationToken cancelacion = default) =>
        await contexto.Facturas
            .Where(factura =>
                factura.ClienteId == clienteId &&
                factura.Estado == EstadoFactura.Anulada &&
                !contexto.Facturas.Any(otra => otra.FacturaReemplazadaId == factura.Id))
            .OrderByDescending(factura => factura.Fecha)
            .ThenByDescending(factura => factura.NumeroComprobante)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El listado con los cinco filtros y el estado derivado resueltos en la base (FR-057 a FR-059).
    ///
    /// <b>La derivación de <c>vencida</c> va escrita en el árbol de la consulta y no extraída a un
    /// método propio</b>, y no es una preferencia de estilo: EF Core sólo traduce lo que ve, y una
    /// llamada a <c>DerivadorEstadoFactura.Derivar</c> acá rompería la traducción dejando la consulta
    /// evaluándose en memoria — con lo que el filtro se aplicaría <b>después</b> de paginar y las
    /// páginas saldrían incompletas (research §15.4, convención [003]).
    ///
    /// El <b>filtro</b> es lo que tiene que estar en SQL. La traducción del enum a su nombre en español
    /// se hace después, sobre las 20 filas ya traídas: ahí no hay nada que paginar.
    ///
    /// Los cuatro valores del filtro son <b>excluyentes</b>: <c>pendiente</c> devuelve sólo las impagas
    /// en plazo y <c>vencida</c> sólo las pasadas de fecha (FR-058a, US3 esc. 11).
    /// </summary>
    public async Task<PaginaDe<FacturaListado>> ConsultarAsync(
        FiltrosDeFacturas filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Facturas.AsQueryable();

        if (filtros.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(factura => factura.ClienteId == clienteId);
        }

        if (filtros.Desde is { } desde)
        {
            consulta = consulta.Where(factura => factura.Fecha >= desde);
        }

        if (filtros.Hasta is { } hasta)
        {
            consulta = consulta.Where(factura => factura.Fecha <= hasta);
        }

        if (filtros.Mes is { } mes)
        {
            consulta = consulta.Where(factura => factura.PeriodoMes == mes);
        }

        if (filtros.Anio is { } anio)
        {
            consulta = consulta.Where(factura => factura.PeriodoAnio == anio);
        }

        if (filtros.TipoComprobante is { } tipo)
        {
            consulta = consulta.Where(factura => factura.TipoComprobante == tipo);
        }

        // La derivación, escrita cuatro veces como predicado. Sin filtro se devuelven **todas,
        // incluidas las anuladas**, y el control de la pantalla dice qué está mostrando (FR-064).
        consulta = filtros.Estado switch
        {
            EstadoFacturaVisible.Pendiente => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pendiente && factura.VencimientoPago >= hoy),

            EstadoFacturaVisible.Vencida => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pendiente && factura.VencimientoPago < hoy),

            EstadoFacturaVisible.Pagada => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Pagada),

            EstadoFacturaVisible.Anulada => consulta.Where(factura =>
                factura.Estado == EstadoFactura.Anulada),

            _ => consulta,
        };

        // El total cuenta las coincidencias completas con los filtros, no las de esta página (FR-059).
        var total = await consulta.CountAsync(cancelacion);

        // Orden **total** de FR-059: fecha de facturación descendente y, a igual fecha, número de
        // comprobante descendente. No termina en `Id` y no hace falta: el índice único garantiza que
        // dos facturas vigentes no comparten número. La salvedad —dos anuladas sí pueden— está anotada
        // en research §10 y no justifica un desempate que sería ruido en el resto de los casos.
        var filas = await consulta
            .OrderByDescending(factura => factura.Fecha)
            .ThenByDescending(factura => factura.NumeroComprobante)
            .Skip((filtros.Pagina - 1) * PaginaDe<FacturaListado>.TamanioPorDefecto)
            .Take(PaginaDe<FacturaListado>.TamanioPorDefecto)
            .Select(factura => new
            {
                factura.Id,
                factura.NumeroComprobante,
                factura.Fecha,
                ClienteId = factura.ClienteId,
                // La **congelada en la factura**, no la del padrón (FR-034a).
                factura.ClienteRazonSocial,
                // Del padrón: `false` se muestra con la palabra `Inactivo` al lado (FR-011).
                ClienteActivo = factura.Cliente!.Activo,
                factura.TipoComprobante,
                factura.PeriodoMes,
                factura.PeriodoAnio,
                factura.Total,
                factura.Estado,
                // La misma derivación, ahora como valor proyectado. Sale de la base junto con la fila,
                // así que la columna que se muestra y el filtro que se aplicó no pueden discrepar.
                Vencida = factura.Estado == EstadoFactura.Pendiente && factura.VencimientoPago < hoy,
                factura.VencimientoPago,
                factura.MotivoAnulacion,
                factura.FechaCobro,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila => new FacturaListado(
                fila.Id,
                fila.NumeroComprobante,
                fila.Fecha.ToString("yyyy-MM-dd"),
                new ClienteResumido(fila.ClienteId, fila.ClienteRazonSocial, fila.ClienteActivo),
                NombresDeEstadoFactura.EnJson(fila.TipoComprobante),
                fila.PeriodoMes,
                fila.PeriodoAnio,
                fila.Total,
                NombresDeEstadoFactura.EnJson(Visible(fila.Estado, fila.Vencida)),
                fila.VencimientoPago.ToString("yyyy-MM-dd"),
                fila.MotivoAnulacion,
                fila.FechaCobro?.ToString("yyyy-MM-dd")))
            .ToList();

        return new PaginaDe<FacturaListado>(
            items,
            total,
            filtros.Pagina,
            PaginaDe<FacturaListado>.TamanioPorDefecto);
    }

    /// <summary>
    /// El estado visible a partir del guardado y del <c>vencida</c> que ya trajo la consulta.
    ///
    /// <b>No llama a <c>DerivadorEstadoFactura</c> con el reloj</b>: la comparación de fechas la hizo la
    /// base, contra el mismo <c>hoy</c> que usó el filtro. Que las dos escrituras de la regla coincidan
    /// lo verifica <c>DerivacionVencidaTests</c> (convención [003]).
    /// </summary>
    private static EstadoFacturaVisible Visible(EstadoFactura guardado, bool vencida) => guardado switch
    {
        EstadoFactura.Pagada => EstadoFacturaVisible.Pagada,
        EstadoFactura.Anulada => EstadoFacturaVisible.Anulada,
        _ => vencida ? EstadoFacturaVisible.Vencida : EstadoFacturaVisible.Pendiente,
    };

    public Task<FacturaCliente?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Facturas
            .Include(factura => factura.Cliente)
            .Include(factura => factura.Viajes)
            .Include(factura => factura.CambiosDeEstado).ThenInclude(cambio => cambio.Usuario)
            .Include(factura => factura.FacturaReemplazada)
            .AsNoTracking()
            .FirstOrDefaultAsync(factura => factura.Id == id, cancelacion);

    public Task<FacturaCliente?> ObtenerParaModificarAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Facturas
            .Include(factura => factura.Cliente)
            .Include(factura => factura.Viajes)
            .FirstOrDefaultAsync(factura => factura.Id == id, cancelacion);

    /// <summary>
    /// La otra dirección de la referencia de refacturación, <b>por consulta</b> (FR-050). Una columna
    /// espejo habría que mantenerla sincronizada y podría discrepar del dato que ya está.
    /// </summary>
    public Task<FacturaCliente?> ObtenerQueLaReemplazaAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Facturas
            .AsNoTracking()
            .FirstOrDefaultAsync(factura => factura.FacturaReemplazadaId == id, cancelacion);

    // ── Emitir (data-model §Emitir, FR-054, SC-005) ─────────────────────────────────────────────

    public async Task<ResultadoDeEmision> EmitirAsync(
        FacturaCliente factura,
        IReadOnlyList<int> viajeIds,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        // ⚠ La colección viene poblada porque **el documento ya se armó con ella**: el detalle del PDF
        // sale de `factura.Viajes` (FR-031e). Esos viajes son entidades sin rastrear traídas por
        // `ObtenerViajesAsync`, y dejarlas colgando de una factura en estado `Added` haría que EF
        // intentara insertarlas de nuevo con su `Id` explícito.
        //
        // Se vacía acá y el vínculo lo establece el `UPDATE` condicional de abajo, que además es lo que
        // cierra la carrera. El PDF ya está escrito en disco, así que nada se pierde.
        factura.Viajes.Clear();

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        contexto.Facturas.Add(factura);

        // Toda factura tiene al menos una entrada: la de su emisión, con `EstadoAnterior = null`
        // —antes no había estado— y `EstadoNuevo = pendiente` (FR-045).
        factura.CambiosDeEstado.Add(new CambioDeEstadoFactura
        {
            FacturaId = factura.Id,
            EstadoAnterior = null,
            EstadoNuevo = EstadoFactura.Pendiente,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await GuardarTraduciendoIndicesAsync(cancelacion);

        // **El `UPDATE` condicional**, que es lo que cierra la carrera entre dos operadores simultáneos
        // (research §4). Bajo el nivel de aislamiento por defecto de SQL Server, la segunda transacción
        // se bloquea sobre la fila que la primera está modificando y, al desbloquearse, reevalúa este
        // `WHERE` contra el dato ya comprometido —`FacturaId` no nulo—, afecta cero filas y se rechaza.
        var afectados = await contexto.Viajes
            .Where(viaje =>
                viajeIds.Contains(viaje.Id) &&
                viaje.Estado == EstadoViaje.Rendido &&
                viaje.FacturaId == null)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(viaje => viaje.Estado, EstadoViaje.Facturado)
                    .SetProperty(viaje => viaje.FacturaId, factura.Id),
                cancelacion);

        if (afectados != viajeIds.Count)
        {
            // Se averigua **cuáles** quedaron afuera antes de deshacer, para que el rechazo pueda
            // nombrarlos con su comprobante: saber que un viaje no está disponible sin saber dónde
            // quedó no ayuda a resolverlo (convención [004]).
            var tomados = await contexto.Viajes
                .Where(viaje =>
                    viajeIds.Contains(viaje.Id) &&
                    (viaje.Estado != EstadoViaje.Facturado || viaje.FacturaId != factura.Id))
                .Select(viaje => new ViajeTomado(
                    viaje.Id,
                    viaje.Numero,
                    viaje.Factura == null ? null : viaje.Factura.NumeroComprobante))
                .AsNoTracking()
                .ToListAsync(cancelacion);

            await transaccion.RollbackAsync(cancelacion);

            // El contexto quedó con la factura rastreada como insertada; se descarta para que un
            // reintento en el mismo alcance no la arrastre.
            contexto.ChangeTracker.Clear();

            return new ResultadoDeEmision(false, tomados);
        }

        // FR-035 del **Módulo 5**, ya vigente: todo cambio de estado de un viaje queda registrado. No lo
        // pide ninguna FR del Módulo 6, y sin estas líneas la ficha del viaje mostraría `facturado` sin
        // una línea que lo explique (research §8).
        foreach (var viajeId in viajeIds)
        {
            contexto.CambiosDeEstadoViaje.Add(new CambioDeEstadoViaje
            {
                ViajeId = viajeId,
                EstadoAnterior = EstadoViaje.Rendido,
                EstadoNuevo = EstadoViaje.Facturado,
                UsuarioId = usuarioId,
                OcurridoEn = ocurridoEn,
            });
        }

        await GuardarTraduciendoIndicesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        return ResultadoDeEmision.Ok;
    }

    // ── Corregir (data-model §Corregir, FR-035, FR-031b) ────────────────────────────────────────

    public async Task CorregirAsync(
        FacturaCliente factura,
        int usuarioId,
        DateTime ocurridoEn,
        Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento,
        CancellationToken cancelacion = default)
    {
        var rutaAnterior = factura.DocumentoRuta;

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        // Se escribe el PDF nuevo **adentro**: si no se puede armar, la corrección no queda guardada y
        // la ficha no dice una cosa mientras el archivo dice otra (FR-031b). La corrección podría
        // regenerar afuera, pero se escribe igual que la anulación, porque una regla con una excepción
        // son dos reglas (plan §Reevaluación post-diseño).
        factura.DocumentoRuta = await escribirDocumento(factura, cancelacion);

        // Una entrada de **corrección**: `EstadoNuevo` en nulo es la marca, y no hay columna que lo
        // repita (FR-037).
        factura.CambiosDeEstado.Add(new CambioDeEstadoFactura
        {
            FacturaId = factura.Id,
            EstadoAnterior = null,
            EstadoNuevo = null,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        // Recién después de confirmar, y como archivo nuevo: nunca se sobreescribe en el lugar, porque
        // una falla a mitad de escritura dejaría un PDF corrupto donde antes había uno bueno
        // (research §6).
        await BorrarSiCambio(rutaAnterior, factura.DocumentoRuta, cancelacion);
    }

    // ── Anular (data-model §Anular, FR-046 a FR-048, FR-031b) ───────────────────────────────────

    public async Task AnularAsync(
        FacturaCliente factura,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento,
        CancellationToken cancelacion = default)
    {
        var estadoAnterior = factura.Estado;
        var rutaAnterior = factura.DocumentoRuta;
        var viajeIds = factura.Viajes.Select(viaje => viaje.Id).ToList();

        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);

        factura.Estado = EstadoFactura.Anulada;
        factura.MotivoAnulacion = motivo;

        factura.CambiosDeEstado.Add(new CambioDeEstadoFactura
        {
            FacturaId = factura.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoFactura.Anulada,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        // **La regeneración va adentro de la transacción** (FR-031b): si el documento no se puede
        // armar, la anulación no queda aplicada a medias y los viajes no vuelven a `rendido`. La
        // factura ya tiene el estado y el motivo puestos, así que el documento sale con la leyenda y el
        // motivo impresos (FR-031d).
        factura.DocumentoRuta = await escribirDocumento(factura, cancelacion);

        await contexto.SaveChangesAsync(cancelacion);

        // **Todos** los viajes vuelven a `rendido` con su `FacturaId` en nulo, y quedan disponibles para
        // facturar de nuevo: o vuelven todos o no vuelve ninguno (FR-048).
        await contexto.Viajes
            .Where(viaje => viaje.FacturaId == factura.Id)
            .ExecuteUpdateAsync(
                cambio => cambio
                    .SetProperty(viaje => viaje.Estado, EstadoViaje.Rendido)
                    .SetProperty(viaje => viaje.FacturaId, (int?)null),
                cancelacion);

        foreach (var viajeId in viajeIds)
        {
            contexto.CambiosDeEstadoViaje.Add(new CambioDeEstadoViaje
            {
                ViajeId = viajeId,
                EstadoAnterior = EstadoViaje.Facturado,
                EstadoNuevo = EstadoViaje.Rendido,
                UsuarioId = usuarioId,
                OcurridoEn = ocurridoEn,
            });
        }

        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);

        await BorrarSiCambio(rutaAnterior, factura.DocumentoRuta, cancelacion);
    }

    // ── Cobro (FR-042) ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El cobro y su línea de historial en un solo <c>SaveChanges</c> —y por lo tanto en una sola
    /// transacción—: no puede quedar una factura cobrada sin su línea, ni al revés.
    ///
    /// <b>No regenera el documento</b>, y es el único cambio de estado que no lo hace: la fecha de cobro
    /// no sale impresa, porque es información interna de cobranzas y el comprobante que se le mandó al
    /// cliente no cambia porque después haya pagado (FR-031b, spec §Clarifications CHK027).
    /// </summary>
    public async Task RegistrarCobroAsync(
        FacturaCliente factura,
        DateOnly fechaCobro,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        var estadoAnterior = factura.Estado;

        factura.Estado = EstadoFactura.Pagada;
        factura.FechaCobro = fechaCobro;

        factura.CambiosDeEstado.Add(new CambioDeEstadoFactura
        {
            FacturaId = factura.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoFactura.Pagada,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        await contexto.SaveChangesAsync(cancelacion);
    }

    // ── Reportes ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El panel de vencimientos (FR-063): las <c>vencida</c> y las que vencen dentro de los 7 días
    /// corridos siguientes.
    ///
    /// <c>Estado == Pendiente</c> excluye las <c>pagada</c> y las <c>anulada</c> como <b>predicado de la
    /// consulta</b>, no como filtrado posterior: escrito así, la exclusión es una garantía y no algo
    /// que alguien pueda olvidar (convención [004]).
    ///
    /// Los días se calculan con <c>DateDiffDay</c> <b>en la base</b>, sobre el mismo <c>hoy</c> que
    /// acota la ventana: si se calcularan al leer, la fila podría decir "vence en 8 días" habiendo
    /// entrado por una comparación contra otro instante.
    /// </summary>
    public async Task<IReadOnlyList<FilaDeVencimiento>> ConsultarVencimientosAsync(
        DateOnly hoy,
        CancellationToken cancelacion = default)
    {
        var limite = hoy.AddDays(7);

        return await contexto.Facturas
            .Where(factura =>
                factura.Estado == EstadoFactura.Pendiente &&
                factura.VencimientoPago <= limite)
            .OrderBy(factura => factura.VencimientoPago)
            .ThenBy(factura => factura.NumeroComprobante)
            .Select(factura => new FilaDeVencimiento(
                factura.Id,
                factura.NumeroComprobante,
                factura.ClienteRazonSocial,
                factura.Total,
                factura.VencimientoPago.ToString("yyyy-MM-dd"),
                EF.Functions.DateDiffDay(hoy, factura.VencimientoPago)))
            .AsNoTracking()
            .ToListAsync(cancelacion);
    }

    /// <summary>
    /// Facturado, cobrado y pendiente por cliente, <b>agregados dentro de la consulta</b> (FR-061).
    ///
    /// La exclusión de las anuladas va escrita <b>una sola vez</b>, sobre el conjunto del que salen las
    /// tres columnas: escrita así no puede diferir entre una columna y otra, ni entre estas y el
    /// listado. Eso es lo que sostiene SC-011 (FR-062).
    ///
    /// El cobrado sale de un <c>CASE WHEN</c> que EF traduce solo: sumar sólo las pagadas en una segunda
    /// consulta daría el mismo número con dos viajes a la base y una oportunidad más de que los dos
    /// predicados se separen.
    /// </summary>
    public async Task<IReadOnlyList<TotalPorCliente>> ConsultarTotalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var enElRango = contexto.Facturas.Where(factura =>
            factura.Fecha >= desde &&
            factura.Fecha <= hasta &&
            factura.Estado != EstadoFactura.Anulada);

        // La agregación se arma sobre un tipo anónimo y recién después se convierte al DTO: EF Core
        // traduce `GroupBy` + `Select` a un `GROUP BY` con sus agregados, pero ordenar por una propiedad
        // de un récord ya proyectado lo obliga a interpretar un constructor propio, y ahí deja de
        // traducir (mismo criterio que los totales del Módulo 5).
        var filas = await enElRango
            .GroupBy(factura => new { factura.ClienteId, factura.ClienteRazonSocial })
            .Select(grupo => new
            {
                grupo.Key.ClienteId,
                grupo.Key.ClienteRazonSocial,
                Cantidad = grupo.Count(),
                Facturado = grupo.Sum(factura => factura.Total),
                Cobrado = grupo.Sum(factura =>
                    factura.Estado == EstadoFactura.Pagada ? factura.Total : 0m),
            })
            .OrderBy(fila => fila.ClienteRazonSocial)
            .ThenBy(fila => fila.ClienteId)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return [.. filas.Select(fila => new TotalPorCliente(
            fila.ClienteId,
            fila.ClienteRazonSocial,
            fila.Cantidad,
            fila.Facturado,
            fila.Cobrado,
            fila.Facturado - fila.Cobrado))];
    }

    // ── Traducción de las violaciones de índice (convención [003]) ──────────────────────────────

    /// <summary>
    /// Traduce las violaciones de los dos índices únicos a excepciones de la capa de aplicación,
    /// <b>distinguiendo cuál se violó por su nombre</b>.
    ///
    /// Sin esa distinción, una carrera por el número de comprobante y una por la refacturación llegarían
    /// arriba como el mismo error y el rechazo no podría decir qué pasó.
    /// </summary>
    private async Task GuardarTraduciendoIndicesAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (IndiceVioladoEn(excepcion) is { } indice)
        {
            throw indice switch
            {
                FacturaConfiguracion.IndiceNumero => new NumeroDuplicadoException(excepcion),
                FacturaConfiguracion.IndiceFacturaReemplazada =>
                    new AnuladaYaReemplazadaException(excepcion),
                _ => excepcion,
            };
        }
    }

    private static string? IndiceVioladoEn(DbUpdateException excepcion)
    {
        if (excepcion.InnerException is not SqlException { Number: 2601 or 2627 } sql)
        {
            return null;
        }

        string[] indices =
        [
            FacturaConfiguracion.IndiceNumero,
            FacturaConfiguracion.IndiceFacturaReemplazada,
        ];

        return Array.Find(indices, indice => sql.Message.Contains(indice, StringComparison.Ordinal));
    }

    /// <summary>
    /// Borra el documento anterior, y sólo si de verdad cambió: si el armador devolviera la misma ruta,
    /// borrarla dejaría a la factura apuntando a un archivo que ya no existe.
    ///
    /// <c>BorrarAsync</c> no falla si el archivo ya no está: el objetivo es que deje de existir, y que
    /// otro se haya adelantado no es un error (Módulo 3).
    /// </summary>
    private Task BorrarSiCambio(string anterior, string nueva, CancellationToken cancelacion) =>
        anterior == nueva
            ? Task.CompletedTask
            : almacen.BorrarAsync(anterior, cancelacion);
}
