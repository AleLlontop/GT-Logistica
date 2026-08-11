# Implementation Plan: Gestión de viajes (Módulo 5)

**Branch**: `005-gestion-viajes` | **Date**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/005-gestion-viajes/spec.md`

## Summary

El Módulo 5 registra **la unidad de trabajo de la empresa**: un cliente pide llevar una carga de un
origen a un destino, y G&T Logística asigna un chofer y un vehículo. Hoy eso vive en planillas y
remitos en papel, se pierde el rastro de qué viajes están en curso y cuáles se rindieron, y salen
unidades a la ruta sin documentación en regla porque nadie lo verifica al momento de asignar.

El módulo trae el padrón de clientes, el registro del viaje con su remito, carga e importe, el control
de habilitación al asignar, el ciclo de vida `pendiente → en curso → rendido` con historial de quién y
cuándo, el listado con filtros y búsqueda, y los totales por cliente y por transportista en un período.

Es el primer módulo con **cuatro cosas que el sistema no tenía**: una entidad con **ciclo de vida y
transiciones cerradas**, un **historial de quién hizo qué**, **recursos compartidos que se ocupan y se
liberan** —un chofer no puede estar en dos viajes a la vez— y **dinero**.

**Enfoque técnico**, con cinco decisiones que definen el módulo:

1. **Los Módulos 3 y 4 se consumen sin tocarles una línea.** Y no por disciplina: los calculadores de
   documentación ya reciben la fecha de referencia como parámetro, nunca leen el reloj. Pasarles
   `viaje.Fecha` en lugar de `FechaHoyArgentina.Hoy()` da exactamente la semántica que pide la spec
   —todo se evalúa contra la fecha del viaje, para que una carga retroactiva pueda decir la verdad—
   sin escribir una sola regla de nuevo (research §3, SC-014).
2. **Tres garantías van a la base como índices únicos filtrados**, no al código: el número de viaje, el
   remito único entre los no anulados, y un chofer y un vehículo en un solo viaje `en curso` a la vez.
   La consulta previa da el mensaje bueno; el índice cierra la carrera entre dos operadores
   simultáneos. Es lo que hace que el 0% de SC-005 sea una garantía y no una intención (research §2).
3. **Asignar y cambiar de estado son recursos propios**, nunca campos del `PUT`. Es el precedente
   [004] extendido a la asignación con el mismo argumento —corregir un destino no puede tocar quién
   maneja— más uno propio: la asignación es la única operación que devuelve bloqueos y advertencias por
   documentación, y sacarla del guardado de datos deja las dos respuestas limpias (research §4).
4. **Las advertencias tienen dos formas, y el criterio es la reversibilidad, no la gravedad.** Origen
   igual a destino y documento próximo a vencer llegan **con** el resultado, porque se corrigen
   editando. Rendir con importe en cero **no se ejecuta al primer intento**: responde `409` sin cambiar
   nada y rinde recién al confirmar, porque después el viaje es inmutable para siempre (research §5).
5. **Un viaje `rendido` es inmutable para todos los roles**, incluido el Administrador del sistema. No
   hay camino de corrección en esta versión, y eso simplifica el módulo entero: cinco caminos de
   escritura consultan el estado antes de tocar nada, y no hay que diseñar ninguna auditoría de
   corrección que nadie pidió.

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10 sobre
SQL Server, MailKit, React 19 + React Router + Vite). **Ninguna dependencia nueva, ninguna
infraestructura nueva y ninguna variable de entorno nueva**: la paginación, la autorización por
permiso, el menú resuelto por el servidor, los calculadores de documentación y `TimeProvider` ya están
y se consumen tal como están (research §3, §7)

**Storage**: SQL Server 2022. Una migración nueva (`Modulo5Viajes`) crea la secuencia
`dbo.NumeroDeViaje` y las tablas `Clientes`, `Viajes` y `CambiosDeEstadoViaje`. **No modifica ninguna
tabla existente**: es el primer módulo que se apoya sobre dos módulos de negocio anteriores sin
agregarles una columna ni una navegación. Los dos permisos nuevos los siembra `SembradorInicial`, que
ya corre en cada arranque y es idempotente

**Testing**: xUnit en `GT.UnitTests` (reglas puras: transiciones permitidas, veredicto de habilitación
a una fecha dada, umbral de demora, validación de importe) y `GT.IntegrationTests`
(`WebApplicationFactory` contra el SQL Server del compose: los tres índices únicos filtrados incluida
la carrera entre dos operadores, la revalidación por cambio de fecha que no debe dejar rastro, los
cinco caminos de escritura sobre un viaje rendido, y la coincidencia entre el total y la suma del
listado); Vitest + React Testing Library en el frontend

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: los listados responden en menos de 1 segundo (p95) con el volumen real. El
filtro por estado, la exclusión de anulados, la señal de `demorado` y las dos agregaciones de totales
se resuelven **dentro de la consulta SQL**. La búsqueda por texto usa colación explícita y no índice, y
eso está aceptado y anotado: con `LIKE '%texto%'` tampoco lo usaría (research §8). El listado pagina de
a 20 filas con el total, filtrando antes de paginar (FR-043)

**Constraints**: ninguna operación de este módulo puede dejar guardado un viaje cuya asignación esté
bloqueada a su propia fecha, se haya llegado ahí asignando, reasignando o moviendo la fecha (FR-022a,
SC-004) —lo que pase después en los Módulos 3 y 4 con esa documentación queda fuera de lo que el
módulo controla, y está declarado—; un chofer y un vehículo figuran en un solo viaje `en curso` por
los **dos** caminos que llevan ahí —poner en curso y reasignar un viaje ya en curso— incluso con dos
operadores simultáneos, garantizado con índice único y no sólo con la pantalla (FR-026, FR-026a,
SC-005); ningún paso irreversible se ejecuta sin confirmación
previa (SC-007a); un viaje `rendido` no se modifica por ningún camino ni para ningún rol (FR-018,
SC-013); el historial no es editable ni borrable (FR-035); el número de viaje no se reutiliza nunca
(FR-011); los importes son `decimal`, nunca punto flotante (research §11)

**Scale/Scope**: una única empresa — decenas de viajes por semana, cientos de clientes. En este módulo:
**7 pantallas nuevas, 16 endpoints nuevos, 0 endpoints modificados, 3 tablas nuevas, 1 secuencia
nueva y 0 tablas modificadas**, que cubren **59 requisitos funcionales** (FR-001 a FR-053, más
FR-015a, FR-019a, FR-019b, FR-022a, FR-026a y FR-046a) y **15 criterios de éxito**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | Cero dependencias nuevas, cero infraestructura nueva, cero variables de entorno nuevas. La evaluación de documentación **reutiliza los calculadores existentes pasándoles otra fecha**, en lugar de reimplementarla (research §3). La exclusividad se resuelve con dos índices filtrados en vez de una tabla de ocupaciones que habría que mantener sincronizada (research §2). La identidad del usuario llega **por parámetro**, sin agregar una abstracción `IUsuarioActual` que hoy tendría cuatro llamadores (research §7). Y `demorado` sale del historial que ya hay que llevar, no de una columna nueva (research §6) |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes en español rioplatense con voseo, definidos textualmente en `contracts/README.md`. **Es el primer módulo con dinero**: los importes van en pesos argentinos con `$ 1.240.000,00` —punto de miles, coma decimal— desde un formateador compartido nuevo, `compartido/moneda.ts`, por el mismo motivo por el que existe `compartido/fechas.ts` (research §11). Las fechas siguen formateándose con `date-fns`, nunca con `toLocaleDateString` |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 59 requisitos y nada más. Quedan explícitamente afuera, tal como fija la spec: facturación —incluida cualquier referencia del viaje a una factura—, liquidación al transportista, tarifas automáticas, cotizaciones, GPS, hojas de ruta con paradas, combustible y gastos, notificaciones, portal de cliente, app de chofer, digitalización del remito, y el ABM de choferes y vehículos. **Tres tentaciones concretas se anotaron y no se construyen**: la corrección auditada de un viaje rendido (FR-018 la deja afuera y la spec la anota para una spec futura), el catálogo de localidades para normalizar origen y destino, y la exportación de los totales a archivo. Ninguna se hace acá |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 7 historias se validan operando la app, con el recorrido de `quickstart.md`. **Dos requisitos no se pueden verificar a mano y se declara por qué**: la concurrencia de SC-005 —dos operadores en el mismo milisegundo— y el viaje demorado de FR-039, que a mano exige esperar cinco días. Los dos quedan en tests que fijan el instante, y el quickstart lo dice en el paso 22 en vez de pedirle a quien valida algo que no puede hacer |
| V. Datos del Usuario con Respeto | ✅ Pasa | Del cliente se piden sólo los datos que la spec enumera, y la dirección es opcional porque el módulo no la usa para operar. **El historial guarda quién hizo cada cambio de estado y cuándo, y nada más**: no registra qué datos se editaron ni desde dónde, porque FR-035 no lo pide y sería recolectar por las dudas. Ningún dato del módulo se borra físicamente, y ningún secreto entra al código |

**Sobre el Principio I y las cuatro cosas nuevas**: este módulo agrega ciclo de vida, historial,
exclusividad y dinero, y ninguna de las cuatro trae maquinaria propia. El ciclo de vida es un `switch`
sobre transiciones permitidas; el historial es una tabla de tres columnas útiles; la exclusividad son
dos índices; el dinero es un `decimal` y un formateador de nueve líneas. No hay máquina de estados
configurable, ni patrón de eventos, ni auditoría genérica. La constitución pide no generalizar antes
de tener un segundo caso de uso real, y acá no hay ninguno.

**Sobre el Principio III y los Módulos 3 y 4**: la spec declara que este módulo **no modifica nada** de
ellos. Se cumple al pie de la letra: la migración no toca una tabla existente y no hay un solo archivo
del Módulo 3 o del 4 en la lista de modificados. La única tentación real —hacer que la baja de un
chofer con viajes asociados se rechace— la spec la descarta explícitamente y la anota como spec aparte.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los cinco principios se sostienen. Cinco cosas que el diseño confirmó o
descubrió, y que conviene tener a la vista al implementar:

- **Que un chofer sin documentos no bloquee la asignación contradice al Módulo 4, y está bien.** Allá,
  una unidad sin documentación no puede quedar `disponible` (FR-013 del 004); acá, un chofer sin
  documentos se asigna igual (FR-024). No es una inconsistencia: son dos preguntas distintas. El
  Módulo 4 pregunta si la unidad está en condiciones y no lo sabe; el Módulo 5 pregunta si hay algo
  cargado que **prohíba** este viaje, y no lo hay. Además, la lista de asignables ya filtró por el
  estado operativo guardado, así que el Módulo 4 ya dijo lo suyo antes (research §3).
- **La revalidación por cambio de fecha (FR-022a) no es una validación más del `PUT`: es lo que hace
  que SC-004 sea cierto.** Sin ella hay dos reglas —una para el alta y otra para la edición— y un
  agujero entre las dos. Con ella hay una sola: nunca queda guardado un viaje cuya asignación esté
  bloqueada a su propia fecha, por ningún camino. El rechazo tiene que abortar el `PUT` **entero**, no
  sólo el campo fecha, y eso hay que verificarlo con un test.
- **La confirmación de FR-038 vive en el backend, a diferencia de todas las confirmaciones anteriores
  del sistema.** Hasta acá la confirmación la pedía la pantalla y el endpoint ejecutaba —dar de baja un
  vehículo, un tipo, un cliente—, porque todas esas se deshacen. Rendir con importe en cero no se
  deshace, así que el primer intento responde `409` sin cambiar nada. El criterio no es la gravedad del
  aviso: es si el paso se puede deshacer.
- **El orden del listado es el primero del sistema que no termina en `Id`.** Termina en `Numero`, que
  tiene índice único propio y es además el que ve el usuario. La convención [003] pide un orden
  **total**, no un orden que termine en `Id`; acá se cumple con el número, y ordenar además por `Id`
  sería ruido (research §12).
- **La revisión de calidad de requisitos cambió seis cosas, y una de ellas era un requisito que se
  contradecía con su propia historia de usuario.** FR-006 rechazaba la baja de un cliente con
  cualquier viaje no anulado —incluidos los rendidos—, así que el único cliente dado de baja posible
  era el que nunca había operado, mientras US1 justifica la baja con "el que dejó de operar con la
  empresa". Ahora mira sólo `pendiente` y `en curso`, que es el mismo criterio de "dependientes vivos"
  del Módulo 3. Las otras cinco están en `spec.md` §Clarifications y en
  `checklists/ciclo-de-vida-e-integracion.md`. Vale como precedente: **el chequeo de calidad de la
  spec encontró un conflicto que ningún test hubiera visto**, porque un test verifica contra la spec.
- **Los tres índices filtrados llevan el valor numérico de `EstadoViaje` escrito a mano.** Es el único
  lugar del módulo donde un cambio inocente —reordenar un enum— rompería una garantía sin fallar al
  compilar. Va un test de integración que inserta un viaje en cada estado y verifica que cada índice
  acepta y rechaza donde corresponde, y un comentario en el enum.

## Project Structure

### Documentation (this feature)

```text
specs/005-gestion-viajes/
├── plan.md              # Este archivo
├── research.md          # Decisiones técnicas y alternativas descartadas
├── data-model.md        # Tablas, campos, reglas derivadas y migración
├── quickstart.md        # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md        # Contrato de UI: pantallas, mensajes y textos
│   └── viajes-api.yaml  # Contrato HTTP (OpenAPI 3.0)
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**. Nada de los Módulos 3 y 4
aparece acá, y no es casualidad: la spec lo prohíbe y el diseño lo respetó.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Viajes/                              # NUEVO
│   │   │   ├── ClientesEndpoints.cs             #   ABM del padrón + alta como recurso propio
│   │   │   ├── ViajesEndpoints.cs               #   listado, ficha, alta, edición
│   │   │   ├── AsignacionEndpoints.cs           #   asignables + asignación (FR-019a)
│   │   │   ├── CicloDeVidaEndpoints.cs          #   en-curso, rendición, anulación (FR-034)
│   │   │   └── TotalesEndpoints.cs              #   los dos cuadros del período
│   │   └── Program.cs                           # MODIFICADO — registra el grupo y los dos permisos
│   ├── GT.Application/
│   │   ├── Viajes/                              # NUEVO — carpeta espejo del módulo
│   │   │   ├── Clientes/                        #   ABM, baja con dependencias, alta idempotente
│   │   │   ├── CrearViaje.cs                    #   nace pendiente + primera fila del historial
│   │   │   ├── ConsultarViajes.cs               #   4 filtros + búsqueda + página de 20
│   │   │   ├── ConsultarFichaViaje.cs           #   incluye el historial completo
│   │   │   ├── ModificarViaje.cs                #   revalida la asignación por fecha (FR-022a)
│   │   │   ├── AsignarChoferYVehiculo.cs        #   bloqueo, advertencia y transportista (FR-028)
│   │   │   ├── ConsultarAsignables.cs
│   │   │   ├── PonerViajeEnCurso.cs             #   exige asignación y libera exclusividad
│   │   │   ├── RendirViaje.cs                   #   confirmación previa si el importe es cero
│   │   │   ├── AnularViaje.cs                   #   motivo obligatorio
│   │   │   ├── ConsultarTotales.cs              #   dos agregaciones, rango obligatorio
│   │   │   ├── IRepositorioViajes.cs
│   │   │   ├── IRepositorioClientes.cs
│   │   │   ├── Dtos.cs                          #   incluye el sobre { viaje, advertencias }
│   │   │   └── Mensajes.cs                      #   textos en es-AR y códigos de error
│   │   └── Autenticacion/CatalogoOpcionesMenu.cs # MODIFICADO — tres entradas del módulo
│   ├── GT.Domain/
│   │   ├── Viajes/                              # NUEVO
│   │   │   ├── Viaje.cs                         #   incluye DiasParaDemora = 5
│   │   │   ├── Cliente.cs
│   │   │   ├── CambioDeEstadoViaje.cs
│   │   │   ├── EstadoViaje.cs                   #   4 valores; su orden sostiene 3 índices
│   │   │   ├── TransicionesDeViaje.cs           #   regla pura: qué transición se permite (FR-033)
│   │   │   ├── HabilitacionAsignacion.cs        #   3 valores derivados
│   │   │   └── EvaluadorHabilitacion.cs         #   regla pura: veredicto a una fecha dada (FR-024)
│   │   └── Usuarios/Rol.cs                      # MODIFICADO — los dos códigos de permiso nuevos
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── Configuraciones/                 # NUEVO — 3 configuraciones
│       │   ├── RepositorioViajes.cs             # NUEVO — filtros, búsqueda y demora en SQL
│       │   ├── RepositorioClientes.cs           # NUEVO
│       │   ├── GtDbContext.cs                   # MODIFICADO — 3 DbSet
│       │   └── Migraciones/                     # NUEVO — Modulo5Viajes
│       └── DatosIniciales/SembradorInicial.cs   # MODIFICADO — dos permisos y su reparto por rol
└── tests/
    ├── GT.UnitTests/Viajes/                     # NUEVO — reglas puras
    └── GT.IntegrationTests/Viajes/              # NUEVO

frontend/
└── src/
    ├── compartido/moneda.ts                     # NUEVO — primer formateo de pesos del sistema
    ├── modules/viajes/                          # NUEVO
    │   ├── paginas/                             #   listado, ficha, formulario, asignación, totales
    │   ├── componentes/                         #   filtros, paginación, confirmaciones
    │   ├── servicios/
    │   └── clientes/                            #   listado y formulario del padrón
    └── App.tsx                                  # MODIFICADO — rutas del módulo
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Viajes/` como carpeta espejo del módulo de negocio, alineada 1 a 1 con
`specs/005-gestion-viajes/` y con `frontend/src/modules/viajes/`, tal como fija la constitución.

`Cliente` vive **dentro** del módulo `viajes` y no como módulo hermano: existe para sostener al viaje,
no tiene spec propia y comparte sus dos permisos (FR-053). Es el mismo criterio con el que
`Transportista` quedó dentro de `choferes` y `TipoVehiculo` dentro de `flota`.

`GT.Domain/Viajes/` es carpeta propia porque la capa de dominio se organiza por área de negocio,
siguiendo lo que ya hicieron los módulos anteriores.

**Lo que llama la atención de esta lista**: la columna de MODIFICADOS tiene **cuatro archivos**, y los
cuatro son puntos de extensión que los módulos anteriores ya tocaron por diseño —el registro de
servicios, el catálogo de menú, los códigos de permiso y el sembrador—. Ningún archivo de negocio de
los Módulos 3 y 4 se modifica.

## Complexity Tracking

Cuatro piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Una secuencia de base de datos para el número de viaje**, en vez de usar la identidad de la tabla | FR-011 pide que no se reutilice nunca y las *Assumptions* que avance de a uno. Una columna `IDENTITY` de SQL Server **salta de a 1000 tras un apagado sucio**, y en un entorno que se levanta y baja con `compose` eso pasa: el viaje siguiente al 12 sería el 1012 | Usar `Id` como número. Descartada en research §1 por el salto de identidad, que contradice el escenario CA5. `NO CACHE` en la secuencia lo elimina a cambio de una escritura de log por número, invisible a este volumen |
| **Tres índices únicos filtrados con el valor del enum escrito a mano** | Son la garantía real de FR-011, FR-014 y FR-026, y lo que hace que el 0% de SC-005 valga también cuando dos operadores actúan en el mismo milisegundo | Validar sólo con una consulta previa. Descartada porque deja una ventana de carrera que SC-005 prohíbe explícitamente. Una tabla de ocupaciones daría la misma garantía pero podría desincronizarse del estado del viaje; el índice filtrado **es** el mismo dato (research §2). El costo —los literales `1` y `3`— se cubre con un test de integración |
| **El sobre `{ viaje, advertencias }` en tres endpoints, y el recurso pelado en el resto** | FR-015a exige que las advertencias que no bloquean lleguen con el resultado, y sólo tres operaciones pueden advertir: alta, edición y asignación | Un campo `advertencias` dentro del propio viaje. Descartada en research §5: una advertencia no es un dato del viaje sino de **esta operación**, y guardada en el recurso reaparecería en cada consulta posterior de la ficha |
| **Un formateador de moneda compartido nuevo** | Es el primer módulo del sistema con dinero, y el Principio II fija el formato argentino exacto | Formatear en cada pantalla. Descartada por el mismo motivo por el que existe `compartido/fechas.ts`: la primera pantalla que lo escriba distinto va a ser la que nadie revise. Son nueve líneas sobre `Intl.NumberFormat` (research §11) |

Las cuatro se resuelven con lo que ya viene en el marco de trabajo y en el proyecto —EF Core, índices
filtrados de SQL Server, `TimeProvider`, `Intl`—, sin dependencias ni servicios externos.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una línea
por decisión, con referencia a la spec (`[005] ...`), en la sección *Decisiones transversales ya
tomadas*. Sólo entran las que son **transversales y relevantes para el proyecto** y que futuras
features pueden aprovechar; nada por completar la lista.

**Ya escrita durante el diseño**: la convención de anunciar con `role="status"` todo resultado que
aparece sin que la pantalla cambie. No es una decisión de esta feature —rige desde el Módulo 3— pero
los tres módulos anteriores la venían repitiendo como tarea sin que estuviera en ninguna spec, así
que se levantó a `AGENTS.md` como `[003]`. T134 no tiene que volver a agregarla.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[005]` Una **confirmación previa vive en el backend cuando el paso no se puede deshacer**, y en la
  pantalla cuando sí: el primer intento responde `409` sin cambiar nada y la operación se ejecuta recién
  con la confirmación explícita. El criterio es la reversibilidad, no la gravedad del aviso.
- `[005]` Una **exclusividad sobre un recurso compartido** —una unidad que no puede estar en dos
  trabajos a la vez— se escribe como **índice único filtrado sobre la tabla que ya tiene el estado**, no
  como tabla de ocupaciones aparte: la tabla aparte puede desincronizarse, el índice **es** el mismo
  dato. La consulta previa da el mensaje bueno y el índice cierra la carrera.
- `[005]` Una regla que se evalúa **a una fecha** se escribe recibiendo la fecha por parámetro y nunca
  leyendo el reloj por dentro. Es lo que permitió reusar entera la evaluación de documentación de los
  Módulos 3 y 4 contra la fecha del viaje, sin tocar una línea.
- `[005]` Un **estado derivado de un instante** —"demorado", "vencido hace tanto"— toma el instante del
  **hecho ya registrado** —el historial— y no de una columna que lo copie: la columna puede discrepar
  del hecho.
- `[005]` **`400` cuando el problema está en lo que se tipeó** —campos, duplicados, dependencias— y
  **`409` cuando está en el estado de algo que se comparte o que cambió** —unidad ocupada, transición no
  permitida, entidad inmutable, confirmación pendiente—. Con eso el frontend sabe, sin leer el código,
  si tiene que marcar un campo o abrir un diálogo.
- `[005]` Los **importes van en `decimal`**, nunca en punto flotante, y se formatean con
  `compartido/moneda`: un total que alguien va a comparar contra una planilla no puede acumular error de
  representación.
