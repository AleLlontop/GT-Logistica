# Implementation Plan: Gestión de adelantos de sueldo (Módulo 10)

**Branch**: `010-gestion-adelantos` | **Date**: 2026-09-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-gestion-adelantos/spec.md`

## Summary

El Módulo 10 registra los adelantos de sueldo que G&T Logística les da a sus choferes y empleados. Hoy se
anotan sueltos, y se pierde el rastro de **cuánto se le adelantó a cada persona en el mes**.

El módulo registra el adelanto sobre una persona del padrón —chofer propio o empleado— en estado
`pendiente`, y lo lleva por un circuito corto: se **aprueba** o se **rechaza** con motivo, y un aprobado se
**anula** con motivo y confirmación. Nada se borra. El listado filtra por persona, rango de fechas y estado,
y muestra el **total adelantado** de la selección, sumando sólo los aprobados. El detalle muestra el
historial de quién hizo qué y cuándo.

**No modifica nada del backend ni de la base de ningún módulo anterior**: lee personas, fichas de chofer,
transportistas y la empresa emisora sin agregarles una columna, un índice ni una navegación. Del frontend
de los Módulos 6 y 9 toca **exactamente tres pantallas**, para que dejen de armar por su cuenta la fecha de
hoy que este módulo lleva a `compartido`, sin cambiar nada de lo que muestran.

**Enfoque técnico**, con cuatro decisiones que definen el módulo:

1. **Quién puede recibir un adelanto se escribe una sola vez.** Una regla pura de dominio,
   `ElegibilidadDeBeneficiario`, decide sobre la proyección de una persona —activa, tipo, ficha, CUIT del
   transportista— y devuelve el motivo si no es elegible. El desplegable la aplica en memoria sobre las
   personas activas; el guardado, sobre la persona pedida. Lo que se ofrece y lo que se acepta no pueden
   separarse (research §1).
2. **Las carreras se cierran con `UPDATE` condicionales sobre el estado**, la convención [009] tal cual.
   Sin edición, el estado distingue las cuatro carreras y no hace falta `Version` (research §2).
3. **El estado se guarda y un `CHECK` lo ata a los motivos**: un rechazado sin motivo, un anulado sin
   motivo o los dos motivos a la vez son imposibles en la base (research §3, data-model §Adelantos).
4. **El rechazo y la anulación se confirman en el backend**, desde su diálogo, porque los dos llevan a un
   estado final. Aprobar no: se deshace con la anulación (research §4).

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10 sobre
SQL Server, React 19 + React Router + Vite + Tailwind + Radix, `date-fns`). **Ninguna dependencia nueva.
Ninguna variable de entorno nueva**

**Storage**: SQL Server 2022. Una migración nueva, `Modulo10Adelantos`, crea **dos tablas** —`Adelantos` y
`CambiosDeAdelanto`—. **No modifica ninguna tabla existente**. Sin secuencias ni migración de datos. Los dos
permisos nuevos los siembra `SembradorInicial`, idempotente en cada arranque

**Testing**: xUnit en `GT.UnitTests` (reglas puras con el día por parámetro: piso de la fecha cruzando el
año, importe válido, motivos de no resolución y no anulación, y la tabla completa de
`ElegibilidadDeBeneficiario`) y en `GT.IntegrationTests` con `WebApplicationFactory` contra el SQL Server del
compose (desplegable de beneficiarios sobre un padrón con cada caso, registro con cada rechazo, filtros
combinados con el total adelantado y el total vacío, las dos carreras, los `CHECK` con una sola violación
por fila, las rutas literales junto a `{id:int}` y los dos permisos); Vitest + React Testing Library en el
frontend, con consultas por rol, etiqueta y texto (convención [007])

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio actuales.
Sin cambios de `Dockerfile` ni de `docker-compose.yml`

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: el listado y el detalle responden en menos de 1 segundo (p95) con el volumen real.
Filtros, conteo y total adelantado se resuelven **dentro de la consulta SQL**, antes de paginar. El
desplegable de beneficiarios filtra en memoria un padrón de decenas de personas (research §1)

**Constraints**: ningún adelanto nace en otro estado que `pendiente` (FR-010, SC-002); ninguno queda en
dos estados ni con dos resoluciones, ni con dos usuarios simultáneos (FR-026, FR-032); un rechazado y un
anulado siempre tienen motivo (SC-006), garantizado por la base; el rechazo y la anulación no se ejecutan sin
`confirmado: true` (FR-024, FR-029); el total adelantado suma la selección entera y sólo los aprobados (FR-016);
nada se borra ni se modifica (FR-035); **cero cambios de backend, de base o de comportamiento a los
Módulos 2, 3, 6 y 9**, y en su frontend sólo los tres cambios enumerados en spec §Assumptions, verificados
por sus suites sin modificar; los importes son `decimal`, nunca punto flotante

**Scale/Scope**: una única empresa — decenas de personas en el padrón y de adelantos por mes. En este
módulo: **3 pantallas y 2 diálogos nuevos, 8 endpoints nuevos, 2 tablas nuevas, 0 tablas modificadas, 0
secuencias, 2 permisos nuevos y 0 dependencias nuevas**, que cubren **45 requisitos funcionales** (FR-001 a
FR-044, más FR-002a) y **10 criterios de éxito**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.1.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | **Cero dependencias, cero variables de entorno, cero cambios de infraestructura, cero secuencias.** Se evaluaron y descartaron: un predicado SQL más un clasificador en C# con test de comparación, `Version`, `rowversion`, `Serializable`, `UPDLOCK` sobre `Personas`, un endpoint aparte para el total, una columna de acumulado, un número visible y reusar `TipoIntegrante` (research §1 a §5). Las tres piezas que suman algo están en *Complexity Tracking* |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y los mensajes en español rioplatense con voseo, definidos textualmente en `contracts/README.md`. Importes con `compartido/moneda` —`$ 150.000,00`—, fechas con `compartido/fechas` —`dd/mm/aaaa`— |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 45 requisitos y nada más. Queda afuera, como fija la spec: liquidación de haberes y descuento, movimiento de caja, topes, edición, corrección de rechazados, comprobante, notificación, portal y exportación. **Dos tentaciones se anotaron y no se construyen**: una columna *Tipo* en el listado —FR-013 no la pide— y el motivo del rechazo o de la anulación debajo de la pastilla del listado. **La confirmación del rechazo en el backend sí se hace**, por decisión de alcance registrada en la spec (FR-024, research §4). **Llevar la fecha de hoy a `compartido` sí se hace**, por decisión de alcance registrada en la spec, y no agrega comportamiento: reemplaza tres copias (research §10b). Detalles que la spec deja al plan, resueltos sin agregar comportamiento: el largo de los motivos, el nombre accesible de la fila y los tonos de los dos estados nuevos |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 6 historias se validan operando la app con `quickstart.md`: 43 pasos, cada uno con el criterio que verifica, con CUIT y CUIL **cuyo dígito verificador está calculado**. La carrera de resolución y la baja de una persona durante el registro **sí** se prueban a mano, con dos navegadores (pasos 13 y 20). **Lo que no se puede verificar a mano se declara**, cada cosa con su test: el cruce en el mismo instante, los `CHECK`, la invocación directa, el 1 de enero y las rutas literales (research §11) |
| V. Datos del Usuario con Respeto | ✅ Pasa | El adelanto pide **cuatro datos** además de la persona: tipo, fecha, motivo e importe (FR-001 a FR-007). Ni CBU, ni sueldo, ni comprobante. De la persona **no se copia nada**: apellido, nombre y DNI se leen del padrón, y lo único que se congela es el tipo, que la spec exige (FR-020). El historial guarda quién y cuándo; el motivo no se copia. Nada se borra y ningún secreto entra al código |
| VI. Interfaz Gobernada por el Sistema de Diseño | ✅ Pasa | Las tres pantallas y los dos diálogos declaran su acción primaria —una o ninguna— y los estados de cada campo en *UI Design Check*. Se leyeron `tokens.md`, `componentes.md` y `contenido.md`. Se reusan las primitivas de `compartido/ui`; **no se agrega ningún componente compartido ni ningún token**, y `Estado.tsx` recibe dos valores en su tabla de tonos |

**Sobre el Principio III y los módulos anteriores**: la spec acota los cambios a módulos anteriores a
**tres**, numerados en *Assumptions*, y research §10b los enumera uno por uno para que la revisión pueda
contarlos (convención [006]). Los tres son el mismo refactor —borrar una función local y usar la
compartida— y **ninguno cambia lo que la pantalla muestra**. La prueba son sus suites existentes, sin
modificar ningún caso; como ninguna miraba la fecha propuesta, cada una **suma** un caso que la busca
antes del cambio (convención [009]). El resto de los archivos que se modifican son **puntos de extensión**
que cada módulo toca para existir: registro de servicios, catálogo de menú, códigos de permiso, sembrador,
`DbContext`, rutas de `App.tsx`, sección del menú, tonos de `Estado` y constantes de permiso del frontend.
Y **un test del Módulo 2** cambia su aserción por el permiso nuevo de Gerencia, igual que en los Módulos 6 y
9 (research §7).

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los seis principios se sostienen. Cinco cosas que el diseño confirmó o
descubrió, y que conviene tener a la vista al implementar:

- **La regla difícil no era el ciclo de vida sino quién puede recibir un adelanto.** Los estados, las
  carreras y la confirmación salen de las convenciones del 009 sin inventar nada. Lo que no tenía
  precedente es una regla sobre datos de **tres** módulos anteriores —persona del 2, ficha y transportista
  del 3, empresa emisora del 6— que la pantalla y el guardado tienen que aplicar igual, y que además tiene
  que decir el motivo. Escribirla una vez como función pura resolvió las dos cosas, y cambia de lugar el
  criterio de [003]: el predicado va al árbol cuando la consulta **tiene** que ser SQL, no siempre.
- **Sin edición no hace falta `Version`.** El Módulo 9 la necesitó porque dos ediciones veían el mismo
  estado. Acá cada operación mueve el estado, así que la condición sobre el estado distingue todas las
  carreras. Es la convención [009] en su caso más simple.
- **Hay una carrera que no se cierra, y se declara.** Una baja de la persona entre la validación y el
  `INSERT` no tiene condición ni índice que la exprese sin tocar el Módulo 2, y su resultado es un estado
  que la spec ya admite. La consulta previa cubre el caso que se ve en pantalla (research §2).
- **`LEN(NULL)` en un `CHECK` habría dejado pasar un rechazado sin motivo.** La comparación da `UNKNOWN`, y
  un `CHECK` sólo rechaza `FALSE`. Escribir el `IS NOT NULL` delante no es redundancia: es lo que hace que
  la restricción funcione (research §9.2).
- **La cuarta copia destapó que la tercera ya se había escrito.** La convención [009] pide llevar un
  formato a `compartido` con la tercera necesidad, y la fecha de hoy ya tenía tres copias en los Módulos 6
  y 9 sin que nadie lo notara. Ninguna suite miraba la fecha propuesta, así que el refactor empieza por
  agregar esos tres casos.

## UI Design Check

*GATE (Principio VI): obligatorio si la feature toca UI. Re-evaluar después de Phase 1.*

Toda interfaz sigue `.claude/skills/gt-ui/SKILL.md`. Para esta sección se leyeron `references/tokens.md`,
`references/componentes.md` y `references/contenido.md`, más los patrones `viajes-listado.png` (listado),
`nuevo-chofer.png` (registro) y `viaje-detalle.png` (detalle). Los textos exactos están en
`contracts/README.md`.

### Acción primaria por pantalla

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| Listado de adelantos — con `gestionar` | `Registrar adelanto` | — | — |
| Listado de adelantos — sólo `consultar` | `ninguna` | — | — |
| Registrar adelanto | `Guardar adelanto` | `Cancelar`, `Volver a adelantos` (terciaria), enlace `Empresa emisora` en el callout | — |
| Detalle — `pendiente`, con `gestionar` | `Aprobar adelanto` | `Rechazar adelanto`, `Volver a adelantos` | — |
| Detalle — `aprobado`, con `gestionar` | `ninguna` | `Volver a adelantos` | `Anular adelanto` |
| Detalle — `rechazado`, `anulado`, o sólo `consultar` | `ninguna` | `Volver a adelantos` | — |
| Diálogo *Rechazar adelanto* | `Rechazar adelanto` | `Volver` | — |
| Diálogo *Anular adelanto* | `ninguna` (la acción es destructiva) | `Volver` | `Anular adelanto` |
| Cualquiera de las tres pantallas, **sin su permiso** (US6 esc. 5) | `ninguna`: sólo el título y el aviso | — | — |

**Rechazar no es destructivo.** `gt-ui` reserva el rojo para eliminar y anular, y rechazar un pendiente no
borra ni revierte nada firme: el dinero nunca contó como adelantado. Que se confirme en el backend porque no
se deshace (research §4) no lo vuelve rojo. En el detalle es secundaria
porque compite con aprobar, que es la resolución esperada; dentro de su diálogo es la única decisión, y es
primaria.

**En el detalle de un `aprobado` no hay primario**: la única acción es la anulación, destructiva, y
ofrecerle un primario al lado sería inventarlo.

### Estados de campo

El error aparece recién después de que el campo perdió el foco habiendo sido tocado, **o** al intentar la
acción que lo necesita —*Guardar adelanto*, *Rechazar adelanto*, *Anular adelanto*—; nunca en el primer
render. El foco es `border-brand` + `bg-white` + `shadow-focus` (anillo de 3 px), visible por sí solo. El
error es `border-danger` + `bg-danger-bg` + mensaje en `text-danger-text` con ícono, a través de `Campo`.
Los obligatorios se marcan con contenido generado desde `required` y no con un `<span>` en el `<label>`
(convención [008]).

| Pantalla | Campo | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Registrar | Tipo de persona | desplegable `corto`, `bg-surface-soft`, `border-line-strong` | anillo `shadow-focus` | `Elegí si el adelanto es para un chofer o un empleado.` | `Seleccioná el tipo` |
| Registrar | Persona | desplegable `largo`. **No se deshabilita** sin tipo: sólo ofrece su texto vacío | anillo | sin tipo: `Elegí primero el tipo de persona.` · con tipo: `Elegí la persona que recibe el adelanto.` | sin tipo: `Elegí primero el tipo de persona` · con tipo: `Seleccioná una persona` · sin personas del tipo: el mismo texto vacío y el mensaje de FR-004 debajo, con `role="status"` |
| Registrar | Fecha | fecha `corto`, propuesta hoy, con `min` y `max`; ayuda `Desde el {piso} hasta hoy.` | anillo | `Elegí la fecha en que se otorgó el adelanto.` · `La fecha tiene que estar entre el {piso} y hoy.` | nunca vacía al abrir: viene hoy |
| Registrar | Motivo | texto `largo`, hasta 200 | anillo | `Escribí el motivo del adelanto. Ejemplo: gastos médicos` | `Gastos médicos` |
| Registrar | Importe | el control de importe de la orden de pago del Módulo 9, `corto`, alineado a la derecha | anillo | `Escribí un importe mayor que cero. Ejemplo: 150000,00` · `Escribí el importe con hasta dos decimales. Ejemplo: 150000,50` | `150000,00` |
| Listado | Persona (filtro) | desplegable compacto `rounded-field`; con valor elegido, borde `line-strong` más marcado | anillo | no aplica: la selección es cerrada y "todas" es válido | `Todas las personas` |
| Listado | Desde (filtro) | fecha compacta; con valor, borde más marcado | anillo | con *Hasta* anterior: los dos campos en error, y debajo de *Hasta* `La fecha desde es posterior a la hasta. Corregí una de las dos.`, al cambiar cualquiera de los dos | vacío: sin límite inferior |
| Listado | Hasta (filtro) | ídem | anillo | ídem | vacío: sin límite superior |
| Listado | Estado (filtro) | desplegable compacto | anillo | no aplica | `Todos los estados` |
| Diálogo rechazar | Motivo | área de texto a todo el ancho, 3 líneas, hasta 500 | anillo | `Escribí el motivo del rechazo: queda en el historial.` | `Ya tiene un adelanto pendiente del mes anterior.` |
| Diálogo anular | Motivo | área de texto a todo el ancho, 3 líneas, hasta 500 | anillo | `Escribí el motivo de la anulación: queda en el historial.` | `Cargado sobre la persona equivocada.` |
| Detalle | — | **sin campos de entrada**: *Aprobar adelanto* ejecuta sin diálogo | — | — | — |

**Deshabilitados**: sólo los botones mientras esperan al servidor —*Guardar adelanto*, *Aprobar adelanto*,
los botones de los diálogos—, con `disabled:opacity-50` y `cursor-not-allowed`. Ningún botón se deshabilita
para indicar un campo vacío: apretarlo marca el campo con el error, que dice qué hacer.

## Project Structure

### Documentation (this feature)

```text
specs/010-gestion-adelantos/
├── plan.md                        # Este archivo
├── research.md                    # Decisiones técnicas y alternativas descartadas
├── data-model.md                  # Tablas, restricciones, reglas, consultas y transacciones
├── quickstart.md                  # Cómo levantar y validar el módulo (43 pasos)
├── contracts/
│   ├── README.md                  # Contrato de UI: pantallas, diálogos y textos
│   └── adelantos-api.yaml         # Contrato HTTP (OpenAPI 3.0)
├── checklists/requirements.md     # De /speckit-specify
└── tasks.md                       # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Adelantos/                               # NUEVO
│   │   │   ├── AdelantosEndpoints.cs                #   beneficiarios, personas (literales), listado,
│   │   │   │                                        #   detalle, registro ({id:int})
│   │   │   ├── CicloDeVidaAdelantoEndpoints.cs      #   aprobación, rechazo, anulación
│   │   │   └── RespuestasDeAdelanto.cs              #   resultado → HTTP, en un solo lugar (research §6)
│   │   └── Program.cs                               # MODIFICADO — servicios, políticas, grupos
│   ├── GT.Application/
│   │   ├── Adelantos/                               # NUEVO — carpeta espejo del módulo
│   │   │   ├── ConsultarBeneficiarios.cs
│   │   │   ├── ConsultarPersonasConAdelantos.cs
│   │   │   ├── RegistrarAdelanto.cs
│   │   │   ├── ConsultarAdelantos.cs
│   │   │   ├── ConsultarDetalleAdelanto.cs          #   también la relectura de las cuatro escrituras
│   │   │   ├── AprobarAdelanto.cs
│   │   │   ├── RechazarAdelanto.cs
│   │   │   ├── AnularAdelanto.cs
│   │   │   ├── IRepositorioAdelantos.cs
│   │   │   ├── ResultadoAdelanto.cs
│   │   │   ├── Dtos.cs                              #   incluye PaginaDeAdelantos
│   │   │   ├── NombresDeEstadoAdelanto.cs
│   │   │   └── Mensajes.cs                          #   textos en es-AR y códigos de error
│   │   └── Autenticacion/CatalogoOpcionesMenu.cs    # MODIFICADO — dos entradas
│   ├── GT.Domain/
│   │   ├── Adelantos/                               # NUEVO
│   │   │   ├── Adelanto.cs
│   │   │   ├── CambioDeAdelanto.cs
│   │   │   ├── EstadoAdelanto.cs                    #   4 valores; su orden sostiene un CHECK
│   │   │   ├── OperacionDeAdelanto.cs
│   │   │   ├── TipoBeneficiario.cs                  #   2 valores; su orden sostiene un CHECK
│   │   │   ├── ReglasDeAdelanto.cs                  #   fecha, importe, estados; día por parámetro
│   │   │   └── ElegibilidadDeBeneficiario.cs        #   la única escritura de FR-002 y FR-009
│   │   └── Usuarios/Rol.cs                          # MODIFICADO — dos códigos de permiso
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── Configuraciones/                     # NUEVO — AdelantoConfiguracion, CambioDeAdelantoConfiguracion
│       │   ├── RepositorioAdelantos.cs              # NUEVO — proyecciones, listado con total, 4 transacciones
│       │   ├── GtDbContext.cs                       # MODIFICADO — 2 DbSet
│       │   └── Migraciones/                         # NUEVO — Modulo10Adelantos
│       └── DatosIniciales/SembradorInicial.cs       # MODIFICADO — dos permisos y su reparto
└── tests/
    ├── GT.UnitTests/Adelantos/                      # NUEVO — ReglasDeAdelanto, ElegibilidadDeBeneficiario
    ├── GT.IntegrationTests/Adelantos/               # NUEVO — beneficiarios, registro, consulta y total,
    │                                                #   personas con adelantos, detalle, aprobación y
    │                                                #   rechazo, anulación, carreras, restricciones,
    │                                                #   rutas, permisos
    └── GT.IntegrationTests/Usuarios/AsignarRolesTests.cs  # MODIFICADO (Módulo 2) — Gerencia suma adelantos.consultar

frontend/
└── src/
    ├── modules/adelantos/                           # NUEVO
    │   ├── paginas/                                 #   ListadoAdelantos, RegistrarAdelanto,
    │   │                                            #   DetalleAdelanto (+ tests)
    │   ├── componentes/                             #   FiltrosAdelantos, DialogoRechazo,
    │   │                                            #   DialogoAnulacion (+ tests)
    │   └── servicios/servicioAdelantos.ts           #   incluye primeraFechaAdmitida(hoy)
    ├── modules/autenticacion/servicios/sesion.ts    # MODIFICADO — dos constantes de permiso
    ├── modules/facturacion/paginas/
    │   ├── FichaFactura.tsx                         # MODIFICADO (Módulo 6) — cambio 1: usa compartido/fechas
    │   ├── FichaFactura.test.tsx                    # MODIFICADO (Módulo 6) — suma un caso: fecha de cobro propuesta
    │   ├── AltaFactura.tsx                          # MODIFICADO (Módulo 6) — cambio 2: usa compartido/fechas
    │   └── AltaFactura.test.tsx                     # MODIFICADO (Módulo 6) — suma un caso: fecha de facturación propuesta
    ├── modules/liquidaciones/paginas/
    │   ├── DetalleLiquidacion.tsx                   # MODIFICADO (Módulo 9) — cambio 3: usa compartido/fechas
    │   └── DetalleLiquidacion.test.tsx              # MODIFICADO (Módulo 9) — suma un caso: fecha de pago propuesta
    ├── compartido/fechas.ts                         # MODIFICADO — enIso, hoyEnIso (research §10b)
    ├── compartido/fechas.test.ts                    # MODIFICADO — sus casos, con el reloj a las 23:30
    ├── compartido/ui/Estado.tsx                     # MODIFICADO — tonos de `aprobado` y `rechazado`
    ├── compartido/seccionesDeMenu.ts                # MODIFICADO — dos códigos en `Operación`
    └── App.tsx                                      # MODIFICADO — tres rutas
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Adelantos/` como carpeta espejo del módulo, alineada con `specs/010-gestion-adelantos/` y
con `frontend/src/modules/adelantos/`.

`DialogoAnulacion` se llama igual que el del Módulo 9 y **no se comparte**: el texto, el cuerpo y el
resultado son del adelanto, y lo que sí es común —superficie, foco, `Escape`— ya lo da `Dialogo`
(convención [007]).

**Lo que llama la atención de esta lista**: la columna de MODIFICADOS tiene **dieciocho archivos**. Once son
puntos de extensión o `compartido`, **seis son de los Módulos 6 y 9** —tres pantallas y sus tres suites, que
sólo suman un caso— y uno es un test del Módulo 2. **Ninguno es un archivo de negocio del backend de otro
módulo.**

## Complexity Tracking

Tres piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **El desplegable de beneficiarios filtra en memoria**, a contramano de la letra de la convención [003] | La regla tiene que clasificar el motivo para FR-009; escrita una vez en C#, el desplegable y el guardado no pueden separarse | Predicado en el árbol de la consulta más un clasificador en C# y un test que los compare. Descartada: dos escrituras de la regla para un desplegable de decenas de filas sin paginar. Límite anotado: con miles de personas, pasa a SQL (research §1) |
| **Dos columnas de motivo atadas por un `CHECK`** con `IS NOT NULL` explícito | FR-024 y FR-029 como garantía de la base; dos nombres dicen qué motivo es sin mirar el estado | Una columna `MotivoDeCierre`, o motivos sin `CHECK`. Descartadas: la primera es ambigua para quien lea la tabla; la segunda deja la obligatoriedad en el código (research §3, §9.2) |
| **Tres pantallas de los Módulos 6 y 9 se tocan** | Cuarta necesidad del mismo formato; la convención [009] pide llevarlo a `compartido` | Una cuarta copia local. Descartada por decisión de alcance registrada en la spec (research §10b) |

Las tres se resuelven con lo que ya viene en EF Core, SQL Server y el frontend —`CHECK`,
`ExecuteUpdateAsync`, una función pura y un archivo compartido que ya existía—, sin bibliotecas, servicios
externos ni infraestructura propia.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una línea
por decisión, con referencia a la spec (`[010] ...`), en la sección *Decisiones transversales ya tomadas*.
No incluir entradas por incluir: sólo las que sean **información transversal y relevante para el
proyecto** que futuras features puedan aprovechar.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[010]` Una **regla de elegibilidad que ofrece un desplegable y además valida el guardado con motivo** se
  escribe **una vez como función pura** sobre una proyección, y las dos consultas la aplican. Filtrar en
  memoria es la decisión correcta cuando la lista no se pagina y es chica: la convención [003] protege a
  una consulta que **tiene** que ser SQL de evaluarse en memoria sin que nadie lo note, no prohíbe elegirlo.
- `[010]` En un `CHECK`, **toda comparación sobre una columna anulable va detrás de su `IS NOT NULL`**:
  `LEN(NULL) > 0` es `UNKNOWN`, un `CHECK` sólo rechaza `FALSE`, y la fila que la restricción debía impedir
  pasa. **No falla**: acepta.
- `[010]` Con el ciclo de vida **sin edición**, la condición sobre el estado del `UPDATE` condicional
  distingue todas las carreras y **no hace falta `Version`**: se agrega sólo cuando dos operaciones pueden ver
  el mismo estado y dejarlo igual, como dos ediciones.
- `[010]` Una **carrera que no se puede cerrar sin bloquear filas de otro módulo** —una baja de la persona
  entre la validación y el `INSERT`— se **declara** en vez de cerrarse, cuando su resultado es un estado que
  la spec ya admite. La consulta previa cubre el caso visible.
- `[010]` Una convención con umbral —la tercera necesidad de [009]— **se revisa contando al empezar cada
  feature**, no al escribir la copia: la fecha de hoy llegó a tres copias en dos módulos sin que ninguno lo
  notara, y ninguna suite miraba el dato.
- `[010]` Una **pantalla a la que se llega sin permiso escribiendo su dirección** dice que falta permiso y a
  quién pedírselo, no que falló la carga: "volvé a intentar" es falso para un `403`. La decide el `403` de su
  carga cuando la tiene, y el permiso de la sesión sólo donde no carga nada al abrir. Los módulos anteriores
  siguen con el mensaje genérico hasta que una spec lo cambie.
