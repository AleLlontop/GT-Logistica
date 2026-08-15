using GT.Application.Choferes;
using GT.Application.Viajes;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia.Configuraciones;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioViajes(GtDbContext contexto) : IRepositorioViajes
{
    /// <summary>
    /// Colación insensible a mayúsculas <b>y a acentos</b>. Es lo que hace que <c>cordoba</c>
    /// encuentre <c>Córdoba</c> y que <c>CÓRDOBA</c> encuentre <c>córdoba</c> (FR-042, research §8).
    ///
    /// La búsqueda con <c>LIKE '%texto%'</c> no usa índice, y está aceptado y anotado: sin la
    /// colación tampoco lo usaría, y el volumen del sistema —decenas de viajes por semana— no lo
    /// justifica (plan §Performance Goals).
    /// </summary>
    private const string ColacionSinAcentos = "Latin1_General_CI_AI";

    public Task AgregarAsync(Viaje viaje, CancellationToken cancelacion = default)
    {
        contexto.Viajes.Add(viaje);
        return Task.CompletedTask;
    }

    public Task<Viaje?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Viajes
            .Include(viaje => viaje.Cliente)
            .Include(viaje => viaje.Chofer!).ThenInclude(chofer => chofer.Persona)
            .Include(viaje => viaje.Chofer!).ThenInclude(chofer => chofer.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .Include(viaje => viaje.Vehiculo!).ThenInclude(vehiculo => vehiculo.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .Include(viaje => viaje.Transportista)
            .FirstOrDefaultAsync(viaje => viaje.Id == id, cancelacion);

    public Task<Viaje?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Viajes
            .Include(viaje => viaje.Cliente)
            .Include(viaje => viaje.Chofer!).ThenInclude(chofer => chofer.Persona)
            .Include(viaje => viaje.Vehiculo)
            .Include(viaje => viaje.Transportista)
            .Include(viaje => viaje.CambiosDeEstado).ThenInclude(cambio => cambio.Usuario)
            // Módulo 6, FR-055: la ficha muestra el número y la fecha de la factura de un viaje
            // facturado. Sale de la navegación, nunca de columnas copiadas al viaje.
            .Include(viaje => viaje.Factura)
            .AsNoTracking()
            .FirstOrDefaultAsync(viaje => viaje.Id == id, cancelacion);

    public Task<Viaje?> ObtenerPorRemitoAsync(
        string numeroRemito,
        int? idAExcluir = null,
        CancellationToken cancelacion = default) =>
        contexto.Viajes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                viaje => viaje.NumeroRemito == numeroRemito &&
                    viaje.Estado != EstadoViaje.Anulado &&
                    (idAExcluir == null || viaje.Id != idAExcluir),
                cancelacion);

    /// <summary>
    /// El listado con todo resuelto en la base (FR-041 a FR-044, research §6, §8, §12).
    ///
    /// <b>Las dos señales derivadas viajan dentro de la consulta</b>, no como un recorrido posterior
    /// de las filas:
    ///
    /// <list type="bullet">
    ///   <item><c>esRetroactivo</c> es una comparación de la fecha del viaje contra el día en curso
    ///   en Argentina (FR-016).</item>
    ///   <item><c>demorado</c> sale de una <b>subconsulta correlacionada</b> al historial, que toma el
    ///   instante en que el viaje pasó a <c>en curso</c>. Existe a lo sumo una de esas líneas, porque
    ///   <c>pendiente → en curso</c> es la única transición que llega a ese estado y no hay camino de
    ///   vuelta; un viaje que nunca arrancó no tiene ninguna y la comparación da <c>false</c>
    ///   (FR-039, research §6).</item>
    /// </list>
    ///
    /// <b>Todo va escrito en el árbol de expresión y nada extraído a un método propio</b>: EF Core
    /// sólo traduce lo que ve, y una llamada a un método rompería la traducción dejando la consulta
    /// evaluándose en memoria (convención [003]). <c>TraduccionConsultaTests</c> lo verifica sobre el
    /// SQL generado.
    /// </summary>
    public async Task<PaginaDe<ViajeListado>> ConsultarAsync(
        FiltrosDeViajes filtros,
        MomentoDeLectura momento,
        CancellationToken cancelacion = default)
    {
        // En variables locales para que EF las tome como parámetros de la consulta. Leer una
        // propiedad calculada del récord dentro del árbol obligaría a EF a decidir si la evalúa acá o
        // en la base, y ese es justo el tipo de duda que termina en evaluación en memoria.
        var limiteDeDemora = momento.LimiteDeDemora;
        var hoy = momento.Hoy;

        var consulta = contexto.Viajes.AsQueryable();

        if (filtros.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(viaje => viaje.ClienteId == clienteId);
        }

        if (filtros.TransportistaId is { } transportistaId)
        {
            consulta = consulta.Where(viaje => viaje.TransportistaId == transportistaId);
        }

        // **Predicado único**, no un filtrado posterior: sin filtro de estado los anulados no
        // aparecen, y con el filtro `anulado` aparecen sólo ellos. Escrito así, la exclusión es una
        // garantía de la consulta y no algo que alguien pueda olvidar más adelante (FR-044).
        consulta = filtros.Estado is { } estado
            ? consulta.Where(viaje => viaje.Estado == estado)
            : consulta.Where(viaje => viaje.Estado != EstadoViaje.Anulado);

        if (filtros.Desde is { } desde)
        {
            consulta = consulta.Where(viaje => viaje.Fecha >= desde);
        }

        if (filtros.Hasta is { } hasta)
        {
            consulta = consulta.Where(viaje => viaje.Fecha <= hasta);
        }

        if (!string.IsNullOrWhiteSpace(filtros.Busqueda))
        {
            var patron = $"%{filtros.Busqueda.Trim()}%";

            consulta = consulta.Where(viaje =>
                EF.Functions.Like(EF.Functions.Collate(viaje.Origen, ColacionSinAcentos), patron) ||
                EF.Functions.Like(EF.Functions.Collate(viaje.Destino, ColacionSinAcentos), patron) ||
                EF.Functions.Like(
                    EF.Functions.Collate(viaje.Cliente!.RazonSocial, ColacionSinAcentos),
                    patron));
        }

        // El total cuenta las coincidencias completas con los filtros, no las de esta página (FR-043).
        var total = await consulta.CountAsync(cancelacion);

        // **Orden total que no termina en `Id`**, y es el primero del sistema: termina en `Numero`,
        // que tiene índice único propio y además es el que ve el usuario. La convención [003] pide un
        // orden total, no uno que termine en `Id`; ordenar además por `Id` sería ruido (research §12).
        var items = await consulta
            .OrderByDescending(viaje => viaje.Fecha)
            .ThenByDescending(viaje => viaje.Numero)
            .Skip((filtros.Pagina - 1) * PaginaDe<ViajeListado>.TamanioPorDefecto)
            .Take(PaginaDe<ViajeListado>.TamanioPorDefecto)
            .Select(viaje => new ViajeListado(
                viaje.Id,
                viaje.Numero,
                viaje.Fecha.ToString("yyyy-MM-dd"),
                new Resumen(viaje.Cliente!.Id, viaje.Cliente.RazonSocial, viaje.Cliente.Activo),
                viaje.Origen,
                viaje.Destino,
                viaje.Chofer == null
                    ? null
                    : new Resumen(
                        viaje.Chofer.Id,
                        viaje.Chofer.Persona!.Apellido + ", " + viaje.Chofer.Persona.Nombre,
                        viaje.Chofer.Activo),
                viaje.Vehiculo == null
                    ? null
                    : new Resumen(viaje.Vehiculo.Id, viaje.Vehiculo.Patente, viaje.Vehiculo.Activo),
                viaje.Transportista == null
                    ? null
                    : new Resumen(
                        viaje.Transportista.Id,
                        viaje.Transportista.Nombre,
                        viaje.Transportista.Activo),
                NombresDeEstadoViaje.EnJson(viaje.Estado),
                viaje.Importe,
                // FR-039. La subconsulta correlacionada al historial: el instante del pase a
                // `en curso` comparado contra `ahora − 5 días`.
                viaje.Estado == EstadoViaje.EnCurso &&
                    viaje.CambiosDeEstado
                        .Where(cambio => cambio.EstadoNuevo == EstadoViaje.EnCurso)
                        .Max(cambio => (DateTime?)cambio.OcurridoEn) < limiteDeDemora,
                // FR-016.
                viaje.Fecha < hoy,
                viaje.MotivoAnulacion,
                // Módulo 6, FR-055. Se resuelve **dentro de la consulta**, por la navegación: la fila
                // dice `Facturado en {número}, del {fecha}` sin una segunda vuelta a la base ni columnas
                // copiadas al viaje.
                viaje.Factura == null
                    ? null
                    : new FacturaDelViaje(
                        viaje.Factura.Id,
                        viaje.Factura.NumeroComprobante,
                        viaje.Factura.Fecha.ToString("yyyy-MM-dd"))))
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return new PaginaDe<ViajeListado>(
            items,
            total,
            filtros.Pagina,
            PaginaDe<ViajeListado>.TamanioPorDefecto);
    }

    /// <summary>
    /// Las dos listas de la pantalla de asignación (FR-021).
    ///
    /// El vehículo se filtra por su <b>estado operativo guardado</b> y no por el derivado del
    /// Módulo 4: el derivado se calcula contra el día en curso, y usarlo acá dejaría fuera a una
    /// unidad que hoy tiene un papel vencido pero estaba en regla el día del viaje retroactivo que se
    /// está asentando (SC-014, research §3).
    ///
    /// Las dos listas pueden venir vacías, y es una respuesta legítima: la pantalla informa qué falta
    /// cargar y el viaje se queda <c>pendiente</c> sin asignar.
    /// </summary>
    /// <summary>
    /// Trae la documentación con su tipo porque de ella sale la observación de cada opción, que la
    /// calcula la capa de aplicación contra la fecha del viaje. Acá no se evalúa nada: la regla de
    /// habilitación vive en el dominio y una sola vez.
    /// </summary>
    public async Task<IReadOnlyList<Domain.Choferes.Chofer>> ConsultarChoferesAsignablesAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Choferes
            .Where(chofer => chofer.Activo)
            .Include(chofer => chofer.Persona)
            .Include(chofer => chofer.Documentacion).ThenInclude(documento => documento.Tipo)
            .OrderBy(chofer => chofer.Persona!.Apellido)
            .ThenBy(chofer => chofer.Persona!.Nombre)
            .ThenBy(chofer => chofer.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public async Task<IReadOnlyList<Domain.Flota.Vehiculo>> ConsultarVehiculosAsignablesAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Vehiculos
            .Where(vehiculo =>
                vehiculo.Activo &&
                vehiculo.EstadoOperativo == Domain.Flota.VehiculoEstado.Disponible)
            .Include(vehiculo => vehiculo.Documentacion).ThenInclude(documento => documento.Tipo)
            .OrderBy(vehiculo => vehiculo.Patente)
            .ThenBy(vehiculo => vehiculo.Id)
            .AsNoTracking()
            .ToListAsync(cancelacion);

    public Task<Domain.Choferes.Chofer?> ObtenerChoferParaAsignarAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Choferes
            .Include(chofer => chofer.Persona)
            .Include(chofer => chofer.Documentacion).ThenInclude(documento => documento.Tipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(chofer => chofer.Id == id, cancelacion);

    public Task<Domain.Flota.Vehiculo?> ObtenerVehiculoParaAsignarAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Vehiculos
            .Include(vehiculo => vehiculo.Documentacion).ThenInclude(documento => documento.Tipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(vehiculo => vehiculo.Id == id, cancelacion);

    public Task<Viaje?> ViajeEnCursoDelChoferAsync(
        int choferId,
        int viajeAExcluir,
        CancellationToken cancelacion = default) =>
        contexto.Viajes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                viaje => viaje.ChoferId == choferId &&
                    viaje.Estado == EstadoViaje.EnCurso &&
                    viaje.Id != viajeAExcluir,
                cancelacion);

    public Task<Viaje?> ViajeEnCursoDelVehiculoAsync(
        int vehiculoId,
        int viajeAExcluir,
        CancellationToken cancelacion = default) =>
        contexto.Viajes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                viaje => viaje.VehiculoId == vehiculoId &&
                    viaje.Estado == EstadoViaje.EnCurso &&
                    viaje.Id != viajeAExcluir,
                cancelacion);

    /// <summary>
    /// Los dos cuadros del período (FR-046, FR-046a, FR-047).
    ///
    /// <b>La exclusión de los anulados va escrita en la consulta</b>, sobre el mismo <c>enElPeriodo</c>
    /// del que salen las dos agregaciones: escrita una sola vez, no puede diferir entre un cuadro y el
    /// otro, ni entre estos y el listado. Eso es lo que sostiene SC-008.
    ///
    /// La fecha de corte es <b>la fecha del viaje</b>, no la de carga ni la de rendición (FR-046a).
    ///
    /// Los viajes sin transportista no aparecen en el segundo cuadro: un viaje sin chofer asignado
    /// todavía no tiene transportista, y es el comportamiento esperado.
    /// </summary>
    public async Task<TotalesDelPeriodo> ConsultarTotalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default)
    {
        var enElPeriodo = contexto.Viajes.Where(viaje =>
            viaje.Fecha >= desde &&
            viaje.Fecha <= hasta &&
            viaje.Estado != EstadoViaje.Anulado);

        // La agregación se arma sobre un tipo anónimo y recién después se convierte al DTO: EF Core
        // traduce `GroupBy` + `Select` a un `GROUP BY` con sus agregados, pero ordenar por una
        // propiedad de un récord ya proyectado lo obliga a interpretar un constructor propio, y ahí
        // deja de traducir.
        var porCliente = await enElPeriodo
            .GroupBy(viaje => new { viaje.ClienteId, viaje.Cliente!.RazonSocial })
            .Select(grupo => new
            {
                grupo.Key.ClienteId,
                grupo.Key.RazonSocial,
                Cantidad = grupo.Count(),
                Importe = grupo.Sum(viaje => viaje.Importe),
            })
            .OrderBy(fila => fila.RazonSocial)
            .ThenBy(fila => fila.ClienteId)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var porTransportista = await enElPeriodo
            .Where(viaje => viaje.TransportistaId != null)
            .GroupBy(viaje => new { viaje.TransportistaId, viaje.Transportista!.Nombre })
            .Select(grupo => new
            {
                grupo.Key.TransportistaId,
                grupo.Key.Nombre,
                Cantidad = grupo.Count(),
                Importe = grupo.Sum(viaje => viaje.Importe),
            })
            .OrderBy(fila => fila.Nombre)
            .ThenBy(fila => fila.TransportistaId)
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return new TotalesDelPeriodo(
            [.. porCliente.Select(fila => new TotalDelPeriodo(
                fila.ClienteId,
                fila.RazonSocial,
                fila.Cantidad,
                fila.Importe))],
            [.. porTransportista.Select(fila => new TotalDelPeriodo(
                fila.TransportistaId!.Value,
                fila.Nombre,
                fila.Cantidad,
                fila.Importe))]);
    }

    /// <summary>
    /// El cambio de estado y su línea de historial, en un solo <c>SaveChanges</c> —y por lo tanto en
    /// una sola transacción— (FR-035).
    ///
    /// El viaje ya viene seguido por el contexto, así que agregar la línea a su colección alcanza
    /// para que EF la inserte junto con el <c>UPDATE</c> del estado.
    /// </summary>
    public Task RegistrarCambioDeEstadoAsync(
        Viaje viaje,
        EstadoViaje estadoNuevo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default)
    {
        var estadoAnterior = viaje.Estado;

        viaje.Estado = estadoNuevo;

        viaje.CambiosDeEstado.Add(new CambioDeEstadoViaje
        {
            ViajeId = viaje.Id,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            UsuarioId = usuarioId,
            OcurridoEn = ocurridoEn,
        });

        return GuardarCambiosAsync(cancelacion);
    }

    /// <summary>
    /// Traduce las violaciones de los índices únicos a excepciones de la capa de aplicación,
    /// <b>distinguiendo cuál se violó por su nombre</b> (convención [003]).
    ///
    /// Sin esa distinción, una carrera por el remito y una por el chofer llegarían arriba como el
    /// mismo error y el rechazo no podría decir qué pasó.
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (IndiceVioladoEn(excepcion) is { } indice)
        {
            throw indice switch
            {
                ViajeConfiguracion.IndiceRemito => new RemitoDuplicadoException(excepcion),
                ViajeConfiguracion.IndiceChoferEnCurso =>
                    new UnidadOcupadaException(esDelChofer: true, excepcion),
                ViajeConfiguracion.IndiceVehiculoEnCurso =>
                    new UnidadOcupadaException(esDelChofer: false, excepcion),
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
            ViajeConfiguracion.IndiceRemito,
            ViajeConfiguracion.IndiceChoferEnCurso,
            ViajeConfiguracion.IndiceVehiculoEnCurso,
        ];

        return Array.Find(
            indices,
            indice => sql.Message.Contains(indice, StringComparison.Ordinal));
    }
}
