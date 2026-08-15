using GT.Domain.Usuarios;

namespace GT.Domain.Facturacion;

/// <summary>
/// Historial de la factura (FR-045) <b>y</b> registro de correcciones (FR-037), en la misma tabla.
///
/// <b>Una entrada es una corrección cuando <see cref="EstadoNuevo"/> es <c>null</c>.</b> No hay
/// columna <c>EsCorreccion</c>: la ausencia de estado nuevo <b>es</b> la marca, y una columna que
/// repite un dato que ya está puede discrepar de él. En la pantalla se lee <c>Corrección de datos</c>.
///
/// <b>No guarda qué campos cambiaron ni sus valores anteriores</b> (FR-037): registra quién y cuándo,
/// y nada más. Una auditoría de valores sería una entidad que ningún otro módulo del sistema tiene, y
/// recolectar por las dudas es lo que el Principio V descarta.
///
/// <b>No se edita ni se borra por ninguna vía</b>: ningún endpoint la escribe directamente. La
/// escriben los casos de uso, en la misma transacción que el cambio que registran.
/// </summary>
public class CambioDeEstadoFactura
{
    public int Id { get; set; }

    public required int FacturaId { get; set; }

    public FacturaCliente? Factura { get; set; }

    /// <summary>
    /// <c>null</c> en el registro de la <b>emisión</b> —antes no había estado— y también en el de una
    /// <b>corrección</b>, que no cambia ningún estado.
    /// </summary>
    public EstadoFactura? EstadoAnterior { get; set; }

    /// <summary>
    /// <c>null</c> <b>sólo</b> en una corrección (FR-037). Es lo que distingue las dos clases de
    /// entrada sin necesidad de una columna que lo diga.
    /// </summary>
    public EstadoFactura? EstadoNuevo { get; set; }

    /// <summary>Llega por parámetro desde el endpoint, igual que en el Módulo 5.</summary>
    public required int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>
    /// Instante UTC del servidor, leído del <c>TimeProvider</c> registrado. Sale con la <c>Z</c> que
    /// lo declara por la conversión de <c>GtDbContext</c> (convención [002]).
    /// </summary>
    public required DateTime OcurridoEn { get; set; }

    /// <summary><c>true</c> si esta entrada registra una corrección de datos y no un cambio de estado.</summary>
    public bool EsCorreccion => EstadoNuevo is null;
}
