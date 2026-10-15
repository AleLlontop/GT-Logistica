using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Flota;
using GT.Domain.Liquidaciones;
using GT.Domain.Personas;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
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

    /// <summary>
    /// Siembra datos de demostración si la base no contiene clientes cargados aún.
    /// Incluye clientes, choferes, flota, documentación (con alertas próximas a vencer),
    /// viajes en distintos estados y una liquidación de prueba.
    /// </summary>
    public async Task SembrarDatosDeDemostracionAsync(CancellationToken cancelacion = default)
    {
        if (await contexto.Clientes.AnyAsync(cancelacion))
        {
            return;
        }

        var ahora = DateTime.UtcNow;
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // 1. Empresa Emisora
        if (!await contexto.EmpresaEmisora.AnyAsync(cancelacion))
        {
            contexto.EmpresaEmisora.Add(new EmpresaEmisora
            {
                Id = EmpresaEmisora.IdUnico,
                RazonSocial = "G&T Logística S.A.",
                Cuit = "30712345671",
                Domicilio = "Av. Pellegrini 1234, Rosario, Santa Fe",
                CondicionIva = "IVA Responsable Inscripto",
                IngresosBrutos = "901-123456-7",
                InicioActividades = new DateOnly(2020, 1, 1),
                PuntoDeVenta = "0001",
                Cbu = "0170099920000012345678",
                Telefono = "0341-444-4444",
                Email = "administracion@gtlogistica.com.ar",
            });
            await contexto.SaveChangesAsync(cancelacion);
        }

        // 2. Tipos de Vehículo
        var semi = await contexto.TiposVehiculo.FirstOrDefaultAsync(t => t.Nombre == "Semirremolque", cancelacion);
        if (semi is null)
        {
            semi = new TipoVehiculo { Nombre = "Semirremolque", Activo = true };
            contexto.TiposVehiculo.Add(semi);
        }

        var chasis = await contexto.TiposVehiculo.FirstOrDefaultAsync(t => t.Nombre == "Camión Chasis", cancelacion);
        if (chasis is null)
        {
            chasis = new TipoVehiculo { Nombre = "Camión Chasis", Activo = true };
            contexto.TiposVehiculo.Add(chasis);
        }
        await contexto.SaveChangesAsync(cancelacion);

        // 3. Tipos de Documentación
        var lnc = await contexto.DocumentacionTipos.FirstOrDefaultAsync(t => t.Nombre == "Licencia Nacional de Conducir", cancelacion);
        if (lnc is null)
        {
            lnc = new DocumentacionTipo
            {
                Nombre = "Licencia Nacional de Conducir",
                DiasAvisoVencimiento = 30,
                Ambito = DocumentacionAmbito.Chofer,
                Activo = true,
            };
            contexto.DocumentacionTipos.Add(lnc);
        }

        var seguro = await contexto.DocumentacionTipos.FirstOrDefaultAsync(t => t.Nombre == "Póliza de Seguro", cancelacion);
        if (seguro is null)
        {
            seguro = new DocumentacionTipo
            {
                Nombre = "Póliza de Seguro",
                DiasAvisoVencimiento = 15,
                Ambito = DocumentacionAmbito.Vehiculo,
                Activo = true,
            };
            contexto.DocumentacionTipos.Add(seguro);
        }

        var rto = await contexto.DocumentacionTipos.FirstOrDefaultAsync(t => t.Nombre == "Revisión Técnica Obligatoria", cancelacion);
        if (rto is null)
        {
            rto = new DocumentacionTipo
            {
                Nombre = "Revisión Técnica Obligatoria",
                DiasAvisoVencimiento = 30,
                Ambito = DocumentacionAmbito.Vehiculo,
                Activo = true,
            };
            contexto.DocumentacionTipos.Add(rto);
        }
        await contexto.SaveChangesAsync(cancelacion);

        // 4. Transportistas
        var t1Cuit = GenerarCuitValido("3070987654");
        var t1 = await contexto.Transportistas.FirstOrDefaultAsync(t => t.Cuit == t1Cuit, cancelacion);
        if (t1 is null)
        {
            t1 = new Transportista
            {
                Nombre = "Logística del Centro S.R.L.",
                Cuit = t1Cuit,
                Tipo = TipoPersona.Juridica,
                Telefono = "0341-4567890",
                Email = "contacto@logisticadelcentro.com.ar",
                Activo = true,
            };
            contexto.Transportistas.Add(t1);
        }

        var t2Cuit = GenerarCuitValido("3071122334");
        var t2 = await contexto.Transportistas.FirstOrDefaultAsync(t => t.Cuit == t2Cuit, cancelacion);
        if (t2 is null)
        {
            t2 = new Transportista
            {
                Nombre = "Transportes Andinos S.A.",
                Cuit = t2Cuit,
                Tipo = TipoPersona.Juridica,
                Telefono = "0351-4890123",
                Email = "operaciones@andinos.com.ar",
                Activo = true,
            };
            contexto.Transportistas.Add(t2);
        }
        await contexto.SaveChangesAsync(cancelacion);

        // 5. Personas y Choferes
        var p1 = new Persona
        {
            Nombre = "Juan Carlos",
            Apellido = "Gómez",
            Dni = "28123456",
            Tipo = TipoIntegrante.Chofer,
            Telefono = "3415550101",
            Email = "jgomez@gtlogistica.local",
            FechaNacimiento = new DateOnly(1980, 5, 12),
            Activa = true,
        };
        var p2 = new Persona
        {
            Nombre = "Roberto",
            Apellido = "Fernández",
            Dni = "30987654",
            Tipo = TipoIntegrante.Chofer,
            Telefono = "3415550102",
            Email = "rfernandez@gtlogistica.local",
            FechaNacimiento = new DateOnly(1984, 8, 22),
            Activa = true,
        };
        var p3 = new Persona
        {
            Nombre = "Esteban",
            Apellido = "Martínez",
            Dni = "32456789",
            Tipo = TipoIntegrante.Chofer,
            Telefono = "3415550103",
            Email = "emartinez@gtlogistica.local",
            FechaNacimiento = new DateOnly(1986, 11, 3),
            Activa = true,
        };
        contexto.Personas.AddRange(p1, p2, p3);
        await contexto.SaveChangesAsync(cancelacion);

        var c1 = new Chofer
        {
            PersonaId = p1.Id,
            Cuil = GenerarCuitValido("2028123456"),
            TransportistaId = t1.Id,
            Activo = true,
        };
        var c2 = new Chofer
        {
            PersonaId = p2.Id,
            Cuil = GenerarCuitValido("2030987654"),
            TransportistaId = t1.Id,
            Activo = true,
        };
        var c3 = new Chofer
        {
            PersonaId = p3.Id,
            Cuil = GenerarCuitValido("2032456789"),
            TransportistaId = t2.Id,
            Activo = true,
        };
        contexto.Choferes.AddRange(c1, c2, c3);
        await contexto.SaveChangesAsync(cancelacion);

        // Documentación Choferes (c2 con alerta próxima a vencer: 5 días)
        contexto.Documentaciones.AddRange(
            new Documentacion
            {
                ChoferId = c1.Id,
                DocumentacionTipoId = lnc.Id,
                Numero = "LNC-28123456",
                FechaEmision = hoy.AddDays(-180),
                FechaVencimiento = hoy.AddDays(180),
            },
            new Documentacion
            {
                ChoferId = c2.Id,
                DocumentacionTipoId = lnc.Id,
                Numero = "LNC-30987654",
                FechaEmision = hoy.AddDays(-360),
                FechaVencimiento = hoy.AddDays(5),
            },
            new Documentacion
            {
                ChoferId = c3.Id,
                DocumentacionTipoId = lnc.Id,
                Numero = "LNC-32456789",
                FechaEmision = hoy.AddDays(-270),
                FechaVencimiento = hoy.AddDays(90),
            });
        await contexto.SaveChangesAsync(cancelacion);

        // 6. Vehículos
        var v1 = new Vehiculo
        {
            Patente = "AF123CD",
            Marca = "Scania",
            Modelo = "R450",
            TipoVehiculoId = semi.Id,
            TransportistaId = t1.Id,
            EstadoOperativo = VehiculoEstado.Disponible,
            Activo = true,
        };
        var v2 = new Vehiculo
        {
            Patente = "AG456EF",
            Marca = "Mercedes-Benz",
            Modelo = "Actros 2045",
            TipoVehiculoId = semi.Id,
            TransportistaId = t1.Id,
            EstadoOperativo = VehiculoEstado.Disponible,
            Activo = true,
        };
        var v3 = new Vehiculo
        {
            Patente = "AC789GH",
            Marca = "Iveco",
            Modelo = "Stralis 460",
            TipoVehiculoId = chasis.Id,
            TransportistaId = t2.Id,
            EstadoOperativo = VehiculoEstado.Disponible,
            Activo = true,
        };
        contexto.Vehiculos.AddRange(v1, v2, v3);
        await contexto.SaveChangesAsync(cancelacion);

        // Documentación Vehículos (v2 con póliza próxima a vencer: 7 días)
        contexto.DocumentacionesVehiculo.AddRange(
            new DocumentacionVehiculo
            {
                VehiculoId = v1.Id,
                DocumentacionTipoId = seguro.Id,
                Numero = "POL-99881",
                FechaEmision = hoy.AddDays(-240),
                FechaVencimiento = hoy.AddDays(120),
            },
            new DocumentacionVehiculo
            {
                VehiculoId = v1.Id,
                DocumentacionTipoId = rto.Id,
                Numero = "RTO-44551",
                FechaEmision = hoy.AddDays(-240),
                FechaVencimiento = hoy.AddDays(120),
            },
            new DocumentacionVehiculo
            {
                VehiculoId = v2.Id,
                DocumentacionTipoId = seguro.Id,
                Numero = "POL-99882",
                FechaEmision = hoy.AddDays(-350),
                FechaVencimiento = hoy.AddDays(7),
            },
            new DocumentacionVehiculo
            {
                VehiculoId = v2.Id,
                DocumentacionTipoId = rto.Id,
                Numero = "RTO-44552",
                FechaEmision = hoy.AddDays(-300),
                FechaVencimiento = hoy.AddDays(65),
            },
            new DocumentacionVehiculo
            {
                VehiculoId = v3.Id,
                DocumentacionTipoId = seguro.Id,
                Numero = "POL-99883",
                FechaEmision = hoy.AddDays(-160),
                FechaVencimiento = hoy.AddDays(200),
            },
            new DocumentacionVehiculo
            {
                VehiculoId = v3.Id,
                DocumentacionTipoId = rto.Id,
                Numero = "RTO-44553",
                FechaEmision = hoy.AddDays(-180),
                FechaVencimiento = hoy.AddDays(180),
            });
        await contexto.SaveChangesAsync(cancelacion);

        // 7. Clientes
        var cli1 = new Cliente
        {
            RazonSocial = "Distribuidora del Litoral S.A.",
            Cuit = GenerarCuitValido("3065432109"),
            Telefono = "0341-4221100",
            Email = "administracion@distribuidoralitoral.com.ar",
            Activo = true,
        };
        var cli2 = new Cliente
        {
            RazonSocial = "Cereales del Sur S.R.L.",
            Cuit = GenerarCuitValido("3061234567"),
            Telefono = "0341-4332211",
            Email = "pagos@cerealesdelsur.com.ar",
            Activo = true,
        };
        var cli3 = new Cliente
        {
            RazonSocial = "Industrias Metalúrgicas Pampeanas",
            Cuit = GenerarCuitValido("3059876543"),
            Telefono = "02302-445566",
            Email = "contacto@metalurgicapampeana.com.ar",
            Activo = true,
        };
        contexto.Clientes.AddRange(cli1, cli2, cli3);
        await contexto.SaveChangesAsync(cancelacion);

        // 8. Viajes
        var viaje1 = new Viaje
        {
            ClienteId = cli1.Id,
            Fecha = hoy,
            Origen = "Rosario, Santa Fe",
            Destino = "Córdoba Capital",
            Estado = EstadoViaje.Pendiente,
            Importe = 450000m,
            DetalleCarga = "Pallets de alimentos secos",
        };
        var viaje2 = new Viaje
        {
            ClienteId = cli2.Id,
            Fecha = hoy,
            Origen = "San Lorenzo, Santa Fe",
            Destino = "Puerto de Buenos Aires",
            Estado = EstadoViaje.Pendiente,
            ChoferId = c1.Id,
            VehiculoId = v1.Id,
            TransportistaId = t1.Id,
            Importe = 380000m,
            NumeroRemito = "R-0001-00001001",
            DetalleCarga = "Carga general y granos",
        };
        var viaje3 = new Viaje
        {
            ClienteId = cli3.Id,
            Fecha = hoy.AddDays(-1),
            Origen = "General Pico, La Pampa",
            Destino = "Rosario, Santa Fe",
            Estado = EstadoViaje.EnCurso,
            ChoferId = c3.Id,
            VehiculoId = v3.Id,
            TransportistaId = t2.Id,
            Importe = 520000m,
            NumeroRemito = "R-0001-00001002",
            DetalleCarga = "Estructuras metálicas y perfiles",
        };
        var viaje4 = new Viaje
        {
            ClienteId = cli1.Id,
            Fecha = hoy.AddDays(-4),
            Origen = "Rosario, Santa Fe",
            Destino = "Mendoza Capital",
            Estado = EstadoViaje.Rendido,
            ChoferId = c1.Id,
            VehiculoId = v1.Id,
            TransportistaId = t1.Id,
            Importe = 600000m,
            NumeroRemito = "R-0001-00001003",
            DetalleCarga = "Bebidas y lácteos",
        };
        contexto.Viajes.AddRange(viaje1, viaje2, viaje3, viaje4);
        await contexto.SaveChangesAsync(cancelacion);

        var admin = await contexto.Usuarios.FirstOrDefaultAsync(
            u => u.UsernameNormalizado == UsernameAdministrador.ToUpperInvariant(),
            cancelacion);

        if (admin is not null)
        {
            contexto.CambiosDeEstadoViaje.AddRange(
                new CambioDeEstadoViaje
                {
                    ViajeId = viaje3.Id,
                    EstadoAnterior = EstadoViaje.Pendiente,
                    EstadoNuevo = EstadoViaje.EnCurso,
                    UsuarioId = admin.Id,
                    OcurridoEn = ahora.AddDays(-1),
                },
                new CambioDeEstadoViaje
                {
                    ViajeId = viaje4.Id,
                    EstadoAnterior = EstadoViaje.Pendiente,
                    EstadoNuevo = EstadoViaje.EnCurso,
                    UsuarioId = admin.Id,
                    OcurridoEn = ahora.AddDays(-4),
                },
                new CambioDeEstadoViaje
                {
                    ViajeId = viaje4.Id,
                    EstadoAnterior = EstadoViaje.EnCurso,
                    EstadoNuevo = EstadoViaje.Rendido,
                    UsuarioId = admin.Id,
                    OcurridoEn = ahora.AddDays(-3),
                });
            await contexto.SaveChangesAsync(cancelacion);

            // 9. Liquidación para viaje4
            var liquidacion = new Liquidacion
            {
                TransportistaId = t1.Id,
                PeriodoMes = (byte)hoy.Month,
                PeriodoAnio = (short)hoy.Year,
                ImporteTotal = viaje4.Importe,
                ImportePagado = 0m,
                Estado = EstadoLiquidacion.Pendiente,
            };
            contexto.Liquidaciones.Add(liquidacion);
            await contexto.SaveChangesAsync(cancelacion);

            contexto.LiquidacionViajes.Add(new LiquidacionViaje
            {
                LiquidacionId = liquidacion.Id,
                ViajeId = viaje4.Id,
                Vigente = true,
            });

            contexto.CambiosDeLiquidacion.Add(new CambioDeLiquidacion
            {
                LiquidacionId = liquidacion.Id,
                Operacion = OperacionDeLiquidacion.Generacion,
                UsuarioId = admin.Id,
                OcurridoEn = ahora.AddDays(-2),
            });
            await contexto.SaveChangesAsync(cancelacion);
        }
    }

    private static string GenerarCuitValido(string prefijoYNumero)
    {
        var multiplicadores = new[] { 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };
        var suma = 0;
        for (var i = 0; i < 10; i++)
        {
            suma += (prefijoYNumero[i] - '0') * multiplicadores[i];
        }

        var resto = suma % 11;
        var digito = resto == 0 ? 0 : resto == 1 ? 9 : 11 - resto;

        return prefijoYNumero + digito;
    }
}
