# Modelo de datos: Gestionar usuarios y roles (Módulo 2)

Fase 1 del plan. Parte del esquema que dejó el Módulo 1 (`Usuarios`, `Roles`, `Permisos`,
`UsuarioRoles`, `RolPermisos`) y le agrega lo que esta spec necesita: cuatro columnas en `Usuarios` y
la tabla `Personas`.

Todo entra en **una sola migración**. Las decisiones de fondo están en
[research.md](./research.md) §5 y §6.

---

## Panorama

```text
Persona ──0..1───1── Usuario ──*────*── Rol ──*────*── Permiso
   │                    │                │             │
 NUEVA            MODIFICADA        sin cambios    sin cambios
```

La relación `Usuario`↔`Persona` es opcional y de uno a uno: un usuario puede no tener persona, y una
persona no puede pertenecer a dos usuarios a la vez (FR-008).

---

## Usuario (tabla `Usuarios`) — MODIFICADA

Columnas que **ya existen** y este módulo no cambia:

| Columna | Tipo | Notas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `Username` | `nvarchar(50)`, obligatorio | Tal como se escribió, para mostrar |
| `UsernameNormalizado` | `nvarchar(50)`, obligatorio, **único** | Recortado y en mayúsculas. Es el campo por el que se busca y el que garantiza la unicidad (FR-002, FR-020) |
| `PasswordHash` | `nvarchar(200)`, obligatorio | Nunca sale hacia una respuesta ni hacia un log (FR-004) |
| `Estado` | `tinyint`, obligatorio | `1` activo, `2` inactivo, `3` bloqueado (FR-005) |
| `UltimoAcceso` | `datetime2`, nulo | Lo escribe el Módulo 1 al ingresar. Se muestra en el listado (FR-011) |
| `PasswordTemporalGeneradaEn` | `datetime2`, nulo | `null` = contraseña definitiva. Este módulo **la escribe** al restablecer y **la vuelve a `null`** cuando el usuario cambia su propia contraseña (FR-031); el Módulo 1 la lee para aplicar el vencimiento de 24 horas |

Columnas que **agrega** este módulo:

| Columna | Tipo | Reglas |
|---|---|---|
| `Email` | `nvarchar(254)`, obligatorio | Formato válido. Tal como se escribió, para mostrar (FR-003) |
| `EmailNormalizado` | `nvarchar(254)`, obligatorio, **único** | Recortado y en minúsculas. Es el campo que garantiza la unicidad y por el que filtra el listado (FR-003, FR-020) |
| `FechaAlta` | `datetime2`, obligatorio | Fecha y hora UTC de creación. Se fija una vez y no se edita (FR-011) |
| `PersonaId` | `int`, nulo, **único filtrado** | FK a `Personas`. `null` es válido y habitual: usuario sin persona asociada |
| `PasswordActualizadaEn` | `datetime2`, obligatorio | Momento UTC del último cambio de contraseña. Es lo que permite cortar las sesiones abiertas cuando la contraseña deja de ser válida (FR-032, research §10) |

### Índices

| Índice | Columnas | Tipo |
|---|---|---|
| `IX_Usuarios_UsernameNormalizado` | `UsernameNormalizado` | Único (ya existe) |
| `IX_Usuarios_EmailNormalizado` | `EmailNormalizado` | Único (nuevo) |
| `IX_Usuarios_PersonaId` | `PersonaId` | Único **con filtro** `WHERE PersonaId IS NOT NULL` (nuevo) |

> El filtro no es un detalle: sin él, SQL Server trata varios `NULL` como duplicados y sólo un
> usuario en todo el sistema podría quedarse sin persona asociada — un caso que la spec declara
> válido (research §5).

### Reglas de validación

| Regla | Requisito | Dónde se aplica |
|---|---|---|
| Al menos un rol asignado, al crear y al editar roles | FR-001, FR-018 | Caso de uso, antes de guardar |
| `username` único; en edición, la comparación excluye al propio usuario | FR-002, FR-015 | Validación previa + índice único |
| `email` único y con formato válido; en edición excluye al propio usuario | FR-003, FR-015 | Validación previa + índice único |
| Contraseña de 8 caracteres o más, sólo al crear | FR-004 | Caso de uso |
| Normalizar antes de comparar: `username` recortado y en mayúsculas, `email` recortado y en minúsculas | FR-020 | `NormalizadorUsername` (ya existe), `NormalizadorEmail` (nuevo) |
| La persona elegida no puede estar vinculada a otro usuario, sea cual sea el estado de ese usuario | FR-008 | Validación previa + índice único filtrado |
| La persona elegida tiene que existir y estar activa | FR-023 | Validación previa |
| Nunca puede quedar el sistema sin un usuario `activo` con el rol *Administrador del sistema* | FR-019 | `ProteccionUltimoAdministrador`, en las tres operaciones que pueden romperlo |
| Al cambiar la contraseña propia, la actual tiene que coincidir y la nueva llevar 8 caracteres o más | FR-030 | Caso de uso, con el `IVerificadorPassword` del Módulo 1 |
| Toda operación que cambie la contraseña actualiza `PasswordActualizadaEn` | FR-032 | Alta, restablecimiento y cambio propio |

### Transiciones de estado

```text
              ┌──────────── editar estado ────────────┐
              ▼                                       │
  (alta) → activo ⇄ inactivo                          │
              ⇅         ▲                             │
          bloqueado      └──── baja lógica (DELETE) ───┘
```

- El alta deja `activo` precargado (FR-005).
- La baja lógica **no borra**: lleva a `inactivo` y el registro sigue visible en el listado
  (FR-006).
- La reactivación es un cambio de estado a `activo` desde la edición, y sigue exigiendo al menos un
  rol asignado.
- Pasar a `inactivo` o `bloqueado` deja a esa cuenta sin poder autenticarse desde ese momento,
  incluso con sesión abierta (FR-016). No hace falta código nuevo: lo resuelve el `RevalidadorSesion`
  del Módulo 1 (research §7).

---

## Persona (tabla `Personas`) — NUEVA

Chofer o empleado de G&T Logística. El padrón **arranca vacío**: no se siembra por migración
(FR-024).

| Columna | Tipo | Reglas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `Nombre` | `nvarchar(100)`, obligatorio | |
| `Apellido` | `nvarchar(100)`, obligatorio | |
| `Dni` | `nvarchar(15)`, obligatorio, **único** | Sólo dígitos. Es el único dato con restricción de unicidad (FR-027) |
| `Tipo` | `tinyint`, obligatorio | `1` chofer, `2` empleado |
| `Telefono` | `nvarchar(30)`, obligatorio | |
| `Email` | `nvarchar(254)`, obligatorio | Formato válido. **Sin** restricción de unicidad (FR-027) |
| `FechaNacimiento` | `date`, obligatorio | |
| `Activa` | `bit`, obligatorio | `true` al crear. La baja lógica lo pone en `false` (research §6) |

### Índices

| Índice | Columnas | Tipo |
|---|---|---|
| `IX_Personas_Dni` | `Dni` | Único |

### Reglas de validación

| Regla | Requisito |
|---|---|
| Los siete datos de FR-026 son obligatorios, y no se piden otros | FR-026 |
| `dni` único en todo el padrón; en edición, la comparación excluye a la propia persona | FR-027 |
| No se puede dar de baja una persona vinculada a un usuario, sin importar el estado de ese usuario | FR-028 |
| Sólo las personas `Activa = true` se ofrecen para asociar a un usuario | FR-023 |

### Transiciones de estado

```text
  (alta) → Activa = true  ──── baja lógica ────►  Activa = false
                          ◄──── reactivación ────
```

La baja lógica sólo procede si la persona no está vinculada a ningún usuario (FR-028). El registro
nunca se borra (FR-022).

---

## Rol y Permiso — SIN CAMBIOS

El catálogo lo sembró el Módulo 1 y este módulo **no lo modifica**: sólo lo lee para asignar roles a
usuarios y para mostrar los permisos agrupados por módulo, en modo lectura (FR-010).

| Entidad | Uso en este módulo |
|---|---|
| `Rol` | Los cuatro roles fijos: `trafico`, `administracion`, `gerencia`, `administrador_sistema`. Se asignan y desasignan; no se crean ni se editan |
| `Permiso` | Se listan agrupados por su columna `Modulo`, en modo lectura |
| `UsuarioRoles` | Tabla de unión. Guardar los roles la deja **exactamente** como quedó la selección: se agregan los marcados y se quitan los desmarcados (FR-018) |

Sobre el catálogo de permisos: hoy sólo existe `usuarios.gestionar`, sembrado por el Módulo 1. Los
tres roles restantes van a mostrarse con su lista de permisos vacía hasta que los módulos que los
usan se implementen. Es correcto y coincide con lo que la spec asume; conviene que quien valide lo
sepa de antemano para no leerlo como un error.

---

## Migración

Un único archivo de migración de EF Core, en este orden:

1. Crear la tabla `Personas` con su índice único de `Dni`.
2. Agregar `Email`, `EmailNormalizado`, `FechaAlta`, `PersonaId` y `PasswordActualizadaEn` a
   `Usuarios`, **nullables por ahora**.
3. Rellenar la única fila preexistente —el usuario `admin` que sembró el Módulo 1— con
   `Email = 'admin@gtlogistica.local'`, su versión normalizada, y `FechaAlta` y
   `PasswordActualizadaEn` en `SYSUTCDATETIME()`.
4. Volver `Email`, `EmailNormalizado`, `FechaAlta` y `PasswordActualizadaEn` obligatorias.
5. Crear el índice único de `EmailNormalizado` y el índice único **filtrado** de `PersonaId`, más la
   clave foránea a `Personas`.

El paso 3 es indispensable: sin él, el paso 4 falla contra cualquier base que ya tenga el
administrador creado. El dominio `.local` no es una dirección real, así que no se le puede mandar un
correo por accidente; el responsable de sistemas la corrige desde la pantalla nueva (research §5).

**Sin datos iniciales.** Esta migración no siembra ninguna persona ni ninguna cuenta (FR-024). El
`SembradorInicial` del Módulo 1 tampoco se toca: la única cuenta que crea el sistema por su cuenta
sigue siendo `admin`.
