# Implementation Plan: Gestionar choferes y su documentación (Módulo 3)

**Branch**: `003-gestion-choferes` | **Date**: 2026-08-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-gestion-choferes/spec.md`

## Summary

El Módulo 3 le da al sector de Tráfico el padrón de transportistas, el registro de choferes con su
documentación obligatoria, y la vista que responde la pregunta que motiva todo el módulo: **quién
está en condiciones de salir a la ruta y quién no**.

Se apoya sobre lo que ya existe: la autenticación y los roles del Módulo 1, y el padrón de `Persona`
del Módulo 2 —del que el chofer toma sus datos personales sin duplicarlos (FR-006)—.

> **Revisión del 2026-08-06 (segunda).** Tras la revisión de checklist, la spec sumó la corrección y
> la eliminación de documentos (FR-015b a FR-015d) y la atomicidad de la carga del archivo
> (FR-015e). El diseño lo absorbe con dos endpoints nuevos, una pantalla más y una regla de orden
> entre el disco y la base (research §10). No cambia ninguna tabla: el documento se borra
> físicamente, así que no lleva `Activo`.
>
> **Revisión del 2026-08-06.** Este plan se escribió antes de la segunda sesión de clarificación de
> la spec y se actualizó después de ella. Lo que cambió: el estado del chofer y las alertas miran
> **sólo el documento vigente de cada tipo** (FR-020a), el listado **pagina** y muestra activos por
> defecto (FR-030, FR-022), y los adjuntos tienen formatos y tamaño fijados por requisito y ya no por
> inferencia del plan (FR-015a). La decisión de construir el almacén de archivos, que el plan
> proponía y marcaba para confirmar, quedó confirmada.

**Enfoque técnico**, con cuatro decisiones que definen el módulo:

1. **El chofer se modela por composición, no por herencia** (`Choferes` con clave foránea única a
   `Personas`). Es lo que permite que alguien ya cargado como empleado se registre como chofer
   reutilizando su fila, que es un caso límite explícito de la spec — y que la herencia de EF Core
   directamente no admite (research §1).
2. **El estado de un documento no se guarda: se calcula al leer.** FR-019 exige que un documento pase
   solo de `vigente` a `proximaAvencer` y luego a `vencida` con el correr de los días. Una columna
   almacenada obligaría a un proceso diario que la actualice; una expresión sobre la fecha no
   (research §2).
3. **De cada tipo de documento manda uno solo: el de vencimiento más lejano.** Los demás son
   historial visible que no alerta ni ensucia el estado del chofer (FR-020a). Es lo que hace que
   cargar una renovación saque la alerta sin que nadie borre el papel viejo, y se resuelve con una
   función de ventana en la misma consulta que ya calcula el estado (research §8).
4. **Un permiso nuevo, `choferes.gestionar`**, otorgado a *Tráfico* y a *Administrador del sistema*
   (FR-027). Es el primer módulo con acceso para un rol que no es el administrador, así que el
   esquema de permisos del Módulo 1 se ejercita por primera vez de verdad (research §5).

El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto de los módulos anteriores

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10
sobre SQL Server, MailKit, React 19 + React Router + Vite). **Ninguna dependencia nueva**: la carga
de archivos se resuelve con `IFormFile`, que viene en el marco de trabajo (research §3)

**Storage**: SQL Server 2022. Una migración nueva crea `Transportistas`, `Choferes`,
`DocumentacionTipos` y `Documentaciones`, y siembra el permiso `choferes.gestionar`. Los archivos
adjuntos **no** van a la base: se guardan en un volumen y la fila conserva su ruta (research §3)

**Testing**: xUnit en `GT.UnitTests` (reglas puras: cálculo del estado de un documento, dígito
verificador de CUIT/CUIL, mayoría de edad) y `GT.IntegrationTests` (`WebApplicationFactory` contra el
SQL Server del compose, para unicidad, reutilización de persona, bajas con dependencias y filtros por
estado calculado); Vitest + React Testing Library en el frontend

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: los listados responden en menos de 1 segundo (p95) con el volumen real. Tanto
el cálculo del estado de la documentación como la elección del documento vigente de cada tipo se
resuelven **dentro de la consulta SQL**, no trayendo los documentos a memoria: es lo que permite
filtrar por estado sin recorrer todo el padrón (research §2 y §8). El listado de choferes pagina de a
20 filas con el total, filtrando antes de paginar (FR-030, research §9)

**Constraints**: el estado de un documento no es editable por nadie, por ninguna vía (FR-018,
SC-004); la unicidad de `dni`, `cuil` y `cuit` se garantiza con índices únicos en la base, no sólo
con validación previa (FR-003, FR-006, FR-007); ninguna baja borra físicamente (FR-001, FR-005,
FR-012); un chofer sin documentos **no** puede mostrarse como en regla (FR-028); los adjuntos se
limitan a PDF, JPG y PNG de hasta 10 MB, validados por firma y no por extensión (FR-015a), y no
quedan versionados en el repositorio ni son accesibles sin sesión con el permiso del módulo (FR-024)

**Scale/Scope**: una única empresa — decenas de transportistas y choferes, cientos de documentos. En
este módulo: 10 pantallas, 20 endpoints y 4 tablas nuevas, que cubren 40 requisitos funcionales
(FR-001 a FR-030, más FR-005a, FR-005b, FR-015a a FR-015e, FR-017a, FR-020a y FR-029a) y 11
criterios de éxito

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ⚠️ Atención | Estado calculado en vez de columna más proceso diario; `IFormFile` y un volumen en vez de almacenamiento de objetos; composición en vez de jerarquía de tipos. Cero dependencias nuevas. **La paginación (FR-030) es la única pieza que el volumen actual no exige** —decenas de choferes entran en una pantalla— y se construye igual porque la spec la fija; queda justificada acá, no dentro del plan técnico. Cada descarte está en `research.md` |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes en español rioplatense, definidos textualmente en `contracts/README.md`. CUIT y CUIL se validan con el dígito verificador del esquema argentino. Este módulo no maneja montos |
| III. Cero Alcance Fantasma | ✅ Pasa | Se implementan los 40 requisitos y nada más. La documentación obligatoria por tipo quedó explícitamente fuera de alcance (FR-029a) en vez de inferirse. La asignación a viajes, el bloqueo automático del chofer con documentación vencida, la notificación de vencimientos y la verificación contra ANSES/CNRT/AFIP quedan afuera, tal como fija la spec |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 7 historias se validan operando la app. Ver a un documento cambiar de estado con el paso de los días se resuelve en `quickstart.md` cargando vencimientos a distintas distancias en vez de pedir que alguien espere. **FR-015e es el único requisito sin escenario de aceptación**, porque describe una falla de almacenamiento que nadie puede provocar desde la pantalla: se declara explícitamente en la spec y su verificación queda en el test T061, en vez de figurar como un criterio que quien valida no podría ejecutar |
| V. Datos del Usuario con Respeto | ✅ Pasa | De la persona se piden sólo los datos que la spec enumera. Este módulo incorpora el primer archivo cargado por el usuario, y un escaneo de licencia o de psicofísico es un dato sensible: FR-024 y FR-015a ahora exigen por requisito lo que el plan proponía por criterio —se sirve sólo con sesión y con el permiso del módulo, nunca por URL pública, con formatos y tamaño acotados, y el volumen fuera del repositorio—. Eliminar un documento se lleva su archivo (FR-015c): un escaneo que ya no corresponde deja de existir en vez de quedar guardado por las dudas |

**Sobre el Principio V y los archivos**: es lo más delicado del módulo. Un psicofísico contiene datos
de salud. El diseño los guarda fuera de la raíz web y los entrega por un endpoint autorizado, de modo
que conocer la ruta no alcanza para verlos. Está detallado en `research.md` §3.

**Sobre el Principio I y la paginación**: la constitución pide justificar por escrito toda
complejidad que el problema no exija. El padrón de choferes de G&T entra hoy en una sola pantalla, y
sin FR-030 este listado se resolvería como los del Módulo 2, sin paginar. Se construye igual porque
la spec lo decidió explícitamente en la clarificación del 2026-08-06, y porque el listado de choferes
crece con la operación mientras que el de usuarios no. El costo es acotado —`OFFSET/FETCH` más un
`COUNT`, sin dependencias— y la forma de la respuesta queda como precedente para los módulos
siguientes (research §9). Es una desviación aceptada, no una omisión.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo, los principios se sostienen. Tres cosas que el diseño confirmó:

- **La composición resuelve más casos de los previstos.** Además del empleado que pasa a chofer,
  permite el camino inverso —un chofer que deja de serlo conserva su persona y su cuenta— y deja
  `TransportistaId` como `NOT NULL` real, algo que bajo herencia sólo se podría sostener por código.
- **El filtro por estado de documentación se traduce a SQL sin traer filas.** Era el riesgo del
  estado calculado, y se despeja: la expresión usa sólo `fechaVencimiento`, `diasAvisoVencimiento` y
  la fecha del día, las tres disponibles en la consulta.
- **`Persona.Tipo` queda ambiguo y hay que decidirlo, no dejarlo pasar.** La fila en `Choferes` pasa
  a ser la única fuente de verdad sobre quién es chofer; el campo `Tipo` del Módulo 2 queda como dato
  informativo del padrón. Está documentado en `research.md` §1 y afecta a una pantalla del Módulo 2.

Y tres más que salieron de rehacer el diseño con las clarificaciones del 2026-08-06:

- **El "documento más reciente" necesitaba un desempate y la spec no lo da.** Dos documentos del
  mismo tipo con la misma fecha de vencimiento son un error de carga plausible, y sin criterio
  adicional la consulta devuelve una fila u otra según el plan de ejecución: el listado cambiaría
  solo entre dos consultas idénticas. El diseño lo cierra con el `Id` mayor (research §8). Es una
  decisión de diseño, no un requisito nuevo: la spec no la contradice.
- **La paginación obligó a fijar un orden total.** `Apellido, Nombre, Id`. Sin el `Id` final, dos
  choferes homónimos pueden intercambiarse entre páginas y aparecer duplicados o desaparecer
  (research §9). Es el error clásico de paginar sin orden estable y no se descubre con pocos datos.
- **La atomicidad entre el disco y la base no se puede garantizar del todo, así que hay que elegir
  qué falla se acepta.** La base es transaccional y el sistema de archivos no. El diseño fija un
  orden —el archivo se escribe antes de confirmar la fila y se borra después— que deja como único
  estado roto posible un archivo huérfano, invisible para quien opera, y descarta el estado roto que
  FR-015e prohíbe: una fila que dice tener adjunto y no lo tiene (research §10). No se construye
  ninguna limpieza de huérfanos: sería alcance fantasma.
- **Dos escalas de estado con nombres distintos.** El documento usa `vigente`; el chofer, `enRegla`.
  El contrato anterior usaba `vigente` para las dos cosas, y con FR-029 dando cuatro valores al
  estado del chofer eso volvía ambiguo qué significaba `vigente` en cada respuesta. Están separados
  en `contracts/choferes-api.yaml` y en los textos de pantalla.

## Project Structure

### Documentation (this feature)

```text
specs/003-gestion-choferes/
├── plan.md              # Este archivo
├── research.md          # Decisiones técnicas y alternativas descartadas
├── data-model.md        # Tablas, campos, reglas y migración
├── quickstart.md        # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md        # Contrato de UI: pantallas, mensajes y textos
│   └── choferes-api.yaml # Contrato HTTP (OpenAPI 3.0)
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Choferes/                       # NUEVO
│   │   │   ├── TransportistasEndpoints.cs
│   │   │   ├── ChoferesEndpoints.cs
│   │   │   ├── DocumentacionEndpoints.cs   #   carga, corrección, eliminación y descarga
│   │   │   └── TiposDocumentacionEndpoints.cs
│   │   └── Program.cs                      # MODIFICADO — registra los grupos y el permiso nuevo
│   ├── GT.Application/
│   │   └── Choferes/                       # NUEVO — carpeta espejo del módulo
│   │       ├── Transportistas/             #   ABM de transportistas
│   │       ├── Documentacion/              #   carga, corrección, eliminación, catálogo y panel
│   │       │                               #   la carga y la corrección coordinan archivo + fila
│   │       │                               #   en el orden de research §10
│   │       ├── CrearChofer.cs              #   reutiliza la Persona del Módulo 2 si el DNI existe
│   │       ├── ConsultarChoferes.cs        #   filtros + activos por defecto + página de 20
│   │       ├── ModificarChofer.cs
│   │       ├── DarDeBajaChofer.cs
│   │       ├── PaginaDe.cs                 #   { items, total, pagina, tamanioPagina } (FR-030)
│   │       ├── Dtos.cs
│   │       └── Mensajes.cs                 #   textos en es-AR y códigos de error
│   ├── GT.Domain/
│   │   ├── Choferes/                       # NUEVO
│   │   │   ├── Chofer.cs                   #   FK única a Persona, CUIL, Transportista
│   │   │   ├── Transportista.cs
│   │   │   ├── TipoPersona.cs              #   {fisica, juridica} — el nombre quedó libre al
│   │   │   │                               #   renombrar el del Módulo 2 a TipoIntegrante
│   │   │   ├── Documentacion.cs
│   │   │   ├── DocumentacionTipo.cs
│   │   │   ├── DocumentacionEstado.cs
│   │   │   ├── CalculadorEstadoDocumento.cs # regla pura (FR-017)
│   │   │   ├── CalculadorEstadoChofer.cs   #   regla pura: peor estado entre los vigentes (FR-029)
│   │   │   ├── ValidadorCuit.cs            #   dígito verificador (FR-003, FR-007)
│   │   │   └── MayoriaDeEdad.cs            #   regla pura (FR-011)
│   │   └── Personas/Persona.cs             # MODIFICADO — navegación inversa al Chofer
│   └── GT.Infrastructure/
│       ├── Persistencia/                   # MODIFICADO — 4 DbSet, configuraciones y migración
│       ├── DatosIniciales/SembradorInicial.cs # MODIFICADO — permiso `choferes.gestionar`
│       └── Archivos/                       # NUEVO — guarda y recupera adjuntos del volumen
│           ├── AlmacenDeArchivos.cs
│           └── ValidadorArchivo.cs         #   PDF/JPG/PNG y 10 MB, por firma (FR-015a)
└── tests/
    ├── GT.UnitTests/Choferes/              # NUEVO — reglas puras
    └── GT.IntegrationTests/Choferes/       # NUEVO

frontend/
└── src/
    ├── modules/choferes/                   # NUEVO
    │   ├── paginas/                        #   listado y ficha de chofer, panel de vencimientos
    │   ├── componentes/                    #   incluye el control de paginación (FR-030)
    │   ├── servicios/
    │   ├── transportistas/                 #   ABM de transportistas
    │   └── documentacion/                  #   carga de documentos y catálogo de tipos
    └── App.tsx                             # MODIFICADO — rutas del módulo

.env.template                               # MODIFICADO — ruta del volumen de adjuntos
docker-compose.yml                          # MODIFICADO — volumen para los archivos
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados, con
`GT.Application/Choferes/` como carpeta espejo del módulo de negocio, alineada 1 a 1 con
`specs/003-gestion-choferes/` y con `frontend/src/modules/choferes/`, tal como fija la constitución.

`Transportista`, `Documentacion` y `DocumentacionTipo` viven **dentro** del módulo `choferes` y no
como módulos hermanos: las tres existen para sostener al chofer y ninguna tiene spec propia. Es el
mismo criterio con el que el padrón de personas quedó dentro de `usuarios` en el Módulo 2.

`GT.Domain/Choferes/` es carpeta propia porque la capa de dominio se organiza por área de negocio y
no por módulo de spec, siguiendo lo que ya hicieron los Módulos 1 y 2.

## Complexity Tracking

Dos piezas para dejar anotadas, ninguna de ellas una violación sin justificar.

| Pieza | Por qué está | Alternativa más simple, y por qué se descartó |
|---|---|---|
| **Almacén de archivos adjuntos** | Lo exigen FR-015 y FR-015a. Es la única infraestructura nueva del módulo | Guardar sólo una URL externa que el operador pega. Descartada en la clarificación del 2026-08-06: un enlace externo no puede respetar FR-027 y expondría datos sensibles a cualquiera con la dirección (research §3) |
| **Paginación del listado** | La exige FR-030 | No paginar, como hace el Módulo 2. Es lo que el volumen actual pediría, y se descartó por decisión explícita del usuario en la clarificación (research §9). Justificación completa en Constitution Check |

Se resuelven las dos con lo que ya viene en el marco de trabajo —`IFormFile` y `OFFSET/FETCH`—, sin
dependencias ni servicios externos.
