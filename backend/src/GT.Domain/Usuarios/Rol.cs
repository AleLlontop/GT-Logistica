namespace GT.Domain.Usuarios;

/// <summary>
/// Agrupación fija de permisos asignada a uno o más usuarios. El catálogo es fijo y queda cargado
/// en la instalación (FR-019); la asignación de roles a usuarios es responsabilidad del Módulo 2.
/// </summary>
public class Rol
{
    public int Id { get; set; }

    /// <summary>Identificador estable para el código, por ejemplo <c>administrador_sistema</c>.</summary>
    public required string Codigo { get; set; }

    /// <summary>Nombre visible, en español.</summary>
    public required string Nombre { get; set; }

    public ICollection<Permiso> Permisos { get; set; } = [];

    public ICollection<Usuario> Usuarios { get; set; } = [];
}

/// <summary>Códigos de los cuatro roles del sistema. Fijos en esta versión.</summary>
public static class CodigosRol
{
    public const string Trafico = "trafico";
    public const string Administracion = "administracion";
    public const string Gerencia = "gerencia";
    public const string AdministradorSistema = "administrador_sistema";
}

/// <summary>Códigos de los permisos del sistema. Cada módulo agrega el suyo cuando se construye.</summary>
public static class CodigosPermiso
{
    public const string UsuariosGestionar = "usuarios.gestionar";

    /// <summary>
    /// Módulo 3. A diferencia del anterior, no es exclusivo del administrador: lo otorgan
    /// *Tráfico* y *Administrador del sistema* (FR-027). Es un único permiso para todo el módulo,
    /// porque la spec no distingue niveles de acceso dentro de él.
    /// </summary>
    public const string ChoferesGestionar = "choferes.gestionar";

    /// <summary>
    /// Sólo el **panel de vencimientos** de la documentación de choferes, sin el resto del Módulo 3.
    /// Lo otorgan *Tráfico*, *Gerencia* y *Administrador del sistema*.
    ///
    /// Va aparte de <c>choferes.gestionar</c> porque Gerencia tiene que ver qué documentación está por
    /// vencer sin poder tocar el padrón ni descargar un escaneo, que es un dato personal sensible
    /// (Módulo 3, FR-024, SC-011). Quien gestiona el módulo lo recibe además de <c>choferes.gestionar</c>,
    /// sembrado por separado: no son niveles ordenados.
    /// </summary>
    public const string ChoferesVencimientosConsultar = "choferes.vencimientos.consultar";

    /// <summary>
    /// Módulo 4: vehículos, su documentación, el panel de vencimientos y la descarga de adjuntos. Lo
    /// otorgan *Tráfico* y *Administrador del sistema* (FR-039).
    /// </summary>
    public const string FlotaGestionar = "flota.gestionar";

    /// <summary>
    /// Sólo el **panel de vencimientos** de la documentación de vehículos, sin el resto del Módulo 4.
    /// Lo otorgan *Tráfico*, *Gerencia* y *Administrador del sistema*, por el mismo motivo que
    /// <see cref="ChoferesVencimientosConsultar"/>.
    /// </summary>
    public const string FlotaVencimientosConsultar = "flota.vencimientos.consultar";

    /// <summary>
    /// Módulo 4: sólo el ABM del catálogo de tipos de vehículo, y lo otorga únicamente
    /// *Administrador del sistema* (FR-039).
    ///
    /// Es la primera vez que un módulo distingue niveles de acceso <b>adentro</b>. Se resuelve con
    /// dos permisos y no con un chequeo de rol en el endpoint, porque la convención del Módulo 1 es
    /// autorizar por permiso y nunca por rol (research §7).
    /// </summary>
    public const string FlotaTiposGestionar = "flota.tipos.gestionar";

    /// <summary>
    /// Módulo 5: registrar viajes, editarlos, asignar chofer y vehículo, y cambiar su estado. Lo
    /// otorgan *Tráfico* y *Administrador del sistema* (FR-051).
    /// </summary>
    public const string ViajesGestionar = "viajes.gestionar";

    /// <summary>
    /// Módulo 5: mirar el listado, la ficha y los totales, sin poder tocar nada.
    ///
    /// Lo otorgan <b>los cuatro roles</b>. Es el primer permiso que llega a *Administración de la
    /// empresa* y a *Gerencia*, que hasta el Módulo 4 no tenían ninguno: la pregunta "¿en qué anda el
    /// viaje de tal cliente?" la hacen ellos, y responderla no exige poder operar (FR-051, research §10).
    /// </summary>
    public const string ViajesConsultar = "viajes.consultar";

    /// <summary>
    /// Módulo 6: configurar la empresa emisora, emitir una factura, corregirla y registrar su cobro.
    /// Lo otorgan *Administración de la empresa* y *Administrador del sistema* (FR-066, research §7).
    ///
    /// Es el primer permiso de escritura que **no** recibe Tráfico: facturar es tarea administrativa.
    /// </summary>
    public const string FacturacionGestionar = "facturacion.gestionar";

    /// <summary>
    /// Módulo 6: listado, ficha, documento, panel de vencimientos y totales, sin poder tocar nada.
    /// Lo otorgan los tres roles anteriores **más** Gerencia (FR-066).
    /// </summary>
    public const string FacturacionConsultar = "facturacion.consultar";

    /// <summary>
    /// Módulo 6: anular una factura. Lo otorga **sólo** *Administrador del sistema* (FR-067).
    ///
    /// Con esto el 6 pasa a ser el módulo con la autorización más granular del sistema —tres permisos—
    /// y no agregó una línea de maquinaria: es el precedente [004] con un nivel más. Quien tiene
    /// `facturacion.gestionar` sin éste ve todas las acciones menos *Anular*, y si la invoca a mano
    /// recibe `403` (FR-068, SC-014).
    /// </summary>
    public const string FacturacionAnular = "facturacion.anular";

    /// <summary>
    /// Módulo 9: generar, editar y anular liquidaciones, y registrar sus órdenes de pago. Lo otorgan
    /// *Administración de la empresa* y *Administrador del sistema* (FR-064, research §9).
    ///
    /// Anular **no** lleva un permiso aparte, a diferencia de la factura: sólo procede sin pagos y
    /// libera los viajes para volver a liquidarlos (spec §Assumptions).
    /// </summary>
    public const string LiquidacionesGestionar = "liquidaciones.gestionar";

    /// <summary>
    /// Módulo 9: listado y detalle. Lo otorgan los dos roles anteriores **más** Gerencia, que responde
    /// "cuánto se le debe a cada fletero" con el listado filtrado (FR-064).
    /// </summary>
    public const string LiquidacionesConsultar = "liquidaciones.consultar";

    /// <summary>
    /// Módulo 10: registrar, aprobar, rechazar y anular adelantos, y el desplegable de beneficiarios.
    /// Lo otorgan *Administración de la empresa* y *Administrador del sistema* (FR-042, research §7).
    ///
    /// Aprobar **no** lleva un permiso aparte: quien registra un adelanto puede aprobarlo (FR-022).
    /// </summary>
    public const string AdelantosGestionar = "adelantos.gestionar";

    /// <summary>
    /// Módulo 10: listado, detalle y las personas del filtro. Lo otorgan los dos roles anteriores **más**
    /// Gerencia, que controla cuánto se adelanta sobre los sueldos (FR-042).
    /// </summary>
    public const string AdelantosConsultar = "adelantos.consultar";

    /// <summary>
    /// Módulo 11: abrir la propia caja, registrar sus movimientos y cerrarla, y los dos desplegables de
    /// referencia. Lo otorgan *Administración de la empresa* y *Administrador del sistema* (FR-031, research §9).
    ///
    /// Un solo permiso de escritura: no hay anulación ni reversión que pida uno aparte. Que la caja sea del
    /// usuario en sesión no lo decide el permiso sino el servidor en cada escritura (FR-035).
    /// </summary>
    public const string CajaGestionar = "caja.gestionar";

    /// <summary>
    /// Módulo 11: listado de cajas, detalle y movimientos. Lo otorgan los dos roles anteriores **más**
    /// Gerencia, que revisa cajas y movimientos sin operar (FR-032).
    /// </summary>
    public const string CajaConsultar = "caja.consultar";
}
