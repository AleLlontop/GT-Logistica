# Tasks: Gestionar choferes y su documentación (Módulo 3)

**Input**: Documentos de diseño de `/specs/003-gestion-choferes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: se incluyen. El plan fija los proyectos de test y `quickstart.md` enumera siete escenarios
que los tests automatizados cubren mejor que el recorrido manual; esas tareas están marcadas y
referencian el escenario que verifican.

**Organization**: las tareas se agrupan por historia de usuario para poder implementarlas y probarlas
de a una.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: se puede hacer en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1…US7)
- Toda tarea lleva la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados, tal como fija la constitución:

- Backend por capa: `backend/src/GT.Api`, `GT.Application`, `GT.Domain`, `GT.Infrastructure`
- Tests: `backend/tests/GT.UnitTests`, `backend/tests/GT.IntegrationTests`
- Frontend por módulo de negocio: `frontend/src/modules/choferes/`

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar el esqueleto del módulo y el volumen de adjuntos listos

- [X] T001 Agregar el volumen de archivos adjuntos al servicio del backend en `docker-compose.yml`, montado en la ruta que consume el módulo
- [X] T002 [P] Documentar la variable `GT_ARCHIVOS_RUTA` con su valor por defecto en `.env.template`
- [X] T003 [P] Crear las carpetas del módulo en el backend: `backend/src/GT.Api/Choferes/`, `backend/src/GT.Application/Choferes/Transportistas/`, `backend/src/GT.Application/Choferes/Documentacion/`, `backend/src/GT.Domain/Choferes/`, `backend/src/GT.Infrastructure/Archivos/`
- [X] T004 [P] Crear las carpetas del módulo en el frontend: `frontend/src/modules/choferes/paginas/`, `componentes/`, `servicios/`, `transportistas/` y `documentacion/`

---

## Phase 2: Foundational (prerrequisitos bloqueantes)

**Purpose**: dominio, persistencia y autorización que **todas** las historias necesitan

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Entidades de dominio

- [X] T005 [P] Crear la entidad `Transportista` en `backend/src/GT.Domain/Choferes/Transportista.cs` con nombre, CUIT, tipo, teléfono, email y `Activo`
- [X] T006 [P] Crear el enum `TipoPersona` con `Fisica` y `Juridica` en `backend/src/GT.Domain/Choferes/TipoPersona.cs`
- [X] T007 [P] Crear la entidad `Chofer` en `backend/src/GT.Domain/Choferes/Chofer.cs` con `PersonaId` único, `Cuil`, `TransportistaId` obligatorio y `Activo` (composición sobre Persona, research §1)
- [X] T008 [P] Crear la entidad `DocumentacionTipo` en `backend/src/GT.Domain/Choferes/DocumentacionTipo.cs` con nombre único, `DiasAvisoVencimiento` y `Activo`
- [X] T009 [P] Crear la entidad `Documentacion` en `backend/src/GT.Domain/Choferes/Documentacion.cs` **sin** columna de estado ni `Activo`, con `ArchivoRuta`, `ArchivoNombre` y `ArchivoTipoContenido` nulos
- [X] T010 [P] Crear el enum `DocumentacionEstado` con `Vigente`, `ProximaAvencer` y `Vencida` en `backend/src/GT.Domain/Choferes/DocumentacionEstado.cs`

### Reglas puras del dominio

- [X] T011 [P] Implementar `CalculadorEstadoDocumento` en `backend/src/GT.Domain/Choferes/CalculadorEstadoDocumento.cs` según FR-017, con "vence hoy" como `ProximaAvencer`, ventana cero sin período intermedio y el día en curso resuelto en hora de Argentina, UTC−3 (FR-017a)
- [X] T012 [P] Implementar `CalculadorEstadoChofer` en `backend/src/GT.Domain/Choferes/CalculadorEstadoChofer.cs` con los cuatro valores de FR-029 y la precedencia `vencida` > `proximaAvencer` > `enRegla`
- [X] T013 [P] Implementar `ValidadorCuit` con dígito verificador argentino en `backend/src/GT.Domain/Choferes/ValidadorCuit.cs`, válido para CUIT y CUIL
- [X] T014 [P] Implementar `MayoriaDeEdad` en `backend/src/GT.Domain/Choferes/MayoriaDeEdad.cs` según FR-011
- [X] T015 [P] Implementar `NormalizadorDocumentoNumerico` en `backend/src/GT.Domain/Choferes/NormalizadorDocumentoNumerico.cs`, que deja sólo dígitos para DNI, CUIL y CUIT (FR-025)

### Tests unitarios de las reglas puras

- [X] T016 [P] Tests de `CalculadorEstadoDocumento` en `backend/tests/GT.UnitTests/Choferes/CalculadorEstadoDocumentoTests.cs`, cubriendo los dos bordes de `quickstart.md` (vence exactamente hoy, tipo con 0 días de aviso), el cambio de estado al pasar el día (FR-019) y que el corte del día sea en UTC−3 aunque el reloj del sistema esté en otra zona (FR-017a)
- [X] T017 [P] Tests de `CalculadorEstadoChofer` en `backend/tests/GT.UnitTests/Choferes/CalculadorEstadoChoferTests.cs`, cubriendo que `sinDocumentacion` no es `enRegla` y que ningún tipo es obligatorio (FR-029a)
- [X] T018 [P] Tests de `ValidadorCuit` en `backend/tests/GT.UnitTests/Choferes/ValidadorCuitTests.cs` con casos válidos e inválidos conocidos
- [X] T019 [P] Tests de `MayoriaDeEdad` en `backend/tests/GT.UnitTests/Choferes/MayoriaDeEdadTests.cs`, incluido el cumpleaños número 18 exacto
- [X] T020 [P] Tests de `NormalizadorDocumentoNumerico` en `backend/tests/GT.UnitTests/Choferes/NormalizadorDocumentoNumericoTests.cs`, comprobando que `20-12345678-3` y `20123456783` normalizan igual

### Persistencia

- [X] T021 [P] Configuración EF de `Transportista` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/TransportistaConfiguracion.cs` con índice único de `Cuit`
- [X] T022 [P] Configuración EF de `Chofer` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/ChoferConfiguracion.cs` con índices únicos de `PersonaId` y `Cuil`, e índice común de `TransportistaId`
- [X] T023 [P] Configuración EF de `DocumentacionTipo` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/DocumentacionTipoConfiguracion.cs` con índice único de `Nombre`
- [X] T024 [P] Configuración EF de `Documentacion` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/DocumentacionConfiguracion.cs` con el índice compuesto `ChoferId, DocumentacionTipoId, FechaVencimiento DESC` (research §8) y el de `FechaVencimiento`
- [X] T025 Registrar los cuatro `DbSet` y aplicar las configuraciones en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs` (depende de T021–T024)
- [X] T026 Generar la migración con las cuatro tablas, sus índices y las claves foráneas con `DeleteBehavior.Restrict` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, sin sembrar ningún dato de negocio
- [X] T027 Sembrar el permiso `choferes.gestionar` y otorgarlo a los roles *Tráfico* y *Administrador del sistema* en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`

### Integración con los módulos anteriores

- [X] T028 Agregar la navegación inversa hacia `Chofer` en `backend/src/GT.Domain/Personas/Persona.cs`, sin tocar ninguna columna existente
- [X] T029 Extender la validación de baja de persona para que también rechace si la persona tiene un chofer asociado, en `backend/src/GT.Application/Usuarios/Personas/` (único cambio de comportamiento del Módulo 2, research §7)
- [X] T030 Registrar el grupo de endpoints del módulo bajo `/api` exigiendo el permiso `choferes.gestionar` en `backend/src/GT.Api/Program.cs`
- [ ] T031 Agregar las entradas *Choferes*, *Transportistas* y *Tipos de documentación*, atadas a `choferes.gestionar`, en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`

### Base compartida del módulo

- [X] T032 Crear `backend/src/GT.Application/Choferes/Mensajes.cs` con los textos en español rioplatense y los códigos de error del contrato (`contracts/README.md`)
- [X] T033 Crear `backend/src/GT.Application/Choferes/Dtos.cs` con los DTO comunes del módulo según `contracts/choferes-api.yaml`
- [ ] T034 Registrar las rutas del módulo en `frontend/src/App.tsx`
- [ ] T035 [P] Crear el cliente HTTP del módulo en `frontend/src/modules/choferes/servicios/api.ts`, reutilizando `frontend/src/compartido/clienteHttp.ts`

**Checkpoint**: base lista — las historias pueden empezar

---

## Phase 3: User Story 1 - Registrar un transportista (Priority: P1) 🎯 MVP

**Goal**: que Tráfico pueda cargar el padrón de transportistas, incluida G&T Logística S.A., para
tener a quién asignarle choferes.

**Independent Test**: con el padrón vacío, abrir *Transportistas*, ver el mensaje de padrón vacío,
cargar G&T Logística S.A. y un terciarizado, y verlos en el listado.

### Tests de la historia

- [ ] T036 [P] [US1] Test de integración de unicidad de CUIT y de formato con dígito verificador en `backend/tests/GT.IntegrationTests/Choferes/TransportistasTests.cs`
- [ ] T037 [P] [US1] Test de integración de normalización de CUIT antes de validar unicidad en `backend/tests/GT.IntegrationTests/Choferes/TransportistasNormalizacionTests.cs`

### Implementación

- [ ] T038 [P] [US1] Implementar `CrearTransportista` en `backend/src/GT.Application/Choferes/Transportistas/CrearTransportista.cs` con validación de CUIT único, formato y tipo de persona obligatorio
- [ ] T039 [P] [US1] Implementar `ConsultarTransportistas` en `backend/src/GT.Application/Choferes/Transportistas/ConsultarTransportistas.cs` con búsqueda por nombre o CUIT y filtro `soloActivos`
- [ ] T040 [US1] Implementar el repositorio de transportistas en `backend/src/GT.Infrastructure/Persistencia/RepositorioTransportistas.cs`
- [ ] T041 [US1] Exponer `GET /transportistas`, `POST /transportistas` y `GET /transportistas/{id}` en `backend/src/GT.Api/Choferes/TransportistasEndpoints.cs`
- [ ] T042 [P] [US1] Crear el servicio de transportistas en `frontend/src/modules/choferes/transportistas/servicioTransportistas.ts`
- [ ] T043 [US1] Crear la pantalla de listado en `frontend/src/modules/choferes/transportistas/ListadoTransportistas.tsx`, con búsqueda, columna de choferes activos y mensaje explícito de padrón vacío
- [ ] T044 [US1] Crear el formulario de alta en `frontend/src/modules/choferes/transportistas/FormularioTransportista.tsx`, con validación de CUIT y tipo de persona obligatorio
- [ ] T045 [P] [US1] Test de frontend del estado vacío y del error de CUIT duplicado en `frontend/src/modules/choferes/transportistas/ListadoTransportistas.test.tsx`

**Checkpoint**: el padrón de transportistas funciona de punta a punta

---

## Phase 4: User Story 2 - Registrar un chofer y asignarlo a su transportista (Priority: P1)

**Goal**: registrar choferes reutilizando el padrón de personas del Módulo 2 y dejando claro a qué
transportista pertenecen.

**Independent Test**: con al menos un transportista cargado, completar el formulario de chofer con
datos válidos y verlo en el listado, activo y con su transportista.

**Depende de**: US1 (necesita al menos un transportista activo).

### Tests de la historia

- [ ] T046 [P] [US2] Test de integración de reutilización de persona por DNI existente, y de rechazo cuando esa persona ya es chofer, en `backend/tests/GT.IntegrationTests/Choferes/CrearChoferTests.cs`
- [ ] T047 [P] [US2] Test de integración de CUIL duplicado y de rechazo por menor de edad en `backend/tests/GT.IntegrationTests/Choferes/CrearChoferValidacionesTests.cs`
- [ ] T048 [P] [US2] Test de integración que verifica el rechazo del alta sin transportista o con transportista inactivo en `backend/tests/GT.IntegrationTests/Choferes/CrearChoferTransportistaTests.cs`

### Implementación

- [ ] T049 [US2] Implementar `CrearChofer` en `backend/src/GT.Application/Choferes/CrearChofer.cs`: normalizar DNI, buscar la persona en el padrón, reutilizarla o crearla, y crear la fila de chofer
- [ ] T050 [US2] Implementar el repositorio de choferes en `backend/src/GT.Infrastructure/Persistencia/RepositorioChoferes.cs`
- [ ] T051 [US2] Exponer `POST /choferes` en `backend/src/GT.Api/Choferes/ChoferesEndpoints.cs`, devolviendo el detalle del chofer creado
- [ ] T052 [P] [US2] Crear el servicio de choferes en `frontend/src/modules/choferes/servicios/servicioChoferes.ts`
- [ ] T053 [US2] Crear el formulario de chofer en `frontend/src/modules/choferes/paginas/FormularioChofer.tsx`, con selector de transportistas activos
- [ ] T054 [US2] Agregar al formulario el aviso de reutilización de persona y el bloqueo con enlace cuando no hay transportistas activos, en `frontend/src/modules/choferes/paginas/FormularioChofer.tsx`
- [ ] T055 [P] [US2] Test de frontend del bloqueo sin transportistas activos en `frontend/src/modules/choferes/paginas/FormularioChofer.test.tsx`

**Checkpoint**: se pueden registrar choferes sin duplicar el padrón de personas

---

## Phase 5: User Story 3 - Cargar, corregir y eliminar documentación (Priority: P1)

**Goal**: que cada chofer tenga su documentación con archivo escaneado y estado calculado, y que un
documento mal cargado se pueda arreglar o eliminar.

**Independent Test**: sobre un chofer registrado, cargar tres documentos con vencimiento lejano,
cercano y pasado, y ver que salen `Al día`, `Próxima a vencer` y `Vencida` sin que nadie eligiera el
estado.

**Depende de**: US2 (necesita un chofer) y **US6** (necesita al menos un tipo en el catálogo).

### Almacén de archivos

- [ ] T056 [P] [US3] Implementar `ValidadorArchivo` en `backend/src/GT.Infrastructure/Archivos/ValidadorArchivo.cs`, que acepta sólo PDF, JPG y PNG de hasta 10 MB validando **la firma del archivo**, no la extensión (FR-015a)
- [ ] T057 [US3] Implementar `AlmacenDeArchivos` en `backend/src/GT.Infrastructure/Archivos/AlmacenDeArchivos.cs`, que guarda con nombre generado por el sistema bajo `GT_ARCHIVOS_RUTA`, recupera y borra, sin aceptar nunca el nombre cargado por el usuario como ruta
- [ ] T058 [P] [US3] Tests unitarios de `ValidadorArchivo` en `backend/tests/GT.UnitTests/Choferes/ValidadorArchivoTests.cs`, incluido un archivo con extensión `.pdf` que no es un PDF

### Tests de la historia

- [ ] T059 [P] [US3] Test de integración del cálculo de estado sobre documentos cargados en `backend/tests/GT.IntegrationTests/Choferes/DocumentacionEstadoTests.cs`
- [ ] T060 [P] [US3] Test de integración del rechazo de vencimiento anterior a la emisión y de tipo inactivo en `backend/tests/GT.IntegrationTests/Choferes/DocumentacionValidacionesTests.cs`
- [ ] T061 [P] [US3] Test de integración de **atomicidad** en `backend/tests/GT.IntegrationTests/Choferes/DocumentacionAtomicidadTests.cs`: con el almacén sustituido por uno que falla, la carga no crea el documento, y la corrección con archivo de reemplazo deja el documento y su adjunto anterior intactos (FR-015e, research §10)
- [ ] T062 [P] [US3] Test de integración de eliminación en `backend/tests/GT.IntegrationTests/Choferes/EliminarDocumentoTests.cs`: la fila y el archivo desaparecen, y al eliminar el vigente de un tipo el anterior vuelve a mandar
- [ ] T063 [P] [US3] Test de integración que verifica que la descarga del archivo sin sesión responde `401` y sin el permiso responde `403`, en `backend/tests/GT.IntegrationTests/Choferes/DescargaArchivoTests.cs` (FR-024, SC-011)

### Implementación

- [ ] T064 [US3] Implementar `CargarDocumento` en `backend/src/GT.Application/Choferes/Documentacion/CargarDocumento.cs`, coordinando archivo y fila en el orden de research §10 y compensando el archivo si la transacción falla
- [ ] T065 [US3] Implementar `CorregirDocumento` en `backend/src/GT.Application/Choferes/Documentacion/CorregirDocumento.cs`, con las validaciones del alta, conservando el adjunto si no viene uno nuevo y borrando el viejo recién después de confirmar
- [ ] T066 [US3] Implementar `EliminarDocumento` en `backend/src/GT.Application/Choferes/Documentacion/EliminarDocumento.cs`, con borrado físico de la fila y después del archivo
- [ ] T067 [US3] Exponer `POST /choferes/{id}/documentacion`, `PUT /documentacion/{id}`, `DELETE /documentacion/{id}` y `GET /documentacion/{id}/archivo` en `backend/src/GT.Api/Choferes/DocumentacionEndpoints.cs`, con los códigos `archivo_no_admitido` y `archivo_no_guardado`
- [ ] T068 [P] [US3] Crear el servicio de documentación en `frontend/src/modules/choferes/documentacion/servicioDocumentacion.ts`
- [ ] T069 [US3] Crear el formulario de carga y corrección en `frontend/src/modules/choferes/documentacion/FormularioDocumento.tsx`, **sin ningún campo de estado**, informando los formatos y el tamaño admitidos antes de subir
- [ ] T070 [US3] Conservar lo tipeado y mostrar el mensaje de `archivo_no_guardado` cuando la carga falla, en `frontend/src/modules/choferes/documentacion/FormularioDocumento.tsx`
- [ ] T071 [US3] Agregar la confirmación de eliminación, con el texto que advierte que no se puede deshacer, reutilizando el diálogo del Módulo 2, en `frontend/src/modules/choferes/documentacion/EliminarDocumento.tsx`
- [ ] T072 [P] [US3] Test de frontend que verifica que el formulario no expone ningún control de estado en `frontend/src/modules/choferes/documentacion/FormularioDocumento.test.tsx` (FR-018, SC-004)

**Checkpoint**: la documentación se carga, se corrige y se elimina, con el estado calculado por el sistema

---

## Phase 6: User Story 4 - Consultar choferes y el estado de su documentación (Priority: P1)

**Goal**: el listado y la ficha que responden, antes de asignar un viaje, si el chofer está en
condiciones.

**Independent Test**: con choferes de distintos transportistas y documentación en los tres estados,
aplicar combinaciones de filtros y ver que el listado y la ficha muestran exactamente lo esperado.

**Depende de**: US2 y US3 (necesita choferes con documentación para que los filtros signifiquen algo).

### Tests de la historia

- [ ] T073 [P] [US4] Test de integración del **documento vigente de cada tipo** en `backend/tests/GT.IntegrationTests/Choferes/DocumentoVigenteTests.cs`: manda el de vencimiento más lejano, y con la misma fecha manda el de `Id` mayor (research §8)
- [ ] T074 [P] [US4] Test de integración del filtro por estado de documentación en `backend/tests/GT.IntegrationTests/Choferes/FiltroEstadoDocumentacionTests.cs`, verificando que se resuelve en la base
- [ ] T075 [P] [US4] Test de integración de **paginación** en `backend/tests/GT.IntegrationTests/Choferes/PaginacionChoferesTests.cs`: 25 choferes dan 20 + 5 con `total` en 25, ninguna fila aparece en dos páginas y el orden se repite entre consultas iguales
- [ ] T076 [P] [US4] Test de integración del filtro por defecto en `backend/tests/GT.IntegrationTests/Choferes/ListadoPorDefectoTests.cs`: sin `estado`, sólo devuelve activos

### Implementación

- [ ] T077 [P] [US4] Crear `PaginaDe` en `backend/src/GT.Application/Choferes/PaginaDe.cs` con `items`, `total`, `pagina` y `tamanioPagina`
- [ ] T078 [US4] Implementar `ConsultarChoferes` en `backend/src/GT.Application/Choferes/ConsultarChoferes.cs`: filtros combinados, activos por defecto, estado calculado y documento vigente por tipo resueltos en SQL, orden `Apellido, Nombre, Id` y página de 20
- [ ] T079 [US4] Implementar `ConsultarFichaChofer` en `backend/src/GT.Application/Choferes/ConsultarFichaChofer.cs`, devolviendo todos los documentos agrupados por tipo con el vigente primero y la marca de reemplazado
- [ ] T080 [US4] Exponer `GET /choferes` y `GET /choferes/{id}` en `backend/src/GT.Api/Choferes/ChoferesEndpoints.cs`
- [ ] T081 [US4] Crear el listado en `frontend/src/modules/choferes/paginas/ListadoChoferes.tsx`, con las cinco columnas, los cinco filtros y el filtro de estado con `Activo` visible de entrada
- [ ] T082 [US4] Crear el control de paginación en `frontend/src/modules/choferes/componentes/Paginacion.tsx`, mostrando el total y volviendo a la página 1 al cambiar cualquier filtro
- [ ] T083 [US4] Crear la ficha del chofer en `frontend/src/modules/choferes/paginas/FichaChofer.tsx`, con sus datos, su transportista y sus documentos, marcando los reemplazados y los que no tienen archivo
- [ ] T084 [US4] Agregar los mensajes explícitos de listado vacío y de sin resultados en `frontend/src/modules/choferes/paginas/ListadoChoferes.tsx`
- [ ] T085 [P] [US4] Test de frontend de los estados vacíos y del filtro por defecto en `frontend/src/modules/choferes/paginas/ListadoChoferes.test.tsx`

**Checkpoint**: se puede responder quién tiene la documentación al día y quién no

---

## Phase 7: User Story 5 - Detectar documentación próxima a vencer o vencida (Priority: P2)

**Goal**: el panel que muestra, al entrar al módulo, qué choferes necesitan renovar algo.

**Independent Test**: cargar documentos con vencimiento dentro y fuera de la ventana de aviso de su
tipo, y ver que sólo los primeros aparecen en el panel.

**Depende de**: US3 y US4.

### Tests de la historia

- [ ] T086 [P] [US5] Test de integración del panel en `backend/tests/GT.IntegrationTests/Choferes/VencimientosTests.cs`: entran sólo los documentos vigentes de cada tipo, y un chofer inactivo no aparece aunque tenga todo vencido
- [ ] T087 [P] [US5] Test de integración de que cargar una renovación saca la alerta sin tocar el documento anterior, en `backend/tests/GT.IntegrationTests/Choferes/RenovacionSacaAlertaTests.cs` (SC-010)

### Implementación

- [ ] T088 [US5] Implementar `ConsultarVencimientos` en `backend/src/GT.Application/Choferes/Documentacion/ConsultarVencimientos.cs`, filtrando choferes activos y documentos vigentes de cada tipo, con los días que faltan o pasaron
- [ ] T089 [US5] Exponer `GET /vencimientos` en `backend/src/GT.Api/Choferes/DocumentacionEndpoints.cs`
- [ ] T090 [US5] Crear el panel en `frontend/src/modules/choferes/paginas/PanelVencimientos.tsx`, ordenado por urgencia y con enlace a la ficha de cada chofer
- [ ] T091 [US5] Agregar el mensaje explícito de que no hay vencimientos pendientes en `frontend/src/modules/choferes/paginas/PanelVencimientos.tsx`
- [ ] T092 [P] [US5] Test de frontend del panel sin alertas en `frontend/src/modules/choferes/paginas/PanelVencimientos.test.tsx`

**Checkpoint**: los vencimientos se ven solos, sin que nadie ejecute nada

---

## Phase 8: User Story 6 - Mantener el catálogo de tipos de documentación (Priority: P2)

**Goal**: administrar los tipos de documento y con cuántos días de anticipación avisa cada uno.

**Independent Test**: crear un tipo con 30 días de aviso, cargar un documento que vence en 20 días,
ver que sale `Próxima a vencer`, cambiar el tipo a 10 días y ver que pasa a `Al día`.

> **Nota de orden**: aunque es P2, el catálogo tiene que existir antes de poder cargar cualquier
> documento (US3). Ver *Orden recomendado* más abajo.

### Tests de la historia

- [ ] T093 [P] [US6] Test de integración de nombre duplicado y de días de aviso negativos en `backend/tests/GT.IntegrationTests/Choferes/TiposDocumentacionTests.cs`
- [ ] T094 [P] [US6] Test de integración del rechazo de baja de un tipo con documentos asociados, con la cantidad en el mensaje, en `backend/tests/GT.IntegrationTests/Choferes/BajaTipoDocumentacionTests.cs`
- [ ] T095 [P] [US6] Test de integración de que cambiar los días de aviso recalcula el estado de los documentos existentes sin actualizar ninguna fila, en `backend/tests/GT.IntegrationTests/Choferes/RecalculoPorDiasAvisoTests.cs`

### Implementación

- [ ] T096 [P] [US6] Implementar `GestionTiposDocumentacion` en `backend/src/GT.Application/Choferes/Documentacion/GestionTiposDocumentacion.cs` con alta, consulta, modificación y baja lógica, y el rechazo de baja con documentos asociados
- [ ] T097 [US6] Implementar el repositorio de tipos en `backend/src/GT.Infrastructure/Persistencia/RepositorioTiposDocumentacion.cs`
- [ ] T098 [US6] Exponer `GET`, `POST`, `PUT` y `DELETE` de `/tipos-documentacion` en `backend/src/GT.Api/Choferes/TiposDocumentacionEndpoints.cs`
- [ ] T099 [P] [US6] Crear el servicio de tipos en `frontend/src/modules/choferes/documentacion/servicioTipos.ts`
- [ ] T100 [US6] Crear el listado y el formulario de tipos en `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`, mostrando cuántos documentos usa cada uno
- [ ] T101 [US6] Agregar la confirmación de baja de tipo y el mensaje de catálogo vacío en `frontend/src/modules/choferes/documentacion/TiposDocumentacion.tsx`

**Checkpoint**: el catálogo se administra y la ventana de aviso de cada tipo se puede ajustar

---

## Phase 9: User Story 7 - Modificar y dar de baja choferes y transportistas (Priority: P3)

**Goal**: mantener el padrón fiel a la realidad: corregir datos, reasignar de transportista y dar de
baja sin perder historia.

**Independent Test**: editar el teléfono de un chofer, reasignarlo a otro transportista, darlo de
baja, y comprobar que sale del listado sin filtros pero aparece filtrando por inactivo, con su
documentación intacta.

**Depende de**: US1, US2 y US4.

### Tests de la historia

- [ ] T102 [P] [US7] Test de integración de reasignación de transportista que conserva la documentación en `backend/tests/GT.IntegrationTests/Choferes/ReasignarChoferTests.cs` (SC-009)
- [ ] T103 [P] [US7] Test de integración de unicidad en modificación, que excluye al propio registro, en `backend/tests/GT.IntegrationTests/Choferes/ModificacionUnicidadTests.cs`
- [ ] T104 [P] [US7] Test de integración del rechazo de baja de transportista con choferes activos, y de la baja que procede cuando todos están inactivos, en `backend/tests/GT.IntegrationTests/Choferes/BajaTransportistaTests.cs`
- [ ] T105 [P] [US7] Test de integración de que el chofer dado de baja sale del listado por defecto y del panel de vencimientos, en `backend/tests/GT.IntegrationTests/Choferes/BajaChoferTests.cs`

### Implementación

- [ ] T106 [P] [US7] Implementar `ModificarChofer` en `backend/src/GT.Application/Choferes/ModificarChofer.cs`, actualizando la persona del padrón y permitiendo la reasignación de transportista
- [ ] T107 [P] [US7] Implementar `DarDeBajaChofer` en `backend/src/GT.Application/Choferes/DarDeBajaChofer.cs` con baja lógica
- [ ] T108 [P] [US7] Implementar `ModificarTransportista` en `backend/src/GT.Application/Choferes/Transportistas/ModificarTransportista.cs`
- [ ] T109 [US7] Implementar `DarDeBajaTransportista` en `backend/src/GT.Application/Choferes/Transportistas/DarDeBajaTransportista.cs`, rechazando si tiene choferes activos e informando cuántos
- [ ] T110 [US7] Exponer `PUT /choferes/{id}` y `DELETE /choferes/{id}` en `backend/src/GT.Api/Choferes/ChoferesEndpoints.cs`
- [ ] T111 [US7] Exponer `PUT /transportistas/{id}` y `DELETE /transportistas/{id}` en `backend/src/GT.Api/Choferes/TransportistasEndpoints.cs`
- [ ] T112 [US7] Habilitar la edición y la reasignación desde `frontend/src/modules/choferes/paginas/FormularioChofer.tsx`
- [ ] T113 [US7] Agregar las confirmaciones de baja de chofer y de transportista, con sus textos, reutilizando el diálogo del Módulo 2, en `frontend/src/modules/choferes/componentes/ConfirmacionBaja.tsx`
- [ ] T114 [US7] Habilitar la edición de transportistas desde `frontend/src/modules/choferes/transportistas/FormularioTransportista.tsx`
- [ ] T115 [P] [US7] Test de frontend de que cancelar una confirmación no dispara ninguna llamada en `frontend/src/modules/choferes/componentes/ConfirmacionBaja.test.tsx` (SC-008)

- [ ] T116 [US7] Implementar `ReactivarChofer` en `backend/src/GT.Application/Choferes/ReactivarChofer.cs`, que vuelve `Activo` a `true` y rechaza si el chofer ya está activo o si su transportista quedó inactivo (FR-005b)
- [ ] T117 [US7] Exponer `POST /choferes/{id}/reactivacion` en `backend/src/GT.Api/Choferes/ChoferesEndpoints.cs`
- [ ] T118 [US7] Mostrar *Reactivar* en lugar de *Dar de baja* cuando el chofer está inactivo, con su confirmación, en `frontend/src/modules/choferes/paginas/FichaChofer.tsx`

**Checkpoint**: las siete historias funcionan

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T119 [P] Verificar que el estado de documentación nunca se comunique sólo por color y que los documentos reemplazados lleven la palabra además del gris, en `frontend/src/modules/choferes/`
- [ ] T120 [P] Anunciar con `role="status"` el resultado de la carga de archivo y el cambio de página, en `frontend/src/modules/choferes/`
- [ ] T121 [P] Revisar que los textos de `backend/src/GT.Application/Choferes/Mensajes.cs` y los de `frontend/src/modules/choferes/` estén en español rioplatense y coincidan con `contracts/README.md`
- [ ] T122 Revisar que ningún endpoint del módulo quede sin el permiso `choferes.gestionar`, incluida la descarga de archivos, en `backend/src/GT.Api/Choferes/`
- [ ] T123 Correr el recorrido completo de `specs/003-gestion-choferes/quickstart.md` con las dos cuentas (`admin` y un usuario de Tráfico)
- [ ] T124 Correr `dotnet test` en `backend/` y `npm test` en `frontend/`, y dejar ambos en verde
- [ ] T125 [P] Actualizar `specs/README.md` con el estado del Módulo 3
- [ ] T126 Puesta en marcha: cargar **G&T Logística S.A.** como transportista con sus datos reales desde la pantalla de transportistas, y dejar el paso anotado en `specs/003-gestion-choferes/quickstart.md` como parte del alta del módulo (FR-004). No se siembra por migración: se carga como cualquier otro transportista

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende del Setup — **bloquea todas las historias**
- **Historias (Phase 3+)**: todas dependen de la Fase 2
- **Polish (Phase 10)**: depende de las historias que se quieran entregar

### User Story Dependencies

Este módulo **no** tiene siete historias independientes entre sí, y conviene decirlo antes de
repartir el trabajo:

| Historia | Depende de | Por qué |
|---|---|---|
| US1 Transportistas | — | Es la única que arranca sola |
| US2 Chofer | US1 | Todo chofer necesita un transportista activo (FR-008) |
| US3 Documentación | US2 y **US6** | Necesita un chofer y un tipo del catálogo |
| US4 Consulta | US2, US3 | Sin documentación cargada, los filtros por estado no significan nada |
| US5 Panel | US3, US4 | Es una vista sobre los mismos datos |
| US6 Tipos | — | Arranca sola, pero **US3 la necesita** |
| US7 Modificar y bajas | US1, US2, US4 | Opera sobre lo que las otras crearon |

### Orden recomendado de ejecución

**US1 → US6 → US2 → US3 → US4 → US5 → US7**

US6 es P2 pero se adelanta: el catálogo arranca vacío y sin un tipo cargado no se puede probar
ninguna parte de US3, que es P1. Es la única alteración del orden de prioridades, y responde a una
dependencia real, no a una preferencia.

### Parallel Opportunities

- Fase 1: T002, T003 y T004 en paralelo
- Fase 2: las seis entidades (T005–T010) en paralelo; después las cinco reglas puras (T011–T015) en
  paralelo; después los cinco tests unitarios (T016–T020) y las cuatro configuraciones EF (T021–T024)
  en paralelo
- Dentro de cada historia: los tests de integración entre sí, y el servicio del frontend en paralelo
  con la implementación del backend
- Entre historias: con la Fase 2 terminada, **US1 y US6 pueden ir en paralelo** desde el principio,
  que es la única paralelización real de historias que este módulo admite

---

## Parallel Example: Fase 2, entidades y reglas

```bash
# Las seis entidades de dominio, juntas:
Task: "Crear la entidad Transportista en backend/src/GT.Domain/Choferes/Transportista.cs"
Task: "Crear el enum TipoPersona en backend/src/GT.Domain/Choferes/TipoPersona.cs"
Task: "Crear la entidad Chofer en backend/src/GT.Domain/Choferes/Chofer.cs"
Task: "Crear la entidad DocumentacionTipo en backend/src/GT.Domain/Choferes/DocumentacionTipo.cs"
Task: "Crear la entidad Documentacion en backend/src/GT.Domain/Choferes/Documentacion.cs"
Task: "Crear el enum DocumentacionEstado en backend/src/GT.Domain/Choferes/DocumentacionEstado.cs"

# Después, las cinco reglas puras, juntas:
Task: "Implementar CalculadorEstadoDocumento en backend/src/GT.Domain/Choferes/CalculadorEstadoDocumento.cs"
Task: "Implementar CalculadorEstadoChofer en backend/src/GT.Domain/Choferes/CalculadorEstadoChofer.cs"
Task: "Implementar ValidadorCuit en backend/src/GT.Domain/Choferes/ValidadorCuit.cs"
Task: "Implementar MayoriaDeEdad en backend/src/GT.Domain/Choferes/MayoriaDeEdad.cs"
Task: "Implementar NormalizadorDocumentoNumerico en backend/src/GT.Domain/Choferes/NormalizadorDocumentoNumerico.cs"
```

---

## Implementation Strategy

### MVP (US1 + US6 + US2 + US3)

1. Fase 1: Setup
2. Fase 2: Foundational — bloquea todo
3. US1 (transportistas) y US6 (tipos), que pueden ir en paralelo
4. US2 (choferes)
5. US3 (documentación)
6. **PARAR Y VALIDAR**: en este punto ya se puede cargar un chofer con su documentación y ver el
   estado calculado, que es el corazón del módulo

El MVP no es US1 sola: registrar transportistas sin poder cargar un chofer no le sirve a nadie en
Tráfico. El primer incremento con valor real es poder registrar un chofer con su documentación.

### Entrega incremental

1. Setup + Foundational → base lista
2. US1 + US6 → padrones de apoyo cargables
3. US2 → choferes registrados
4. US3 → documentación con estado calculado ← **primer incremento con valor**
5. US4 → consulta y filtros
6. US5 → panel de vencimientos
7. US7 → correcciones y bajas
8. Fase 10 → accesibilidad, textos y validación completa

### Con más de una persona

Con la Fase 2 terminada: una persona toma US1 y después US2; otra toma US6 y después US3. Se
encuentran en US4, que necesita las dos ramas. US5 y US7 se reparten al final.

---

## Notes

- `[P]` = archivos distintos, sin dependencias pendientes
- Cada tarea nombra el archivo exacto; ninguna dice "agregar validaciones" sin decir dónde
- Las tareas de test referencian el escenario de `spec.md` o `quickstart.md` que verifican
- Conviene commitear por tarea o por grupo lógico
- Los 30 ítems abiertos de `checklists/documentacion.md` son deuda de spec y no bloquean estas
  tareas; si alguno se resuelve, puede agregar tareas acá
