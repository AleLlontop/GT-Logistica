# Implementation Plan: Gestión de flota (Módulo 4)

**Branch**: `004-gestion-flota` | **Date**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/004-gestion-flota/spec.md`

## Summary

El Módulo 4 le da a Tráfico el padrón de vehículos con el que trabaja la empresa y responde, unidad por
unidad, la pregunta que hoy vive en planillas sueltas: **qué camión está en condiciones de salir a la
ruta y cuál no**. Mantiene el catálogo de tipos de vehículo, el padrón de flota con su pertenencia a un
transportista, la documentación obligatoria de cada unidad y el panel que avisa qué está por vencer.

Es el primer módulo que **se apoya sobre otro módulo de negocio ya construido** en vez de sobre la
infraestructura común: consume el `Transportista`, el catálogo `DocumentacionTipo`, el almacén de
archivos y la regla de vencimientos del Módulo 3. Buena parte del diseño consistió en decidir qué se
comparte, qué se copia y qué se toca —porque la spec acota los cambios al Módulo 3 a exactamente dos—.

**Enfoque técnico**, con cinco decisiones que definen el módulo:

1. **Los documentos del vehículo van a una tabla propia, no a la de choferes.** La alternativa —una
   tabla con dos dueños posibles— exigiría volver `Documentaciones.ChoferId` anulable, mover la
   entidad de namespace y reescribir `CalculadorEstadoChofer`: tres cambios al Módulo 3 que la spec no
   autoriza, y una garantía `NOT NULL` real cambiada por una restricción escrita a mano (research §1).
2. **El estado operativo se guarda, pero el que manda se deriva al consultar.** FR-014 pide que una
   unidad con el seguro vencido figure fuera de servicio sin que nadie la edite, y que vuelva sola al
   renovar. La columna guardada distingue "parado por reparación" de "parado por papeles"; el valor
   mostrado se calcula (research §4).
3. **Un solo filtro de estado con tres valores excluyentes**, resuelto dentro de la consulta. Sus dos
   valores operativos son complementarios dentro de los activos, y por eso `disponible` no puede
   devolver una unidad con documentación vencida o ausente: lo garantiza el predicado, no un chequeo
   posterior (research §5, SC-006).
4. **Dos permisos, no uno.** `flota.gestionar` para Tráfico y Administrador; `flota.tipos.gestionar`
   sólo para el Administrador. Es la primera vez que la spec distingue niveles de acceso *adentro* de
   un módulo, y la convención del Módulo 1 es autorizar por permiso, nunca por rol (research §7).
5. **Los dos únicos cambios al Módulo 3 son los que la spec declara**: `DocumentacionTipo` gana el
   ámbito (chofer / vehículo) y la baja de `Transportista` pasa a mirar también su flota. Todo lo
   demás del Módulo 3 se consume tal como está (research §2 y §3).

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10 sobre
SQL Server, MailKit, React 19 + React Router + Vite). **Ninguna dependencia nueva y ninguna
infraestructura nueva**: el almacén de archivos, el validador por firma y la paginación ya existen
desde el Módulo 3 y se consumen sin tocarlos (research §2)

**Storage**: SQL Server 2022. Una migración nueva (`Modulo4Flota`) crea `TiposVehiculo`, `Vehiculos` y
`DocumentacionesVehiculo`, agrega la columna `Ambito` a `DocumentacionTipos` con valor `Chofer` para
todas las filas existentes, y siembra los permisos `flota.gestionar` y `flota.tipos.gestionar`. Los
adjuntos van al **mismo volumen** que los del Módulo 3: ninguna variable de entorno nueva

**Testing**: xUnit en `GT.UnitTests` (reglas puras: normalización y formato de patente, estado general
del vehículo, estado operativo derivado, documento vigente de cada tipo) y `GT.IntegrationTests`
(`WebApplicationFactory` contra el SQL Server del compose: unicidad de patente, filtros por estado
calculado, bajas con dependencias, migración del ámbito, y la atomicidad del adjunto con un almacén
forzado a fallar); Vitest + React Testing Library en el frontend

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: los listados responden en menos de 1 segundo (p95) con el volumen real. El
estado de la documentación, la elección del documento vigente de cada tipo y **el estado operativo
derivado** se resuelven **dentro de la consulta SQL**, no trayendo documentos a memoria: es lo que
permite filtrar por `disponible` sin recorrer toda la flota (research §4 y §5). El listado pagina de a
20 filas con el total, filtrando antes de paginar (FR-032, research §9)

**Constraints**: el estado de un documento no es editable por nadie, por ninguna vía (FR-021, SC-004);
la unicidad de `Patente` se garantiza con un índice único en la base, no sólo con validación previa
(FR-002); ninguna baja de vehículo ni de tipo borra físicamente, y el documento es la única entidad que
sí se borra (FR-028); un vehículo sin documentos **no** puede mostrarse en regla ni quedar disponible
(FR-013, FR-033); "hoy" es el día en curso en Argentina (UTC−3), no el del servidor ni el del navegador
(FR-020); los adjuntos se limitan a PDF, JPG y PNG de hasta 10 MB, validados por firma y no por
extensión, y se sirven sólo por endpoint autorizado (FR-025, FR-038, SC-011)

**Scale/Scope**: una única empresa — decenas de vehículos, cientos de documentos. En este módulo:
**6 pantallas nuevas y 2 modificadas, 15 endpoints nuevos y 5 modificados, 3 tablas nuevas y 1 columna
agregada**, que cubren **54 requisitos funcionales** (FR-001 a FR-039, más FR-008a a FR-008f, FR-014a,
FR-016a, FR-017a a FR-017d, FR-019a, FR-026a y FR-030a) y **14 criterios de éxito**

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | Cero dependencias nuevas y cero infraestructura nueva: el almacén de archivos, el validador por firma, `PaginaDe<T>`, `CalculadorEstadoDocumento` y `FechaHoyArgentina` se consumen tal como están. Los estados derivables se calculan al leer en vez de guardarse con un proceso que los mantenga al día. La tabla propia para los documentos de vehículo es **la opción más simple disponible**, no la más elaborada: la alternativa "elegante" —una tabla polimórfica— salía más cara en cambios y en garantías perdidas (research §1). La duplicación que eso deja está acotada, medida y cubierta por tests (research §1, Complexity Tracking) |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes en español rioplatense, definidos textualmente en `contracts/README.md`. La patente se valida contra los dos formatos argentinos vigentes —viejo y Mercosur—, y "hoy" es el día en curso en Argentina (UTC−3) por requisito. Las fechas se formatean con `date-fns` desde `compartido/fechas`, nunca con `toLocaleDateString`. Este módulo no maneja montos |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 52 requisitos y nada más. Quedan explícitamente afuera, tal como fija la spec: asignación a viajes y choferes, kilometraje, mantenimiento, taller, combustible, GPS, notificaciones, validación contra organismos externos y auditoría. Tres tentaciones concretas se anotaron y **no** se construyen: unificar los dos paneles de vencimientos (research §10), extraer un coordinador de adjuntos compartido (research §1) y buscar por texto en el listado, que la spec deja afuera. Ninguna se hace acá |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 6 historias se validan operando la app, con el recorrido de `quickstart.md`. Ver una unidad quedar fuera de servicio con el paso del tiempo se resuelve cargando vencimientos a distintas distancias en vez de pedir que alguien espere (paso 12). **FR-029 es el único requisito sin escenario de aceptación**, porque describe una falla de almacenamiento que nadie puede provocar desde la pantalla: la spec lo declara explícitamente y su verificación queda en un test con el almacén forzado a fallar, en vez de figurar como un criterio que quien valida no podría ejecutar |
| V. Datos del Usuario con Respeto | ✅ Pasa | Del vehículo se piden sólo los datos que la spec enumera. Los adjuntos —cédulas, pólizas— siguen el mismo resguardo que el Módulo 3: volumen fuera del repositorio y de la raíz web, nombre generado por el sistema, tipo validado por firma, y entrega por endpoint que exige sesión y permiso, de modo que conocer la ruta no alcanza (FR-038, SC-011). Eliminar un documento se lleva su archivo (FR-027): un papel que ya no corresponde deja de existir en vez de quedar guardado por las dudas |

**Sobre el Principio I y la duplicación**: la constitución pide elegir siempre lo más simple de
implementar **y de mantener**, y este módulo copia unas 250 líneas del Módulo 3 —la orquestación de
documentos y la regla del vigente por tipo—. No se disfraza: está medida en research §1, la alternativa
que la eliminaría está evaluada y descartada con motivo, y las dos piezas que podrían separarse con el
tiempo —la regla en C# y la misma regla en SQL— llevan un test que las compara sobre el mismo dato, que
ya es convención del proyecto. Es una duplicación elegida, no una omitida.

**Sobre el Principio III y el Módulo 3**: la spec declara en sus *Assumptions* que este módulo modifica
**dos** cosas del Módulo 3 y ninguna más. Ese límite es lo que decidió la arquitectura de la
documentación de vehículos (research §1) y lo que dejó afuera dos relocaciones de namespace que serían
prolijas y no son necesarias (research §2, §3). Quedan anotadas como candidatas para una spec futura.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los cinco principios se sostienen. Cuatro cosas que el diseño confirmó o
descubrió:

- **El alta de un vehículo sólo admite `fuera de servicio`, y no es evidente al leer FR-012.** Sale de
  cruzar FR-013 con FR-014a: una unidad recién registrada no tiene documentos, así que su estado
  general es `sinDocumentacion` y `disponible` queda rechazado. El formulario lo dice de entrada en vez
  de dejar que el operador lo descubra con un error (research §4, contracts/README).
- **FR-014 y FR-014a no son la misma regla y hay que implementarlas por separado.** FR-014a es una
  validación de formulario que explica el motivo en el momento; FR-014 es la derivación al consultar,
  que cubre el paso del tiempo —el seguro que vence de un día para el otro sin que nadie abra la
  pantalla—. Una sola de las dos deja un agujero: sin la primera el operador no entiende por qué no
  puede elegir lo que quiere; sin la segunda hace falta un proceso nocturno.
- **Los dos valores operativos del filtro son complementarios dentro de los activos, y eso es lo que
  cumple SC-006.** Todo vehículo activo cae en exactamente uno de `disponible` o `fueraDeServicio`.
  Escribirlo así hace que el 0% que SC-006 exige lo garantice el predicado de la consulta, no un
  filtrado posterior que alguien podría olvidar (research §5).
- **La asimetría entre las reglas de baja es correcta y no hay que "arreglarla".** El transportista se
  rechaza por dependientes **activos**; el tipo de vehículo y el de documentación, por dependientes
  **cualesquiera**. Tiene motivo: un vehículo dado de baja sigue mostrando su tipo y un documento
  histórico sigue necesitando los días de aviso del suyo, mientras que un transportista inactivo no le
  hace falta a nadie que ya esté de baja. Es el mismo criterio que el Módulo 3 ya aplicaba (research
  §8).

Y una consecuencia que conviene tener a la vista al implementar: **la ficha necesita devolver el estado
operativo dos veces** —el derivado, para mostrar, y el guardado, para poblar el formulario de edición—.
Si devolviera sólo el derivado, editar una unidad fuera de servicio por papeles vencidos le pisaría en
silencio el motivo real al operador.

## Project Structure

### Documentation (this feature)

```text
specs/004-gestion-flota/
├── plan.md              # Este archivo
├── research.md          # Decisiones técnicas y alternativas descartadas
├── data-model.md        # Tablas, campos, reglas y migración
├── quickstart.md        # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md        # Contrato de UI: pantallas, mensajes y textos
│   └── flota-api.yaml   # Contrato HTTP (OpenAPI 3.0), incluidos los endpoints del M3 modificados
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Flota/                              # NUEVO
│   │   │   ├── VehiculosEndpoints.cs           #   listado, ficha, alta, edición, baja, reactivación
│   │   │   ├── TiposVehiculoEndpoints.cs       #   ABM del catálogo — permiso propio (FR-039)
│   │   │   └── DocumentacionVehiculoEndpoints.cs #  carga, corrección, eliminación, descarga y panel
│   │   ├── Choferes/TiposDocumentacionEndpoints.cs # MODIFICADO — ámbito en cuerpo y filtro
│   │   └── Program.cs                          # MODIFICADO — registra el grupo y los dos permisos
│   ├── GT.Application/
│   │   ├── Flota/                              # NUEVO — carpeta espejo del módulo
│   │   │   ├── Documentacion/                  #   carga, corrección, eliminación, descarga y panel
│   │   │   │                                   #   coordinan archivo + fila en el orden de [003]
│   │   │   ├── TiposVehiculo/                  #   ABM del catálogo
│   │   │   ├── CrearVehiculo.cs                #   normaliza y valida patente; FR-008f
│   │   │   ├── ConsultarFlota.cs               #   4 filtros + activos por defecto + página de 20
│   │   │   ├── ConsultarFichaVehiculo.cs
│   │   │   ├── ModificarVehiculo.cs            #   incluye reasignación de transportista (FR-008c)
│   │   │   ├── DarDeBajaVehiculo.cs
│   │   │   ├── ReactivarVehiculo.cs            #   exige transportista y tipo activos (FR-008e)
│   │   │   ├── IRepositorioVehiculos.cs
│   │   │   ├── Dtos.cs
│   │   │   └── Mensajes.cs                     #   textos en es-AR y códigos de error
│   │   ├── Choferes/Documentacion/GestionTiposDocumentacion.cs # MODIFICADO — ámbito (FR-017, 017d)
│   │   └── Choferes/Transportistas/            # MODIFICADO — la baja mira también la flota (FR-008d)
│   ├── GT.Domain/
│   │   ├── Flota/                              # NUEVO
│   │   │   ├── Vehiculo.cs
│   │   │   ├── TipoVehiculo.cs
│   │   │   ├── DocumentacionVehiculo.cs
│   │   │   ├── VehiculoEstado.cs               #   {disponible, fueraDeServicio} (FR-012)
│   │   │   ├── EstadoDocumentacionVehiculo.cs  #   4 valores derivados (FR-033)
│   │   │   ├── CalculadorEstadoVehiculo.cs     #   regla pura: peor estado entre los vigentes
│   │   │   ├── CalculadorEstadoOperativo.cs    #   regla pura: la derivación de FR-014
│   │   │   ├── NormalizadorPatente.cs          #   regla pura (FR-003)
│   │   │   └── ValidadorPatente.cs             #   regla pura: viejo y Mercosur (FR-004)
│   │   ├── Choferes/DocumentacionAmbito.cs     # NUEVO — {chofer, vehiculo} (FR-017)
│   │   ├── Choferes/DocumentacionTipo.cs       # MODIFICADO — propiedad Ambito
│   │   ├── Choferes/Transportista.cs           # MODIFICADO — navegación inversa a Vehiculos
│   │   └── Usuarios/Rol.cs                     # MODIFICADO — los dos códigos de permiso nuevos
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── Configuraciones/                # NUEVO — 3 configuraciones; +1 MODIFICADA (ámbito)
│       │   ├── RepositorioVehiculos.cs         # NUEVO — listado con todo resuelto en SQL
│       │   ├── RepositorioTiposVehiculo.cs     # NUEVO
│       │   ├── RepositorioDocumentacionVehiculo.cs # NUEVO
│       │   ├── RepositorioTiposDocumentacion.cs    # MODIFICADO — cuenta las dos tablas (FR-017b)
│       │   ├── RepositorioTransportistas.cs        # MODIFICADO — cuenta vehículos activos (FR-008d)
│       │   ├── GtDbContext.cs                  # MODIFICADO — 3 DbSet
│       │   └── Migraciones/                    # NUEVO — Modulo4Flota
│       └── DatosIniciales/SembradorInicial.cs  # MODIFICADO — dos permisos y su reparto por rol
└── tests/
    ├── GT.UnitTests/Flota/                     # NUEVO — reglas puras
    └── GT.IntegrationTests/Flota/              # NUEVO

frontend/
└── src/
    ├── modules/flota/                          # NUEVO
    │   ├── paginas/                            #   listado, ficha, formulario, panel de vencimientos
    │   ├── componentes/                        #   filtros del listado y confirmaciones
    │   ├── servicios/
    │   ├── documentacion/                      #   formulario de documento de vehículo
    │   └── tiposVehiculo/                      #   ABM del catálogo
    ├── modules/choferes/documentacion/TiposDocumentacion.tsx # MODIFICADO — campo y filtro de ámbito
    └── App.tsx                                 # MODIFICADO — rutas del módulo
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Flota/` como carpeta espejo del módulo de negocio, alineada 1 a 1 con
`specs/004-gestion-flota/` y con `frontend/src/modules/flota/`, tal como fija la constitución.

`TipoVehiculo` y `DocumentacionVehiculo` viven **dentro** del módulo `flota` y no como módulos
hermanos: las dos existen para sostener al vehículo y ninguna tiene spec propia. Es el mismo criterio
con el que `Transportista` y `DocumentacionTipo` quedaron dentro de `choferes` en el Módulo 3.

`GT.Domain/Flota/` es carpeta propia porque la capa de dominio se organiza por área de negocio y no por
módulo de spec, siguiendo lo que ya hicieron los módulos anteriores.

**Lo que queda fuera de lugar a propósito**: `DocumentacionAmbito` se declara en `GT.Domain/Choferes/`,
junto a `DocumentacionTipo`, aunque describa algo que ya no es sólo de choferes. Mover el catálogo a
una carpeta propia sería más prolijo y toca una decena de archivos del Módulo 3 sin cambiar una sola
conducta; la spec acotó los cambios a ese módulo, así que la relocación queda anotada para una spec
futura (research §2 y §3).

## Complexity Tracking

Tres piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Tabla propia para los documentos de vehículo**, con ~250 líneas de orquestación parecidas a las del Módulo 3 | Es lo que permite tocar el Módulo 3 sólo en las dos cosas que la spec autoriza, y conservar la garantía `ChoferId NOT NULL` que hoy da la base | Una tabla con `ChoferId`/`VehiculoId` anulables y un `CHECK`. Descartada en research §1: obliga a tres cambios extra al Módulo 3 y cambia una garantía real por una restricción escrita a mano. La duplicación queda cubierta por la convención [003] del orden disco/base y por el test que compara la regla en C# contra la consulta en SQL |
| **Dos permisos en vez de uno** | Lo exige FR-039: el catálogo de tipos de vehículo es sólo del Administrador del sistema, el resto del módulo también es de Tráfico | Un permiso único y un chequeo de rol dentro del endpoint del catálogo. Descartada en research §7: la convención del Módulo 1 es autorizar por permiso y nunca por rol, y el menú ya sabe resolver una entrada por permiso sin código nuevo |
| **El estado operativo se guarda y además se deriva** | Lo exige FR-014 junto con FR-012: sin la columna guardada no se distingue una unidad parada por reparación de una parada por papeles, y al renovar el seguro el sistema marcaría disponible un camión roto | Guardar sólo el derivado, o sólo el elegido. Descartadas en research §4: la primera exige un proceso nocturno que mantenga la columna al día —y que la revierta al renovar—; la segunda pierde el motivo real de la parada |

Las tres se resuelven con lo que ya viene en el marco de trabajo y en el proyecto —EF Core, `IFormFile`,
`OFFSET/FETCH`, el almacén de archivos del Módulo 3—, sin dependencias ni servicios externos.

## Mantenimiento al cerrar la feature

Último paso de la fase final, antes de dar el módulo por terminado:

**Actualizar `CLAUDE.md` con las decisiones de diseño y convenciones nuevas de esta feature**, una línea
por decisión, con referencia a la spec (`[004] ...`), en la sección *Decisiones transversales ya
tomadas*. Sólo entran las que son **transversales y relevantes para el proyecto** y que futuras
features pueden aprovechar; nada por completar la lista.

Candidatas que este plan ya identifica, a confirmar recién al implementar:

- `[004]` Un estado que el operador elige y el sistema puede contradecir se guarda **y** se deriva: la
  columna conserva el motivo real y el valor mostrado se calcula al leer. Devolver los dos en la ficha
  —el derivado para mostrar, el guardado para editar— evita pisarle en silencio el motivo al operador.
- `[004]` Cuando un módulo nuevo necesita la misma clase de dato que otro ya construido (documentación,
  adjuntos), se comparte la **regla** y el **almacén**, no necesariamente la **tabla**: una clave
  foránea anulable con un `CHECK` cambia una garantía de la base por una convención escrita a mano.
- `[004]` Un módulo con dos niveles de acceso adentro lleva **dos permisos**, no un permiso y un
  chequeo de rol en el endpoint.
- `[004]` Un filtro de estado cuyos valores son complementarios dentro de un universo (activo /
  inactivo) se escribe como predicado único en la consulta: es lo que hace que la exclusión sea una
  garantía y no un filtrado posterior que alguien puede olvidar.
