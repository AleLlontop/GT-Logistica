using GT.Application.Choferes;
using GT.Application.Flota;
using GT.Domain.Flota;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioVehiculos(GtDbContext contexto) : IRepositorioVehiculos
{
    public Task AgregarAsync(Vehiculo vehiculo, CancellationToken cancelacion = default)
    {
        contexto.Vehiculos.Add(vehiculo);
        return Task.CompletedTask;
    }

    public Task<Vehiculo?> ObtenerPorPatenteAsync(
        string patenteNormalizada,
        int? idAExcluir = null,
        CancellationToken cancelacion = default) =>
        contexto.Vehiculos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                vehiculo => vehiculo.Patente == patenteNormalizada &&
                    (idAExcluir == null || vehiculo.Id != idAExcluir),
                cancelacion);

    public Task<Vehiculo?> ObtenerPorIdConRelacionesAsync(
        int id,
        CancellationToken cancelacion = default) =>
        contexto.Vehiculos
            .Include(vehiculo => vehiculo.Tipo)
            .Include(vehiculo => vehiculo.Transportista)
            .Include(vehiculo => vehiculo.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .AsNoTracking()
            .FirstOrDefaultAsync(vehiculo => vehiculo.Id == id, cancelacion);

    public Task<Vehiculo?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Vehiculos
            .Include(vehiculo => vehiculo.Tipo)
            .Include(vehiculo => vehiculo.Transportista)
            .Include(vehiculo => vehiculo.Documentacion)
                .ThenInclude(documento => documento.Tipo)
            .FirstOrDefaultAsync(vehiculo => vehiculo.Id == id, cancelacion);

    /// <summary>
    /// Traduce la violación del índice único de la patente a una excepción de la capa de aplicación
    /// (convención [003]).
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion = default)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsPatenteDuplicada(excepcion))
        {
            throw new PatenteDuplicadaException(excepcion);
        }
    }

    /// <summary>
    /// El listado con todo resuelto en la base (research §4, §5 y §9).
    ///
    /// Tres cosas viajan como subconsulta correlacionada y no como filas traídas a memoria:
    /// <list type="bullet">
    ///   <item><b>Cuál es el documento vigente de cada tipo</b>, expresado como "no existe otro del
    ///   mismo tipo que le gane por vencimiento, o por <c>Id</c> si empatan". EF lo traduce a un
    ///   <c>NOT EXISTS</c> que el índice <c>VehiculoId, TipoId, FechaVencimiento DESC</c> resuelve
    ///   directo (FR-024).</item>
    ///   <item><b>El estado de esos vigentes</b>, contado en tres números —cuántos hay, cuántos
    ///   vencidos y cuántos por vencer—. Con eso alcanza para los cuatro valores de FR-033 sin traer
    ///   un solo documento.</item>
    ///   <item><b>El estado operativo derivado</b>, que sale de combinar esos conteos con la columna
    ///   guardada. Es lo que permite filtrar por <c>disponible</c> sin recorrer toda la flota
    ///   (FR-014, FR-015).</item>
    /// </list>
    ///
    /// <b>El predicado del vigente va escrito a mano en cada conteo, y no extraído a un método</b>,
    /// porque EF Core sólo traduce lo que ve en el árbol de expresión: una llamada a un método propio
    /// rompería la traducción y la consulta se evaluaría en memoria (convención [003]).
    /// </summary>
    public async Task<PaginaDe<VehiculoListado>> ConsultarAsync(
        FiltrosDeFlota filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Vehiculos.AsQueryable();

        if (filtros.TransportistaId is { } transportistaId)
        {
            consulta = consulta.Where(vehiculo => vehiculo.TransportistaId == transportistaId);
        }

        if (filtros.TipoVehiculoId is { } tipoVehiculoId)
        {
            consulta = consulta.Where(vehiculo => vehiculo.TipoVehiculoId == tipoVehiculoId);
        }

        var conEstado = consulta.Select(vehiculo => new
        {
            Vehiculo = vehiculo,

            Vigentes = vehiculo.Documentacion.Count(documento =>
                !vehiculo.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id)))),

            Vencidos = vehiculo.Documentacion.Count(documento =>
                !vehiculo.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))) &&
                documento.FechaVencimiento < hoy),

            PorVencer = vehiculo.Documentacion.Count(documento =>
                !vehiculo.Documentacion.Any(otro =>
                    otro.DocumentacionTipoId == documento.DocumentacionTipoId &&
                    (otro.FechaVencimiento > documento.FechaVencimiento ||
                     (otro.FechaVencimiento == documento.FechaVencimiento && otro.Id > documento.Id))) &&
                documento.FechaVencimiento >= hoy &&
                documento.FechaVencimiento <= hoy.AddDays(documento.Tipo!.DiasAvisoVencimiento)),
        });

        // El control de estado es único y sus tres valores son excluyentes (FR-030a). Los dos
        // operativos son complementarios dentro de los activos: todo vehículo activo cae en
        // exactamente uno, y por eso `disponible` **no puede** devolver una unidad con documentación
        // vencida o ausente. Lo garantiza este predicado, no un filtrado posterior (FR-015, SC-006).
        conEstado = filtros.Estado switch
        {
            FiltroEstadoVehiculo.DadoDeBaja => conEstado.Where(fila => !fila.Vehiculo.Activo),

            FiltroEstadoVehiculo.Disponible => conEstado.Where(fila =>
                fila.Vehiculo.Activo &&
                fila.Vehiculo.EstadoOperativo == VehiculoEstado.Disponible &&
                fila.Vigentes > 0 &&
                fila.Vencidos == 0),

            FiltroEstadoVehiculo.FueraDeServicio => conEstado.Where(fila =>
                fila.Vehiculo.Activo &&
                (fila.Vehiculo.EstadoOperativo == VehiculoEstado.FueraDeServicio ||
                 fila.Vigentes == 0 ||
                 fila.Vencidos > 0)),

            // Sin filtro de estado se muestran sólo los activos (FR-031). No es lo mismo que "todos".
            _ => conEstado.Where(fila => fila.Vehiculo.Activo),
        };

        // El filtro por estado de documentación se aplica sobre el valor calculado, en la base.
        conEstado = filtros.EstadoDocumentacion switch
        {
            EstadoDocumentacionVehiculo.SinDocumentacion => conEstado.Where(fila => fila.Vigentes == 0),
            EstadoDocumentacionVehiculo.Vencida => conEstado.Where(fila => fila.Vencidos > 0),
            EstadoDocumentacionVehiculo.ProximaAvencer => conEstado.Where(fila =>
                fila.Vencidos == 0 && fila.PorVencer > 0),
            EstadoDocumentacionVehiculo.EnRegla => conEstado.Where(fila =>
                fila.Vigentes > 0 && fila.Vencidos == 0 && fila.PorVencer == 0),
            _ => conEstado,
        };

        // El total cuenta las coincidencias completas con los filtros, no las de esta página (FR-032).
        var total = await conEstado.CountAsync(cancelacion);

        // Orden total: la patente ya es única, así que ordenar por ella alcanzaría. El `Id` va igual
        // porque la convención [003] lo pide sin excepciones y un orden que depende de una
        // restricción de unicidad para ser total es frágil ante cualquier cambio futuro (research §9).
        var filas = await conEstado
            .OrderBy(fila => fila.Vehiculo.Patente)
            .ThenBy(fila => fila.Vehiculo.Id)
            .Skip((filtros.Pagina - 1) * PaginaDe<VehiculoListado>.TamanioPorDefecto)
            .Take(PaginaDe<VehiculoListado>.TamanioPorDefecto)
            .Select(fila => new
            {
                fila.Vehiculo.Id,
                fila.Vehiculo.Patente,
                fila.Vehiculo.Marca,
                fila.Vehiculo.Modelo,
                TipoId = fila.Vehiculo.Tipo!.Id,
                TipoNombre = fila.Vehiculo.Tipo.Nombre,
                TransportistaId = fila.Vehiculo.Transportista!.Id,
                TransportistaNombre = fila.Vehiculo.Transportista.Nombre,
                fila.Vehiculo.Activo,
                fila.Vehiculo.EstadoOperativo,
                fila.Vigentes,
                fila.Vencidos,
                fila.PorVencer,
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        var items = filas
            .Select(fila =>
            {
                var estadoDocumentacion = EstadoDesde(fila.Vigentes, fila.Vencidos, fila.PorVencer);

                return new VehiculoListado(
                    fila.Id,
                    fila.Patente,
                    fila.Marca,
                    fila.Modelo,
                    new Resumen(fila.TipoId, fila.TipoNombre),
                    new Resumen(fila.TransportistaId, fila.TransportistaNombre),
                    fila.Activo,
                    // El estado que se muestra es el derivado, no el guardado (FR-014).
                    NombresDeEstadoFlota.DelVehiculo(
                        CalculadorEstadoOperativo.Derivar(fila.EstadoOperativo, estadoDocumentacion)),
                    NombresDeEstadoFlota.DeLaDocumentacion(estadoDocumentacion));
            })
            .ToList();

        return new PaginaDe<VehiculoListado>(
            items,
            total,
            filtros.Pagina,
            PaginaDe<VehiculoListado>.TamanioPorDefecto);
    }

    /// <summary>
    /// Precedencia de FR-033: <c>vencida</c> &gt; <c>proximaAvencer</c> &gt; <c>enRegla</c>. Sin
    /// ningún documento vigente, <c>sinDocumentacion</c>, que no es lo mismo que estar en regla.
    ///
    /// Es la misma regla que <c>CalculadorEstadoVehiculo</c> aplica en el dominio, escrita acá sobre
    /// los tres conteos que resolvió la consulta. Un test compara las dos sobre el mismo dato
    /// (convención [003]).
    /// </summary>
    private static EstadoDocumentacionVehiculo EstadoDesde(int vigentes, int vencidos, int porVencer)
    {
        if (vigentes == 0) return EstadoDocumentacionVehiculo.SinDocumentacion;
        if (vencidos > 0) return EstadoDocumentacionVehiculo.Vencida;

        return porVencer > 0
            ? EstadoDocumentacionVehiculo.ProximaAvencer
            : EstadoDocumentacionVehiculo.EnRegla;
    }

    private static bool EsPatenteDuplicada(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_Vehiculos_Patente", StringComparison.Ordinal);
}
