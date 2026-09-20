using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.DatosIniciales;

/// <summary>
/// Datos iniciales del sistema (FR-019): el catálogo fijo de roles y permisos, y el usuario
/// administrador inicial.
///
/// Es idempotente: si el usuario <c>admin</c> ya existe, no se toca ni se le pisa la contraseña.
/// No crea ninguna otra cuenta, ni de ejemplo ni de prueba: el resto de los usuarios se dan de alta
/// desde el Módulo 2.
/// </summary>
public class SembradorInicial(GtDbContext contexto, IHasheadorPassword hasheador)
{
    public const string UsernameAdministrador = "admin";
    public const string EmailAdministradorInicial = "admin@gtlogistica.local";
    public const string VariablePasswordInicial = "GT_ADMIN_PASSWORD_INICIAL";

    private static readonly (string Codigo, string Nombre)[] RolesDelSistema =
    [
        (CodigosRol.Trafico, "Tráfico"),
        (CodigosRol.Administracion, "Administración de la empresa"),
        (CodigosRol.Gerencia, "Gerencia"),
        (CodigosRol.AdministradorSistema, "Administrador del sistema"),
    ];

    private static readonly (string Codigo, string Modulo, string Descripcion)[] PermisosDelSistema =
    [
        (CodigosPermiso.UsuariosGestionar, "Usuarios",
            "Crear, consultar, modificar y dar de baja usuarios y sus roles"),

        (CodigosPermiso.ChoferesGestionar, "Choferes",
            "Gestionar transportistas, choferes y su documentación"),

        (CodigosPermiso.ChoferesVencimientosConsultar, "Choferes",
            "Consultar el panel de vencimientos de la documentación de choferes"),

        (CodigosPermiso.FlotaGestionar, "Flota",
            "Gestionar vehículos, su documentación y el panel de vencimientos"),

        (CodigosPermiso.FlotaVencimientosConsultar, "Flota",
            "Consultar el panel de vencimientos de la documentación de vehículos"),

        (CodigosPermiso.FlotaTiposGestionar, "Flota",
            "Mantener el catálogo de tipos de vehículo"),

        (CodigosPermiso.ViajesGestionar, "Viajes",
            "Registrar viajes y clientes, asignar chofer y vehículo, y cambiar el estado del viaje"),

        (CodigosPermiso.ViajesConsultar, "Viajes",
            "Consultar viajes, clientes y totales por período"),

        (CodigosPermiso.FacturacionGestionar, "Facturación",
            "Configurar la empresa emisora, emitir facturas, corregirlas y registrar el cobro"),

        (CodigosPermiso.FacturacionConsultar, "Facturación",
            "Consultar facturas, su documento, el panel de vencimientos y los totales facturados"),

        (CodigosPermiso.FacturacionAnular, "Facturación",
            "Anular una factura y devolver sus viajes a rendido"),

        (CodigosPermiso.LiquidacionesGestionar, "Liquidaciones",
            "Generar, editar y anular liquidaciones a transportistas, y registrar sus órdenes de pago"),

        (CodigosPermiso.LiquidacionesConsultar, "Liquidaciones",
            "Consultar liquidaciones a transportistas, sus viajes, órdenes de pago e historial"),

        (CodigosPermiso.AdelantosGestionar, "Adelantos",
            "Registrar adelantos de sueldo, aprobarlos, rechazarlos y anularlos"),

        (CodigosPermiso.AdelantosConsultar, "Adelantos",
            "Consultar adelantos de sueldo, su total adelantado e historial"),

        (CodigosPermiso.CajaGestionar, "Caja",
            "Abrir la propia caja, registrar sus ingresos y egresos, y cerrarla"),

        (CodigosPermiso.CajaConsultar, "Caja",
            "Consultar cajas, sus saldos y sus movimientos"),

        (CodigosPermiso.ReportesEmitir, "Reportes",
            "Emitir los reportes de viajes, vencimientos y movimientos de caja en PDF y Excel"),
    ];

    /// <summary>
    /// Qué permisos otorga cada rol. Cada módulo agrega el suyo cuando se construye.
    ///
    /// El Módulo 3 es el primero que habilita algo para un rol que no es el administrador: *Tráfico*
    /// recibe `choferes.gestionar` y ningún permiso del Módulo 2 (FR-027).
    ///
    /// El Módulo 4 es el primero que reparte **dos** permisos del mismo módulo de forma distinta:
    /// Tráfico gestiona la flota pero no el catálogo de tipos de vehículo, que es sólo del
    /// administrador (Módulo 4, FR-039, research §7).
    ///
    /// El Módulo 5 es el primero que le da algo a *Administración de la empresa* y a *Gerencia*:
    /// `viajes.consultar` lo reciben **los cuatro roles**, porque mirar el listado, la ficha y los
    /// totales no exige poder operar. `viajes.gestionar` sigue el reparto de siempre, Tráfico y
    /// administrador (Módulo 5, FR-051, research §10).
    ///
    /// El Módulo 6 es el primero cuyo permiso de **escritura no llega a Tráfico**: facturar es tarea
    /// administrativa. `facturacion.gestionar` va a *Administración de la empresa* y al administrador;
    /// `facturacion.consultar` suma además a *Gerencia*; y `facturacion.anular` queda **sólo** en el
    /// administrador, que es el tercer nivel de granularidad del sistema (Módulo 6, FR-066, FR-067,
    /// research §7).
    ///
    /// El Módulo 9 repite el reparto de dos niveles del 6, sin el tercero: `liquidaciones.gestionar` a
    /// *Administración de la empresa* y al administrador —anular incluido—, y `liquidaciones.consultar`
    /// a esos dos más *Gerencia*. Tráfico no recibe ninguno (Módulo 9, FR-064, research §9).
    ///
    /// El Módulo 10 repite el mismo reparto: `adelantos.gestionar` a *Administración de la empresa* y al
    /// administrador —aprobar incluido, sin control por oposición—, y `adelantos.consultar` a esos dos más
    /// *Gerencia*. Tráfico no recibe ninguno (Módulo 10, FR-042, research §7).
    ///
    /// El Módulo 11 repite el mismo reparto: `caja.gestionar` a *Administración de la empresa* y al
    /// administrador —abrir, registrar y cerrar—, y `caja.consultar` a esos dos más *Gerencia*, que revisa
    /// cajas y movimientos sin operar. Tráfico no recibe ninguno (Módulo 11, FR-031, FR-032, research §9).
    ///
    /// Los dos **paneles de vencimientos** —choferes y flota— se separan de sus permisos de gestión con
    /// `choferes.vencimientos.consultar` y `flota.vencimientos.consultar`, para que *Gerencia* vea qué
    /// documentación está por vencer sin quedarse con el padrón, la carga de documentos ni la descarga de
    /// escaneos, que es un dato personal sensible. Los reciben además *Tráfico* y el administrador, que ya
    /// llegaban a los dos paneles por `choferes.gestionar` y `flota.gestionar`: sembrarlos por separado es
    /// lo que hace que para ellos no cambie nada. *Administración de la empresa* sigue sin ninguno de los
    /// dos módulos.
    ///
    /// El Módulo 12 es el primero que **invierte** el reparto: `reportes.emitir` lo reciben *Gerencia* y
    /// el administrador, y **no** lo reciben Tráfico ni *Administración de la empresa*. Hasta acá Gerencia
    /// siempre tenía un subconjunto de lo de Administración; "sólo del gerente" acota a los roles
    /// operativos, y el administrador lo recibe como recibe todo lo demás (Módulo 12, FR-014).
    /// </summary>
    private static readonly Dictionary<string, string[]> PermisosPorRol = new()
    {
        [CodigosRol.AdministradorSistema] =
        [
            CodigosPermiso.UsuariosGestionar,
            CodigosPermiso.ChoferesGestionar,
            CodigosPermiso.ChoferesVencimientosConsultar,
            CodigosPermiso.FlotaGestionar,
            CodigosPermiso.FlotaVencimientosConsultar,
            CodigosPermiso.FlotaTiposGestionar,
            CodigosPermiso.ViajesGestionar,
            CodigosPermiso.ViajesConsultar,
            CodigosPermiso.FacturacionGestionar,
            CodigosPermiso.FacturacionConsultar,
            CodigosPermiso.FacturacionAnular,
            CodigosPermiso.LiquidacionesGestionar,
            CodigosPermiso.LiquidacionesConsultar,
            CodigosPermiso.AdelantosGestionar,
            CodigosPermiso.AdelantosConsultar,
            CodigosPermiso.CajaGestionar,
            CodigosPermiso.CajaConsultar,
            CodigosPermiso.ReportesEmitir,
        ],

        [CodigosRol.Trafico] =
        [
            CodigosPermiso.ChoferesGestionar,
            CodigosPermiso.ChoferesVencimientosConsultar,
            CodigosPermiso.FlotaGestionar,
            CodigosPermiso.FlotaVencimientosConsultar,
            CodigosPermiso.ViajesGestionar,
            CodigosPermiso.ViajesConsultar,
        ],

        [CodigosRol.Administracion] =
        [
            CodigosPermiso.ViajesConsultar,
            CodigosPermiso.FacturacionGestionar,
            CodigosPermiso.FacturacionConsultar,
            CodigosPermiso.LiquidacionesGestionar,
            CodigosPermiso.LiquidacionesConsultar,
            CodigosPermiso.AdelantosGestionar,
            CodigosPermiso.AdelantosConsultar,
            CodigosPermiso.CajaGestionar,
            CodigosPermiso.CajaConsultar,
        ],

        [CodigosRol.Gerencia] =
        [
            CodigosPermiso.ChoferesVencimientosConsultar,
            CodigosPermiso.FlotaVencimientosConsultar,
            CodigosPermiso.ViajesConsultar,
            CodigosPermiso.FacturacionConsultar,
            CodigosPermiso.LiquidacionesConsultar,
            CodigosPermiso.AdelantosConsultar,
            CodigosPermiso.CajaConsultar,
            CodigosPermiso.ReportesEmitir,
        ],
    };

    /// <param name="passwordInicial">
    /// Valor de <c>GT_ADMIN_PASSWORD_INICIAL</c>. Sólo hace falta cuando el usuario administrador
    /// todavía no existe; una vez sembrado, puede venir vacío.
    /// </param>
    public async Task SembrarAsync(string? passwordInicial, CancellationToken cancelacion = default)
    {
        await SembrarPermisosAsync(cancelacion);
        await SembrarRolesAsync(cancelacion);
        await SembrarAdministradorInicialAsync(passwordInicial, cancelacion);
    }

    private async Task SembrarPermisosAsync(CancellationToken cancelacion)
    {
        foreach (var (codigo, modulo, descripcion) in PermisosDelSistema)
        {
            if (await contexto.Permisos.AnyAsync(p => p.Codigo == codigo, cancelacion))
            {
                continue;
            }

            contexto.Permisos.Add(new Permiso
            {
                Codigo = codigo,
                Modulo = modulo,
                Descripcion = descripcion,
            });
        }

        await contexto.SaveChangesAsync(cancelacion);
    }

    private async Task SembrarRolesAsync(CancellationToken cancelacion)
    {
        var permisos = await contexto.Permisos.ToDictionaryAsync(p => p.Codigo, cancelacion);

        foreach (var (codigo, nombre) in RolesDelSistema)
        {
            var rol = await contexto.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Codigo == codigo, cancelacion);

            if (rol is null)
            {
                rol = new Rol { Codigo = codigo, Nombre = nombre };
                contexto.Roles.Add(rol);
            }

            var codigosEsperados = PermisosPorRol.GetValueOrDefault(codigo, []);

            foreach (var codigoPermiso in codigosEsperados)
            {
                var yaLoTiene = rol.Permisos.Any(p => p.Codigo == codigoPermiso);

                if (!yaLoTiene && permisos.TryGetValue(codigoPermiso, out var permiso))
                {
                    rol.Permisos.Add(permiso);
                }
            }
        }

        await contexto.SaveChangesAsync(cancelacion);
    }

    private async Task SembrarAdministradorInicialAsync(
        string? passwordInicial,
        CancellationToken cancelacion)
    {
        var normalizado = UsernameAdministrador.ToUpperInvariant();

        var yaExiste = await contexto.Usuarios
            .AnyAsync(u => u.UsernameNormalizado == normalizado, cancelacion);

        if (yaExiste)
        {
            // Idempotente: no se pisa la contraseña de un administrador ya creado, y la variable de
            // entorno deja de ser necesaria a partir de acá (research §6).
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordInicial))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {VariablePasswordInicial}, necesaria para crear el " +
                "usuario administrador inicial. Copiá .env.ejemplo a .env, definila y volvé a " +
                "levantar el sistema. Una vez creado el administrador, la variable deja de hacer " +
                "falta.");
        }

        var rolAdministrador = await contexto.Roles
            .FirstAsync(r => r.Codigo == CodigosRol.AdministradorSistema, cancelacion);

        var ahora = DateTime.UtcNow;

        var administrador = new Usuario
        {
            Username = UsernameAdministrador,
            UsernameNormalizado = normalizado,
            // Dirección de arranque, no real: el dominio `.local` no existe, así que no se le puede
            // mandar un correo por accidente. El responsable de sistemas la corrige desde el Módulo 2
            // apenas entra (research §5). Es el mismo valor con el que la migración rellena la fila
            // del `admin` que ya existía antes de este módulo.
            Email = EmailAdministradorInicial,
            EmailNormalizado = NormalizadorEmail.Normalizar(EmailAdministradorInicial),
            PasswordHash = hasheador.Hashear(passwordInicial),
            Estado = EstadoUsuario.Activo,
            FechaAlta = ahora,
            UltimoAcceso = null,
            PasswordTemporalGeneradaEn = null,
            PasswordActualizadaEn = ahora,
        };

        administrador.Roles.Add(rolAdministrador);
        contexto.Usuarios.Add(administrador);

        await contexto.SaveChangesAsync(cancelacion);
    }
}
