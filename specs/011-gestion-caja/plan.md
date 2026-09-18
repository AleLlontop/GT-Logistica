# Implementation Plan: Gestión de caja (Módulo 11)

**Branch**: `011-gestion-caja` | **Date**: 2026-09-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/011-gestion-caja/spec.md`

## Summary

El Módulo 11 controla el efectivo diario de G&T Logística: el empleado administrativo abre una caja con
un saldo inicial y queda como responsable, registra ingresos y egresos del día —opcionalmente vinculados
a una factura pendiente de cobro (Módulo 6) o a una orden de pago (Módulo 9)—, y la cierra viendo un
resumen con el saldo final calculado. El gerente sólo consulta.

**No modifica nada del backend ni de la base de ningún módulo anterior**: lee facturas y órdenes de pago
sin agregarles una columna, un índice ni una navegación.

**Enfoque técnico**, con cuatro decisiones que definen el módulo (detalle y alternativas descartadas en
[research.md](./research.md)):

1. **RN1 (una caja abierta por empleado) es un índice único filtrado** sobre `Cajas`, igual que la
   exclusividad de una unidad del Módulo 5: la consulta previa da el mensaje, el índice cierra la carrera
   del doble clic (research §1).
2. **La misma fila de `Caja` sirve de candado para dos operaciones distintas.** Tanto cerrar como
   registrar un movimiento empiezan su transacción con un `UPDATE` que no cambia nada
   (`SET Estado = Estado WHERE Id = @id AND Estado = Abierta`), sólo para tomar el lock de la fila antes
   de leer o insertar. Es la convención [009] extendida: acá el candado no protege una segunda escritura
   sobre la misma fila, protege un `INSERT` en la tabla hija contra un cierre simultáneo (research §2).
3. **El cierre se confirma contra el número que muestra**, no contra un `confirmado: true` a secas: el
   cuerpo lleva `saldoFinalConfirmado`, y si no coincide con lo recién calculado, el servidor responde
   `409` con el resumen actualizado y no cierra nada (research §3, FR-018).
4. **La referencia del egreso a una orden de pago no lleva filtro de estado**: `OrdenDePago` del Módulo 9
   es hoy un registro de un pago ya hecho, sin ningún campo de "pendiente" — se decidió no inventarle uno.
   El desplegable ofrece las 50 más recientes y lo avisa debajo del campo (research §4, FR-011).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el frontend.
Sin cambios respecto de los módulos anteriores.

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10 sobre
SQL Server, React 19 + React Router + Vite + Tailwind + Radix, `date-fns`). **Ninguna dependencia nueva.
Ninguna variable de entorno nueva.**

**Storage**: SQL Server 2022. Una migración nueva, `Modulo11Caja`, crea **dos tablas** —`Cajas` y
`MovimientosDeCaja`—. **No modifica ninguna tabla existente**, no le agrega columnas a `Facturas` ni a
`OrdenesDePago`. Sin secuencias: la caja no tiene número visible (spec §Assumptions). Los dos permisos
nuevos los siembra `SembradorInicial`, idempotente en cada arranque.

**Testing**: xUnit en `GT.UnitTests` (`ReglasDeCaja`: saldo final, validación de importe y de saldo
inicial) y en `GT.IntegrationTests` con `WebApplicationFactory` contra el SQL Server del compose (apertura
y su carrera, movimiento sobre caja cerrada y su carrera con el cierre simultáneo, los dos `CHECK`, el
índice único filtrado, el listado con sus filtros y su mensaje de "sin movimientos", el desplegable de
facturas pendientes reevaluado al guardar, las rutas literales junto a `{id:int}`, los dos permisos);
Vitest + React Testing Library en el frontend, consultas por rol, etiqueta y texto (convención [007]).

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio actuales.
Sin cambios de `Dockerfile` ni de `docker-compose.yml`.

**Project Type**: aplicación web con backend y frontend separados.

**Performance Goals**: el listado de movimientos y el de cajas responden en menos de 1 segundo (p95) con
el volumen real. Filtros y conteo se resuelven dentro de la consulta SQL, antes de paginar (research §8).
El desplegable de facturas pendientes también va a SQL, a diferencia del de beneficiarios del Módulo 10,
porque las facturas no son un padrón chico (research §5).

**Constraints**: ningún empleado con dos cajas `abiertas` a la vez (FR-003, FR-004); ningún movimiento sin
caja abierta (FR-007); ningún importe ≤ 0 ni con más de dos decimales (FR-008); ningún concepto vacío,
tampoco tras recortar espacios, ni de más de 200 caracteres (FR-009); sólo el responsable registra
movimientos y cierra su caja (FR-035); la referencia sigue el tipo del movimiento y, si es una factura,
tiene que seguir `Pendiente` al momento de guardar (FR-011); nada se edita, se anula ni se borra (FR-014);
el cierre exige `saldoFinalConfirmado` igual al recién calculado (FR-018); una caja `cerrada` no admite
movimientos nuevos (FR-019); dos cierres simultáneos, exactamente uno gana (FR-021); los importes son
`decimal`, nunca punto flotante.

**Scale/Scope**: una única empresa, decenas de movimientos por caja y por día. En este módulo: **6
pantallas y 0 diálogos de confirmación en el frontend** (la confirmación del cierre vive en su propia
pantalla de resumen, no en un diálogo aparte — FR-018 la trata como un paso con datos propios, no como un
sí/no), **11 endpoints nuevos, 2 tablas nuevas, 0 tablas modificadas, 0 secuencias, 2 permisos nuevos y 0
dependencias nuevas**, que cubren **35 requisitos funcionales** (FR-001 a FR-035) y **9 criterios de
éxito**.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.1.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | **Cero dependencias, cero variables de entorno, cero cambios de infraestructura, cero secuencias.** Se evaluaron y descartaron: una columna de "caja abierta" en `Usuario`, una `Version` en `Cajas`, un criterio de "pendiente" inventado para `OrdenDePago`, y un endpoint aparte para el resumen de cierre (research §1 a §5). Las tres piezas que suman algo están en *Complexity Tracking* |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y los mensajes en español rioplatense, definidos textualmente en `contracts/README.md`. Importes con `compartido/moneda` —`$ 11.500,00`—, fechas con `compartido/fechas` —`dd/mm/aaaa`— |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 35 requisitos y nada más. Queda afuera, como fija la spec: edición, anulación o eliminación de movimientos; reapertura de cajas; arqueo físico; transferencias entre cajas; medios de pago diferenciados; roles adicionales; exportación; integración contable. **Una tentación se anotó y no se construye**: filtrar `OrdenDePago` por algún criterio de "pendiente" que el Módulo 9 no define (research §4) |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 5 historias se validan operando la app con `quickstart.md`: 19 pasos, cada uno con el criterio que verifica, incluida la carrera de cierre con dos pestañas (paso 12). **Lo que no se puede verificar a mano se declara**, con su test: las dos carreras concurrentes, los `CHECK`, la invocación directa (research §11 análogo, quickstart §"Lo que no se puede verificar a mano") |
| V. Datos del Usuario con Respeto | ✅ Pasa | El movimiento pide **cuatro datos**: tipo, importe, concepto y una referencia opcional. Ni CBU, ni comprobante, ni ningún dato que la spec no pida. De la factura o la orden de pago referenciada no se copia nada más que su identificador: el nombre visible se arma con datos ya persistidos en su módulo de origen, no se congela ninguna copia (a diferencia del Módulo 6, que sí congela datos del emisor porque su documento tiene que sobrevivir a un cambio de domicilio — acá la referencia es sólo informativa, FR-012, y no hay documento que la use) |
| VI. Interfaz Gobernada por el Sistema de Diseño | ✅ Pasa | Las seis pantallas declaran su acción primaria —una o ninguna— y los estados de cada campo en *UI Design Check*. Se leyeron `tokens.md`, `componentes.md` y `contenido.md`. Se reusan las primitivas de `compartido/ui`; **no se agrega ningún componente compartido ni ningún token nuevo** |

**Sobre el Principio III y los módulos anteriores**: este módulo **no modifica ningún archivo** de los
Módulos 6 o 9 — a diferencia del Módulo 10, que tocó tres pantallas para llevar un formato a
`compartido`, acá no hay ninguna copia que ya haya llegado a su tercera necesidad. Los únicos archivos
modificados son puntos de extensión: `Rol.cs`, `SembradorInicial.cs`, `CatalogoOpcionesMenu.cs`,
`Program.cs`, `GtDbContext.cs`, `sesion.ts`, `seccionesDeMenu.ts`, `App.tsx` y `Estado.tsx` (los dos
valores de la pastilla, como hizo el Módulo 10).

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los seis principios se sostienen. Tres cosas que el diseño confirmó o
descubrió:

- **El candado de una carrera no siempre protege una segunda escritura sobre la misma fila.** El Módulo 9
  usó el `UPDATE` condicional para dos escrituras que competían entre sí (dos pagos, un pago y una
  anulación). Acá la competencia es entre una escritura sobre `Cajas` (el cierre) y un `INSERT` en
  `MovimientosDeCaja` que depende de leer el estado de `Cajas` sin que cambie mientras tanto. Un `UPDATE`
  que no cambia nada, sólo para tomar el lock, resuelve la misma clase de problema sin agregar ninguna
  columna (research §2).
- **No toda confirmación de FR-018 tipo "409 y mostrame el número" necesita que el usuario haya elegido
  un valor.** El Módulo 9 confirma un importe que el usuario tipeó contra un tope calculado. Acá no hay
  ningún valor que el usuario elija: todo el resumen lo calcula el servidor, así que lo que se confirma es
  el resumen mismo, con su propio número de vuelta en el cuerpo de la confirmación (research §3).
- **Una entidad de otro módulo puede no tener el estado que la spec da por sentado.** `OrdenDePago` no
  tiene ningún campo de "pendiente" en el Módulo 9 actual. Inventarle uno sería alcance fantasma sobre un
  módulo ajeno; la decisión correcta, confirmada con el negocio, fue tratarla tal cual existe hoy
  (research §4).

## UI Design Check

*GATE (Principio VI): obligatorio porque la feature toca UI. Re-evaluado después de Phase 1.*

Toda interfaz sigue `.claude/skills/gt-ui/SKILL.md`. Para esta sección se leyeron `references/tokens.md`,
`references/componentes.md` y `references/contenido.md`, más los patrones `viajes-listado.png` (listados),
`nuevo-chofer.png` (formularios) y `viaje-detalle.png` (resumen/ficha). Los textos exactos están en
`contracts/README.md`.

### Acción primaria por pantalla

| Pantalla | Acción primaria (una) | Secundarias / terciarias | Destructiva |
|---|---|---|---|
| Consulta de cajas — con `gestionar` | `Abrir caja` (sólo si no tiene una abierta) | — | — |
| Consulta de cajas — sólo `consultar` | `ninguna` | — | — |
| Abrir caja | `Abrir caja` | `Cancelar` | — |
| Detalle de caja — abierta y propia (resumen en vivo) | `Registrar movimiento` | `Cerrar caja` (secundaria: compite con seguir operando, no es la acción esperada por defecto) | — |
| Detalle de caja — cerrada o ajena (sólo lectura) | `ninguna` | `Volver a cajas` | — |
| Registrar movimiento | `Guardar movimiento` | `Cancelar` | — |
| Resumen de cierre | `Confirmar cierre` | `Cancelar` (vuelve a la caja abierta sin cambios) | — |
| Consulta de movimientos | `ninguna` (pantalla de sólo lectura) | — | — |
| Cualquiera de las seis, sin su permiso | `ninguna`: sólo el título y el aviso | — | — |

**`Cerrar caja` no es destructiva ni primaria en la caja abierta**: es la acción esperada al final del
día, pero mientras la caja sigue abierta, *Registrar movimiento* es la operación del día a día y la que
se repite; cerrar es un paso que ocurre una vez. En la propia pantalla de resumen de cierre, ahí sí
*Confirmar cierre* es la primaria: es la única decisión de esa pantalla.

### Estados de campo

El error aparece recién después de que el campo perdió el foco habiendo sido tocado, **o** al intentar la
acción que lo necesita —*Abrir caja*, *Guardar movimiento*, *Confirmar cierre*—; nunca en el primer
render. El foco es `border-brand` + `bg-white` + `shadow-focus` (anillo de 3 px), visible por sí solo. El
error es `border-danger` + `bg-danger-bg` + mensaje en `text-danger-text` con ícono, a través de `Campo`.
Los obligatorios se marcan con contenido generado desde `required`, no con un `<span>` en el `<label>`
(convención [008]).

| Pantalla | Campo | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Abrir caja | Saldo inicial | `InputImporte`, `corto`, alineado a la derecha | anillo `shadow-focus` | `Escribí el saldo inicial.` · `El saldo inicial no puede ser negativo.` | `0,00` |
| Registrar movimiento | Tipo | dos botones tipo *toggle* (`Ingreso`/`Egreso`), ninguno preseleccionado | anillo | `Elegí si es un ingreso o un egreso.` | ninguno marcado |
| Registrar movimiento | Importe | `InputImporte`, `corto`, alineado a la derecha | anillo | `Escribí un importe mayor que cero.` · `Escribí el importe con hasta dos decimales.` | `0,00` |
| Registrar movimiento | Concepto | texto `largo`, hasta 200 | anillo | `Escribí el concepto del movimiento.` | `Cobro flete a Cliente SA` |
| Registrar movimiento | Referencia (factura/orden) | desplegable `largo`, sólo habilitado tras elegir tipo; opciones según tipo elegido; con *Egreso*, debajo: `Se muestran las 50 órdenes de pago más recientes.` | anillo | `La factura elegida ya no está pendiente de cobro.` (sólo al guardar, no al elegir) | `Sin referencia` |
| Resumen de cierre | — | **sin campos de entrada**: el resumen es de sólo lectura, *Confirmar cierre* ejecuta sin diálogo aparte | — | — | — |
| Consulta de movimientos | Desde / Hasta (filtro) | fecha compacta; con valor, borde más marcado | anillo | *Desde* posterior a *Hasta*: los dos en error, `La fecha desde es posterior a la hasta.` | vacío: sin límite |
| Consulta de movimientos | Caja (filtro) | desplegable compacto | anillo | no aplica | `Todas las cajas` |

**Deshabilitados**: sólo los botones mientras esperan al servidor, con `disabled:opacity-50` y
`cursor-not-allowed`. Ningún botón se deshabilita para indicar un campo vacío: apretarlo marca el campo.

## Project Structure

### Documentation (this feature)

```text
specs/011-gestion-caja/
├── plan.md                        # Este archivo
├── research.md                    # Decisiones técnicas y alternativas descartadas
├── data-model.md                  # Tablas, restricciones, reglas, consultas y transacciones
├── quickstart.md                  # Cómo levantar y validar el módulo (19 pasos)
├── contracts/README.md            # Contrato de UI: pantallas, endpoints y textos
├── checklists/requirements.md     # De /speckit-specify
└── tasks.md                       # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega**. No modifica ningún archivo de negocio
de otro módulo (research, §Constitution Check).

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Caja/                                     # NUEVO
│   │   │   ├── CajaEndpoints.cs                      #   abrir, listado, detalle, facturas-pendientes,
│   │   │   │                                         #   ordenes-de-pago ({id:int} restringido)
│   │   │   ├── MovimientosDeCajaEndpoints.cs         #   registrar, listado por caja, listado global
│   │   │   ├── CierreDeCajaEndpoints.cs               #   resumen (GET) y confirmación (POST)
│   │   │   └── RespuestasDeCaja.cs                    #   resultado → HTTP, en un solo lugar
│   │   └── Program.cs                                 # MODIFICADO — servicios, políticas, grupos
│   ├── GT.Application/
│   │   ├── Caja/                                      # NUEVO — carpeta espejo del módulo
│   │   │   ├── AbrirCaja.cs
│   │   │   ├── RegistrarMovimiento.cs
│   │   │   ├── ConsultarCajas.cs
│   │   │   ├── ConsultarDetalleCaja.cs
│   │   │   ├── ConsultarMovimientos.cs
│   │   │   ├── ConsultarResumenDeCierre.cs
│   │   │   ├── CerrarCaja.cs
│   │   │   ├── ConsultarFacturasPendientes.cs
│   │   │   ├── ConsultarOrdenesDePago.cs
│   │   │   ├── IRepositorioCaja.cs
│   │   │   ├── ResultadoCaja.cs
│   │   │   ├── Dtos.cs                                #   incluye PaginaDe<T> reusado (convención [003])
│   │   │   ├── NombresDeEstadoCaja.cs
│   │   │   └── Mensajes.cs                            #   textos en es-AR y códigos de error
│   │   └── Autenticacion/CatalogoOpcionesMenu.cs      # MODIFICADO — dos entradas
│   ├── GT.Domain/
│   │   ├── Caja/                                      # NUEVO
│   │   │   ├── Caja.cs
│   │   │   ├── MovimientoDeCaja.cs
│   │   │   ├── EstadoCaja.cs                          #   2 valores; su orden sostiene un CHECK
│   │   │   ├── TipoMovimientoCaja.cs                  #   2 valores; su orden sostiene un CHECK
│   │   │   └── ReglasDeCaja.cs                        #   saldo final, importe válido, saldo inicial válido
│   │   └── Usuarios/Rol.cs                             # MODIFICADO — dos códigos de permiso
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── Configuraciones/                        # NUEVO — CajaConfiguracion, MovimientoDeCajaConfiguracion
│       │   ├── RepositorioCaja.cs                       # NUEVO — proyecciones, listados, 3 transacciones
│       │   ├── GtDbContext.cs                           # MODIFICADO — 2 DbSet
│       │   └── Migraciones/                             # NUEVO — Modulo11Caja
│       └── DatosIniciales/SembradorInicial.cs           # MODIFICADO — dos permisos y su reparto
└── tests/
    ├── GT.UnitTests/Caja/                               # NUEVO — ReglasDeCaja
    ├── GT.IntegrationTests/Caja/                        # NUEVO — apertura y su carrera, movimiento y su
    │                                                     #   carrera con el cierre, resumen, cierre y su
    │                                                     #   carrera, referencias, restricciones, rutas,
    │                                                     #   permisos
    └── GT.IntegrationTests/Usuarios/AsignarRolesTests.cs  # MODIFICADO (Módulo 2) — Gerencia suma caja.consultar

frontend/
└── src/
    ├── modules/caja/                                    # NUEVO
    │   ├── paginas/                                     #   ConsultaDeCajas, AbrirCaja, DetalleDeCaja,
    │   │                                                 #   RegistrarMovimiento, ResumenDeCierre,
    │   │                                                 #   ConsultaDeMovimientos (+ tests)
    │   ├── componentes/                                  #   FiltrosDeMovimientos (+ tests)
    │   └── servicios/servicioCaja.ts
    ├── modules/autenticacion/servicios/sesion.ts          # MODIFICADO — dos constantes de permiso
    ├── compartido/seccionesDeMenu.ts                      # MODIFICADO — dos códigos en `Operación`
    ├── compartido/ui/Estado.tsx                           # MODIFICADO — `abierta` y `cerrada` en TONO_POR_VALOR
    └── App.tsx                                            # MODIFICADO — seis rutas
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Caja/` como carpeta espejo del módulo, alineada con `specs/011-gestion-caja/` y con
`frontend/src/modules/caja/`. El nombre es singular —`Caja`, no `Cajas`— porque el módulo tiene dos
entidades de primer nivel y ninguna domina como para pluralizar la carpeta; sigue el precedente de
`Facturacion`, que nombra el proceso y no una entidad (research §10).

**Lo que llama la atención de esta lista**: sólo **nueve archivos modificados**, todos puntos de
extensión de infraestructura compartida (permisos, menú, DI, `DbContext`, sembrador, rutas, sesión,
secciones de menú, pastilla de estado) y un test de asignación de roles del Módulo 2. **Cero archivos de negocio de otro
módulo**, a diferencia del Módulo 10, que tocó tres pantallas de los Módulos 6 y 9.

## Complexity Tracking

Dos piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Un `UPDATE` que no cambia nada, sólo para tomar un lock** (`SET Estado = Estado WHERE ...`), en vez de una columna `Version` | Cerrar una caja y registrar un movimiento compiten por el mismo estado sin que ninguna de las dos sea una "edición" en el sentido del Módulo 9; el lock de fila sin `Version` alcanza (research §2) | Una columna `Version` en `Cajas`, como el Módulo 9. Descartada: ahí hacía falta porque dos ediciones podían dejar la fila en el mismo estado sin que el `WHERE` las distinguiera; acá cada operación cambia el estado o inserta en otra tabla, y el `WHERE Estado = Abierta` ya distingue todas las carreras |
| **El cierre se confirma contra `saldoFinalConfirmado`**, no sólo contra `confirmado: true` (sin `confirmado`, `409 confirmacion_requerida`; con un saldo que no coincide, `409 cierre_desactualizado`; los dos con el resumen en el cuerpo) | FR-018 exige que un movimiento nuevo entre el resumen y la confirmación **no** cierre en silencio con el número nuevo: tiene que mostrarlo y pedir confirmar de nuevo (research §3) | `confirmado: true` a secas, recalculando y cerrando siempre con el número del momento. Descartada: es exactamente el comportamiento que FR-018 prohíbe |

Las dos se resuelven con lo que ya viene en EF Core y SQL Server —`ExecuteUpdateAsync`, una comparación
de `decimal` en la capa de aplicación— sin bibliotecas, servicios externos ni infraestructura propia.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una línea
por decisión, con referencia a la spec (`[011] ...`), en la sección *Decisiones transversales ya
tomadas*. No incluir entradas por incluir: sólo las que sean **información transversal y relevante para
el proyecto** que futuras features puedan aprovechar.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[011]` Cuando una carrera enfrenta una **escritura sobre una fila padre** contra un **`INSERT` en una
  tabla hija** que depende de leer esa fila sin que cambie mientras tanto, un `UPDATE` que no modifica
  ningún valor —`SET Estado = Estado WHERE ... AND Estado = @esperado`— toma el mismo lock que un
  `UPDATE` real y sirve de candado, sin agregar ninguna columna. Es la convención [009] aplicada a una
  carrera entre tablas distintas, no dentro de la misma fila.
- `[011]` Una confirmación de FR-018/[005] sólo necesita el número calculado de vuelta en el cuerpo
  cuando **el usuario no eligió ningún valor propio** que confirmar: si todo el resultado lo calcula el
  servidor, lo que se confirma es el resultado mismo —viaja y vuelve con su propio número—, y no alcanza
  con `confirmado: true` a secas para saber contra qué versión se está confirmando.
- `[011]` Una entidad de otro módulo puede no tener el estado que una spec nueva da por sentado.
  Inventarle uno es alcance fantasma sobre un módulo que no es el que se está construyendo: se trata la
  entidad tal cual existe, y si falta un estado que el negocio necesita, es una spec futura sobre **ese**
  módulo, no una decisión de la spec que la consume.
