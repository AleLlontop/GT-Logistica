# Implementation Plan: Gestión de liquidación a transportistas (Módulo 9)

**Branch**: `009-gestion-liquidacion` | **Date**: 2026-09-14 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-gestion-liquidacion/spec.md`

## Summary

El Módulo 9 le paga al fletero lo que el Módulo 5 registró. Hoy la liquidación mensual se arma a mano
juntando planillas, con dos riesgos: **liquidar dos veces el mismo viaje** y **olvidarse alguno**.

El módulo genera la liquidación de un transportista externo para un período agrupando todos sus viajes
rendidos —también los ya facturados al cliente— que no estén en otra liquidación vigente. Trae además
el listado con filtros por transportista, período y estado, el detalle con viajes, órdenes de pago e
historial, la **edición** de los viajes de una liquidación todavía sin pagos, la **anulación** con motivo
que libera los viajes, y el **registro de órdenes de pago**, con la liquidación pasando sola a `pagada`
cuando no resta nada.

**El valor central es la trazabilidad viaje ↔ liquidación ↔ orden de pago**, y la garantía de que un
viaje nunca esté en dos liquidaciones vigentes.

**No modifica nada del backend ni de la base de ningún módulo anterior**: lee transportistas, viajes y
la empresa emisora sin agregarles una columna, un índice ni una navegación. Del frontend de los Módulos 3
y 5 toca **exactamente dos pantallas**, para que dejen de repetir el formato del CUIT que este módulo lleva
a `compartido`, sin cambiar nada de lo que muestran. Y es el primero con **cuatro carreras distintas** que
cerrar —generar, pagar, editar y anular—.

**Enfoque técnico**, con cinco decisiones que definen el módulo:

1. **El vínculo viaje ↔ liquidación va en una tabla propia con marca de vigencia**, y no en una columna
   de `Viajes` como hizo el Módulo 6 con `FacturaId`. Una columna tocaría el Módulo 5, que la spec
   excluye, y al anular perdería qué viajes agrupaba la liquidación, que FR-028 exige mostrar. Un
   **índice único filtrado sobre los vínculos vigentes** es la garantía de que un viaje no esté en dos
   liquidaciones (research §1).
2. **Las otras tres carreras son sobre la misma fila y se cierran con `UPDATE` condicionales.** Pagar,
   editar y anular empiezan actualizando la fila de la liquidación con una condición —pendiente, sin
   pagos, el importe entra, la edición sobre la misma versión que se abrió— y verifican que haya
   afectado una fila. Eso obliga a **guardar el total y lo
   pagado** en la liquidación; un `CHECK` y un test de coherencia impiden que discrepen de las sumas
   (research §2).
3. **El estado se guarda y un `CHECK` lo ata a los importes.** `pagada` exige pagado igual al total,
   `pendiente` exige saldo y `anulada` exige cero pagado y motivo. Con eso FR-030, FR-031 y FR-053 son
   garantías de la base, y el filtro del listado opera sobre la columna sin escribir la regla dos veces
   (research §3).
4. **Externo es "CUIT distinto del de la empresa emisora"**, resuelto con una consulta sobre datos que ya
   están normalizados en los Módulos 3 y 6. Sin empresa emisora configurada no se puede generar, y la
   pantalla lo dice con su salida (research §5).
5. **Los dos pasos irreversibles se confirman en el backend.** La orden de pago responde `409` con el
   saldo resultante calculado por el servidor; la anulación viaja confirmada desde su diálogo, que ya es
   la confirmación explícita. Sin `confirmado: true`, ninguna de las dos se ejecuta (research §7).

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10 sobre
SQL Server, React 19 + React Router + Vite + Tailwind + Radix). **Ninguna dependencia nueva. Ninguna
variable de entorno nueva**: la liquidación no genera archivos

**Storage**: SQL Server 2022. Una migración nueva, `Modulo9Liquidaciones`, crea **cinco tablas** —
`Liquidaciones`, `LiquidacionViajes`, `OrdenesDePago`, `CambiosDeLiquidacion`,
`CambiosDeLiquidacionViajes`— y **dos secuencias** —
`NumeroDeLiquidacion`, `NumeroDeOrdenDePago`—. **No modifica ninguna tabla existente**. Sin migración de
datos. Los dos permisos nuevos los siembra `SembradorInicial`, idempotente en cada arranque

**Testing**: xUnit en `GT.UnitTests` (reglas puras con fecha por parámetro: período admitido, rango de la
fecha de pago, estado tras un pago, motivos de no edición y no anulación, formato `LQ-`/`OP-`) y en
`GT.IntegrationTests` con `WebApplicationFactory` contra el SQL Server del compose (el índice filtrado y
la carrera de generación, las tres carreras sobre la fila de la liquidación, el `CHECK` del estado en
cada valor, la coherencia de total, pagado y vigencia contra sus fuentes, la consulta de disponibles y
la de transportistas externos, las rutas literales junto a `{id:int}`, y los dos permisos); Vitest +
React Testing Library en el frontend, con consultas por rol, etiqueta y texto (convención [007])

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio actuales.
Sin cambios de `Dockerfile` ni de `docker-compose.yml`

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: el listado y el detalle responden en menos de 1 segundo (p95) con el volumen real.
Los filtros, la resta pagar y la exclusión de viajes liquidados se resuelven **dentro de la consulta
SQL**, antes de paginar. La consulta de disponibles filtra por transportista y período, con los índices
que `Viajes` ya tiene sobre `TransportistaId` y `Fecha`

**Constraints**: ningún viaje en dos liquidaciones vigentes, ni con dos operadores simultáneos (FR-014,
SC-002); lo pagado nunca supera el total, ni con dos pagos simultáneos (FR-043, SC-007); ninguna
liquidación con pagos se edita ni se anula, ni cruzándose con un pago (FR-045, FR-053); dos ediciones de
la misma liquidación no se pisan: la segunda se rechaza (FR-048); generar, editar,
anular y pagar son todo o nada (FR-018, FR-051, FR-060); una anulada sigue mostrando sus viajes (FR-028);
**cero cambios de backend, de base o de comportamiento a los Módulos 3, 5 y 6**, y en su frontend sólo
los dos cambios enumerados en spec §Assumptions, verificados por sus suites sin modificar; los importes
son `decimal`, nunca punto flotante

**Scale/Scope**: una única empresa — decenas de liquidaciones por mes, decenas de fleteros. En este
módulo: **4 pantallas y 2 diálogos nuevos, 8 endpoints nuevos, 5 tablas nuevas, 0 tablas modificadas,
2 secuencias, 2 permisos nuevos y 0 dependencias nuevas**, que cubren **68 requisitos funcionales**
(FR-001 a FR-066, más FR-001a y FR-012a) y **14 criterios de éxito**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.1.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | **Cero dependencias, cero variables de entorno, cero cambios de infraestructura.** Todo lo transversal se consume tal como está (research §11). Se evaluaron y descartaron: una columna en `Viajes`, una segunda tabla para los viajes de las anuladas, `rowversion`, aislamiento `Serializable`, bloqueos escritos a mano, una columna de fecha de generación, una de resta pagar y una pantalla aparte para la orden de pago (research §1, §2, §3, §10; data-model §*Lo que este modelo deliberadamente no tiene*). Las tres piezas que suman algo están justificadas en *Complexity Tracking* |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y los mensajes en español rioplatense con voseo, definidos textualmente en `contracts/README.md`. Importes con `compartido/moneda` —`$ 355.000,00`—, fechas con `compartido/fechas` —`dd/mm/aaaa`—, períodos `MM/AAAA` |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 68 requisitos y nada más. Queda afuera, como fija la spec: corregir liquidaciones con pagos, modificar o anular órdenes de pago, cambiar transportista o período, tarifas y retenciones, documento imprimible, envío por correo, portal del transportista, totales en pantalla propia y exportación. **Dos tentaciones concretas se anotaron y no se construyen**: un enlace desde la ficha del viaje a su liquidación y un cuadro de lo adeudado por fletero. Qué viajes cambió cada edición **sí** se registra, porque la spec lo pide desde su clarificación (FR-033, research §3b). **Llevar el formato del CUIT a `compartido` sí se hace**, por decisión de alcance registrada en la spec, y no agrega comportamiento: reemplaza dos copias idénticas (research §11b). **Dos detalles que la spec deja al plan** se resuelven sin agregar comportamiento: el formato del número (`LQ-12`, research §4) y que una edición sin cambios no escriba nada (data-model §Editar) |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 7 historias se validan operando la app con `quickstart.md`: 43 pasos, cada uno con el criterio que verifica, con datos de ejemplo cuyos CUIT **pasan la validación**. **Cinco cosas no se pueden verificar a mano y se declara por qué**, cada una con su test: las carreras de pago, edición y anulación, la coherencia de los agregados, el `CHECK` del estado, la confirmación en invocación directa y las rutas literales. La carrera de generación **sí** se prueba a mano con dos navegadores (paso 15) |
| V. Datos del Usuario con Respeto | ✅ Pasa | La orden de pago pide **dos datos**, fecha e importe (FR-036): ni medio de pago, ni cuenta bancaria, ni comprobante adjunto. El historial guarda quién y cuándo y, en las ediciones, sólo qué viajes se quitaron y agregaron (FR-033): ni importes ni composiciones copiadas. Del transportista no se copia nada: se lee del padrón. Ningún dato se borra físicamente —lo único que se borra es el vínculo de un viaje quitado de una liquidación todavía sin pagos— y ningún secreto entra al código |
| VI. Interfaz Gobernada por el Sistema de Diseño | ✅ Pasa | Las cuatro pantallas y los dos diálogos declaran su acción primaria —una o ninguna— y los estados de cada campo en *UI Design Check*. Se leyeron `tokens.md`, `componentes.md` y `contenido.md`. Se reusan las primitivas de `compartido/ui` de los Módulos 7 y 8; **no se agrega ningún componente compartido** ni ningún token |

**Sobre el Principio III y los módulos anteriores**: la spec acota los cambios a módulos anteriores a
**dos**, numerados en *Assumptions*, y el diseño los enumera uno por uno en research §11b para que la
revisión pueda contarlos (convención [006]). Los dos son el mismo refactor en dos pantallas —borrar una
función local y usar la compartida— y **ninguno cambia lo que la pantalla muestra**: la prueba son sus
suites existentes, que no se modifican. El resto de los archivos que se modifican son **puntos de
extensión** que cada módulo toca para existir —el registro de servicios, el catálogo de menú, los códigos
de permiso, el sembrador, el `DbContext`, las rutas de `App.tsx`, la sección del menú y la constante de
permisos del frontend—.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los seis principios se sostienen. Cinco cosas que el diseño confirmó o
descubrió, y que conviene tener a la vista al implementar:

- **El precedente del Módulo 6 no se aplicaba, y copiarlo habría roto dos requisitos.** Lo natural era
  repetir `Viajes.FacturaId` como `Viajes.LiquidacionId`: tocaba el Módulo 5, que la spec excluye, y al
  anular perdía los viajes de la anulada. Es la convención [006] en su forma más útil —**la convención
  nombra el objetivo, no el mecanismo**—, pero al revés que allá: el Módulo 6 descartó el índice filtrado
  porque el dato era escalar; acá vuelve porque el dato no lo es.
- **Las carreras eran cuatro y no una.** La spec sólo nombra la del viaje en dos liquidaciones, pero el
  pago, la edición y la anulación compiten sobre la misma fila. Verlas juntas es lo que llevó a un único
  mecanismo —`UPDATE` condicional sobre la fila de la liquidación— en vez de tres soluciones distintas.
  **La quinta la encontró el checklist de ciclo de vida** (CHK025): dos ediciones de la misma liquidación
  pasan las dos condiciones de estado y de pago, y la segunda podía aplicar una diferencia de viajes
  calculada sobre una composición vieja. Entró en el mismo mecanismo con un dato más, `Version`.
- **Guardar los agregados era el precio de cerrarlas, y un `CHECK` lo abarata.** La convención [003]
  prefiere derivar, y total y pagado son sumas. Pero sin ellos en la fila no hay condición que poner en
  el `UPDATE`. Con el `CHECK`, FR-043 pasa de validación a garantía, y el costo queda en un test que
  compara cada agregado con su suma.
- **Un `CHECK` sobre el estado vuelve innecesario derivarlo.** `pagada` era el candidato natural a estado
  derivado, con el costo que [006] documentó para `vencida`: escribir el filtro dos veces. Atado por un
  `CHECK` a los importes, el estado guardado **no puede** discrepar, y el filtro opera sobre la columna.
- **Las dos confirmaciones del backend no son iguales, y está bien.** La de la orden de pago tiene que
  mostrar un número que sólo el servidor sabe con certeza —el saldo después del pago— y hace el viaje de
  ida y vuelta; la de la anulación no tiene nada que mostrar y viaja confirmada desde el diálogo. Las dos
  cumplen [005]: sin `confirmado`, el backend no ejecuta.

## UI Design Check

*GATE (Principio VI): obligatorio si la feature toca UI. Re-evaluar después de Phase 1.*

Toda interfaz sigue `.claude/skills/gt-ui/SKILL.md`. Para esta sección se leyeron `references/tokens.md`,
`references/componentes.md` y `references/contenido.md`, más los patrones `viajes-listado.png` (listado),
`nuevo-chofer.png` (generar y editar) y `viaje-detalle.png` (detalle). Los textos exactos están en
`contracts/README.md`.

### Acción primaria por pantalla

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| Listado de liquidaciones — con `gestionar` | `Generar liquidación` | — | — |
| Listado de liquidaciones — sólo `consultar` | `ninguna` | — | — |
| Generar liquidación | `Guardar liquidación` (habilitado con viajes y total > 0) | `Buscar viajes` (secundaria de sección), `Cancelar`, `Volver a liquidaciones` (terciaria) | — |
| Generar liquidación — sin empresa emisora o sin transportistas | `ninguna` | enlace `Empresa emisora` / `Transportistas`, `Volver a liquidaciones` | — |
| Detalle — `pendiente` sin pagos, con `gestionar` | `Registrar orden de pago` | `Editar liquidación`, `Volver a liquidaciones` | `Anular liquidación` |
| Detalle — `pendiente` con pagos, con `gestionar` | `Registrar orden de pago` | `Volver a liquidaciones` | — |
| Detalle — `pagada`, `anulada`, o sólo `consultar` | `ninguna` | `Volver a liquidaciones` | — |
| Editar liquidación | `Guardar cambios` (habilitado con viajes, total > 0 y algún cambio) | `Quitar` / `Agregar` por fila, `Cancelar`, `Volver a la liquidación` | — |
| Editar liquidación — no editable al abrir | `ninguna` | `Volver a la liquidación` | — |
| Diálogo *Registrar orden de pago*, paso 1 | `Registrar orden de pago` | `Cancelar` | — |
| Diálogo *Registrar orden de pago*, paso 2 | `Confirmar orden de pago` | `Volver` | — |
| Diálogo *Anular liquidación* | `ninguna` (la acción es destructiva) | `Volver` | `Anular liquidación` |

**`Quitar` y `Agregar` por fila** son botones secundarios chicos, siempre en el DOM y con nombre
accesible que nombra el viaje —`Quitar viaje #13`—: un control que aparece al pasar el mouse es
inalcanzable con teclado (convención [008]). No van en un menú `···` porque son la operación de la
pantalla, no una acción secundaria de la fila.

**En el diálogo de anulación no hay primario** y el destructivo ocupa su lugar: ofrecer un primario al
lado de *Anular* sería competir con la única decisión del diálogo.

### Estados de campo

El error aparece recién después de que el campo perdió el foco habiendo sido tocado, **o** al intentar
la acción que lo necesita —*Buscar viajes*, *Registrar orden de pago*, *Anular liquidación*—; nunca en el
primer render. El foco es `border-brand` + `bg-white` + `shadow-focus` (anillo de 3 px), visible por sí
solo. El error es `border-danger` + `bg-danger-bg` + mensaje en `text-danger-text` con ícono. Los
obligatorios se marcan con `:has(:required)::after` y no con un `<span>` en el `<label>` (convención
[008]).

| Pantalla | Campo | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Generar | Transportista | desplegable ~420 px, `bg-surface-soft`, `border-line-strong` | anillo `shadow-focus` | `Elegí el transportista al que le vas a liquidar.` | `Seleccioná un transportista` |
| Generar | Mes | desplegable ~120 px | anillo | `Elegí el mes del período.` | `Mes` |
| Generar | Año | desplegable ~120 px | anillo | `Elegí el año del período.` | `Año` |
| Listado | Transportista (filtro) | desplegable compacto `rounded-field`; con valor elegido, borde `line-strong` más marcado | anillo | no aplica: la selección es cerrada y "todos" es válido | `Todos los transportistas` |
| Listado | Mes (filtro) | desplegable compacto | anillo | no aplica | `Todos los meses` |
| Listado | Año (filtro) | desplegable compacto | anillo | no aplica | `Todos los años` |
| Listado | Estado (filtro) | desplegable compacto | anillo | no aplica | `Todos los estados` |
| Diálogo orden de pago | Fecha de pago | fecha ~200 px, propuesta hoy; ayuda `Entre el {generación} y hoy.` | anillo | `Elegí la fecha en que se pagó.` · `La fecha de pago tiene que estar entre el {desde} y hoy.` | nunca vacío al abrir: viene hoy |
| Diálogo orden de pago | Importe | número ~200 px, alineado a la derecha, propuesto lo que resta; ayuda `Hasta $ {resta}.` | anillo | `Escribí un importe mayor que cero. Ejemplo: 120000,00` · `El importe supera lo que resta pagar: $ {resta}.` | nunca vacío al abrir: viene lo que resta |
| Diálogo anular | Motivo | área de texto a todo el ancho, 3 líneas, hasta 500 | anillo | `Escribí el motivo de la anulación: queda en el historial.` | `El período no correspondía: los viajes eran de junio.` |
| Editar | — | **sin campos de entrada**: transportista y período son texto de sólo lectura, y la edición se hace con `Quitar` / `Agregar` por fila | — | — | — |

Los **deshabilitados** —*Guardar liquidación*, *Guardar cambios*— usan `disabled:opacity-50` con
`cursor-not-allowed`, y la razón está siempre escrita en pantalla (sin viajes, total en cero), nunca sólo
en el estado del botón.

## Project Structure

### Documentation (this feature)

```text
specs/009-gestion-liquidacion/
├── plan.md                        # Este archivo
├── research.md                    # Decisiones técnicas y alternativas descartadas
├── data-model.md                  # Tablas, restricciones, índices y transacciones
├── quickstart.md                  # Cómo levantar y validar el módulo (43 pasos)
├── contracts/
│   ├── README.md                  # Contrato de UI: pantallas, diálogos y textos
│   └── liquidaciones-api.yaml     # Contrato HTTP (OpenAPI 3.0)
├── checklists/requirements.md     # De /speckit-specify
└── tasks.md                       # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Liquidaciones/                           # NUEVO
│   │   │   ├── ArmadoLiquidacionEndpoints.cs        #   transportistas, disponibles (literales)
│   │   │   ├── LiquidacionesEndpoints.cs            #   listado, detalle, generar, editar ({id:int})
│   │   │   ├── CicloDeVidaLiquidacionEndpoints.cs   #   anulación, órdenes de pago
│   │   │   └── RespuestasDeLiquidacion.cs           #   resultado → HTTP, en un solo lugar (research §8)
│   │   └── Program.cs                               # MODIFICADO — servicios, políticas, grupos
│   ├── GT.Application/
│   │   ├── Liquidaciones/                           # NUEVO — carpeta espejo del módulo
│   │   │   ├── ConsultarTransportistasLiquidables.cs
│   │   │   ├── ConsultarViajesDisponibles.cs
│   │   │   ├── ValidadorDeViajes.cs                 #   compartido por generar y editar (FR-011, FR-049)
│   │   │   ├── GenerarLiquidacion.cs
│   │   │   ├── ConsultarLiquidaciones.cs
│   │   │   ├── ConsultarDetalleLiquidacion.cs       #   también la relectura de las cuatro escrituras
│   │   │   ├── EditarLiquidacion.cs
│   │   │   ├── AnularLiquidacion.cs
│   │   │   ├── RegistrarOrdenDePago.cs
│   │   │   ├── IRepositorioLiquidaciones.cs
│   │   │   ├── ResultadoLiquidacion.cs
│   │   │   ├── Dtos.cs
│   │   │   ├── NombresDeEstadoLiquidacion.cs
│   │   │   └── Mensajes.cs                          #   textos en es-AR y códigos de error
│   │   └── Autenticacion/CatalogoOpcionesMenu.cs    # MODIFICADO — dos entradas
│   ├── GT.Domain/
│   │   ├── Liquidaciones/                           # NUEVO
│   │   │   ├── Liquidacion.cs
│   │   │   ├── LiquidacionViaje.cs
│   │   │   ├── OrdenDePago.cs
│   │   │   ├── CambioDeLiquidacion.cs
│   │   │   ├── CambioDeLiquidacionViaje.cs          #   qué viajes quitó y agregó cada edición
│   │   │   ├── EstadoLiquidacion.cs                 #   3 valores; su orden sostiene un CHECK
│   │   │   ├── OperacionDeLiquidacion.cs            #   3 valores; su orden sostiene un índice
│   │   │   ├── ReglasDeLiquidacion.cs               #   reglas puras, fechas por parámetro
│   │   │   └── NumerosVisibles.cs                   #   LQ-{n}, OP-{n}
│   │   └── Usuarios/Rol.cs                          # MODIFICADO — dos códigos de permiso
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── Configuraciones/                     # NUEVO — 5 configuraciones
│       │   ├── RepositorioLiquidaciones.cs          # NUEVO — consultas, 4 transacciones, traducción de índices
│       │   ├── GtDbContext.cs                       # MODIFICADO — 4 DbSet + 2 secuencias
│       │   └── Migraciones/                         # NUEVO — Modulo9Liquidaciones
│       └── DatosIniciales/SembradorInicial.cs       # MODIFICADO — dos permisos y su reparto
└── tests/
    ├── GT.UnitTests/Liquidaciones/                  # NUEVO — ReglasDeLiquidacion, NumerosVisibles
    └── GT.IntegrationTests/Liquidaciones/           # NUEVO — generación, disponibles, externos,
                                                     #   carreras, edición, anulación, pagos,
                                                     #   coherencia, restricciones, listado,
                                                     #   rutas, permisos

frontend/
└── src/
    ├── modules/liquidaciones/                       # NUEVO
    │   ├── paginas/                                 #   ListadoLiquidaciones, GenerarLiquidacion,
    │   │                                            #   DetalleLiquidacion, EditarLiquidacion (+ tests)
    │   ├── componentes/                             #   FiltrosLiquidaciones, TablaDeViajes,
    │   │                                            #   DialogoOrdenDePago, DialogoAnulacion (+ tests)
    │   └── servicios/servicioLiquidaciones.ts
    ├── modules/autenticacion/servicios/sesion.ts    # MODIFICADO — dos constantes de permiso
    ├── modules/choferes/transportistas/
    │   └── ListadoTransportistas.tsx                # MODIFICADO (Módulo 3) — usa compartido/cuit
    ├── modules/viajes/clientes/ListadoClientes.tsx  # MODIFICADO (Módulo 5) — usa compartido/cuit
    ├── compartido/cuit.ts                           # NUEVO — formatearCuit (research §11b)
    ├── compartido/cuit.test.ts                      # NUEVO
    ├── compartido/seccionesDeMenu.ts                # MODIFICADO — dos códigos en `Operación`
    └── App.tsx                                      # MODIFICADO — cuatro rutas
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Liquidaciones/` como carpeta espejo del módulo, alineada con
`specs/009-gestion-liquidacion/` y con `frontend/src/modules/liquidaciones/` —el nombre de módulo que la
constitución ya enumera—.

`TablaDeViajes` es componente **del módulo** y no de `compartido/ui`: la usan las tres pantallas que
muestran viajes de una liquidación —generar, detalle, editar— con la misma columna de importe y el mismo
pie de total, y ningún otro módulo la necesita.

**Lo que llama la atención de esta lista**: la columna de MODIFICADOS tiene **once archivos. Nueve son
puntos de extensión y dos son pantallas de otros módulos**, las de *Transportistas* y *Clientes*, con un
cambio que borra una función y agrega una importación. Ninguno es un archivo de negocio del backend de
otro módulo: es la diferencia con el Módulo 6, que tenía cuatro archivos del Módulo 5 en esa columna y
cambiaba el comportamiento de uno.

## Complexity Tracking

Cuatro piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Una tabla `LiquidacionViajes`** en vez de una columna en `Viajes`, que es el precedente del Módulo 6 | La spec prohíbe modificar el Módulo 5, y FR-028 exige que la anulada siga mostrando sus viajes | `Viajes.LiquidacionId` + `UPDATE` condicional. Descartada por los dos motivos: es una columna nueva en una tabla ajena y se vacía al anular (research §1) |
| **`Vigente` repite el estado de la liquidación** | Un índice único filtrado sólo mira columnas de su tabla, y es lo que pone la exclusividad del viaje en la base | Dos tablas —vigentes y copia de anuladas— sin dato repetido. Descartada: cada anulación mueve filas entre tablas y el detalle lee de dos lugares. `CoherenciaDeLiquidacionTests` verifica la columna contra el estado (research §1) |
| **`ImporteTotal` e `ImportePagado` guardados**, siendo sumas | Son la condición de los `UPDATE` que cierran tres carreras, y el soporte del `CHECK` de FR-043 | Derivarlos y bloquear con `UPDLOCK` a mano, o usar `rowversion`, o `Serializable`. Descartadas en research §2: SQL fuera de EF, un caso sin detección, o bloqueos por rango. Un test compara cada agregado con su suma |
| **El estado guardado, atado por un `CHECK`** | FR-029 exige filtrar por estado con valores excluyentes; derivar `pagada` obligaría a escribir la regla en C# y en SQL | Derivarlo al leer, como `vencida` en el Módulo 6. Descartada: el `CHECK` hace imposible la discrepancia que la derivación evitaba, sin duplicar el filtro (research §3) |

Las cuatro se resuelven con lo que ya viene en EF Core y SQL Server —índices filtrados, `CHECK`,
`ExecuteUpdateAsync`, secuencias—, sin bibliotecas, servicios externos ni infraestructura propia.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una línea
por decisión, con referencia a la spec (`[009] ...`), en la sección *Decisiones transversales ya
tomadas*. No incluir entradas por incluir: sólo las que sean **información transversal y relevante para
el proyecto** que futuras features puedan aprovechar.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[009]` Cuando una agrupación tiene que **recordar lo que agrupaba después de liberarlo** —una
  anulación que devuelve los elementos—, el vínculo va en **tabla propia con marca de vigencia** e índice
  único filtrado sobre la vigencia. Una columna escalar en la entidad agrupada pierde el pasado al
  liberar, y además obliga a modificar el módulo dueño de esa entidad.
- `[009]` Cuando **varias carreras recaen sobre la misma fila** —pagar, editar y anular una misma
  liquidación—, cada operación empieza con un **`UPDATE` condicional sobre esa fila** y verifica una fila
  afectada. Eso obliga a guardar los agregados que la condición mira; un `CHECK` y un test que los
  compara con su suma son lo que impide que discrepen.
- `[009]` Un **estado que podría derivarse se puede guardar** si un `CHECK` lo ata a las columnas de las
  que depende: la discrepancia que la derivación evitaba se vuelve imposible, y el filtro opera sobre la
  columna sin escribir la regla dos veces.
- `[009]` Una **confirmación previa que tiene que mostrar un número que sólo el servidor sabe** —el saldo
  después de un pago— hace el viaje de ida y vuelta con `409`. La que no muestra nada del servidor viaja
  confirmada desde el diálogo que ya la pidió. En los dos casos, sin `confirmado` el backend no ejecuta.
- `[009]` Un **formato de presentación copiado localmente** —el CUIT con guiones— se lleva a
  `compartido` **cuando aparece la tercera necesidad**, y las copias anteriores se reemplazan en la misma
  feature. El refactor conserva también el caso borde de las copias, y lo verifican las suites de las
  pantallas tocadas sin modificarse: si hubiera que cambiarlas, no era un refactor.
- `[009]` El **formato visible de un identificador** que también aparece en mensajes del servidor
  —`LQ-12`— lo arma el backend una sola vez y viaja armado en el JSON: escribirlo en C# y en TypeScript
  son dos formatos que se pueden separar.
