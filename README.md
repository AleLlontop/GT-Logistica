# Sistema Integral de Gestión — G&T Logística

Aplicación web para que G&T Logística administre su flujo organizacional: viajes, facturación,
liquidaciones y flota.

## Estado de los módulos

| Módulo | Estado | Qué hay hoy |
|---|---|---|
| [1. Autenticación de usuarios](specs/001-autenticacion-usuarios/) | Implementado | Ingreso con cookie de sesión, permisos revalidados por petición, límite de intentos fallidos y menú calculado en el servidor |
| [2. Gestión de usuarios y roles](specs/002-gestion-usuarios-roles/) | Implementado | ABM de usuarios, asignación de roles, restablecimiento de contraseña y padrón de personas |
| [3. Gestión de choferes y su documentación](specs/003-gestion-choferes/) | Implementado | CRUDS de transportistas, choferes, documentación , listado con filtros y paginación, panel de vencimientos, catálogo de tipos y bajas.|
| [4. Gestión de flota](specs/004-gestion-flota/) | Implementado | Padrón de vehículos con su transportista dueño, documentación con estado calculado, estado operativo derivado, listado con filtros y paginación, panel de vencimientos, catálogo de tipos de vehículo, bajas y reactivaciones |


> Los identificadores de tarea (`T059`, `T123`, …) **se numeran desde uno en cada módulo**, así que
> el mismo ID significa cosas distintas en cada `tasks.md`. Cuando haga falta nombrar uno, va con su
> carpeta: `[001] T059`.

El estado completo está en [specs/README.md](specs/README.md), y el detalle tarea por tarea en el
`tasks.md` de cada carpeta de `specs/`.

## Levantar el sistema

La primera vez hay que definir dos contraseñas.

```bash
cp .env.template .env    # y completá las dos variables
podman compose up -d     # SQL Server + backend + frontend
```

La aplicación queda en `http://localhost:5173`. Al arrancar, el backend aplica las migraciones y
crea el catálogo de roles y permisos junto con el usuario `admin`.

| Variable | Para qué sirve |
|---|---|
| `GT_ADMIN_PASSWORD_INICIAL` | Contraseña del administrador inicial. Obligatoria **sólo mientras el usuario `admin` no exista**: una vez creado podés borrarla, porque la contraseña ya vive hasheada en la base |
| `GT_SQL_PASSWORD` | Contraseña de `sa` en el SQL Server de desarrollo |

Si falta `GT_ADMIN_PASSWORD_INICIAL` justo cuando hacía falta crear el administrador, el backend se
detiene con un mensaje explicando qué falta. 

De ahí en más alcanza con `podman compose up -d`.

## Probar

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # tests de frontend
```

Los tests de integración necesitan el SQL Server del compose corriendo. Crean su propia base por
corrida (`GtLogistica_Test_<guid>`) y la borran al terminar, así que no tocan la base de desarrollo.
Leen `GT_SQL_PASSWORD` del `.env`.

## Estructura

```
backend/
├── src/
│   ├── GT.Api/             Endpoints, autenticación por cookie, autorización por permiso
│   ├── GT.Application/     Casos de uso y mensajes 
│   ├── GT.Domain/          Entidades y reglas puras
│   └── GT.Infrastructure/  EF Core, hasheo, datos iniciales
└── tests/

frontend/src/
├── modules/                Un directorio por módulo de negocio, no por tipo de archivo
│   ├── autenticacion/      Ingreso y sesión
│   ├── usuarios/           Usuarios, roles y padrón de personas
│   ├── choferes/           Choferes, transportistas y documentación
│   └── flota/              Vehículos, tipos de vehículo y su documentación
└── compartido/             Layout, menú y cliente HTTP

specs/                      Una carpeta por módulo: spec, plan y tareas
.specify/memory/            Constitución del proyecto
```

## Cómo se toman las decisiones

Las reglas de producto viven en `.specify/memory/constitution.md` y el estado de cada módulo en
`specs/`. Cada módulo pasa por spec → clarificación → plan → tareas → implementación.

Dos decisiones del Módulo 1 que conviene conocer antes de tocar el código:

- **La sesión es una cookie, no un token.** Los permisos se recalculan contra la base en cada
  petición, así que quitarle un rol a alguien con la sesión abierta surte efecto en su operación
  siguiente. Con un token autocontenido eso exigiría una lista de revocación. El razonamiento
  completo está en `specs/001-autenticacion-usuarios/research.md` §1.

