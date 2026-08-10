# Tasks: Gestión de flota (Módulo 4)

**Input**: Documentos de diseño de `/specs/004-gestion-flota/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: se incluyen. El plan fija los proyectos de test y `quickstart.md` enumera ocho escenarios
que los tests automatizados cubren mejor que el recorrido manual; esas tareas están marcadas y
referencian el escenario que verifican.

**Organization**: las tareas se agrupan por historia de usuario para poder implementarlas y probarlas
de a una.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1…US6)
- Toda tarea lleva la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados, tal como fija la constitución:

- Backend por capa: `backend/src/GT.Api`, `GT.Application`, `GT.Domain`, `GT.Infrastructure`
- Tests: `backend/tests/GT.UnitTests`, `backend/tests/GT.IntegrationTests`
- Frontend por módulo de negocio: `frontend/src/modules/flota/`

## Lo que este módulo toca del Módulo 3

La spec acota los cambios al Módulo 3 a **dos**, y las tareas que los ejecutan están marcadas
`MODIFICA M3` para que se vean de un vistazo:

1. **El ámbito en `DocumentacionTipo`** (T005, T009, T022, T024, T031–T036). Va en la Fase 2 y no en
   una historia: **no tiene historia propia** —la spec extiende una pantalla que ya existe— y US3 no
   puede empezar sin él.
2. **La baja de `Transportista` mirando también su flota** (T100–T102). Va en US6, que es donde vive
   el resto de las reglas de baja.

Cualquier otro archivo del Módulo 3 se **consume sin tocarlo** (research §2). Si una tarea te pide
editar algo del Módulo 3 que no está en esa lista, revisá el diseño antes de hacerlo.

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar el esqueleto del módulo listo

Este módulo **no agrega ninguna variable de entorno ni ningún volumen**: los adjuntos van al mismo
`GT_ARCHIVOS_RUTA` que ya usa el Módulo 3, así que `docker-compose.yml` y `.env.template` no se tocan.

- [X] T001 Crear las carpetas del módulo en el backend: `backend/src/GT.Api/Flota/`, `backend/src/GT.Application/Flota/`, `backend/src/GT.Application/Flota/Documentacion/`, `backend/src/GT.Application/Flota/TiposVehiculo/`, `backend/src/GT.Domain/Flota/`
- [X] T002 [P] Crear las carpetas del módulo en el frontend: `frontend/src/modules/flota/paginas/`, `componentes/`, `servicios/`, `documentacion/` y `tiposVehiculo/`

---

## Phase 2: Foundational (prerrequisitos bloqueantes)

**Purpose**: dominio, persistencia, autorización y el ámbito del catálogo de documentación, que
**todas** las historias necesitan

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Entidades y enums de dominio

- [X] T003 [P] Crear el enum `VehiculoEstado` con `Disponible = 1` y `FueraDeServicio = 2` en `backend/src/GT.Domain/Flota/VehiculoEstado.cs` (FR-012, sin valor intermedio)
- [X] T004 [P] Crear el enum `EstadoDocumentacionVehiculo` con `SinDocumentacion = 1`, `EnRegla = 2`, `ProximaAvencer = 3` y `Vencida = 4` en `backend/src/GT.Domain/Flota/EstadoDocumentacionVehiculo.cs` (FR-033, derivado y nunca almacenado)
- [X] T005 [P] **MODIFICA M3** — Crear el enum `DocumentacionAmbito` con `Chofer = 1` y `Vehiculo = 2` en `backend/src/GT.Domain/Choferes/DocumentacionAmbito.cs`, junto a `DocumentacionTipo` (FR-017, research §3)
- [X] T006 [P] Crear la entidad `TipoVehiculo` en `backend/src/GT.Domain/Flota/TipoVehiculo.cs` con nombre único, `Activo` y la colección `Vehiculos` que se cuenta al intentar la baja (FR-009, FR-010)
- [X] T007 [P] Crear la entidad `Vehiculo` en `backend/src/GT.Domain/Flota/Vehiculo.cs` con `Patente` normalizada, `Marca`, `Modelo`, `TipoVehiculoId`, `TransportistaId`, `EstadoOperativo` y `Activo` (FR-001, FR-006, FR-012)
- [X] T008 [P] Crear la entidad `DocumentacionVehiculo` en `backend/src/GT.Domain/Flota/DocumentacionVehiculo.cs` **sin** columna de estado y **sin** `Activo`, con `ArchivoRuta`, `ArchivoNombre` y `ArchivoTipoContenido` nulos y la propiedad calculada `TieneArchivo` (FR-016, FR-016a, FR-028)
- [X] T009 **MODIFICA M3** — Agregar la propiedad obligatoria `Ambito` a `backend/src/GT.Domain/Choferes/DocumentacionTipo.cs`, sin tocar ninguna otra propiedad (FR-017)
- [X] T010 **MODIFICA M3** — Agregar la navegación inversa `ICollection<Vehiculo> Vehiculos` a `backend/src/GT.Domain/Choferes/Transportista.cs`, sin tocar ninguna columna existente (research §8)

### Reglas puras del dominio

- [X] T011 [P] Implementar `NormalizadorPatente` en `backend/src/GT.Domain/Flota/NormalizadorPatente.cs`, que pasa a mayúsculas y descarta todo lo que no sea letra o dígito, de modo que `ab 123 cd`, `AB-123-CD` y `AB123CD` den lo mismo (FR-003)
- [X] T012 [P] Implementar `ValidadorPatente` en `backend/src/GT.Domain/Flota/ValidadorPatente.cs` con los dos formatos argentinos —`^[A-Z]{3}[0-9]{3}$` y `^[A-Z]{2}[0-9]{3}[A-Z]{2}$`— aplicados **sobre la patente ya normalizada** (FR-004, research §6)
- [X] T013 [P] Implementar `CalculadorEstadoVehiculo` en `backend/src/GT.Domain/Flota/CalculadorEstadoVehiculo.cs` con `VigentesDeCadaTipo` —vencimiento más lejano y, con empate, `Id` mayor— y el estado general de cuatro valores con la precedencia `vencida` > `proximaAvencer` > `enRegla`, reutilizando `CalculadorEstadoDocumento` del Módulo 3 sin modificarlo (FR-024, FR-033, research §2)
- [X] T014 [P] Implementar `CalculadorEstadoOperativo` en `backend/src/GT.Domain/Flota/CalculadorEstadoOperativo.cs` con la derivación de FR-014: `fueraDeServicio` cuando la documentación es `vencida` o `sinDocumentacion`, y el valor guardado en cualquier otro caso

### Tests unitarios de las reglas puras

- [X] T015 [P] Tests de `NormalizadorPatente` en `backend/tests/GT.UnitTests/Flota/NormalizadorPatenteTests.cs`, comprobando que `ab 123 cd`, `AB-123-CD`, `AB.123.CD` y `AB123CD` normalizan igual (FR-003)
- [X] T016 [P] Tests de `ValidadorPatente` en `backend/tests/GT.UnitTests/Flota/ValidadorPatenteTests.cs` con los dos formatos válidos y casos inválidos conocidos: largo distinto, letras y dígitos en el orden equivocado, y cadena vacía (FR-004)
- [X] T017 [P] Tests de `CalculadorEstadoVehiculo` en `backend/tests/GT.UnitTests/Flota/CalculadorEstadoVehiculoTests.cs`, cubriendo que `sinDocumentacion` **no** es `enRegla` (FR-033), que ningún tipo es obligatorio (FR-034), que la falta de archivo adjunto **no** altera el estado (FR-016a), y el desempate por `Id` mayor con dos documentos del mismo tipo y la misma fecha de vencimiento (FR-024, `quickstart.md` §Tests)
- [X] T018 [P] Tests de `CalculadorEstadoOperativo` en `backend/tests/GT.UnitTests/Flota/CalculadorEstadoOperativoTests.cs`, comprobando que `disponible` guardado con documentación `vencida` o `sinDocumentacion` deriva a `fueraDeServicio`, y que al volver la documentación a `enRegla` el derivado vuelve a `disponible` sin tocar el valor guardado (FR-014)

### Persistencia

- [X] T019 [P] Configuración EF de `TipoVehiculo` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/TipoVehiculoConfiguracion.cs` con índice único de `Nombre`
- [X] T020 [P] Configuración EF de `Vehiculo` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/VehiculoConfiguracion.cs` con índice **único sin filtro** de `Patente` (FR-002), índices de `TransportistaId` y `TipoVehiculoId`, y las dos claves foráneas con `DeleteBehavior.Restrict`
- [X] T021 [P] Configuración EF de `DocumentacionVehiculo` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/DocumentacionVehiculoConfiguracion.cs` con el índice compuesto `VehiculoId, DocumentacionTipoId, FechaVencimiento DESC`, los índices de `DocumentacionTipoId` y `FechaVencimiento`, y `Ignore` sobre `TieneArchivo`
- [X] T022 **MODIFICA M3** — Agregar el mapeo de `Ambito` como `tinyint` obligatorio en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/DocumentacionTipoConfiguracion.cs`, sin tocar el índice único de `Nombre`, que sigue siendo global y no por ámbito (research §3)
- [X] T023 Registrar los tres `DbSet` nuevos y aplicar las configuraciones en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs` (depende de T019–T022)
- [X] T024 Generar la migración `Modulo4Flota` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, que agrega `DocumentacionTipos.Ambito` **con valor por defecto `Chofer` para las filas existentes** (FR-017c) y crea las tres tablas nuevas con sus índices y claves foráneas, sin sembrar ningún dato de negocio
- [X] T025 Test de integración de la migración en `backend/tests/GT.IntegrationTests/Flota/MigracionAmbitoTests.cs`: que los tipos de documentación preexistentes queden en `chofer` y que ningún documento de chofer cambie de estado por la migración (FR-017c, `quickstart.md` paso 2)

### Autorización y menú

- [X] T026 Agregar las constantes `FlotaGestionar = "flota.gestionar"` y `FlotaTiposGestionar = "flota.tipos.gestionar"` a `CodigosPermiso` en `backend/src/GT.Domain/Usuarios/Rol.cs`
- [X] T027 Sembrar los dos permisos y repartirlos por rol en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`: `flota.gestionar` a *Tráfico* y *Administrador del sistema*, `flota.tipos.gestionar` **sólo** a *Administrador del sistema* (FR-039, research §7)
- [X] T028 Registrar las políticas de los dos permisos y mapear los grupos de endpoints del módulo en `backend/src/GT.Api/Program.cs`
- [X] T029 Agregar las entradas *Flota* (`/flota`, permiso `flota.gestionar`) y *Tipos de vehículo* (`/tipos-vehiculo`, permiso `flota.tipos.gestionar`) en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`
- [X] T030 Test de integración del acceso por rol en `backend/tests/GT.IntegrationTests/Flota/AccesoPorRolTests.cs`: que Tráfico llegue a `/api/flota/vehiculos` y **no** a `/api/flota/tipos-vehiculo`, y que un rol sin permisos reciba `403` en los dos (FR-039, `quickstart.md` paso 1)

### El ámbito en el catálogo de tipos de documentación (cambio #1 al Módulo 3)

- [X] T031 **MODIFICA M3** — Exigir `ambito` al crear y al modificar, y rechazar el cambio de ámbito cuando el tipo tiene documentos asociados informando cuántos son, en `backend/src/GT.Application/Choferes/Documentacion/GestionTiposDocumentacion.cs` (FR-017, FR-017d)
- [X] T032 **MODIFICA M3** — Hacer que `ContarDocumentosAsync` sume **las dos tablas** —`Documentaciones` y `DocumentacionesVehiculo`— en `backend/src/GT.Infrastructure/Persistencia/RepositorioTiposDocumentacion.cs`, y agregar el filtro por ámbito a la consulta del catálogo (FR-017a, FR-017b)
- [X] T033 **MODIFICA M3** — Agregar `ambito` a los DTO de tipo de documentación y el parámetro de query `ambito` al listado, en `backend/src/GT.Application/Choferes/Documentacion/Dtos.cs` y `backend/src/GT.Api/Choferes/TiposDocumentacionEndpoints.cs`, declarándolo según `contracts/flota-api.yaml`
- [X] T034 Tests de integración del ámbito en `backend/tests/GT.IntegrationTests/Flota/AmbitoTiposDocumentacionTests.cs`: que el listado filtrado por `vehiculo` no devuelva los de chofer (FR-017a), que la baja cuente los documentos de los dos lados (FR-017b), y que el cambio de ámbito se acepte sin documentos y se rechace con ellos (FR-017d)
- [X] T035 **MODIFICA M3** — Agregar el campo *Ámbito* obligatorio al formulario y la columna con su filtro al listado, en `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`, con las etiquetas *Chofer* y *Vehículo* de `contracts/README.md`
- [X] T036 [P] **MODIFICA M3** — Test de frontend del campo obligatorio de ámbito y del rechazo al cambiarlo con documentos, en `frontend/src/modules/choferes/documentacion/TiposDocumentacion.test.tsx`

### Base compartida del módulo

- [X] T037 [P] Crear `backend/src/GT.Application/Flota/Mensajes.cs` con los textos en español rioplatense y los códigos de error de `contracts/README.md` (§Errores)
- [X] T038 Crear `backend/src/GT.Application/Flota/Dtos.cs` con los DTO comunes según `contracts/flota-api.yaml`, reutilizando `PaginaDe<T>` del Módulo 3 y traduciendo los enums a camelCase (`fueraDeServicio`, `dadoDeBaja`, `enRegla`, `proximaAvencer`)
- [X] T039 Registrar las rutas `/flota`, `/flota/nuevo`, `/flota/:id`, `/flota/:id/editar`, `/flota/vencimientos` y `/tipos-vehiculo` en `frontend/src/App.tsx`
- [X] T040 [P] Crear el cliente HTTP del módulo en `frontend/src/modules/flota/servicios/api.ts`, reutilizando `frontend/src/compartido/clienteHttp.ts`

**Checkpoint**: base lista — las historias pueden empezar

---

## Phase 3: User Story 1 - Mantener el catálogo de tipos de vehículo (Priority: P1) 🎯 MVP

**Goal**: que el Administrador pueda cargar los tipos con los que trabaja la empresa, para que Tráfico
tenga cómo clasificar cada unidad al registrarla.

**Independent Test**: con el catálogo vacío, abrir *Tipos de vehículo*, ver el mensaje explícito de
catálogo vacío, cargar dos tipos y comprobar que los dos quedan disponibles para elegir al registrar
un vehículo.

### Tests de la historia

- [X] T041 [P] [US1] Test de integración de nombre duplicado y de baja lógica en `backend/tests/GT.IntegrationTests/Flota/TiposVehiculoTests.cs`, comprobando que el registro **no se borra** (FR-009, US1 esc. 3 y 4)
- [X] T042 [P] [US1] Test de integración del rechazo de baja con vehículos asociados —activos **e inactivos**— informando la cantidad, en `backend/tests/GT.IntegrationTests/Flota/TiposVehiculoBajaTests.cs` (FR-010, SC-008, research §8)

### Implementación

- [X] T043 [US1] Implementar `GestionTiposVehiculo` con crear, consultar, modificar y dar de baja en `backend/src/GT.Application/Flota/TiposVehiculo/GestionTiposVehiculo.cs`, con nombre único y el rechazo de baja por vehículos asociados
- [X] T044 [US1] Declarar `IRepositorioTiposVehiculo` en `backend/src/GT.Application/Flota/TiposVehiculo/IRepositorioTiposVehiculo.cs` e implementarlo en `backend/src/GT.Infrastructure/Persistencia/RepositorioTiposVehiculo.cs`, contando los vehículos en la misma consulta y traduciendo la violación del índice único a excepción de la capa de aplicación
- [X] T045 [US1] Exponer los cuatro endpoints de `/api/flota/tipos-vehiculo` en `backend/src/GT.Api/Flota/TiposVehiculoEndpoints.cs`, con el permiso `flota.tipos.gestionar` para escribir y `flota.gestionar` para leer (`contracts/flota-api.yaml`)
- [X] T046 [P] [US1] Crear el servicio de tipos de vehículo en `frontend/src/modules/flota/tiposVehiculo/servicioTiposVehiculo.ts`
- [X] T047 [US1] Crear la pantalla de listado en `frontend/src/modules/flota/tiposVehiculo/ListadoTiposVehiculo.tsx`, con el mensaje explícito de catálogo vacío de `contracts/README.md` (FR-036, US1 esc. 1)
- [X] T048 [US1] Crear el formulario de alta y edición en `frontend/src/modules/flota/tiposVehiculo/FormularioTipoVehiculo.tsx`, con la confirmación de baja y el mensaje de rechazo que dice cuántos vehículos dependen del tipo
- [X] T049 [P] [US1] Test de frontend del estado vacío y del error de nombre duplicado en `frontend/src/modules/flota/tiposVehiculo/ListadoTiposVehiculo.test.tsx`

**Checkpoint**: el catálogo de tipos de vehículo funciona de punta a punta

---

## Phase 4: User Story 2 - Registrar un vehículo en el padrón de flota (Priority: P1)

**Goal**: incorporar unidades al padrón con su patente única, su tipo y el transportista dueño, que es
lo que distingue la flota propia de la contratada.

**Independent Test**: con al menos un tipo de vehículo y un transportista activo, completar el
formulario con una patente nueva, guardar, ver la unidad en el listado con su tipo, su transportista y
su estado; repetir la misma patente con espacios y en minúsculas y comprobar que se rechaza como
duplicada.

### Tests de la historia

- [X] T050 [P] [US2] Test de integración de unicidad de patente sobre el valor normalizado en `backend/tests/GT.IntegrationTests/Flota/VehiculosPatenteTests.cs`: `AB123CD`, `ab 123 cd` y `AB-123-CD` son la misma patente y sólo la primera crea un vehículo (FR-002, FR-003, SC-002)
- [X] T051 [P] [US2] Test de integración de FR-008f en `backend/tests/GT.IntegrationTests/Flota/PatenteDeVehiculoDadoDeBajaTests.cs`: registrar una patente que pertenece a una unidad **dada de baja** devuelve `patente_de_vehiculo_dado_de_baja`, no `patente_duplicada` (research §6)
- [X] T052 [P] [US2] Test de integración del alta en `backend/tests/GT.IntegrationTests/Flota/CrearVehiculoTests.cs`: rechazo sin tipo, sin transportista, con tipo o transportista **inactivo**, con patente de formato inválido, y rechazo de `disponible` porque la unidad nueva no tiene documentación (FR-005, FR-008a, FR-014a, US2 esc. 3 a 8)

### Implementación

- [X] T053 [US2] Implementar `CrearVehiculo` en `backend/src/GT.Application/Flota/CrearVehiculo.cs`, normalizando y validando la patente antes de comparar, exigiendo tipo y transportista activos, y rechazando `disponible` con documentación ausente (FR-003 a FR-005, FR-008a, FR-014a)
- [X] T054 [US2] Declarar `IRepositorioVehiculos` en `backend/src/GT.Application/Flota/IRepositorioVehiculos.cs` e implementar el alta, `ObtenerPorPatenteAsync` y `ObtenerPorIdConRelacionesAsync` en `backend/src/GT.Infrastructure/Persistencia/RepositorioVehiculos.cs`, traduciendo la violación de `IX_Vehiculos_Patente` a excepción de la capa de aplicación (convención [003])
- [X] T055 [US2] Exponer `POST /api/flota/vehiculos` y `GET /api/flota/vehiculos/{id}` en `backend/src/GT.Api/Flota/VehiculosEndpoints.cs`, con el permiso `flota.gestionar`
- [X] T056 [P] [US2] Crear el servicio de flota en `frontend/src/modules/flota/servicios/servicioFlota.ts`
- [X] T057 [US2] Crear el formulario de alta en `frontend/src/modules/flota/paginas/FormularioVehiculo.tsx`, con validación de formato de patente antes de enviar, selectores de tipo y transportista **activos**, y el estado operativo fijado en *Fuera de servicio* con el texto explicativo de `contracts/README.md` (FR-014a, US2 esc. 8)
- [X] T058 [US2] Bloquear el alta con mensaje explícito cuando no hay ningún tipo de vehículo activo o ningún transportista activo, en `frontend/src/modules/flota/paginas/FormularioVehiculo.tsx` (US2 esc. 6 y 7)
- [X] T059 [P] [US2] Test de frontend del formulario en `frontend/src/modules/flota/paginas/FormularioVehiculo.test.tsx`: que el estado operativo no ofrezca *Disponible* en el alta, y que una patente mal formada marque el campo con el motivo puntual

**Checkpoint**: se pueden registrar unidades con su tipo y su transportista, sin patentes duplicadas

---

## Phase 5: User Story 3 - Cargar la documentación de un vehículo (Priority: P1)

**Goal**: que cada unidad tenga su documentación con archivo escaneado y estado calculado por el
sistema, y que un documento cargado mal se pueda corregir o eliminar.

**Independent Test**: sobre un vehículo registrado y con al menos un tipo de documentación de ámbito
vehículo, cargar tres documentos con vencimiento lejano, cercano y pasado, y comprobar que el sistema
los muestra como `vigente`, `proximaAvencer` y `vencida` sin que nadie haya elegido el estado y sin
que el campo sea editable.

### Tests de la historia

- [X] T060 [P] [US3] Test de integración de FR-017a en `backend/tests/GT.IntegrationTests/Flota/DocumentacionAmbitoTests.cs`: cargar un documento de vehículo con un tipo de ámbito **chofer** se rechaza con `tipo_inexistente` (US3 esc. 12)
- [X] T061 [P] [US3] Test de integración de la renovación y el historial en `backend/tests/GT.IntegrationTests/Flota/RenovacionDocumentoTests.cs`: el documento anterior queda como historial, deja de contar para el estado general y deja de alertar; y al eliminar el vigente, el más reciente de los que quedan vuelve a mandar (FR-023, FR-024, SC-010)
- [X] T062 [P] [US3] Test de integración del rechazo de archivos en `backend/tests/GT.IntegrationTests/Flota/ArchivoAdjuntoTests.cs`: tipo no admitido, tamaño excedido, y un archivo con extensión `.pdf` que no es un PDF —validación por firma, no por extensión— (FR-025, US3 esc. 8)
- [X] T063 [P] [US3] Test de integración de la atomicidad en `backend/tests/GT.IntegrationTests/Flota/AtomicidadAdjuntoTests.cs`, sustituyendo `IAlmacenDeArchivos` por uno que falla: al cargar, el documento **no** queda creado; al corregir con un archivo de reemplazo que falla, el documento **no** queda modificado ni pierde el adjunto anterior (FR-029, el único requisito sin escenario de aceptación). Sumar el camino exitoso: al reemplazar bien, el archivo anterior **queda borrado** y el documento apunta al nuevo (CHK023)

### Implementación

- [X] T064 [US3] Implementar `CargarDocumentoVehiculo` en `backend/src/GT.Application/Flota/Documentacion/CargarDocumentoVehiculo.cs`, exigiendo tipo activo de ámbito vehículo y vencimiento **posterior** a la emisión, con el archivo opcional y **el archivo escrito antes de confirmar la fila**, borrándolo si la fila falla (FR-016 a FR-018, FR-025, FR-029, convención [003])
- [X] T065 [US3] Implementar `CorregirDocumentoVehiculo` en `backend/src/GT.Application/Flota/Documentacion/CorregirDocumentoVehiculo.cs`, con las mismas validaciones que el alta, conservando el archivo previo si no viene uno nuevo, **borrando el archivo anterior después de confirmar la fila** cuando el reemplazo se aplica (CHK023), y recalculando el estado (FR-026, FR-029)
- [X] T066 [US3] Implementar `EliminarDocumentoVehiculo` en `backend/src/GT.Application/Flota/Documentacion/EliminarDocumentoVehiculo.cs`, que **borra físicamente** la fila y después su archivo (FR-027, FR-028, convención [003])
- [X] T067 [US3] Implementar `DescargarArchivoDocumentoVehiculo` en `backend/src/GT.Application/Flota/Documentacion/DescargarArchivoDocumentoVehiculo.cs`, devolviendo contenido, tipo y nombre original
- [X] T068 [US3] Declarar `IRepositorioDocumentacionVehiculo` en `backend/src/GT.Application/Flota/Documentacion/IRepositorioDocumentacionVehiculo.cs` e implementarlo en `backend/src/GT.Infrastructure/Persistencia/RepositorioDocumentacionVehiculo.cs`, reutilizando `IAlmacenDeArchivos` e `IValidadorDeArchivo` del Módulo 3 **sin modificarlos** (research §2)
- [X] T069 [US3] Exponer los cuatro endpoints de documentación en `backend/src/GT.Api/Flota/DocumentacionVehiculoEndpoints.cs` como `multipart/form-data` con `DisableAntiforgery`, y la descarga bajo el **mismo** permiso `flota.gestionar` (FR-038, SC-011)
- [X] T070 [P] [US3] Crear el servicio de documentación en `frontend/src/modules/flota/documentacion/servicioDocumentacionVehiculo.ts`
- [X] T071 [US3] Crear el formulario de documento en `frontend/src/modules/flota/documentacion/FormularioDocumentoVehiculo.tsx`, **sin ningún campo de estado**, con el selector de tipo filtrado a `ambito=vehiculo&soloActivos=true` y el archivo etiquetado *"Archivo (opcional)"* (FR-016a, FR-017a, FR-021)
- [X] T072 [US3] Crear la ficha del vehículo en `frontend/src/modules/flota/paginas/FichaVehiculo.tsx` con sus documentos agrupados por tipo, los históricos atenuados **y con la palabra "Histórico"**, y *"Sin archivo adjunto"* en los que no tienen (FR-038, FR-016a, convención [003])
- [X] T073 [US3] Agregar la confirmación de eliminación de documento en `frontend/src/modules/flota/componentes/ConfirmacionEliminarDocumento.tsx`, con la advertencia de que no se puede deshacer, y que cancelar no cambie nada (FR-027, SC-009, US3 esc. 10 y 11)
- [X] T074 [P] [US3] Test de frontend en `frontend/src/modules/flota/documentacion/FormularioDocumentoVehiculo.test.tsx`: que no exista ningún control de estado, que el archivo sea opcional y que el selector no ofrezca tipos de ámbito chofer

**Checkpoint**: la documentación se carga, se corrige y se elimina, con el estado calculado por el sistema

---

## Phase 6: User Story 4 - Consultar la flota y el estado de su documentación (Priority: P1)

**Goal**: el listado y la ficha que responden, antes de asignar un viaje, qué unidad está en
condiciones de salir a la ruta.

**Independent Test**: con vehículos de distintos tipos y transportistas y documentación en los tres
estados, aplicar combinaciones de los cuatro filtros y comprobar que el listado y la ficha muestran
exactamente lo esperado.

### Tests de la historia

- [X] T075 [P] [US4] Test de integración de los cuatro filtros combinados en `backend/tests/GT.IntegrationTests/Flota/FiltrosFlotaTests.cs`, incluido que sin filtro de estado se devuelvan **sólo los activos** (FR-030, FR-031, US4 esc. 2 y 3)
- [X] T076 [P] [US4] Test de integración de SC-006 en `backend/tests/GT.IntegrationTests/Flota/FiltroDisponibleTests.cs`: el filtro `disponible` devuelve **0%** de unidades con documentación `vencida` o `sinDocumentacion`, y el 100% de las excluidas por esa causa aparece en el panel (FR-015, US4 esc. 4)
- [X] T077 [P] [US4] Test de integración del estado derivado en `backend/tests/GT.IntegrationTests/Flota/EstadoOperativoDerivadoTests.cs`: una unidad guardada como `disponible` con el seguro vencido se lista como `fueraDeServicio` sin que nadie la edite, y vuelve a `disponible` al cargar la renovación (FR-014, US4 esc. 11, `quickstart.md` paso 12)
- [X] T078 [P] [US4] Test de equivalencia entre la regla en C# y la consulta en SQL en `backend/tests/GT.IntegrationTests/Flota/EquivalenciaEstadoTests.cs`: sobre el mismo conjunto de datos, `CalculadorEstadoVehiculo` y el listado tienen que dar el mismo estado para cada unidad (convención [003] de `CLAUDE.md`, research §13)
- [X] T079 [P] [US4] Test de integración de la paginación en `backend/tests/GT.IntegrationTests/Flota/PaginacionFlotaTests.cs`: 25 vehículos dan 20 + 5 con el total en 25, ninguna fila aparece en dos páginas y el orden es el mismo entre dos consultas iguales (FR-032, US4 esc. 9)

### Implementación

- [X] T080 [US4] Implementar `ConsultarFlota` en `backend/src/GT.Application/Flota/ConsultarFlota.cs` con los cuatro filtros, activos por defecto y página de 20 (FR-030, FR-030a, FR-031, FR-032)
- [X] T081 [US4] Implementar `ConsultarAsync` en `backend/src/GT.Infrastructure/Persistencia/RepositorioVehiculos.cs`, resolviendo en la base los conteos de vigentes, vencidos y por vencer con subconsultas correlacionadas, el estado operativo derivado y el filtro de estado con los cuatro predicados de `data-model.md`; **el predicado del vigente va escrito en el árbol de expresión, no extraído a un método** (research §5, convención [003])
- [X] T082 [US4] Ordenar por `Patente, Id` y devolver `{ items, total, pagina, tamanioPagina }` con `PaginaDe<T>` en `backend/src/GT.Infrastructure/Persistencia/RepositorioVehiculos.cs` (FR-032, research §9)
- [X] T083 [US4] Implementar `ConsultarFichaVehiculo` en `backend/src/GT.Application/Flota/ConsultarFichaVehiculo.cs`, devolviendo **el estado derivado y el guardado** —el primero para mostrar, el segundo para poblar el formulario de edición— y todos los documentos con `esVigenteDelTipo` (FR-038, plan §Reevaluación post-diseño)
- [X] T084 [US4] Exponer `GET /api/flota/vehiculos` con sus cinco parámetros de query en `backend/src/GT.Api/Flota/VehiculosEndpoints.cs`, declarando los booleanos como `bool?` con `?? false` (convención [003])
- [X] T085 [US4] Crear la pantalla de listado en `frontend/src/modules/flota/paginas/ListadoFlota.tsx` con las siete columnas de `contracts/README.md` y los mensajes de padrón vacío y de sin resultados (FR-030, FR-036)
- [X] T086 [US4] Crear los cuatro filtros en `frontend/src/modules/flota/componentes/FiltrosFlota.tsx`, con el control de estado del vehículo como **único selector de tres valores** y mostrando siempre explícitamente qué está filtrando (FR-030a, FR-037, US4 esc. 10)
- [X] T087 [P] [US4] Crear el control de paginación en `frontend/src/modules/flota/componentes/Paginacion.tsx` con el total de coincidencias (FR-032)
- [X] T088 [P] [US4] Test de frontend en `frontend/src/modules/flota/paginas/ListadoFlota.test.tsx`: los dos estados vacíos con sus textos distintos, y que el control diga qué estado está filtrando

**Checkpoint**: se puede responder qué unidad está en condiciones de salir a la ruta

---

## Phase 7: User Story 5 - Detectar documentación próxima a vencer o vencida (Priority: P2)

**Goal**: el panel que muestra, al entrar al módulo, qué unidades necesitan renovar algo antes de
quedar inhabilitadas para circular.

**Independent Test**: cargar documentos con vencimiento dentro y fuera de la ventana de aviso de su
tipo, y comprobar que sólo los primeros aparecen en el panel.

### Tests de la historia

- [X] T089 [P] [US5] Test de integración del panel en `backend/tests/GT.IntegrationTests/Flota/VencimientosFlotaTests.cs`: excluye vehículos **dados de baja**, excluye documentos ya reemplazados por una renovación, incluye a todos los que el filtro `disponible` dejó afuera, y ordena por urgencia (FR-035, US5 esc. 1 a 4, SC-006)

### Implementación

- [X] T090 [US5] Implementar `ConsultarVencimientosFlota` en `backend/src/GT.Application/Flota/Documentacion/ConsultarVencimientosFlota.cs`, evaluando sólo el vigente de cada tipo de los vehículos activos y ordenando por `FechaVencimiento` y después por `Id` (FR-035, research §10)
- [X] T091 [US5] Agregar `ConsultarVigentesDeVehiculosActivosAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioDocumentacionVehiculo.cs`
- [X] T092 [US5] Exponer `GET /api/flota/vencimientos` en `backend/src/GT.Api/Flota/DocumentacionVehiculoEndpoints.cs`
- [X] T093 [US5] Crear el panel en `frontend/src/modules/flota/paginas/PanelVencimientosFlota.tsx`, con *"Vence en N días"* / *"Venció hace N días"*, el enlace a la ficha de cada unidad y el mensaje *"No hay vencimientos pendientes."* cuando está vacío (FR-035, FR-036, US5 esc. 2 y 5)
- [X] T094 [P] [US5] Test de frontend del panel vacío y del texto de días en `frontend/src/modules/flota/paginas/PanelVencimientosFlota.test.tsx`

**Checkpoint**: los vencimientos se ven solos, sin que nadie ejecute nada

---

## Phase 8: User Story 6 - Modificar, reasignar, dar de baja y reactivar vehículos (Priority: P3)

**Goal**: mantener el padrón fiel a la realidad: corregir datos, reasignar de transportista, dar de
baja lógicamente y reactivar unidades que vuelven.

**Independent Test**: editar la marca y el modelo de un vehículo, reasignarlo a otro transportista,
cambiar su estado operativo, darlo de baja y comprobar que deja de figurar en el listado sin filtros
pero reaparece con el filtro `Dado de baja`, con su registro y su documentación intactos.

### Tests de la historia

- [X] T095 [P] [US6] Test de integración de la modificación en `backend/tests/GT.IntegrationTests/Flota/ModificarVehiculoTests.cs`: la unicidad de patente **excluye al propio vehículo**, la reasignación conserva la documentación íntegra, y se rechaza dejarlo sin transportista o con uno inactivo (FR-002, FR-008a, FR-008c, SC-003c, US6 esc. 2 a 4)
- [X] T096 [P] [US6] Test de integración de baja y reactivación en `backend/tests/GT.IntegrationTests/Flota/BajaReactivacionVehiculoTests.cs`: la baja conserva documentos y archivos, la unidad sale del listado por defecto y del panel, y al reactivarla vuelve a los dos con toda su documentación (FR-008, FR-008e, FR-031, FR-035, US6 esc. 5, 8 y 9)
- [X] T097 [P] [US6] Test de integración de la reactivación con dependencias inactivas en `backend/tests/GT.IntegrationTests/Flota/ReactivacionConDependenciasTests.cs`: con el transportista o el tipo dados de baja, la reactivación se rechaza pidiendo uno activo, y procede al enviarlo (FR-008e, US6 esc. 11)
- [X] T098 [P] [US6] Test de integración de FR-008d en `backend/tests/GT.IntegrationTests/Flota/BajaTransportistaConFlotaTests.cs`: la baja se rechaza con vehículos activos informando **las dos cantidades**, y procede cuando choferes y vehículos están todos inactivos (US6 esc. 12, SC-008)
- [X] T099 [P] [US6] Test de integración de FR-014a en la edición, en `backend/tests/GT.IntegrationTests/Flota/DisponibleConDocumentacionVencidaTests.cs`: dejar `disponible` una unidad con un documento `vencida` se rechaza nombrando qué documentación lo impide (US6 esc. 7)

### Implementación

- [X] T100 [US6] Implementar `ModificarVehiculo` en `backend/src/GT.Application/Flota/ModificarVehiculo.cs`, con la unicidad de patente excluyendo al propio registro, la reasignación de transportista y el rechazo de `disponible` con documentación vencida o ausente (FR-002, FR-008c, FR-014a)
- [X] T101 [US6] Implementar `DarDeBajaVehiculo` en `backend/src/GT.Application/Flota/DarDeBajaVehiculo.cs`, que pone `Activo` en `false` **sin tocar la documentación** (FR-001, FR-008, FR-028)
- [X] T102 [US6] Implementar `ReactivarVehiculo` en `backend/src/GT.Application/Flota/ReactivarVehiculo.cs`, con el cuerpo opcional de `transportistaId` y `tipoVehiculoId` y el rechazo si alguno de los actuales está inactivo y no vino reemplazo (FR-008e, research §11)
- [X] T103 [US6] Agregar el rechazo de alta con patente de una unidad dada de baja —`patente_de_vehiculo_dado_de_baja`, distinto de `patente_duplicada`— en `backend/src/GT.Application/Flota/CrearVehiculo.cs` (FR-008f, research §6)
- [X] T104 [US6] Exponer `PUT /api/flota/vehiculos/{id}`, `DELETE /api/flota/vehiculos/{id}` y `POST /api/flota/vehiculos/{id}/reactivacion` en `backend/src/GT.Api/Flota/VehiculosEndpoints.cs`
- [X] T105 [US6] **MODIFICA M3** — Contar los vehículos activos junto con los choferes activos en la misma consulta, y renombrar `TransportistaConChoferesActivos` a `TransportistaConDependenciasActivas`, en `backend/src/GT.Infrastructure/Persistencia/RepositorioTransportistas.cs` y `backend/src/GT.Application/Choferes/Transportistas/` (FR-008d, research §8)
- [X] T106 [US6] **MODIFICA M3** — Rechazar la baja de un transportista con al menos un vehículo activo, informando las dos cantidades, en `backend/src/GT.Application/Choferes/Transportistas/ModificarTransportista.cs` (`DarDeBajaTransportista`) y su mensaje en `backend/src/GT.Application/Choferes/Mensajes.cs` (FR-008d)
- [X] T107 [US6] **MODIFICA M3** — Mostrar la cantidad de vehículos activos junto a la de choferes en el listado y en el mensaje de rechazo de baja, en `frontend/src/modules/choferes/transportistas/ListadoTransportistas.tsx`
- [X] T108 [US6] Habilitar la edición en `frontend/src/modules/flota/paginas/FormularioVehiculo.tsx`, donde *Disponible* sí se ofrece y el rechazo de FR-014a se muestra nombrando el documento que lo impide
- [X] T109 [US6] Agregar las confirmaciones de baja y de reactivación en `frontend/src/modules/flota/componentes/ConfirmacionBajaVehiculo.tsx`, con los textos de `contracts/README.md` y sin efecto al cancelar (FR-007, FR-008e, SC-009, US6 esc. 6)
- [X] T110 [P] [US6] Test de frontend de las dos confirmaciones en `frontend/src/modules/flota/componentes/ConfirmacionBajaVehiculo.test.tsx`, comprobando que cancelar no dispara ninguna petición

**Checkpoint**: las seis historias funcionan

---

## Phase 9: Polish & Cross-Cutting Concerns

- [X] T111 [P] Verificar que ningún estado se comunique sólo por color y que los documentos históricos lleven la palabra *"Histórico"* además del gris, en `frontend/src/modules/flota/` (convención [003])
- [X] T112 [P] Anunciar con `role="status"` el resultado de la carga de archivo, el cambio de página y el cambio de estado operativo, en `frontend/src/modules/flota/`
- [X] T113 [P] Verificar que todas las fechas se formateen con `date-fns` desde `frontend/src/compartido/fechas.ts` y que no quede ningún `new Date(iso).toLocaleDateString()` en `frontend/src/modules/flota/` (convención [003])
- [X] T114 [P] Revisar que los textos de `backend/src/GT.Application/Flota/Mensajes.cs` y los de `frontend/src/modules/flota/` estén en español rioplatense y coincidan con `contracts/README.md`
- [X] T115 Revisar que ningún endpoint de `backend/src/GT.Api/Flota/` quede sin su permiso, incluida la descarga de archivos, y que el ABM de tipos de vehículo exija `flota.tipos.gestionar` y no `flota.gestionar` (FR-039, SC-011)
- [X] T116 Correr el recorrido completo de `specs/004-gestion-flota/quickstart.md` con las dos cuentas (`admin` y un usuario de Tráfico) — hecho. Como en los tres módulos anteriores, encontró lo que ningún test veía: dos comportamientos que estaban implementados según la spec pero que al operarlos no eran los correctos (Fase 10)
- [X] T117 Correr `dotnet test` en `backend/` y `npm test` en `frontend/`, y dejar ambos en verde
- [X] T118 [P] Actualizar `specs/README.md` con el estado del Módulo 4
- [X] T119 Actualizar `CLAUDE.md` con las decisiones transversales de esta feature, una línea por decisión y con la referencia `[004]`, tomando como punto de partida las cuatro candidatas de `plan.md` §Mantenimiento y descartando las que al implementar resulten no ser transversales

---

## Phase 10: Lo que encontró el recorrido manual (T116)

Ninguno de los dos es un defecto contra la spec: los dos estaban implementados como la spec pedía. Lo
que el recorrido mostró es que **la spec pedía lo que no había que hacer**, y eso sólo aparece
operando la aplicación. Los dos cambios llevaron su ajuste de spec y de contratos.

- [X] T120 Permitir dar de alta un tipo de vehículo dado de baja (FR-009, US1 esc. 6). El catálogo dejaba dar de baja y nunca volver: un `Utilitario` bajado por error quedaba inactivo para siempre. Backend: `ReactivarAsync` en `backend/src/GT.Application/Flota/TiposVehiculo/GestionTiposVehiculo.cs` y `POST /api/flota/tipos-vehiculo/{id}/reactivacion` en `backend/src/GT.Api/Flota/TiposVehiculoEndpoints.cs`, con `flota.tipos.gestionar`. Frontend: `reactivarTipoVehiculo` y el botón **Dar de alta** dentro de `frontend/src/modules/flota/tiposVehiculo/FormularioTipoVehiculo.tsx`, visible sólo al editar un tipo inactivo. **Recurso aparte y no un campo del `PUT`**: guardar el nombre no puede cambiar de paso el estado
- [X] T121 Servir los escaneos **en línea** en vez de como descarga, en los dos módulos (Módulo 3 y Módulo 4). *Abrir archivo* bajaba el archivo y obligaba a abrirlo a mano. `Results.File(..., nombre)` escribe `Content-Disposition: attachment`; el helper nuevo `backend/src/GT.Api/Archivos/ResultadoArchivo.cs` escribe `inline` con el nombre original más `X-Content-Type-Options: nosniff`, y lo usan `DocumentacionEndpoints.cs` y `DocumentacionVehiculoEndpoints.cs`. **Vive fuera de los dos módulos a propósito**: si cada uno resolviera lo suyo, la misma acción podría comportarse distinto según de dónde se la tome. El frontend no cambió —los enlaces ya tenían `target="_blank"`—, y eso es justamente lo que hace que la decisión sea del backend

**Checkpoint**: el módulo queda validado

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende del Setup — **bloquea todas las historias**
- **Historias (Phase 3+)**: todas dependen de la Fase 2
- **Polish (Phase 9)**: depende de las historias que se quieran entregar

### User Story Dependencies

Este módulo **no** tiene seis historias independientes entre sí, y conviene decirlo antes de repartir
el trabajo:

| Historia | Depende de | Por qué |
|---|---|---|
| US1 Tipos de vehículo | — | Es la única que arranca sola |
| US2 Registrar vehículo | US1 | Todo vehículo necesita un tipo activo (FR-005) y un transportista activo, que viene del Módulo 3 |
| US3 Documentación | US2 | Necesita un vehículo. El catálogo de tipos con ámbito ya quedó en la Fase 2 |
| US4 Consulta | US2, US3 | Sin documentación cargada, los filtros por estado no significan nada |
| US5 Panel | US3, US4 | Es una vista sobre los mismos datos |
| US6 Modificar y bajas | US2, US4 | Opera sobre lo que las otras crearon |

### Orden recomendado de ejecución

**US1 → US2 → US3 → US4 → US5 → US6** — el orden de prioridades, sin alteraciones.

A diferencia del Módulo 3, acá **no hace falta adelantar ninguna historia P2**: el catálogo de tipos
de documentación no es una historia de este módulo, sino una extensión de una pantalla del Módulo 3,
y por eso su ámbito vive en la Fase 2, antes que todo.

### Parallel Opportunities

- Fase 1: T001 y T002 en paralelo
- Fase 2: las seis entidades y enums (T003–T008) en paralelo; después las cuatro reglas puras
  (T011–T014) en paralelo; después los cuatro tests unitarios (T015–T018) y las tres configuraciones
  EF nuevas (T019–T021) en paralelo
- Dentro de cada historia: los tests de integración entre sí, y el servicio del frontend en paralelo
  con la implementación del backend
- Entre historias: **este módulo casi no admite paralelizar historias.** La cadena
  US1 → US2 → US3 → US4 es lineal. Lo único que sí se puede repartir: mientras alguien hace US1, otra
  persona puede tomar el bloque del ámbito de la Fase 2 (T031–T036), que es independiente del resto

---

## Parallel Example: Fase 2, entidades y reglas

```bash
# Las seis entidades y enums de dominio, juntos:
Task: "Crear el enum VehiculoEstado en backend/src/GT.Domain/Flota/VehiculoEstado.cs"
Task: "Crear el enum EstadoDocumentacionVehiculo en backend/src/GT.Domain/Flota/EstadoDocumentacionVehiculo.cs"
Task: "Crear el enum DocumentacionAmbito en backend/src/GT.Domain/Choferes/DocumentacionAmbito.cs"
Task: "Crear la entidad TipoVehiculo en backend/src/GT.Domain/Flota/TipoVehiculo.cs"
Task: "Crear la entidad Vehiculo en backend/src/GT.Domain/Flota/Vehiculo.cs"
Task: "Crear la entidad DocumentacionVehiculo en backend/src/GT.Domain/Flota/DocumentacionVehiculo.cs"

# Después, las cuatro reglas puras, juntas:
Task: "Implementar NormalizadorPatente en backend/src/GT.Domain/Flota/NormalizadorPatente.cs"
Task: "Implementar ValidadorPatente en backend/src/GT.Domain/Flota/ValidadorPatente.cs"
Task: "Implementar CalculadorEstadoVehiculo en backend/src/GT.Domain/Flota/CalculadorEstadoVehiculo.cs"
Task: "Implementar CalculadorEstadoOperativo en backend/src/GT.Domain/Flota/CalculadorEstadoOperativo.cs"
```

---

## Implementation Strategy

### MVP (US1 + US2 + US3)

1. Fase 1: Setup
2. Fase 2: Foundational — bloquea todo
3. US1 (tipos de vehículo)
4. US2 (registrar unidades)
5. US3 (documentación con estado calculado)
6. **PARAR Y VALIDAR**: en este punto ya se puede cargar un tipo, registrar una unidad y cargar su
   documentación completa sin intervención técnica, que es exactamente SC-001

El MVP no es US1 sola: un catálogo de tipos sin unidades que clasificar no le sirve a nadie en Tráfico.
El primer incremento con valor real es poder registrar un vehículo con su documentación.

### Entrega incremental

1. Setup + Foundational → base lista, con el ámbito ya migrado
2. US1 → catálogo de tipos cargable
3. US2 → unidades registradas
4. US3 → documentación con estado calculado ← **primer incremento con valor** (SC-001)
5. US4 → consulta, filtros y el estado operativo derivado ← **el corazón del módulo** (SC-006)
6. US5 → panel de vencimientos
7. US6 → correcciones, reasignación y bajas
8. Fase 9 → accesibilidad, textos, validación completa y `CLAUDE.md`

### Con más de una persona

Con la Fase 2 terminada hay poco que repartir, porque la cadena de historias es lineal. Lo que sí
funciona: una persona arranca US1 → US2 → US3 mientras otra toma el bloque del ámbito (T031–T036) y
después se suma en US4, que es la historia más grande del módulo —cinco tests de integración y la
consulta con todo resuelto en SQL—. US5 y US6 se reparten al final.

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes
- Cada tarea nombra el archivo exacto; ninguna dice "agregar validaciones" sin decir dónde
- Las tareas de test referencian el escenario de `spec.md` o `quickstart.md` que verifican
- Las tareas marcadas **MODIFICA M3** son las únicas que pueden tocar el Módulo 3: son doce, y
  ejecutan los dos cambios que la spec autoriza
- Conviene commitear por tarea o por grupo lógico

### Tres cosas para tener a mano al implementar

Salen del diseño y son las que más fácil se pasan por alto:

1. **La ficha devuelve el estado operativo dos veces** (T083): el derivado para mostrar y el guardado
   para poblar el formulario de edición. Con uno solo, editar una unidad parada por papeles vencidos
   le pisa en silencio el motivo real al operador.
2. **El predicado del documento vigente va escrito en el árbol de expresión** (T081), no extraído a un
   método propio: extraerlo rompe la traducción de EF Core y la consulta pasa a evaluarse en memoria.
   Es la convención [003] y es la diferencia entre filtrar en la base y traer toda la flota.
3. **El archivo se escribe antes de confirmar la fila y se borra después** (T064–T066). Deja como
   único estado roto posible un archivo huérfano, invisible para quien opera, en vez del que FR-029
   prohíbe: una fila que dice tener adjunto y no lo tiene.
