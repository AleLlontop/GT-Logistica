# Validación de punta a punta — Feature 013

Esta guía se ejecutará después de implementar; no afirma que el módulo ya exista. Referencias: [contratos](contracts/README.md), [modelo](data-model.md), [spec](spec.md).

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
