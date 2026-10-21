# Registro de implementación — 015 Ampliación de reportes

## T001 — Línea base en curso

Fecha: 2026-10-19, Argentina. Contexto activo: specs/015-ampliacion-reportes.
No se modificó código de producción antes de ejecutar esta línea base. Spec/plan y decisiones aprobadas permanecen cerradas.

Entorno comprobado: SDK .NET 10.0.302 en /home/joaco/.dotnet/dotnet, Node v24.22.0, npm 11.19.1. `docker compose ps --format json` informa SQL Server running/healthy. Configuración externa existente, sin copiar secretos.

Build ejecutado: `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo`.
Resultado: correcto, 0 errores y 14 advertencias preexistentes (NU1902/NU1903 Pkcs, CS8604 y analizadores xUnit en archivos anteriores). No se actualizaron dependencias ni debilitaron tests.

En ejecución: GT.UnitTests completa; integración SQL Reportes y módulos Choferes/Flota/Viajes/Facturacion/Liquidaciones/Caja/Proveedores/Pagos; frontend de esos módulos y Reportes.
T001 no está completada hasta registrar resultados reales. TRX temporales previstos en /tmp/gt015-baseline, sin versionarlos. El baseline intermitente009 se observa sin modificar su aserción.

### Resultados parciales y comandos T001

- GT.UnitTests: 763 aprobados, 0 fallos/omitidos; `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=baseline-unit.trx' --results-directory /tmp/gt015-baseline`.
- Frontend: 467 pruebas aprobadas en 63 archivos, 0 fallos. Desde frontend: `npm test -- src/modules/reportes src/modules/choferes src/modules/flota src/modules/viajes src/modules/facturacion src/modules/liquidaciones src/modules/caja src/modules/proveedores src/modules/pagos`.
- SQL en ejecución: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Reportes|FullyQualifiedName~Choferes|FullyQualifiedName~Flota|FullyQualifiedName~Viajes|FullyQualifiedName~Facturacion|FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores|FullyQualifiedName~Pagos' --logger 'trx;LogFileName=baseline-sql.trx' --results-directory /tmp/gt015-baseline`.

Resultado SQL final: **1025 aprobados, 0 fallos/omitidos**, duración 2m34s. Conteos por namespace: Caja 88, Choferes 137, Facturacion 118, Flota 120, Liquidaciones 94, Pagos 187, Proveedores 64, Reportes 38, Viajes 177. Test baseline009: ['Passed']. No se declara resuelta su intermitencia histórica. T001 completada antes de cambios de producción.

## T002 — Superficies compartidas con Empleados

Inspeccionados código real y git status: GT.Api/Program.cs (DI/políticas/rutas),
GT.Domain/Usuarios/Rol.cs (constantes), GT.Infrastructure/DatosIniciales/SembradorInicial.cs
(catálogo/asignaciones), GT.Application/Autenticacion/CatalogoOpcionesMenu.cs (entrada),
frontend/src/App.tsx (rutas), frontend/src/compartido/Menu.tsx y seccionesDeMenu.ts
(agrupación), autenticacion/servicios/sesion.ts (constantes) y fixture
backend/tests/GT.IntegrationTests/Infraestructura/AplicacionDePrueba.cs.

Regla de integración: cambios aditivos mínimos en esos archivos; módulos, DTOs, consultas y
pruebas nuevos dentro de Reportes. No leer/copiar código de rama Empleados, no merge/cherry-pick,
no ordenar archivos completos. Comparación final en T088 contra esta lista y git diff.
Validación: git branch --show-current y git status --short (rama015; sólo documentación previa),
lectura de fuentes de catálogo/fixture y puntos de integración. T002 completada sin cambios de producción.

## T003 — Período y contexto

Archivos nuevos: GT.Application/Reportes/Analisis/PeriodoAnalisis.cs y ContextoAnalisis.cs;
GT.UnitTests/Reportes/PeriodoAnalisisTests.cs. Período inmutable inclusivo, meses liquidados
completos y límites UTC argentinos semiabiertos; contexto captura reloj una sola vez.
Situación actual no admite un período histórico. Hasta DateOnly.MaxValue se rechaza al
convertir a intervalo de instantes porque su fin de día argentino excede DateTime/SQL,
sin envolver ni recortar fecha (alternativa técnica ya prevista en data-model).

Comandos: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~PeriodoAnalisisTests --logger 'trx;LogFileName=t003.trx' --results-directory /tmp/gt015-fundamentos`;
`/home/joaco/.dotnet/dotnet build backend/src/GT.Application/GT.Application.csproj --no-restore --nologo`.
9 pruebas aprobadas, 0 fallos/omitidos; build Application correcto sin warnings/errores.
Primera corrida restringida terminó1 durante compilación sin resultado de tests; repetida
fuera del sandbox con autorización. Sólo warnings Pkcs/CS8604 previos al compilar dependencias.
Sin cambios de negocio ni desviaciones funcionales. T003 completada.

## T004 — DTOs de actividad y dashboard

Nuevos ContratosActividad.cs y ContratosDashboard.cs en GT.Application/Reportes/Analisis,
y ContratosActividadAnalisisTests.cs en unitarios Reportes. Contratos tipados de KPI/mes/grupos,
Viajes, Documentos/Cobros y dashboard completo sin entidades ni IQueryable. Sección denegada
no puede llevar Datos; disponible sin datos se rechaza. Los resúmenes de las demás áreas se
definen aquí por ser parte del dashboard; T005 añade sus filas/páginas sin duplicarlos.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ContratosActividadAnalisisTests --logger 'trx;LogFileName=t004-completo.trx' --results-directory /tmp/gt015-fundamentos`.
Resultado: 4 aprobadas, 0 fallos/omitidos. Primera tanda parcial t004.trx también4 aprobadas;
se amplió contrato dashboard y se revalidó. Compilación correcta, sólo warnings previos Pkcs/CS8604.
Fechas de factura/cobro y snapshot se conservan sin recalcular desde DTO. T004 completada.

## T005 — Contratos de fuentes restantes

ContratosFuentes.cs y ContratosFuentesAnalisisTests.cs: filas/páginas de Liquidaciones,
Pagos, Caja y Flota, referencias mínimas y grupos. Vencimientos reutiliza tipos de los
paneles existentes sin copiar sus reglas. OP/OPP IDs separados; nullabilidad preserva
origen manual/reversión y deuda no aplicable. PaginaDe default20 ya existente se reutiliza.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~ContratosFuentesAnalisisTests --logger 'trx;LogFileName=t005-corregida.trx' --results-directory /tmp/gt015-fundamentos`.
Resultado:3 aprobadas,0 fallos/omitidos. La primera compilación detectó un using faltante
para AlertaVencimiento (namespace Documentacion); corregido antes de validar, sin modificar
el módulo fuente. Warnings previos Pkcs/CS8604, ninguno nuevo de Reportes. T005 completada.

## T006 — Frontera de consultas y filtros

FiltrosAnalisis.cs e IRepositorioAnalisisReportes.cs en Application; FiltrosAnalisisTests.cs.
Filtros tipados separados por perspectiva, contexto/periodo y paginación validada con
protección de overflow en skip. Listas cerradas de estados/orden/vigencia/dimensiones e
identificadores positivos; interfaz materializada sin IQueryable ni referencia Infrastructure.
No se publican handlers ni implementaciones temporales de consultas todavía pendientes.

Comandos: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~FiltrosAnalisisTests --logger 'trx;LogFileName=t006.trx' --results-directory /tmp/gt015-fundamentos`;
`/home/joaco/.dotnet/dotnet build backend/src/GT.Application/GT.Application.csproj --no-restore --nologo`.
10 tests aprobados, build Application correcto sin warnings/errores; al compilar proyectos
dependientes sólo Pkcs/CS8604 previos. Sin decisiones funcionales nuevas. T006 completada.

## T007 — JSON monetario localizado

SerializadorAnalisisReportes.cs (Api/Reportes) y SerializacionAnalisisTests.cs. Se inspeccionó
ImporteDecimalJsonConverter013: lectura sólo no negativa y limitada al máximo de factura;
por eso se conserva intacto y015 usa converter localizado firmado y sin tope de formulario.
Escritura rechaza escala superior a2 en vez de redondear; lectura verifica forma canónica y
round-trip para no aceptar decimal.Parse con pérdida de precisión. Contexto UTC con Z,
enums camelCase y error tipado de campo. No cambio de opciones JSON globales ni endpoints nuevos.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~SerializacionAnalisisTests --logger 'trx;LogFileName=t007.trx' --results-directory /tmp/gt015-fundamentos`.
15 aprobadas,0 fallos/omitidos; sólo warnings Pkcs previos. Incluye negativo, máximo de fila,
agregado mayor y decimal.MaxValue, formato/cultura/rango inválido y contrato013 intacto.
T007 completada.

## T008 — Permisos y semillas

Rol.cs añade dos códigos, SembradorInicial agrega catálogo y asignación explícita sólo a
Gerencia/AdministradorSistema. PoliticasAnalisisReportes.cs registra base y conjunciones
por fuente/emisión; Program agrega un único registro sin reordenar rutas ni lógica anterior.
Sin endpoints incompletos ni menú prematuro; la entrada Reportes se integra en T076.

Pruebas: `dotnet test` ruta SDK explícita/proyecto GT.UnitTests, filtroPoliticasAnalisisTests,
TRX t008-politicas.trx (7 aprobadas); IntegrationTests filtro
`FullyQualifiedName~SemillasAnalisisTests|FullyQualifiedName~PermisosDeReporteTests`,
TRX t008-semillas-legacy.trx (9 aprobadas). Ambos con --no-restore y resultados en
/tmp/gt015-fundamentos. Semillas idempotentes y ocho casos legacy de autorización012 verdes.
Sin fallos/omitidos ni warnings nuevos (Pkcs/CS8604/analizadores anteriores). T008 completada.

## T009 — Matriz base de autorización

PermisosAnalisisTests.cs usa usuarios/roles de prueba reales, autenticación de cookie y
IAuthorizationService del contenedor para las ocho combinaciones base/fuente/emisión.
Comprueba sesión efectiva, revocación y exportación legacy sin base nueva. Nuevas rutas
analíticas aún no publicadas; su matriz HTTP se extenderá en T022/T025/T028/T030/T033/T035,
T039 yT059, incluidos referencias/grupos/exportaciones y secciones denegadas del dashboard.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~PermisosAnalisisTests|FullyQualifiedName~SemillasAnalisisTests|FullyQualifiedName~PermisosDeReporteTests' --logger 'trx;LogFileName=t009-permisos-base.trx' --results-directory /tmp/gt015-fundamentos`.
18 pruebas SQL/HTTP aprobadas,0 fallos/omitidos; warnings previos. No se expusieron handlers
sin caso de uso ni se añadieron chequeos de rol. T009 completada.

## T010 — Detección Snapshot sin mutación

EstadoSnapshotAnalisis.cs en Application y DetectorSnapshotAnalisis.cs en Infrastructure:
SELECT sólo de sys.databases/DB_ID, enum OFF/ON/transiciones y RCSI independiente.
Respeta propiedad de conexión (abre/cierra sólo si la abrió) y no hace ALTER ni imprime cadena.
EstadoSnapshotAnalisisTests.cs prueba OFF/ON y transición real aON retenida por transacción
sobre tabla sintética, sin sleeps. Sólo la cuenta administrativa del harness altera su
base efímera; runtime no tiene ni ejecuta esa operación.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EstadoSnapshotAnalisisTests --logger 'trx;LogFileName=t010-snapshot-corregida.trx' --results-directory /tmp/gt015-fundamentos`.
2 aprobadas,0 fallos/omitidos; primera tanda1 aprobado/1 falló por precondiciónOFF no aislada
entre los dos tests de la misma base. Se corrigió precondición del harness, no producción.
RCSI intacto; advertencias anteriores. T010 completada; no se habilitó Snapshot en desarrollo.

## T011 — Habilitación administrada

backend/scripts/habilitar-snapshot-reportes.sql, wrapper .sh yREADME.md. Destino explícito
validado, base existente no sistema, cuenta con ALTER, fuera de transacción. Idempotente,
verifica ON/RCSI intacto, sin SINGLE_USER/ROLLBACK IMMEDIATE/RCSI. Password sólo por variable
SQLCMDPASSWORD; runtime no llama el script ni recibe ALTER. Sin secretos nuevos.

Validación: `bash -n backend/scripts/habilitar-snapshot-reportes.sh` correcto. Pruebas
SQL ejecutan el mismo archivo mediante SqlClient sólo en base efímera (sqlcmd no instalado
en este host; wrapper queda para operador con herramienta instalada y certificado confiable).
Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~PreparacionSnapshotAnalisisTests --logger 'trx;LogFileName=t011-preparacion.trx' --results-directory /tmp/gt015-fundamentos`.
2 aprobadas,0 fallos/omitidos: repetición ON e invarianciaRCSI, rechazos base sistema y
transacción. Sólo warnings anteriores. Ninguna base de desarrollo alterada. T011 completada.

## T012 — Provisión de base nueva

README operativo amplía secuencia migrar→habilitación administrada→verificar/readiness→runtime
sin ALTER. InstalacionSnapshotAnalisisTests.cs verifica base nueva migrada hasta014 sin viajes,
prepara ON mediante script real, RCSI intacto, escritura normal de Banco y cinco exportaciones
legacy con sus permisos anteriores. No se habilita Snapshot automáticamente en el arranqueAPI.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~InstalacionSnapshotAnalisisTests --logger 'trx;LogFileName=t012-base-nueva.trx' --results-directory /tmp/gt015-fundamentos`.
1 recorrido SQL/HTTP aprobado,0 fallos/omitidos; warnings anteriores. Operación sólo en base
efímera del fixture y sin migración nueva. T012 completada.

## T013 — Preparación de base existente

UpgradeSnapshotAnalisisTests.cs y procedimiento de upgrade en backend/scripts/README.md.
Base efímera con generación real014 en efectivo y Fletero (tres OP), más OP manual parcial009.
Se captura grafo de facturas/liquidaciones/OP/OPP/movimientos/resultados y migraciones antes
de ejecutar el script administrado; después coincide exactamente, opciónON y RCSI intacto.
Sin downgrade, datos de desarrollo, migración nueva ni cambio de permisos del runtime.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~UpgradeSnapshotAnalisisTests --logger 'trx;LogFileName=t013-upgrade.trx' --results-directory /tmp/gt015-fundamentos`.
1 recorrido SQL aprobado,0 fallos/omitidos; warnings anteriores. T013 completada.

## T014 — Fixture optativo y regresión012

AplicacionAnalisisDePrueba.cs añade opt-in explícito de Snapshot mediante scriptadministrado,
base efímera verificada y cuenta de harness; AplicacionDePrueba original no se modifica.
FixtureSnapshotAnalisisTests comprueba ON, fixtureOFFindependiente, cierre/eliminación por
metadata y que el fixtureONpermanece independiente. No preparación automática de runtime.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~FixtureSnapshotAnalisisTests|FullyQualifiedName~PermisosDeReporteTests|FullyQualifiedName~ReporteViajesTests|FullyQualifiedName~ReportesDeVencimientosTests|FullyQualifiedName~ReporteMovimientosCajaTests|FullyQualifiedName~ErroresDeReporteTests|FullyQualifiedName~ArmadoresTests' --logger 'trx;LogFileName=t014-fixture-legacy.trx' --results-directory /tmp/gt015-fundamentos`.
39 aprobadas (38legacy012+1fixture),0fallos/omitidos; warningsanteriores. T014 completada.

## T015 — Readiness específico015

ReadinessAnalisisReportes.cs, AnalisisNoDisponibleException.cs, traducción localizada de503
en SerializadorAnalisisReportes y dos registrosDI aditivos en Program. OFF/transición no
habilita la lectura; falla SQL de diagnóstico se traduce sin revelar detalle interno.
No cambia arranque, rutaslegacy ni aislamiento global. Sin endpoints analíticos provisorios.

Unitarios: filtroReadinessRespuestaAnalisisTests, t015-respuesta.trx:1aprobada.
Integración: filtrosReadinessAnalisisOffTests/ReadinessAnalisisOnTests,
t015-readiness-sql.trx:2aprobadas, incluidos DIreal y cinco exportacioneslegacy disponiblesOFF.
Comandos dotnet test con rutaSDK explícita/proyecto correspondiente, --no-restore,
--logger 'trx;LogFileName=...' y --results-directory /tmp/gt015-fundamentos.
0fallos/omitidos, warningsanteriores. MatrizHTTP nuevasrutas se añade enfase2. T015 completada.

## T016 — Coordinación de foto materializada

CoordinadorFotoAnalisis.cs y registroDI aditivo: readiness previo, una transacción Snapshot
con el mismo GtDbContext, consultas secuenciales, materialización y cierre antes de consumirDTO.
Rechaza IQueryable diferido y transacción anidada; rollback/dispose ante error, sin reintento
ni fallback. SQLtimeout/deadlock/opciónsnapshot ya no disponible se traducen a excepción503.
No UPDATE/applock ni locks de escritura desde el lector.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~CoordinadorFotoAnalisisTests --logger 'trx;LogFileName=t016-foto.trx' --results-directory /tmp/gt015-fundamentos`.
3 pruebas SQL aprobadas: misma transacción/conexión sintracking yDTOutilizable tras cerrar,
fallo sin transacción abierta/reintento, rechazo diferido/anidado. Warnings anteriores.
T016 completada; las consultas de negocio usarán este coordinador al implementarse.

## T017 — Bloqueo seguro con Snapshot OFF

SnapshotAnalisisOffTests.cs: coordinador real/SQL y respuesta503 para consumidores dashboard,
tabla, grupos y exportación sin ejecutar sus callbacks ni entregar datos parciales; transacción
no se inicia. Las cinco rutas012 siguen devolviendo XLSX. Nuevas rutas aún no publicadas:
la matriz HTTP015 se amplía al implementar cada endpoint según las dependencias aprobadas.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SnapshotAnalisisOffTests --logger 'trx;LogFileName=t017-off.trx' --results-directory /tmp/gt015-fundamentos`.
5 aprobadas,0fallos/omitidos; warnings anteriores. Sin fallback ni sleeps. T017 completada.

## T018 — Snapshot ON frente a escritor concurrente

SnapshotAnalisisOnTests.cs: transacción Snapshot real cuenta filas, barrera libera escritor
independiente que actualiza importe e inserta otra fila y confirma; lector luego obtiene
SUM y detalle de la foto anterior. Segunda consulta ve count2/total350, primera count1/total100.
Sólo tabla sintética del harness, conexiones independientes y TaskCompletionSource con
cancelación de seguridad; sin sleeps ni locks de escritura desde Reportes.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SnapshotAnalisisOnTests --logger 'trx;LogFileName=t018-on-concurrente.trx' --results-directory /tmp/gt015-fundamentos`.
1 prueba SQL concurrente aprobada,0fallos/omitidos; warningsprevios. Los casos de negocio
cobro/pago/anulación/tipo se añaden enT038 después de consultas estables. T018 completada.

## T019 — Checkpoint obligatorio de fundamentos

Build completo: `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo`,
0errores/8warnings Pkcs previos. Unitarios COMPLETOS:812aprobados,0fallos/omitidos:
`/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=t019-unit-completa.trx' --results-directory /tmp/gt015-fundamentos`.
Integración Reportes COMPLETA (012+fundamentos015):66aprobados,0fallos/omitidos:
`/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~GT.IntegrationTests.Reportes --logger 'trx;LogFileName=t019-sql-reportes-fuera-sandbox.trx' --results-directory /tmp/gt015-fundamentos`.
Primera corrida SQL se abortó por SocketException13 de VSTest dentro del sandbox sin ejecutar
pruebas; repetida con autorización fuera, no fallo funcional. No se oculta el intento abortado.

Contratos/serializador/políticas/semillas, fixture optativo, OFF/ON/transición, script base nueva
/existente, foto concurrente y compatibilidad012 validados. Script operativo documentado,
sin ALTER en runtime, sinRCSI/locks económicos/fallback. No endpoints incompletos publicados.
Spec/plan/research/modelo/contratos/quickstart intactos, git diff --check correcto.
Estado: T001–T019 completas; se habilita T020. Baseline009 pasó enT001 sin modificarlo.


## T020 — Tabla analítica de Viajes

Extraído el predicado existente a FiltrosViajesSql y reutilizado por el listado005 y la tabla015. RepositorioAnalisisReportes materializa página/conteo en Snapshot; conserva búsqueda sin acentos, filtros, orden y referencias inactivas. Sin rutas incompletas expuestas.

Comando: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~TablaViajesAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Viajes' --logger 'trx;LogFileName=t020-tabla-regresion005.trx' --results-directory /tmp/gt015-consultas`. Resultado comprobado en TRX:178 aprobados,0 fallos/omitidos. Incluye177 pruebas existentes de005; aserciones originales intactas.


## T021 — Agregados de Viajes

RepositorioAnalisisReportes.Viajes agrega agrupamientos SQL por estado/mes/cliente/transportista sobre el predicado compartido, sin materializar viajes para sumar. Meses vacíos se completan como cero sobre resultados agregados; Rendido+Facturado se cuentan una vez, Anulado no aporta importe. Distribuciones conservan categorías y recorte declarado top10/Otros; estabilización de grupos SQL en T036.

Validación: prueba de202 viajes no anulados, centavos exactos, páginas independientes, mes vacío, inactivo, transportista nulo y anulado explícito. `dotnet test ... --filter FullyQualifiedName~TablaViajesAnalisisTests` con SDK explícito:2 aprobadas (t021-agregados.trx). Regresión posterior `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Viajes|FullyQualifiedName~TablaViajesAnalisisTests' --logger 'trx;LogFileName=t021-regresion005.trx' --results-directory /tmp/gt015-consultas`:179 aprobadas,0 fallos/omitidos. Warnings previos Pkcs/nulabilidad/analizadores; sin nuevos. git diff --check correcto.


## T022 — HTTP Viajes, grupos y referencias

Cuatro rutas GET de Viajes publicadas con política base+viajes.consultar, JSON monetario local, contexto capturado, errores de campo, no-store/nosniff. Grupos y referencias paginan en SQL bajo Snapshot; opciones incluyen inactivos sin acceso a gestión. Interfaz IConsultaViajesAnalisis integrada en IRepositorioAnalisisReportes permite registrar únicamente la perspectiva implementada sin cuerpos provisionales para otras operaciones. Program sólo agrega DI/mapeo; sin menú ni pantallas incompletas.

Pruebas HTTP/SQL de contrato y matriz8 combinaciones base/fuente/emisión, más revocación. OFF devuelve503 en las4 rutas autorizadas y403 sin cualquiera de los permisos, legacy conserva requisitos. ON entrega tabla/grupos/opciones y dinero exacto; parámetros inválidos400. Primera corrida9 aprobados/1 fallo de expectativa del test nuevo: middleware agrega no-cache/must-revalidate a no-store. Se verifica directiva NoStore sin exigir ausencia de protecciones adicionales. Repetición `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RutasViajesAnalisisTests|FullyQualifiedName~PermisosAnalisisTests' --logger 'trx;LogFileName=t022-http-corregida.trx' --results-directory /tmp/gt015-consultas`:10 aprobados,0 fallos/omitidos. Warnings previos; git diff --check correcto.


## T023 — Documentos de Facturación

RepositorioAnalisisReportes.Facturacion proyecta importes persistidos y snapshot del cliente; agrupa SQL por estado visible/mes/ClienteId con nombre actual rotulado. Estado pendiente/vencida exclusivo según hoy capturado, anuladas sólo por filtro explícito con importes nominales visibles y contribución financiera cero. EstadoFacturaAnalisis conserva palabras reales de Facturación. Ninguna escritura ni cambio006.

Prueba SQL de cuatro estados, día de vencimiento, cliente inactivo/snapshot y refacturación. Primera corrida falló sólo porque el helper de cliente agrega un sufijo único al nombre esperado; corregido el test nuevo comparando el nombre real creado. Regresión `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~FacturacionAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Facturacion' --logger 'trx;LogFileName=t023-regresion006-corregida.trx' --results-directory /tmp/gt015-consultas`:119 aprobados,0 fallos/omitidos, incluidos118 originales006 sin cambios. Warnings preexistentes.


## T024 — Cobros por fecha efectiva

RepositorioAnalisisReportes.Cobros filtra Pagada/FechaCobro, sin cortar por FechaFactura; total completo, series y grupos SQL, detalle por fechaCobro/Id. Sin pagos parciales inventados ni resta entre universos.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~CobrosAnalisisTests|FullyQualifiedName~FacturacionAnalisisTests' --logger 'trx;LogFileName=t024-cobros.trx' --results-directory /tmp/gt015-consultas`:2 aprobados,0 fallos/omitidos. Facturas septiembre cobradas1/31octubre incluidas,30septiembre/1noviembre excluidas, cliente ajeno excluido; páginas y número, vacío/cero y deuda documental independiente comprobados. Fuentes aprobadas verificadas por SHA256 sin cambios; diff-check correcto, warnings preexistentes.


## T025 — HTTP Facturación y Cobros

Cinco rutas de documentos/cobros/grupos/referencias con política base+facturacion.consultar, contextos separados y conversor exacto. IConsultaFacturacionAnalisis se integra al contrato general como superficie implementada; sin métodos provisionales. Grupos SQL/paginados por identidad, snapshot en documentos y referencia actual mínima. No cambios006 ni012.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RutasFacturacionAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Facturacion|FullyQualifiedName~GT.IntegrationTests.Reportes' --logger 'trx;LogFileName=t025-http-regresion.trx' --results-directory /tmp/gt015-consultas`:190 aprobados,0 fallos/omitidos. Incluye regresión006 y012 completa y matriz base/fuente/ambos/sin permisos sobre5 rutas. Factura septiembre cobrada octubre aparece sólo en flujo. Ampliación OFF de ReadinessAnalisisOffTests:5 rutas nuevas503 tipado y5legacy200; corrida t025-off-http.trx1 aprobada. Warnings previos; diff-check correcto.


## T026 — Documentos de Liquidaciones

RepositorioAnalisisReportes.Liquidaciones selecciona meses completos por límites año/mes, no generación; total/pagado/pendiente vigentes009, estados/grupos/series SQL sin unir viajes para sumar, identidad actual e inactivo preservados. Anulada conserva nominal y pendiente nulo con contribución financiera cero.

Primera prueba detectó conversión de parámetro año*100+mes a smallint inferida por EF; corregido a comparaciones lexicográficas año/mes, sin cálculo de clave. Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~LiquidacionesAnalisisTests' --logger 'trx;LogFileName=t026-documentos-corregida.trx' --results-directory /tmp/gt015-consultas`:1 aprobado,0 fallos/omitidos. 15sept–10oct incluye ambos meses completos, generación noviembre no excluye septiembre, parcial/manual y pagada reconcilian SUM(OP Vigente), dos viajes no multiplican total, página no cambia agregados y anulada sin deuda. Warnings previos.


## T027 — Flujo de pagos OP

RepositorioAnalisisReportes.Pagos usa FechaPago y OP como unidad, origen/OPP separada, vigencia actual e historia de reversión. Agregados/series/grupos SQL excluyen revertidas del importe vigente; sin restricción por período liquidado ni suma de OPP/Caja.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~PagosAnalisisTests' --logger 'trx;LogFileName=t027-pagos.trx' --results-directory /tmp/gt015-consultas`:1 aprobado,0 fallos/omitidos. Operaciones reales014 generan3 OP por OPP y anulan en noviembre: octubre conserva363 revertidos y sólo50 manual vigente de liquidación septiembre. Nuevo pago noviembre no duplica octubre; filtros vigentes/revertidos, autor/instante/origen y número OP/OPP comprobados. Sin escrituras nuevas en Reportes, warnings anteriores.


## T028 — HTTP Liquidaciones y Pagos

Cinco rutas con política base+liquidaciones.consultar, contextos períodoLiquidado/FechaPago, grupos SQL y referencias mínimas. IConsultaLiquidacionesAnalisis integrada al contrato general; filtros de vigencia cerrados, OP/OPP distintas, sin permisos manuales implícitos. OFF503 tipado ampliado a estas5 rutas.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Reportes|FullyQualifiedName~GT.IntegrationTests.Liquidaciones|FullyQualifiedName~GT.IntegrationTests.Pagos' --logger 'trx;LogFileName=t028-http-regresion009014.trx' --results-directory /tmp/gt015-consultas`:356 aprobados,0 fallos/omitidos. Incluye00994 y014187 originales; baseline temporal009 aprobado sin modificar test ni declarar resuelta su intermitencia. Matriz4 combinaciones base/fuente sobre5 rutas,3OP/1OPP y contextos/meses/verificación de vigencia. Warnings preexistentes.


## T029 — Caja analítica

RepositorioAnalisisReportes.Caja comparte filtros/proyección/ALista de RepositorioCaja mediante visibilidad internal y extracción del predicado existente; escrituras/DTOs/SQL legacy sin cambios funcionales. Una consulta adicional por página trae identificación mínima de caja y FK OP para015, acotada e independiente de filas (sin N+1). Series agrupan movimientos en mes argentino; neto=ingresos-egresos, no saldo. NombreCajaAnalisis arma responsable/apertura argentina.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~CajaAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Caja|FullyQualifiedName~GT.IntegrationTests.Reportes' --logger 'trx;LogFileName=t029-caja-regresion.trx' --results-directory /tmp/gt015-consultas`:165 aprobados,0 fallos/omitidos, incluidos88 originales011 y012. Cuatro bordes argentinos y serie octubre/neto negativo cotejados con listado legacy; egreso/compensación OPP en meses distintos conservan referencias y FKs separadas. Warnings previos; diff-check correcto.


## T030 — HTTP Caja y referencias

Tres rutas con base+caja.consultar, snapshot/no-store, errores de campo, cadenas exactas firmadas, contexto argentino y lookup paginado mínimo sin permisos de gestión. Tipos agrupados SQL y nombres de caja por responsable/apertura; IConsultaCajaAnalisis integrada al contrato general.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RutasCajaAnalisisTests|FullyQualifiedName~ReadinessAnalisisOffTests|FullyQualifiedName~CajaAnalisisTests' --logger 'trx;LogFileName=t030-http-caja.trx' --results-directory /tmp/gt015-consultas`:4 aprobados,0 fallos/omitidos. Matriz4 combinaciones sobre3 rutas, neto -0.01 exacto, lookup sin datos de gestión, cajaId inválido400 y acceso lateral Bancos403. OFF503 nuevo/legacy200; bordes y compensación revalidados. Warnings previos.


## T031 — Conteos de utilización

RepositorioAnalisisReportes.Flota arma LEFT JOIN de todas las unidades filtradas con conteos SQL de Viajes Rendido/Facturado porVehiculoId/Fecha; ceros/bajas, orden cantidad/Patente/Id y filtros antes de página. ConteosFlotaAsync ofrece la proyección de conteos sin publicar endpoint ni fingir disponibilidad pendiente de T032.

Primera ejecución detectó que EF no traduce el chequeo nulo del objeto anónimo agregado; reemplazado por COALESCE del conteo nullable, sin evaluación en memoria. `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~ConteosFlotaAnalisisTests' --logger 'trx;LogFileName=t031-flota-corregida.trx' --results-directory /tmp/gt015-consultas`:1 aprobado,0 fallos/omitidos. Incluye0/1/2viajes, baja,3 estados excluidos, fuera de período, sin vehículo, páginas y filtros activo/Id. Warnings previos.


## T032 — Flota: atributos actuales y disponibilidad

RepositorioAnalisisReportes.FlotaAnalisis agrega resumen/series/distribuciones SQL y detalle actual. Documentos vigentes se proyectan en lote sólo para página, sin escaneos/historia completa; CalculadorEstadoDocumento, CalculadorEstadoOperativo y EstadoDesde004 reutilizados (éste sólo cambia a internal). Sin vehículo se cuenta por período fuera de filtros de unidades, rotulado explícitamente en UI futura.

SQL: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~FlotaAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Flota|FullyQualifiedName~GT.IntegrationTests.Choferes' --logger 'trx;LogFileName=t032-flota-regresion003004.trx' --results-directory /tmp/gt015-consultas`:259 aprobados,0 fallos/omitidos (120Flota/137Choferes+2nuevos). Unitarios existentes004 con --no-build --no-restore --filter FullyQualifiedName~GT.UnitTests.Flota, t032-reglas004.trx:48 aprobados. Igualdad dominio/lectura ante documento renovado, empates, ceros/bajas, nueva propiedad/tipo sin perder viajes y contador sinvehículo comprobados. Warnings previos, diff-check correcto.


## T033 — HTTP Utilización de flota

Cinco rutas con base+reportes.flota.consultar, sin flota.gestionar. Referencias paginadas incluyen inactivos y sóloId/nombre/activo; activo ausente=ambos, filtros booleanos inválidos400, grupos SQL/tipo/transportista. IConsultaFlotaAnalisis integrada al contrato general. OFF503 de las5 rutas añadido.

Primera corrida3 aprobados/1 fallo propio: el test lateral usó /api/vehiculos inexistente (404). Corregido a ruta real /api/flota/vehiculos para afirmar403. Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RutasFlotaAnalisisTests|FullyQualifiedName~ReadinessAnalisisOffTests' --logger 'trx;LogFileName=t033-http-corregida.trx' --results-directory /tmp/gt015-consultas`:2 aprobados,0 fallos/omitidos. Matriz4 combinaciones en5 rutas, ceros/bajas y lookup mínimo verificados. Warnings previos; reglas y conteos SQL verdes T032.


## T034 — Fachada de Vencimientos

RepositorioAnalisisReportes.Vencimientos reutiliza los casos actuales de Choferes/Flota con fecha explícita y la consulta original de Facturas con hoy capturado. RepositorioFacturas extrae esa lectura a colaborador estático internal sin cambiar el SQL ni requerir el almacén de archivos de sus escrituras. Tres paneles conservan universo, orden, estados y metadatos autorizados originales; sin bytes, rutas, enlaces de adjuntos ni cheques. Paginación sólo de fachada sobre las listas vigentes originales en backend, sin enviar el conjunto completo al navegador; total/importe antes de paginar. Sin período histórico.

Validación real:21 choferes alertados e inactivo excluido,20+1 páginas idénticas al panel, flota idéntica, facturas ventana inclusiva7días/exclusiónpagada/total363 y página1 idénticos a006. `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~VencimientosAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Facturacion|FullyQualifiedName~GT.IntegrationTests.Reportes' --logger 'trx;LogFileName=t034-vencimientos-regresion.trx' --results-directory /tmp/gt015-consultas`:200 aprobados,0 fallos/omitidos (006/012incluidos). Regresión adicional requerida003/004 con --no-build --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Choferes|FullyQualifiedName~GT.IntegrationTests.Flota', t034-regresion003004.trx:257 aprobados,0 fallos/omitidos. Warnings anteriores, diff-check correcto. Sin repetición de tareas cerradas.


## T035 — HTTP Vencimientos

Tres rutas con base+permiso de cada panel, contexto situacionActual y paginación015; rechazan desde/hasta en vez de simular corte histórico. IConsultaVencimientosAnalisis integrada al contrato general, sin modificar políticas/rutas legacy.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~RutasVencimientosAnalisisTests|FullyQualifiedName~VencimientosAnalisisTests|FullyQualifiedName~PermisosDeReporteTests' --logger 'trx;LogFileName=t035-http-vencimientos.trx' --results-directory /tmp/gt015-consultas`:10 aprobados,0 fallos/omitidos. Fuente sola403, base sola403, base+fuente habilita sólo su panel;21facturas con20+1 filas y total25.41constante. Situación actual sin período y rechazo400 comprobados; permisos012 originales conservados. Warnings anteriores; diff-check correcto.


## T036 — Grupos y referencias estabilizados

Distribuciones por identidades usan Count/Take10/Skip+SUM en SQL, orden métrica/Id numérico; sólo10 categorías y Otros se materializan para gráfico. Estados y tipos de movimiento tienen universo cerrado<=5, sin recorte. Grupos completos siguen paginados por identidad; null Sin asignar distinto de Id sintético otros. Referencias SQL mínimas y paginadas. Metadata por grupo declara403 uniforme de contrato015 sin cambiar cookie/políticas ni textos012.

Prueba SQL21 clientes/transportistas/tipos/unidades,22 categorías de transportista con null, facturas/cobros/liquidaciones/pagos reales y21+1viajes: top10+Otros reconcilia1231/279.51/231 y21viajes, grupos20+1sin duplicados y referencia20de21. `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GruposAnalisisTests|FullyQualifiedName~GT.IntegrationTests.Reportes' --logger 'trx;LogFileName=t036-grupos-sql.trx' --results-directory /tmp/gt015-consultas`:84 aprobados,0 fallos/omitidos. Tras metadata403, filtro 'FullyQualifiedName~PermisosAnalisisTests|FullyQualifiedName~GruposAnalisisTests', t036-metadata-contratos.trx:10 aprobados. Warnings previos, diff-check correcto.


## T037 — Dashboard autorizado y foto única

ConsultarDashboard captura instante; API evalúa IAuthorizationService por fuente antes de entregar conjunto autorizado al repositorio, sin roles. RepositorioAnalisisReportes.Dashboard ejecuta secuencialmente sólo agregados autorizados bajo un único coordinadorSnapshot; lectores de agregados extraídos de las páginas para no consultar detalle inútil. Ocho secciones tipadas, denegadas sin datos, documentos/flujos separados, rankingflota SQL y vencimientos con contexto actual. Error inesperado analisis_no_generado500 sin SQL ni foto parcial.

SQL real con DbCommandInterceptor: base sola no consulta tablas de negocio; base+Viajes consulta sólo su sección; fuente sola403. Snapshot comprobado en cada comando de negocio, sin consulta de páginas. Factura septiembre cobrada octubre y manual septiembre pagado octubre más OPP3OP reconcilian242documental/121cobros/363liquidado/413pagos, caja neto-5 y flota1. THROW50015 real en consultaCaja devuelve500sin secciones/cifras/SQL. Período vacío disponible/cero distinto de sinPermiso.

Comando `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~GT.IntegrationTests.Reportes' --logger 'trx;LogFileName=t037-dashboard.trx' --results-directory /tmp/gt015-consultas`:87 aprobados,0 fallos/omitidos. Corregido warningxUnit2029 del test nuevo sin cambiar aserción; cobertura ampliada con --filter 'FullyQualifiedName~DashboardAnalisisTests|FullyQualifiedName~ReadinessAnalisisOffTests', t037-dashboard-cobertura-final.trx:4 aprobados. OFF503 extendido a dashboard y3Vencimientos, legacy012200. Sin warnings nuevos; diff-check correcto.


## T038 — Foto concurrente sobre negocio real

Ocho carreras SQL con aplicaciones/bases independientes y barrera ReaderExecutedAsync entre SELECT: cobro006, pago manual009, anulaciónOPP014 posterior al rango y cambio de tipo004; cada uno en dashboard y consulta financiera/flota. Escritor confirma en otra conexión antes de liberar lector, sin sleeps ni locks económicos de Reportes. Cobro/pago/reversión ejecutan servicios/primitivas originales; tipo cambia por UPDATE de fixture válido.

`/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~ConcurrenciaFotoNegocioAnalisisTests' --logger 'trx;LogFileName=t038-concurrencia-negocio.trx' --results-directory /tmp/gt015-consultas`:8 aprobados,0 fallos/omitidos. Request actual conserva0/363 y tipo anterior, siguiente ve121/50/0 y nuevo tipo; detalle/grupos e importes documentales/flujo coherentes. Materialización antes de cerrar se conserva; la exportación futura se verificará en T059. Bases eliminadas por fixture en finally. Warnings previos.


## T039 — Matriz HTTP completa de consultas

Inventario de26endpoints reales contrastado con tabla de pruebas.18 perfiles de permisos (ninguno/base/emisión/fuentes aisladas/base+cadafuente/todas) recorren26rutas;26anon401 y26tras revocación403. Cada403 conserva texto uniforme y cero consultas de negocio instrumentadas. Gestiónflota/choferes/Bancos siguen403 aun con todas las lecturas015. Semillas/permisos012 y SnapshotOFF de todas las nuevas lecturas revalidados.

Primera compilación detectó sintaxis del inicializador del nuevo test, corregida a expresión de colección tipada sin cambiar casos. `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~MatrizRutasAnalisisTests|FullyQualifiedName~PermisosAnalisisTests|FullyQualifiedName~SemillasAnalisisTests|FullyQualifiedName~PermisosDeReporteTests|FullyQualifiedName~ReadinessAnalisisOffTests' --logger 'trx;LogFileName=t039-matriz-corregida.trx' --results-directory /tmp/gt015-consultas`:20 aprobados,0 fallos/omitidos. Matriz principal523requests (468perfiles+26anon+26revocación+3laterales), sin contar pruebas complementarias. Warnings previos.


## T040 — Dataset reproducible y entorno de medición

DatosMedicionAnalisis (semilla15040/año2026) prepara10000Viajes/5000Facturas/5000Liquidaciones/10000Movimientos sintéticos, relaciones documento-viaje y liquidación-viaje,4000OPmanuales, estados/IVA/historial/instantessintéticos válidos. Se comprobaron cuatro conteos exactos, netos de facturas vigentes contra viajes, totalesLQ contra vínculos y pagadoLQ contra OPvigentes. Sin datos reales ni nuevas dependencias.

Primera corrida pasó conteos/relaciones y falló una suposición nueva incorrecta del test: RCSI heredado estabaON. El TRX demuestra que el proveedor EF de la provisión original emite READ_COMMITTED_SNAPSHOT ON al crear la base, antes del opt-in015. Se corrige el test nuevo para exigir invariancia antes/después, sin alterar opción ni fixture original. Las referencias previas del log a «sinRCSI» significan que015 no lo activa ni lo usa como sustituto de Snapshot; no afirman que la provisión previa tengaRCSIOFF. Las transacciones de análisis siguen Snapshot explícito comprobado en T037/T038.

`/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~DatasetMedicionAnalisisTests' --logger 'trx;LogFileName=t040-dataset-corregida.trx' --results-directory /tmp/gt015-medicion`:1 aprobado,0 fallos/omitidos. Preparación68143ms (fuera de latencia de consulta). Metadata confirmó base eliminada; /tmp/gt015-medicion/dataset.json registra evidencia sin conexión/credenciales. SDK10.0.302/.NET10.0.10, SQL16.0.4265.3, Node24.22.0/npm11.19.1, Linux6.18.57-1-lts; Ryzen7 7730U,16procesadores, RAM14135640kB/memoriaGC14474895360bytes. SnapshotON/RCSIheredadoONconservado; sin límites CPU/RAM declarados en compose. Warnings previos; diff-check correcto.


## T041 — Consultas, tiempos y planes reales sin índices nuevos

Arnés HTTP con una sesión, dataset T040 ampliado a250unidades/200choferes y documentación sintética para comparar páginas reales20/200 en los diez detalles. Dos calentamientos separados y20muestras por cada uno de11recursos; todas las consultas de negocio instrumentadas usan Snapshot. Dashboard46consultas; Viajes10, Facturación6, Cobros7, Liquidaciones6, Pagos6, Caja4, Flota11 y cada Vencimientos1. Los diez detalles conservan el mismo número de consultas entre20y200filas; las lecturas pueden crecer con las filas, sin consultas por fila.

Primera corrida completó tiempos pero falló la captura de planes: STATISTICS XML devuelve un resultset Showplan que SqlClient identifica como nvarchar(max), no necesariamente tipo xml. Corregido únicamente el interceptor de medición para reconocer el nombre Showplan; sonda pequeña aprobada antes de repetir medición. Documentación primaria: https://learn.microsoft.com/en-us/sql/t-sql/statements/set-statistics-xml-transact-sql?view=fabric-sqldb .

Comandos: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~InstrumentacionPlanAnalisisTests --logger 'trx;LogFileName=t041-sonda-planes.trx' --results-directory /tmp/gt015-medicion` y misma suite con `--no-build --no-restore --filter FullyQualifiedName~MedicionAnalisisTests.BaselineIndicesExistentes --logger 'trx;LogFileName=t041-baseline-corregida.trx' --results-directory /tmp/gt015-medicion`. Sonda1aprobada y medición1aprobada,0fallos/omitidos; contadores TRX comprobados.

Máximos observados en ms: Dashboard416, Viajes135, Facturación48, Cobros42, Liquidaciones54, Pagos81, Caja43, Flota82, VencimientosChofer17/Flota13/Facturas15. Las220muestras están por debajo de5s; esta es línea base, no sustituye T083 posterior.64planes reales con IO guardados en /tmp/gt015-medicion/baseline/planes, tiempos.json y planes-io.json; sin credenciales ni filas de negocio en esa evidencia. finalizada.json confirma64planes; limpieza.json confirma base efímera eliminada. Primer intento preservado y fallo no ocultado. Warnings anteriores; no índices ni cambios de esquema.

## T042 — Decisión por candidato de índice

Se inspeccionaron planes reales/IO y SQL capturado, no sólo sugerencias del optimizador. FechaCobro: las páginas usan Clustered Index Scan de336lecturas lógicas, costo estimado0,293044 y sugerencia de índice; el recurso completo tarda como máximo42ms. Se descarta por ahora el candidato filtrado FechaCobro/Id: no hay incumplimiento ni scan costoso en el volumen objetivo que justifique otro índice mantenido por las escrituras006.

FechaPago: planes sin sugerencia de índice, costo0,193258,162lecturas para20filas y1242para200por joins/proyecciones;6consultas constantes y máximo81ms. Se descarta FechaPago/Id en esta entrega, evitando costo de mantenimiento adicional sobre pagos009/014 sin necesidad demostrada.

Conteo por vehículo: plan Flota con Hash Match/Clustered Index Scan, costo0,304624;302lecturas en20filas y1022en200, más documentación en lote84/10lecturas. Existe sugerencia pero máximo82ms y11consultas constantes. Se descartan ambas variantes VehiculoId/Fecha y Fecha/VehiculoId: el rango anual de aceptación selecciona actividad amplia y el agregado sobre10000viajes cumple holgadamente. No se afirma que las variantes sean equivalentes ni se inventa una mejora medida; no se crea ninguna preventivamente. Viajes generales usa índice existente con seeks en el detalle (181/1651lecturas para20/200), máximo135ms; no índice adicional. Liquidaciones/Caja tampoco muestran necesidad de nuevos índices.

La alternativa aceptada es conservar índices existentes y repetir medición final T083; si los cambios posteriores incumplen el objetivo se revisará evidencia antes de introducir un índice. Los costos del plan son estimaciones SQL, no tiempos observados. Fuentes aprobadas intactas.

## T043 — Sin cambio de esquema

Con la evidencia T041/T042 no se genera migración ni configuración de índices. Ninguna migración histórica ni snapshot EF alterados por015. Instalación vacía y upgrade conservador ya ejecutados T012/T013 sobre el esquema vigente; no hay nuevo esquema que aplicar en esta tarea. Se conserva habilitación administrada Snapshot separada de migraciones y del runtime. Validación documental contra planes/IO reales y revisión del diff de configuraciones/migraciones; sin modificaciones de esos archivos.


## T044 — Checkpoint backend previo a exportaciones

Build completo correcto:0errores/8warningsNU1902/NU1903 preexistentes.155unitariosReportes aprobados y1086integraciónSQL real aprobados,0fallos/omitidos, incluyendo Reportes completo (dataset/medición/Snapshot/matriz), Choferes/Flota/Viajes/Facturación/Liquidaciones/Caja/Proveedores/Pagos. El test temporal conocido009 está Passed en el TRX; aserción intacta, no se declara resuelta su intermitencia histórica.

Comandos `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo`; `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Reportes' --logger 'trx;LogFileName=t044-unit-reportes.trx' --results-directory /tmp/gt015-checkpoint`; integración mismo comando sobre GT.IntegrationTests con filtro 'FullyQualifiedName~Reportes|FullyQualifiedName~Choferes|FullyQualifiedName~Flota|FullyQualifiedName~Viajes|FullyQualifiedName~Facturacion|FullyQualifiedName~Liquidaciones|FullyQualifiedName~Caja|FullyQualifiedName~Proveedores|FullyQualifiedName~Pagos', archivo t044-sql-regresion.trx. RegresiónSQL duró3m36s. Contratos/filtros/lecturas/readiness estabilizados; sin frontend ni exportaciones nuevas antes de esta puerta.


## T045 — Punto de comparación de exportaciones012

Antes de tocar armadores/fuentes:38pruebas SQL originales012 aprobadas,12frontend GenerarReporte aprobadas. Los155unitariosReportes del checkpoint inmediato T044 incluyen toda la suite012 y no hubo cambio ejecutable entre ambas tareas; se conserva esa corrida sin duplicarla. Comando SQL sobre GT.IntegrationTests --no-build --no-restore con filtro 'FullyQualifiedName~Reportes.ArmadoresTests|FullyQualifiedName~Reportes.ErroresDeReporteTests|FullyQualifiedName~Reportes.PermisosDeReporteTests|FullyQualifiedName~Reportes.ReporteMovimientosCajaTests|FullyQualifiedName~Reportes.ReporteViajesTests|FullyQualifiedName~Reportes.ReportesDeVencimientosTests', --logger 'trx;LogFileName=t045-sql012.trx' --results-directory /tmp/gt015-exportacion. Frontend `npm test -- src/modules/reportes/componentes/GenerarReporte.test.tsx`.0fallos/omitidos, sin modificar aserciones ni archivos de tests originales.

## T046 — Status de precisión monetaria XLSX

Inspeccionados RespuestasDeReporte, Mensajes/CodigosErrorReporte, AGENTS[005]/[012] y contrato015. Se conserva409 precision_monetaria_xlsx: la petición/filtros son válidos y consulta/PDF funcionan, pero el estado actual de las cifras impide emitir Excel sin pérdida. Es la misma clasificación012 de409tope_de_filas_superado; no se introduce422 por un dato tipeado inválido ni handshake de confirmación. El422desbordamiento de análisis sigue siendo otra condición.

Escenarios T052–T055: celda intermedia pierde centavo, sólo total pierde, ambos rechazan archivo completo antes de bytes/Content-Disposition con409y mensaje único aprobado; importes ordinarios y grande representable generan Number; PDF disponible; formato400/tope409/permisos403/motor500 preservados. Validación ejecutable del status pendiente T055 por dependencia explícita, sin publicar handler provisional. No decisión funcional nueva.


## T047 — Armado con instante de foto y cantidades

Sobrecarga aditiva de ArmadoDeReporte recibe DateTimeOffset capturado; encabezado y nombre usan ese instante sin releer reloj. Firma previa delega conservando su reloj/comportamiento. TotalDeReporte.Cantidad distingue pie entero del monetario sin cambiar constructor existente; PDF muestra entero y XLSX Number con formato#,##0 (dinero conserva#,##0.00). Mismo modelo neutro para ambos motores.

Unitario con reloj que arroja excepción prueba que render no lo consulta y conserva filas/autor/filtros/nombre argentino en cambio de día; libro real por DI separa cantidad21 de importe123.45 y genera PDF real. `/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~Reportes --logger 'trx;LogFileName=t047-unit.trx' --results-directory /tmp/gt015-exportacion`:156aprobados. Integración mismo comando/proyectoGT.IntegrationTests, filtro 'FullyQualifiedName~ResumenEnteroReporteTests|FullyQualifiedName~Reportes.ArmadoresTests', t047-armadores.trx:6aprobados.0fallos/omitidos, warnings anteriores; tests originales intactos.


## T048 — Fuentes analíticas Viajes y Caja

FuentesActividadAnalisis traduce DTOs completos al único ReporteTabular; comparte las mismas columnas de fuentes012 (visibilidad internal aditiva), sin modificar sus filas ni contratos. IExportacionAnalisis expone sólo fuentes implementadas; repositorio parcial comparte predicados/proyecciones/agregados privados dentro de Snapshot único. Filtros resuelven nombres actuales en esa foto; conserva día/instante/autor, estado visible y referenciasOP/OPP. No endpoints provisionales.

Count/tope antes de filas se introduce como fundamento necesario para no permitir un modelo parcial; T057 lo validará exhaustivamente para todas las fuentes. OpcionesDeReporte sigue siendo el único tope5000 inyectable. Pagina del navegador se ignora al materializar; render no ocurre dentro del repositorio/transacción. Viajes incluye resumen de cantidades y total vigente rotulado excluyendo anulados; Caja movimiento neto, no saldo.

Build completo correcto0errores/14warnings anteriores. Unitarios fuente+legacy28aprobados (t048-unit.trx); integración14aprobados (t048-sql.trx),0fallos/omitidos. Comandos dotnet test con --no-restore; unitarios filtro 'FullyQualifiedName~FuentesActividadAnalisisTests|FullyQualifiedName~FuenteReporteViajesTests|FullyQualifiedName~FuenteReporteMovimientosCajaTests'; integración filtro 'FullyQualifiedName~ExportacionActividadAnalisisTests|FullyQualifiedName~Reportes.ReporteViajesTests|FullyQualifiedName~Reportes.ReporteMovimientosCajaTests', --results-directory /tmp/gt015-exportacion y logger trx con esos nombres.21filas desdePagina2se exportan21 en ordenSQL, inactivo/nombre, columnas10/6idénticas; Caja21egresos-21.21 en borde argentino y movimiento de noviembre excluido. DbContext sin transacción al devolver modelo. diff-check correcto.


## T049 — Fuentes Facturación documental y Cobros

FuentesFacturacionAnalisis y ExportacionFacturacion: modelos separados por FechaFactura y FechaCobro, misma consulta privada de análisis, count previo, conjunto completo y nombres de filtro resueltos en Snapshot. Filas documentales conservan snapshot/Neto/IVA/Total persistidos/estado visible/FechaCobro; resumen vigente excluye anuladas incluso al consultarlas explícitamente. Cobros no restringe FechaFactura ni resta deuda documental. Cabecera identifica criterio de cada perspectiva; no nuevas rutas aún.

Unitario1aprobado fija columnas monetarias, estado y fecha nula; SQL4aprobados incluyen fuentes y consultas Facturación/Cobros. Prueba real septiembre→octubre, cliente inactivo/renombrado con snapshot, factura anulada/refacturada sin doble venta, totalesNeto/IVA y universo completo desdePagina2. Anulada muestra nominal y todos los totales financieros cero. Comandos dotnet test --no-restore, unitarios filtro FullyQualifiedName~FuentesFacturacionAnalisisTests, t049-unit.trx; SQL filtro 'FullyQualifiedName~ExportacionFacturacionAnalisisTests|FullyQualifiedName~FacturacionAnalisisTests|FullyQualifiedName~CobrosAnalisisTests', t049-sql.trx, directorio /tmp/gt015-exportacion.0fallos/omitidos, warnings anteriores.


## T050 — Fuentes Liquidaciones documental y Pagos

FuentesLiquidacionesAnalisis y parcial ExportacionLiquidaciones reutilizan consultas privadas estables bajo Snapshot y count previo. Documental rotula meses completos (inicial/final) sin prorrateo, pagado/deuda actuales y anulación sin deuda; flujoFechaPago días exactos de cualquier mes liquidado. Una fila porOP, origenManual/OPP, referenciaOPP, vigencia y reversión con autor/instante argentino; resumen vigente separado del importe revertido histórico, sin sumarOPPotra vez. Copia declarada de tres palabras del mapa real servicioLiquidaciones; sólo comentario de referencia agregado al mapaTS, sin comportamiento frontend.

Unitarios4aprobados, fijan palabras y una OP revertida con importe/fecha/autor conservados y cero vigente. SQL4aprobados: septiembre completo dentro15/09–10/10, manualparcial50 y tresOPdeOPP363, reversión real posterior conserva4filas/50vigente/363histórico; rango corto excluye pago18/10. Comandos dotnet test --no-restore, filtro unitario FullyQualifiedName~FuentesLiquidacionesAnalisisTests (t050-unit.trx); SQL 'FullyQualifiedName~ExportacionLiquidacionesAnalisisTests|FullyQualifiedName~LiquidacionesAnalisisTests|FullyQualifiedName~PagosAnalisisTests' (t050-sql.trx), /tmp/gt015-exportacion.0fallos/omitidos, warnings anteriores.


## T051 — Fuente Flota y adaptadores Vencimientos

FuenteAnalisisFlota usa fila/ranking del análisis completo con ceros/bajas, nombres y disponibilidad actuales, cantidades enteras y sinvehículo global rotulado fuera del filtro de unidades; sin km/horas/porcentajes. Copia declarada de las dos palabras del mapa TEXTO_ESTADO_VEHICULO real, con comentario espejoTS sin lógica nueva.

Vencimientos comparte literalmente Columnas/Fila de las tres fuentes012 mediante visibilidadinternal; no copia estados ni urgencias ni incorpora adjuntos/cheques. Mismo hoy capturado y cabecera situación actual sin corte histórico. Adaptadores todavía sin endpoint; T057 completará countSQL previo en estos paneles antes de publicar exportación, porque sus consultas originales materializan candidatos. Tope actual evita modelo/archivo parcial, pero no se declara aún validado count antes de materializar para los paneles.

Unitarios22aprobados (Flota y fuentesVencimientos), SQL14aprobados (nuevasfuentes, reportesVencimientos012 y consultasFlota/Vencimientos).21unidades/rankingreconciliado/19ceros/baja con actividad, sinvehículo separado; JSON del modelo neutro compara exactamente filas/columnas/totales de los3paneles originales por DI. Comandos dotnet test --no-restore, filtros 'FullyQualifiedName~FuenteFlotaAnalisisTests|FullyQualifiedName~FuentesDeVencimientosTests' y 'FullyQualifiedName~ExportacionFlotaVencimientosTests|FullyQualifiedName~Reportes.ReportesDeVencimientosTests|FullyQualifiedName~FlotaAnalisisTests|FullyQualifiedName~VencimientosAnalisisTests', logs t051-unit.trx/t051-sql.trx en /tmp/gt015-exportacion.0fallos/omitidos, warnings anteriores y diff-check correcto.


## T052 — Comparación monetaria de lectura XLSX

VerificadorMonetarioXlsx internal interpreta double recuperado mediante representaciónR invariante y decimal.TryParse con exponente; compara decimal.Round(lectura,2,AwayFromZero)==fuente, exclusivamente escala de comparación. Fuente exacta no se modifica; rechaza fuente con más de2decimales, no finitos y overflow. Sin epsilon/castdecimaldouble que reduzca dígitos ni identidad binaria. Excepción tipadaApplication sin valores sensibles ni coordenadas en mensaje. Visibilidad de internals sólo para GT.UnitTests, sin exponer double al dominio.

Primera corrida17aprobados/1fallo detectó clasificación incorrecta de fixture:70368744177664.01 ya pierde centavo. Se conserva caso como rechazo y agrega35184372088832.01 representable; no cambia algoritmo ni se elimina frontera. Corrida corregida19aprobados,0fallos/omitidos:0/0.01/1.10/ordinarios/negativos, grande1e15 y suma1e16 representables, máximodecimal(18,2)/sumasconcentavo/fronteras rechazadas; NaN/infinito/overflow/fuenteescala3rechazados. Comando dotnet test GT.UnitTests --no-restore --filter FullyQualifiedName~VerificadorMonetarioXlsxTests --logger 'trx;LogFileName=t052-unit-corregida.trx' --results-directory /tmp/gt015-exportacion. PrimerTRX t052-unit.trx conservado; warnings anteriores.


## T053 — Escritura numérica y manifest monetario

ArmadorClosedXML prepara libro y manifest interno de hoja/fila/columna/decimalfuente en el mismo punto de escritura. Toda columnaImporte no nula y totalmonetario queda registrado; cantidades/nulos quedan fuera. Valores asignados como decimal a Number con#,##0.00, sin cálculo financiero en double ni texto. Preparar dispone el libro si falla; manifest sólo memoria.

Unitarios20aprobados (19verificador+manifest multicolumna): seis coordenadas únicas con positivos/negativo/cero/totales, Number/formato, fuente exacta, ausencia de nulo/resumen entero. Comando dotnet test GT.UnitTests --no-restore --filter 'FullyQualifiedName~ManifestMonetarioXlsxTests|FullyQualifiedName~VerificadorMonetarioXlsxTests' --logger 'trx;LogFileName=t053-manifest.trx' --results-directory /tmp/gt015-exportacion.0fallos/omitidos, warnings anteriores. Validación de bytes reales/reapertura corresponde T054 siguiente.


## T054 — Guardar, reabrir y verificar antes de entregar

ArmadorClosedXML guarda libro completo a memoria, reabre exactamente esos bytes y recorre cada coordenada del manifest (Number+comparación monetaria). Sólo después devuelve los bytes originales. Una discrepancia descarta todo con PrecisionMonetariaXlsxException, sin envolverla como fallo genérico; demás errores conservan ReporteNoGeneradoException. Libros/streams liberados; ninguna escritura persistida.

10pruebas SQL/armadores porDI aprobadas,0fallos/omitidos: ordinarios1.10/negativo-0.01/grande1e15/nulo, pérdida en celda intermedia, sólo total máximo y suma mayor que fila; ninguna entrega parcial/byte de éxito y fuente decimal intacta, PDF real disponible en los3rechazos. Regresión armadores012 y cantidadentera incluye reapertura. Comando dotnet test GT.IntegrationTests --no-restore --filter 'FullyQualifiedName~PrecisionLibroRealTests|FullyQualifiedName~Reportes.ArmadoresTests|FullyQualifiedName~ResumenEnteroReporteTests' --logger 'trx;LogFileName=t054-libro-real.trx' --results-directory /tmp/gt015-exportacion. Warnings anteriores; traducciónHTTP se completa inmediatamente enT055.


## T055 — Error409 tipado antes de entregar archivo

Codigos/Mensajes centralizan precision_monetaria_xlsx. Handler012 captura excepción antes de ReporteNoGenerado; traductor015 antes del catch genérico (también tope y motor preservados). Rechazo409 sólo{codigo,mensaje}, sin cifras/SQL/Content-Disposition de éxito. Servicio frontend reconoce resultado de precisión y componente existente muestra mensaje servidor y vuelve a habilitar generación; no handshake, texto recompuesto ni pantallas nuevas.

13unitarios de traducción/respuestas,22SQL/HTTP de precisión y regresión012(formato/tope/permisos/motor/Viajes),25frontend en3archivos aprobados;0fallos/omitidos. CasoHTTP real con importe9999999999999999.99: Excel409JSON sin archivo; consulta200/PDF200. Nuevas rutas se comprobarán al publicarlasT058; traducción015 ya ejecutada por IResult real en unitario. Comandos dotnet test --no-restore: unitario filtro 'FullyQualifiedName~PrecisionRespuestaTests|FullyQualifiedName~RespuestasDeReporteTests', t055-unit.trx; SQL filtro 'FullyQualifiedName~PrecisionHttpLegacyTests|FullyQualifiedName~ErroresDeReporteTests|FullyQualifiedName~Reportes.PermisosDeReporteTests|FullyQualifiedName~ReporteViajesTests', t055-http.trx; /tmp/gt015-exportacion. Frontend `npm test -- src/modules/reportes src/compartido/archivos.test.ts`. Decisión409deT046validada ejecutablemente; warnings anteriores.


## T056 — PDF real de las diez perspectivas

EmitirAnalisisReporte captura reloj una vez, materializa fuente y entrega modelo a ArmadoDeReporte después de cerrarSnapshot; sobrecarga aditiva recibe modelo ya construido. Nuevos prefijos de archivo para Facturación/Cobros/Liquidaciones/Pagos/Utilización sin renumerar los cinco tipos012. Reutilización del motorQuestPDF único, sin gráficos obligatorios ni almacenamiento.

Prueba porDI genera7perspectivas temporales+3paneles con datos sintéticos reales y comprueba ausencia de transacción antes de devolver bytesPDF. Diez muestras en /tmp/gt015-exportacion/pdf; pdftotext verificó autor/filas/contexto y pdftoppm+montage permitió inspección visual de todas. Se detectó fecha de pago partida en tabla11columnas; anchoPDF opcional aditivo porcolumna, aplicado sólo a Pagos, corrigió fecha y encabezadoLiquidación sin modificar columnas/datos/armadoreslegacy. Capturas pagos-final.png/revision.png temporales. Pillow ausente: se usaron herramientas instaladas, sin dependencias nuevas.

Corridas t056-pdf.trx1aprobado, t056-pdf-ajuste.trx6aprobados (incluye armadores012), t056-pdf-final.trx1aprobado después del último ancho. Comandos dotnet test GT.IntegrationTests --no-restore --filter FullyQualifiedName~PdfAnalisisTests (en ajuste agrega '|FullyQualifiedName~Reportes.ArmadoresTests'), logger con esos nombres en /tmp/gt015-exportacion. Revisión final abrió PNG1500/1400 y confirmó3fechas18/10/2026 enteras, títulos/filtros/autor/horaArgentina/cantidad/ARS/resúmenes legibles.0fallos/omitidos, warnings anteriores. No aprobación humana supuesta.


## T057 — Tope completo antes de materializar

Todas las fuentes temporales hacenCount bajo la misma Snapshot antes de detalle/modelo; los3paneles ahora también. Se extraen literalmente predicados actuales de vigentes de repos003/004 y pendientes que vencen006 como helpers internos Infrastructure (sin IQueryable en Application); métodos originales siguen usando el mismo árbol. Count de alertas traduce DateDiffDay<=díasAviso y pruebas lo cotejan con evaluación original en vencido/hoy/borde7/fuera8/fuera30. Sin reglas nuevas ni cambios de escritura.

5000filas reales admitidas completas desdePagina2/tamaño1, importe50 exacto;5001rechazadas con única consultaCountdeViajes y transacciónSnapshot comprobada, sin filas/modelo parcial. Vacío produce modelo vacío como012 (UI futura deshabilita exportación). Paneles con tope inyectado2 rechazan3antes de datos; fuente normal3conserva universo.6SQLpruebas aprobadas en t057-tope.trx;2unitarios traducciónAPI tipada409filas5001/tope5000 sinContent-Disposition en t057-traduccion.trx. HTTP final sobre rutas reales se amplía T058 al publicarlas, sin handler provisional.

Regresión completa003/004/006+Vencimientos012:382aprobadas,0fallos/omitidos, t057-regresion-fuentes.trx. Comandos dotnet test GT.IntegrationTests --no-restore filtro 'FullyQualifiedName~TopeExportacionAnalisisTests|FullyQualifiedName~ExportacionFlotaVencimientosTests|FullyQualifiedName~VencimientosAnalisisTests'; unitario filtro FullyQualifiedName~PrecisionRespuestaTests; regresión --no-build --no-restore filtro 'FullyQualifiedName~GT.IntegrationTests.Choferes|FullyQualifiedName~GT.IntegrationTests.Flota|FullyQualifiedName~GT.IntegrationTests.Facturacion|FullyQualifiedName~Reportes.ReportesDeVencimientosTests'; logs en /tmp/gt015-exportacion. Warnings anteriores; sin transacción al renderizar y diff-check correcto.


## T058 — Diez rutas de exportación publicadas con contratos estables

AnalisisReportesEndpoints.Exportacion agrega siete recursos temporales y tres paneles, ambos formatos y filtros compartidos con consulta. Políticas declarativas base+fuente+emitir antes de handler, no rol; permisos/texto de012 intactos. Vencimientos rechaza período histórico; formato obligatorio pdf/excel. Cache-Control no-store/nosniff, PDFinline/XLSXattachment, filename backend y error tipado antes de cabecera exitosa. No exportación dashboard.

Primera corrida7aprobados/2fallos de tests nuevos: importe extremo de otro caso contaminaba el conjunto sin filtro del test de formatos y esperaba punto de miles cuando el formatterlegacy producecoma según cultura. Se aísla test de formatos en período vacío2025 y se verifica cantidad5000normalizada más5000filasNumber/fin de tabla, preservando formatter anterior sin cambiar producción por el test.

Corrida corregida9aprobados,0fallos/omitidos (t058-rutas-corregida.trx; primer t058-rutas.trx conservado).20descargas de10rutas/2formatos con cabeceras/tipos/instante/autor/contextos;21filas completas desdePagina2/tamaño1, precisión409sinarchivo/PDF200, permisos independientes403, diezOFF503/legacy200 y5000HTTPadmitidas/5001HTTP409con números. Comando dotnet test GT.IntegrationTests --no-restore --filter 'FullyQualifiedName~RutasExportacionAnalisisTests|FullyQualifiedName~ReadinessAnalisisOffTests|FullyQualifiedName~TopeExportacionAnalisisTests' --logger 'trx;LogFileName=t058-rutas-corregida.trx' --results-directory /tmp/gt015-exportacion. Warnings anteriores; no caso de uso provisional.


## T059 — Permisos completos, no mutaciones y foto de exportación

Inventario real de10rutas cotejado;21perfiles por10recursos/2formatos,20anon401,20revocados403 y3laterales403:463requests. Base/fuente/emisión independientes, cada fuente faltante y cada fuente aislada; rechazo uniforme sin archivo ni consultas de negocio. Ningún rol en endpoint. Matriz de26lecturas anterior conserva su cobertura separada y excluía exportaciones previstas.

Dos carreras reales sin sleeps: cobro confirmado entreCountyfilas mantiene archivo anterior vacío y siguiente1fila121; cobro confirmado después del modelo y antes de renderPDF deja estado/deuda anterior en ese modelo, siguientePagada/deuda0. Wrapper de observación porDI comprueba ausencia de transacción al llamar al motorQuestPDF original. Barreras/conexiones/scopes separados, tiempos límite explícitos.

Comparación exacta en memoria de todas las tablas mapeadas de Viajes/Facturación/Liquidaciones/Caja/Pagos/Proveedores/Flota/Choferes (columnas+PK+historia) antes/después de20descargas: idénticas; volumen de archivos idéntico. No dumps/credenciales/usuarios en esa captura, no auditoría ni archivos persistidos nuevos. SQLFORJSON de datos sintéticos sólo en memoria del test.

4pruebas SQL/HTTP aprobadas,0fallos/omitidos, t059-permisos-foto.trx. Comando dotnet test GT.IntegrationTests --no-restore --filter 'FullyQualifiedName~PermisosExportacionAnalisisTests|FullyQualifiedName~ConcurrenciaExportacionAnalisisTests|FullyQualifiedName~SinMutacionesExportacionTests' --logger 'trx;LogFileName=t059-permisos-foto.trx' --results-directory /tmp/gt015-exportacion. Warnings anteriores.


## T060 — Checkpoint exportaciones/compatibilidad012

Build completo0errores/12warnings anteriores (Pkcs y analizadores de tests originales).188unitariosReportes,127integraciónSQLReportes completa y25frontend/3archivos aprobados,0fallos/omitidos. Suite SQL incluye38originales012, Snapshot/permisos/concurrencia/no mutaciones, límites y guardia nueva; fronteras adicionales Viajes/VencimientosFacturas/Caja con celdas representables y total que pierde centavo devuelven409/PDFdisponible. No aserciones previas debilitadas. Regresión completa003/004/006 de382pruebas inmediatamente anteriorT057 continúa válida: no código ejecutable posterior de esos módulos;009/011/013/014 validadasT044, cambios posteriores son lectura Reportes/armadores.

Comandos `/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo`; dotnet test GT.UnitTests --no-build --no-restore --filter FullyQualifiedName~Reportes (t060-unit-reportes.trx); integración mismo comando con filtro FullyQualifiedName~GT.IntegrationTests.Reportes (t060-sql-reportes.trx); loggertrx en /tmp/gt015-exportacion. Frontend `npm test -- src/modules/reportes src/compartido/archivos.test.ts`. SQL1m57s. Fuentes aprobadas spec/plan/research/modelo/contratos hashes intactos,91IDsúnicos/T001–T059cerradas antes de este checkpoint y gitdiff--check correcto. Copia temporal de planes previos en /tmp/gt015-medicion/previo-checkpoint-exportacion preserva evidencia antes de repetir harness en suite. T060completa; recién ahora se habilita fase4.


## T061 — Baseline de bundle antes de gráficos

Puertas backend/exportaciónT044/T060cerradas. `npm run build` desdefrontend aprobótsc-b/Vite8.2.0 sin Recharts instalado. Nodev24.22.0/npm11.19.1/React19.2.8; package/lock hashes iniciales y bytes exactos en /tmp/gt015-frontend/bundle-baseline.json. JSinicial752814bytes/gzip191683bytes; CSS71926bytes/gzip26522bytes, gzipnivel6 consistente para comparación futura. Vite informa752.81kB/193.03kB y conserva warning previo>500kB. Sin instalación ni otra dependencia modificada hasta esta medición.


## T062 — Recharts estable y peers verificados antes de instalar

Metadatos oficiales npm: latest3.10.1, canary3.11.0-canary.6separado; se fija exactamente3.10.1 estable. Peersreact/react-dom/react-is admiten^19.0.0 y engineNode>=18, compatibles con React/ReactDOM19.2.8/Node24.22.0 instalados. README oficial exige react-is igual a React;19.2.8 existe en registro. npm ls confirmó TypeScript resuelto6.0.3 (rango del proyecto6.0.2, sin cambiarlo); no peer/restricción TypeScript excluyente declarada. Compilación/render reales de la combinación se validan inmediatamenteT063, sin afirmar ejecución previa. No bloqueo de compatibilidad detectado.

Fuentes primarias: [package3.10.1](https://raw.githubusercontent.com/recharts/recharts/v3.10.1/package.json), [README3.10.1](https://raw.githubusercontent.com/recharts/recharts/v3.10.1/README.md), npm view recharts dist-tags / recharts@3.10.1 version peerDependencies engines / react-is@19.2.8 version engines --json. Consulta npm sandbox fallóEAI_AGAIN; reejecutada fuera con permisos y timeout10s/reintentos0, resultados comprobados. Sin instalación previa ni canary. Recharts incorpora dependencias internas como react-redux; no se agrega storeRedux ni dependencia de estado propia de la aplicación.


## T063 — Instalación reproducible y compatibilidad real

Sólo dependencias directas nuevas recharts3.10.1/react-is19.2.8 exactas;37entradaslock añadidas, única versión existente cambiada react-israíz17.0.2→19.2.8 porpeer aprobado, sin actualizar otras versiones previas. Dependencias internas de biblioteca, sin storeRedux propio. Primera ejecución fuera del sandbox encontróNode22.23.3/npm12.2.0yEBADENGINE del proyecto, sin modificar manifiestos. Reejecutada con PATH/ruta absoluta /home/joaco/.nvm/versions/node/v24.22.0/bin/npm: instalación correcta con Node24/npm11; npmci posterior desde lock también correcto (209paquetes).

Comandos instalación `npm install --save-exact recharts@3.10.1 react-is@19.2.8 --fetch-retries=0 --fetch-timeout=20000` con rutaNode24explícita fuera del sandbox; comprobación `npm ci --no-audit --fetch-retries=0 --fetch-timeout=20000` misma ruta. Trasci: `npm test -- src/modules/reportes src/compartido/archivos.test.ts`26aprobados/4archivos, incluye renderSVGaccesible real de Recharts con React19/TS6 sinmock. `npm run build`/typecheck y `npm run lint` correctos. Versiones reales React/DOM/Is19.2.8, Recharts3.10.1, TypeScript6.0.3. No bloqueo de compatibilidad; no canary.

Bundle inicial sigueJS752814/CSS71926bytes, mismos hashes, porque sólo prueba importa gráficos todavía; no se afirma costo final cero. /tmp/gt015-frontend/bundle-instalacion.json registra bytes/gzip6; warning previoVite>500kB. npm informó5alertas: auditJSON sólolectura identificó @vitest/mocker4.1.10/source-map-js1.2.1/undici7.29.0/vite8.2.0/vitest4.1.10, todas exactamente en lockanterior. Sin auditfix ni actualización ajena; evidencia temporal audit-instalacion.json. diff-check correcto.


## T064 — Tipos y cliente de análisis exacto

TiposAnalisis localiza los diez contratos, dashboard, grupos/referencias, estados/contextos, OP/OPP/reversión y atributos actuales. Vencimientos reuse tipos originales de Choferes/Flota y Omit de Total numérico deFactura sólo en contrato015; no cambia DTOlegacy. Servicio usa peticion compartido/cookies/401, filtros cerrados por recurso/dimensión, queryencode sin perderfalse, fechasISO sin conversión. Respuestas monetarias string intactas; parserCentavosBigInt firmado sin tope de formulario ni Number financiero. Errores015 incorporados al tipo común y campos múltiples conservados en ErrorAnalisis.

17pruebas cliente/parsing aprobadas: máximos/sumas mayores quefila/negativos, diez rutas, ocho referencias mínimas, grupos anidados, cookiesGET,400campos/503/401señal, sin Storage.setItem/history.pushState; canónicos/no canónicos. Build/tsc-b correcto; warningViteprevio. Primer comando npmtest desde raíz fallóENOENT antes de ejecutar tests; corregido cwdfrontend, sin cambios de aserciones por ese error. Comandos desde frontend `npm test -- src/modules/reportes/servicios/servicioAnalisis.test.ts`, `npm run build`. Sin DTO en storage/history ni suma autoritativa frontend.


## T065 — Secuencia de consultas, sesión y borrador

useSolicitudAnalisis asocia respuesta con clave/sesión/revisión, ignora éxito/error de secuencias anteriores y oculta datos de otra identidad ya en el render anterior al efecto. Cambio de consulta/sesión limpia resultado en memoria; cleanup impide publicar al desmontar/deshabilitar. Reintento tiene identidad nueva. Loaderref actualizado en layoutEffect evita reconsultar por identidad incidental de función y captura su función por secuencia. Sin storage/history ni cambio del clienteHTTP compartido. useFiltrosAnalisis separa borrador/aplicados con copias independientes.

7pruebas deterministas aprobadas: BantesdeA con éxito/errorA, cambio página/sección, sesiónnull/otro usuario,401/reintento, deshabilitar/desmontar y borrador sinaplicar/restablecer. Build/tsc-b correcto; avisoViteprevio. Comandos `npm --prefix frontend test -- src/modules/reportes/servicios/useSolicitudAnalisis.test.tsx` y build desde frontend. Primer comando invocado desde raíz sinprefix fallóENOENT antes de tests; se corrigió a prefix explícito para evitar repetir el error de cwd. No sleeps ni timers falseados.


## T066 — Período, KPI, contexto y estados comunes

SelectorPeriodo controla dates nativos/required/labels/ayuda/error/describedby/foco de controles compartidos; errores tras blur o submit, no al abrir. MesActualArgentina aplica−03independiente de zona del navegador; validador calendario no normaliza fechas inválidas y admite extremosDateOnly. KPI distingue importe string/cantidad/sinPermiso, sin Number monetario ni interactividad falsa. Resumen muestra criterio, días/meses completos/hoy/instanteArgentina y vigencia actual. Carga/vacío/error/403 separados y reintento sólo cuando corresponde.

EstadoVacio gana opción aditiva anuncioEnMensaje para situar rolealert/status en el párrafo según[008]; default conserva DIVlegacy, probado. No tokens globales ni reglas visuales inventadas. Copia del helper de instante movida a servicios tras warningFastRefresh nuevo; lintfinal limpio.

28pruebas/3archivos aprobadas (10componentes+GenerarReportelegacy+Encabezado), luego10componentes revalidadas tras moverhelper. Incluye bordeUTCnoviembre→octubreArgentina, bisiesto/extremos/invertido/requerido sin consulta, dinero máximo/negativo exacto, sin permiso distinto de0, contexto y anuncios enP/compatibilidadDIV. `npm test -- src/modules/reportes/componentes/ComponentesAnalisis.test.tsx src/modules/reportes/componentes/GenerarReporte.test.tsx src/compartido/ui`; última sóloComponentesAnalisis. Build/tsc-b/lint correctos, warningViteprevio. Ninguna primaria de lectura agregada.


## T067 — Gráficos lazy con tabla exacta

GraficoAccesible carga Recharts medianteimportdinámico, tabla independiente del lazy/errorboundary. Título/unidad/leyenda, patrones de línea nombrados, botón nativo para tabla y acceso a categorías completas/recorte declarado porcaller; ninguna interpretación requiere color. Sólo tokens existentes brand/ink-soft/danger-text. Ceros conservan categorías y explicación, fallo de dibujo esalertsin convertirlo a0. Sin filas con tabIndex.

Coordenadas monetarias se escalan desdecentavosBigInt, Number sólo después de acotar coordenada; no suma/total financiero ni mutación del original. Eje declaraescala aproximada; tooltip y tabla usan cadena original/formatearPesos incluso máximo/negativo y agregados mayores. Cantidades Number sólo enteros seguros no negativos. Recharts accessibilityLayer activo y animación desactivada.

9pruebas/4archivos aprobadas: título/unidad/leyenda/recorte, tecladoEnter/tabla, lazy pendiente con tabla operable, error de render con tabla intacta, cero, coords acotadas sin tocarfuente, tooltip máximo/negativo exacto y render realReact19. Wrapperlazy se simula para carreras/fallos; CompatibilidadGraficos prueba biblioteca real sinmock. `npm test -- src/modules/reportes/componentes/GraficoAccesible.test.tsx src/modules/reportes/componentes/TooltipExacto.test.tsx src/modules/reportes/servicios/coordenadasGrafico.test.ts src/modules/reportes/componentes/CompatibilidadGraficos.test.tsx`; build/tsc-b/lint correctos, avisoViteprevio. Inspección visual/teclado real y network se completanT077/T078, sin presumirlos.


## T068 — Exportación frontend aditiva

GenerarReporte y servicioReportes conservan cinco usos legacy y agregan diez perspectivas analíticas mediante mapa tipado. Filtros aplicados inmutables, false preservado y paginación excluida del archivo completo; PDF/Excel/Cancelar y error de precisión reutilizan diálogo/descarga existentes, sin otra implementación. Sin permiso de emisión no se dibuja acción; sin filas queda deshabilitada.

31 pruebas focalizadas en5archivos aprobadas: GenerarReporte/GenerarAnalisis/servicioReportes/servicioExportacionAnalisis/archivos. Incluyen foco inicial y retorno, cancelar sin request, filtros/página intactos,10rutas nuevas, legacy,409precisión/tope sin descarga. La salida de una corrida previa se perdió al compactar contexto y su proceso ya no existía: se repitió sólo este conjunto focalizado para recuperar evidencia comprobable. Build/tsc-b/lint correctos por cambio del contrato compartido; avisoViteprevio,JS753,54kB. No suites completas.

Se incorpora la optimización de frecuencia indicada por el usuario: duranteT067–078 sólo pruebas específicas y regresión inmediata, conservando íntegramente tareas/checkpointsT079/T081/T082 y aceptación/performance posteriores.


## T069 — Dashboard

Dashboard.tsx ofrece período borrador/aplicado, indicadores antes de gráficos y accesos al final. Respuesta del servidor gobierna secciones disponibles/sinPermiso; fallo de foto no dibuja cifras. Distingue documentos y flujos, pendiente vigente y movimiento neto; vencimientos rotulados actuales sin período en acceso. Importes originales exactos, gráficos lazy y tabla independiente; período conservado en enlaces. No exportación artificial del dashboard ni integración de rutas prematura.

4 pruebas focalizadas aprobadas: base aislada sin cifras/enlaces sensibles, sinbase sin consulta y fallo sin KPI/cero, borde UTC/Argentina y aplicar sin consultar borrador, dinero máximo negativo/vencimientos actuales. `npm --prefix frontend test -- src/modules/reportes/paginas/Dashboard.test.tsx`; `frontend/node_modules/.bin/tsc -b frontend` correcto por composición nueva de contratos. Sin suites completas.


## T070 — Viajes analíticos

ViajesReporte conserva diez columnas y palabras de estado originales; KPI/serie y distribuciones del servidor, realizados/rendición/Pendiente explicados sin reglas financieras cliente. ConsultaTemporal separa filtros borrador/aplicados, página y contexto; usa período de enlace y referencias mínimas paginadas con teclado/inactivos. TablaAnalisis conserva orden recibido y paginación20; DistribucionReporte permite categorías completas paginadas por endpoint, sin mezclar null y Otros. Exportación usa conjunto completo aplicado; enlaces sólo con permiso propio. Sin base/fuente no consulta ni ofrece filtros/exportación.

Validaciones:11pruebas iniciales (7hook+3página pasaron,1testnuevo ambiguo por palabra Facturado en option/celda); selector corregido a rolecell. Luego17pruebas página+GenerarReportelegacy aprobadas y4página revalidadas tras ocultar filtros sinpermiso y mover hook a servicios por FastRefresh. Typecheck correcto; warningFastRefresh detectado y corregido. No suite completa. Comandos focalizados `npm --prefix frontend test -- src/modules/reportes/paginas/Viajes.test.tsx` y combinaciones indicadas con useSolicitudAnalisis/GenerarReporte. No cambios ejecutables del módulo005.


## T071 — Facturación documental y Cobros

FacturacionReporte separa Documentos/Cobros con navegación por botones/teclado. Conserva fechas/cliente/número aplicados al cambiar; no traslada estado documental al flujo. Tablas muestran snapshots, Neto/IVA/Total y fechaCobro; flujo explica factura anterior cobrada en rango con ambas fechas. KPI/series/distribuciones exactos del servidor, exportación distinta por perspectiva y permiso compuesto. Hook temporal admite filtros iniciales aditivos; referencias conservadas muestran identidad al regresar sin cargar padrón completo.

8 pruebas focalizadas páginaFacturación+regresión directaViajes aprobadas: septiembre/octubre, máximo exacto, columnas, deuda separada, estado sólo documental, filtros comunes preservados, ambos formatos/perspectivas y sinfuente sinconsulta. `npm --prefix frontend test -- src/modules/reportes/paginas/Facturacion.test.tsx src/modules/reportes/paginas/Viajes.test.tsx`; typecheck correcto por ampliación del hook común. Sin suites completas ni cambios006.


## T072 — Liquidaciones documentales y OP por fecha

LiquidacionesReporte separa meses liquidados completos de pagos por días exactos. Conserva filtros comunes, excluye estado documental/vigencia al cruzar perspectivas; pagado/parcial/deuda vigentes provienen del servidor. OP como fila y origen manual/OPP identificado sin suma adicional; revertidos conservan instante/autor/importes históricos explícitamente excluidos del vigente. LinkOPP sólo con pagos.consultar. Ambas exportaciones y gráficos/contextos separados.

7pruebas focalizadas Liquidaciones+Facturación aprobadas:09/10 completos,parcial,manual/OPP/revertida posterior,permiso lateral, ambos formatos/perspectivas y filtros. Primera corrida:2pasaron/1expectativa nueva de fecha falló por separador regional; corregida manteniendo día/hora exactos. Typecheck detectó opción de testexact no soportada en Facturación/Liquidaciones; eliminada (ByRole con nombrestring ya exacto), typecheck posterior correcto. Se precisa evidenciaT071: su typecheck productivo previo era correcto; este control incluyó y corrigió tests recién creados. No suites completas ni cambios009/014.


## T073 — Caja analítica

CajaReporte usa ingresos/egresos/movimiento neto y serie del servidor; horario argentino visible y referencias tipadasOP/OPP sin confundirlas. Egreso y compensación son filas independientes, no se suman cobros/pagos documentales. Reutiliza tabla/controles de consulta compartidos, no reemplaza pantalla011. Filtro mínimo paginadoCaja sin gestionar, exportación propia y enlaceOPP sólo con pagos.consultar.

3 pruebas focalizadas aprobadas: neto negativo,UTC02:30en día argentinoanterior, referencias/compensación e inexistencia de saldo disponible; filtroCaja aplicado/exportación; permisoOPP y emisión separados. `npm --prefix frontend test -- src/modules/reportes/paginas/Caja.test.tsx`. Sin cambios compartidos ni011; no suites completas/build redundante.


## T074 — Utilización de flota

UtilizacionFlota incluye filtros mínimos vehículo/tipo/propietario, actividadtodos/activa/inactiva y orden. Ranking conserva orden/desempates del servidor, unidades0/inactivas, atributos actuales y sinvehículo del período fuera del filtro de unidades. Gráfico de vehículos declara recorte de página; distribuciones tipo/propietario tienenTop10+Otros y categorías completas paginadas. No km/horas/porcentajes ni acceso implícito a gestión.

4 pruebas focalizadas aprobadas: ceros/inactivos/actuales/sinvehículo, recorteOtros/tabla exacta, false y orden explícito/exportación, referencia mínima/categorías completas y fuente sinbase sinconsulta. `npm --prefix frontend test -- src/modules/reportes/paginas/UtilizacionFlota.test.tsx`. Sin cambios de componentes compartidos ni004, sin suites completas redundantes.


## T075 — Vencimientos actuales

VencimientosReporte muestra sólo paneles autorizados; no selector/período global ni cheques. Reutiliza literalmente mapas/plazos de Choferes/Flota, fechas y tablas; tipos separados conservan totales monetarios string nuevos. Paginación sobre universo completo, contexto de hoy, exportaciones por panel y enlaces a fichas sólo con gestión/lectura propias. Ningún enlace/descarga de adjuntos.

4pruebas focalizadas aprobadas y29de las tres pantallas originales aprobadas: palabrasAl día/Vigente/plazo, sin período ni adjunto, tres exportaciones/página y permiso aislado sin gestión. Typecheck corrigió anotación accidental de parámetro al añadirimporttype; posterior correcto. Comandos `npm --prefix frontend test -- src/modules/reportes/paginas/Vencimientos.test.tsx` y tresPanelVencimientoslegacy. Sin suites completas.


## T076 — Rutas y menú

App incorpora siete rutasReportes con claves de sesión, sin reordenar rutas previas ni traer Empleados. Catálogo servidor agrega entradaúnicaReportes porreportes.consultar; emitir/flota.consultar no crean entrada. Frontend sólo agrupa código recibido en Seguimiento y agrega constantes, sin inventar autorizaciones ni descartar desconocidos. Secciones verifican base/fuente, links propios y emisión independientes.

14unitariosbackendCatálogo/MenuAnalisis aprobados (`t076-menu.trx` en /tmp/gt015-frontend),20frontendRutasReportes/seccionesDeMenu/sesion aprobados: siete accesos directos sinpermiso sin consulta, opciones recibidas/desconocidas, compatibilidadsesión. Build/tsc-b/lint correctos; chunk gráfico lazy375,90kB y principal802,53kB, warningprincipal previo. Medición/networkformalT078. Sin suite completa.


## T077 — Responsive, foco y teclado reales

HostHTTPS temporal015 con SQL efímero/Snapshot y dataset sintético, frontend compilado servido realmente. Primera medición375 confirmó contenido sinancho por menú266px fijo. Layout/Menu reciben opciónaditiva adaptable sólo en rutasReportes: columna móvil y ancho completo, estilos previos por defecto intactos. Filtros usan superficie existente para ayuda/contraste. Referencia escrita sin seleccionar invalida campo y bloquea aplicar; corrección vaciando/seleccionando. Navegación financiera se conserva montada fuera de perspectivas para mantener foco al cambiar. No tokens/reglas de negocio nuevos.

Chrome real operó7pantallas×2anchos1280/375: documento sin overflow horizontal, main960/351px, tablas con scroll local, filas sin tabIndex, tabla porEnter y diálogo foco inicial/Escape/retorno. Además dos flujos financieros por flecha conservan foco y muestran fechas/contexto. Capturas tabla desplazada muestran últimos importes/acciones. /tmp/gt015-aceptacion/visual.json:14recorridos,0erroresJS. Capturas revisadas; primer script falló porque selectorVer cambiaba al siguiente botón tras pasar aOcultar, corregido con handleestable y recorrido completo aprobado.

53pruebasregresión directa páginas/GenerarReporte/menú aprobadas;5Viajes revalidadas con referencia inválida;15páginas financieras/rutas y8financieras tras ajuste final de foco aprobadas. Build/typecheck/lint correctos; sin suites completas. Herramientas/capturas temporales, host sigue activo paraT078/aceptaciónfinal. Compilación inicial del host falló por capturar retornoTaskvoid del fixture, corregida sin código productivo. Fuentesgt-ui buscadas nuevamente y ausentes: R10documental pendiente, sin afirmar cotejo pleno.


## T078 — Bundle y carga gráfica exclusiva

Build final vigente deT077 medido con el mismo gzipPython6debaseline: principal803111bytes/gzip201429 (baseline752814/191683; +50297/+9746), chunklazygráficos375905/106655, CSS74999/27043 (baseline71926/26522; +3073/+521). /tmp/gt015-frontend/bundle-final.json. Warningprincipal>500kB ya existía; crecimiento explícito015se registra, sin atribuir costo cero a biblioteca. React/DOM/Is19.2.8,Recharts3.10.1,TS6.0.3 confirmados npm ls.

Chrome nuevo contexto comprobó0pedidosGraficosRecharts en login/inicio/Viajesoriginal;1pedido al abrirReportes. Barrera de red retuvo chunk: indicadores y tabla exacta operables por clic antes de liberar, luego7gráficosSVG reales renderizaron. /tmp/gt015-aceptacion/lazy.json y script temporal node /tmp/gt015-browser/lazy.cjs: LAZY_Y_TABLA_VALIDOS. Sin cambios productivos ni repetición de suites/build ya vigente.


## T079 — Checkpoint frontend (corridas y entorno)

Regresión indicada de Reportes/Choferes/Flota/Viajes/Facturación/Liquidaciones/Caja/Proveedores/Pagos y compartidos. Primera corrida594aprobados/87archivos pero cwdfrontend resolvió Node22.23.3/npm12.2.0; se conserva como ejecución real, no evidencia del runtime objetivo. Desde raíz Node24.22.0/npm11.19.1:586aprobados/8fallos (7timeouts5000ms y1fallo posterior de DOM en archivo que tuvo timeout). Siete tests registraron pausas de145–150segundos; no se demuestra causa ni se declara arreglo de tests.

Repetición focalizada de los6archivos implicados con --maxWorkers=2:47aprobados/0fallos, sin tocar código/aserciones/timeouts. Se ejecuta el checkpoint entero con Node24 y2workers para evidencia íntegra, sin reducir cobertura. JSONtemporales t079.json/t079-node24.json/t079-timeouts-focal.json/t079-node24-final.json en /tmp/gt015-frontend. Build/tsc-b/lint ya correctos; avisoViteprevio. Inspección fuente no encuentraStorage/history.state/parseFloat/toFixed financieros; única conversiónNumber en coordenada visual acotada porBigInt. No DTO persistido.


CierreT079: checkpoint entero enNode24con2workers:594aprobados/87archivos,0fallos/omitidos,156,04s. Sin alterar tests ni timeouts. Build/tsc-b/lint correctos, bundle idéntico aT078. Comando reproducible desde raíz: `npm --prefix frontend test -- src/modules/reportes src/modules/choferes src/modules/flota src/modules/viajes src/modules/facturacion src/modules/liquidaciones src/modules/caja src/modules/proveedores src/modules/pagos src/compartido --maxWorkers=2 --reporter=default --reporter=json --outputFile=/tmp/gt015-frontend/t079-node24-final.json`; `npm --prefix frontend run build`; `npm --prefix frontend run lint`. Las corridas fallidas anteriores permanecen registradas, no sustituidas ni atribuidas a causa demostrada.

## T080 — Contraste de implementación y fuentes cerradas

Spec/plan/research/modelo/contratos sin cambios. Contraste de código real y evidencia ejecutada; aceptación final/performance de T083–087 todavía pendientes, no supuestas.

| Requisito | Implementación y evidencia comprobada |
|---|---|
| FR001–004 / AC01 / SC001–002 | Siete rutas, dashboard KPI→gráficos→accesos, período argentino y filtros aplicados/página separados. PeriodoAnalisisTests, DashboardAnalisisTests, Dashboard/Viajes/frontend y T077 real. |
| FR005–006 / C01 | Viajes estados0–4, realizados2+4, EnCurso1; predicado compartido con005, diez columnas originales. TablaViajesAnalisisTests, RutasViajesAnalisisTests y páginaViajes; fuentes012 conservadas. |
| FR007–008 / AC02 / C03 / SC003 | Facturas documentales porFecha y Cobros porFechaCobro, snapshots y deuda vigente sin resta entre universos. FacturacionAnalisisTests/CobrosAnalisisTests/ExportacionFacturacionAnalisisTests y frontend financiero. |
| FR009–011 / AC03 / C03 / SC003 | Meses liquidados completos, flujos diarios, OP única manual/OPP, revertidos históricos excluidos del vigente. LiquidacionesAnalisisTests/PagosAnalisisTests y páginaLiquidaciones. |
| FR012–013 | Caja ingresos/egresos/neto porArgentina, OP/OPP separados y compensación histórica real. CajaAnalisisTests/ExportacionActividadAnalisisTests y páginaCaja. |
| FR014–015 / C02 / SC005 | LEFTJOIN unidades0/inactivas, conteo2+4 porVehiculoId/Fecha, actuales rotulados/sinvehículo fuera de filtro. ConteosFlotaAnalisisTests/FlotaAnalisisTests/GruposAnalisisTests y UtilizacionFlota. |
| FR016 | Fachada de tres paneles reutilizados, hoy capturado, sincheques/adjuntos. VencimientosAnalisisTests/RutasVencimientosAnalisisTests y cuatro pruebas página más29legacy. |
| FR017–019 | Página20/orden total, grupos SQLTop10+Otros y completos paginados, Recharts lazy con tabla exacta/patrones/unidades. GruposAnalisisTests, tests gráficos y recorridoT077/T078. |
| FR020–024 / AC05 / SC004/010 | Modelo tabular común, diez exportaciones nuevas y cinco legacy, count5000/5001 antes de materializar, foto/instante, dinero numérico y SaveAs/reapertura de celdas/totales. TopeExportacionAnalisisTests/RutasExportacionAnalisisTests/PrecisionLibroRealTests/FronterasLegacyXlsxTests/PdfAnalisisTests; PDF reales abiertosT056. |
| FR025–026 / AC04 / C04 / SC009 | Base+fuente/propiaFlota, exportación agregaemitir, menúúnico, autorización antes de consultas. MatrizRutasAnalisisTests/PermisosExportacionAnalisisTests/SemillasAnalisisTests/MenuAnalisisTests y siete rutasfrontend directas. Legacy sin requisito nuevo. |
| FR027–028 / SC006 | Snapshot y proyección readonly, sinmutaciones ni archivos persistidos, actuales/snapshots diferenciados. SinMutacionesExportacionTests/ConcurrenciaFotoNegocioAnalisisTests; fixtures/proyecciones originales y regresiones012. |
| FR029–030 / SC007 | Solicitudes por secuencia/sesión, loading/error/vacío/403 distintos, foco/labels/controles nativos y responsive opt-in. useSolicitudAnalisis/GraficoAccesible/ComponentesAnalisis y ChromeT077. Cotejo documentalgt-ui sigueR10. |
| NFR001 / SC010 | Decimal SQL/.NET, dinero stringnuevo/BigInt y coordenada visual única Number, guardiaXLSX de centavos con libro real. SerializacionAnalisisTests/VerificadorMonetarioXlsxTests/ManifestMonetarioXlsxTests y tooltip/frontend. |
| NFR002–004 / SC008 | Agregados SQL y página independiente, instrumentación20vs200 sinN+1, dataset10000/5000/5000/10000,64planes/IO y baselineT040–043. Final20consultas y exportación1000 enT083 pendientes. Sin índices preventivos. |
| NFR005 | Snapshot opt-in/readiness503 sinfallback, escritor entreSELECT y render después de materializar. SnapshotAnalisisOn/OffTests/ConcurrenciaFotoNegocioAnalisisTests/ConcurrenciaExportacionAnalisisTests. |
| NFR006 | Rutas/políticas/columnas012 conservadas, excepción explícita precisión; T060 completo y T079frontend. Suites completasT081/T082 pendientes. |

US1–6 tienen implementación/API/UI/exportaciones correspondientes. Hallazgos de claridad corregidos dentro del contrato: resumen muestra filtros aplicados independientemente del borrador; facturas anuladas explican importe nominal sin contribución financiera. No reglas nuevas. Veinte pruebas focalizadas consumidores temporales aprobadas; aserción adicional Viajes comprueba estado aplicado, no borrador. Build/typecheck/lint y prueba focalizada adicional se registran al finalizar su proceso, sin sustituir checkpoints finales.

CierreT080:5pruebas adicionalesViajes aprobadas; build/tsc-b/lint correctos, avisoViteprincipal804,03kB. Se conserva matriz con validaciones finales explícitamente pendientes; ninguna fuente aprobada modificada.

## T081 — Build y suites backend completas

Build `backend/GT.sln` correcto:0errores/8warningsNU1902/NU1903preexistentesPkcs. GT.UnitTests completa848aprobados,0fallos/omitidos. Integración completa en ejecución, sin filtros ni weakening. Comandos reales desde raíz:

```bash
/home/joaco/.dotnet/dotnet build backend/GT.sln --no-restore --nologo
/home/joaco/.dotnet/dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --logger 'trx;LogFileName=t081-unit-completa.trx' --results-directory /tmp/gt015-final
/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --logger 'trx;LogFileName=t081-sql-completa.trx' --results-directory /tmp/gt015-final
```

Resultados fuera de repositorio. Sin cambios ejecutables backend después de build; hashes spec/plan/research/modelo/contratos nuevamente comprobados intactos. No se cierra tarea hasta revisar TRX SQL.

Primera integraciónT081:1303aprobados/3fallos,1306totales,0omitidos. Fallo009misma aserción temporal conocida; otros2son supuestos legacy incompatibles con extensión explícitaFR025: Gerencia ahora tiene consultar/flota.consultar además deemitir y su menú recibeReportes porconsultar. En AsignarRolesTests se amplía la lista exacta esperada con los2permisos aprobados, conservando count8módulos y prohibición de gestión. En PermisosDeReporteTests se usa cuenta con sóloemitir en lugar deGerencia, manteniendo intactas las dos aserciones originales: emitir presente y ninguna entradaReporte. No cambia producción ni se elimina/debilita cobertura012; los permisos nuevos tienen pruebas independientes de semillas/políticas/menú. Diff revisado explícitamente.

Cuatro pruebas focalizadas (dosajustes,semillas ybaseline009aislado) aprobadas,0fallos. t081-focal-compatibilidad-baseline.trx. Se repite SQLcompleta en t081-sql-completa-validada.trx para conservar evidencia final; no se oculta corrida inicial. Baseline009no modificado ni causa histórica declarada resuelta. Compilación focal reprodujo advertencias preexistentesCS8604/xUnit2031/xUnit2000 además dePkcs.


CierreT081: segunda integración completa1306aprobados,0fallos/omitidos (4m13s); contadoresXML comprobados. Baseline009Passed en eseTRX y en focal; test/aserción intactos, intermitencia histórica no resuelta. Buildcompleto0errores/8warningspreexistentes y848unitarios completos verdes. Segunda corrida real: `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-build --no-restore --logger 'trx;LogFileName=t081-sql-completa-validada.trx' --results-directory /tmp/gt015-final`. Se conservan primera corrida y cuatrofocales, sin omitir resultados.


## T082 — Frontend completo final

699pruebas/101archivos aprobados,0fallos/omitidos; JSON comprobado. Todos los módulos sin filtros, Node24.22.0/npm11.19.1 desde raíz, concurrencia2workers sin cambiar cobertura/aserciones/timeouts. Build/tsc-b/lint correctos; warningViteprincipal804,03kB previo en clase, crecimiento015explícito. Se observaron avisosReactact en prueba antigua ListadoAdelantos no modificada; no fallos ni supresión de avisos.

Comandos: `npm --prefix frontend test -- --maxWorkers=2 --reporter=default --reporter=json --outputFile=/tmp/gt015-final/t082-frontend-completo.json`; `npm --prefix frontend run build`; `npm --prefix frontend run lint`. Bundle vigente después de precisión editorialT080 en /tmp/gt015-final/bundle-final.json (mismo métodoPython gzip6queT061). No código frontend adicional después de esta evidencia; aceptación/performance sólo pruebas salvo hallazgo real.


## T083 — Rendimiento final y crecimiento acotado

RendimientoFinalAnalisisTests ejecutó el harness completo con10000Viajes/5000Facturas/5000Liquidaciones/10000Movimientos,250vehículos/200choferes, período anual2026, una sesión de carga secuencial,2calentamientos separados y20muestras por recurso. SQLSnapshotON, sin nuevos índices; mismos recursos locales16CPU/RAM documentadaT040,ArchLinux/.NET10.0.10/SDK10.0.302/SQL16.0.4265.3. RCSItrue es el valor previo de provisión EF, no alterado por015ni usado como fallback; aislamiento explícitoSnapshot confirmado por interceptor.

Todos11recursos cumplieron20/20<=5s (220mediciones): máximosms dashboard398,Viajes129,Facturación48,Cobros34,Liquidaciones50,Pagos55,Caja40,Flota73,VenciChoferes14/Flota13/Facturas14. Consultas20/200idénticas:Viajes10,Fact6,Cobros7,LQ6,Pagos6,Caja4,Flota11,Venci1cada; dashboard46.64planes reales/IOguardados, sinN+1. Descarte de índicesT042/T043 ratificado por medición final, sin migración nueva.

Exportación HTTP de1000facturas reales vencidas de enero–mayo: PDF1203ms/270084bytes; XLSX2010ms/37013bytes incluyendo SaveAs/reapertura/verificación monetaria del armador. Cada formato<15s; XLSX reabierto adicionalmente para comprobar1000fechas de detalle.1pruebaSQLperformance aprobada,0fallos/omitidos,83s; sin código productivo cambiado ni repetición de suites completas. `/home/joaco/.dotnet/dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~RendimientoFinalAnalisisTests --logger 'trx;LogFileName=t083-rendimiento-final.trx' --results-directory /tmp/gt015-final`. Compilación sólo warningsprevios. Datos/planes/JSON/PDF/XLSX temporales en /tmp/gt015-medicion/final; limpiezaSQL verificada BaseEfimeraEliminada=true.


## T084 — Aceptación Dashboard/Viajes/Vencimientos

Chrome/HTTP/SQL reales operados y registrados en acceptance-evidence.md. Dashboard coincide con SQL;21viajes sintéticos inactivos muestran clasificación10Rendidos/10Pendientes/1EnCurso, página20+1 y XLSX21desde página2; mismo21enlegacy. Diez archivos originales012 (5×2formatos) válidos. Tres paneles nuevos coinciden con APIs originales por cantidad/orden/identidad; sin período global. BordeUTCsimulado sóloDate propone mesargentino correcto. SQLantes/después idéntico.

Primer contraste falló sólo por referencia temporalSQLque incluía6unidadesinactivas enVenciFlota;004lasexcluye, R6lasincluye. Referencia corregida24/30respectivamente sin código productivo ni suites completas. Script focalizado completo después aprobó4bloques, JSONt084 y librosZIP/XML21revisados. Compilación/control/herramientas/capturas sólotemporales; host/base siguen paraaceptación. Operadoragente, noaprobaciónhumana. T084completa.


## T085 — Aceptación financiera

Tres bloques Chrome/HTTP/SQL aprobados: septiembre cobradaoctubre121.01 vs deudaOct242.02; meses completos09/10(total500,pagado50,pendiente450) vs OPdíasexactos150incluyeLQagosto; OPP3OPvigentes363 y otra3OPrevertidasdespués delrango0vigente/363histórico.10PDF/XLSX financieros generados, snapshots/inactivos y pestañas/filtros correctos. Detalleenacceptance-evidence. SQLglobal independiente confirmó importes esperados.

HostanteriorT084 cerrado y BASE_EFIMERA_ELIMINADA comprobado. Hostnuevo prepara operaciones014reales históricas mediante repositorio, sin cambiar reglas ni producción. Primercompilefixture tuvo2errores propios (evento/importehistorial009 inexistentes), corregidos leyendo enum/entidad: parcial sóloOP, completadaeventoPagada. Buildtemporalposterior correcto; sólo Pkcswarnings. No suites completas repetidas; hostnuevo continúaT086.


## T086 — Aceptación Caja/Flota/permisos/exportación

CajaSep-121/Oct+121 por egreso/compensación realesOPP, anulador distinto; flota ceros/inactivas/atributos actuales/conteo SQL;5000XLSXcompleto/5001HTTP409yUI; máximo exacto enconsulta/PDF conXLSX409. Cuatro perfiles de permisos/revocación/storage aprobados.18librosClosedXMLabiertos y17PDFporfamilia renderizados/revisados. Detalleenacceptance-evidence yJSON/capturas temporales.

Hallazgo real dePDF máximo: importe partido visualmente. Único cambio productivo local: FuenteAnalisisViajes copia sus columnas preservandoNombre/Tipo y da pesoPDF3 aImporte; legacy/motorExcel/PDF/consulta no modificados.2unitarios y14SQL/HTTPfocalizados aprobados, warningsprevios; t086-fuente-viajes-unit.trx y t086-pdf-focal-regresion.trx. PDFactual regenerado porDI sobrebaseefímera y revisadoPNG/texto: importe/total completos enuna línea. Código frontend sin cambios desdeT082; no suites completas por cambio local según instrucción de frecuencia. PerformanceFactura1000T083 no afectada.

Comandos scripts `node /tmp/gt015-browser/t086.cjs`, repetición sólo `--permisos` conNode24absoluto; Control015.dll preparar-limites/validar-archivos/pdf-max-actual, pdftoppm/pdftotext. Dos errores propios del script (búsquedaCaja por etiqueta compuesta y ruta/api/flota404) corregidos; escenarios ya pasados no repetidos al completarpermisos. Sin decisiones nuevas. T086completa.


PrecisióndecierreT086: la revisión de la primera imagen ampliada detectó también fecha partida tras ampliarImporte; T086se reabrió hasta corregir pesoFecha1.4 en la misma fuente nueva. Tres pruebas SQLdirectas adicionales (PdfAnalisis/ExportacionActividad) aprobaron,0fallos; t086-fecha-pdf-final.trx. PDFregenerado otra vez con DIactual y revisadoimagen/texto:03/10/2026 y9999999999999999.99 íntegros en la misma línea, total completo. No motor/columnaslegacy alterados ni fuente deFactura de performance tocada. T086cerrada tras esta revisión real, no por mera escritura.


## T087 — Aceptación final visual/teclado/carreras frontend

14recorridos Chrome1280/375 aprobados sinoverflow/erroresJS, incluyendocambiodeperspectivafinanciera con foco conservado y tablas desplazadas. ÉxitoA/errorA después deBreal no alteran KPI5002/contexto, mediante retención de transporte y render sin sleeps. Rango inválido sinrequest, ariaerror y diálogoEnter/Escape/retorno correctos.

Sietemuestras reales colorCSS+píxelfondo:15.183/10.313/4.544/5.272/4.591/18.720/9.297>=4.5. Capturas revisadas y referenciasenacceptance-evidence; tokenssinmodificar, R10documental todavía pendiente. Node24explícito deldriver. Frontendsin cambiosT082; no suites completas. T087completa.


## T088 — Alcance y superficies compartidas

Revisión del diff contra T002: Program agrega DI/políticas/rutas; Rol y semillas agregan los dos permisos aprobados; catálogo una entrada Reportes. App integra siete rutas y Layout/Menu incorporan adaptación opt-in para Reportes, conservando el comportamiento anterior por defecto. Sin código de Empleados, reordenamientos ajenos ni cambios de escrituras económicas. Proyecciones/predicados compartidos son de lectura. Guardia XLSX es la extensión explícita aprobada; columnas y rutas legacy preservadas. Los dos ajustes de tests de permisos se justifican en T081, sin debilitar aserciones.

Migraciones históricas, GtDbContext, fixture original AplicacionDePrueba y test temporal009 sin diff. No se agregó migración ni índice: mediciones T041–T043/T083 justifican conservar esquema. Contratos runtime segregados por perspectiva; la interfaz agregadora no es un handler provisional ni una dependencia registrada. Hashes de spec/plan/research/modelo/contratos idénticos al inicio. T088 completa.

## T089 — Auditoría de archivos y datos

226 archivos modificados/nuevos examinados, incluidos untracked; informe local /tmp/gt015-final/auditoria-final.json. Sin claves privadas/tokens, archivos sensibles o generados accidentales, marcadores de conflicto ni TODO/FIXME/debugger/console.log/NotImplementedException en código analítico. Whitespace de código nuevo revisado; saltos Markdown intencionales de fuentes aprobadas preservados. git diff --check correcto; staging vacío. Herramientas, capturas, TRX, libros y dumps no se versionan. Verificación real de navegador confirmó storage vacío e history sin DTO; Number monetario sólo en coordenadas visuales acotadas, no agregados autoritativos. Sin commits ni push. T089 completa.

## T090 — Evidencia consolidada y limpieza

Cierre de ejecución: 2026-10-21, Argentina. SDK10.0.302, Node24.22.0/npm11.19.1, SQL Server real. Contadores XML finales releídos:848 unitarios completos y1306 integración completa aprobados,0fallos/omitidos; adicional1prueba final de performance aprobada. Frontend completo699pruebas/101archivos,0fallos/omitidos. Build backend0errores/8warningsPkcs preexistentes; build/tsc-b/lint frontend correctos. No se suman repeticiones focalizadas a esos totales. Tras ajuste local de ancho PDF se ejecutaron2unitarios,14SQL/HTTP y3SQL adicionales; no cambio frontend después de T082.

Snapshot OFF/ON, instalación vacía/actualización legacy y concurrencia validados en T010–T018/T038; runtime sin ALTER ni fallback. Habilitación administrada sigue siendo precondición operativa del despliegue. RCSI previo no alterado. No nuevos índices. NFR003/004: consultas acotadas20/200;11recursos20/20<=5s, máximo dashboard398ms; PDF1000filas1203ms/XLSX2010ms incluyendo reapertura. Bundle principal804033/gzip201772bytes, gráfico lazy375905/106656, aumento principal51219/10089 frente a baseline; Recharts3.10.1 estable con React19.

Aceptación Chrome/HTTP/SQL en acceptance-evidence.md: siete pantallas375/1280, filtros/carreras/foco, documentos vs flujos, OP/OPP/reversión, Caja, flota, permisos/revocación,21/5000/5001, PDF/XLSX y precisión. Operador agente, no aprobación humana.18libros reabiertos y17PDF renderizados; mínimo de siete muestras de contraste4.544:1. Cinco exportaciones012 y regresiones fuente verificadas. R1gráficos,R2precisión,R3volumen,R4universos,R5Snapshot,R6compatibilidad,R7permisos,R8atributos actuales yR9integración revisados con evidencia anterior; R10gt-ui continúa residual documental por fuente ausente, sin cotejo pleno supuesto.

Host final cerrado normalmente. Verificación independiente por metadata devolvió BASE_EFIMERA_ELIMINADA para GtLogistica_Test_9bed21672ba8495fb89a342f350bbef6; host anterior y base de performance también eliminados y registrados. Comando final: /home/joaco/.dotnet/dotnet backend/tests/GT.IntegrationTests/bin/Acceptance015/Aceptacion015.dll verificar <base-efímera>. No datos de desarrollo alterados. Baseline009 pasó en suite completa final y aislada, tras reproducir intermitencia conocida en primera corrida; causa histórica no declarada resuelta. Warnings preexistentes y resultados iniciales conservados. T090 completa.


## T091 — Mantenimiento transversal y cierre

Como último mantenimiento se agregaron tres convenciones[015] a AGENTS.md: foto Snapshot materializada/readiness sin fallback, verificación monetaria del XLSX real por coordenadas y descarte de respuestas obsoletas por filtros/sesión. No duplican reglas de dinero HTTP ni almacenamiento013. Diff de AGENTS revisado y git diff --check correcto.

Comprobación final:91IDs únicos,T001–T091 completos,ninguna tarea pendiente; contexto015 correcto. Fuentes aprobadas conservan hashes iniciales. Evidencia T081–T087/T090 comprobada, sin resultados supuestos ni tareas fusionadas. No funcionalidad adicional en el cierre. Sin staging, commits ni push. Lista para revisión humana con R10documental y advertencias/intermitencia histórica registradas. T091 completa.
