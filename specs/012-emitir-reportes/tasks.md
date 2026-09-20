# Tasks: Emitir reportes (Módulo 12)

**Input**: Documentos de diseño de `/specs/012-emitir-reportes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Tests**: **Sí se incluyen.** El plan los pide explícitamente (§Technical Context) y research §13 los
enumera uno por uno con su ubicación. No es TDD estricto: dentro de cada historia los tests figuran
antes de la implementación para que se escriban contra el contrato, pero se dan por cumplidos cuando
pasan en verde sobre el código de esa historia.

**Organization**: por historia de usuario, para que cada una se implemente y se pruebe sola.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1, US2, US3)
- Toda tarea lleva su ruta de archivo exacta

## Path Conventions

Aplicación web con backend y frontend separados (plan §Project Structure):
`backend/src/GT.{Api,Application,Domain,Infrastructure}/`, `backend/tests/GT.{Unit,Integration}Tests/`,
`frontend/src/`.

## Lo que esta feature NO hace

Para que la revisión lo pueda contar (data-model §7): **cero tablas, cero columnas, cero migraciones,
cero variables de entorno, cero pantallas nuevas, cero rutas de frontend nuevas, cero entradas de
menú, cero archivos en disco.** Una dependencia nueva (ClosedXML), un permiso nuevo, cinco endpoints
y cinco pantallas existentes modificadas, más **cinco agregados aditivos a módulos anteriores** para
las palabras de los estados (T070–T074, data-model §2.5): ni una firma, ni un contrato JSON, ni una
pantalla, ni una suite existente cambian.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: la dependencia nueva y las carpetas de la feature.

- [ ] T001 Agregar `ClosedXML` 0.105.1 como `PackageReference` en `backend/src/GT.Infrastructure/GT.Infrastructure.csproj` y verificar con `cd backend && dotnet restore` que resuelve; confirmar que **ni el `Dockerfile` ni el `docker-compose.yml` cambian** (research §1: ClosedXML es C# puro, sin requisitos nativos, a diferencia de QuestPDF)
- [ ] T002 [P] Crear las carpetas vacías de la feature: `backend/src/GT.Application/Reportes/`, `backend/src/GT.Application/Reportes/Fuentes/`, `backend/src/GT.Infrastructure/Reportes/`, `backend/src/GT.Api/Reportes/`, `backend/tests/GT.UnitTests/Reportes/`, `backend/tests/GT.IntegrationTests/Reportes/`, `frontend/src/modules/reportes/componentes/`, `frontend/src/modules/reportes/servicios/`
- [ ] T003 [P] Verificar que las tres rutas de identificador bajo cuyos prefijos van rutas literales nuevas ya llevan `{id:int}` — `backend/src/GT.Api/Viajes/ViajesEndpoints.cs`, `backend/src/GT.Api/Facturacion/FacturasEndpoints.cs`, `backend/src/GT.Api/Caja/CajaEndpoints.cs` — y dejar anotado en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` (comentario de cabecera, archivo que crea T018) que no hace falta agregar ninguna (convención [005], research §6)

**Checkpoint**: compila y restaura con la dependencia nueva; las carpetas existen.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el modelo único, los dos armadores, el permiso, la política de dos permisos y toda la
maquinaria compartida del frontend. **Las tres historias dependen entera de esta fase**: cada una
suma después su fuente, su endpoint y su pantalla.

**⚠️ CRITICAL**: ninguna historia puede empezar hasta que esta fase esté completa.

### El modelo en memoria (`GT.Application/Reportes/`)

- [ ] T004 [P] Crear `ReporteTabular`, `EncabezadoDeReporte`, `ColumnaDeReporte`, `TipoDeColumna` (`Texto`/`Fecha`/`Importe`/`Entero`), `CeldaDeReporte` y `TotalDeReporte` en `backend/src/GT.Application/Reportes/ReporteTabular.cs`, según data-model §2. `CeldaDeReporte` expone **cuatro constructores estáticos anulables** —`Texto(string?)`, `Fecha(DateOnly?)`, `Importe(decimal?)`, `Entero(int?)`— y **nunca guarda texto ya formateado**: es lo que hace convivir FR-010 con FR-011. Un valor nulo es una celda vacía, nunca un texto de relleno
- [ ] T005 [P] Crear el enum `FormatoDeReporte` (`Pdf` | `Excel`) y sus tres derivados en un solo lugar —tipo de contenido, extensión y `Content-Disposition` (`inline` / `attachment`)— en `backend/src/GT.Application/Reportes/FormatoDeReporte.cs` (data-model §5)
- [ ] T006 [P] Crear `OpcionesDeReporte` en `backend/src/GT.Application/Reportes/ReglasDeReporte.cs` con la forma exacta de data-model §4: un `record` con `TopeDeFilas`, la constante `TopePorDefecto = 5000` —**el valor de FR-016 escrito una sola vez**— y `PorDefecto`. Se registra en el contenedor y las fuentes lo reciben **por inyección**, no leyendo una constante: un `const` no se puede bajar para un test, y research §13 pide probar el `409` sin sembrar 5.001 filas
- [ ] T007 [P] Crear los cuatro textos en es-AR con sus códigos —`formato_invalido`, `sin_permiso`, `tope_de_filas_superado`, `reporte_no_generado`— en `backend/src/GT.Application/Reportes/Mensajes.cs`, con la redacción exacta de contracts §Cuerpos de error. El del tope se arma con las filas y el tope como números con separador de miles
- [ ] T008 [P] Crear `ResultadoReporte` (entregado | tope superado | formato inválido) y `ReporteNoGeneradoException` en `backend/src/GT.Application/Reportes/ResultadoReporte.cs` y `backend/src/GT.Application/Reportes/ReporteNoGeneradoException.cs`
- [ ] T009 [P] Crear las dos interfaces de frontera con las bibliotecas, `IArmadorReportePdf` e `IArmadorReporteExcel` —cada una recibe un `ReporteTabular` y devuelve `byte[]`— en `backend/src/GT.Application/Reportes/IArmadorReportePdf.cs` y `backend/src/GT.Application/Reportes/IArmadorReporteExcel.cs`
- [ ] T010 Crear `NombreDeArchivoDeReporte.Para(reporte, formato, generadoEn)` como función pura en `backend/src/GT.Application/Reportes/NombreDeArchivoDeReporte.cs`: `viajes-2026-09-20-1432.pdf`, con fecha **y hora de Argentina**, sólo ASCII, minúsculas y guiones (FR-012, data-model §6). Depende de T005
- [ ] T011 [P] Crear `LineaDeFiltros` en `backend/src/GT.Application/Reportes/LineaDeFiltros.cs`: pares `Etiqueta: valor` unidos por ` · `, o el literal `Sin filtros aplicados` cuando no hay ninguno (FR-006, research §9). Las fechas en `dd/MM/yyyy`

### Las palabras de los estados en C# (data-model §2.5, research §14)

**Por qué esta sección existe**: las clases `NombresDeEstado*` del backend **no traducen a palabras**
—devuelven el código camelCase del JSON, que es para lo que existen ([003])— y las palabras visibles
viven sólo en los mapas de TypeScript. Sin estas cinco tareas, los cinco reportes salen con
`enCurso`, `proximaAvencer` e `ingreso` en la columna Estado, que es **exactamente lo que FR-010
prohíbe**: compila, pasa los tests de las historias y sólo se descubre abriendo un archivo.

Las cinco son **aditivas**: ninguna firma cambia, ningún contrato JSON cambia, ninguna pantalla
cambia y ninguna suite existente se toca. Las palabras se copian **literalmente** del mapa de
TypeScript que cada tarea nombra, tildes y singulares incluidos.

- [ ] T070 [P] Agregar `EnPantalla(EstadoViaje)` a `backend/src/GT.Application/Viajes/NombresDeEstadoViaje.cs`, al lado de `EnJson`: `Pendiente` · `En curso` · `Rendido` · `Anulado` · `Facturado`. Copiadas de `NOMBRES_DE_ESTADO` en `frontend/src/modules/viajes/servicios/servicioViajes.ts`
- [ ] T071 [P] Agregar `EnPantalla(DocumentacionEstado)` a `NombresDeEstado` en `backend/src/GT.Application/Choferes/Dtos.cs`: `Al día` · `Próxima a vencer` · `Vencida`. Copiadas de `TEXTO_ESTADO_DOCUMENTO` en `frontend/src/modules/choferes/servicios/estados.ts`. **Es el estado del documento, no el del chofer**: es el que los dos paneles muestran
- [ ] T072 [P] Agregar `EnPantalla(DocumentacionEstado)` a `NombresDeEstadoFlota` en `backend/src/GT.Application/Flota/Dtos.cs`, con las mismas tres palabras, copiadas de `TEXTO_ESTADO_DOCUMENTO` en `frontend/src/modules/flota/servicios/estados.ts`
- [ ] T073 [P] Agregar `EnPantalla(TipoMovimientoCaja)` a `backend/src/GT.Application/Caja/NombresDeEstadoCaja.cs`: `Ingreso` · `Egreso`, **con mayúscula inicial**, no el `ingreso`/`egreso` del JSON
- [ ] T074 [P] Crear `backend/src/GT.Application/Facturacion/SituacionDeVencimiento.cs` con `Para(int dias)` y **las tres ramas exactas** de `situacion(dias)` de `frontend/src/modules/facturacion/servicios/api.ts:213`: `dias < 0` → `Vencida hace N días`; `dias == 0` → `Vence hoy` (no `Vence en 0 días`, que sería correcto y no es lo que nadie diría); `dias > 0` → `Vence en N días`. **Singular con 1**: `1 día`
- [ ] T075 Test unitario que **fija las palabras exactas** de las cinco piezas anteriores en `backend/tests/GT.UnitTests/Reportes/PalabrasDeEstadoTests.cs`, incluidos los tres bordes de T074 —día singular, cero y atraso— y las tildes de `Al día` y `Próxima a vencer`. Es lo que cierra la copia declarada de data-model §2.5, y lo más lejos que llega entre dos lenguajes el precedente de [003]. Depende de T070–T074
- [ ] T076 [P] Agregar un comentario de una línea en cada mapa de TypeScript —`servicioViajes.ts`, los dos `estados.ts`, el de Caja y `facturacion/servicios/api.ts`— señalando su copia en C# y el test que la fija. **Es la única parte de esta sección que toca el frontend, y no cambia ni una línea de código ejecutable**

### Los dos armadores (`GT.Infrastructure/Reportes/` — las únicas clases que conocen las bibliotecas)

- [ ] T012 Implementar `ArmadorReportePdfQuestPdf` en `backend/src/GT.Infrastructure/Reportes/ArmadorReportePdfQuestPdf.cs`: A4 **apaisado** en los cinco reportes, márgenes de 1 cm, encabezado repetido en cada página con las cinco piezas de FR-006, número de página al pie y la fila de totales al final de la tabla (research §10). Importes en pesos argentinos `$ 1.240.000,00` y fechas `dd/MM/yyyy`, alineados a la derecha los importes (FR-010). **El instante de generación entra al archivo y también a los metadatos `CreationDate`** — es deliberado y contrario a la regla del Módulo 6 (research §7)
- [ ] T013 Implementar `ArmadorReporteExcelClosedXml` en `backend/src/GT.Infrastructure/Reportes/ArmadorReporteExcelClosedXml.cs`: el encabezado en las primeras filas, la tabla con los nombres de columna de la pantalla, y **los importes y las fechas asignados como `decimal` y `DateTime` con `Style.NumberFormat.Format`** (`#,##0.00` y `dd/mm/yyyy`), nunca como texto — es FR-011 y SC-007. Los totales del pie también como número

### El permiso y la autorización

- [ ] T014 Agregar la constante `CodigosPermiso.ReportesEmitir = "reportes.emitir"` en `backend/src/GT.Domain/Usuarios/Rol.cs`, junto a las que ya están (data-model §1). **Es el único archivo de `GT.Domain` que esta feature toca**
- [ ] T015 Sembrar en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs` la fila del permiso (`Codigo` `reportes.emitir`, `Modulo` `Reportes`, `Descripcion` `Emitir los reportes de viajes, vencimientos y movimientos de caja en PDF y Excel`) y su reparto **sólo a Gerencia y a Administrador del sistema** en `PermisosPorRol` (FR-014). No lleva entrada de menú: el catálogo es una lista de pares permiso → pantalla y este permiso no tiene pantalla propia (research §5)
- [ ] T016 Agregar `ParaTodos(params string[] codigos)` a `backend/src/GT.Api/Autorizacion/PermisoRequirement.cs`, que arma una política con **varios `PermisoRequirement`**. ASP.NET Core exige que todos se satisfagan, así que la conjunción de FR-015 sale gratis y **el `PermisoHandler` del Módulo 1 no cambia una línea** (research §5)
- [ ] T017 Registrar en `backend/src/GT.Api/Program.cs`: los dos armadores en el contenedor de DI, la política simple de `reportes.emitir` y **las cinco políticas combinadas** de contracts §Endpoints, una por reporte. Depende de T012, T013 y T016

### Los endpoints: el esqueleto compartido

- [ ] T018 Crear `backend/src/GT.Api/Reportes/RespuestasDeReporte.cs` —traduce un `ResultadoReporte` a HTTP: `200` con `Content-Type`, `Content-Disposition` (`inline` para PDF, `attachment` para Excel) y `X-Content-Type-Options: nosniff`; `400 formato_invalido`; `409 tope_de_filas_superado` **con `filas` y `tope` en el cuerpo además del mensaje ya armado**; `500 reporte_no_generado`— y el esqueleto de `backend/src/GT.Api/Reportes/ReportesEndpoints.cs`, con el comentario de cabecera que explica por qué **los cinco `MapGet` van juntos en un archivo**: para que la revisión cuente cinco y sólo cinco (FR-001, research §6). El parseo de `?formato=pdf|excel` vive acá y es común a los cinco

### Tests de la fase foundational

- [ ] T077 [P] Test unitario de la traducción a HTTP en `backend/tests/GT.UnitTests/Reportes/RespuestasDeReporteTests.cs`: los **cuatro** cuerpos `{ codigo, mensaje }` de contracts §Cuerpos de error con su texto exacto y su código de estado —`400 formato_invalido` con `formato` ausente, vacío y distinto de `pdf`/`excel`; `403 sin_permiso`; `409 tope_de_filas_superado` **con `filas` y `tope` además del mensaje**; `500 reporte_no_generado`— más las tres cabeceras del `200` por formato (`Content-Type`, `Content-Disposition` `inline`/`attachment` y `X-Content-Type-Options`). Es pura traducción: no necesita endpoint ni base. Depende de T018
- [ ] T019 [P] Test unitario del nombre del archivo —los cinco prefijos, la extensión por formato, la hora de Argentina, dos reportes del mismo día que no colisionan— en `backend/tests/GT.UnitTests/Reportes/NombreDeArchivoDeReporteTests.cs` (FR-012, research §13)
- [ ] T020 [P] Test unitario de la línea de filtros **con filtros y sin ninguno**, con las fechas en `dd/MM/yyyy` y el separador ` · `, en `backend/tests/GT.UnitTests/Reportes/LineaDeFiltrosTests.cs` (FR-006, research §9)
- [ ] T021 [P] Test unitario de que el encabezado trae **las cinco piezas de FR-006** —título, filtros, filas incluidas, instante de generación y usuario—, con el reloj inyectado, en `backend/tests/GT.UnitTests/Reportes/EncabezadoDeReporteTests.cs`. **No se escribe ningún test de igualdad byte a byte sobre un reporte**: sería un test que pasa o falla según el reloj (research §7)
- [ ] T022 [P] Test de integración que ejercita **las dos bibliotecas de verdad resolviendo los armadores del contenedor** —no instanciando las clases— en `backend/tests/GT.IntegrationTests/Reportes/ArmadoresTests.cs`: el `.xlsx` generado se **reabre** y se verifica que las celdas de importe sean número y las de fecha sean fecha (FR-011, SC-007), y que el PDF se genere sin fallar por falta de `libfontconfig1` (convención [006], research §1 y §13)

### El frontend compartido

- [ ] T023 [P] Crear `frontend/src/compartido/archivos.ts` con `obtenerArchivo` (pide por `fetch`, devuelve el `Blob` y el nombre leído del `Content-Disposition` con una expresión regular de una línea, **sin recomponerlo en TypeScript**) y `entregarArchivo` (PDF → `window.open(url,'_blank','noopener')` y **si devuelve `null` cae a `<a download>`**; Excel → `<a download>` siempre), más su test `frontend/src/compartido/archivos.test.ts` que incluye **el caso de `window.open` falseado devolviendo `null`** (research §8, §13)
- [ ] T024 Llevar `obtenerPdf` de `frontend/src/modules/facturacion/servicios/api.ts` a `compartido/archivos.ts` como `obtenerArchivo` y cambiar **sólo la línea de importación** en `frontend/src/modules/facturacion/servicios/servicioFacturas.ts`. **La prueba de que es un refactor y no un cambio de comportamiento es que la suite de Facturación pasa sin modificarse** (convención [009], research §8). Depende de T023
- [ ] T025 [P] Agregar `reportesEmitir: 'reportes.emitir'` en `frontend/src/modules/autenticacion/servicios/sesion.ts`
- [ ] T026 [P] Agregar los tres códigos de error nuevos —`formato_invalido`, `tope_de_filas_superado`, `reporte_no_generado`— en `frontend/src/compartido/tipos.ts`
- [ ] T027 Agregar la prop **opcional** `accionSecundaria` a `frontend/src/compartido/ui/EncabezadoDePantalla.tsx`, dibujada a la izquierda de `accionPrincipal` en la misma fila, y su caso en `frontend/src/compartido/ui/EncabezadoDePantalla.test.tsx`. Es aditiva: ninguna de las 42 pantallas deja de compilar ni cambia. **No se pasa la acción secundaria por `accionPrincipal`** — un slot llamado *acción principal* que recibe una secundaria es exactamente lo que la convención [007] señala
- [ ] T028 Crear `frontend/src/modules/reportes/servicios/servicioReportes.ts` con las cinco rutas de contracts §Endpoints y el manejo del `409`: `tope_de_filas_superado` **vuelve como resultado, no como excepción** (convención [009], contracts §El 409 vuelve como resultado). **Sólo el `409` vuelve como resultado**: el `400` y el `500` vuelven como error con el `codigo` y el `mensaje` del cuerpo, que la pantalla muestra tal cual llegan — nunca con un texto compuesto en TypeScript (research §4). Un fallo de red, sin cuerpo que leer, cae en el mensaje genérico de `reporte_no_generado`. `pagina` **no se manda nunca**: el reporte abarca todas las filas del filtro (FR-007). Depende de T023 y T026
- [ ] T029 Crear `frontend/src/modules/reportes/componentes/GenerarReporte.tsx` con las cuatro props de contracts §El componente compartido (`reporte`, `filtros`, `cantidadDeFilas`, `puedeEmitir`): **`puedeEmitir` falso devuelve `null`**, no un botón deshabilitado (FR-013); acción **secundaria** (FR-005); con `cantidadDeFilas === 0` queda deshabilitada y al lado dice *"No hay filas para reportar."* enlazado con `aria-describedby` (FR-003); mientras genera queda deshabilitada y la región viva dice *"Generando el reporte…"* (FR-004). El diálogo usa el `Dialogo` de `compartido/ui` con *PDF* (primario), *Excel* (secundario) y *Cancelar* — **el botón del formato ejecuta**, para que SC-001 se cumpla en dos clics. El resultado va en un `<p role="status">` con **el `role` en el `<p>` del mensaje, no en un contenedor** (convenciones [003] y [008]); el rechazo va en un `role="alert"` aparte. **Después de cualquier rechazo la acción vuelve a quedar accionable y el componente no toca ningún estado de la pantalla**, así los filtros y la página siguen donde estaban y se puede reintentar sin recargar (FR-017). Depende de T027 y T028
- [ ] T030 Test del componente compartido en `frontend/src/modules/reportes/componentes/GenerarReporte.test.tsx`, por rol, etiqueta y texto (convención [007]): sin permiso no se dibuja nada; con cero filas el botón está deshabilitado y se explica por qué; el diálogo ofrece **exactamente dos formatos en el orden PDF → Excel**; *Cancelar*, `Escape` y el clic afuera cierran sin generar y devuelven el foco al disparador; durante la generación el botón queda deshabilitado; el `409` del tope se muestra **con el mensaje que llega del servidor**; y **el caso de FR-017**: una generación que falla muestra el mensaje en el `role="alert"`, deja el botón accionable otra vez y **un segundo intento que sí funciona entrega el archivo**, sin que los filtros que el componente recibió hayan cambiado
- [ ] T031 Calcular `puedeEmitirReportes` una sola vez en `frontend/src/App.tsx` a partir del permiso de la sesión, listo para pasarlo a las cinco pantallas (cada historia conecta las suyas)

**Checkpoint**: el modelo, las palabras de los estados, los dos armadores, el permiso, la política de
dos permisos y todo el frontend compartido están listos y probados. **Las tres historias pueden
empezar, incluso en paralelo.**

---

## Phase 3: User Story 1 — Reporte de viajes con los filtros aplicados (Priority: P1) 🎯 MVP

**Goal**: en el listado de Viajes, con los filtros ya aplicados, *Generar reporte* entrega un PDF o un
Excel con **todos** los viajes del filtro —no los de la página visible—, en el orden de la pantalla,
con el encabezado, las diez columnas de FR-008 y el total de importes.

**Independent Test**: aplicar en `/viajes` un filtro que deje unos pocos viajes, generar el reporte
en cada formato y comprobar que el archivo trae exactamente esos viajes, con los mismos datos que la
pantalla y con los filtros aplicados escritos en el encabezado. Con un filtro que deje más de una
página, el archivo trae **todas** las filas, no las veinte visibles.

### Tests for User Story 1

- [ ] T032 [P] [US1] Test unitario de la fuente de viajes en `backend/tests/GT.UnitTests/Reportes/FuenteReporteViajesTests.cs`: las **diez** columnas de FR-008 con los nombres de la pantalla y en orden; *Ruta* y *Asignación* abiertas en dos columnas cada una; el estado con **`NombresDeEstadoViaje.EnPantalla`** —`En curso`, nunca `enCurso` (FR-010, T070)—; un viaje sin chofer ni vehículo deja **las dos celdas vacías**, sin texto de relleno; y **el total de importes coincide con la suma de las filas del propio reporte** (FR-009, SC-004)
- [ ] T033 [P] [US1] Test de integración de paridad en `backend/tests/GT.IntegrationTests/Reportes/ReporteViajesTests.cs`: sobre los mismos datos sembrados, la fuente del reporte y la consulta del listado devuelven **la misma cantidad de filas y en el mismo orden** (SC-002, FR-007), incluido el caso de un filtro que deja más de una página; y con la aplicación de prueba registrando `new OpcionesDeReporte(3)` y **cuatro** filas sembradas, el `409` llega con `filas`, `tope` y el mensaje ya armado (FR-016, SC-008). Verificar además el borde: con filas **iguales** al tope el reporte se entrega, porque FR-016 rechaza "más de"
- [ ] T078 [P] [US1] Test de integración de los dos caminos de error de `/api/viajes/reporte` en `backend/tests/GT.IntegrationTests/Reportes/ErroresDeReporteTests.cs`: pedir sin `formato` y con `formato=csv` devuelve **`400 formato_invalido`** y **ningún archivo**; y con un `IArmadorReportePdf` doblado que lanza, la respuesta es **`500 reporte_no_generado`** con el texto de contracts, no una excepción sin manejar ni un `200` con cero bytes. Es el único lugar donde el `500` se ejerce de verdad: T077 prueba la traducción, éste prueba que el fallo del armador llega hasta ella (FR-017)
- [ ] T034 [P] [US1] Test de integración de permisos de `/api/viajes/reporte` en `backend/tests/GT.IntegrationTests/Reportes/PermisosDeReporteTests.cs`: Gerencia y Administrador del sistema obtienen el archivo; Tráfico y Administración de la empresa reciben `403`; y **quien tiene `reportes.emitir` pero no `viajes.consultar` también recibe `403`**, con el mismo texto (FR-013, FR-015, contracts §Los dos permisos dan el mismo 403)

### Implementation for User Story 1

- [ ] T035 [US1] Agregar el parámetro **opcional** `int? tamanioPagina = null` a `ConsultarAsync` en `backend/src/GT.Application/Viajes/IRepositorioViajes.cs`. **El valor por defecto se declara sólo acá, en la interfaz, y la implementación lo resuelve con `?? PaginaDe<ViajeListado>.TamanioPorDefecto`** — un valor por defecto repetido en la implementación se resuelve por el tipo estático de quien llama y las dos copias pueden divergir sin fallar, que es la forma exacta del bug de `HasSentinel` de [009]. **Ninguna llamada existente cambia y ninguna suite de Viajes se toca** (research §3)
- [ ] T036 [US1] Usar ese `tamanioPagina` en `backend/src/GT.Infrastructure/Persistencia/RepositorioViajes.cs` en **las tres apariciones de `TamanioPorDefecto`**, no sólo en dos: el `Skip` (~línea 146), el `Take` (~línea 147) y **el `PaginaDe<>` que se construye al devolver (~línea 197)**. Si esta última queda con la constante, la respuesta informa `TamanioPagina: 20` con 5.000 filas adentro — **no falla, miente**. Sin tocar el `OrderBy` ni el `CountAsync` **que ya cuenta sobre el filtro completo antes de paginar**, de donde sale el número de FR-016 sin una consulta nueva
- [ ] T037 [US1] Crear `backend/src/GT.Application/Reportes/Fuentes/FuenteReporteViajes.cs`: llama a **la misma consulta del listado** con `Pagina: 1` y `tamanioPagina: opciones.TopeDeFilas` (T006), proyecta al `ReporteTabular` con las diez columnas de data-model §3.1 y el total de importes, y arma la línea de filtros resolviendo **cliente y transportista a su nombre** contra los repositorios que ya existen (research §9). Título: `Reporte de viajes`. El estado sale de `NombresDeEstadoViaje.EnPantalla`. Depende de T035, T036, T070
- [ ] T038 [US1] Agregar el `MapGet` de `/api/viajes/reporte` en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` con la política `viajes.consultar` + `reportes.emitir` y los parámetros `clienteId`, `transportistaId`, `estado`, `desde`, `hasta`, `busqueda` — **los mismos nombres que el listado ya recibe**, sin inventar un enlace de parámetros propio (contracts §Endpoints)
- [ ] T039 [US1] Colgar `<GenerarReporte reporte="viajes" …>` como **acción secundaria del encabezado** en `frontend/src/modules/viajes/paginas/ListadoViajes.tsx`, pasándole los filtros aplicados y el `total` del listado como `cantidadDeFilas`. **`Nuevo viaje` sigue siendo la única primaria** y para Gerencia la pantalla sigue sin primaria, como antes de esta feature (FR-005, plan §UI Design Check)
- [ ] T040 [US1] Pasar `puedeEmitirReportes` a la pantalla de `/viajes` en `frontend/src/App.tsx`
- [ ] T041 [US1] Agregar a `frontend/src/modules/viajes/paginas/ListadoViajes.test.tsx` los casos de la historia: con el permiso aparece la acción y **el resto de la pantalla no cambia**; sin el permiso **no aparece en ningún lado**; cerrar el diálogo sin elegir deja los filtros y la página **exactamente como estaban** (FR-002)

**Checkpoint**: US1 funciona entera y se puede demostrar sola. Con T077 y T078 quedan además ejercidos **los cuatro cuerpos de error del contrato**, que valen para los cinco reportes: US2 y US3 no los repiten. **Es el MVP**: con esto ya se saca de
la aplicación la información de viajes en los dos formatos, y queda construida toda la maquinaria que
US2 y US3 reusan sin agregarle nada.

---

## Phase 4: User Story 2 — Reporte de los tres paneles de vencimientos (Priority: P2)

**Goal**: la misma acción, con las mismas dos opciones, en los tres paneles de vencimientos —choferes,
flota y facturas—. Los tres son pantallas de lectura sin filtros: el reporte se lleva el panel
completo y su encabezado dice `Sin filtros aplicados`.

**Independent Test**: panel por panel —abrir el panel, generar el reporte en cada formato y comparar
fila por fila contra lo que la pantalla muestra, **incluido el orden por urgencia**—. En el de
facturas, además, el total de los importes cierra contra la suma de las filas del archivo.

**Nota**: los tres paneles **ya devuelven la lista entera hoy**, así que las fuentes llaman a sus casos
de uso tal cual están: **cero cambios en Choferes, Flota y Facturación del lado del backend**
(research §3). Los tres sub-bloques son independientes entre sí y se pueden repartir.

### Tests for User Story 2

- [ ] T042 [P] [US2] Test unitario de las tres fuentes de vencimientos en `backend/tests/GT.UnitTests/Reportes/FuentesDeVencimientosTests.cs`: las columnas de data-model §3.2, §3.3 y §3.4 con los nombres de la pantalla; el estado con **`EnPantalla`** —`Al día`, `Próxima a vencer`, `Vencida`, nunca `proximaAvencer` (FR-010, T071, T072)—; la *Situación* de facturas con las tres ramas de `SituacionDeVencimiento` (T074); el encabezado con `Sin filtros aplicados` en los tres; y en facturas, el **total de importes igual a la suma de las filas** (FR-009, SC-004)
- [ ] T043 [P] [US2] Test de integración de paridad y orden de los tres paneles en `backend/tests/GT.IntegrationTests/Reportes/ReportesDeVencimientosTests.cs`: sobre los mismos datos sembrados, cada reporte trae **las mismas filas y en el mismo orden por urgencia** que el panel (SC-002)
- [ ] T044 [P] [US2] Extender `backend/tests/GT.IntegrationTests/Reportes/PermisosDeReporteTests.cs` con las tres rutas de esta historia, completando la matriz de **4 roles × los endpoints ya entregados**: cada una exige su permiso de lectura propio —`choferes.vencimientos.consultar`, `flota.vencimientos.consultar`, `facturacion.consultar`— **además** de `reportes.emitir` (FR-015)

### Implementation for User Story 2 — Vencimientos de choferes

- [ ] T045 [P] [US2] Crear `backend/src/GT.Application/Reportes/Fuentes/FuenteReporteVencimientosChoferes.cs`: llama a `ConsultarVencimientos` de Choferes tal cual está, proyecta las cinco columnas de data-model §3.2 —chofer como `Apellido, Nombre`—, sin totales, con el encabezado `Reporte de vencimientos de choferes` y `Sin filtros aplicados`. **No lleva el escaneo del documento ni su enlace**: el permiso separado de los Módulos 3 y 4 existe para que Gerencia vea qué vence sin llegar al dato personal sensible, y el reporte no puede ser una puerta lateral a eso (plan §Constitution Check, Principio V). El estado sale de `NombresDeEstado.EnPantalla` (T071)
- [ ] T046 [US2] Agregar el `MapGet` de `/api/vencimientos/reporte` en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` con la política `choferes.vencimientos.consultar` + `reportes.emitir`, sin parámetros además de `formato`
- [ ] T047 [US2] Colgar `<GenerarReporte reporte="vencimientos-choferes" …>` como acción secundaria en `frontend/src/modules/choferes/paginas/PanelVencimientos.tsx`. **La pantalla sigue sin acción primaria**: no se inventa una, y `Volver al listado` sigue siendo terciaria (FR-005)

### Implementation for User Story 2 — Vencimientos de flota

- [ ] T048 [P] [US2] Crear `backend/src/GT.Application/Reportes/Fuentes/FuenteReporteVencimientosFlota.cs`: llama a `ConsultarVencimientosFlota` tal cual está, con las cinco columnas de data-model §3.3, sin totales, título `Reporte de vencimientos de flota`. El estado sale de `NombresDeEstadoFlota.EnPantalla` (T072). Mismo criterio que T045 sobre el escaneo
- [ ] T049 [US2] Agregar el `MapGet` de `/api/flota/vencimientos/reporte` en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` con la política `flota.vencimientos.consultar` + `reportes.emitir`
- [ ] T050 [US2] Colgar `<GenerarReporte reporte="vencimientos-flota" …>` como acción secundaria en `frontend/src/modules/flota/paginas/PanelVencimientosFlota.tsx`, con el mismo criterio de T047

### Implementation for User Story 2 — Vencimientos de facturas

- [ ] T051 [P] [US2] Crear `backend/src/GT.Application/Reportes/Fuentes/FuenteReporteVencimientosFacturas.cs`: llama a `ConsultarVencimientos` de Facturación tal cual está, con las cinco columnas de data-model §3.4 y el **total de importes** (FR-009). El número viaja **ya armado por el backend** (`NumeroComprobante`, convención [009]) y *Situación* sale de `SituacionDeVencimiento.Para(fila.Dias)` (T074), que es la versión en C# de la función de la pantalla: **no se puede llamar a la original porque está en TypeScript** (research §14)
- [ ] T052 [US2] Agregar el `MapGet` de `/api/facturas/vencimientos/reporte` en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` con la política `facturacion.consultar` + `reportes.emitir`
- [ ] T053 [US2] Colgar `<GenerarReporte reporte="vencimientos-facturas" …>` como acción secundaria en `frontend/src/modules/facturacion/paginas/PanelVencimientos.tsx`, con el mismo criterio de T047

### Cierre de la historia

- [ ] T054 [US2] Pasar `puedeEmitirReportes` a las tres pantallas de vencimientos en `frontend/src/App.tsx`
- [ ] T055 [P] [US2] Agregar los casos de la historia a las tres suites de pantalla —`frontend/src/modules/choferes/paginas/PanelVencimientos.test.tsx`, `frontend/src/modules/flota/paginas/PanelVencimientosFlota.test.tsx` y `frontend/src/modules/facturacion/paginas/PanelVencimientos.test.tsx`—: con el permiso aparece la acción; sin el permiso no aparece; y **con el panel sin ninguna alerta el botón queda deshabilitado y se explica por qué** (FR-003, escenario 4 de la historia)

**Checkpoint**: US1 y US2 funcionan cada una por su cuenta. Cuatro de los cinco reportes entregados.

---

## Phase 5: User Story 3 — Reporte de movimientos de caja (Priority: P3)

**Goal**: en *Movimientos de caja*, la misma acción sobre el listado ya filtrado por rango de fechas y
por caja. El archivo trae los movimientos del filtro con **los tres totales**: ingresos, egresos y
neto.

**Independent Test**: acotar la consulta por rango de fechas y por caja, generar el reporte en cada
formato y comprobar que trae los mismos movimientos que la pantalla y que **los tres totales cierran
contra las filas del propio archivo**, sumándolas con la calculadora de la planilla.

### Tests for User Story 3

- [ ] T056 [P] [US3] Test unitario de la fuente de caja en `backend/tests/GT.UnitTests/Reportes/FuenteReporteMovimientosCajaTests.cs`: las seis columnas de data-model §3.5; el tipo con `NombresDeEstadoCaja.EnPantalla` —`Ingreso`/`Egreso`, nunca `ingreso`/`egreso` (FR-010, T073)—; la referencia **vacía cuando no hay**; y **los tres totales calculados sobre las filas del reporte**, con `Neto = ingresos − egresos` (FR-009, SC-004)
- [ ] T057 [P] [US3] Test de integración en `backend/tests/GT.IntegrationTests/Reportes/ReporteMovimientosCajaTests.cs`: paridad fila a fila y de orden contra el listado con los mismos filtros (SC-002), **el filtro por días cortando en el día de Argentina** tal como lo hace el listado ([011], sin duplicar la regla), y la ruta en la matriz de permisos de `backend/tests/GT.IntegrationTests/Reportes/PermisosDeReporteTests.cs` con `caja.consultar` + `reportes.emitir`

### Implementation for User Story 3

- [ ] T058 [US3] Agregar el parámetro **opcional** `int? tamanioPagina = null` a `ConsultarMovimientosAsync` en `backend/src/GT.Application/Caja/IRepositorioCaja.cs`, **con el valor por defecto declarado sólo en la interfaz** y resuelto en la implementación, igual que T035 y por el mismo motivo. **Ninguna llamada existente cambia y ninguna suite de Caja se toca** (research §3)
- [ ] T059 [US3] Usar ese `tamanioPagina` en `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs` (`ConsultarMovimientosAsync`, ~línea 462) **en las tres apariciones**: `Skip`, `Take` y el `PaginaDe<>` que se devuelve. Sin tocar el `OrderBy` ni el `CountAsync`
- [ ] T060 [US3] Crear `backend/src/GT.Application/Reportes/Fuentes/FuenteReporteMovimientosCaja.cs`: la misma consulta del listado sin paginar, las seis columnas de data-model §3.5, los tres totales de FR-009 y la línea de filtros `Desde · Hasta · Caja` **con la caja resuelta a su nombre visible en el backend** (research §9). Título: `Reporte de movimientos de caja`. El tipo sale de `NombresDeEstadoCaja.EnPantalla`. Depende de T058, T059, T073
- [ ] T061 [US3] Agregar el `MapGet` de `/api/movimientos-caja/reporte` en `backend/src/GT.Api/Reportes/ReportesEndpoints.cs` con la política `caja.consultar` + `reportes.emitir` y los parámetros `desde`, `hasta`, `cajaId`. **Con éste son cinco `MapGet` en el archivo, y cinco es el número de FR-001**
- [ ] T062 [US3] Colgar `<GenerarReporte reporte="movimientos-caja" …>` como acción secundaria en `frontend/src/modules/caja/paginas/ConsultaDeMovimientos.tsx`, pasándole los filtros y el `total` del listado. La pantalla sigue sin acción primaria
- [ ] T063 [US3] Pasar `puedeEmitirReportes` a `/movimientos-caja` en `frontend/src/App.tsx`
- [ ] T064 [US3] Agregar a `frontend/src/modules/caja/paginas/ConsultaDeMovimientos.test.tsx` los casos de la historia: con el permiso aparece la acción, sin el permiso no; y **con un rango de fechas que no deja ningún movimiento el botón queda deshabilitado** y se explica por qué (FR-003)

**Checkpoint**: los cinco reportes entregados. Las tres historias funcionan de forma independiente.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: lo que cierra la feature y lo que la próxima feature necesita encontrar escrito.

- [ ] T065 [P] Corregir `.claude/skills/gt-ui/SKILL.md`: la línea *"en una pantalla de solo lectura la acción primaria es la que quede accionable (exportar, imprimir)"* y la fila equivalente de su tabla de jerarquía pasan a decir que **exportar no se vuelve primaria por descarte; es primaria sólo cuando es el propósito de la pantalla**. Es la convención [008]: cuando una skill es la fuente de un sistema de diseño también es su destino, y sin esto la skill queda diciendo una cosa y la aplicación haciendo otra (research §11)
- [ ] T066 Verificar en verde las suites completas: `cd backend && dotnet test` (GT.UnitTests + GT.IntegrationTests contra el SQL Server del compose) y `cd frontend && npm test`. **Las suites de Facturación y de Viajes tienen que pasar sin modificarse** donde esta feature no agregó casos: es la prueba de que T024 fue un refactor y de que T035 y T058 fueron aditivos
- [ ] T067 Recorrer `specs/012-emitir-reportes/quickstart.md` de punta a punta sobre la aplicación levantada con `podman compose up -d`, sus 32 pasos, incluidos los que se comprueban **con la calculadora de la planilla** (SC-004, SC-007). Los dos casos declarados como no verificables a mano —el bloqueo de emergentes y el `409` del tope con datos reales— quedan cubiertos por T023 y T033 y **no se intenta reproducirlos a mano**
- [ ] T068 Actualizar `specs/README.md` con el estado del Módulo 12
- [ ] T069 Actualizar `AGENTS.md` en la sección *Decisiones transversales ya tomadas* con las decisiones de esta feature, **una línea por decisión, con la referencia `[012]`**, confirmando contra lo implementado las cinco candidatas que el plan identifica (plan §Mantenimiento al cerrar la feature): el modelo intermedio único para dos formatos; la excepción con nombre a la regla del artefacto que no lee el reloj; el `409` con el mensaje ya armado aunque la pantalla tenga el dato; los dos permisos exigidos juntos como política y nunca con un `if` adentro del endpoint; y la acción que conserva el mismo nivel en todas las pantallas, con la skill corregida en la misma feature. **Sumar la sexta, que salió del análisis** (research §14): antes de escribir "usa la función que la pantalla ya usa", hay que verificar **de qué lado de la red vive esa función** — las palabras en español de los estados viven en TypeScript y las clases `NombresDeEstado*` del backend devuelven el código del JSON, así que un artefacto armado en el servidor que dice llevar "la palabra de la pantalla" lleva el código y nadie lo nota hasta abrirlo. **No incluir entradas por incluir**: sólo lo que sea transversal y aprovechable por features futuras

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias, arranca ya
- **Foundational (Fase 2)**: depende de Setup — **bloquea las tres historias**. Incluye T070–T076, las
  palabras de los estados: sin ellas los cinco reportes incumplen FR-010 sin que falle nada
- **US1 (Fase 3)**: depende sólo de Foundational
- **US2 (Fase 4)**: depende sólo de Foundational — **no depende de US1**
- **US3 (Fase 5)**: depende sólo de Foundational — **no depende de US1 ni de US2**
- **Polish (Fase 6)**: depende de las historias que se quieran entregar

### User Story Dependencies

Las tres historias son **independientes entre sí**: comparten el modelo, los armadores, el permiso y
el componente de pantalla, y los tres están en Foundational. Cada historia agrega **su fuente, su
`MapGet` y su pantalla**, y ninguna toca los archivos de otra — con dos excepciones de archivo
compartido, no de dependencia lógica:

- `backend/src/GT.Api/Reportes/ReportesEndpoints.cs`: las tres historias agregan `MapGet` al mismo
  archivo (T038, T046/T049/T052, T061). Es deliberado (FR-001, research §6) y significa que esas
  tareas **no son `[P]` entre sí**
- `frontend/src/App.tsx`: T040, T054 y T063 agregan la prop a sus pantallas. Mismo caso

### Within Each User Story

- Los tests de la historia se escriben contra el contrato, antes o junto con su implementación
- Repositorio → fuente → endpoint → pantalla → test de pantalla
- La fuente antes que el endpoint; el endpoint antes que la pantalla

### Parallel Opportunities

- **Fase 1**: T002 y T003 en paralelo
- **Fase 2**: T004 a T009 y T011 todas en paralelo (archivos distintos del mismo espacio de nombres); **T070 a T074 en paralelo** (cinco módulos distintos), con T075 detrás de las cinco y T076 independiente; T019 a T022 y T077 en paralelo; T023, T025 y T026 en paralelo
- **Fase 3**: T032, T033, T034 y T078 en paralelo
- **Fase 4**: **los tres paneles son tres bloques independientes** —(T045, T046, T047), (T048, T049, T050) y (T051, T052, T053)— repartibles entre tres personas, salvo los tres `MapGet`, que tocan el mismo archivo. T042, T043 y T044 en paralelo
- **Fase 5**: T056 y T057 en paralelo
- **Entre historias**: con Foundational cerrada, US1, US2 y US3 pueden ir en paralelo

---

## Parallel Example: Phase 2 (Foundational)

```bash
# El modelo entero, seis archivos distintos, a la vez:
Task: "T004 ReporteTabular.cs — modelo, columnas, celdas, totales"
Task: "T005 FormatoDeReporte.cs — enum y sus tres derivados"
Task: "T006 ReglasDeReporte.cs — TopeDeFilas = 5000"
Task: "T007 Mensajes.cs — los cuatro textos en es-AR"
Task: "T008 ResultadoReporte.cs + ReporteNoGeneradoException.cs"
Task: "T009 IArmadorReportePdf.cs + IArmadorReporteExcel.cs"

# Los cuatro tests unitarios de la fase, a la vez:
Task: "T019 NombreDeArchivoDeReporteTests.cs"
Task: "T020 LineaDeFiltrosTests.cs"
Task: "T021 EncabezadoDeReporteTests.cs"
Task: "T022 ArmadoresTests.cs (integración)"
```

## Parallel Example: Phase 4 (US2, tres paneles)

```bash
# Tres bloques independientes, uno por panel:
Dev A: T045 → T046 → T047   # choferes
Dev B: T048 → T049 → T050   # flota
Dev C: T051 → T052 → T053   # facturas
# Los MapGet (T046, T049, T052) tocan el mismo archivo: se serializan entre sí.
```

---

## Implementation Strategy

### MVP First (sólo US1)

1. Fase 1: Setup (T001–T003)
2. Fase 2: Foundational (T004–T031) — **crítica, bloquea todo**
3. Fase 3: US1 (T032–T041)
4. **PARAR y VALIDAR**: el reporte de viajes en los dos formatos, con filtros y sin filtros, con más
   de una página, con y sin permiso
5. Entregable: la necesidad principal resuelta y toda la maquinaria construida

### Incremental Delivery

1. Setup + Foundational → la base
2. + US1 → validar → **MVP**
3. + US2 → validar los tres paneles → entregar
4. + US3 → validar → entregar
5. Fase 6 → la skill, las suites, el quickstart y `AGENTS.md`

### Parallel Team Strategy

1. El equipo cierra Setup + Foundational junto — es donde está casi toda la sustancia técnica
2. Después: A → US1, B → US2 (o los tres paneles entre B, C y D), C → US3
3. Coordinar sólo dos archivos: `ReportesEndpoints.cs` y `App.tsx`

---

## Notes

- **`[P]` = archivos distintos y sin dependencias pendientes.** Las tareas que tocan
  `ReportesEndpoints.cs` o `App.tsx` nunca son `[P]` entre sí
- **No se escribe ningún test de igualdad byte a byte sobre un reporte** (research §7): el instante de
  generación entra al archivo a propósito, y ese test pasaría o fallaría según el reloj
- **El frontend no pre-verifica el tope de 5.000** aunque conozca el total: el mensaje lo arma el
  servidor y viaja en el `409` (research §4). Lo mismo vale para el `400` y el `500`: los cuatro
  textos del contrato los escribe el backend y la pantalla los muestra tal cual llegan
- **Un rechazo no es un callejón sin salida**: después de cualquier error la acción vuelve a quedar
  accionable y los filtros y la página siguen donde estaban (FR-017, T029, T030)
- **El backend no rechaza un reporte de cero filas**: FR-003 es sobre la disponibilidad del botón y la
  decide la pantalla. Inventarle un rechazo sería alcance fantasma (research §4)
- **Ninguna migración, ninguna tabla, ninguna variable de entorno, ningún archivo en disco**
- **Las palabras de los estados se copian literalmente** de los mapas de TypeScript que T070–T074
  nombran, tildes y singulares incluidos. Si una palabra de pantalla cambia, cambian las dos copias:
  el test T075 es lo que obliga a acordarse
- Commitear por tarea o por grupo lógico; parar en cada checkpoint para validar la historia sola
