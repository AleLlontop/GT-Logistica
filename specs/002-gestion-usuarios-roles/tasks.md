# Tasks: Gestionar usuarios y roles (Módulo 2)

**Input**: Documentos de diseño en `/specs/002-gestion-usuarios-roles/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: SÍ se incluyen, por la misma razón que en el Módulo 1: el `quickstart.md` designa a los
tests de integración como la única forma práctica de verificar tres escenarios —la unicidad bajo
concurrencia, las tres variantes de la protección del último administrador y el corte de sesiones al
restablecer una contraseña— que a mano son imposibles de reproducir de forma confiable. Los tests
unitarios cubren las tres reglas puras del módulo.

**Organization**: las tareas se agrupan por historia de usuario para que cada una se pueda
implementar y validar por separado.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: se puede hacer en paralelo (archivos distintos, sin dependencias pendientes)
- **[Story]**: a qué historia de usuario pertenece (US1…US7)
- Cada tarea indica la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados, según la estructura fija de la constitución:
`backend/src/GT.*`, `backend/tests/GT.*Tests`, `frontend/src/modules/<modulo>/`.

**Nota sobre el repositorio**: el Módulo 1 ya está implementado y funcionando. Este módulo **extiende**
lo existente; casi ninguna tarea parte de cero. Cuando una tarea dice MODIFICAR, hay que leer el
archivo actual antes de tocarlo.

**El padrón de personas vive dentro del módulo `usuarios`** (`GT.Application/Usuarios/Personas/`,
`frontend/src/modules/usuarios/personas/`), no como módulo hermano — ver *Structure Decision* en
`plan.md`.

**Una advertencia sobre el Módulo 1**: T050 modifica `RevalidadorSesion.cs`, que es código del
Módulo 1 en producción. Es el único archivo suyo con cambio de comportamiento en toda esta feature.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: dejar disponible la única dependencia y la única configuración nuevas del módulo.

- [X] T001 Agregar el paquete MailKit a `backend/src/GT.Infrastructure/GT.Infrastructure.csproj` (research §1)
- [X] T002 [P] Agregar al final de `.env.template` las cinco variables opcionales de correo (`GT_CORREO_HOST`, `GT_CORREO_PUERTO`, `GT_CORREO_USUARIO`, `GT_CORREO_PASSWORD`, `GT_CORREO_REMITENTE`) con el comentario que explica que dejarlas vacías hace que el envío se registre en el log en vez de enviarse
- [X] T003 [P] Pasar las cinco variables de correo al servicio `backend` en `docker-compose.yml`, mapeadas a la configuración `Correo:*`, usando sólo sintaxis estándar de Compose

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: el esquema de base de datos, las reglas puras, los textos compartidos y la **consulta de
personas** que varias historias necesitan.

**⚠️ CRÍTICO**: ninguna historia de usuario puede arrancar hasta terminar esta fase. La migración
(T013) es el cuello de botella: sin ella no hay dónde guardar nada de lo que sigue.

### Dominio y reglas puras

- [X] T004 [P] Crear `Persona` y `TipoIntegrante` en `backend/src/GT.Domain/Personas/`, con los nueve campos de `data-model.md` (nombre, apellido, dni, tipo, telefono, email, fechaNacimiento, activa) y ni uno más (FR-026)
- [X] T005 [P] MODIFICAR `backend/src/GT.Domain/Usuarios/Usuario.cs` agregando `Email`, `EmailNormalizado`, `FechaAlta`, `PersonaId` (nullable) y `PasswordActualizadaEn`, y actualizar el comentario de cabecera que hoy dice que esos campos son del Módulo 2
- [X] T006 [P] Crear `NormalizadorEmail` en `backend/src/GT.Domain/Usuarios/`, con recorte de espacios y pasaje a minúsculas, siguiendo el mismo patrón que `NormalizadorUsername` del Módulo 1 (FR-020)
- [X] T007 [P] Crear `ProteccionUltimoAdministrador` en `backend/src/GT.Domain/Usuarios/` como función pura que recibe la cantidad de administradores activos **excluyendo al usuario afectado** y la operación intentada, y decide si se permite (FR-019, research §8)
- [X] T008 [P] Escribir los tests unitarios de `NormalizadorEmail` en `backend/tests/GT.UnitTests/Usuarios/NormalizadorEmailTests.cs`, cubriendo espacios al costado, mayúsculas mezcladas y email ya normalizado
- [X] T009 [P] Escribir los tests unitarios de `ProteccionUltimoAdministrador` en `backend/tests/GT.UnitTests/Usuarios/ProteccionUltimoAdministradorTests.cs`, cubriendo las tres operaciones (cambio de estado, quita de rol, baja) con cero y con uno o más administradores restantes

### Persistencia y migración

- [X] T010 [P] Crear `PersonaConfiguracion` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/`, con el índice único de `Dni` y los largos de columna de `data-model.md` (depende de T004)
- [X] T011 MODIFICAR `backend/src/GT.Infrastructure/Persistencia/Configuraciones/UsuarioConfiguracion.cs` agregando el índice único de `EmailNormalizado`, el índice único **filtrado** de `PersonaId` (`WHERE PersonaId IS NOT NULL`) y la clave foránea a `Personas` (depende de T005, T010)
- [X] T012 MODIFICAR `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs` agregando `DbSet<Persona>` (depende de T004)
- [X] T013 Generar la migración de EF Core en `backend/src/GT.Infrastructure/Persistencia/Migraciones/` siguiendo los cinco pasos de `data-model.md`: crear `Personas` **sin sembrar ninguna fila** (FR-024), agregar las columnas nullables a `Usuarios`, **rellenar la fila del usuario `admin`** con `admin@gtlogistica.local`, `FechaAlta` y `PasswordActualizadaEn`, volver obligatorias las columnas, y recién entonces crear índices y clave foránea (research §5; depende de T010, T011, T012)

### Contratos compartidos

- [X] T014 [P] Crear `backend/src/GT.Application/Usuarios/Mensajes.cs` con los once códigos de error y los textos en español rioplatense **exactamente** como los fija `contracts/README.md`, siguiendo el patrón de `Mensajes.cs` del Módulo 1 y sin duplicar los códigos que ese módulo ya define
- [X] T015 [P] Crear `backend/src/GT.Application/Usuarios/Dtos.cs` con los esquemas de `contracts/usuarios-api.yaml` (`UsuarioListado`, `UsuarioDetalle`, `CrearUsuarioRequest`, `ModificarUsuarioRequest`, `RolConPermisos`), sin ningún campo de contraseña en las respuestas
- [X] T016 [P] MODIFICAR `frontend/src/compartido/tipos.ts` agregando los códigos de error nuevos al tipo `CodigoError` y los tipos de usuario, rol y persona que devuelven los endpoints
- [X] T017 Crear el andamiaje de tests de integración en `backend/tests/GT.IntegrationTests/Usuarios/`: helpers para autenticarse como `admin`, crear usuarios y personas de prueba, y limpiar entre tests, reutilizando `AplicacionDePrueba` del Módulo 1 (depende de T013)

### Consulta de personas (la usan US1, US3 y US6)

> Va en la fase base a propósito: sin estos cuatro pasos, el selector de persona de US1 llamaría a un
> endpoint inexistente y mostraría un error en vez de la leyenda de padrón vacío. La escritura del
> padrón queda para US6.

- [X] T018 [P] Crear `backend/src/GT.Application/Usuarios/Personas/Dtos.cs` con `Persona` y `PersonaRequest` según `contracts/usuarios-api.yaml`
- [X] T019 Crear el caso de uso `ConsultarPersonas` en `backend/src/GT.Application/Usuarios/Personas/ConsultarPersonas.cs`, con búsqueda parcial por nombre, apellido y DNI, y el filtro `soloActivas` que usa el selector del formulario de usuario (FR-023, FR-025; depende de T018)
- [X] T020 Crear `backend/src/GT.Api/Usuarios/Personas/PersonasEndpoints.cs` con **sólo** `GET /api/personas` y `GET /api/personas/{id}`, protegidos con el permiso `usuarios.gestionar`, y registrar el grupo en `backend/src/GT.Api/Program.cs` (depende de T019)
- [X] T021 [P] Crear `frontend/src/modules/usuarios/personas/servicios/personas.ts` con las llamadas de lectura al backend, usando el `clienteHttp` del Módulo 1 tal cual

**Checkpoint**: base lista — las historias de usuario pueden empezar.

---

## Phase 3: User Story 1 - Crear un usuario nuevo con al menos un rol (Priority: P1) 🎯 MVP

**Goal**: el responsable de sistemas puede dar de alta una cuenta con sus roles, y esa cuenta puede
autenticarse de inmediato.

**Independent Test**: completar el formulario con datos válidos y un rol marcado, guardar, y
comprobar que el usuario aparece en el listado con estado `activo`, el rol elegido y `fechaAlta` de
hoy. El selector de persona muestra su estado vacío hasta que exista US6: eso es correcto, porque el
padrón arranca vacío en toda instalación nueva (FR-024).

### Tests para User Story 1 ⚠️

- [X] T022 [P] [US1] Test de integración de unicidad bajo concurrencia en `backend/tests/GT.IntegrationTests/Usuarios/UnicidadConcurrenteTests.cs`: dos altas simultáneas del mismo username; la segunda recibe el error de duplicado y no una excepción técnica (caso límite de la spec, research §3)
- [X] T023 [P] [US1] Test de integración de las validaciones de alta en `backend/tests/GT.IntegrationTests/Usuarios/CrearUsuarioTests.cs`: username duplicado, email duplicado, contraseña de menos de 8, sin ningún rol, y persona ya vinculada

### Implementación para User Story 1

- [X] T024 [US1] Crear el caso de uso `CrearUsuario` en `backend/src/GT.Application/Usuarios/CrearUsuario.cs`: normaliza username y email, valida unicidad, exige al menos un rol, hashea la contraseña con `IHasheadorPassword`, fija `FechaAlta` y `PasswordActualizadaEn`, y deja `UltimoAcceso` en `null` (FR-001 a FR-005, FR-020)
- [X] T025 [US1] Capturar en `CrearUsuario` la `DbUpdateException` por violación de índice único y traducirla al código de error de duplicado que corresponda según el índice, para cerrar la ventana entre validación e inserción (research §3; depende de T024)
- [X] T026 [US1] Validar en `CrearUsuario` que la persona elegida exista, esté activa y no esté vinculada a ningún otro usuario, cualquiera sea el estado de ese usuario (FR-008, FR-023; depende de T024)
- [X] T027 [US1] Crear `backend/src/GT.Api/Usuarios/UsuariosEndpoints.cs` con `POST /api/usuarios`, protegido con el permiso `usuarios.gestionar`, y registrar el grupo en `backend/src/GT.Api/Program.cs` (FR-007; depende de T024)
- [X] T028 [US1] RETIRAR de `backend/src/GT.Api/Program.cs` el endpoint provisional `GET /api/usuarios` que dejó el Módulo 1 como andamio, para que no quede devolviendo un array vacío si US2 se posterga (depende de T027)
- [X] T029 [P] [US1] Crear `frontend/src/modules/usuarios/servicios/usuarios.ts` con las llamadas al backend, usando el `clienteHttp` del Módulo 1 tal cual
- [X] T030 [US1] Crear `frontend/src/modules/usuarios/paginas/FormularioUsuario.tsx` en modo alta, con los campos y el comportamiento que fija `contracts/README.md`: estado precargado en `activo`, contraseña enmascarada, cuatro casillas de rol, y validación en pantalla que marca el campo en rojo sin llamar al servidor (depende de T029)
- [X] T031 [US1] Crear el selector de persona en `frontend/src/modules/usuarios/componentes/`, que consume `GET /api/personas?soloActivas=true` mediante el servicio de T021 y muestra la leyenda de padrón vacío cuando la lista viene vacía (depende de T021, T029)
- [X] T032 [US1] Agregar la ruta `/usuarios/nuevo` en `frontend/src/App.tsx`, dentro de la ruta protegida existente (depende de T030)
- [X] T033 [P] [US1] Escribir los tests de frontend del formulario de alta en `frontend/src/modules/usuarios/paginas/FormularioUsuario.test.tsx`: estado precargado, rechazo sin roles marcados y marcado en rojo de un email inválido

**Checkpoint**: se puede crear un usuario y ese usuario ya puede iniciar sesión.

---

## Phase 4: User Story 2 - Consultar usuarios existentes (Priority: P1)

**Goal**: encontrar cualquier usuario combinando filtros y ver su detalle completo.

**Independent Test**: cargar usuarios de prueba con distintos roles y estados, aplicar combinaciones
de filtros, y comprobar que el listado y el detalle muestran exactamente lo esperado.

### Tests para User Story 2 ⚠️

- [X] T034 [P] [US2] Test de integración de los filtros en `backend/tests/GT.IntegrationTests/Usuarios/ConsultarUsuariosTests.cs`: coincidencia parcial y sin distinguir mayúsculas en username y email, igualdad exacta en rol y estado, y combinación de los cuatro con "y"

### Implementación para User Story 2

- [X] T035 [US2] Crear el caso de uso `ConsultarUsuarios` en `backend/src/GT.Application/Usuarios/ConsultarUsuarios.cs`, filtrando por `UsernameNormalizado` y `EmailNormalizado` con `Contains` sobre el término normalizado, para no depender de la *collation* del servidor (FR-011, research §4)
- [X] T036 [US2] Agregar `GET /api/usuarios` y `GET /api/usuarios/{id}` a `backend/src/GT.Api/Usuarios/UsuariosEndpoints.cs`, devolviendo los campos de `UsuarioListado` y `UsuarioDetalle` sin ningún dato de contraseña (FR-011, FR-013; depende de T035)
- [X] T037 [US2] Crear `frontend/src/modules/usuarios/paginas/ListadoUsuarios.tsx` con las seis columnas de FR-011, el último acceso vacío mostrado como `Nunca ingresó`, el mensaje de "sin resultados" en vez de una tabla vacía (FR-012) y las cuatro acciones por fila
- [X] T038 [US2] Crear los filtros en `frontend/src/modules/usuarios/componentes/`: dos campos de texto (username, email) y dos listas desplegables (rol, estado), combinables (depende de T037)
- [X] T039 [US2] Crear `frontend/src/modules/usuarios/paginas/DetalleUsuario.tsx` según la sección *Detalle de usuario* de `contracts/README.md`: datos de la cuenta, persona asociada o la leyenda `Sin persona asociada`, las cuatro acciones, y **sin contraseña en ninguna forma** (FR-013)
- [X] T040 [US2] Reemplazar en `frontend/src/App.tsx` el placeholder `<h1>Gestión de usuarios</h1>` por el listado real, y agregar la ruta `/usuarios/{id}` para el detalle (depende de T037, T039)
- [X] T041 [P] [US2] Escribir los tests de frontend en `frontend/src/modules/usuarios/paginas/ListadoUsuarios.test.tsx`: mensaje de sin resultados, `Nunca ingresó` y filtrado parcial

**Checkpoint**: US1 y US2 funcionan juntas — se crea un usuario, se lo encuentra y se lo abre.

---

## Phase 5: User Story 3 - Modificar datos y restablecer contraseña (Priority: P2)

**Goal**: mantener los datos al día y devolverle el acceso a quien perdió su contraseña.

**Independent Test**: abrir un usuario existente, cambiar un dato válido y guardar; y por separado
pedir un restablecimiento y comprobar que se confirma el envío sin exponer la contraseña y que la
sesión abierta de ese usuario queda cortada.

**⚠️ Depende de US2**: el botón *Restablecer contraseña* vive en la pantalla de detalle que entrega
US2 (T039). Si US3 se hace antes, hay que montarlo provisoriamente en el listado.

### Tests para User Story 3 ⚠️

- [X] T042 [P] [US3] Test de integración del corte de sesión por estado en `backend/tests/GT.IntegrationTests/Usuarios/CorteDeSesionTests.cs`: un usuario con sesión abierta que pasa a `inactivo` o `bloqueado` queda rechazado en su petición siguiente (FR-016, SC-006). No requiere código nuevo: verifica que el `RevalidadorSesion` del Módulo 1 ya lo cubre (research §7)
- [X] T043 [P] [US3] Test de integración del restablecimiento en `backend/tests/GT.IntegrationTests/Usuarios/RestablecerPasswordTests.cs`: la respuesta nunca incluye la contraseña, se escribe `PasswordTemporalGeneradaEn`, **la sesión abierta de ese usuario queda cortada en su petición siguiente** (FR-032, SC-010), y un envío fallido devuelve `enviado: false` sin revertir el restablecimiento (FR-021, SC-004)

### Implementación para User Story 3

- [X] T044 [P] [US3] Definir `IEnviadorCorreo` en `backend/src/GT.Application/Usuarios/`, con una operación de envío que devuelve si se pudo entregar en vez de lanzar excepción (research §1)
- [X] T045 [P] [US3] Implementar `EnviadorCorreoSmtp` con MailKit en `backend/src/GT.Infrastructure/Correo/`, leyendo host, puerto, usuario, contraseña y remitente de la configuración `Correo:*` (depende de T001, T044)
- [X] T046 [P] [US3] Implementar `EnviadorCorreoRegistrado` en `backend/src/GT.Infrastructure/Correo/`, que escribe destinatario y asunto al log y **nunca la contraseña temporal** (depende de T044)
- [X] T047 [US3] Registrar en `backend/src/GT.Api/Program.cs` la implementación de `IEnviadorCorreo` que corresponda: SMTP cuando hay `Correo:Host` configurado, la que registra al log cuando no (depende de T045, T046)
- [X] T048 [P] [US3] Crear `GeneradorPasswordTemporal` en `backend/src/GT.Infrastructure/Seguridad/`, con `RandomNumberGenerator` sobre un alfabeto de 12 caracteres sin `l`, `1`, `O` ni `0` (research §2)
- [X] T049 [P] [US3] Escribir los tests unitarios de `GeneradorPasswordTemporal` en `backend/tests/GT.UnitTests/Usuarios/GeneradorPasswordTemporalTests.cs`: largo, alfabeto sin ambigüedades y dos llamadas que no devuelven lo mismo
- [X] T050 [US3] MODIFICAR `backend/src/GT.Api/Autenticacion/RevalidadorSesion.cs` (**archivo del Módulo 1**) para rechazar además toda sesión cuyo `IssuedUtc` sea anterior a `PasswordActualizadaEn` del usuario, y volver a correr los tests de integración de autenticación del Módulo 1 después del cambio (FR-032, research §10)
- [X] T051 [US3] Crear el caso de uso `ModificarUsuario` en `backend/src/GT.Application/Usuarios/ModificarUsuario.cs`: valida unicidad de username y email **excluyendo al propio usuario**, valida la persona elegida, y consulta `ProteccionUltimoAdministrador` antes de un cambio de estado que saque al usuario de `activo` (FR-015, FR-019)
- [X] T052 [US3] Crear el caso de uso `RestablecerPassword` en `backend/src/GT.Application/Usuarios/RestablecerPassword.cs`: genera la temporal, la hashea, escribe `PasswordTemporalGeneradaEn` y `PasswordActualizadaEn`, intenta el envío y devuelve si se entregó, sin revertir el restablecimiento si falla (FR-009, FR-021, FR-032; depende de T044, T048)
- [X] T053 [US3] Agregar `PUT /api/usuarios/{id}` y `POST /api/usuarios/{id}/restablecer-password` a `backend/src/GT.Api/Usuarios/UsuariosEndpoints.cs`, este último sin cuerpo de petición (depende de T051, T052)
- [X] T054 [US3] Extender `frontend/src/modules/usuarios/paginas/FormularioUsuario.tsx` al modo edición: datos precargados y **sin ningún campo de contraseña** (FR-014)
- [X] T055 [US3] Agregar el botón *Restablecer contraseña* con confirmación previa en `frontend/src/modules/usuarios/paginas/DetalleUsuario.tsx`, sin campo de contraseña, mostrando el mensaje de envío exitoso —que advierte que la sesión abierta se cerró— o el de fallo de envío (contracts/README.md; depende de T039)
- [X] T056 [US3] Agregar la ruta `/usuarios/{id}/editar` en `frontend/src/App.tsx` (depende de T054)

**Checkpoint**: se puede editar un usuario y devolverle el acceso.

---

## Phase 6: User Story 4 - Asignar roles y consultar permisos (Priority: P2)

**Goal**: ajustar los roles de un usuario después del alta y ver, en modo lectura, qué habilita cada
rol.

**Independent Test**: abrir el panel de roles de un usuario, cambiar la selección, guardar, y
comprobar que los roles quedan exactamente como se dejaron marcados; y por separado abrir un rol para
ver sus permisos agrupados por módulo.

### Tests para User Story 4 ⚠️

- [X] T057 [P] [US4] Test de integración del reemplazo de roles en `backend/tests/GT.IntegrationTests/Usuarios/AsignarRolesTests.cs`: la selección enviada queda exactamente reflejada (ni más ni menos), una lista vacía se rechaza, y quitarle el rol al único administrador activo se rechaza (FR-018, FR-019)

### Implementación para User Story 4

- [X] T058 [P] [US4] Crear el caso de uso `AsignarRoles` en `backend/src/GT.Application/Usuarios/AsignarRoles.cs`, que agrega los marcados y quita los desmarcados en una sola operación, exige al menos uno, y consulta `ProteccionUltimoAdministrador` antes de quitar el rol de administrador (FR-001, FR-018, FR-019)
- [X] T059 [P] [US4] Crear el caso de uso `ConsultarRoles` en `backend/src/GT.Application/Usuarios/ConsultarRoles.cs`, que devuelve los cuatro roles con sus permisos agrupados por la columna `Modulo`, en sólo lectura (FR-010)
- [X] T060 [US4] Crear `backend/src/GT.Api/Usuarios/RolesEndpoints.cs` con `GET /api/roles`, y agregar `PUT /api/usuarios/{id}/roles` a `UsuariosEndpoints.cs`; registrar el grupo en `Program.cs` (depende de T058, T059)
- [X] T061 [US4] Crear `frontend/src/modules/usuarios/paginas/PanelRoles.tsx` con los cuatro roles y los asignados marcados, que rechaza guardar sin ninguno marcado (contracts/README.md)
- [X] T062 [US4] Crear la vista de permisos por rol en `frontend/src/modules/usuarios/componentes/`, agrupados por módulo, **sin casillas ni botones de edición**, y con la leyenda para los roles que todavía no habilitan nada implementado (FR-010; depende de T061)
- [X] T063 [US4] Agregar la ruta `/usuarios/{id}/roles` en `frontend/src/App.tsx` (depende de T061)

**Checkpoint**: los roles de un usuario se ajustan después del alta.

---

## Phase 7: User Story 6 - Gestionar el padrón de personas (Priority: P2)

**Goal**: registrar, corregir y dar de baja choferes y empleados, para que la asociación con usuarios
tenga de dónde elegir.

**Independent Test**: registrar una persona desde su pantalla, comprobar que aparece en el listado,
corregir un dato, darla de baja, y comprobar que deja de ofrecerse al asociar una persona a un
usuario.

**Nota**: la **lectura** del padrón ya está en la Fase 2, porque el selector de persona de US1 la
necesita. Esta historia agrega la escritura y las dos pantallas.

### Tests para User Story 6 ⚠️

- [X] T064 [P] [US6] Test de integración del padrón en `backend/tests/GT.IntegrationTests/Usuarios/PersonasTests.cs`: sobre una base recién migrada el padrón está vacío (FR-024), un DNI duplicado se rechaza (FR-027), la baja de una persona libre funciona, y la baja de una persona vinculada se rechaza aunque el usuario dueño esté `inactivo` (FR-028)

### Implementación para User Story 6

- [X] T065 [P] [US6] Crear el caso de uso `CrearPersona` en `backend/src/GT.Application/Usuarios/Personas/CrearPersona.cs`, validando el DNI único y capturando también la violación del índice único (FR-027)
- [X] T066 [P] [US6] Crear el caso de uso `ModificarPersona` en `backend/src/GT.Application/Usuarios/Personas/ModificarPersona.cs`, con la comparación de DNI **excluyendo a la propia persona**
- [X] T067 [US6] Crear el caso de uso `DarDeBajaPersona` en `backend/src/GT.Application/Usuarios/Personas/DarDeBajaPersona.cs`, que pone `Activa = false` y **rechaza** si la persona está vinculada a un usuario, informando a cuál, sin importar el estado de ese usuario (FR-028)
- [X] T068 [US6] Completar `backend/src/GT.Api/Usuarios/Personas/PersonasEndpoints.cs` con `POST /api/personas`, `PUT /api/personas/{id}` y `DELETE /api/personas/{id}`, sobre el grupo que ya creó T020 (depende de T065, T066, T067)
- [X] T069 [US6] Agregar la entrada *Personas* apuntando a `/personas` en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`, atada al permiso `usuarios.gestionar` que ya existe (no se crea ningún permiso nuevo, research §7)
- [X] T070 [US6] Completar `frontend/src/modules/usuarios/personas/servicios/personas.ts` con las llamadas de escritura (depende de T021)
- [X] T071 [US6] Crear `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.tsx` con las ocho columnas, la búsqueda por texto y los dos mensajes de estado vacío distintos —padrón sin cargar y búsqueda sin coincidencias— (FR-025; depende de T070)
- [X] T072 [US6] Crear `frontend/src/modules/usuarios/personas/paginas/FormularioPersona.tsx` con los siete campos obligatorios de FR-026 y ninguno más (depende de T070)
- [X] T073 [US6] Agregar las rutas `/personas`, `/personas/nueva` y `/personas/{id}/editar` en `frontend/src/App.tsx` (depende de T071, T072)
- [X] T074 [P] [US6] Escribir los tests de frontend del padrón en `frontend/src/modules/usuarios/personas/paginas/ListadoPersonas.test.tsx`: mensaje de padrón vacío y mensaje de búsqueda sin resultados, que son textos distintos

**Checkpoint**: el padrón funciona y el selector de persona de US1 y US3 ya tiene qué ofrecer.

---

## Phase 8: User Story 7 - Cambiar mi propia contraseña (Priority: P2)

**Goal**: que cualquier usuario convierta su contraseña temporal en una propia, sin depender del
responsable de sistemas.

**Independent Test**: ingresar con cualquier cuenta —incluso una sin el rol de administrador—,
cambiar la contraseña, cerrar sesión y comprobar que se entra con la nueva y no con la anterior.

**⚠️ Atención a la autorización**: es la única pantalla del módulo que NO exige el rol *Administrador
del sistema*. Ver research §9 antes de implementar.

### Tests para User Story 7 ⚠️

- [X] T075 [P] [US7] Test de integración del cambio de contraseña en `backend/tests/GT.IntegrationTests/Usuarios/CambiarPasswordPropiaTests.cs`: un usuario **sin** el permiso `usuarios.gestionar` puede cambiar la suya, una contraseña actual incorrecta se rechaza sin cambiar nada, tras el cambio `PasswordTemporalGeneradaEn` queda en `null`, **la sesión que hizo el cambio sigue funcionando y otra sesión del mismo usuario queda cortada** (FR-029 a FR-032, SC-009)

### Implementación para User Story 7

- [X] T076 [US7] Crear el caso de uso `CambiarPasswordPropia` en `backend/src/GT.Application/Usuarios/CambiarPasswordPropia.cs`: verifica la contraseña actual con el `IVerificadorPassword` del Módulo 1, exige 8 caracteres o más en la nueva, la hashea, pone `PasswordTemporalGeneradaEn` en `null` y actualiza `PasswordActualizadaEn` (FR-030, FR-031, FR-032)
- [X] T077 [US7] Crear `backend/src/GT.Api/Usuarios/MiCuentaEndpoints.cs` con `POST /api/mi-cuenta/contrasena`, protegido con `RequireAuthorization()` **sin política de permiso**, tomando el usuario de los *claims* de la sesión y nunca de la petición, y volviendo a emitir la cookie con `SignInAsync` para que la sesión en curso sobreviva al corte de FR-032; registrarlo en `Program.cs` (FR-029, research §9 y §10; depende de T076)
- [X] T078 [US7] Crear `frontend/src/modules/usuarios/paginas/CambiarPassword.tsx` con los tres campos enmascarados y vacíos al abrir, y la comprobación en pantalla de que las dos repeticiones coinciden antes de llamar al servidor
- [X] T079 [US7] MODIFICAR `frontend/src/compartido/Layout.tsx` para agregar el enlace fijo *Cambiar contraseña* junto a *Cerrar sesión*, **fuera** del menú calculado por permisos, visible para todo usuario autenticado (research §9)
- [X] T080 [US7] Agregar la ruta `/mi-cuenta/contrasena` en `frontend/src/App.tsx`, protegida sólo por sesión y no por rol (depende de T078)
- [X] T081 [P] [US7] Escribir los tests de frontend en `frontend/src/modules/usuarios/paginas/CambiarPassword.test.tsx`: campos vacíos al abrir, rechazo cuando las repeticiones no coinciden y rechazo con menos de 8 caracteres

**Checkpoint**: el circuito de restablecimiento queda cerrado de punta a punta.

---

## Phase 9: User Story 5 - Dar de baja un usuario (Priority: P3)

**Goal**: desvincular personal sin borrar su registro histórico.

**Independent Test**: seleccionar un usuario de prueba, confirmar la baja, y comprobar que queda
`inactivo` en el listado y ya no puede iniciar sesión; y por separado cancelar la confirmación y
comprobar que nada cambió.

### Tests para User Story 5 ⚠️

- [X] T082 [P] [US5] Test de integración de la baja en `backend/tests/GT.IntegrationTests/Usuarios/DarDeBajaUsuarioTests.cs`: el usuario queda `inactivo` y su registro sigue existiendo, y dar de baja al único administrador activo se rechaza (FR-006, FR-019, SC-005)

### Implementación para User Story 5

- [X] T083 [US5] Crear el caso de uso `DarDeBajaUsuario` en `backend/src/GT.Application/Usuarios/DarDeBajaUsuario.cs`, que cambia el estado a `inactivo` **sin borrar** y consulta `ProteccionUltimoAdministrador` antes (FR-006, FR-019)
- [X] T084 [US5] Agregar `DELETE /api/usuarios/{id}` a `backend/src/GT.Api/Usuarios/UsuariosEndpoints.cs` (depende de T083)
- [X] T085 [US5] Crear el diálogo de confirmación de baja en `frontend/src/modules/usuarios/componentes/`, con el texto exacto de `contracts/README.md`, que recibe el foco al abrirse, se cierra con `Escape` sin modificar nada y devuelve el foco a la fila de origen (FR-017)
- [X] T086 [US5] Conectar la acción *Dar de baja* del listado y del detalle al diálogo y al endpoint, refrescando con el estado nuevo (depende de T084, T085)

**Checkpoint**: las siete historias funcionan de forma independiente.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: cerrar los detalles que atraviesan varias historias.

- [X] T087 [P] Revisar que ningún endpoint del módulo devuelva `PasswordHash` ni contraseña en texto plano, en ninguna respuesta ni en ningún log (FR-004, FR-013, SC-004)
- [X] T088 [P] Verificar el piso de accesibilidad en las siete pantallas nuevas: operables con teclado, etiquetas asociadas a cada campo, errores anunciados a lectores de pantalla, encabezados de columna reales en las dos tablas y foco correcto en los diálogos de confirmación (contracts/README.md)
- [X] T089 [P] Verificar que los textos visibles coincidan **exactamente** con la tabla de `contracts/README.md`, en español rioplatense con voseo (Principio II)
- [X] T090 Correr `cd backend && dotnet test` y dejar en verde las suites unitaria y de integración, **incluidos los tests de autenticación del Módulo 1**, que T050 puede haber afectado
- [X] T091 Correr `cd frontend && npm test` y `npm run lint`, y dejar ambos en verde
- [X] T092 Recorrer completo el `quickstart.md`, los doce pasos, sobre una base levantada desde cero con `podman compose up -d`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias, se puede empezar ya
- **Foundational (Fase 2)**: depende de Setup — BLOQUEA todas las historias. La migración T013 es el cuello de botella real
- **Historias (Fases 3 a 9)**: todas dependen de la Fase 2 completa
- **Polish (Fase 10)**: depende de las historias que se quieran entregar

### User Story Dependencies

- **US1 (P1)**: arranca apenas termina la Fase 2. Sin dependencias de otras historias — el selector de persona funciona contra la lectura de padrón que entrega T020
- **US2 (P1)**: arranca apenas termina la Fase 2. Independiente de US1, aunque cargar usuarios desde US1 hace la validación más cómoda que insertarlos a mano
- **US3 (P2)**: **depende de US2** para la pantalla de detalle, donde vive el botón de restablecer (T055 → T039). Es la única dependencia real entre historias del módulo
- **US4 (P2)**: independiente
- **US6 (P2)**: independiente. Agrega la escritura del padrón; la lectura ya está en la Fase 2
- **US7 (P2)**: independiente. Cierra el circuito que abre US3, pero se puede construir y probar antes
- **US5 (P3)**: independiente

### Within Each User Story

- Los tests se escriben primero y tienen que fallar antes de implementar
- Dominio antes que aplicación, aplicación antes que endpoints, endpoints antes que frontend
- La historia se termina antes de pasar a la siguiente prioridad

### Parallel Opportunities

- Fase 1: T002 y T003 en paralelo
- Fase 2: las seis tareas de dominio y reglas puras (T004 a T009) en paralelo; después T010 y T012 en paralelo; T014, T015, T016, T018 y T021 en paralelo
- Fase 3 en adelante: todas las tareas marcadas [P] dentro de una misma historia
- Con equipo: cerrada la Fase 2, seis de las siete historias se pueden repartir en paralelo; US3 espera a que US2 tenga el detalle

---

## Parallel Example: Fase 2 (Foundational)

```bash
# Dominio y reglas puras, todo junto:
Tarea: "Crear Persona y TipoIntegrante en backend/src/GT.Domain/Personas/"
Tarea: "Modificar Usuario.cs agregando Email, EmailNormalizado, FechaAlta, PersonaId y PasswordActualizadaEn"
Tarea: "Crear NormalizadorEmail en backend/src/GT.Domain/Usuarios/"
Tarea: "Crear ProteccionUltimoAdministrador en backend/src/GT.Domain/Usuarios/"
Tarea: "Tests unitarios de NormalizadorEmail"
Tarea: "Tests unitarios de ProteccionUltimoAdministrador"
```

## Parallel Example: User Story 3

```bash
# Las tres piezas de correo y el generador, en paralelo:
Tarea: "Definir IEnviadorCorreo en backend/src/GT.Application/Usuarios/"
Tarea: "Implementar EnviadorCorreoSmtp con MailKit"
Tarea: "Implementar EnviadorCorreoRegistrado"
Tarea: "Crear GeneradorPasswordTemporal en backend/src/GT.Infrastructure/Seguridad/"
```

---

## Implementation Strategy

### MVP primero (sólo User Story 1)

1. Fase 1: Setup
2. Fase 2: Foundational (CRÍTICO — bloquea todo)
3. Fase 3: User Story 1
4. **PARAR Y VALIDAR**: crear un usuario y comprobar que puede iniciar sesión
5. Entregar o demostrar si está listo

### Entrega incremental

1. Setup + Foundational → base lista
2. US1 → validar → entregar (MVP: ya se pueden crear cuentas, que es lo que el Módulo 1 no podía)
3. US2 → validar → entregar (el módulo ya es usable a diario, y habilita US3)
4. US6 → validar → entregar (el selector de persona deja de estar vacío)
5. US3 + US7 juntas → validar → entregar (el circuito de contraseñas queda cerrado; separadas dejan a alguien con una temporal que vence sin poder cambiarla)
6. US4 → validar → entregar
7. US5 → validar → entregar

---

## Notes

- Las tareas [P] tocan archivos distintos y no dependen de nada pendiente
- La etiqueta [Story] permite rastrear cada tarea hasta su historia
- Verificar que los tests fallen antes de implementar
- Hacer *commit* después de cada tarea o grupo lógico
- Se puede parar en cualquier *checkpoint* para validar la historia por separado
- **Cuidado con US7**: es la única con autorización distinta al resto del módulo. Si se implementa
  copiando y pegando un endpoint de otra historia, va a quedar exigiendo el permiso
  `usuarios.gestionar` y sólo el administrador va a poder cambiar su contraseña, que es exactamente
  lo contrario de lo que pide FR-029
- **Cuidado con T050**: toca código del Módulo 1 que hoy está funcionando. Si el corte de sesiones
  queda mal calibrado, el síntoma no es un error visible sino gente expulsada del sistema sin motivo
  aparente. Correr los tests de autenticación del Módulo 1 antes de dar la tarea por terminada
