# Base demo integral DEMO015 (sólo desarrollo)

Herramienta explícita; no modifica el modelo ni SembradorInicial. Versión lógica 2,
Random(15015), fechas de negocio agosto–noviembre2026. Los IDs/secuencias, hashes de
contraseña, claves físicas de archivos y tiempos internos no son una identidad binaria
reproducible: lo reproducible es el conjunto y sus relaciones/escenarios de negocio.

## Entorno demo

Requiere `.env` externo según el README principal, incluido el password inicial de admin.
Podés definir `GT_DEMO_PASSWORD` externo; si falta, Compose usa el password inicial externo
para las cuentas DEMO. Nunca se imprime el password ni los hashes. Cuentas locales:
`administrador` (administración), `gerencia`, `operaciones`, `tesoreria`. No usarlas fuera de desarrollo.

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
La versión lógica está definida en `Dataset.Version = 2` y la marca en
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

La pertenencia de v2 se guarda en propiedades extendidas de la base SQL Server,
con prefijo `GT.SeedDemo.DEMO015`. Contienen exclusivamente versión, completitud
y PK exactas (incluidas claves compuestas), comprimidas y fragmentadas en bloques
menores a 7500 bytes con hash de integridad. No son tablas/columnas/entidades nuevas;
no necesitan migraciones, archivos locales ni cambios de modelo. Se conservan con
el volumen SQL y jamás se exponen por los endpoints de negocio.

El interceptor **exclusivo del tooling** registra cada INSERT y su metadata en la
misma transacción. Si el caso de uso ya abrió una transacción, participa de ella;
para un SaveChanges sin transacción exterior, el tooling abre y confirma una que
incluye ambas escrituras. Un corte no puede confirmar filas sin su identificación.
La API no registra este interceptor. La metadata inicial queda incompleta hasta
validar todo el dataset. Misma versión completa: SKIP sin registros nuevos;
versión distinta o incompleta: rechazo, nunca regeneración automática.

Un applock de sesión exclusivo evita dos seeders simultáneos. Antes de aceptar una
versión existente se comprueban las PK, mínimos por tabla, los 450 viajes, los
textos visibles persistidos en SQL y el dashboard de octubre. Estos controles no
prometen detectar cualquier edición manual de importes o reemplazar las reglas
productivas de negocio.

El reset de v2 utiliza únicamente las PK registradas, nunca nombres, emails, CUIT,
DNI ni rangos de identidad. Corregir un nombre no cambia la pertenencia. Una fila
ajena, aunque tenga el mismo nombre o refiera al dataset, no se adopta: si la FK
impide eliminar, el reset se rechaza antes de borrar. La lectura y eliminación
ocurren dentro de una transacción Serializable; la metadata se retira en ese mismo
commit. No se reseedean identidades ni secuencias.

Para migrar v1, `--reset-demo` conserva el clasificador legacy de `DEMO015`, habilitado
sólo si existe su cuenta exacta de versión. Es el único camino que usa textos
visibles para reconocer el dataset anterior. La configuración emisora se corrige
sólo si coincide exactamente con los valores que creó v1; una empresa ajena se
conserva. Luego se genera v2 y se deja de usar ese clasificador. Si falta/corrompe
la metadata de v2, no se reconstruye pertenencia a partir de nombres comerciales.

Las cuentas DEMO tienen alta en julio2026, anterior a sus operaciones agosto–noviembre.
La empresa emisora es configuración compartida y se conserva incluso al resetear DEMO.

Las operaciones económicas conservan sus transacciones propias y compensación de archivos.
No se envuelve todo en una transacción exterior que contradiga esos contratos. Ante error,
los bloques confirmados siguen siendo estados válidos y quedan marcados incompletos;
`--reset-demo` limpia/regenera explícitamente. El reset entero es transaccional, por PK
registrada y orden inverso de FK reales. No usa DROP, TRUNCATE ni DELETE sin WHERE.
Si una fila no DEMO referencia raíces DEMO y no puede clasificarse inequívocamente, aborta.
Archivos de filas DEMO se borran sólo después del commit. Usuarios ajenos, permisos y roles
estructurales se conservan. La empresa emisora ajena no se sobrescribe.

## Inventario real de main

37 DbSet. Cheques/Transferencias/Efectivos son subtipos TPH de MediosPagoProveedor.
La salida `--summary` enumera también tipos EF y las dos uniones implícitas, sin ocultar ceros.

| Clase | Tablas/DbSet | Generación |
|---|---|---|
| Maestros | Roles, Permisos, DocumentacionTipos, TiposVehiculo, Bancos, EmpresaEmisora | Roles/permisos por SembradorInicial sin duplicar; catálogos DEMO; empresa sólo si ausente |
| Personas/padrones | Usuarios, Personas, Transportistas, Choferes, Clientes, Proveedores | PK en metadata interna, nombres naturales y contactos `.invalid` |
| Documentación | Documentaciones, DocumentacionesVehiculo | Carga real, documento opcional sin adjunto; vencimientos derivados |
| Operaciones | Viajes, Liquidaciones, Adelantos, Cajas | Casos de uso reales |
| Financieros | Facturas, FacturasProveedor, OrdenesDePago, OrdenesPagoProveedor, MediosPagoProveedor, Cheques, Transferencias, Efectivos, MovimientosDeCaja | Emisión/cobro/pago/compensación reales; PDFs en volumen |
| Históricos/vínculos | CambiosDeEstadoViaje, CambiosDeEstadoFactura, LiquidacionViajes, CambiosDeLiquidacion, CambiosDeLiquidacionViajes, CambiosDeAdelanto, OrdenPagoProveedorFacturas, HistorialEstadosCheque | Derivados exclusivamente de operaciones |
| Técnicas/autorización | ResultadosOperacionPago, UsuarioRoles, RolPermisos | Resultado real de comandos y relaciones de roles; sin filas falsas |
| Framework | __EFMigrationsHistory | Sólo EF Migrate; nunca seed manual |

Gestión de Empleados está integrada en main y usa Persona.Tipo=Empleado: se generan
50 empleados y 50 personas choferes sin inventar una tabla Empleados. No existen tablas Equipo, Tractor,
Semi, Rendición, Cobro, Asignación ni Vencimiento: flota es Vehiculo/TipoVehiculo,
las asignaciones son actuales en Viaje, rendición/cobro son transiciones y vencimientos se
calculan. No se inventan emparejamientos históricos tractor/semi ni métricas no almacenadas.
CUIT/CUIL usan checksum real y unicidad local; sin conexión a padrón oficial no se puede
garantizar que un número sintético jamás haya sido asignado. Datos explícitamente DEMO.

La versión 2 conserva 75 vehículos en siete categorías del catálogo real (15 tractores
y 10 de cada categoría restante), por encima del objetivo orientativo inicial de
40–60. No existe una entidad separada de cuentas bancarias: se usan Banco y medios
de pago. Cantidades y distribución del resto del dataset en `validation.md`.

## Verificación

```bash
dotnet run --project backend/tools/GT.SeedDemo -- --self-test
docker compose run --rm seed-demo --self-test-sql --confirm
docker compose run --rm seed-demo --self-test-reset --confirm
docker compose run --rm seed-demo --check-visible --confirm
python3 backend/tools/GT.SeedDemo/smoke.py
```

El smoke usa la contraseña externa, recorre todas las páginas de los listados,
exige los indicadores de octubre y varias unidades en el ranking, verifica 14
exportaciones PDF/XLSX y cuatro comprobantes/adjuntos persistidos. Requiere
`pdftotext` para inspeccionar el texto de los PDF.
Guarda evidencia temporal en `/tmp/gt-demo015-smoke`, fuera del repositorio.
Los vencimientos se calculan a la fecha de consulta: sus cantidades cambian con
el calendario aunque las fechas del dataset sean fijas.

Sólo guardias propias, compilación, SQL/HTTP/exportaciones y persistencia de Compose.
No requiere repetir suites completas de015. El wrapper nativo y el smoke HTTP usan Python3
(estándar, sin paquetes extra); el arranque automático con Compose sólo requiere Docker. Evidencia de ejecución y limitaciones reales
de v1 se conservan en `validation.md`; los resultados de v2 se documentan en
`presentation-validation.md`. No confundir una intención de validación con un resultado.

`--check-visible` sólo lee: inspecciona mediante SQL cada columna textual visible de
las PK propias y sus referencias compartidas. Busca DEMO (incluye DEMO015), TEST,
PRUEBA, FAKE y SAMPLE, sin distinguir mayúsculas; informa tabla/campo/PK, nunca el
valor. Excepciones explícitas: PasswordHash, DocumentoRuta/ArchivoRuta físicos y
la tabla técnica de idempotencia (canon y replay). Nombres originales de archivos,
usuarios, emails, motivos y snapshots de facturas sí se inspeccionan. Los adjuntos
y exportaciones se inspeccionan adicionalmente por su contenido en el smoke.
`--self-test-sql` usa sólo escrituras revertidas para probar atomicidad; no regenera.
`--self-test-reset` prueba cambios de nombre y referencias ajenas en memoria, sin
escribir. El servidor local requiere permiso de administración de las propiedades
extendidas para el tooling; no se agrega ningún privilegio al runtime productivo.
