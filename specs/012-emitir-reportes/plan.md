# Implementation Plan: Emitir reportes (Módulo 12)

**Branch**: `012-emitir-reportes` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-emitir-reportes/spec.md`

## Summary

*Emitir reportes* le da a Gerencia una manera de **sacar del sistema lo que ya ve en pantalla**: en
cinco pantallas existentes —el listado de Viajes, los tres paneles de vencimientos y la consulta de
Movimientos de caja— aparece una acción secundaria *Generar reporte* que ofrece dos formatos, PDF y
Excel, y entrega un archivo con **todas** las filas del filtro aplicado, con encabezado, totales y el
instante de generación.

**No es un módulo con pantalla propia.** No agrega ninguna ruta del frontend, ninguna entrada de
menú, ninguna tabla, ninguna columna y ninguna migración. Lo único que persiste es **una fila del
catálogo de permisos** y su reparto a dos roles, que siembra `SembradorInicial` en el próximo
arranque.

**Enfoque técnico**, con cinco decisiones que definen la feature (detalle y alternativas descartadas
en [research.md](./research.md)):

1. **Un modelo de reporte, dos armadores — no diez.** Las cinco pantallas proyectan a un mismo
   `ReporteTabular` en memoria, y dos armadores lo rinden a PDF (QuestPDF, ya presente) y a Excel
   (ClosedXML, la única dependencia nueva). Es la convención [006] aplicada a los dos **formatos**:
   el PDF y el Excel del mismo reporte no pueden traer filas distintas porque leen el mismo objeto.
   **La celda lleva el valor y su tipo, no texto ya formateado**, que es lo que hace convivir FR-010
   —pesos en el PDF— con FR-011 —número de verdad en el Excel— (research §1, §2).
2. **El reporte usa la misma consulta que el listado**, no una parecida: se agrega un parámetro
   opcional `tamanioPagina` a las dos consultas paginadas y los tres paneles se usan tal cual están.
   El orden de SC-002 no se replica, se **hereda**; y el conteo de FR-016 sale del `Total` que esa
   consulta ya calcula, sin una consulta nueva (research §3).
3. **El tope de 5.000 filas vive sólo en el backend** y se rechaza con `409` y el mensaje ya armado.
   El frontend no lo pre-verifica aunque conozca el total: escribir el mensaje en TypeScript además
   de en C# son dos textos que se separan (research §4, convención [009]).
4. **Los dos permisos de FR-015 son una política con dos `PermisoRequirement`.** ASP.NET Core exige
   que todos se satisfagan, así que la conjunción sale gratis y el `PermisoHandler` del Módulo 1 no
   cambia una línea (research §5).
5. **La entrega revierte media assumption de la spec, y se declara.** La pantalla pide el archivo por
   `fetch` y lo entrega ella, porque FR-004, FR-016 y FR-017 exigen que **la pantalla** anuncie el
   progreso y muestre el rechazo — un enlace directo manda la respuesta a otra pestaña y un `409` se
   ve ahí como JSON crudo. El PDF se abre en una pestaña nueva y **cae a descarga** si el navegador
   bloquea emergentes, cosa que puede pasar porque SC-003 admite 15 segundos y la activación
   transitoria de un clic dura 5 (research §8).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores.

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10
sobre SQL Server, QuestPDF 2026.7.3, React 19 + React Router + Vite + Tailwind + Radix, `date-fns`).
**Una dependencia nueva: `ClosedXML` 0.105.1** en `GT.Infrastructure`, licencia MIT, sin requisitos
nativos — el `Dockerfile` **no cambia** (research §1). **Ninguna variable de entorno nueva.**

**Storage**: SQL Server 2022. **Ninguna migración.** Ninguna tabla, columna, índice, `CHECK` ni
secuencia nueva o modificada; `GtDbContext` no se toca. El permiso `reportes.emitir` y su reparto a
Gerencia y al administrador los crea `SembradorInicial`, que ya es idempotente y corre en cada
arranque (data-model §1).

**Testing**: xUnit en `GT.UnitTests` (totales contra la suma de las filas, las cinco piezas del
encabezado, el nombre del archivo, la línea de filtros en palabras con y sin filtros) y en
`GT.IntegrationTests` con `WebApplicationFactory` contra el SQL Server del compose (el `.xlsx`
generado se reabre y se verifica que las celdas de importe y de fecha **sean** número y fecha; el PDF
se genera de verdad; paridad fila a fila y de orden contra la consulta del listado; el `409` del tope
con el tope bajado por configuración de prueba; la matriz de 4 roles × 5 endpoints). Vitest + React
Testing Library en el frontend, consultas por rol, etiqueta y texto (convención [007]), incluida la
caída de `window.open` con un doble que devuelve `null`.

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales. Sin cambios de `Dockerfile` ni de `docker-compose.yml`.

**Project Type**: aplicación web con backend y frontend separados.

**Performance Goals**: un reporte de 1.000 filas disponible en **menos de 15 segundos** en los dos
formatos (SC-003). El filtrado, el orden y el conteo se resuelven **dentro de la consulta SQL**, que
es la misma del listado; el armado es una pasada lineal sobre filas ya materializadas.

**Constraints**: todas las filas del filtro y en el orden de la pantalla, nunca la página visible
(FR-007); más de 5.000 filas se rechaza y **no se entrega un archivo recortado** (FR-016); cero filas
deja la acción no disponible (FR-003); la acción exige **dos** permisos y quien no puede mirar el
listado no puede exportarlo (FR-013, FR-015); la acción es **secundaria** en las cinco pantallas y no
convierte a una pantalla de lectura en una con acción primaria (FR-005); los importes son `decimal` y
en el PDF van en pesos argentinos, y en el Excel quedan como número (FR-010, FR-011); los estados van
con la palabra de la pantalla, nunca con el código interno (FR-010); una celda sin dato queda vacía;
un fallo no pierde los filtros ni la página (FR-017).

**Scale/Scope**: una única empresa, hasta 5.000 filas por reporte. En esta feature: **0 pantallas
nuevas, 0 rutas de frontend nuevas, 0 entradas de menú, 5 endpoints nuevos, 0 tablas, 0 migraciones,
1 permiso nuevo y 1 dependencia nueva**, más **5 pantallas existentes modificadas** (sólo para
colgarles la acción) y **1 diálogo**, que cubren **17 requisitos funcionales** (FR-001 a FR-017) y
**8 criterios de éxito**.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.1.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | **Una dependencia, cero migraciones, cero tablas, cero variables de entorno, cero cambios de infraestructura.** La decisión que más simplicidad compra es el modelo único: **5 fuentes + 2 armadores en vez de 10 armadores**, y la sexta pantalla de una feature futura será una fuente. Se evaluaron y descartaron: un armador por reporte y formato, un método `ConsultarTodoAsync` paralelo por repositorio, un endpoint genérico `/api/reportes/{cual}`, cinco permisos de reportes en vez de uno, una primitiva de menú desplegable nueva y un historial de reportes generados (research §2 a §6, §12) |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI, los mensajes y **el contenido de los dos formatos** en español rioplatense, definidos textualmente en `contracts/README.md`. Importes del PDF con formato de pesos —`$ 1.240.000,00`—; fechas en `dd/MM/yyyy`; el instante de generación **en hora de Argentina**, con el desplazamiento fijo de −03:00 que el sistema ya usa |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 17 requisitos y nada más. **Exactamente cinco pantallas** reciben la acción, declaradas en una tabla de `contracts/README.md` y mapeadas en un único archivo de endpoints para que la revisión pueda contarlas (FR-001, research §6). Queda afuera, como fija la spec: cualquier otro listado del sistema, historial de reportes, reportes programados, envío por correo y filtros nuevos en los tres paneles. **Dos tentaciones anotadas y no construidas**: rechazar en el servidor un reporte de cero filas, que la spec no pide, y agregarle filtros a los paneles de vencimientos (research §4, spec §Assumptions) |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 3 historias y los 8 criterios se validan operando la app con `quickstart.md`: 32 pasos, cada uno con lo que verifica, incluidos los que se comprueban **con la calculadora de la planilla** (SC-004, SC-007). **Lo que no se puede verificar a mano se declara**, con su test: el tope de 5.000 filas, el bloqueo de emergentes, la paridad sobre volúmenes grandes y el tipo real de las celdas del `.xlsx` |
| V. Datos del Usuario con Respeto | ✅ Pasa | El reporte **no pide ningún dato**: se arma con los filtros que la pantalla ya tiene. **No se guarda nada** —ni el archivo ni quién lo generó— y ningún archivo se escribe en disco, a diferencia del documento de la factura. El único dato personal que entra al archivo es el **nombre de usuario** de quien lo generó, y entra porque FR-006 lo pide para que el archivo se pueda atribuir. **Los reportes de vencimientos no llevan el escaneo del documento ni su enlace**: el permiso separado de los Módulos 3 y 4 existe justamente para que Gerencia vea qué vence sin llegar al dato personal sensible, y el reporte no puede ser una puerta lateral a eso (FR-015) |
| VI. Interfaz Gobernada por el Sistema de Diseño | ⚠ Pasa con una corrección al sistema de diseño | Las cinco pantallas declaran su acción primaria y sus campos en *UI Design Check*. Se leyeron `tokens.md`, `componentes.md` y `contenido.md`. **No se agrega ningún token ni ninguna primitiva nueva**; el diálogo de formato usa el `Dialogo` que ya existe. **Pero FR-005 contradice una línea de la skill** —"en una pantalla de solo lectura la acción primaria es la que quede accionable (exportar, imprimir)"—, y la resolución incluye **corregir la skill**, no sólo el código (ver abajo) |

### Sobre el Principio VI: la acción es secundaria y la skill se corrige

`gt-ui/SKILL.md` dice que en una pantalla de sólo lectura la primaria es la que quede accionable,
nombrando *exportar* e *imprimir*. Aplicada al pie de la letra, *Generar reporte* sería **primaria**
en los tres paneles de vencimientos y en Movimientos de caja, y **secundaria** en Viajes, donde
compite con *Nuevo viaje*: la misma acción con dos pesos según la pantalla.

**Manda FR-005**, y se sostiene contra lo que de verdad gobierna: el Principio VI exige *"a lo sumo
una acción primaria"* y *"no se inventa un primario"* en una pantalla de lectura. Una acción
secundaria no viola ninguna de las dos. La línea de la skill es una guía interna, no la regla de la
constitución.

**Y por eso la skill se corrige en esta misma feature** (convención [008]: cuando una skill es la
fuente de un sistema de diseño, también es su destino). Una línea en `SKILL.md` y la fila equivalente
de su tabla de jerarquía: *exportar no se vuelve primaria por descarte; es primaria sólo cuando es el
propósito de la pantalla*. Sin eso, la skill queda diciendo una cosa y la aplicación haciendo otra, y
la feature siguiente vuelve a introducir el valor viejo (research §11).

### Sobre el Principio III y los módulos anteriores

Esta feature **toca cinco pantallas de cuatro módulos anteriores** —Viajes, Choferes, Flota,
Facturación y Caja—, y eso pide la disciplina de la convención [006]: *la spec acota los cambios a
una lista y el plan los enumera uno por uno, para que la revisión pueda contarlos*. La lista es
exactamente ésta y no tiene nada más:

| # | Archivo | Qué cambia |
|---|---|---|
| 1 | `viajes/paginas/ListadoViajes.tsx` | Recibe `puedeEmitirReportes`; cuelga `<GenerarReporte>` como acción secundaria del encabezado |
| 2 | `choferes/paginas/PanelVencimientos.tsx` | Lo mismo |
| 3 | `flota/paginas/PanelVencimientosFlota.tsx` | Lo mismo |
| 4 | `facturacion/paginas/PanelVencimientos.tsx` | Lo mismo |
| 5 | `caja/paginas/ConsultaDeMovimientos.tsx` | Lo mismo |
| 6 | `GT.Application/Viajes/IRepositorioViajes.cs` + su implementación | Un parámetro **opcional** `tamanioPagina`, con el valor de hoy por defecto |
| 7 | `GT.Application/Caja/IRepositorioCaja.cs` + su implementación | Lo mismo |
| 8 | `facturacion/servicios/api.ts` y `servicioFacturas.ts` | Se les quita `obtenerPdf`, que se va a `compartido` como `obtenerArchivo`; `servicioFacturas.ts` cambia sólo su línea de importación (convención [009]) |
| 9 | `GT.Application/Viajes/NombresDeEstadoViaje.cs` | Un método **nuevo** `EnPantalla(EstadoViaje)` (data-model §2.5) |
| 10 | `GT.Application/Choferes/Dtos.cs` (`NombresDeEstado`) | Un método **nuevo** `EnPantalla(DocumentacionEstado)` |
| 11 | `GT.Application/Flota/Dtos.cs` (`NombresDeEstadoFlota`) | Un método **nuevo** `EnPantalla(DocumentacionEstado)` |
| 12 | `GT.Application/Caja/NombresDeEstadoCaja.cs` | Un método **nuevo** `EnPantalla(TipoMovimientoCaja)` |
| 13 | `GT.Application/Facturacion/SituacionDeVencimiento.cs` | **Archivo nuevo**: la situación en palabras a partir de los días, en C# |

Los cambios 6 y 7 son **aditivos y con valor por defecto**: ninguna llamada existente cambia y
ninguna suite de esos módulos se toca. El cambio 8 es un refactor, y **la prueba de que lo es son las
suites de Facturación pasando sin modificarse**.

**Los cambios 9 a 13 salieron del análisis de `/speckit-analyze`, no del diseño original**, y están
acá porque sin ellos los cinco reportes salen con el código interno del JSON —`enCurso`,
`proximaAvencer`, `ingreso`— en la columna Estado, que es exactamente lo que FR-010 prohíbe. La razón
es que **las palabras en español no existen en el backend**: las clases `NombresDeEstado*` traducen al
camelCase del JSON y las palabras viven sólo en los mapas de TypeScript (data-model §2.5). Los cinco
son **estrictamente aditivos** —cuatro métodos nuevos y un archivo nuevo—: ninguna firma cambia,
ningún contrato JSON cambia, ninguna pantalla cambia y ninguna suite existente se toca.

El resto de los archivos modificados son **puntos de extensión** de infraestructura compartida:
`Rol.cs`, `SembradorInicial.cs`, `PermisoRequirement.cs`, `Program.cs`, `sesion.ts`, `tipos.ts`,
`App.tsx` y `EncabezadoDePantalla.tsx`.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los seis principios se sostienen. Cuatro cosas que el diseño confirmó o
descubrió:

- **Una convención sobre artefactos generados puede tener una excepción con nombre.** El Módulo 6
  fijó que un artefacto generado es función de sus datos y **nunca lee el reloj**, protegido por un
  test de igualdad byte a byte. Acá el reloj **sí** entra al archivo, porque un reporte es la foto de
  un listado que cambia y saber a qué momento corresponde es parte del dato. La consecuencia
  operativa es concreta: **no se escribe ningún test de igualdad byte a byte sobre un reporte**, que
  sería un test que pasa o falla según el reloj (research §7).
- **Lo que evita la divergencia entre dos formatos no es un test, es la estructura.** El Módulo 6
  necesitó un test que comparara la vista previa contra el archivo guardado porque eran dos caminos
  al mismo destino. Acá el modelo intermedio único hace que el PDF y el Excel **no puedan** diferir:
  no hay nada que comparar. La convención [006] se cumple mejor eliminando el segundo camino que
  probándolo (research §2).
- **Un `409` que la pantalla muestra vale también cuando el frontend podría calcularlo solo.** La
  pantalla conoce el total del listado y podría rechazar el tope sin llamar al servidor; no lo hace,
  porque el mensaje que FR-016 exige quedaría escrito dos veces. La regla resultante es más general
  que [009]: **lo que decide es dónde vive el texto, no dónde está el dato** (research §4).
- **La restricción de tipo de `{id:int}` volvió a pagar.** Tres de las cinco rutas nuevas son
  literales bajo prefijos que ya tienen una ruta con identificador. Se verificó que las tres
  existentes ya llevan `{id:int}`; sin eso, las rutas nuevas habrían quedado inalcanzables sin fallar
  ni al compilar ni al arrancar (convención [005], research §6).

## UI Design Check

*GATE (Principio VI): obligatorio porque la feature toca UI. Re-evaluado después de Phase 1.*

Se leyeron `references/tokens.md`, `references/componentes.md`, `references/contenido.md` y el patrón
`viajes-listado.png` (las cinco pantallas alcanzadas son listados). Los textos exactos están en
[contracts/README.md](./contracts/README.md).

### Acción primaria por pantalla

**Ninguna pantalla cambia de acción primaria por esta feature.** *Generar reporte* entra como
**secundaria** en las cinco (FR-005), y en las cuatro de sólo lectura la pantalla **sigue sin
primaria**: no se inventa una.

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| `/viajes` — Viajes | `Nuevo viaje` (sin cambios; sólo con `viajes.gestionar`) | **`Generar reporte` (nueva)**, filtros del listado | — |
| `/choferes/vencimientos` — Vencimientos de choferes | `ninguna` | **`Generar reporte` (nueva)**, `Volver al listado` (terciaria, sin cambios) | — |
| `/flota/vencimientos` — Vencimientos de flota | `ninguna` | **`Generar reporte` (nueva)**, `Volver al listado` (terciaria, sin cambios) | — |
| `/facturas/vencimientos` — Vencimientos de facturas | `ninguna` | **`Generar reporte` (nueva)**, `Volver al listado de facturas` (terciaria, sin cambios) | — |
| `/movimientos-caja` — Movimientos de caja | `ninguna` | **`Generar reporte` (nueva)**, filtros del listado | — |
| Diálogo *Generar reporte* | `PDF` | `Excel`, `Cancelar` | — |

**Sobre Viajes**: para quien tiene `viajes.gestionar` conviven la primaria `Nuevo viaje` y la
secundaria `Generar reporte`, una sola primaria. Para Gerencia —que no gestiona— la pantalla queda
**sin primaria**, que es lo que ya pasaba antes de esta feature.

**Sobre el diálogo**: sus dos opciones no son equivalentes en peso porque FR-002 fija un orden, PDF
primero. La primaria se la lleva el primero de ese orden; queda una sola por superficie.

### Estados de campo

**Esta feature no agrega ningún campo de entrada.** El diálogo de formato tiene **sólo botones**: no
hay nada que tipear, nada que validar y ningún error de campo que mostrar — es una elección entre dos
opciones, no un formulario (research §12).

Los filtros de las dos pantallas que filtran ya existen y **no se tocan**: el reporte los lee tal
como están.

| Pantalla | Campo | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| `/viajes` | cliente, transportista, estado, desde, hasta, búsqueda | **Sin cambios** — `surface-soft`, `rounded-field`, ancho proporcional al dato | **Sin cambios** — anillo `rgba(68,83,168,0.16)` de 3px, visible por sí solo | **Sin cambios** | **Sin cambios** |
| `/movimientos-caja` | desde, hasta, caja | **Sin cambios** | **Sin cambios** | **Sin cambios** — el rango invertido ya se rechaza hoy y no llega a listar | **Sin cambios** |
| Los tres paneles de vencimientos | — | No tienen campos hoy y **esta feature no se los agrega** (spec §Assumptions) | — | — | — |
| Diálogo *Generar reporte* | — | **No tiene campos**: dos botones de formato y *Cancelar* | Cada botón lleva el anillo de foco del sistema | — | — |

### Los otros puntos de la checklist de la skill

- **Existe una salida**: el diálogo se cierra con *Cancelar*, con `Escape` y con el clic afuera, y
  devuelve el foco al disparador (FR-002).
- **Los estados nunca van sólo por color**, tampoco dentro del archivo: el reporte lleva la palabra
  del estado, nunca el código interno (FR-010, convención [003]).
- **Los importes van a la derecha, en una línea**, en el PDF y en la planilla.
- **Los estados vacíos explican**: con cero filas la acción queda deshabilitada **y al lado se dice
  por qué**, no se muestra un botón muerto ni una columna de guiones (FR-003).
- **El resultado se anuncia con `role="status"`** puesto **en el `<p>` del mensaje**, porque la
  pantalla no cambia al generar (convenciones [003] y [008]).

## Project Structure

### Documentation (this feature)

```text
specs/012-emitir-reportes/
├── plan.md              # Este archivo
├── research.md          # Fase 0 — 13 decisiones
├── data-model.md        # Fase 1 — el modelo en memoria y las 5 proyecciones
├── quickstart.md        # Fase 1 — 32 pasos de validación manual
├── contracts/
│   └── README.md        # Fase 1 — contrato de UI y HTTP
└── tasks.md             # Fase 2 — lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Reportes/                                  # NUEVO
│   │   │   ├── ReportesEndpoints.cs                   #   los CINCO MapGet juntos (FR-001)
│   │   │   └── RespuestasDeReporte.cs                 #   resultado → HTTP + Content-Disposition
│   │   ├── Autorizacion/PermisoRequirement.cs         # MODIFICADO — ParaTodos(params string[])
│   │   └── Program.cs                                 # MODIFICADO — DI, 1 política simple + 5 combinadas
│   ├── GT.Application/
│   │   ├── Reportes/                                  # NUEVO — carpeta espejo de la feature
│   │   │   ├── ReporteTabular.cs                      #   Encabezado, Columna, Celda, Total
│   │   │   ├── FormatoDeReporte.cs                    #   Pdf | Excel + tipo, extensión, disposición
│   │   │   ├── ReglasDeReporte.cs                     #   OpcionesDeReporte — tope 5000, inyectable
│   │   │   ├── NombreDeArchivoDeReporte.cs            #   FR-012, función pura
│   │   │   ├── LineaDeFiltros.cs                      #   los filtros en palabras (research §9)
│   │   │   ├── IArmadorReportePdf.cs                  #   frontera con QuestPDF
│   │   │   ├── IArmadorReporteExcel.cs                #   frontera con ClosedXML
│   │   │   ├── ReporteNoGeneradoException.cs
│   │   │   ├── ResultadoReporte.cs                    #   entregado | tope superado | formato inválido
│   │   │   ├── Mensajes.cs                            #   los 4 textos en es-AR y sus códigos
│   │   │   └── Fuentes/                               #   una por pantalla, las CINCO
│   │   │       ├── FuenteReporteViajes.cs
│   │   │       ├── FuenteReporteVencimientosChoferes.cs
│   │   │       ├── FuenteReporteVencimientosFlota.cs
│   │   │       ├── FuenteReporteVencimientosFacturas.cs
│   │   │       └── FuenteReporteMovimientosCaja.cs
│   │   ├── Viajes/IRepositorioViajes.cs               # MODIFICADO — `tamanioPagina` opcional
│   │   ├── Viajes/NombresDeEstadoViaje.cs             # MODIFICADO — `EnPantalla` (data-model §2.5)
│   │   ├── Choferes/Dtos.cs                           # MODIFICADO — `NombresDeEstado.EnPantalla`
│   │   ├── Flota/Dtos.cs                              # MODIFICADO — `NombresDeEstadoFlota.EnPantalla`
│   │   ├── Facturacion/SituacionDeVencimiento.cs      # NUEVO — la situación en palabras, en C#
│   │   ├── Caja/NombresDeEstadoCaja.cs                # MODIFICADO — `EnPantalla`
│   │   └── Caja/IRepositorioCaja.cs                   # MODIFICADO — `tamanioPagina` opcional
│   ├── GT.Domain/
│   │   └── Usuarios/Rol.cs                            # MODIFICADO — CodigosPermiso.ReportesEmitir
│   └── GT.Infrastructure/
│       ├── Reportes/                                  # NUEVO — las únicas clases que conocen las libs
│       │   ├── ArmadorReportePdfQuestPdf.cs
│       │   └── ArmadorReporteExcelClosedXml.cs
│       ├── Persistencia/RepositorioViajes.cs          # MODIFICADO — usa el `tamanioPagina`
│       ├── Persistencia/RepositorioCaja.cs            # MODIFICADO — idem
│       ├── DatosIniciales/SembradorInicial.cs         # MODIFICADO — el permiso y su reparto
│       └── GT.Infrastructure.csproj                   # MODIFICADO — ClosedXML 0.105.1
└── tests/
    ├── GT.UnitTests/Reportes/                         # NUEVO — totales, encabezado, nombre, filtros
    └── GT.IntegrationTests/Reportes/                  # NUEVO — armadores reales, paridad, tope, permisos

frontend/
└── src/
    ├── modules/reportes/                              # NUEVO
    │   ├── componentes/GenerarReporte.tsx             #   botón + diálogo + anuncio + errores (+ test)
    │   └── servicios/servicioReportes.ts              #   las 5 rutas, el 409 como resultado
    ├── compartido/archivos.ts                         # NUEVO — obtenerArchivo + entregarArchivo (+ test)
    ├── compartido/ui/EncabezadoDePantalla.tsx         # MODIFICADO — prop `accionSecundaria`
    ├── compartido/tipos.ts                            # MODIFICADO — 3 códigos de error
    ├── modules/autenticacion/servicios/sesion.ts      # MODIFICADO — reportesEmitir
    ├── modules/facturacion/servicios/api.ts           # MODIFICADO — obtenerPdf se va a compartido
    ├── modules/facturacion/servicios/servicioFacturas.ts # MODIFICADO — sólo la línea de importación
    ├── modules/viajes/paginas/ListadoViajes.tsx       # MODIFICADO — la acción
    ├── modules/choferes/paginas/PanelVencimientos.tsx # MODIFICADO — la acción
    ├── modules/flota/paginas/PanelVencimientosFlota.tsx # MODIFICADO — la acción
    ├── modules/facturacion/paginas/PanelVencimientos.tsx # MODIFICADO — la acción
    ├── modules/caja/paginas/ConsultaDeMovimientos.tsx  # MODIFICADO — la acción
    └── App.tsx                                         # MODIFICADO — puedeEmitirReportes a 5 pantallas

.claude/skills/gt-ui/SKILL.md                           # MODIFICADO — la línea de exportar (convención [008])
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados.
`GT.Application/Reportes/` es la carpeta espejo de `specs/012-emitir-reportes/` y de
`frontend/src/modules/reportes/`, como exige la constitución, **aunque *Reportes* no sea un módulo de
negocio con pantalla propia**: es una capacidad transversal sobre cinco módulos, y darle carpeta
propia es lo que evita que las cinco fuentes se repartan por cinco carpetas ajenas y dejen de poder
contarse (FR-001).

**Las dos bibliotecas quedan encerradas en `GT.Infrastructure/Reportes/`**, detrás de dos interfaces
de `GT.Application`, igual que QuestPDF quedó encerrada en `Documentos/` en el Módulo 6. La capa de
aplicación y el dominio no saben que existe un PDF ni una planilla.

**Lo que llama la atención de esta lista**: **cero archivos de `GT.Domain` salvo la constante del
permiso, cero migraciones, y cinco pantallas de otros módulos modificadas** — que es mucho más que
las de cualquier módulo anterior, y por eso están enumeradas una por una en el *Constitution Check*,
como pide la convención [006].

## Complexity Tracking

Tres piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Una dependencia nueva: ClosedXML** | FR-011 y SC-007 exigen que los importes y las fechas del `.xlsx` sean **valores numéricos y de fecha**, ordenables y sumables en la planilla. No hay forma de producir un `.xlsx` real sin una biblioteca (research §1) | `DocumentFormat.OpenXml` a mano: decenas de líneas de XML por reporte para algo que la spec da por resuelto. Un CSV: no es lo que la spec pide y no puede llevar formato de número ni de fecha, con lo cual SC-007 no se cumple. EPPlus: licencia *Polyform Noncommercial*, incompatible con el uso de la empresa |
| **Un modelo intermedio, `ReporteTabular`, entre las consultas y los armadores** | Es lo que convierte 10 armadores en 2 y, sobre todo, lo que hace **estructuralmente imposible** que el PDF y el Excel del mismo reporte difieran, que es lo que la convención [006] persigue (research §2) | Que cada fuente arme su archivo. Descartada: diez caminos al mismo destino, que es el problema exacto que [006] describe — dos traducciones se separan sin que nadie lo note, y ahí revisar un formato deja de decir nada sobre el otro |
| **`ParaTodos(params string[])`: una política con dos requirements** | FR-015 exige **dos** permisos por endpoint y las políticas de ASP.NET se declaran por endpoint (research §5) | Un `if` sobre los claims adentro del handler. Descartada: la convención del Módulo 1 es autorizar por permiso en el pipeline y nunca a mano adentro del endpoint; un `if` es una regla de autorización que el pipeline no ve. Y no agrega maquinaria: el `PermisoHandler` no cambia una línea |

Las tres se resuelven con lo que ya viene en ASP.NET Core y con una biblioteca MIT sin requisitos
nativos — sin servicios externos, sin infraestructura propia y **sin un solo cambio en el
`Dockerfile` ni en el `docker-compose.yml`**.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar la feature por terminada:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una
línea por decisión, con referencia a la spec (`[012] ...`), en la sección *Decisiones transversales
ya tomadas*. No incluir entradas por incluir: sólo las que sean **información transversal y relevante
para el proyecto** que futuras features puedan aprovechar.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[012]` Cuando **una misma información sale en dos formatos de archivo**, lo que evita que se
  separen no es un test que los compare —son incomparables byte a byte— sino un **modelo intermedio
  único** del que los dos armadores leen. Es la convención [006] resuelta eliminando el segundo
  camino en vez de probándolo, y además es lo que permite que una celda lleve **el valor y su tipo**
  y no texto ya formateado: el PDF lo rinde en pesos y la planilla lo escribe como número, desde el
  mismo `decimal`.
- `[012]` La regla del Módulo 6 —*un artefacto generado es función de sus datos y nunca lee el
  reloj*— **no se extiende a un artefacto que es la foto de un listado que cambia**. Ahí el instante
  de generación es parte del dato y entra al archivo. La consecuencia operativa: a ese artefacto
  **no se le escribe un test de igualdad byte a byte**, porque sería un test que pasa o falla según
  el reloj.
- `[012]` Un **`409` con el mensaje ya armado** se prefiere aunque la pantalla tenga el dato para
  rechazar sola: lo que decide dónde vive la regla no es dónde está el dato sino **dónde está el
  texto**. Duplicar en TypeScript un mensaje que el backend ya escribe son dos textos que se separan,
  y ninguna suite lo nota.
- `[012]` **Un permiso de acción y el permiso de lectura de la pantalla de la que sale se exigen
  juntos**, como una política con varios `PermisoRequirement` —ASP.NET exige que todos se satisfagan—
  y nunca con un `if` adentro del endpoint: una exportación no puede ser una puerta lateral a datos
  que la pantalla no muestra.
- `[012]` Una **acción que aparece en varias pantallas conserva el mismo nivel en todas**. Si en una
  compite con la primaria, es secundaria en las cinco: la alternativa —primaria donde queda sola,
  secundaria donde no— le da dos pesos distintos al mismo verbo. Y cuando eso contradice una línea de
  la skill del sistema de diseño, **se corrige la skill en la misma feature** ([008]), no sólo el
  código.
