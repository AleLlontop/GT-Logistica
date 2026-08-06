# Implementation Plan: Gestionar usuarios y roles (Módulo 2)

**Branch**: `002-gestion-usuarios-roles` | **Date**: 2026-08-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-gestion-usuarios-roles/spec.md`

## Summary

El Módulo 2 le da al responsable de sistemas el ABM completo de cuentas: crear un usuario con al
menos un rol, encontrarlo, editarlo, restablecerle la contraseña, darlo de baja lógicamente y
ajustar sus roles, más la consulta en modo lectura de los permisos que otorga cada rol. La sesión de
clarificación del 2026-08-05 le sumó el **padrón de personas** (choferes y empleados): un ABM propio,
sin datos precargados, porque la asociación usuario↔persona no tiene de dónde elegir si nadie carga
esas personas primero.

El Módulo 1 ya dejó montado el andamiaje —solución en capas, app de React, `docker-compose.yml`,
esquema inicial con el administrador y el catálogo de roles y permisos—, así que este módulo **no
crea infraestructura nueva salvo una**: el envío de correo, que la spec necesita para el
restablecimiento de contraseña y que hoy no existe en el repositorio, pese a que las *Assumptions* lo
daban por existente.

**Enfoque técnico**: extender el esquema existente en una sola migración (columnas nuevas en
`Usuarios` + tabla `Personas`), apoyarse en las piezas del Módulo 1 que ya resuelven requisitos de
éste sin escribir código nuevo (el `RevalidadorSesion` corta la sesión de una cuenta que deja de
estar activa → FR-016; `VigenciaPasswordTemporal` y `PasswordTemporalGeneradaEn` ya definen la
contraseña temporal de 24 horas → FR-009), y agregar el envío de correo por SMTP detrás de una
interfaz con dos implementaciones, para que el sistema levante y se valide sin depender de un
servidor SMTP real. El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend. Sin cambios respecto del Módulo 1

**Primary Dependencies**: las ya presentes (ASP.NET Core con autenticación por cookie, EF Core 10
sobre SQL Server, `Microsoft.AspNetCore.Identity.PasswordHasher`, React 19 + React Router + Vite) más
**una sola dependencia nueva**: MailKit, para el envío SMTP del restablecimiento de contraseña
(research §1)

**Storage**: SQL Server 2022. Una migración nueva agrega `Email`, `EmailNormalizado`, `FechaAlta` y
`PersonaId` a `Usuarios`, y crea la tabla `Personas`. El catálogo de roles y permisos ya está
sembrado por el Módulo 1 y este módulo **no lo modifica** (FR-010)

**Testing**: xUnit en `GT.UnitTests` (reglas puras: normalización de email, protección del último
administrador, generación de contraseña temporal) y `GT.IntegrationTests`
(`WebApplicationFactory` contra el SQL Server del compose, para unicidad, concurrencia y corte de
sesión); Vitest + React Testing Library en el frontend

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: las pantallas de listado responden en menos de 1 segundo (p95) con el volumen
real del sistema. El restablecimiento de contraseña queda dominado por dos costos deliberadamente
lentos —el hasheo (~100 ms) y el envío SMTP— y por eso el envío no bloquea el resultado de la
operación (FR-021)

**Constraints**: la contraseña temporal nunca se muestra en pantalla ni se devuelve en ninguna
respuesta (FR-009, SC-004); las credenciales SMTP viajan por variables de entorno y no quedan
versionadas (Principio V); la unicidad de `username`, `email` y `dni` se garantiza con índices
únicos en la base, no sólo con validación previa (FR-002, FR-003, FR-027); nunca puede quedar el
sistema sin un usuario `activo` con el rol *Administrador del sistema*, ni siquiera cuando la cuenta
afectada es la de quien opera (FR-019); ninguna baja borra físicamente (FR-006, FR-022); las
pantallas nuevas mantienen el piso de accesibilidad que fijó el Módulo 1

**Scale/Scope**: personal de una única empresa — decenas de usuarios y personas, no miles. En este
módulo: 7 pantallas (listado, detalle y formulario de usuarios, panel de roles, listado y formulario
de personas, cambio de contraseña propia), 14 endpoints y 1 tabla nueva

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | Se reutiliza todo lo que el Módulo 1 ya resolvió en vez de duplicarlo: el corte de sesión (FR-016) no escribe una línea de código nueva, la vigencia de la contraseña temporal ya existe, y el hasheador y el cliente HTTP se usan tal cual. La única dependencia nueva es MailKit, y la baja lógica de Persona es un `bool`, no un enum de dos valores. Cada descarte está en `research.md` |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes en español rioplatense (voseo), definidos textualmente en `contracts/README.md` junto a los del Módulo 1. Este módulo no maneja montos, así que el formato ARS sigue sin aplicar |
| III. Cero Alcance Fantasma | ✅ Pasa | El esquema agrega exactamente los campos que la spec pide y ni uno más — el cambio de contraseña propia no agrega ninguno. Las dos ampliaciones de alcance (padrón de personas y cambio de contraseña) entraron por clarificaciones registradas, no por anticipación. Los siete campos de Persona son los que fija FR-026: no se agregan legajo, CUIL ni fecha de ingreso, que se evaluaron y se descartaron |
| IV. Verificable por una Persona No Técnica | ✅ Pasa | Las 7 historias se validan operando la app, `quickstart.md` las recorre completas. Este módulo además **repara** la limitación que el Módulo 1 dejó abierta: la User Story 4 de aquel módulo (cuentas `inactiva` y `bloqueada`) ya no exige tocar la base a mano, porque ahora hay una pantalla para cambiar el estado |
| V. Datos del Usuario con Respeto | ✅ Pasa | La contraseña temporal se genera, se hashea y se envía sin quedar nunca en una respuesta, en un log ni en pantalla. Las credenciales SMTP se leen de variables de entorno declaradas en `.env.template`. De la persona se piden sólo los siete datos de FR-026 |

**Sobre el Principio III y el padrón de personas**: la spec de origen listaba "alta de personas"
como fuera de alcance. La clarificación del 2026-08-05 lo movió adentro y la spec quedó actualizada
en consecuencia (User Story 6, FR-022 a FR-028, *Assumptions*). No es alcance fantasma —es alcance
acordado y escrito— pero sí es el mayor riesgo de estimación del módulo, y conviene tenerlo
presente: son 2 pantallas y 5 endpoints que no estaban en el planteo original.

**Estructura de carpetas**: respeta el esquema fijo de la constitución. El padrón de personas queda
**dentro** del módulo `usuarios` (subcarpetas `personas/`), no como módulo hermano, para sostener la
regla de "una carpeta de spec ↔ una carpeta de módulo" — ver *Structure Decision*.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño completo (`data-model.md`, `contracts/`, `quickstart.md`), los cinco principios
siguen pasando y no aparecieron violaciones nuevas. Cuatro cosas que el diseño confirmó o corrigió:

- **El envío de correo no existía y hay que construirlo.** Las *Assumptions* de la spec daban por
  existente un mecanismo de correo saliente que nadie había escrito. Se construye en este módulo, por
  SMTP con MailKit, detrás de una interfaz con dos implementaciones (research §1). Es la única
  infraestructura nueva del módulo y la única dependencia agregada.
- **La migración toca una fila existente.** El usuario `admin` que sembró el Módulo 1 no tiene
  `Email`, y FR-003 lo exige único y obligatorio. El diseño lo resuelve rellenando esa única fila
  con `admin@gtlogistica.local` —editable desde la pantalla nueva— en vez de agregar una variable de
  entorno más. Está documentado en `research.md` §5 y en `data-model.md`.
- **La unicidad de `PersonaId` necesita un índice filtrado.** Un índice único común trataría varios
  `NULL` como duplicados en SQL Server y rompería el caso "usuario sin persona asociada", que la
  spec declara válido. El diseño usa `WHERE PersonaId IS NOT NULL`.
- **El endpoint provisional `GET /api/usuarios` del Módulo 1 desaparece.** Lo dejó `Program.cs` como
  andamio para que la opción de menú tuviera destino; ahora lo reemplaza el listado real. No es
  deuda nueva: es andamio previsto que se retira.
- **Cortar sesiones al restablecer obliga a tocar el Módulo 1.** FR-032 no se puede cumplir sin
  agregar una condición al `RevalidadorSesion`, que hoy sólo mira el estado de la cuenta. Se resuelve
  con una columna `PasswordActualizadaEn` y una comparación contra el `IssuedUtc` de la cookie, sin
  *claims* nuevos ni tabla de sesiones (research §10). Es el único archivo del Módulo 1 con cambio de
  comportamiento, así que sus tests de integración de autenticación hay que volver a correrlos.

**El hueco de las 24 horas queda cerrado**: el diseño detectó que la contraseña temporal vence a las
24 horas (regla del Módulo 1) sin que existiera ninguna pantalla donde el usuario pudiera cambiarla,
con lo cual quien no ingresaba en ese plazo quedaba sin acceso indefinidamente. Se agregó el cambio
de contraseña propia (User Story 7, FR-029 a FR-031).

Esa pantalla obliga a una excepción de autorización que conviene tener presente al implementar: **es
la única del módulo que no exige el rol *Administrador del sistema***. Cualquier usuario autenticado
tiene que poder llegar a ella, así que su endpoint se protege sólo con `RequireAuthorization()` sin
política de permiso, y su enlace no sale del `CatalogoOpcionesMenu` —que mapea permisos a opciones—
sino que vive fijo en el encabezado, al lado de *Cerrar sesión*.

## Project Structure

### Documentation (this feature)

```text
specs/002-gestion-usuarios-roles/
├── plan.md              # Este archivo
├── research.md          # Decisiones técnicas y alternativas descartadas
├── data-model.md        # Tablas, campos, reglas y migración
├── quickstart.md        # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md        # Contrato de UI: pantallas, mensajes y textos
│   └── usuarios-api.yaml # Contrato HTTP (OpenAPI 3.0)
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

Sólo se listan las carpetas y archivos que este módulo **agrega o modifica**; el resto del árbol lo
dejó el Módulo 1 y no se toca.

```text
backend/
├── src/
│   ├── GT.Api/
│   │   ├── Usuarios/                       # NUEVO — endpoints de usuarios, roles y personas
│   │   │   ├── UsuariosEndpoints.cs
│   │   │   ├── RolesEndpoints.cs
│   │   │   ├── MiCuentaEndpoints.cs        #   cambio de contraseña propia — el único
│   │   │   │                               #   SIN política de permiso (FR-029)
│   │   │   └── Personas/PersonasEndpoints.cs
│   │   └── Program.cs                      # MODIFICADO — registra los endpoints nuevos y
│   │                                       #   retira el GET /api/usuarios provisional
│   ├── GT.Application/
│   │   └── Usuarios/                       # NUEVO — carpeta espejo del módulo
│   │       ├── CrearUsuario.cs             #   un caso de uso por operación
│   │       ├── ConsultarUsuarios.cs
│   │       ├── ModificarUsuario.cs
│   │       ├── DarDeBajaUsuario.cs
│   │       ├── AsignarRoles.cs
│   │       ├── RestablecerPassword.cs
│   │       ├── CambiarPasswordPropia.cs
│   │       ├── ConsultarRoles.cs
│   │       ├── Dtos.cs
│   │       ├── Mensajes.cs                 #   textos en es-AR y códigos de error
│   │       └── Personas/                   #   ABM del padrón
│   │           ├── CrearPersona.cs
│   │           ├── ConsultarPersonas.cs
│   │           ├── ModificarPersona.cs
│   │           ├── DarDeBajaPersona.cs
│   │           └── Dtos.cs
│   ├── GT.Api/Autenticacion/
│   │   └── RevalidadorSesion.cs            # MODIFICADO (Módulo 1) — corta la sesión si la
│   │                                       #   contraseña cambió después de emitirse (FR-032)
│   ├── GT.Domain/
│   │   ├── Usuarios/
│   │   │   ├── Usuario.cs                  # MODIFICADO — Email, EmailNormalizado,
│   │   │   │                               #   FechaAlta, PersonaId, PasswordActualizadaEn
│   │   │   ├── NormalizadorEmail.cs        # NUEVO — regla pura
│   │   │   └── ProteccionUltimoAdministrador.cs  # NUEVO — regla pura (FR-019)
│   │   └── Personas/                       # NUEVO
│   │       ├── Persona.cs
│   │       └── TipoIntegrante.cs
│   └── GT.Infrastructure/
│       ├── Persistencia/
│       │   ├── GtDbContext.cs              # MODIFICADO — DbSet<Persona>
│       │   ├── Configuraciones/
│       │   │   ├── UsuarioConfiguracion.cs # MODIFICADO — índices y relación con Persona
│       │   │   └── PersonaConfiguracion.cs # NUEVO
│       │   └── Migraciones/                # NUEVO — una migración: usuarios + personas
│       ├── Seguridad/
│       │   └── GeneradorPasswordTemporal.cs # NUEVO
│       └── Correo/                         # NUEVO — la única infraestructura nueva
│           ├── EnviadorCorreoSmtp.cs       #   MailKit, para entorno real
│           └── EnviadorCorreoRegistrado.cs #   escribe al log, para desarrollo y CI
└── tests/
    ├── GT.UnitTests/Usuarios/              # NUEVO — reglas puras
    └── GT.IntegrationTests/Usuarios/       # NUEVO — unicidad, concurrencia, último admin,
                                            #   corte de sesión, restablecimiento

frontend/
└── src/
    ├── modules/usuarios/                   # NUEVO
    │   ├── paginas/
    │   │   ├── ListadoUsuarios.tsx
    │   │   ├── DetalleUsuario.tsx          #   /usuarios/{id} — exigida por FR-013
    │   │   ├── FormularioUsuario.tsx
    │   │   ├── PanelRoles.tsx
    │   │   └── CambiarPassword.tsx         #   /mi-cuenta/contrasena — para todo usuario
    │   ├── componentes/                    #   filtros, confirmación de baja, permisos por módulo
    │   ├── servicios/usuarios.ts
    │   └── personas/                       #   el padrón, dentro del mismo módulo
    │       ├── paginas/
    │       │   ├── ListadoPersonas.tsx
    │       │   └── FormularioPersona.tsx
    │       └── servicios/personas.ts
    ├── compartido/tipos.ts                 # MODIFICADO — códigos de error nuevos
    ├── compartido/Layout.tsx               # MODIFICADO — enlace fijo a "Cambiar contraseña"
    │                                       #   junto a "Cerrar sesión", fuera del menú por
    │                                       #   permisos (no depende de roles)
    └── App.tsx                             # MODIFICADO — rutas reales en vez del placeholder

.env.template                               # MODIFICADO — variables SMTP
docker-compose.yml                          # MODIFICADO — pasa las variables SMTP al backend
```

**Structure Decision**: se mantiene la aplicación web con backend y frontend separados que fijó el
Módulo 1, con el backend en sus cuatro capas y `GT.Application/Usuarios/` como carpeta espejo del
módulo de negocio.

La decisión con más consecuencias es **dónde vive el padrón de personas**. La constitución pide que
las carpetas de `/specs/` estén alineadas 1 a 1 con las carpetas de módulo del frontend y del
backend. Como el padrón entró por una clarificación de la spec 002 y no tiene spec propia, se
implementa **dentro** del módulo `usuarios` (`GT.Application/Usuarios/Personas/`,
`frontend/src/modules/usuarios/personas/`) en vez de crear un módulo hermano `personas` que no
tendría carpeta de spec que lo respalde. En esta versión la persona sólo existe para identificar al
titular de una cuenta, así que la anidación describe bien la relación real. Cuando el padrón crezca
más allá de eso —choferes asignados a viajes, por ejemplo— le va a corresponder su propia spec, y
ahí se promueve a módulo hermano.

`GT.Domain/Personas/` sí queda como carpeta propia: la capa de dominio se organiza por área de
negocio y no por módulo de spec, siguiendo lo que ya hizo el Módulo 1 al separar
`GT.Domain/Usuarios/` de `GT.Domain/Autenticacion/`.

## Complexity Tracking

> Sin violaciones que justificar. La única pieza de infraestructura nueva —el envío de correo— la
> exige FR-009 de la spec, y se resuelve con una interfaz y dos implementaciones, que es el mínimo
> necesario para que el sistema levante y se valide sin un servidor SMTP real. Las alternativas
> descartadas están en [research.md](./research.md).
