# Base demo integral DEMO015 (sólo desarrollo)

Herramienta explícita; no modifica el modelo ni SembradorInicial. Versión lógica1,
Random(15015), fechas de negocio agosto–noviembre2026. Los IDs/secuencias, hashes de
contraseña, claves físicas de archivos y tiempos internos no son una identidad binaria
reproducible: lo reproducible es el conjunto y sus relaciones/escenarios de negocio.

## Entorno demo

Requiere `.env` externo según el README principal, incluido el password inicial de admin.
Podés definir `GT_DEMO_PASSWORD` externo; si falta, Compose usa el password inicial externo
para las cuentas DEMO. Nunca se imprime el password ni los hashes. Cuentas locales:
`demo.DEMO015.v1.ready` (administración), `demo.DEMO015.gerencia`,
`demo.DEMO015.operaciones`, `demo.DEMO015.caja`. No usarlas fuera de desarrollo.

```bash
docker compose up -d --build
docker compose ps
docker compose run --rm seed-demo --seed --prepare --confirm
docker compose run --rm seed-demo --summary --confirm
docker compose run --rm seed-demo --reset-demo --prepare --confirm
```

Abrí http://localhost:5173/reportes y aplicá **01/10/2026–31/10/2026**.
Para ver evolución de cuatro meses, ampliá a **01/08/2026–30/11/2026**.

SQL healthy → seeder one-shot (migraciones, semillas estructurales, Snapshot, DEMO) →
backend → frontend. La API no ejecuta el seeder. `GT_SEED_DEMO=false` lo desactiva;
Production también queda inactivo sin abrir conexión. Generar exige ambas condiciones:
`ASPNETCORE_ENVIRONMENT=Development` y `GT_SEED_DEMO=true`, más `--confirm`.
Sólo se acepta Database=GtLogistica y localhost/127.0.0.1/::1/sqlserver. No destinos remotos.
El usuario SQL de preparación debe poder migrar y habilitar Snapshot; no se otorga ese
privilegio al runtime desde el tooling. No se cambia RCSI.

Este Compose es de desarrollo: sus valores por defecto son `Development` y
`GT_SEED_DEMO=true`. Para un despliegue de producción hay que configurar
`ASPNETCORE_ENVIRONMENT=Production`; no se infiere el entorno por el servidor.
La versión lógica está definida en `Dataset.Version = 1` y la marca en
`Dataset.Mark = "DEMO015"`; no existe una variable de entorno `DEMO015_VERSION`.

Con configuración Docker el wrapper usa el contenedor y el volumen de adjuntos de la API:

```bash
./backend/scripts/seed-demo-completo.sh --seed --prepare --confirm
./backend/scripts/seed-demo-completo.sh --summary --confirm
./backend/scripts/seed-demo-completo.sh --reset-demo --prepare --confirm
```

En modo Docker las dos guardias son variables explícitas del servicio Compose; el C# vuelve
a validarlas. Para API nativa, definir explícitamente `ASPNETCORE_ENVIRONMENT=Development` y
`GT_SEED_DEMO=true`, y externamente `ConnectionStrings__Gt` y `GT_ARCHIVOS_RUTA` compartida
con esa API, además de las dos guardias. `GT_DOTNET` permite seleccionar el SDK local.
`.env` se interpreta como datos; nunca se ejecuta con source/eval.

## Persistencia

Se conservan los named volumes existentes `datos-sqlserver` y `archivos-documentacion`.
Imagen=software; volumen=estado; código de seed=receta reproducible.

```bash
docker compose stop
docker compose start
docker compose restart
docker compose down                 # conserva datos
docker compose up -d --build        # reconstruye software, conserva datos
```

**`docker compose down -v` elimina también los volúmenes y toda la base local**, incluidos
datos NO DEMO. No es el comando de reset del dataset. En un volumen vacío se vuelve a
preparar desde código. No se versionan MDF/LDF, backups ni volúmenes.

## Seguridad de generación y reset

La cuenta `demo.DEMO015.seed.v1.incomplete` marca una ejecución incompleta; sólo pasa a
`demo.DEMO015.v1.ready` después de validar. Misma versión válida: SKIP, sin duplicar.
Versiones distintas/incompletas: error explícito; nunca regeneración destructiva automática.
Un applock de sesión exclusivo evita dos seeders simultáneos. No afecta locks de negocio.

Antes de aceptar una versión existente, se comprueban mínimos de filas DEMO por
tabla, los 450 viajes y los escenarios del dashboard de octubre. La marca `ready`
sola no alcanza. Estos controles detectan faltantes; no comparan cada campo contra
una copia inmutable del dataset. `DEMO015` queda reservado para los datos del seed:
el reset reconoce esa marca en campos propios y sigue los vínculos del modelo.

Las cuentas DEMO tienen alta en julio2026, anterior a sus operaciones agosto–noviembre.
La empresa emisora es configuración compartida y se conserva incluso al resetear DEMO.

Las operaciones económicas conservan sus transacciones propias y compensación de archivos.
No se envuelve todo en una transacción exterior que contradiga esos contratos. Ante error,
los bloques confirmados siguen siendo estados válidos y quedan marcados incompletos;
`--reset-demo` limpia/regenera explícitamente. El reset entero sí es transaccional, por PK
filtrada y orden inverso de FK reales. No usa DROP, TRUNCATE ni DELETE sin WHERE.
Si una fila no DEMO referencia raíces DEMO y no puede clasificarse inequívocamente, aborta.
Archivos de filas DEMO se borran sólo después del commit. Usuarios ajenos, permisos y roles
estructurales se conservan. La empresa emisora existente no se sobrescribe.

## Inventario real de main

37 DbSet. Cheques/Transferencias/Efectivos son subtipos TPH de MediosPagoProveedor.
La salida `--summary` enumera también tipos EF y las dos uniones implícitas, sin ocultar ceros.

| Clase | Tablas/DbSet | Generación |
|---|---|---|
| Maestros | Roles, Permisos, DocumentacionTipos, TiposVehiculo, Bancos, EmpresaEmisora | Roles/permisos por SembradorInicial sin duplicar; catálogos DEMO; empresa sólo si ausente |
| Personas/padrones | Usuarios, Personas, Transportistas, Choferes, Clientes, Proveedores | Raíces identificadas DEMO015, contactos `.invalid` |
| Documentación | Documentaciones, DocumentacionesVehiculo | Carga real, documento opcional sin adjunto; vencimientos derivados |
| Operaciones | Viajes, Liquidaciones, Adelantos, Cajas | Casos de uso reales |
| Financieros | Facturas, FacturasProveedor, OrdenesDePago, OrdenesPagoProveedor, MediosPagoProveedor, Cheques, Transferencias, Efectivos, MovimientosDeCaja | Emisión/cobro/pago/compensación reales; PDFs en volumen |
| Históricos/vínculos | CambiosDeEstadoViaje, CambiosDeEstadoFactura, LiquidacionViajes, CambiosDeLiquidacion, CambiosDeLiquidacionViajes, CambiosDeAdelanto, OrdenPagoProveedorFacturas, HistorialEstadosCheque | Derivados exclusivamente de operaciones |
| Técnicas/autorización | ResultadosOperacionPago, UsuarioRoles, RolPermisos | Resultado real de comandos y relaciones de roles; sin filas falsas |
| Framework | __EFMigrationsHistory | Sólo EF Migrate; nunca seed manual |

No existe entidad Empleado de la rama de Juan. Persona.Tipo=Empleado sí existe y sirve al
padrón/adelantos; no se inventa ABM ni se trae esa rama. No existen tablas Equipo, Tractor,
Semi, Rendición, Cobro, Asignación ni Vencimiento: flota es Vehiculo/TipoVehiculo,
las asignaciones son actuales en Viaje, rendición/cobro son transiciones y vencimientos se
calculan. No se inventan emparejamientos históricos tractor/semi ni métricas no almacenadas.
CUIT/CUIL usan checksum real y unicidad local; sin conexión a padrón oficial no se puede
garantizar que un número sintético jamás haya sido asignado. Datos explícitamente DEMO.

La versión 1 genera 75 vehículos en siete categorías del catálogo real (15 tractores
y 10 de cada categoría restante), por encima del objetivo orientativo inicial de
40–60. No existe una entidad separada de cuentas bancarias: se usan Banco y medios
de pago. Cantidades y distribución del resto del dataset en `validation.md`.

## Verificación

```bash
dotnet run --project backend/tools/GT.SeedDemo -- --self-test
python3 backend/tools/GT.SeedDemo/smoke.py
```

El smoke usa la contraseña externa, consulta 23 endpoints, exige los indicadores
de octubre y varias unidades en el ranking, y verifica 14 exportaciones PDF/XLSX.
Guarda evidencia temporal en `/tmp/gt-demo015-smoke`, fuera del repositorio.
Los vencimientos se calculan a la fecha de consulta: sus cantidades cambian con
el calendario aunque las fechas del dataset sean fijas.

Sólo guardias propias, compilación, SQL/HTTP/exportaciones y persistencia de Compose.
No requiere repetir suites completas de015. El wrapper nativo y el smoke HTTP usan Python3
(estándar, sin paquetes extra); el arranque automático con Compose sólo requiere Docker. Evidencia de ejecución y limitaciones reales
se registran en `validation.md`; no confundir una intención de validación con un resultado.
