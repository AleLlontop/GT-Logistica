using GT.Application.Choferes.Transportistas;
using GT.Domain.Choferes;

namespace GT.Application.Choferes;

/// <summary>
/// Baja lógica de un chofer (FR-005).
///
/// No borra nada: lo deja inactivo. Su persona queda en el padrón del Módulo 2 y su documentación se
/// conserva intacta (FR-005a), así que vuelve completa si más adelante se lo reactiva.
///
/// A partir de la baja deja de aparecer en el listado sin filtros y en el panel de vencimientos
/// (FR-021, FR-022). Eso no se hace acá: sale solo de que las dos consultas filtran por chofer
/// activo.
/// </summary>
public class DarDeBajaChofer(IRepositorioChoferes choferes)
{
    public async Task<ResultadoChofer> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var chofer = await choferes.ObtenerParaModificarAsync(id, cancelacion);
        if (chofer is null)
        {
            return new ResultadoChofer(ErrorChofer.NoEncontrado);
        }

        // Dar de baja a alguien que ya está de baja no es un error: el resultado buscado ya se
        // cumple, y fallar sólo complicaría a quien tocó dos veces el botón.
        chofer.Activo = false;
        await choferes.GuardarCambiosAsync(cancelacion);

        return new ResultadoChofer(ErrorChofer.Ninguno);
    }
}

/// <summary>
/// Reactivación de un chofer dado de baja (FR-005b).
///
/// Vuelve a ponerlo activo. Desde ese momento aparece en el listado por defecto y, si su
/// documentación lo amerita, vuelve a alertar en el panel sin que nadie recargue nada: el estado se
/// calcula al consultarlo.
///
/// Se rechaza en dos casos, y los dos importan:
/// <list type="bullet">
///   <item><b>Si ya está activo</b>: la pantalla ofrece <i>Reactivar</i> sólo cuando corresponde, así
///   que pedirlo sobre uno activo significa que la vista quedó vieja.</item>
///   <item><b>Si su transportista quedó inactivo</b>: reactivarlo dejaría un chofer activo colgando
///   de un transportista dado de baja, que es justo lo que FR-008 no admite al darlo de alta.</item>
/// </list>
/// </summary>
public class ReactivarChofer(IRepositorioChoferes choferes, IRepositorioTransportistas transportistas)
{
    public async Task<ResultadoChofer> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var chofer = await choferes.ObtenerParaModificarAsync(id, cancelacion);
        if (chofer is null)
        {
            return new ResultadoChofer(ErrorChofer.NoEncontrado);
        }

        if (chofer.Activo)
        {
            return new ResultadoChofer(ErrorChofer.YaEstaActivo);
        }

        var transportista = await transportistas.ObtenerPorIdAsync(chofer.TransportistaId, cancelacion);
        if (transportista is null || !transportista.Activo)
        {
            return new ResultadoChofer(ErrorChofer.TransportistaInexistente, Campo: "transportistaId");
        }

        chofer.Activo = true;
        await choferes.GuardarCambiosAsync(cancelacion);

        return new ResultadoChofer(ErrorChofer.Ninguno);
    }
}
