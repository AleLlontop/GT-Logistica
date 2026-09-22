# Sistema Integral de Gestión — G&T Logística

Aplicación web para que G&T Logística administre su flujo organizacional: viajes, facturación,
liquidaciones, adelantos de sueldo y flota.

## Requisitos previos

- **Docker y Docker Compose** — levantan SQL Server, backend y frontend. En Windows o Mac, Docker
  Desktop tiene que estar corriendo
- **Node.js 24** y npm — para trabajar el frontend fuera de los contenedores (el `package.json`
  exige `>=24 <25`)
- **.NET 10 SDK** — para correr los tests del backend fuera de los contenedores

## Guía de instalación y ejecución

### Usando Docker Compose (recomendado)

1. **Clonar el repositorio:**

   ```bash
   git clone <url-del-repositorio>
   cd gt-logistica
   ```

2. **Definir las dos contraseñas** (sólo la primera vez):

   ```bash
   cp .env.template .env    # y completá GT_SQL_PASSWORD y GT_ADMIN_PASSWORD_INICIAL
   ```

3. **Levantar los servicios:**

   ```bash
   docker compose up -d --build
   ```

¡Eso es todo! Docker instala las dependencias, crea la base y corre los tres servicios. La primera
vez, el backend aplica las migraciones, siembra el catálogo de roles y permisos y crea el usuario
`admin`.

- **Frontend:** http://localhost:5173
- **API (backend):** http://localhost:8080
- **SQL Server:** `localhost,1433`

Para detener los contenedores: `docker compose down`. Si corre sin `-d`, alcanza con `Ctrl+C`.

| Variable | Para qué sirve |
|---|---|
| `GT_ADMIN_PASSWORD_INICIAL` | Contraseña del administrador inicial. Obligatoria **sólo mientras el usuario `admin` no exista**: una vez creado podés borrarla, porque la contraseña ya vive hasheada en la base |
| `GT_SQL_PASSWORD` | Contraseña de `sa` en el SQL Server de desarrollo |

Los comandos del día a día:

| Para qué | Comando |
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

Si falta `GT_ADMIN_PASSWORD_INICIAL` justo cuando hacía falta crear el administrador, el backend se
detiene con un mensaje explicando qué falta.

## Estado de los módulos

| Módulo | Estado | Qué hay hoy |
|---|---|---|
| [1. Autenticación de usuarios](specs/001-autenticacion-usuarios/) | Implementado | Ingreso con cookie de sesión, permisos revalidados por petición, límite de intentos fallidos y menú calculado en el servidor |
| [2. Gestión de usuarios y roles](specs/002-gestion-usuarios-roles/) | Implementado | ABM de usuarios, asignación de roles, restablecimiento de contraseña y padrón de personas |
| [3. Gestión de choferes y su documentación](specs/003-gestion-choferes/) | Implementado | CRUDS de transportistas, choferes, documentación , listado con filtros y paginación, panel de vencimientos, catálogo de tipos y bajas.|
| [4. Gestión de flota](specs/004-gestion-flota/) | Implementado | Padrón de vehículos con su transportista dueño, documentación con estado calculado, estado operativo derivado, listado con filtros y paginación, panel de vencimientos, catálogo de tipos de vehículo, bajas y reactivaciones |
| [5. Gestión de viajes](specs/005-gestion-viajes/) | Implementado | Padrón de clientes, alta de viajes con chofer y vehículo validados contra su documentación **a la fecha del viaje**, ciclo pendiente → en curso → rendido / anulado con historial de quién y cuándo, unidad ocupada mientras el viaje está en curso, listado con filtros y paginación, y totales de cantidad e importe por cliente y por transportista en un período |
| [6. Gestión de facturación](specs/006-gestion-facturacion/) | Implementado | Configuración de la empresa emisora con su logo, emisión agrupando viajes rendidos de un cliente y período con neto / IVA / total calculados, vista previa y documento PDF generado por el sistema, CAE y su vencimiento, estados pendiente / vencida / pagada / anulada con registro del cobro, anulación con motivo que devuelve los viajes a rendido, refacturación, listado con filtros, panel de vencimientos y totales facturado / cobrado / pendiente |
| [9. Liquidación a transportistas](specs/009-gestion-liquidacion/) | Implementado | Liquidación mensual a fleteros agrupando los viajes rendidos del período, un viaje en una sola liquidación, detalle de los viajes que la componen y las órdenes de pago que la cancelan, estados pendiente / pagada / anulada y listado con filtros combinables |
| [10. Gestión de adelantos de sueldo](specs/010-gestion-adelantos/) | Implementado | Registro de adelantos a choferes propios y empleados, circuito pendiente → aprobado / rechazado con motivo y anulación confirmada, listado con filtros por persona, rango de fechas y estado, total adelantado de la selección e historial de quién hizo qué y cuándo |
| [11. Gestión de caja](specs/011-gestion-caja/) | Implementado | Apertura de caja con saldo inicial y responsable, una sola caja abierta por empleado, registro de ingresos y egresos con concepto y referencia opcional a una factura o a una orden de pago, resumen previo al cierre con el saldo final calculado y confirmado contra el número que se vio, cierre que deja la caja sin admitir movimientos, y consulta por caja o por rango de días  |
| [12. Emitir reportes](specs/012-emitir-reportes/) | Implementado | Acción *Generar reporte* en PDF y Excel sobre cinco listados ya existentes —viajes, los tres paneles de vencimientos y los movimientos de caja—, con las filas que los filtros dejan y no sólo la página a la vista, encabezado con la línea de filtros y el instante de generación, totales al pie, importes y fechas que la planilla puede sumar, y permiso propio de emisión exigido junto al de consulta del módulo |


> Los identificadores de tarea (`T059`, `T123`, …) **se numeran desde uno en cada módulo**, así que
> el mismo ID significa cosas distintas en cada `tasks.md`. Cuando haga falta nombrar uno, va con su
> carpeta: `[001] T059`.

El estado completo está en [specs/README.md](specs/README.md), y el detalle tarea por tarea en el
`tasks.md` de cada carpeta de `specs/`.

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
│   ├── facturacion/        Facturas, empresa emisora y totales facturado/cobrado
│   ├── liquidaciones/      Liquidaciones a transportistas y órdenes de pago
│   ├── adelantos/          Adelantos de sueldo a choferes y empleados
│   ├── caja/               Apertura, movimientos y cierre de caja
│   └── reportes/           Acción compartida Generar reporte
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
specify init --here --integration <claude | copilot | cursor | gemini | opencode | …>
```

Eso te crea tus comandos (`.claude/`, `.github/prompts/`, `.cursor/commands/`, `.opencode/`, según
cuál elijas) sin tocar los de nadie. En Linux o Mac agregá `--script sh`: hoy sólo están generados
los scripts de PowerShell.

Las instrucciones del proyecto para asistentes están en [AGENTS.md](AGENTS.md), que sí se versiona.
Si tu herramienta busca otro nombre, creá el archivo que espera con una sola línea que lo importe
—por ejemplo un `CLAUDE.md` con `@AGENTS.md`— y quedará ignorado por git.

Una decision del Módulo 1 que conviene conocer antes de tocar el código:

- **La sesión es una cookie, no un token.** Los permisos se recalculan contra la base en cada
  petición, así que quitarle un rol a alguien con la sesión abierta surte efecto en su operación
  siguiente. Con un token autocontenido eso exigiría una lista de revocación. El razonamiento
  completo está en `specs/001-autenticacion-usuarios/research.md` §1.

