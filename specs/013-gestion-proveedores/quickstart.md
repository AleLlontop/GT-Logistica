# Validación de punta a punta — Feature 013

Guía y registro de verificaciones. Referencias: [contratos](contracts/README.md), [modelo](data-model.md), [spec](spec.md).

**Estado final del cierre, 2026-10-18**: T045 y T062 completas; junto con T063–T066, queda cerrada documentalmente la feature 013. La evidencia final informada por el usuario se registra al final de este documento y reemplaza los pendientes de las entradas históricas. Las secciones anteriores conservan el estado de cada ejecución. Ver también la [evidencia de performance/UI](evidencia-cierre.json).

## Ejecución parcial previa 2026-10-16

Preparación y fundamentos escritos; no están implementados todavía los recorridos de proveedores ni facturas. No se aplicó la migración a ninguna base ni se levantó SQL Server, por indicación expresa del usuario. No se publicaron endpoints ni menús de la feature.

| Verificación ejecutada | Resultado |
|---|---|
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --verbosity minimal`, antes de agregar las pruebas de fundamentos | 498/500 aprobadas; dos fallos en `FuentesDeVencimientosTests` (Choferes/Flota, caso Próxima a vencer: devuelve Vencida). No se tocaron esas fuentes ni sus aserciones. |
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore --filter FullyQualifiedName~FundamentosProveedorTests` | 6/6 aprobadas: metadata, índices, CHECK, permisos aislados y búsqueda mínima OR. No ejecutan locks ni restricciones contra SQL. |
| `dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EscenarioProveedoresTests` | 1/1 aprobada: CUIT de fixtures, condición externa y ausencia de copias de identidad/contacto. Esta prueba de fixtures no conecta a SQL. |
| `dotnet ef migrations add Modulo13Proveedores --project backend/src/GT.Infrastructure --output-dir Persistencia/Migraciones --no-build` | Generó `20261016162244_Modulo13Proveedores`, su diseñador y snapshot. |
| `dotnet ef migrations script --project backend/src/GT.Infrastructure --no-build` (salida temporal) | SQL generado y revisado: dos tablas, tipos/capacidades, FKs restrict, CHECK e índices únicos filtrados previstos. No aplicado. |
| Build frontend: TypeScript `-b` y Vite `build` con Node 24.21.0 | Aprobado; aviso de tamaño de bundle existente. Tipos nuevos incluidos por TypeScript. |
| Pruebas de integración filtradas por `FullyQualifiedName~Transportistas` | 13 fallos al preparar el host: falta `GT_SQL_PASSWORD`. No llegaron a ejecutar casos de negocio ni validar la migración. |

Node 24 se invocó por ruta absoluta porque el shim local usaba otra versión. Las dependencias del lockfile se instalaron sin modificar manifiestos; se usó el CLI npm 10.9.8 existente con Node 24 porque el npm incluido en esa instalación de Node está incompleto. No se cambió la configuración global del equipo ni compose.

Al finalizar esa ejecución previa, las garantías de espera, orden y exclusión entre conexiones del coordinador seguían pendientes de SQL real (T008/T013). La continuación siguiente verifica el mecanismo de T008; las carreras de negocio de T013 siguen fuera del checkpoint. Tampoco se verificaron atomicidad de factura/PDF, recorridos UI, rendimiento ni SC-001–SC-007. La feature no está terminada.

## Continuación del checkpoint de fundamentos 2026-10-16

Se revisaron T001–T010 contra código y contratos. T001–T007 y T009–T010 ya estaban implementadas; sólo faltaba la verificación SQL de T008. No se reimplementaron tareas ni se inició T011. No se usó Docker, no se hizo commit y no se alteraron producción ni tests para adaptar la conexión local.

Conexión de esta validación: `Server=.\SQLEXPRESS02;Database=GtLogistica;Trusted_Connection=True;TrustServerCertificate=True`. La migración ya estaba aplicada: no se ejecutaron migraciones ni escrituras de registros. La autenticación Windows requirió ejecutar las consultas fuera del sandbox, con autorización.

| Verificación ejecutada | Resultado |
|---|---|
| `sqlcmd -S '.\SQLEXPRESS02' -d GtLogistica -E` consultando `__EFMigrationsHistory` | Presente `20261016162244_Modulo13Proveedores`. |
| Harness temporal .NET 10 con referencia al proyecto Infrastructure, instanciando `CoordinadorIdentidadProveedor` real y tres DbContext sobre esa conexión | Aprobado. Sin transacción lanza InvalidOperationException. Dentro del resolver se verifica que el global ya está en Exclusive; un interceptor observa global → `20123456786` → `30712345671` al suministrar CUIT desordenados, repetidos y con separadores. APPLOCK_MODE confirma Exclusive/Transaction. |
| Dos conexiones del harness, sincronizadas observando `sys.dm_tran_locks`, sin sleeps | Aprobado para commit y rollback: segunda transacción en WAIT antes de ejecutar su resolver; al liberar la primera, adquiere los recursos. No se condiciona la adquisición a la existencia de filas. APPLOCK_TEST confirma liberación al terminar. |
| Timeout real de sp_getapplock con primera conexión reteniendo el global | Aprobado: retorno negativo produce SqlException 51013 y el resolver de la segunda conexión no se ejecuta. Rollback y comprobación de liberación posterior. |
| `GetPendingMigrationsAsync` y `HasPendingModelChanges` con EF real | Sin migraciones pendientes ni diferencias entre modelo y snapshot. |
| Consulta de `sys.check_constraints` y `sys.indexes` en la base existente | Proveedores tiene 3 CHECK y FacturasProveedor 6; presentes índices únicos de CUIT, TransportistaId, proveedor/número y LiquidacionId, con los filtros previstos, e índice Fecha/Id. |
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-restore -v minimal` | Compilan backend y pruebas. 504/506 aprobadas; mismos dos fallos previos en Reportes/FuentesDeVencimientosTests (Choferes/Flota: esperaba Próxima a vencer y obtuvo Vencida). Casos con vencimiento fijo 23/09/2026; no se modificaron. |
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~Proveedores -v minimal` | 6/6 aprobadas: fundamentos, metadata, CHECK, índices y políticas aisladas/OR. |
| `dotnet test backend/tests/GT.IntegrationTests/GT.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EscenarioProveedoresTests -v minimal` | Compila el proyecto de integración; 1/1 aprobada. Verifica fixtures sin conexión, no equivale a una prueba de integración SQL. |
| Frontend con Node 24.21.0: CLI locales `typescript/bin/tsc -b`, `vite/bin/vite.js build`, `oxlint/bin/oxlint` y `vitest/vitest.mjs run` | Build y lint aprobados; 64 archivos y 492/492 tests aprobados. Aviso de bundle >500 kB. Se invocó Node por ruta absoluta: npm ejecutaba los scripts con Node 21.2.0 aun anteponiendo Node 24 al PATH. No se cambió instalación/configuración global ni dependencias. |
| `git diff --check` y revisión de archivos ignore | Sin errores de whitespace; ignores existentes cubren artefactos y configuración local. No requirieron cambios. |

El harness temporal sólo tomó locks y leyó metadata; se retiró después de ejecutarlo. La integración del coordinador en escrituras de Proveedor/Transportista/factura y sus carreras corresponde a T013/T017/T018/T029 y posteriores, no a este checkpoint. Al cerrar ese checkpoint todavía no había rutas ni menús nuevos de negocio publicados.

Limitación de integración: `AplicacionDePrueba`/`ConfiguracionEntorno` fijan `127.0.0.1,1433`, usuario `sa` y `GT_SQL_PASSWORD`, con bases aisladas; no aceptan la conexión Windows indicada. No se adaptaron ni se ejecutó nuevamente la suite SQL del compose contra la base de desarrollo. Antes de validar los próximos incrementos será necesario un entorno compatible con esa infraestructura de tests. Advertencias NuGet preexistentes NU1902/NU1903 para System.Security.Cryptography.Pkcs 10.0.0 permanecen fuera de alcance.

## Administración de proveedores US1 2026-10-16

T011–T022 implementadas; se detiene antes de T023. Se mantuvieron T001–T010, la migración y las políticas existentes sin cambios. Pruebas de reglas, SQL/concurrencia y pantallas escritas primero; se observaron fallos de compilación/importación por los tipos y componentes todavía ausentes antes de implementarlos.

El incremento incorpora padrón paginado, filtros combinados literales, alta/edición por tipo, detalle, baja confirmada y reactivación separada e idempotente. Fletero guarda únicamente el vínculo para identidad/contacto; las lecturas proyectan los datos del Transportista. El selector externo usa búsqueda y páginas SQL de 20 sin filtrar por Activo, elimina selección fuera del conjunto y descarta respuestas tardías. En edición comienza por el CUIT actual. El contrato mínimo del selector no expone teléfono/email: el formulario informa dónde se mantienen; al editar el mismo vínculo muestra los datos de origen obtenidos en el detalle, siempre sin inputs de contacto propios.

Las escrituras de identidad y la modificación de Transportistas usan el coordinador de T008; las ediciones verifican Version y las bajas/reactivaciones toman la fila. La edición de Transportista que conserva su CUIT no agrega una prohibición a los cambios de contacto. El resultado existente de CUIT duplicado también traduce el conflicto cruzado. No se modificaron reglas de dependencia, pagos ni facturas.

Para que T022 deje operable el padrón con un rol autorizado, se agregó el mínimo de inicialización: sólo `proveedores.gestionar` a Administración y Administrador, su entrada de menú y agrupación en Padrones. No se sembraron permisos de facturas ni se anunciaron sus pantallas; T058 sigue pendiente para completarlos y probarlos cuando existan. El backend que ya estaba corriendo no se reinició: al reiniciarlo carga el código nuevo y ejecuta su sembrado habitual; volver a ingresar renueva el menú de sesión. No se alteraron procesos ni configuración global del entorno.

| Verificación ejecutada | Resultado |
|---|---|
| `dotnet build backend/GT.sln --no-restore -v quiet` | Aprobado; todos los proyectos, incluidos tests de integración. Advertencias previas de NuGet, nulabilidad y analizadores en archivos ajenos a US1. |
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~Proveedores -v quiet` | 18/18 aprobadas, incluidas las seis de fundamentos. |
| Suite completa de GT.UnitTests con `--no-build --no-restore -v quiet` | 516/518 aprobadas. Sólo fallan los dos casos previos de FuentesDeVencimientosTests de Reportes (Choferes/Flota, Próxima a vencer). No se tocaron. |
| Integración filtrada por PadronProveedorTests, ConcurrenciaIdentidadTests y Transportistas | 33 casos bloqueados al preparar el fixture original: exige GT_SQL_PASSWORD y servidor 127.0.0.1,1433 con sa. No llegaron a ejecutar aserciones. No se modificó ni se sustituyó la configuración de estos tests. |
| Verificación SQL independiente con repositorios reales, instancia `.\SQLEXPRESS02`, autenticación Windows | Aprobada. Sólo lectura inicial en GtLogistica; escrituras en una base `GtLogistica_US1_<guid>` creada y eliminada por el harness temporal. No se modificaron registros de desarrollo. |
| Casos SQL independientes | Alta normal/Fletero con transportista inactivo, identidad/contacto sin copia, baja conservada en edición, reactivación idempotente, Version obsoleta, duplicados propios/cruzados/inactivos, modificación de Transportista y contacto actualizado, contacto sin cambio de CUIT permitido sin vínculo, filtros y búsqueda literal. Páginas de 20/3 para 23 homónimos y 23 externos, incluidos inactivos; CHECK de razón social NULL/vacía/espacios. Carreras entre altas normalizadas, normal/Fletero y dos ediciones con igual Version: exactamente una confirma. No equivale a ejecutar toda la suite del fixture. |
| Frontend: `tsc -b`, `vite build`, `oxlint`, usando CLI locales con Node 24.21.0 por ruta absoluta | Aprobados; permanece aviso de bundle >500 kB. Sin instalación de bibliotecas ni cambios a manifiestos. |
| Frontend afectado: proveedores, transportistas y secciones de menú | 4 archivos, 17/17 tests aprobados. |
| Suite frontend completa: `vitest run` | 65 archivos, 500/500 tests aprobados. |

El harness y los logs temporales se retiraron al cerrar. Fallos nuevos de comportamiento pendientes: ninguno observado en las verificaciones ejecutadas. Limitación pendiente: ejecutar las pruebas de integración y regresión completas con un entorno compatible con su fixture; no afirmar que sus casos HTTP/concurrencia estén todos validados por la comprobación independiente.

Archivos creados en esta continuación:

- `backend/src/GT.Application/Proveedores/DtosProveedor.cs`
- `backend/src/GT.Application/Proveedores/ReglasProveedor.cs`
- `backend/src/GT.Application/Proveedores/IRepositorioProveedores.cs`
- `backend/src/GT.Application/Proveedores/GestionarProveedores.cs`
- `backend/src/GT.Infrastructure/Persistencia/RepositorioProveedores.cs`
- `backend/src/GT.Api/Proveedores/ProveedoresEndpoints.cs`
- `backend/tests/GT.UnitTests/Proveedores/ReglasProveedorTests.cs`
- `backend/tests/GT.IntegrationTests/Proveedores/PadronProveedorTests.cs`
- `backend/tests/GT.IntegrationTests/Proveedores/ConcurrenciaIdentidadTests.cs`
- `frontend/src/modules/proveedores/servicios/proveedores.ts`
- `frontend/src/modules/proveedores/paginas/PadronProveedores.tsx`
- `frontend/src/modules/proveedores/paginas/FormularioProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/DetalleProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/PadronProveedores.test.tsx`

Archivos existentes modificados en esta continuación:

- `backend/src/GT.Api/Program.cs` (DI y mapeo del padrón)
- `backend/src/GT.Infrastructure/Persistencia/RepositorioTransportistas.cs` (coordinación y conflicto cruzado)
- `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs` (sólo gestionar)
- `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs` (sólo padrón)
- `frontend/src/App.tsx` (rutas con sesión y permiso en formulario)
- `frontend/src/compartido/seccionesDeMenu.ts` (ubicación de Proveedores)
- `specs/013-gestion-proveedores/tasks.md`
- `specs/013-gestion-proveedores/quickstart.md`



Hooks before/after implement: ambos de commit opcionales, omitidos por la instrucción expresa de no hacer commit.

## Carga de factura normal US2 2026-10-16

T023–T036 completas; T037 no iniciada. Se implementó exclusivamente carga para Proveedor normal Activo. No se implementaron pagos, órdenes de pago, conciliaciones, cambios de estado, carga de Fletero ni selección/disponibilidad de liquidaciones. No se modificaron migraciones ni esquema existente. Los archivos del bloque previo se conservaron, salvo las integraciones explícitas de búsqueda mínima, rutas/DI, menú y moneda exigidas por US2.

T023–T026 se escribieron primero: reglas monetarias/fechas, casos SQL/multipart, compensación y autocomplete; se observaron fallos iniciales por tipos y pantallas ausentes. Luego se implementaron T027–T036. Los tests adicionales de T035 comprueban pérdida real del provider al desmontar el runtime y crear otro, Id diferente, ausencia de GET tras POST, ausencia del DTO en estado del router/Storage, fecha local de Argentina con Date falseado, rechazo de cero/negativo, error recuperable y retiro del PDF opcional.

Se reutiliza IVA de Facturación con decimal, 21% y redondeo comercial; neto y resultados deben caber en decimal(18,2). El converter decimal↔cadena F2 se aplica sólo a respuestas de este módulo y rechaza JSON number al deserializar. Multipart recibe texto canónico, sin double ni redondeo del neto. Frontend calcula en centavos BigInt y formatea cadenas con moneda compartida, preservando literalmente el camino previo para number. Las pruebas distinguen 100000000000000.01 y 100000000000000.02.

El backend valida/escribe el PDF completo antes de iniciar SQL; usa firma compartida restringida a application/pdf, máximo 10×1024×1024 y metadata 400/255/100 sin truncar. La transacción toma el coordinador de identidad y la fila de Proveedor Activo, relee tipo/identidad y congela razón social/CUIT. El número es único por proveedor, con índice como respaldo. Un rechazo anterior al commit compensa con CancellationToken.None; después del commit nunca borra por un fallo de relectura. Ante commit incierto verifica la referencia y conserva el PDF si no puede comprobarla; la consulta usa READCOMMITTEDLOCK para no tomar una foto anterior como ausencia en una base con RCSI. Los fallos de compensación sólo dejan huérfanos invisibles y se registran con el logging existente, mediante interfaz de aplicación sin biblioteca nueva.

POST responde 201/Location con detalle completo. El contexto React conserva sólo el último DTO, en memoria y con Id coincidente; navegar no transporta el DTO ni usa un GET. El provider se descarta al cerrar/cambiar usuario y desaparece al recargar el runtime. Si falta el resultado o cambia el Id, se ejecuta GET bajo consultar; el 403 indica solicitar acceso, sin reintento engañoso. No se usa localStorage, sessionStorage, IndexedDB ni history.state para el DTO.

Dependencias mínimas para hacer operable T034: se agregó el permiso de cargar y su entrada de menú a los roles previstos, sin sembrar consultar ni ampliar gestionar. Se incluyeron GET de detalle y PDF necesarios para recarga/ficha, ambos exclusivamente con consultar; no hay listado ni menú de consulta. Un usuario con sólo cargar puede recibir el DTO del POST y no puede invocar esos GET. Las operaciones sobre Fletero se rechazan aunque se invoque la ruta a mano. T037–T045 quedan pendientes.

| Verificación ejecutada | Resultado |
|---|---|
| `dotnet build backend/GT.sln -c Release --no-restore -v quiet` | Aprobado. Se usa Release porque GT.Api en ejecución bloquea DLLs de Debug; no se detuvo ni reinició el proceso de localhost:5005. Advertencias previas de paquetes/nulabilidad/analizadores en módulos ajenos. |
| `dotnet test backend/tests/GT.UnitTests/GT.UnitTests.csproj -c Release --no-build --no-restore --filter FullyQualifiedName~Proveedores -v quiet` | 41/41 aprobadas; 23 nuevas de US2. Incluyen bordes monetarios/fechas, converter, escritura fallida, borrado fallido, cancelación, relectura postcommit, commit incierto y metadata. |
| Suite unitaria completa Release | 539/541 aprobadas. Únicamente los dos fallos preexistentes de Reportes/FuentesDeVencimientosTests, Choferes/Flota: esperaba Próxima a vencer y obtuvo Vencida. No se tocaron. |
| GT.IntegrationTests Release, filtro CargarFacturaProveedorTests | 18 casos compilados, bloqueados en preparación por GT_SQL_PASSWORD/sa/127.0.0.1,1433. No llegaron a sus aserciones. No se modificó AplicacionDePrueba ni ConfiguracionEntorno para el SQL local. |
| Host HTTP temporal independiente, API y DI reales, SQL Server `.\SQLEXPRESS02` con autenticación Windows | Aprobado con base `GtLogistica_US2_<guid>` y volumen temporal propios, ambos eliminados al terminar. GtLogistica y el backend local no se modificaron. El host independiente no equivale a ejecutar la suite original. |
| Multipart/serialización/permisos/PDF del host temporal | Login/cookies reales. Proveedor creado por API; luego sesión reducida a un solo permiso cargar dentro de la base aislada: búsqueda mínima y POST permitidos, GET detalle/PDF 403. Ambos importes grandes vuelven como cadenas con sus centavos exactos. El PDF conserva todos los bytes; con consultar agregado sólo al fixture aislado, GET devuelve importes exactos y PDF inline/nosniff. |
| Concurrencia, compensación y CHECK en SQL local | Dos POST simultáneos del mismo número dejan exactamente una fila y un PDF. PDF renombrado, >10 MB, neto inválido, JSON numérico, asociación enviada a normal, proveedor dado de baja y Fletero bloqueado se rechazan sin fila parcial/PDF residual. Metadata 255 aceptada/256 rechazada sin truncar. Rechazo real de CHECK Archivo con NULL y de Importes, Estado, Fechas, Textos y Liquidacion, una restricción por caso. Snapshot conservado al editar el origen. Referencia confirmada/ausente consultada con el mecanismo usado ante commit incierto. |
| Frontend con Node 24.21.0: CLI locales `tsc -b`, `vite build`, `oxlint` | Aprobados. Sin advertencias nuevas de lint; aviso previo de bundle >500 kB. No se instalaron bibliotecas ni cambiaron manifiestos. |
| `vitest run --maxWorkers=4` completo | 68 archivos, 523/523 tests aprobados. Incluye regresión sin cambios de los callers number de moneda y de todos los módulos anteriores. |
| Revisión de Git/ignores | `git diff --check` sin errores; ignores existentes cubren artefactos y temporales, sin modificaciones necesarias. |

Durante la revisión apareció un borde real: InputImporte elimina signos al formatear, de modo que -100 podía convertirse en +100. Se fijó primero el caso fallido y se corrigió sólo esta pantalla capturando la entrada antes del formato; no se cambió el componente compartido ni el comportamiento previo de otros módulos. También se fijó el borde de flecha arriba desde ninguna opción activa del autocomplete. Una corrida frontend sin limitar workers mostró dos fallos en EmpresaEmisora; ese archivo pasó aislado sin cambios, y la corrida final completa con cuatro workers pasó todos los casos. No se modificaron tests anteriores para ocultar regresiones.

Pendiente de entorno: ejecutar la suite de integración original en un servidor compatible con su fixture. No hay fallos nuevos pendientes observados en las verificaciones finales ejecutadas. Las pruebas de concurrencia de baja/edición están escritas y compiladas en el fixture original; la validación HTTP local independiente cubrió baja y edición posteriores, además del duplicado simultáneo, y no se presenta como ejecución de todos esos casos originales.

Archivos creados en US2:

- `backend/src/GT.Domain/Proveedores/ReglasFacturaProveedor.cs`
- `backend/src/GT.Application/Proveedores/DtosFacturaProveedor.cs`
- `backend/src/GT.Application/Proveedores/IRepositorioFacturasProveedor.cs`
- `backend/src/GT.Application/Proveedores/PrepararPdfFacturaProveedor.cs`
- `backend/src/GT.Application/Proveedores/CargarFacturaProveedor.cs`
- `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturasProveedor.cs`
- `backend/src/GT.Infrastructure/Persistencia/RegistroErroresFacturaProveedor.cs`
- `backend/src/GT.Api/Proveedores/ImporteDecimalJsonConverter.cs`
- `backend/src/GT.Api/Proveedores/RespuestasProveedor.cs`
- `backend/src/GT.Api/Proveedores/FacturasProveedorEndpoints.cs`
- `backend/tests/GT.UnitTests/Proveedores/ReglasFacturaProveedorTests.cs`
- `backend/tests/GT.UnitTests/Proveedores/AtomicidadFacturaProveedorTests.cs`
- `backend/tests/GT.IntegrationTests/Proveedores/CargarFacturaProveedorTests.cs`
- `frontend/src/modules/proveedores/componentes/AutocompleteProveedor.tsx`
- `frontend/src/modules/proveedores/componentes/AutocompleteProveedor.test.tsx`
- `frontend/src/modules/proveedores/componentes/ResultadoFacturaContexto.tsx`
- `frontend/src/modules/proveedores/componentes/resultadoFactura.ts`
- `frontend/src/modules/proveedores/servicios/facturasProveedor.ts`
- `frontend/src/modules/proveedores/servicios/importesFactura.ts`
- `frontend/src/modules/proveedores/servicios/importesFactura.test.ts`
- `frontend/src/modules/proveedores/paginas/CargarFacturaProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/DetalleFacturaProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/CargarFacturaProveedor.test.tsx`

Archivos modificados en US2:

- `backend/src/GT.Api/Program.cs`
- `backend/src/GT.Api/Proveedores/ProveedoresEndpoints.cs`
- `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`
- `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`
- `frontend/src/App.tsx`
- `frontend/src/compartido/moneda.ts`
- `frontend/src/compartido/moneda.test.ts`
- `frontend/src/compartido/seccionesDeMenu.ts`
- `specs/013-gestion-proveedores/tasks.md`
- `specs/013-gestion-proveedores/quickstart.md`

El harness y logs temporales se retiraron al cerrar. Hooks before/after implement de commit opcionales omitidos por instrucción expresa. Sin Docker ni commit. Para usar el código nuevo en el backend que ya estaba abierto, reiniciarlo y volver a ingresar para renovar los permisos/menú de sesión.

## Preparación

Node 24, .NET 10 y Podman según el entorno del proyecto. Configurar `.env` a partir de `.env.template` con valores locales; no pegar secretos en documentación ni comandos versionados. SQL Server debe estar accesible; `ConfiguracionEntorno` de los tests ya lee GT_SQL_PASSWORD desde entorno o .env y crea bases aisladas.

Desde raíz:

```powershell
podman compose up -d
```

Si las imágenes no contienen la implementación nueva, reconstruir mediante `podman compose up -d --build`. Ante fallo DNS seguir la corrección de AGENTS; no modificar compose para esta feature.

Desde backend:

```powershell
dotnet test
```

Desde frontend:

```powershell
npm test
npm run build
npm run lint
```

Resultado esperado: suites nuevas de Proveedores y regresión de Transportistas, Liquidaciones, Facturación, adjuntos y menú verdes. No ajustar aserciones antiguas para esconder cambios no autorizados.

## Datos de aceptación

Sesión Administración de la empresa, sesión sin permisos y Administrador. Un proveedor normal Activo y uno Inactivo con CUIT válidos distintos; un transportista externo del padrón con empresa emisora configurada; dos proveedores Fleteros de distintos transportistas. Preparar liquidaciones reales mediante el flujo del Módulo 9: Pendiente disponible de $121.000,00, una ya asociada, una pagada, una anulada y una de otro transportista. No inventar datos de pagos en esta feature: cualquier preparación usa las operaciones existentes. PDF pequeño válido, un JPG renombrado .pdf y archivo > 10 MB.

## Recorridos observables

1. Crear normal completando sólo obligatorios. Crear Fletero seleccionando transportista y comprobar que identidad/contacto vienen del origen sin permitir otra edición. Cambiar contacto en Transportistas y volver a abrir proveedor: refleja la fuente. Editar sólo datos propios del proveedor.
2. Intentar CUIT repetido activo/inactivo y con separadores distintos; se rechaza sin duplicar. Modificar CUIT de un Transportista vinculado hacia el de otro proveedor: se rechaza y conserva el original.
3. Buscar por razón social, nombre comercial y CUIT; combinar tipo/estado y recorrer >20 resultados. Estados vacíos diferencian padrón sin registros y filtros sin coincidencias.
4. Cargar factura normal por autocomplete, neto $100.000,00, fecha de hoy Argentina y CAE con vencimiento igual a fecha: IVA $21.000,00, total $121.000,00, sin selector de liquidación. Guardar sin PDF, queda Pendiente y navega a detalle. No hay acción de pagar.
5. Intentar neto vacío, $0,00 y negativo mediante petición directa de pruebas: rechazos. $0,01 se acepta para normal; $0,50 muestra IVA $0,11 y total $0,61. Mañana y vencimiento anterior se rechazan.
6. Cargar Fletero: sólo liquidaciones de su transportista, Pendientes y libres. Sin elegir o sin disponibles no guarda. Elegir $121.000,00 con neto $100.000,00 guarda; diferencia de un centavo rechaza mostrando ambos valores. Cambiar proveedor limpia liquidación; normal rechaza vínculo enviado directamente.
7. Adjuntar PDF: guardar y abrir en navegador con nombre original. JPG renombrado y exceso de tamaño rechazan sin factura. Probar desde usuario sin permisos que el PDF no es accesible por URL.
8. Consultar por fechas con registros en ambos extremos y fuera; combinar proveedor y Pendiente, limpiar filtros. Autocomplete incluye Inactivos. Abrir ficha, cotejar todos los campos e identificación histórica.
9. Dar de baja proveedor: sale de nuevas cargas y conserva facturas. Cancelar baja no altera estado. Editar datos no reactiva; reactivar separadamente sí. Cambiar tipo/razón social/CUIT del origen no altera identificación ni asociación de factura previa.
10. Operar liquidación ya asociada desde el módulo autorizado: factura no cambia importes ni estado y muestra importe al asociar y situación actual, sin introducir restricciones nuevas a Módulo 9.
11. Recorrer con teclado autocomplete, filtros, select de liquidación y diálogos. Escape/Tab no retienen foco en sugerencias; confirmación sí usa diálogo existente. Ninguna fila navegable agrega tabIndex a la fila. Verificar mensajes de carga/vacío y 403 que indica pedir permiso.

## Fallos y concurrencia (automatizados con SQL real)

- Dos altas mismo CUIT y carreras cruzadas normal/Fletero/modificación de Transportista: sólo una identidad efectiva se confirma. También incluir inactivos y conservar CUIT propio.
- CUIT inicialmente inexistente en ambos padrones: iniciar simultáneamente alta/edición de Proveedor y modificación de Transportista hacia ese CUIT; probar variantes con separadores que normalizan al mismo valor. Verificar que usan el mismo recurso `GT.Proveedores.Cuit:<onceDigitos>` y esperan aun sin fila. En cambios que afectan CUIT anterior y nuevo, comprobar adquisición única en orden ordinal tras la coordinación global, antes de locks de filas, sin carreras ni inversiones entre fuentes.
- Dos ediciones de proveedor: segunda Version obsoleta rechazada; edición/baja contra carga no deja factura de un proveedor ya Inactivo en el orden serializado.
- Dos facturas mismo proveedor/número y dos facturas misma liquidación: exactamente una, sin factura parcial ni vínculo reservado por fracaso.
- Asociación frente a editar/anular/pago total: resultado acorde al orden de locks; total/estado se verifican después del bloqueo. Pago parcial conserva elegibilidad; cambio posterior al commit no modifica factura.
- Escritura PDF falla: ninguna fila; SQL rechaza tras escritura: archivo compensado; fallo de relectura tras commit: PDF no se borra. Commit incierto: no borrar sin comprobar rollback/existencia. Fallo de compensación: sólo huérfano invisible, según AGENTS.
- CHECK con columnas NULL, no sólo vacías, y fixtures que violan una restricción cada vez. DI real para carga y servir archivo con cabeceras inline/nosniff.
- Metadata de archivo: comprobar HasMaxLength/columnas nvarchar(400), nvarchar(255), nvarchar(100), iguales a documentación de Choferes/Flota. Validar bordes de capacidad y rechazo sin truncamiento ni factura parcial cuando se exceden.
- Permisos aislados y sin sesión: catálogo sólo ofrece la pantalla correspondiente; autocompletes no otorgan gestionar de otros módulos ni documentos.
- Usuario con sólo proveedores.facturas.cargar: POST devuelve 201 con detalle completo y la UI lo muestra desde un contexto/store exclusivo de memoria React con Id coincidente, sin GET automático ni DTO en location.state/history.state; GET del detalle (incluso recién creado), listado y PDF devuelven 403. Recargar completamente con un runtime/contexto nuevo pierde necesariamente el DTO y ejecuta GET que devuelve 403, mostrando falta de permiso; un Id diferente también ejecuta GET. No rehidratar DTO desde localStorage, sessionStorage, IndexedDB, history.state ni location.state. Usuario con sólo proveedores.facturas.consultar: GET de detalle/listado/PDF permitido, POST 403. No hay implicación automática entre ambos permisos.

## Medición y criterio de cierre

Padrón de 1.000 proveedores: medir desde fin de escritura hasta coincidencias visibles en al menos 100 búsquedas por los tres datos; 95% <2 segundos, incluyendo debounce, red y render. No transformar esa prueba en precarga de todo el padrón. Revisar contraste sobre los fondos usados con tokens restaurados; escritorio 1280 px y zoom/teclado conforme al sistema existente.

Los siete SC de la spec deben verificarse por sus recorridos y evidencia de tests; no declarar implementada la feature sólo por compilar. La planificación termina con artefactos de diseño; tareas se generan posteriormente con `$speckit-tasks`.

## Verificación de ajustes I1, I2 y U1

- I1: con sólo cargar, guardar y ver el DTO en contexto React sin GET; abrir Id distinto dispara GET→403. Hacer recarga real (nuevo runtime/contexto, no simple rerender con el mismo provider), comprobar memoria vacía y GET→403; inspeccionar que router/history/storage no contienen DTO. Cerrar/cambiar sesión limpia el último resultado. Con consultar, recarga obtiene detalle por GET.
- I2: multipart envía neto invariant como texto. Probar por separado 100000000000000.01 y 100000000000000.02, verificar conservación exacta a través de decimal C#, POST, GET, lista/detalle, preview y formato. Las respuestas de todos los importes, incluidos total disponible, histórico, actual y conflictos, deben ser cadenas canónicas de dos decimales. No convertir el importe completo a number. Regresión sin modificar de los callers number de compartido/moneda más pruebas nuevas de su sobrecarga de cadena.
- U1: con al menos 21 transportistas externos, avanzar desde página 1 a 2 y elegir el originalmente 21. El select nativo contiene sólo los 20 (o restantes) de la página; Anterior/Siguiente y búsqueda son operables por teclado. Cambiar q vuelve a página 1 y limpia TransportistaId si queda fuera del conjunto; probar carga, vacío, fallo recuperable y respuesta tardía. En edición, q inicial con CUIT actual recupera la selección sin descargar todo. No transformar este control en autocomplete.

## Factura de Fletero US3 — T037–T045 — 2026-10-17

Se implementó exclusivamente US3. T037–T044 completas; T045 queda parcialmente validada por la limitación del fixture SQL original indicada debajo. T046 no fue iniciada. No se agregaron funcionalidades de pagos, órdenes de pago ni conciliaciones, ni restricciones nuevas a las operaciones existentes de Liquidaciones. No se usó Docker, no se hizo commit y no se reiniciaron los servicios locales. Los apartados US1/US2 anteriores describen sus respectivos checkpoints históricos.

T037–T039 se escribieron y ejecutaron antes de implementar: cinco casos frontend inicialmente fallidos por ausencia del selector; ocho casos SQL iniciales bloqueados al preparar el fixture del compose. Luego se completaron backend/selector y se ampliaron las pruebas hasta doce casos SQL y siete frontend específicos de Fletero.

Disponibles exige exclusivamente `proveedores.facturas.cargar`, fuera del grupo que requiere gestionar. Devuelve sólo Pendientes del Transportista externo vinculado sin factura asociada, incluyendo liquidaciones con pagos parciales y comparando el total, nunca el saldo. Normal devuelve colección vacía; al guardar rechaza cualquier asociación. No se filtra por actividad del Transportista: el estado propio del proveedor conserva su regla anterior.

La transacción conserva coordinador global/CUIT → proveedor activo → Transportista → lectura estable de EmpresaEmisora → UPDATE condicional de Liquidacion por Id/Transportista/Pendiente. Después del lock relee total y disponibilidad; guarda factura/FK/snapshot juntos y mantiene el índice único filtrado existente como respaldo. Diferencia de importes responde 409 con mensaje del servidor y ambos valores exactos. No acepta una confirmación que saltee la igualdad. La liquidación no cambia estado, total ni pagos por asociarla.

El PDF conserva la preparación y compensación de US2, antes de SQL y con borrado sólo ante fallo previo al commit. El selector nativo muestra número/período/importe, estados de carga/vacío/error, descarta respuestas tardías y limpia la selección al cambiar proveedor. Bloquea guardar sin una opción válida; los conflictos conservan el formulario y muestran importes recibidos del servidor sin convertirlos a number. La ficha mínima de resultado muestra la asociación del POST desde memoria React, sin GET automático ni DTO en router/storage. No se agregó listado ni otra funcionalidad de US4.

| Verificación ejecutada | Resultado |
|---|---|
| `dotnet test backend/tests/GT.UnitTests -c Release --filter "FullyQualifiedName~Proveedores\|FullyQualifiedName~Liquidaciones" --no-restore` | 71/71 aprobadas, incluidas compensación/commit incierto de T025 y regresión unitaria de Liquidaciones. |
| Suite unitaria completa Release, sin recompilar/restaurar | 539/541 aprobadas. Sólo los dos fallos preexistentes de `FuentesDeVencimientosTests`, Choferes y Flota con `vencida:false`: esperaba Próxima a vencer, obtuvo Vencida. No se modificaron. |
| Integración original: Liquidaciones + CargarFacturaProveedorTests + AsociarLiquidacionTests + ConcurrenciaLiquidacionFacturaTests | 120 casos compilados; todos bloqueados en preparación por falta de `GT_SQL_PASSWORD` y fixture exclusivo de sa/compose. Incluye 90 de regresión de Liquidaciones, 18 de T024 y 12 de US3. Ninguna aserción SQL de esa corrida llegó a ejecutarse. Fixture y tests anteriores sin adaptación artificial. |
| Escenarios nuevos compartidos con T037/T038, ejecutados por runner HTTP independiente temporal | Los 12 escenarios pasaron contra una base `GtLogistica_US3_<guid>` creada en `.\SQLEXPRESS02` con Windows Authentication. Host real con migraciones, DI y almacén privado; sin sustituir repositorios ni modificar la base GtLogistica. Se eliminaron bases y volúmenes temporales al terminar. No equivale a aprobar la suite original completa. |
| Cobertura SQL local de US3 | Selección obligatoria, normal con asociación rechazado, ajena, estados no Pendiente, ausencia de disponibles y Transportista que dejó de ser externo; total diferente con importes canónicos; parcial elegible por total; exclusividad ante dos cargas simultáneas; PDF/fila/FK sin resultado parcial; asociada deja de ofrecerse. Espera controlada del lock compartido y revalidación de edición/anulación/pago total/parcial. APIs existentes invocadas tanto después como concurrentemente: conservan sus reglas y el snapshot de factura. |
| Precisión SQL/HTTP | Neto `100000000000000.01` y `.02` produce totales distintos `121000000000000.01` y `.02`; disponibles, POST histórico/actual y conflictos como cadenas de dos decimales. Cambio posterior conserva histórico y distingue importe actual. |
| `dotnet build backend/GT.sln -c Release --no-restore --verbosity quiet` | Aprobado, cero errores. Release evita las DLL Debug bloqueadas por el backend local. Persisten advertencias previas de paquetes y disponibilidad del servicio de auditoría NuGet. |
| Frontend: Node 24.21.0, CLI locales `tsc -b`, `vite build`, `oxlint` | Aprobados. Se conserva el aviso de tamaño de bundle >500 kB. Sin dependencias nuevas. |
| `vitest run --maxWorkers=4` completo | 69 archivos y 530/530 tests aprobados. Incluye T026/T035, siete casos nuevos de Fletero y regresión frontend de Liquidaciones. |

No se observaron fallos nuevos pendientes en las verificaciones ejecutables. **Limitación de T045**: la regresión SQL original completa queda pendiente de ejecutar en un entorno compatible con su fixture. La tarea permanece sin marcar, con la ejecución intentada y la validación local complementaria documentadas; no se declaran aprobados esos 120 casos bloqueados. Los dos errores de Reportes son distintos de esta limitación de preparación.

Archivos de este bloque (se preservaron los demás cambios previos del workspace):

- Creado: `backend/src/GT.Application/Proveedores/ConsultarLiquidacionesDisponibles.cs`.
- Creados: `backend/tests/GT.IntegrationTests/Proveedores/AsociarLiquidacionTests.cs` y `ConcurrenciaLiquidacionFacturaTests.cs`.
- Creado: `frontend/src/modules/proveedores/paginas/CargarFacturaFletero.test.tsx`.
- Modificados: `backend/src/GT.Api/Program.cs`, `backend/src/GT.Api/Proveedores/ProveedoresEndpoints.cs` y `RespuestasProveedor.cs`.
- Modificados: `backend/src/GT.Application/Proveedores/CargarFacturaProveedor.cs` e `IRepositorioFacturasProveedor.cs`.
- Modificado: `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturasProveedor.cs`.
- Modificado: `backend/tests/GT.UnitTests/Proveedores/AtomicidadFacturaProveedorTests.cs` (implementación de la nueva consulta en el doble; aserciones previas conservadas).
- Modificados: `frontend/src/modules/proveedores/paginas/CargarFacturaProveedor.tsx`, `CargarFacturaProveedor.test.tsx` y `DetalleFacturaProveedor.tsx`. La aserción anterior de Fletero deshabilitado era temporal de US2; se conserva el caso de normal sin selector y se cubre Fletero en su suite nueva.
- Modificados: `frontend/src/modules/proveedores/servicios/facturasProveedor.ts` y `frontend/src/compartido/tipos.ts`.
- Modificados: `specs/013-gestion-proveedores/tasks.md` y `quickstart.md`.

## Consulta de facturas US4 — T046–T054 — 2026-10-17

Se implementó exclusivamente GFP_02. T045 permanece sin marcar: no se ejecutó satisfactoriamente la regresión SQL original de Liquidaciones. T055 y las tareas posteriores no se iniciaron. No se agregaron pagos, órdenes de pago, conciliaciones, edición/anulación de factura, exportación ni estados nuevos. Sin Docker, commit ni cambios de migraciones. Se conservaron los cambios previos del workspace y los servicios locales en ejecución.

T046–T048 se escribieron y ejecutaron antes de implementar. El listado frontend inicialmente falló por pantalla ausente; la ficha aún no ofrecía referencias por permiso. Los dos casos SQL compilaron pero quedaron bloqueados en preparación por el fixture original. T049–T053 completan el listado y reutilizan la ficha/contexto existentes, sin rehacer US2/US3.

Listado con filtros combinables desde/hasta/proveedor por Id/estado; fechas DateOnly inclusivas e independientes, rango invertido y valores inválidos con 400. Pendiente es el único estado. Count y filtros se ejecutan en SQL antes de Skip/Take; 20 filas, Fecha DESC/Id DESC. Una página positiva fuera de rango devuelve colección vacía sin desbordar el offset. La identidad mostrada es el snapshot de factura, aunque el proveedor actual cambie o esté inactivo. El autocomplete existente usa soloActivos=false; texto sin selección no filtra por proveedor y Limpiar elimina también su texto. Hay resumen de filtros aplicados, carga/error/reintento, vacíos distintos, enlaces reales, estados anunciados y ninguna acción primaria artificial.

La ficha conserva razón social, CUIT y tipo históricos; muestra importe total destacado y, para Fletero, importe de asociación separado del importe/estado actuales. Referencias a proveedor y liquidación únicamente con gestionar de Proveedores y consultar de Liquidaciones, respectivamente. El resultado del POST sigue siendo exclusivamente el último DTO en memoria React por Id coincidente, sin GET automático ni escritura/rehidratación en router, history, localStorage, sessionStorage o IndexedDB. Un runtime nuevo o Id distinto usa GET y deja que consultar sea exigido por el servidor, incluso si sólo tiene cargar y recibe 403.

GET listado/detalle/PDF exige exclusivamente proveedores.facturas.consultar. El caso AbrirPdfFacturaProveedor reutiliza almacén privado y ResultadoArchivo.EnLinea: bytes originales, application/pdf, nombre original, inline y nosniff; ausencia devuelve 404 y ninguna ruta física viaja al cliente. Cargar no habilita PDF. Para verificar los GET se otorgó consultar sólo al rol de la base temporal de prueba; **no se adelantó la siembra ni el menú previstos en T058**. La ruta de consulta existe en App y el servidor devuelve 403 real cuando falta el permiso.

La prueba local detectó un defecto nuevo: el binding de una fecha inválida terminaba en 500 por el middleware global de errores. Se corrigió sólo el endpoint nuevo, parseando fechas e identificadores/página con errores de campo 400. Se verificaron fecha imposible, texto inválido, rango invertido y páginas inválidas, sin cambiar el middleware ni otros módulos.

| Verificación | Resultado |
|---|---|
| Unitarios afectados: `dotnet test backend/tests/GT.UnitTests -c Release --no-restore --filter FullyQualifiedName~Proveedores` | 41/41 aprobados; el doble de Atomicidad implementa la nueva consulta sin cambiar sus aserciones. |
| Unitarios completos Release | 539/541 aprobados. Sólo los dos fallos preexistentes de Reportes/FuentesDeVencimientosTests (Choferes y Flota, vencida:false), esperaba Próxima a vencer y obtuvo Vencida. No se modificaron. |
| `dotnet build backend/GT.sln -c Release --no-restore --verbosity quiet` | Aprobado, cero errores. Advertencias previas de paquetes/auditoría NuGet. Release evita las DLL Debug del backend local en ejecución; no se reinició. |
| Integración original, filtro ConsultarFacturasProveedorTests, antes y después de implementar | Dos casos compilados; bloqueados en preparación por GT_SQL_PASSWORD/sa/compose. No se modificó su fixture ni se declaran aprobadas sus aserciones en esa corrida. T045 conserva su limitación anterior. |
| Escenarios SQL/HTTP compartidos de T046 en runner temporal independiente | Listado y detalle aprobados con host real, migraciones, DI, cookies y SQL Server en una base GtLogistica_US4_<guid> de SQLEXPRESS02 con Windows Authentication. Misma lógica de aserciones de los tests nuevos, sin cambiar la infraestructura original ni la base GtLogistica. Bases y volúmenes temporales eliminados, también después del primer fallo corregido. |
| Cobertura SQL local | Filtros combinados, cuatro bordes de fechas, rangos abiertos/invertidos, página 20 y desempate Id, proveedor inactivo, snapshot de nombre/CUIT/tipo tras cambios del origen; GET normal y Fletero con histórico 121000000000000.02 frente a actual .03 y estado Anulada. PDF original, inline/nombre/nosniff, sin PDF 404 y cargar sin consultar 403 para los tres GET. Importes de listado/detalle como cadenas exactas, distinguiendo los centavos grandes. |
| Frontend afectados: `vitest run src/modules/proveedores --maxWorkers=4` | 7 archivos, 47/47 aprobados; incluye alta sin GET automático, recarga/runtime nuevo/Id distinto, router/storage/IndexedDB ignorados, sólo cargar sin enlace de PDF, referencias según permisos y los seis recorridos de listado. |
| Frontend completo y build/lint finales | `vitest run --maxWorkers=4`: 71 archivos y 540/540 tests aprobados. Node 24.21.0 con CLI locales `tsc -b`, `vite build` y `oxlint`: aprobados; lint exit 0. Se conserva el aviso previo de bundle >500 kB. Sin dependencias nuevas. |
| Regresión local contractual compartida con US3 | Dos escenarios adicionales aprobados para 100000000000000.01 y .02: disponibles/POST/conflictos con cadenas canónicas distintas; no sustituye la regresión original pendiente de T045. |

T046–T054 completas y marcadas. No quedan fallos nuevos pendientes observados en las verificaciones finales ejecutadas. Los dos errores de Reportes se separan de la limitación de preparación del fixture SQL original. No se presenta la validación independiente como aprobación de ese fixture ni de T045. La validación de GFP_02 se realizó mediante SQL/HTTP real y recorridos automatizados de UI, sin afirmar una revisión manual de navegador no ejecutada. Los temporales del runner y sus volúmenes/bases fueron eliminados; no se modificaron la conexión del backend local ni los datos de GtLogistica.

Archivos creados en este bloque:

- `backend/src/GT.Application/Proveedores/ConsultarFacturasProveedor.cs`
- `backend/src/GT.Application/Proveedores/AbrirPdfFacturaProveedor.cs`
- `backend/tests/GT.IntegrationTests/Proveedores/ConsultarFacturasProveedorTests.cs`
- `frontend/src/modules/proveedores/paginas/ConsultarFacturasProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/ConsultarFacturasProveedor.test.tsx`
- `frontend/src/modules/proveedores/paginas/DetalleFacturaProveedor.test.tsx`

Archivos modificados en este bloque:

- `backend/src/GT.Api/Program.cs`
- `backend/src/GT.Api/Proveedores/FacturasProveedorEndpoints.cs`
- `backend/src/GT.Application/Proveedores/IRepositorioFacturasProveedor.cs`
- `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturasProveedor.cs`
- `backend/tests/GT.UnitTests/Proveedores/AtomicidadFacturaProveedorTests.cs`
- `frontend/src/App.tsx`
- `frontend/src/modules/proveedores/paginas/DetalleFacturaProveedor.tsx`
- `frontend/src/modules/proveedores/servicios/facturasProveedor.ts`
- `frontend/src/modules/proveedores/servicios/tipos.ts`
- `specs/013-gestion-proveedores/tasks.md`
- `specs/013-gestion-proveedores/quickstart.md`

## Permisos y menú US5 — T055–T061 — 2026-10-17

Se implementó exclusivamente US5. T045 permanece sin marcar por su regresión SQL original pendiente. T062 y siguientes no se iniciaron. Sin Docker, commit, pagos, órdenes de pago ni conciliaciones nuevas. Los servicios locales no se reiniciaron y la base GtLogistica no se modificó durante la validación aislada; la nueva siembra se aplica al iniciar el backend actualizado.

T055–T057 se escribieron y ejecutaron antes del cambio de producción. El test de menú aislado de consultar fallaba por entrada ausente; dos tests frontend fallaban por agrupación de consulta y alta visible en el padrón sin gestionar. Los once casos de integración originales compilaron, pero quedaron bloqueados al preparar el fixture con sa/GT_SQL_PASSWORD/compose.

La siembra incorpora explícitamente proveedores.facturas.consultar al catálogo y a Administración de la empresa y Administrador del sistema, junto con gestionar/cargar ya existentes. Es aditiva e idempotente; conserva asignaciones previas y roles personalizados. No agrega ninguno de estos permisos a Tráfico ni Gerencia. CatalogoOpcionesMenu registra las tres pantallas implementadas; consultar expone exclusivamente /proveedores/facturas. SeccionesDeMenu agrupa el padrón en Padrones y ambas opciones de factura en Operación, sin generar opciones ni perder el fallback desconocido.

Auditoría T060: el grupo de ABM y transportistas-externos requiere gestionar; busqueda tiene la política OR explícita gestionar/cargar/consultar; disponibles y POST factura requieren sólo cargar; los tres GET de factura requieren sólo consultar. No hay chequeos de rol, exenciones para administrador, autorización escondida en handlers ni GET HTTP posterior al POST. No fue necesario ampliar ni modificar estas políticas. Una referencia por Id no saltea autorización.

App continúa usando sesión/permisos/opciones del servidor. El padrón ahora recibe gestionar para no ofrecer Nuevo proveedor ni acciones de fila a quien no lo tiene. La autorización efectiva sigue siendo el 403 del backend. Se conservan POST→DTO en memoria sin GET/PDF para sólo cargar, recarga/URI directa→GET→403, y limpieza del resultado al cerrar sesión e ingresar con otro usuario en el mismo runtime. Buscar un Fletero consulta únicamente los datos mínimos/disponibles y no ofrece documentos de Choferes ni enlaces ajenos.

La prueba anterior MultipartRealDevuelveDetalleCompletoEImportesExactos se prepara ahora con un rol personalizado de único permiso cargar, manteniendo sus aserciones de PDF 403. Antes usaba administrador porque todavía no recibía consultar: conservarlo habría convertido una prueba de cargar en una de administrador después de T058. No se adaptó la conexión ni el fixture original.

Matriz SQL/API validada con cookies reales: diez perfiles, trece operaciones, **130 verificaciones de estado**. Los siete accesos de ABM son listado, ficha, alta, edición, baja, reactivación y selección de Transportista externo. Permitido significa 200 o 201 según la operación; las escrituras autorizadas reciben datos válidos y se ejecutan realmente en la base temporal.

| Perfil | ABM (7) | Búsqueda mínima | Disponibles | POST factura | GET lista/detalle/PDF (3) |
|---|---|---|---|---|---|
| Sin sesión | 401 | 401 | 401 | 401 | 401 |
| Sin permisos / sólo permisos ajenos / Tráfico / Gerencia | 403 | 403 | 403 | 403 | 403 |
| Sólo gestionar | 200/201 | 200 | 403 | 403 | 403 |
| Sólo cargar | 403 | 200 | 200 | 201 + detalle completo | 403 |
| Sólo consultar | 403 | 200 | 403 | 403 | 200 |
| Administración / Administrador | 200/201 | 200 | 200 | 201 | 200 |

Se verificó además que sólo cargar/consultar no permite GET de Transportistas; las búsquedas no exponen contacto/dirección/documentos. Cada sesión devuelve exactamente sus permisos de Proveedores y las entradas correspondientes. Dos resiembras antes de la matriz y otras dos después preservaron roles personalizados/asignaciones anteriores, sin duplicar permisos o vínculos. Los tres permisos son independientes: los dos roles autorizados reciben tres asignaciones explícitas, no implicaciones entre códigos.

| Verificación | Resultado |
|---|---|
| T055 + unitarios afectados, filtro CatalogoOpcionesMenuTests o Proveedores | 52/52 aprobados; incluye regresión de menú anterior. |
| Unitarios backend completos Release | 543/545 aprobados. Sólo los dos fallos preexistentes de Reportes/FuentesDeVencimientosTests, Choferes y Flota con vencida:false: esperaba Próxima a vencer, obtuvo Vencida. No se modificaron. |
| `dotnet build backend/GT.sln -c Release --no-restore --verbosity quiet` | Aprobado, cero errores. Release conserva el backend Debug local en ejecución; advertencias previas de paquetes/auditoría NuGet. |
| Integración original, filtro PermisosProveedorTests, antes y después de implementar | Once casos compilados, bloqueados en preparación por GT_SQL_PASSWORD/sa/compose; no ejecutaron sus aserciones y no se declaran aprobados en ese fixture. |
| Matriz local aislada compartida con T056 | Aprobada: mismos escenarios con host real, DI, SQL Server, migraciones, siembra, almacén privado, cookies y revalidación de sesión. Base GtLogistica_US5_<guid> en SQLEXPRESS02 con Windows Authentication; base y volumen temporal eliminados al terminar. No equivale a ejecutar la regresión pendiente de T045. |
| T057 | Once recorridos de App aprobados, con roles deliberadamente contradictorios respecto de los permisos, menú, URI sin sesión, 403 sin reintento, Fletero sin documentos, POST sin GET/storage/router/PDF y cambio de usuario sin rehidratar DTO. |
| Frontend afectados, Proveedores + seccionesDeMenu.test.ts | 9 archivos y 63/63 tests aprobados, incluida regresión del fallback y del menú previo. |
| Frontend build y lint | `tsc -b`, `vite build`, `oxlint`, con Node 24.21.0: aprobados, lint exit 0. Sin dependencias nuevas; permanece el aviso previo de bundle >500 kB. |
| Frontend completo y diff final | `vitest run --maxWorkers=4`: 72 archivos, 551/551 tests aprobados. `git diff --check` sin errores; sólo avisos de normalización LF/CRLF ya presentes en el workspace. |

T055–T061 completas y marcadas. No quedan fallos nuevos pendientes observados en las verificaciones finales. Los dos errores preexistentes de Reportes son distintos del bloqueo de preparación del fixture de integración original. Se omitieron los hooks opcionales de commit por instrucción del usuario. T045 sigue sin marcar y no se inició T062. No se afirma una prueba manual de navegador no ejecutada; los recorridos de App se verificaron con Testing Library y la matriz con HTTP/SQL reales. Los temporales del runner local se eliminaron al cerrar el bloque.

Archivos creados en US5:

- `backend/tests/GT.IntegrationTests/Proveedores/PermisosProveedorTests.cs`
- `frontend/src/modules/proveedores/paginas/AccesoProveedores.test.tsx`

Archivos modificados en US5:

- `backend/tests/GT.UnitTests/Autenticacion/CatalogoOpcionesMenuTests.cs`
- `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`
- `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`
- `backend/tests/GT.IntegrationTests/Proveedores/CargarFacturaProveedorTests.cs`
- `frontend/src/compartido/seccionesDeMenu.ts`
- `frontend/src/modules/proveedores/paginas/PadronProveedores.tsx`
- `frontend/src/App.tsx`
- `specs/013-gestion-proveedores/tasks.md`
- `specs/013-gestion-proveedores/quickstart.md`

## Cierre T062–T066 — 2026-10-17

Alcance de esta ejecución: regresión, performance, revisión UI y mantenimiento transversal. No se reimplementaron T001–T061. T045 permanece sin marcar. La evidencia cuantitativa está en [evidencia-cierre.json](evidencia-cierre.json); contiene las 120 mediciones individuales, las comprobaciones de las pantallas y los estados, sin credenciales.

### T062 — Regresión ejecutada y límites

Se ejecutó `dotnet test -c Release --logger "trx;LogFileName=cierre013.trx"` desde `backend`, sobre la solución completa. Se usó Release para conservar el backend Debug activo en 5005. También se ejecutó `dotnet build -c Release --no-restore` desde backend.

| Verificación | Resultado final |
|---|---|
| Unitarios backend completos | **543/545 aprobados**. Los únicos dos fallos son los preexistentes de Reportes indicados debajo. |
| Regresión unitaria explícita | Incluida en la corrida completa: nombres de tests de Transportistas (7), Liquidaciones (30), Facturación (79), documentación/adjuntos (14), autenticación (34) y catálogo de menú (11). Estos grupos pueden superponerse; no son suites adicionales. Todos esos casos aprobaron. |
| Integración original completa | **1/1.022 aprobado; 1.021 bloqueados durante preparación**. Se agruparon los mensajes del TRX: los 1.021 tienen el mismo error de falta de GT_SQL_PASSWORD. El caso aprobado comprueba datos de fixtures sin SQL. No se ejecutaron las aserciones de negocio de los casos bloqueados. |
| Host independiente con SQL Server local | Aprobados los escenarios reutilizables de permisos, consulta, PDF y concurrencia enumerados abajo. No se alteró AplicacionDePrueba, ConfiguracionEntorno, compose ni producción para adaptarlos. No sustituye el fixture original. |
| Frontend completo, después de las correcciones | **552/552 tests**, 72 archivos, 114,72 s, `vitest run --maxWorkers=4`. Antes de los arreglos: 551/551; se agregó un caso de accesibilidad. |
| Frontend afectado | **59/59**, ocho archivos de Proveedores, después del arreglo del formulario y los mensajes. La corrida completa posterior incluye las aserciones finales y el menú anterior. |
| Build backend Release | Aprobado, cero errores. Advertencias previas de paquetes/auditoría NuGet; no se modificaron dependencias. |
| Build frontend | `tsc -b` y `vite build` aprobados; Vite final 2,26 s. Persiste el aviso previo de bundle >500 kB. |
| Lint frontend | `oxlint`, exit 0, sin diagnósticos. |
| `git diff --check` | Aprobado. Sólo avisos existentes de normalización LF/CRLF; ningún error de espacios. |

Se intentaron `npm test`, `npm run build` y `npm run lint` con el CLI npm instalado. El gestor local hizo que los scripts heredaran Node 21.2.0 y Vitest/Vite no pudieran iniciar (falta styleText de node:util). Se verificaron sus comandos exactos con Node **24.21.0** por ruta absoluta: `node .../vitest.mjs run --maxWorkers=4`, `node .../typescript/bin/tsc -b`, `node .../vite/bin/vite.js build` y `node .../oxlint/bin/oxlint`. No se cambiaron package.json, lockfile ni configuración del proyecto/equipo para forzar npm. Estos resultados corresponden a los comandos equivalentes ejecutados, no a afirmar que los scripts npm iniciaron correctamente en este equipo.

Los dos fallos conocidos, separados del bloqueo de integración, son `GT.UnitTests.Reportes.FuentesDeVencimientosTests.Choferes_ElEstadoVaConLaPalabraDelPanel` y `Flota_ElEstadoVaConLaPalabraDelPanel`, ambos con `vencida:false`: esperan **Próxima a vencer** y obtienen **Vencida**. No se tocaron esas fuentes, fixtures ni aserciones. No quedaron fallos funcionales nuevos observados en las verificaciones finales ejecutadas.

Escenarios locales adicionales, con host API/DI reales, cookies y Windows Authentication en SQLEXPRESS02:

- Matriz de **10 perfiles × 13 operaciones = 130 estados HTTP**: anónimo, ningún permiso, gestionar/cargar/consultar aislados, otros módulos, Tráfico, Gerencia, Administración y Administrador. Incluye POST completo, GET/listado/PDF, búsquedas mínimas, ausencia de acceso extra a Transportistas y sesión/menú exactos. Siembra repetida conserva asignaciones anteriores y roles personalizados.
- Consulta: filtros combinados y abiertos, cuatro bordes de fechas de Argentina, páginas estables de 20/5, proveedor inactivo, datos históricos, importes grandes con centavos distintos, detalle normal/Fletero y PDF privado con inline/nosniff y nombre original. GET/PDF sin consultar devuelve 403.
- Dos facturas concurrentes por una liquidación: sólo una confirma; la otra devuelve conflicto. Se comprobó fila, vínculo y cantidad de PDFs, sin fila parcial. Esperas de locks ante edición/anulación/pago total/pago parcial revalidan el estado/importe al guardar y compensan el PDF rechazado.
- Reglas Fletero/normal: liquidación ausente, ajena, no Pendiente/no disponible, diferencia exacta de importes; un normal rechaza vínculo. Asociación válida congela snapshot y no cambia estado ni registra pago. Importes grandes `.01`/`.02` conservan sus cadenas exactas.
- Regresión adicional con los **endpoints existentes** de Liquidaciones: edición, anulación, pago total y parcial antes/durante/después de asociar; se conserva el importe histórico de la factura y se muestran los datos actuales por separado. Sólo se ejercitó funcionalidad anterior en una segunda base descartable; no se agregó funcionalidad de pagos.

**T062 queda sin marcar**: se ejecutó la solución completa y se documentó la evidencia disponible, pero no se completó la regresión de integración original, incluyendo Transportistas, Liquidaciones, Facturación, adjuntos y autorización. T045 tampoco se marca.

### T063 — SC-001 medida en navegador

Se prepararon exactamente **1.000 proveedores** (999 normales y un Fletero, CUIT válidos y datos distribuidos) en una base descartable de SQLEXPRESS02. Se inició la API Release en 5006, Vite en 5174 con GT_API_URL=http://localhost:5006 y **Edge 155.0.4283.45 headless**, mediante CDP, perfil temporal y viewport 1280 × 900. No se tocaron los datos de GtLogistica ni los servicios 5005/5173.

Se operó el autocomplete real de carga con eventos de entrada del navegador. El inicio se tomó con performance.now() **en el evento input final**; el fin, después de encontrar coincidencias visibles y esperar **dos requestAnimationFrame**. La medida incluye el debounce existente de 250 ms, HTTP, consultas SQL y render; el sondeo del DOM y los frames agregan espera a la medición. No es una medición aislada del endpoint ni un test con API mockeada.

| Métrica | Resultado |
|---|---|
| Búsquedas | **120**: 40 por razón social, 40 por nombre comercial y 40 por CUIT |
| Distribución | Grupos de razón social, sectores comerciales y CUIT de registros distintos; no se repitió una única consulta 100 veces. |
| Menos de 2 s | **120/120, 100%** |
| p95 | **382,1 ms**, rango más cercano: posición ceil(0,95 × 120) de los tiempos ordenados |
| Peor tiempo | **433,6 ms** |
| Solicitudes de búsqueda | 120, contra el endpoint real; máximo 20 opciones recibidas/renderizadas por página |

Se revisaron los comandos SQL realmente emitidos mediante el log de EF del host temporal: COUNT con los mismos filtros; LEFT JOIN de Transportista para identidad efectiva; LIKE parametrizados por los tres datos y filtro activo dentro de WHERE; luego ORDER BY identidad efectiva e Id y OFFSET/FETCH. Esquema del SQL observado:

```sql
SELECT COUNT(*)
FROM [Proveedores] AS [p]
LEFT JOIN [Transportistas] AS [t] ON [p].[TransportistaId] = [t].[Id]
WHERE (CASE WHEN [p].[Tipo] = 0 THEN [t].[Nombre] ELSE [p].[RazonSocial] END LIKE @patron ESCAPE N'\'
    OR [p].[NombreComercial] LIKE @patron0 ESCAPE N'\'
    OR CASE WHEN [p].[Tipo] = 0 THEN [t].[Cuit] ELSE [p].[Cuit] END LIKE @patron1 ESCAPE N'\')
  AND CASE WHEN [p].[Activo] = CAST(1 AS bit) THEN N'activo' ELSE N'inactivo' END = @filtros_Estado;
-- La consulta de items conserva ese WHERE y proyecta sólo los seis campos del resumen:
-- ORDER BY CASE WHEN [p].[Tipo] = 0 THEN [t].[Nombre] ELSE [p].[RazonSocial] END, [p].[Id]
-- OFFSET @p ROWS FETCH NEXT @p8 ROWS ONLY
```

RepositorioProveedores fija Take(20); la respuesta informa tamanioPagina:20. La traza de red del recorrido de performance no contiene una descarga del padrón completo. Se comprobó también con teclado la paginación real del autocomplete: 20 opciones, Siguientes con 5 y regreso a 20. Los estados sin coincidencias se ejercitaron aparte durante T064. **No se agregó ningún índice ni se modificó la consulta antes/después de medir.** Resultado local de SC-001 aprobado; no se extrapola a otro hardware, despliegue o red.

### T064 — Revisión UI y accesibilidad

Se contrastaron plan.md, `.claude/skills/gt-ui/SKILL.md`, tokens.md, los componentes existentes y las pantallas renderizadas. Revisión visual de capturas, árbol accesible de Edge, DOM, estilos computados y vueltas completas de Tab hasta repetir el primer destino. Se inspeccionaron tanto la variante alta como edición del mismo tipo de formulario.

| Pantalla/variante | Pasos de Tab de la vuelta observada | Primarias | Contraste mínimo de texto |
|---|---:|---:|---:|
| Padrón | 80 | 1 | 4,51:1 |
| Alta proveedor normal | 40 | 1 | 4,58:1 |
| Ficha proveedor | 33 | 0 | 4,54:1 |
| Edición proveedor | 40 | 1 | 4,58:1 |
| Carga factura normal | 43 | 1 | 4,54:1 |
| Consulta de facturas | 59 | 0 | 4,51:1 |
| Ficha factura normal | 32 | 0 | 4,54:1 |

Los pasos incluyen la navegación común del usuario administrador, no sólo campos del módulo. Se revisaron además formulario Fletero, carga con liquidación y ficha de factura Fletero (mínimo 4,54:1), errores de ambos formularios y estados de listados. Ninguna pantalla desbordó horizontalmente el viewport de 1280 px; las tablas conservan su contenedor desplazable. Las fichas usan aside de 330 px; consulta/detalle de factura no tienen primaria artificial.

El contraste se calculó con colores computados, conversión de los formatos CSS a sRGB mediante canvas y composición de las transparencias sobre sus fondos reales en el árbol DOM; no se comparó todo contra blanco. Texto sobre el lienzo usa ink/ink-soft; no se cambiaron tokens ni colores compartidos. Los textos habilitados medidos superan 4,5:1; no se contó la decoración aria-hidden ni controles inactivos. Los focos emplean brand sobre superficies claras; se inspeccionó también el segmento de día nativo enfocado del control fecha, que muestra selección y anillo aunque el host no exponga :focus-visible por el foco interno del navegador.

Comprobaciones ejecutadas:

- Labels y nombres del árbol accesible en todos los inputs/selects/textarea; required, indicador visible sin alterar la etiqueta, aria-invalid y asociación por aria-describedby al error. Sólo Fletero tiene selector de liquidación obligatorio; no muestra inputs duplicados de razón social/CUIT/contacto.
- Foco visible y recorrido sin trampa; autocomplete con flechas/Enter, aria-activedescendant, Escape/Tab y paginación; Tipo/Transportista y Liquidación operados con teclado nativo. Cambiar proveedor limpia selección y selector anterior. Diálogo de baja: foco inicial adentro, Escape cancela y devuelve foco, sin escribir la baja.
- Navegación mediante enlaces reales; filas sin tabIndex artificial. Máximo una primaria; guardar primario, consulta/ficha sólo acciones secundarias y enlaces autorizados. No se añadieron controles de pagos ni acciones sobre la liquidación.
- Estados reales vacíos del padrón, consulta y autocomplete; carga de consulta; error del padrón; 403 sin botón Reintentar; conflicto de importe con los importes exactos del servidor; Fletero sin liquidaciones con Guardar bloqueado. Para carga/error/403 se controló únicamente la red del navegador temporal mediante CDP: no se simularon como aceptación del backend ni se cambiaron fixtures/producción.
- Normal válido: neto 100, IVA 21, total 121, sin liquidación; Fletero válido: misma cuenta y liquidación 121. El intento Fletero con neto 99 se rechazó; al corregirlo confirmó y la liquidación dejó de ofrecerse. Estados/anuncios dinámicos registrados en la evidencia.

Correcciones limitadas al módulo:

1. FormularioProveedor mostraba errores de campos sin una región de alerta. Se agregó el párrafo «Revisá los campos marcados.». Un test nuevo falló antes del arreglo y pasó después; también comprueba el vínculo al error y que no se envíen datos incompletos.
2. Se agregó MensajeListado local para que el role status quede en el párrafo del mensaje, conservando el tratamiento visual de GT UI. Sustituye los estados vacíos/carga de los dos listados y elimina el anidamiento de párrafos del padrón. Las pruebas fijan la región viva en el mensaje visible. No se modificaron GT UI, sus tokens ni pantallas anteriores.

### T065 — Evidencia técnica y aceptación manual confirmada

**T065 completa.** Confirmación del usuario registrada el 2026-10-17: la validación manual fue realizada **desde la interfaz por un usuario/empleado, sin herramientas técnicas ni asistencia técnica**. Esta evidencia es independiente de los recorridos automatizados ya registrados; no se atribuye al asistente la ejecución manual del empleado.

Resultados informados por el usuario:

- Alta, consulta, edición y baja lógica de proveedor: **OK**.
- GFP_01 Proveedor normal: **OK**.
- Cálculo de IVA/total y validaciones: **OK**.
- GFP_01 Fletero con liquidación obligatoria: **OK**.
- GFP_02 filtros, listado y detalle: **OK**.
- PDF: **OK**.
- Snapshot histórico: **OK**.
- Validaciones negativas principales: **OK**.
- Buscar, cargar, revisar cálculos, guardar, filtrar y abrir detalle sin asistencia técnica: **OK (SC-005)**.

La confirmación completa la aceptación manual de GFP_01/GFP_02 y SC-005, junto con la evidencia técnica existente. No implica que se hayan repetido manualmente todas las carreras concurrentes o la matriz de permisos, ni que se haya desbloqueado el fixture original. **T045 y T062 permanecen sin marcar**; no se ejecutaron nuevas pruebas técnicas con esta actualización documental.

| Criterio | Evidencia de este cierre | Estado de aceptación manual |
|---|---|---|
| SC-001 | 120 mediciones con 1.000 proveedores, aprobadas. | Medición completada en T063. |
| SC-002 | Unitarios completos y carga normal/Fletero en Edge con importes exactos. Reglas, fechas, CAE y netos inválidos incluidas en las suites disponibles. | Usuario confirma GFP_01 normal, cálculo IVA/total y validaciones: OK. |
| SC-003 | Carrera local por liquidación y atomicidad PDF/factura/vínculo aprobadas. Las carreras de CUIT y número tienen evidencia previa US1/US2; sus casos del fixture original no se pudieron repetir en esta corrida completa. | Usuario confirma validaciones negativas principales: OK. No se afirma repetición manual de todas las carreras; regresión original pendiente en T045/T062. |
| SC-004 | Escenarios SQL/API de modificación/baja, snapshot y PDF autorizado aprobados; detalle y listado real revisados en Edge. | Usuario confirma baja lógica, PDF y snapshot histórico: OK. |
| SC-005 | Recorridos automatizados previos conservados como evidencia técnica adicional. | **OK confirmado por el usuario**: empleado completa búsqueda, carga, revisión de cálculos, guardado, filtros y detalle de GFP_01/GFP_02 desde la interfaz, sin herramientas ni asistencia técnica. |
| SC-006 | Matriz local de 130 estados, siembra/roles/menú, App y sesión; 401/403 reales en el host independiente. | No se informó repetición manual de la matriz por perfiles; se conserva la evidencia técnica y el bloqueo del fixture original en T045/T062. |
| SC-007 | Reglas, disponibilidad, snapshot, concurrencia y PDF mediante host SQL independiente; selector, conflicto y asociación en Edge. | Usuario confirma GFP_01 Fletero con liquidación obligatoria y validaciones negativas principales: OK. |

Separación POST/GET comprobada también en la red de Edge: después del POST normal sólo apareció el POST; la pantalla mostró el DTO. localStorage y sessionStorage vacíos, history.state sin DTO (sólo metadatos del router). Una recarga completa pidió GET. Los tests finales de App comprueban adicionalmente sólo cargar → POST permitido/detalle en memoria, refresh → GET 403, sin PDF y limpieza al cerrar/cambiar sesión. La respuesta no se guardó en storage, history.state ni IndexedDB; el código del contexto permanece exclusivamente en memoria.

Auditoría de alcance de FR-037, sin cambios de backend en este cierre: (1) modificación de CUIT del Transportista vinculado coordinada con la identidad de proveedores, rechazo de duplicado cruzado; (2) asociación que se serializa con las operaciones existentes de Liquidaciones y revalida Pendiente/disponibilidad/importe. La regresión HTTP local probó que editar/anular/pagar una liquidación asociada sigue permitido por las reglas anteriores y no altera el snapshot histórico. No se agregó otra restricción sobre Transportistas ni sobre esas operaciones.

Se revisaron los endpoints y acciones del módulo: factura de proveedor tiene únicamente POST de carga y GET de consulta/detalle/PDF. **No se agregaron Gestión de Pagos, órdenes de pago, conciliaciones, edición/anulación de factura, cambios manuales de su estado, nuevos impuestos ni generación fiscal/validación externa de CAE.** La liquidación se asocia sin pagarla ni cambiar su estado. Las llamadas a pagos en la regresión ejercitan endpoints anteriores sólo con datos descartables.

### T066, limpieza e inventario del cierre

AGENTS.md recibió únicamente dos decisiones [013] reutilizables: importes HTTP exactos con cadena/BigInt y detalle postalta en memoria cuando crear/consultar requieren permisos independientes. No se repitieron las convenciones existentes de snapshot, permisos, adjuntos o paginación. GT UI y sus referencias no cambiaron.

Se eliminaron ambas bases descartables del cierre (performance/UI/escenarios y regresión HTTP adicional). Se detuvieron exclusivamente los procesos propios de API5006, Vite5174 y Edge9223, verificando PID y comando. Los datos temporales de performance, PDFs, perfil, ejecutables y logs auxiliares se retiraron del workspace; queda únicamente la evidencia sin secretos. GtLogistica y los servicios originales 5005/5173 se conservaron. Comprobación final del host: backend escuchando en 5005, frontend http://localhost:5173 y http://[::1]:5173 con HTTP 200 (Vite original escucha sólo en IPv6). Sin Docker ni commit; hooks opcionales de commit omitidos por instrucción expresa.

Archivos creados en T062–T066:

- `frontend/src/modules/proveedores/componentes/MensajeListado.tsx`
- `specs/013-gestion-proveedores/evidencia-cierre.json`

Archivos modificados en T062–T066:

- `frontend/src/modules/proveedores/paginas/FormularioProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/PadronProveedores.tsx`
- `frontend/src/modules/proveedores/paginas/PadronProveedores.test.tsx`
- `frontend/src/modules/proveedores/paginas/ConsultarFacturasProveedor.tsx`
- `frontend/src/modules/proveedores/paginas/ConsultarFacturasProveedor.test.tsx`
- `AGENTS.md`
- `specs/013-gestion-proveedores/quickstart.md`
- `specs/013-gestion-proveedores/tasks.md`

Este inventario corresponde sólo al cierre; el workspace conserva cambios anteriores de la feature. La actualización posterior de aceptación manual modificó exclusivamente quickstart.md y tasks.md; no cambió implementación ni evidencia técnica previa. `backend/cierre-013.tmp` no existe ni forma parte de los cambios. Estado final: **T063/T064/T065/T066 completas; T045/T062 sin marcar**. No se declara cerrada la feature mientras continúen esos pendientes. Sin commit.


## Evidencia final de cierre — 2026-10-18

Resultados finales informados por el usuario, registrados en esta actualización documental:

| Verificación | Resultado |
|---|---|
| Entorno | Arch Linux. |
| Servicios | Docker Compose con SQL Server 2022 healthy. |
| `dotnet test` completo desde `backend` | 1567 total, 1567 correctos, 0 errores, 0 omitidos. |
| `GT.IntegrationTests` | Ejecutados correctamente contra SQL Server real. |
| Frontend `npm test` | 72 archivos / 552 tests aprobados. |
| Frontend `npm run build` | Aprobado. |
| Frontend `npm run lint` | Aprobado, 0 warnings y 0 errors. |

Durante el cierre se corrigió una **regresión preexistente de Reportes**: las consultas de vencimientos de Choferes y Flota usaban el reloj del sistema en lugar de la fecha del reporte. Esa regresión no se atribuye a la feature 013.

Esta evidencia completa **T045 y T062** y reemplaza los bloqueos y pendientes consignados en los registros históricos de este documento. Con T063–T066 ya completas, la **feature 013 queda cerrada documentalmente**. Esta actualización sólo modifica `tasks.md` y `quickstart.md`; no ejecuta nuevamente las verificaciones informadas, no modifica código y no hace commit.
