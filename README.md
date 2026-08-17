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
| [5. Gestión de viajes](specs/005-gestion-viajes/) | Implementado | Padrón de clientes, alta de viajes con chofer y vehículo validados contra su documentación **a la fecha del viaje**, ciclo pendiente → en curso → rendido / anulado con historial de quién y cuándo, unidad ocupada mientras el viaje está en curso, listado con filtros y paginación, y totales de cantidad e importe por cliente y por transportista en un período |
| [6. Gestión de facturación](specs/006-gestion-facturacion/) | Implementado | Configuración de la empresa emisora con su logo, emisión agrupando viajes rendidos de un cliente y período con neto / IVA / total calculados, vista previa y documento PDF generado por el sistema, CAE y su vencimiento, estados pendiente / vencida / pagada / anulada con registro del cobro, anulación con motivo que devuelve los viajes a rendido, refacturación, listado con filtros, panel de vencimientos y totales facturado / cobrado / pendiente |


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

### Si ya tenés Docker instalado

No hace falta instalar Podman: el `docker-compose.yml` es uno solo y usa sólo sintaxis estándar de
Compose, así que corre igual en los dos. Cambiá `podman` por `docker` y listo.

```bash
cp .env.template .env    # y completá las dos variables
docker compose up -d     # SQL Server + backend + frontend
```

Con Docker Compose V1 —el binario viejo, separado— el comando es `docker-compose up -d`. En Windows
o Mac, Docker Desktop tiene que estar corriendo antes.

Los comandos del día a día son los mismos con el prefijo cambiado:

| Para qué | Con Docker |
|---|---|
| Levantar | `docker compose up -d` |
| Ver los logs del backend | `docker compose logs -f backend` |
| Reconstruir tras cambiar código | `docker compose up -d --build` |
| Bajar todo | `docker compose down` |
| Bajar y borrar la base y los archivos subidos | `docker compose down -v` |

`down -v` borra los volúmenes `datos-sqlserver` y `archivos-documentacion`: se pierden la base de
desarrollo y los escaneos de documentación cargados, y la próxima vez el backend vuelve a crear el
usuario `admin`, así que `GT_ADMIN_PASSWORD_INICIAL` tiene que estar definida otra vez.

Los puertos que quedan tomados son el `5173` (frontend), el `8080` (backend) y el `1433` (SQL
Server). Si alguno está ocupado —por ejemplo un SQL Server instalado en la máquina—, el `up` falla
al publicarlo.

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
│   ├── flota/              Vehículos, tipos de vehículo y su documentación
│   ├── viajes/             Viajes, clientes y totales por cliente y transportista
│   └── facturacion/        Facturas, empresa emisora y totales facturado/cobrado
└── compartido/             Layout, menú y cliente HTTP

specs/                      Una carpeta por módulo: spec, plan y tareas
.specify/memory/            Constitución del proyecto
```

## Cómo se toman las decisiones

Las reglas de producto viven en `.specify/memory/constitution.md` y el estado de cada módulo en
`specs/`. Cada módulo pasa por spec → clarificación → plan → tareas → implementación.

### Enganchar tu asistente de IA

El proceso es [Spec Kit](https://github.com/github/spec-kit), y **cada uno lo usa con la IA que
prefiera**. Lo compartido está versionado —la constitución, las plantillas, los scripts, la
extensión de git y todas las `specs/`—; lo que genera Spec Kit para una IA en particular, no. Así
que después de clonar, una vez:

```bash
specify init --here --ai <claude | copilot | cursor | gemini | …>
```

Eso te crea tus comandos (`.claude/`, `.github/prompts/`, `.cursor/commands/`, según cuál elijas) sin
tocar los de nadie. En Linux o Mac agregá `--script sh`: hoy sólo están generados los scripts de
PowerShell.

Las instrucciones del proyecto para asistentes están en [AGENTS.md](AGENTS.md), que sí se versiona.
Si tu herramienta busca otro nombre, creá el archivo que espera con una sola línea que lo importe
—por ejemplo un `CLAUDE.md` con `@AGENTS.md`— y quedará ignorado por git.

Dos decisiones del Módulo 1 que conviene conocer antes de tocar el código:

- **La sesión es una cookie, no un token.** Los permisos se recalculan contra la base en cada
  petición, así que quitarle un rol a alguien con la sesión abierta surte efecto en su operación
  siguiente. Con un token autocontenido eso exigiría una lista de revocación. El razonamiento
  completo está en `specs/001-autenticacion-usuarios/research.md` §1.

