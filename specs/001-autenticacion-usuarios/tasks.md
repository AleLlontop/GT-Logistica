# Tasks: Autenticación de usuarios (Módulo 1)

**Input**: Documentos de diseño en `/specs/001-autenticacion-usuarios/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: SÍ se incluyen. No es una preferencia de estilo: el quickstart designa a los tests de
integración como la **única** forma de verificar la User Story 4 completa y varios escenarios de las
User Stories 2 y 3, porque hasta que exista el Módulo 2 no hay pantalla para crear cuentas en otros
estados. Los nombres de esos tests ya están comprometidos en `quickstart.md` y se respetan acá.

**Organization**: las tareas se agrupan por historia de usuario para que cada una se pueda
implementar y validar por separado.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia de usuario pertenece (US1…US5)
- Cada tarea indica la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados, según la estructura fija de la constitución:
`backend/src/GT.*`, `backend/tests/GT.*Tests`, `frontend/src/modules/<modulo>/`.

**Nota sobre el repositorio**: hoy está vacío. La Fase 1 lo crea por primera vez; ese montaje no es
alcance extra, es la condición para que exista una pantalla de ingreso donde correr nada.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dejar el repositorio en condiciones de compilar y levantar.

- [X] T001 Crear la solución `backend/GT.sln` con los cuatro proyectos en capas `GT.Api`, `GT.Application`, `GT.Domain` y `GT.Infrastructure` bajo `backend/src/`, con las referencias entre capas (Api → Application → Domain, Infrastructure → Domain)
- [X] T002 [P] Crear los proyectos de test `backend/tests/GT.UnitTests/` y `backend/tests/GT.IntegrationTests/` con xUnit y agregarlos a `backend/GT.sln`
- [X] T003 [P] Inicializar el frontend con Vite + React + TypeScript en `frontend/`, con Vitest y React Testing Library configurados para `npm test`
- [X] T004 [P] Crear `docker-compose.yml` en la raíz con los servicios de SQL Server, backend y frontend, usando sintaxis estándar sin extensiones propias de Docker ni Podman (research §9)
- [X] T005 [P] Crear `.gitignore` (patrones de .NET y Node: `bin/`, `obj/`, `node_modules/`, `dist/`, `.env*`) y `.dockerignore` en la raíz del repositorio
- [X] T006 [P] Crear `.env.ejemplo` en la raíz con `GT_ADMIN_PASSWORD_INICIAL=` vacío y un comentario que explique que es obligatoria en la primera instalación (research §6)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: infraestructura que TODAS las historias necesitan. Sin esto no se puede empezar ninguna.

**⚠️ CRÍTICO**: ninguna historia de usuario puede arrancar hasta terminar esta fase.

### Dominio y persistencia

- [X] T007 [P] Crear las entidades de dominio `Usuario`, `EstadoUsuario`, `Rol` y `Permiso` en `backend/src/GT.Domain/Usuarios/`, con los campos exactos de `data-model.md` (incluido `PasswordTemporalGeneradaEn` nullable, sin `Email`, `FechaAlta` ni `PersonaId`, que son del Módulo 2)
- [X] T008 Crear `GtDbContext` y las configuraciones de EF Core en `backend/src/GT.Infrastructure/Persistencia/`, con los índices únicos de `UsuarioNormalizado`, `Rol.Codigo` y `Permiso.Codigo`, y las claves compuestas de `UsuarioRol` y `RolPermiso` (depende de T007)
- [X] T009 Generar la migración inicial de EF Core en `backend/src/GT.Infrastructure/Persistencia/Migraciones/` con las 5 tablas del modelo de datos (depende de T008)

### Seguridad base

- [X] T010 [P] Implementar `HasheadorPassword` en `backend/src/GT.Infrastructure/Seguridad/`, envolviendo `PasswordHasher<Usuario>` y exponiendo hasheo y verificación (FR-002, research §2)
- [X] T011 Implementar la siembra de datos iniciales en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`: los 4 roles, el permiso `usuarios.gestionar`, su asignación al rol *Administrador del sistema* y el usuario `admin`. La siembra debe ser idempotente y exigir `GT_ADMIN_PASSWORD_INICIAL` **sólo cuando el usuario `admin` no existe**, deteniendo el arranque con un mensaje explícito si falta (FR-019, research §6; depende de T009, T010)
- [X] T012 Configurar la autenticación por cookie en `backend/src/GT.Api/Program.cs`: cookie no persistente, `ExpireTimeSpan` de 8 horas leído de configuración, `SlidingExpiration`, sin tope absoluto, y los atributos `HttpOnly` + `Secure` + `SameSite=Strict` (FR-010, FR-022, FR-023, research §1)
- [X] T013 Implementar la revalidación por petición en el evento `OnValidatePrincipal` en `backend/src/GT.Api/Autenticacion/`: recarga el usuario desde la base, rechaza la sesión si dejó de estar `activo` y recalcula los permisos efectivos como unión de los permisos de sus roles vigentes (FR-006, FR-009; depende de T012)
- [X] T014 Configurar redirección forzada a HTTPS y HSTS en `backend/src/GT.Api/Program.cs`, de modo que ninguna credencial se acepte por una conexión sin cifrar (FR-024)

### Contrato de errores y arranque

- [X] T015 [P] Crear el tipo `ErrorResponse` y el catálogo de mensajes en español rioplatense en `backend/src/GT.Application/Autenticacion/Mensajes.cs`, con los seis códigos del contrato (`datos_incompletos`, `credenciales_invalidas`, `cuenta_no_habilitada`, `demasiados_intentos`, `sesion_expirada`, `sin_permiso`) y los textos exactos de `contracts/README.md` (FR-015)
- [X] T016 Configurar el manejo global de errores en `backend/src/GT.Api/Program.cs` para que toda respuesta de error use `ErrorResponse` y ninguna exponga detalles técnicos ni la contraseña (FR-015, FR-018; depende de T015)

### Esqueleto del frontend

- [X] T017 [P] Crear el cliente HTTP en `frontend/src/compartido/clienteHttp.ts`, que envía siempre las credenciales del navegador y nunca guarda nada en `localStorage` ni `sessionStorage` (contracts §Reglas transversales)
- [X] T018 [P] Crear el layout base y el componente de menú en `frontend/src/compartido/Layout.tsx` y `frontend/src/compartido/Menu.tsx`, dibujando únicamente las opciones que llegan del servidor, sin lógica propia de permisos (FR-020, research §8)

**Checkpoint**: la base está lista — se puede empezar con las historias de usuario.

---

## Phase 3: User Story 1 - Iniciar sesión con credenciales válidas (Priority: P1) 🎯 MVP

**Goal**: un usuario `activo` con credenciales correctas entra al sistema y llega a la pantalla de
inicio, que muestra su usuario, sus roles y el menú con las opciones que sus roles autorizan.

**Independent Test**: ingresar con `admin` y la contraseña definida en `.env`, y comprobar que se
llega a la pantalla de inicio con el rol *Administrador del sistema* y la opción *Gestión de
usuarios* en el menú. Ingresar de nuevo escribiendo `  ADMIN  ` y comprobar que entra igual.

### Tests for User Story 1

- [X] T019 [P] [US1] Test unitario de normalización de username (recorte de espacios, sin distinguir mayúsculas) en `backend/tests/GT.UnitTests/Autenticacion/NormalizadorUsernameTests.cs` (FR-012)
- [X] T020 [P] [US1] Test de integración `ActualizaUltimoAcceso_TrasIngresoExitoso` en `backend/tests/GT.IntegrationTests/Autenticacion/LoginTests.cs`, comprometido en `quickstart.md` como única verificación de FR-005

### Implementation for User Story 1

- [X] T021 [P] [US1] Implementar `NormalizadorUsername` en `backend/src/GT.Domain/Autenticacion/NormalizadorUsername.cs` (FR-012)
- [X] T022 [US1] Implementar el caso de uso de autenticación en `backend/src/GT.Application/Autenticacion/AutenticarUsuario.cs`: normaliza el username, busca por `UsernameNormalizado`, verifica la contraseña y actualiza `UltimoAcceso` en el ingreso exitoso, siguiendo el orden de validaciones de `data-model.md` (FR-005, FR-012; depende de T021)
- [X] T023 [US1] Implementar el cálculo de opciones de menú autorizadas en `backend/src/GT.Application/Autenticacion/OpcionesMenu.cs`, mapeando permisos efectivos a entradas de menú y devolviendo lista vacía si ninguna aplica (FR-020, research §8)
- [X] T024 [US1] Implementar el endpoint `POST /api/auth/login` en `backend/src/GT.Api/Autenticacion/AutenticacionEndpoints.cs` para el caso exitoso, emitiendo la cookie de sesión y devolviendo `SesionResponse` según `contracts/auth-api.yaml` (depende de T022, T023)
- [X] T025 [US1] Implementar el endpoint `GET /api/auth/sesion` en `backend/src/GT.Api/Autenticacion/AutenticacionEndpoints.cs`, recalculando roles y menú en cada llamada (FR-006; depende de T023)
- [X] T026 [P] [US1] Crear el servicio de sesión del frontend en `frontend/src/modules/autenticacion/servicios/sesion.ts`, con las llamadas a login y a consulta de sesión (depende de T017)
- [X] T027 [US1] Crear la pantalla de ingreso en `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx`: dos campos obligatorios, marcado en pantalla sin llamar al servidor si alguno está vacío, y botón deshabilitado mientras la petición está en curso (FR-011, contracts §Pantallas)
- [X] T028 [US1] Crear la pantalla de inicio en `frontend/src/modules/autenticacion/paginas/PantallaInicio.tsx`, mostrando username, roles, menú y botón de cerrar sesión (FR-020; depende de T018)
- [X] T029 [US1] Aplicar el piso mínimo de accesibilidad en `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx` y `frontend/src/modules/autenticacion/paginas/PantallaInicio.tsx`: operables sólo con teclado, foco visible y en orden visual, foco inicial en el campo de usuario, etiquetas visibles asociadas a cada campo, mensajes de error anunciados a lectores de pantalla y contraste suficiente (FR-025, SC-008; depende de T027, T028)
- [X] T030 [US1] Definir las rutas de la aplicación en `frontend/src/App.tsx` (`/ingresar` y `/`) y cargar la sesión al arrancar para decidir a cuál llevar (depende de T026, T027, T028)

**Checkpoint**: User Story 1 funciona de punta a punta y se puede validar sola.

---

## Phase 4: User Story 2 - Proteger funcionalidades sin sesión activa o sin permisos (Priority: P1)

**Goal**: toda funcionalidad salvo la pantalla de ingreso exige sesión activa y verifica en el
servidor que los roles vigentes autoricen la operación, sin importar lo que muestre el menú.

**Independent Test**: abrir por URL directa una funcionalidad (a) sin sesión y (b) con sesión pero
sin el rol requerido, comprobando que el servidor rechaza en ambos casos.

### Tests for User Story 2

- [X] T031 [P] [US2] Test de integración `RechazaOperacionSinPermiso` en `backend/tests/GT.IntegrationTests/Autorizacion/AutorizacionTests.cs` (FR-008)
- [X] T032 [US2] Test de integración `UsaRolesVigentesNoLosDelIngreso` en `backend/tests/GT.IntegrationTests/Autorizacion/AutorizacionTests.cs`: se le quita el rol a un usuario con sesión abierta y la operación siguiente se rechaza (FR-006). Mismo archivo que T031, va después
- [X] T033 [P] [US2] Test de integración `CortaSesionSiLaCuentaSeDesactiva` en `backend/tests/GT.IntegrationTests/Autenticacion/SesionTests.cs` (FR-009)

### Implementation for User Story 2

- [X] T034 [US2] Implementar la autorización por permiso en `backend/src/GT.Api/Autorizacion/PermisoRequirement.cs` y `PermisoHandler.cs`, evaluando permisos y no roles, y devolviendo `401` sin sesión y `403` sin permiso con los códigos del contrato (FR-007, FR-008, research §7; depende de T013)
- [X] T035 [US2] Crear el componente de ruta protegida en `frontend/src/modules/autenticacion/componentes/RutaProtegida.tsx`, que redirige a `/ingresar` cuando no hay sesión y guarda la ruta pedida (FR-007, FR-026)
- [X] T036 [US2] Implementar el retorno a la ruta original tras autenticarse en `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx`, aceptando únicamente rutas internas de la aplicación y cayendo a la pantalla de inicio si el destino no es interno o los roles no lo autorizan (FR-026; depende de T035)
- [X] T037 [US2] Agregar al cliente HTTP el manejo de `401`: descarta el estado de sesión, muestra `Tu sesión expiró. Ingresá de nuevo.` y redirige a `/ingresar` en `frontend/src/compartido/clienteHttp.ts` (FR-015; depende de T017)
- [X] T038 [US2] Proteger todas las rutas de la aplicación salvo `/ingresar` en `frontend/src/App.tsx`, envolviéndolas con el componente de ruta protegida (FR-007; depende de T035)

**Checkpoint**: User Stories 1 y 2 funcionan de forma independiente.

---

## Phase 5: User Story 3 - Recibir un rechazo claro con credenciales inválidas (Priority: P2)

**Goal**: quien se equivoca recibe siempre el mismo mensaje genérico, sin pistas sobre qué dato
falló ni sobre qué cuentas existen, y puede reintentar de inmediato.

**Independent Test**: enviar el formulario con un username inexistente y por separado con un
username válido y contraseña incorrecta, comprobando el mismo mensaje en ambos casos.

### Tests for User Story 3

- [X] T039 [P] [US3] Test unitario de vigencia de contraseña temporal (válida dentro de 24 horas, vencida después) en `backend/tests/GT.UnitTests/Autenticacion/VigenciaPasswordTemporalTests.cs` (FR-017)
- [X] T040 [P] [US3] Test de integración `RechazaPasswordTemporalVencida` en `backend/tests/GT.IntegrationTests/Autenticacion/LoginTests.cs`, comprobando que devuelve el mensaje genérico y no uno específico (FR-017)
- [X] T041 [P] [US3] Test de integración `NoAfectaAOtrasCuentasDelMismoOrigen` en `backend/tests/GT.IntegrationTests/Autenticacion/IntentosFallidosTests.cs`: con una cuenta ya frenada, otra cuenta del mismo origen ingresa sin demora (FR-021, SC-007)
- [X] T042 [US3] Test de integración que comprueba que un username inexistente y una contraseña incorrecta devuelven cuerpos idénticos y tiempos de respuesta del mismo orden, en `backend/tests/GT.IntegrationTests/Autenticacion/LoginTests.cs` (FR-003, research §3). Mismo archivo que T040, va después

### Implementation for User Story 3

- [X] T043 [P] [US3] Implementar `VigenciaPasswordTemporal` en `backend/src/GT.Domain/Autenticacion/VigenciaPasswordTemporal.cs`, que resuelve si una contraseña temporal sigue vigente a partir de `PasswordTemporalGeneradaEn` y las 24 horas (FR-017)
- [X] T044 [US3] Agregar al caso de uso de autenticación el rechazo con mensaje genérico ante username inexistente, contraseña incorrecta o contraseña temporal vencida, incluyendo la verificación contra un hash ficticio cuando el usuario no existe para no delatar por tiempo qué cuentas existen, en `backend/src/GT.Application/Autenticacion/AutenticarUsuario.cs` (FR-003, research §3; depende de T022, T043)
- [X] T045 [US3] Implementar el contador de intentos fallidos en `backend/src/GT.Infrastructure/Seguridad/ContadorIntentosFallidos.cs` sobre `IMemoryCache`, con clave compuesta de **IP de origen + username normalizado**, ventana de 5 minutos, bloqueo de 1 minuto al sexto intento y reinicio del contador ante un ingreso exitoso (FR-021, research §4)
- [X] T046 [US3] Conectar el contador al endpoint de login en `backend/src/GT.Api/Autenticacion/AutenticacionEndpoints.cs`, devolviendo `429` con el código `demasiados_intentos` y la cabecera `Retry-After` (FR-021; depende de T045, T024)
- [X] T047 [US3] Mostrar los mensajes de error sobre el formulario sin borrar lo escrito, en `frontend/src/modules/autenticacion/paginas/PantallaIngreso.tsx` (contracts §Pantallas; depende de T027)

**Checkpoint**: User Stories 1, 2 y 3 funcionan de forma independiente.

---

## Phase 6: User Story 4 - Rechazar el ingreso de una cuenta no habilitada (Priority: P2)

**Goal**: quien tiene la contraseña correcta pero la cuenta `inactiva` o `bloqueada` recibe un
mensaje distinto que le explica que debe contactar al responsable de sistemas.

**Independent Test**: hasta que exista el Módulo 2 esta historia **no se puede validar operando la
app** (FR-019 prohíbe sembrar cuentas de ejemplo y este módulo no cambia estados). Se valida con los
tests de integración de abajo, o cambiando a mano el `Estado` en la base como describe el quickstart.

### Tests for User Story 4

- [X] T048 [P] [US4] Test de integración `RechazaCuentaInactiva` en `backend/tests/GT.IntegrationTests/Autenticacion/EstadoCuentaTests.cs` (FR-004)
- [X] T049 [US4] Test de integración `RechazaCuentaBloqueada` en `backend/tests/GT.IntegrationTests/Autenticacion/EstadoCuentaTests.cs` (FR-004). Mismo archivo que T048, va después
- [X] T050 [US4] Test de integración `CuentaInactivaConPasswordIncorrecta_DevuelveMensajeGenerico` en `backend/tests/GT.IntegrationTests/Autenticacion/EstadoCuentaTests.cs`, que fija el orden de los controles (FR-003, FR-004). Mismo archivo que T048 y T049, va después

### Implementation for User Story 4

- [X] T051 [US4] Agregar al caso de uso de autenticación el control de estado de cuenta en `backend/src/GT.Application/Autenticacion/AutenticarUsuario.cs`, devolviendo `cuenta_no_habilitada` sólo cuando la contraseña era correcta y ubicando este control **después** de verificar la contraseña, para que una contraseña incorrecta sobre una cuenta inactiva siga dando el mensaje genérico (FR-001, FR-004; depende de T044)

**Checkpoint**: las cuatro primeras historias funcionan de forma independiente.

---

## Phase 7: User Story 5 - Cerrar sesión de forma definitiva (Priority: P3)

**Goal**: quien cierra sesión no puede retomar el acceso con el botón "atrás", y cerrar el navegador
también termina la sesión.

**Independent Test**: ingresar, cerrar sesión y comprobar que el botón "atrás" no recupera ninguna
pantalla protegida; después ingresar, cerrar el navegador entero y comprobar que al reabrirlo pide
autenticarse de nuevo.

### Tests for User Story 5

- [X] T052 [P] [US5] Test de integración `CierreDeSesionInvalidaLaCookie` en `backend/tests/GT.IntegrationTests/Autenticacion/SesionTests.cs`: tras cerrar sesión, la cookie anterior ya no autoriza ninguna operación (FR-013)
- [X] T053 [US5] Test de integración `PermiteSesionesSimultaneas` en `backend/tests/GT.IntegrationTests/Autenticacion/SesionTests.cs`: dos sesiones del mismo usuario funcionan a la vez (FR-014). Mismo archivo que T052, va después

### Implementation for User Story 5

- [X] T054 [US5] Implementar el endpoint `POST /api/auth/logout` en `backend/src/GT.Api/Autenticacion/AutenticacionEndpoints.cs`: invalida la sesión, borra la cookie, responde `204` y es idempotente (FR-013)
- [X] T055 [US5] Agregar `Cache-Control: no-store` a las respuestas protegidas y a la de cierre de sesión en `backend/src/GT.Api/Program.cs`, para que el navegador no sirva pantallas protegidas desde su caché con el botón "atrás" (FR-013; depende de T054)
- [X] T056 [US5] Conectar el botón de cerrar sesión del layout al endpoint y limpiar el estado de sesión en memoria en `frontend/src/compartido/Layout.tsx` y `frontend/src/modules/autenticacion/servicios/sesion.ts` (FR-013; depende de T018, T026)
- [X] T057 [P] [US5] Test de frontend que comprueba que tras cerrar sesión no queda ningún rastro de la sesión en memoria ni en el almacenamiento del navegador, en `frontend/src/modules/autenticacion/servicios/sesion.test.ts` (contracts §Reglas transversales)

**Checkpoint**: las cinco historias funcionan de forma independiente.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: cierre y validación de extremo a extremo.

- [X] T058 [P] Verificar por inspección que la contraseña no aparece en ningún log del backend ni en ninguna URL, revisando `backend/src/GT.Api/Program.cs` y la configuración de registro (FR-018, SC-006)
- [ ] T059 [P] Hacer el recorrido de accesibilidad de la sección correspondiente de `specs/001-autenticacion-usuarios/quickstart.md` —ingreso, error, reintento y cierre de sesión sin tocar el mouse— y corregir en `frontend/src/modules/autenticacion/` lo que falle (FR-025, SC-008)
- [X] T060 [P] Documentar en `README.md` de la raíz cómo levantar el sistema por primera vez, incluyendo la variable `GT_ADMIN_PASSWORD_INICIAL` y su carácter obligatorio sólo en la instalación inicial
- [ ] T061 Ejecutar la validación completa de [quickstart.md](./quickstart.md) historia por historia y anotar el resultado de cada criterio de éxito (SC-001 a SC-008)
- [X] T062 Correr `dotnet test` sobre `backend/GT.sln` y `npm test` sobre `frontend/package.json`, y dejar ambas suites en verde
- [X] T063 Revisar el cumplimiento de los cinco principios de `.specify/memory/constitution.md` antes de dar el módulo por terminado, prestando atención al Principio III: ningún endpoint, campo ni pantalla que no esté en la spec

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias, arranca de inmediato
- **Foundational (Fase 2)**: depende de la Fase 1 — BLOQUEA todas las historias
- **User Stories (Fases 3 a 7)**: todas dependen de la Fase 2
- **Polish (Fase 8)**: depende de las historias que se quieran entregar

### User Story Dependencies

- **US1 (P1)**: arranca apenas termina la Fase 2. Sin dependencias de otras historias
- **US2 (P1)**: arranca apenas termina la Fase 2. Es independiente, aunque en la práctica se prueba
  más cómodo con US1 ya andando
- **US3 (P2)**: extiende el caso de uso de autenticación que crea US1 (T044 depende de T022). No es
  independiente en el código, sí en la validación
- **US4 (P2)**: extiende el mismo caso de uso (T051 depende de T044, de US3). Es la única historia
  con una dependencia real de código sobre otra, y viene del orden de validaciones que fija la spec:
  el control de estado tiene que ir después del de contraseña
- **US5 (P3)**: arranca apenas termina la Fase 2. Independiente

### Within Each User Story

- Los tests se escriben primero y tienen que fallar antes de implementar
- Dominio antes que aplicación, aplicación antes que endpoints
- Backend antes que la pantalla que lo consume

### Parallel Opportunities

- Fase 1: T002 a T006 en paralelo, después de T001
- Fase 2: T007, T010, T015, T017 y T018 en paralelo; T008 → T009 → T011 en cadena
- Todos los tests de una misma historia marcados [P] van juntos
- Con equipo, tras la Fase 2 se pueden encarar US1, US2 y US5 en paralelo; US3 y US4 comparten
  archivo con US1 y van en orden

---

## Parallel Example: User Story 1

```bash
# Los dos tests de la historia, juntos:
Task: "Test unitario de normalización en backend/tests/GT.UnitTests/Autenticacion/NormalizadorUsernameTests.cs"
Task: "Test ActualizaUltimoAcceso_TrasIngresoExitoso en backend/tests/GT.IntegrationTests/Autenticacion/LoginTests.cs"

# Backend y frontend avanzan en paralelo una vez definido el contrato:
Task: "Implementar NormalizadorUsername en backend/src/GT.Domain/Autenticacion/NormalizadorUsername.cs"
Task: "Crear servicio de sesión en frontend/src/modules/autenticacion/servicios/sesion.ts"
```

---

## Implementation Strategy

### MVP primero (sólo User Story 1)

1. Fase 1: Setup
2. Fase 2: Foundational (crítica, bloquea todo)
3. Fase 3: User Story 1
4. **PARAR Y VALIDAR**: ingresar con `admin`, ver la pantalla de inicio con el menú correcto
5. En ese punto el sistema ya es demostrable

### Entrega incremental

1. Setup + Foundational → base lista
2. + US1 → validar → **MVP demostrable**
3. + US2 → validar → el sistema ya es seguro para agregarle módulos
4. + US3 → validar → los errores de ingreso son claros
5. + US4 → validar (con tests; la validación manual completa espera al Módulo 2)
6. + US5 → validar → ciclo de sesión cerrado

**Corte recomendado para dar el módulo por entregable**: hasta US2 inclusive. US1 sola es
demostrable, pero sin US2 el sistema no es seguro para que el Módulo 2 se apoye encima.

---

## Notes

- Las tareas [P] tocan archivos distintos y no dependen de nada pendiente
- Marcar cada tarea como `[X]` al terminarla
- Commit por tarea o por grupo lógico
- Se puede parar en cualquier checkpoint para validar una historia por separado
- **Deuda conocida y aceptada**: la User Story 4 no es validable por una persona no técnica hasta
  que exista el Módulo 2. Está anotado en las *Assumptions* de la spec y en el chequeo constitucional
  del plan; no es un olvido de este desglose
