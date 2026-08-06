# Implementation Plan: Autenticación de usuarios (Módulo 1)

**Branch**: `001-autenticacion-usuarios` | **Date**: 2026-08-04 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-autenticacion-usuarios/spec.md`

## Summary

El Módulo 1 es el punto de entrada del Sistema Integral de Gestión: pantalla de inicio de sesión,
validación de credenciales contra contraseñas hasheadas, sesión con vencimiento por inactividad,
autorización verificada en el servidor en cada operación, y cierre de sesión.

Como es el primer módulo del repositorio, también deja montado el esqueleto sobre el que se apoyan
los módulos siguientes: la solución en capas del backend, la app de React, el `docker-compose.yml`
y el esquema inicial de base de datos con el usuario administrador y el catálogo de roles y
permisos (FR-019).

**Enfoque técnico**: autenticación por *cookie* de sesión de ASP.NET Core (no JWT), revalidando el
usuario contra la base de datos en cada petición. Esa única decisión resuelve cuatro requisitos que
un token autocontenido no puede cumplir sin agregar complejidad: permisos efectivos calculados en
cada operación (FR-006), corte inmediato de la sesión cuando la cuenta deja de estar activa
(FR-009), cierre real al cerrar sesión (FR-013) y fin de la sesión al cerrar el navegador (FR-022).
El detalle y las alternativas descartadas están en [research.md](./research.md).

## Technical Context

**Language/Version**: C# / .NET 10 (LTS) en el backend; TypeScript 5.x sobre Node 22 LTS en el
frontend

**Primary Dependencies**: ASP.NET Core (autenticación por cookie, autorización por políticas),
EF Core 10 con proveedor SQL Server, `Microsoft.AspNetCore.Identity.PasswordHasher` (sólo el
hasher, no ASP.NET Core Identity completo); React 19 + React Router + Vite

**Storage**: SQL Server 2022 (contenedor en desarrollo). Esquema y datos iniciales por migraciones
de EF Core. La sesión no se persiste: vive en la cookie cifrada

**Testing**: xUnit en `GT.UnitTests` (reglas de negocio puras) y `GT.IntegrationTests`
(`WebApplicationFactory` contra el SQL Server del compose); Vitest + React Testing Library en el
frontend

**Target Platform**: aplicación web servida desde contenedores Linux; navegadores de escritorio
actuales (es el equipamiento de oficina de G&T Logística)

**Project Type**: aplicación web con backend y frontend separados

**Performance Goals**: el inicio de sesión responde en menos de 1 segundo (p95) en condiciones
normales; el costo dominante es el hasheo de contraseña, deliberadamente lento (~100 ms). El resto
de las operaciones protegidas agrega una consulta de revalidación de usuario y roles por petición

**Constraints**: la contraseña nunca viaja en la URL, nunca se registra en logs y nunca se muestra
(FR-018); toda credencial viaja sólo por conexión cifrada y el servidor rechaza las que lleguen sin
cifrar (FR-024, resuelto con redirección forzada a HTTPS más HSTS); el dato de sesión queda fuera
del alcance de los scripts de la página y no acompaña peticiones de otros sitios (FR-023, resuelto
con los atributos `HttpOnly` y `SameSite=Strict`); la contraseña del administrador inicial no puede
quedar versionada en el repositorio (Principio V); un único `docker-compose.yml` tiene que funcionar
igual en Podman (local) y Docker (CI); las dos pantallas cumplen el piso mínimo de accesibilidad de
FR-025 (operables con teclado, etiquetas asociadas, errores anunciados a lectores de pantalla,
contraste suficiente), que además queda como base para los módulos siguientes

**Scale/Scope**: personal de una única empresa — decenas de usuarios, no miles. En este módulo: 2
pantallas (ingreso e inicio), 3 endpoints de autenticación y 5 tablas

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluado contra `.specify/memory/constitution.md` v2.0.0.

| Principio | Estado | Cómo lo cumple este plan |
|---|---|---|
| I. Simplicidad Ante Todo | ✅ Pasa | Cookie de sesión en vez de JWT + *refresh token*; sólo el `PasswordHasher` en vez de ASP.NET Core Identity completo; contador de intentos en memoria en vez de Redis; sin tabla de sesiones. Cada descarte está justificado en `research.md` |
| II. Idioma y Mercado Argentino | ✅ Pasa | Toda la UI y todos los mensajes de error en español rioplatense (voseo), definidos textualmente en `contracts/`. Este módulo no maneja montos, así que el formato ARS no aplica todavía |
| III. Cero Alcance Fantasma | ✅ Pasa | El esquema incluye sólo los campos que exige la spec del Módulo 1. `Email`, `FechaAlta` y `PersonaId` son del Módulo 2 y los agregará su propia migración, aunque ya se sepa que van a existir |
| IV. Verificable por una Persona No Técnica | ⚠️ Parcial | 4 de las 5 historias se validan operando la app (`quickstart.md`). La User Story 4 (cuentas `inactiva` y `bloqueada`) no se puede validar sin tocar la base hasta que exista el Módulo 2 — ver más abajo |
| V. Datos del Usuario con Respeto | ✅ Pasa | La contraseña del administrador inicial se lee de la variable de entorno `GT_ADMIN_PASSWORD_INICIAL`, obligatoria mientras el usuario `admin` no exista y descartable después; cookie `HttpOnly` + `Secure` + `SameSite=Strict`; la contraseña se excluye explícitamente de todo log y de toda respuesta |

**Sobre el Principio IV**: FR-019 prohíbe expresamente sembrar cuentas de ejemplo, y el Módulo 1 no
tiene ninguna pantalla para cambiar el estado de una cuenta. La consecuencia es que los escenarios
de la User Story 4 sólo pueden comprobarse cargando datos a mano hasta que el Módulo 2 exista; ya
está registrado en las *Assumptions* de la spec. Este plan lo mitiga cubriendo esos escenarios con
tests de integración automatizados y documentando en `quickstart.md` el paso manual mínimo, pero no
lo resuelve: revertirlo exigiría cambiar FR-019, que es una decisión de producto ya tomada.

**Estructura de carpetas**: respeta el esquema fijo de la constitución (`/frontend/src/modules/`,
`/backend/src/GT.*`, `/backend/tests/GT.*Tests`, `/specs/`). No se crean carpetas fuera de ese
esquema.

### Reevaluación post-diseño (después de Fase 1)

Revisado el diseño ya completo (`data-model.md`, `contracts/`, `quickstart.md`), el resultado no
cambia: los principios I, II, III y V siguen pasando y el IV sigue parcial por la misma razón
—FR-019 prohíbe sembrar cuentas de ejemplo— sin que hayan aparecido violaciones nuevas.

Dos cosas que el diseño confirmó:

- El modelo de datos quedó en 5 tablas y ningún campo de más: `Email`, `FechaAlta` y `PersonaId`
  quedaron deliberadamente afuera pese a que ya se sabe que el Módulo 2 los va a necesitar.
- La cookie de sesión evitó tanto la tabla de sesiones como el manejo de tokens en el frontend, así
  que el diseño terminó con menos piezas que las que habría tenido la alternativa habitual.

## Project Structure

### Documentation (this feature)

```text
specs/001-autenticacion-usuarios/
├── plan.md              # Este archivo
├── research.md          # Decisiones técnicas y alternativas descartadas
├── data-model.md        # Tablas, campos, reglas y datos iniciales
├── quickstart.md        # Cómo levantar y validar el módulo
├── contracts/
│   ├── README.md        # Contrato de UI: pantallas, mensajes y menú
│   └── auth-api.yaml    # Contrato HTTP (OpenAPI 3.0)
├── checklists/
│   └── requirements.md  # Checklist de calidad de la spec
└── tasks.md             # Lo genera /speckit-tasks, no este comando
```

### Source Code (repository root)

```text
backend/
├── GT.sln
├── src/
│   ├── GT.Api/                        # Endpoints, middleware, configuración
│   │   ├── Autenticacion/             # Endpoints de login, logout y sesión
│   │   ├── Autorizacion/              # Políticas y handler de permisos
│   │   └── Program.cs
│   ├── GT.Application/
│   │   └── Autenticacion/             # Casos de uso, DTOs y mensajes en es-AR
│   ├── GT.Domain/
│   │   ├── Usuarios/                  # Usuario, EstadoUsuario, Rol, Permiso
│   │   └── Autenticacion/             # Reglas puras (normalización, vigencia)
│   └── GT.Infrastructure/
│       ├── Persistencia/              # DbContext, configuraciones, migraciones
│       ├── Seguridad/                 # Hasheo y contador de intentos fallidos
│       └── DatosIniciales/            # Siembra de roles, permisos y admin
└── tests/
    ├── GT.UnitTests/
    └── GT.IntegrationTests/

frontend/
├── package.json
└── src/
    ├── modules/
    │   └── autenticacion/             # Pantalla de ingreso, pantalla de inicio,
    │       ├── componentes/           # guard de rutas, cliente de sesión
    │       ├── paginas/
    │       └── servicios/
    ├── compartido/                    # Layout, menú, cliente HTTP
    └── App.tsx

docker-compose.yml                     # SQL Server + backend + frontend
```

**Structure Decision**: aplicación web con backend y frontend separados, tal como fija la
constitución. El backend queda dividido en las cuatro capas obligatorias
(`GT.Api` / `GT.Application` / `GT.Domain` / `GT.Infrastructure`), con `GT.Application/Autenticacion`
como carpeta espejo del módulo. El frontend se organiza por módulo de negocio
(`frontend/src/modules/autenticacion/`), nunca por tipo de archivo; `frontend/src/compartido/`
guarda el layout y el menú, que son transversales a todos los módulos y no pertenecen a ninguno.

Como el repositorio está vacío, este módulo crea esa estructura por primera vez. Ese trabajo de
montaje no es alcance extra: sin él no existe nada donde correr la pantalla de inicio de sesión.

## Complexity Tracking

> Sin violaciones que justificar. El plan no introduce ninguna complejidad por encima de lo que
> exige la spec; las decisiones técnicas relevantes eligen deliberadamente la opción más simple y
> quedan documentadas con sus alternativas descartadas en [research.md](./research.md).
