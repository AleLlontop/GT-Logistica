# Tasks: Gestión de viajes (Módulo 5)

**Input**: Documentos de diseño de `/specs/005-gestion-viajes/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: se incluyen. El plan fija los proyectos de test y `quickstart.md` enumera ocho escenarios
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
- Frontend por módulo de negocio: `frontend/src/modules/viajes/`

## Lo que este módulo NO toca

Al revés del Módulo 4, que modificaba dos cosas del Módulo 3, **este módulo no modifica nada de los
Módulos 3 y 4**: ni una tabla, ni una columna, ni una navegación, ni un archivo de negocio. Los
consume tal como están (research §3).

Los únicos archivos ajenos al módulo que se editan son **cuatro puntos de extensión** que todos los
módulos anteriores ya tocaron por diseño:

| Archivo | Tarea | Qué se le agrega |
|---|---|---|
| `GT.Domain/Usuarios/Rol.cs` | T019 | Los dos códigos de permiso |
| `GT.Infrastructure/DatosIniciales/SembradorInicial.cs` | T020 | Los permisos y su reparto por rol |
| `GT.Api/Program.cs` | T021 | Políticas y registro de los grupos de endpoints |
| `GT.Application/Autenticacion/CatalogoOpcionesMenu.cs` | T022 | Las tres entradas de menú |

Más `GtDbContext.cs` (T016), `App.tsx` (rutas) y `compartido/moneda.ts` (T027, nuevo).

**Si una tarea te pide editar algo de `GT.Domain/Choferes/`, `GT.Domain/Flota/`, sus repositorios,
sus endpoints o sus pantallas, revisá el diseño antes de hacerlo**: la spec lo prohíbe explícitamente
y no hay ninguna tarea que lo requiera.

## Dos trampas de implementación, marcadas donde se caen

Las detectó `/speckit-analyze` y valen más acá que en un comentario de código:

1. **Toda ruta con `{id}` lleva la restricción `{id:int}`.** `/api/viajes/asignables` y
   `/api/viajes/totales` conviven con `/api/viajes/{id}`: sin la restricción las tres son ambiguas y
   las dos literales quedan inalcanzables. Afecta a T039, T057, T071, T088 y T112.
2. **`Viaje.Numero` no se declara `required`.** Se alimenta del `DEFAULT` de la secuencia, y una
   propiedad `required int` obliga al código a asignarla, con lo que EF manda el `0` que escribió el
   constructor y el default de la base nunca se aplica. Afecta a T006, T014 y T046.

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar el esqueleto del módulo listo

Este módulo **no agrega ninguna variable de entorno, ningún volumen y ninguna dependencia**:
`docker-compose.yml`, `.env.template` y los `.csproj` no se tocan.

- [X] T001 Crear las carpetas del módulo en el backend: `backend/src/GT.Api/Viajes/`, `backend/src/GT.Application/Viajes/`, `backend/src/GT.Application/Viajes/Clientes/`, `backend/src/GT.Domain/Viajes/`, `backend/tests/GT.UnitTests/Viajes/` y `backend/tests/GT.IntegrationTests/Viajes/`
- [X] T002 [P] Crear las carpetas del módulo en el frontend: `frontend/src/modules/viajes/paginas/`, `componentes/`, `servicios/` y `clientes/`

---

## Phase 2: Foundational (prerrequisitos bloqueantes)

**Purpose**: dominio, persistencia, autorización y los contratos compartidos que **todas** las
historias necesitan

**⚠️ CRITICAL**: ninguna historia puede empezar hasta terminar esta fase

### Entidades y enums de dominio

- [X] T003 [P] Crear el enum `EstadoViaje` con `Pendiente = 0`, `EnCurso = 1`, `Rendido = 2` y `Anulado = 3` en `backend/src/GT.Domain/Viajes/EstadoViaje.cs`, con un comentario que advierta que **los tres índices únicos filtrados dependen de estos números** y que reordenarlos los invalida sin fallar al compilar (FR-031, research §2)
- [X] T004 [P] Crear el enum `HabilitacionAsignacion` con `Habilitado`, `ConAdvertencia` y `Bloqueado` en `backend/src/GT.Domain/Viajes/HabilitacionAsignacion.cs`, documentado como **derivado y nunca almacenado** (FR-022, FR-023, FR-024)
- [X] T005 [P] Crear la entidad `Cliente` en `backend/src/GT.Domain/Viajes/Cliente.cs` con `RazonSocial`, `Cuit` normalizado, `Telefono`, `Email`, `Direccion` anulable, `Activo` y la colección `Viajes` que se cuenta al intentar la baja (FR-001, FR-002)
- [X] T006 [P] Crear la entidad `Viaje` en `backend/src/GT.Domain/Viajes/Viaje.cs` con `ClienteId`, `Fecha` (`DateOnly`), `Origen`, `Destino`, `NumeroRemito` anulable, `DetalleCarga` anulable, `Importe` (`decimal`), `Estado`, `MotivoAnulacion` anulable, y `ChoferId`, `VehiculoId` y `TransportistaId` anulables. **`Numero` se declara `public int Numero { get; private set; }`, sin `required` y sin asignación en el constructor**: lo genera la secuencia y el código nunca lo escribe (FR-011, trampa 2). Incluir la constante `DiasParaDemora = 5` y la regla pura `EstaDemorado(DateTime? enCursoDesde, DateTime ahora)` (FR-010, FR-039)
- [X] T007 [P] Crear la entidad `CambioDeEstadoViaje` en `backend/src/GT.Domain/Viajes/CambioDeEstadoViaje.cs` con `ViajeId`, `EstadoAnterior` **anulable** —`null` sólo en el registro del alta—, `EstadoNuevo`, `UsuarioId` y `OcurridoEn`, sin ninguna propiedad que permita editarlo (FR-035)

### Reglas puras del dominio

- [X] T008 [P] Implementar `TransicionesDeViaje` en `backend/src/GT.Domain/Viajes/TransicionesDeViaje.cs`, que responde si una transición está permitida: sólo `pendiente → enCurso`, `enCurso → rendido`, `pendiente → anulado` y `enCurso → anulado`; `rendido` y `anulado` son terminales y no tienen salida (FR-033)
- [X] T009 [P] Implementar `EvaluadorHabilitacion` en `backend/src/GT.Domain/Viajes/EvaluadorHabilitacion.cs`, que recibe los documentos de una unidad y **la fecha del viaje** y devuelve el veredicto de tres valores, **reutilizando `CalculadorEstadoDocumento` y la regla de vigente por tipo de los Módulos 3 y 4 sin modificarlos**: alguno vencido → `bloqueado`; ninguno vencido y alguno por vencer → `conAdvertencia`; todos vigentes o **ninguno cargado** → `habilitado` (FR-022, FR-023, FR-024, research §3)

### Tests unitarios de las reglas puras

- [X] T010 [P] Tests de `TransicionesDeViaje` en `backend/tests/GT.UnitTests/Viajes/TransicionesDeViajeTests.cs`, cubriendo las cuatro permitidas y las rechazadas que la spec nombra: `pendiente → rendido` (US4 esc. 10), `rendido → *` y `anulado → *` (FR-033)
- [X] T011 [P] Tests de `EvaluadorHabilitacion` en `backend/tests/GT.UnitTests/Viajes/EvaluadorHabilitacionTests.cs` con los bordes que a mano dependen del calendario: documento que vence **exactamente el día del viaje** —que es `conAdvertencia`, no `bloqueado`—, tipo con **0 días de aviso**, unidad **sin ningún documento** —que es `habilitado`—, y un documento vencido hoy pero vigente a la fecha de un viaje retroactivo (FR-024, SC-014, `quickstart.md` §Tests)
- [X] T012 [P] Tests de la regla de demora en `backend/tests/GT.UnitTests/Viajes/DemoraViajeTests.cs`, fijando el instante del pase a `en curso` y comprobando el borde exacto: **a los 5 días todavía no está demorado, pasados los 5 sí**, y que el estado guardado no cambia nunca (FR-039, `quickstart.md` §Tests)

### Persistencia

- [X] T013 [P] Configuración EF de `Cliente` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/ClienteConfiguracion.cs` con índice **único sin filtro** de `Cuit` —el CUIT de un cliente dado de baja sigue ocupado— y los largos de columna de `data-model.md` (FR-003)
- [X] T014 [P] Configuración EF de `Viaje` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/ViajeConfiguracion.cs` con: `Numero` mapeado con `HasDefaultValueSql("NEXT VALUE FOR dbo.NumeroDeViaje")` **y `ValueGeneratedOnAdd()`**, de modo que EF lo omita en el `INSERT` y lo recupere por `OUTPUT` (trampa 2); `Importe` como `decimal(18,2)` con `CHECK (Importe >= 0)`; las cuatro claves foráneas en `DeleteBehavior.Restrict`; y los siete índices, incluidos los **tres únicos filtrados** de `data-model.md` (FR-011, FR-013, FR-014, FR-026)
- [X] T015 [P] Configuración EF de `CambioDeEstadoViaje` en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CambioDeEstadoViajeConfiguracion.cs` con la FK al viaje en **cascada**, la FK al usuario en `Restrict` y el índice `ViajeId, OcurridoEn` (FR-035)
- [X] T016 Registrar los tres `DbSet` nuevos en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs` (depende de T013–T015)
- [X] T017 Generar la migración `Modulo5Viajes` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, que crea la secuencia `dbo.NumeroDeViaje` con **`NO CACHE`** y las tres tablas con sus índices y claves foráneas. **No modifica ninguna tabla existente y no siembra ningún dato de negocio**: los padrones arrancan vacíos y la numeración en 1 (research §1)
- [X] T018 Test de integración de los tres índices únicos filtrados en `backend/tests/GT.IntegrationTests/Viajes/IndicesFiltradosTests.cs`: insertar viajes en los cuatro estados y verificar que el de remito acepta duplicados **sólo** entre anulados, y que los de chofer y vehículo aceptan repetición en `pendiente`, `rendido` y `anulado` pero no en `enCurso`. Es lo que protege contra un reordenamiento futuro de `EstadoViaje` (research §2, §15)

### Autorización y menú

- [X] T019 Agregar las constantes `ViajesGestionar = "viajes.gestionar"` y `ViajesConsultar = "viajes.consultar"` a `CodigosPermiso` en `backend/src/GT.Domain/Usuarios/Rol.cs`
- [X] T020 Sembrar los dos permisos y repartirlos por rol en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`: `viajes.gestionar` a *Tráfico* y *Administrador del sistema*; `viajes.consultar` a **los cuatro roles**, incluidas *Administración de la empresa* y *Gerencia*, que hasta ahora no tenían ningún permiso (FR-051, research §10)
- [X] T021 Registrar las políticas de los dos permisos y mapear los cinco grupos de endpoints del módulo en `backend/src/GT.Api/Program.cs`, junto con el registro de los casos de uso y los dos repositorios en el contenedor
- [X] T022 Agregar las entradas *Viajes* (`/viajes`), *Clientes* (`/clientes`) y *Totales* (`/viajes/totales`) en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`, **las tres atadas a `viajes.consultar`**, porque las tres pantallas se pueden mirar sin poder tocar nada (FR-050, research §10)
- [X] T023 Test de integración del acceso por rol en `backend/tests/GT.IntegrationTests/Viajes/AccesoPorRolTests.cs`: que *Gerencia* llegue a los `GET` de viajes, clientes y totales y reciba `403` en todo `POST`, `PUT` y `DELETE`; que *Tráfico* llegue a todo; y que un rol sin permisos reciba `403` en los dos niveles (FR-052, SC-012, `quickstart.md` paso 1)

### Contratos compartidos de la capa de aplicación

- [X] T024 [P] Crear los DTO compartidos del módulo en `backend/src/GT.Application/Viajes/Dtos.cs`: `Resumen(Id, Nombre, Activo)`, `Advertencia(Codigo, Mensaje)` y el sobre `RespuestaViaje(Viaje, Advertencias)` que usan **sólo** las tres operaciones que pueden advertir —alta, edición y asignación—, reutilizando `PaginaDe<T>` del Módulo 3 sin tocarlo (FR-015a, research §5)
- [X] T025 [P] Crear `CodigosErrorViajes`, `MensajesViajes`, `ErrorConDependencias` y `ErrorDeBloqueo` en `backend/src/GT.Application/Viajes/Mensajes.cs`, con **los textos exactos** de `contracts/README.md` en español rioplatense y los códigos de `contracts/viajes-api.yaml`
- [X] T026 [P] Crear `NombresDeEstadoViaje` en `backend/src/GT.Application/Viajes/NombresDeEstadoViaje.cs`, con la serialización en **camelCase** (`enCurso`, no `EnCurso`) y la lectura de los valores de query, devolviendo `null` ante un valor desconocido para que el filtro se ignore en vez de romper (convención [003])

### Frontend compartido

- [X] T027 [P] Crear el formateador de moneda en `frontend/src/compartido/moneda.ts` con `Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS' })` y su test en `frontend/src/compartido/moneda.test.ts`, comprobando el formato `$ 1.240.000,00` —punto de miles, coma decimal— y el cero. Es el primer dinero del sistema (Principio II, research §11)
- [X] T028 [P] Crear el cliente HTTP del módulo en `frontend/src/modules/viajes/servicios/api.ts`, **sin repetir el prefijo `/api`**: es el defecto que dejó las 19 pantallas del Módulo 3 sin funcionar y que ningún test veía (`specs/README.md` §Lo que encontraron los recorridos)

**Checkpoint**: dominio, base de datos, permisos y contratos listos — las historias pueden empezar

---

## Phase 3: User Story 1 - Mantener el padrón de clientes (Priority: P1) 🎯 MVP

**Goal**: el padrón sin el cual no se puede registrar ningún viaje: alta, consulta, corrección, baja
lógica con confirmación y alta de nuevo.

**Independent Test**: abrir la pantalla de clientes con el padrón vacío, cargar dos clientes, y
comprobar que los dos quedan disponibles para elegir al registrar un viaje.

### Tests de la historia

- [X] T029 [P] [US1] Test de integración del CUIT en `backend/tests/GT.IntegrationTests/Viajes/CuitClienteTests.cs`: el CUIT es único **incluidos los dados de baja**, la modificación excluye al propio cliente, el CUIT mal formado se rechaza con el campo marcado, y el CUIT de un cliente dado de baja devuelve `cuit_de_cliente_dado_de_baja` y **no** `cuit_duplicado` (FR-003, FR-004, FR-007, US1 esc. 3, 4, 5 y 10)
- [X] T030 [P] [US1] Test de integración de la baja en `backend/tests/GT.IntegrationTests/Viajes/BajaClienteTests.cs`: se rechaza con al menos un viaje `pendiente` o `en curso`, informando `cantidadViajes` **en el cuerpo del error**; y **procede** cuando todos los viajes del cliente están `rendido` o `anulado`, que es el caso del que dejó de operar con la empresa (FR-006, SC-009, US1 esc. 6 y 8)
- [X] T031 [P] [US1] Test de integración del alta de nuevo en `backend/tests/GT.IntegrationTests/Viajes/AltaClienteTests.cs`: es **idempotente** —darle de alta a un cliente ya activo no cambia nada—, vuelve a ofrecerlo al registrar viajes, y sus viajes históricos quedan intactos (FR-007, US1 esc. 9)

### Implementación

- [X] T032 [US1] Declarar `IRepositorioClientes` en `backend/src/GT.Application/Viajes/Clientes/IRepositorioClientes.cs` con alta, consulta paginada, obtención por Id y por CUIT, y el conteo de viajes `pendiente` o `en curso` del cliente
- [X] T033 [US1] Implementar `RepositorioClientes` en `backend/src/GT.Infrastructure/Persistencia/RepositorioClientes.cs`, con la paginación de 20 filas, el orden total terminando en `Id`, y la traducción de la violación del índice único a una excepción de la capa de aplicación (convención [003], FR-009)
- [X] T034 [P] [US1] Implementar `CrearCliente` en `backend/src/GT.Application/Viajes/Clientes/CrearCliente.cs`, **reutilizando `ValidadorCuit` y `NormalizadorDocumentoNumerico` del Módulo 3 sin modificarlos**, normalizando antes de validar y de guardar, y distinguiendo el CUIT de un cliente dado de baja (FR-004, FR-007, research §13)
- [X] T035 [P] [US1] Implementar `ConsultarClientes` en `backend/src/GT.Application/Viajes/Clientes/ConsultarClientes.cs` con `soloActivos` declarado `bool?` con `?? false` y la búsqueda parcial por razón social (convención [003], FR-009)
- [X] T036 [P] [US1] Implementar `ModificarCliente` en `backend/src/GT.Application/Viajes/Clientes/ModificarCliente.cs`, con la comparación de CUIT excluyendo al propio cliente y **sin aceptar `activo` en el cuerpo** (FR-003, FR-007)
- [X] T037 [P] [US1] Implementar `DarDeBajaCliente` en `backend/src/GT.Application/Viajes/Clientes/DarDeBajaCliente.cs`, que rechaza sólo por viajes `pendiente` o `en curso` e informa cuántos son en el mensaje y en el cuerpo (FR-006)
- [X] T038 [P] [US1] Implementar `DarDeAltaCliente` en `backend/src/GT.Application/Viajes/Clientes/DarDeAltaCliente.cs`, **idempotente y sin confirmación aparte** (FR-007)
- [X] T039 [US1] Exponer los seis endpoints de cliente en `backend/src/GT.Api/Viajes/ClientesEndpoints.cs` según `contracts/viajes-api.yaml`, **con `{id:int}` en las cuatro rutas que llevan identificador** (trampa 1), los `GET` bajo `viajes.consultar` y las escrituras bajo `viajes.gestionar` (FR-053)
- [X] T040 [P] [US1] Crear `frontend/src/modules/viajes/clientes/servicioClientes.ts` con las seis llamadas del contrato
- [X] T041 [US1] Crear el listado en `frontend/src/modules/viajes/clientes/ListadoClientes.tsx` con las cinco columnas, la paginación, el mensaje de padrón vacío y el de sin coincidencias de `contracts/README.md`, mostrando los inactivos atenuados **y con la palabra `Inactivo`**; registrar su ruta `/clientes` en `frontend/src/App.tsx` (FR-009, FR-049, US1 esc. 1)
- [X] T042 [US1] Crear el formulario en `frontend/src/modules/viajes/clientes/FormularioCliente.tsx` con los cinco campos y sus largos, marcando el campo puntual ante cada rechazo; registrar sus rutas `/clientes/nuevo` y `/clientes/:id` en `frontend/src/App.tsx` (FR-002, FR-004)
- [X] T043 [US1] Crear la confirmación de baja en `frontend/src/modules/viajes/componentes/ConfirmacionBajaCliente.tsx` con el texto de `contracts/README.md`, **sin efecto al cancelar**, y sin confirmación para el alta de nuevo (FR-005, FR-007, US1 esc. 7)
- [X] T044 [P] [US1] Test de frontend del listado en `frontend/src/modules/viajes/clientes/ListadoClientes.test.tsx`: el mensaje de padrón vacío y la palabra `Inactivo` junto al atenuado
- [X] T045 [P] [US1] Test de frontend del formulario y de la confirmación en `frontend/src/modules/viajes/clientes/FormularioCliente.test.tsx`, comprobando que cancelar la baja **no dispara ninguna petición**

**Checkpoint**: el padrón de clientes funciona de punta a punta y ya se puede registrar un viaje

---

## Phase 4: User Story 2 - Registrar un viaje (Priority: P1)

**Goal**: el registro del trabajo comprometido, con su número propio, su remito único y su primera
fila de historial.

**Independent Test**: con al menos un cliente cargado, completar cliente, origen, destino y fecha,
guardar, y comprobar que el viaje aparece con estado `pendiente` y un número que el sistema asignó y
que nadie puede editar.

**Nota de alcance**: esta fase construye el listado y la ficha **básicos** —paginación y orden— porque
sin ellos la historia no se puede verificar. Los cuatro filtros, la búsqueda, las señales derivadas y
los estados vacíos son de US5.

### Tests de la historia

- [X] T046 [P] [US2] Test de integración del número en `backend/tests/GT.IntegrationTests/Viajes/NumeroDeViajeTests.cs`: arranca en 1, avanza de a uno, **no se reutiliza tras anular** el viaje que lo tenía, ningún cuerpo de petición puede fijarlo, y **el alta nunca envía `Numero` en el `INSERT`** —si el primer viaje sale con número 0, la propiedad quedó declarada mal (FR-011, SC-002, US2 esc. 5, trampa 2)
- [X] T047 [P] [US2] Test de integración del remito en `backend/tests/GT.IntegrationTests/Viajes/RemitoTests.cs`: es opcional, es único entre los **no anulados**, el rechazo **nombra el número del viaje que ya lo usa**, y el remito de un viaje anulado vuelve a estar libre (FR-014, SC-003, US2 esc. 8 y 9)
- [X] T048 [P] [US2] Test de integración de la carrera del remito en `backend/tests/GT.IntegrationTests/Viajes/RemitoConcurrenciaTests.cs`: dos altas simultáneas con el mismo remito; una gana y la otra recibe `remito_duplicado` (SC-003, `quickstart.md` §Tests)
- [X] T049 [P] [US2] Test de integración del alta en `backend/tests/GT.IntegrationTests/Viajes/CrearViajeTests.cs`: nace `pendiente`, escribe **la primera fila del historial** con `estadoAnterior = null` y el usuario de la sesión, rechaza el importe negativo, acepta el cero, acepta fecha pasada y futura, y devuelve las advertencias `origen_igual_a_destino` y `carga_retroactiva` **sin frenar el guardado**. Verificar además que el viaje recién creado vuelve del listado con `demorado: false` y con `esRetroactivo` según su fecha, que el contrato exige presentes desde acá (FR-013, FR-015, FR-016, FR-032, FR-035, FR-039, US2 esc. 1, 6, 7, 10, 11 y 12)
- [X] T050 [P] [US2] Test de integración de la edición en `backend/tests/GT.IntegrationTests/Viajes/ModificarViajeTests.cs`: aplica las mismas validaciones que el alta; el cuerpo **no acepta** `numero`, `estado`, `choferId` ni `vehiculoId`, así que la asignación queda intacta y ningún `PUT` puede avanzar ni anular un viaje; y al mover la fecha a un día en que la documentación está vencida **no se guarda nada**, ni la fecha ni los demás campos del mismo `PUT` (FR-017, FR-019a, FR-022a, FR-034, SC-004, US2 esc. 13 y 14, US3 esc. 15, `quickstart.md` §Tests)

### Implementación

- [X] T051 [US2] Declarar `IRepositorioViajes` en `backend/src/GT.Application/Viajes/IRepositorioViajes.cs` con alta, obtención por Id con relaciones, consulta paginada, registro de cambio de estado y guardado
- [X] T052 [US2] Implementar `RepositorioViajes` en `backend/src/GT.Infrastructure/Persistencia/RepositorioViajes.cs` con el alta, la ficha con `Include` de cliente, chofer, vehículo, transportista e historial, la **traducción de las violaciones de los índices únicos** a excepciones de la capa de aplicación —distinguiendo cuál índice se violó por su nombre—, y **las dos señales derivadas que el contrato declara obligatorias desde el primer listado**: `esRetroactivo` (`Fecha` anterior al día en curso en Argentina) y `demorado` (subconsulta correlacionada al historial que toma el instante del pase a `en curso`). **Las dos van acá y no en US5**: la subconsulta funciona desde el primer viaje cargado, porque un viaje que nunca arrancó no tiene esa fila y devuelve `false`. Escribirla **en el árbol de expresión y no extraída a un método**, para que EF Core la traduzca (FR-016, FR-039, convención [003], research §2, §6)
- [X] T053 [US2] Implementar `CrearViaje` en `backend/src/GT.Application/Viajes/CrearViaje.cs`, que valida los cuatro campos obligatorios, exige cliente **activo**, rechaza el importe negativo, escribe la fila de historial del alta en la misma transacción, y devuelve el sobre con las advertencias (FR-012, FR-013, FR-032, FR-035)
- [X] T054 [US2] Implementar `ModificarViaje` en `backend/src/GT.Application/Viajes/ModificarViaje.cs`, que rechaza los estados `rendido` y `anulado`, y **revalida la asignación contra la fecha nueva cuando la fecha cambia**, abortando el `PUT` entero si queda bloqueada (FR-017, FR-018, FR-022a)
- [X] T055 [P] [US2] Implementar `ConsultarViajes` en `backend/src/GT.Application/Viajes/ConsultarViajes.cs` con la paginación de 20 filas, el orden **`Fecha` descendente, `Numero` descendente** —total sin necesitar `Id`— y **`demorado` y `esRetroactivo` en cada fila**, que `contracts/viajes-api.yaml` declara obligatorios en el esquema `Viaje`: el listado tiene que cumplir su contrato desde US2, no desde US5 (FR-016, FR-039, FR-043, research §12)
- [X] T056 [P] [US2] Implementar `ConsultarFichaViaje` en `backend/src/GT.Application/Viajes/ConsultarFichaViaje.cs`, devolviendo todos los campos de FR-045 y el historial ordenado del más viejo al más nuevo
- [X] T057 [US2] Exponer `GET /api/viajes`, `GET /api/viajes/{id:int}`, `POST /api/viajes` y `PUT /api/viajes/{id:int}` en `backend/src/GT.Api/Viajes/ViajesEndpoints.cs`, **con la restricción `:int` para que `/viajes/asignables` y `/viajes/totales` no queden capturadas por la ruta de identificador** (trampa 1), con los `GET` bajo `viajes.consultar`, las escrituras bajo `viajes.gestionar` y `pagina` declarado `int?` (convención [003])
- [X] T058 [P] [US2] Crear `frontend/src/modules/viajes/servicios/servicioViajes.ts` con las llamadas de listado, ficha, alta y edición
- [X] T059 [US2] Crear el listado básico en `frontend/src/modules/viajes/paginas/ListadoViajes.tsx` con las diez columnas de FR-040 y la paginación, formateando el importe con `formatearPesos` y las fechas con `formatearFecha`; registrar su ruta `/viajes` en `frontend/src/App.tsx` (FR-040, FR-043)
- [X] T060 [US2] Crear el formulario en `frontend/src/modules/viajes/paginas/FormularioViaje.tsx` con los siete campos, **sin chofer ni vehículo**, con el número mostrado y no editable, y el aviso con enlace cuando no hay ningún cliente activo; registrar sus rutas `/viajes/nuevo` y `/viajes/:id/editar` en `frontend/src/App.tsx` (FR-012, FR-019a, US2 esc. 3 y 4)
- [X] T061 [US2] Crear la ficha en `frontend/src/modules/viajes/paginas/FichaViaje.tsx` con todos los campos de FR-045; registrar su ruta `/viajes/:id` en `frontend/src/App.tsx`
- [X] T062 [P] [US2] Test de frontend del formulario en `frontend/src/modules/viajes/paginas/FormularioViaje.test.tsx`: que **no ofrece** chofer ni vehículo, que el número no es editable, que sin clientes activos no deja completar el alta, y que el importe negativo se marca en el campo mientras el cero se acepta (FR-013, US2 esc. 3, 4, 6 y 7, US3 esc. 14)

**Checkpoint**: se registran viajes con número propio, remito único e historial de alta

---

## Phase 5: User Story 3 - Asignar un chofer y un vehículo habilitados (Priority: P1)

**Goal**: el control que justifica el módulo — que no salga a la ruta una unidad sin documentación en
regla a la fecha del viaje.

**Independent Test**: con un viaje registrado, un chofer en regla, un chofer con un documento vencido
y un vehículo con uno por vencer, comprobar que el primero se asigna sin objeción, el segundo se
rechaza nombrando el documento, y el tercero se asigna mostrando la advertencia.

### Tests de la historia

- [X] T063 [P] [US3] Test de integración de la lista de asignables en `backend/tests/GT.IntegrationTests/Viajes/AsignablesTests.cs`: no aparece ningún chofer dado de baja, ningún vehículo dado de baja ni ninguno cuyo **estado operativo guardado** sea `fuera de servicio`, y la lista **no** filtra por documentación (FR-021, US3 esc. 2 y 3)
- [X] T064 [P] [US3] Test de integración del bloqueo en `backend/tests/GT.IntegrationTests/Viajes/BloqueoPorDocumentacionTests.cs`: con un documento vencido a la fecha del viaje se rechaza **nombrando tipo y número** y no se guarda nada; con un viaje del mes que viene se rechaza si el documento vence antes de esa fecha; y con un viaje retroactivo **se acepta** si el documento estaba vigente ese día aunque hoy esté vencido (FR-022, FR-024, SC-004, SC-014, US3 esc. 4, 6 y 13)
- [X] T065 [P] [US3] Test de integración de la advertencia en `backend/tests/GT.IntegrationTests/Viajes/AdvertenciaAsignacionTests.cs`: un documento dentro de la ventana de aviso a la fecha del viaje **guarda la asignación** y devuelve `documentacion_proxima_a_vencer` en `advertencias[]`, nombrando el documento (FR-023, FR-015a, US3 esc. 5)
- [X] T066 [P] [US3] Test de integración del transportista en `backend/tests/GT.IntegrationTests/Viajes/TransportistaDelViajeTests.cs`: al asignar queda registrado el del chofer; si después el chofer cambia de transportista el viaje **no se mueve**; si le corrigen la razón social al transportista el viaje **muestra la corregida**; y reasignar el chofer vuelve a tomar el del nuevo (FR-028, SC-010, US3 esc. 9 y 10)
- [X] T067 [P] [US3] Test de integración de la asignación parcial en `backend/tests/GT.IntegrationTests/Viajes/AsignacionParcialTests.cs`: el cuerpo con sólo `choferId` o sólo `vehiculoId` se rechaza, y no queda ningún viaje con una sola de las dos unidades (FR-019b, US3 esc. 17)

### Implementación

- [X] T068 [US3] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioViajes.cs` las consultas de choferes y vehículos asignables —activos, y para el vehículo con **estado operativo guardado** `disponible`— y la de qué viaje `en curso` ocupa a una unidad (FR-021, FR-026)
- [X] T069 [P] [US3] Implementar `ConsultarAsignables` en `backend/src/GT.Application/Viajes/ConsultarAsignables.cs`, sin paginación, devolviendo las dos listas que pueden venir vacías (FR-021)
- [X] T070 [US3] Implementar `AsignarChoferYVehiculo` en `backend/src/GT.Application/Viajes/AsignarChoferYVehiculo.cs`: permite asignar y reasignar mientras el viaje esté `pendiente` o `en curso` y rechaza en `rendido` y `anulado` (FR-019, FR-020); exige **los dos juntos** (FR-019b); evalúa la habilitación de cada unidad con `EvaluadorHabilitacion` contra la fecha del viaje; registra el transportista del chofer (FR-028); **no compara** el transportista del vehículo con el del chofer (FR-029); y **si el viaje ya está `en curso`, verifica ocupación** antes de guardar (FR-026a)
- [X] T071 [US3] Exponer `GET /api/viajes/asignables` y `POST /api/viajes/{id:int}/asignacion` en `backend/src/GT.Api/Viajes/AsignacionEndpoints.cs`, **registrando la ruta literal `asignables` con `{id:int}` en la de identificador** para que no compitan (trampa 1), con el mapeo de los rechazos a `400` y `409` según la regla de `contracts/README.md`
- [X] T072 [P] [US3] Agregar a `backend/src/GT.Application/Viajes/Mensajes.cs` los textos y códigos de asignación, incluidos `documentacion_vencida` con `unidadQueBloquea` y `documentoQueBloquea` en el cuerpo del error (FR-022, SC-004)
- [X] T073 [P] [US3] Agregar las llamadas de asignables y asignación a `frontend/src/modules/viajes/servicios/servicioViajes.ts`
- [X] T074 [US3] Crear la pantalla en `frontend/src/modules/viajes/paginas/AsignacionViaje.tsx` con los dos desplegables **obligatorios**, el aviso de contra qué fecha se valida, los mensajes de lista vacía y la advertencia mostrada junto al resultado; registrar su ruta `/viajes/:id/asignacion` en `frontend/src/App.tsx` (FR-019b, FR-021, FR-023)
- [X] T075 [US3] Agregar la acción *Asignar chofer y vehículo* a `frontend/src/modules/viajes/paginas/FichaViaje.tsx`, visible sólo en `pendiente` y `en curso` y sólo con `viajes.gestionar` (FR-020, FR-052)
- [X] T076 [P] [US3] Test de frontend en `frontend/src/modules/viajes/paginas/AsignacionViaje.test.tsx`: el botón no se habilita con una sola unidad elegida, y la advertencia por documento próximo a vencer **no** impide que la asignación se haya guardado (FR-019b, FR-023)

**Checkpoint**: ninguna unidad con documentación vencida a la fecha del viaje se puede asignar

---

## Phase 6: User Story 4 - Avanzar el viaje de pendiente a rendido (Priority: P1)

**Goal**: el ciclo de vida completo, con exclusividad de unidades, confirmación de lo irreversible e
historial de quién hizo qué.

**Independent Test**: con un viaje asignado, ponerlo `en curso`, comprobar que un segundo viaje con
el mismo chofer se rechaza, rendir el primero, y comprobar que recién entonces el segundo arranca.

### Tests de la historia

- [X] T077 [P] [US4] Test de integración de las transiciones en `backend/tests/GT.IntegrationTests/Viajes/TransicionesTests.cs`: las cuatro permitidas funcionan y `pendiente → rendido` se rechaza aunque se invoque el endpoint directamente (FR-033, US4 esc. 10)
- [X] T078 [P] [US4] Test de integración de la exclusividad en `backend/tests/GT.IntegrationTests/Viajes/ExclusividadTests.cs`: el segundo viaje con el mismo chofer o el mismo vehículo se rechaza **nombrando el número del viaje que lo ocupa**; dos viajes `pendiente` con el mismo chofer y la misma fecha se aceptan; y al rendir el primero, el segundo arranca (FR-026, FR-027, US4 esc. 3, 4 y 5, US3 esc. 12)
- [X] T079 [P] [US4] Test de integración de la carrera en `backend/tests/GT.IntegrationTests/Viajes/ExclusividadConcurrenciaTests.cs`: dos operaciones simultáneas que ponen en curso el mismo chofer; una gana y la otra recibe `chofer_ocupado`. A mano es imposible de provocar y es lo que sostiene el 0% de SC-005 (`quickstart.md` §Tests)
- [X] T080 [P] [US4] Test de integración de FR-026a en `backend/tests/GT.IntegrationTests/Viajes/ReasignacionEnCursoTests.cs`: reasignarle a un viaje **ya `en curso`** una unidad que está en otro viaje `en curso` se rechaza; reasignar un viaje `pendiente` a esa misma unidad se acepta, porque un pendiente no ocupa (FR-026a, FR-027, US3 esc. 16)
- [X] T081 [P] [US4] Test de integración de FR-025 en `backend/tests/GT.IntegrationTests/Viajes/ArranqueDelViajeTests.cs`: sin asignación se rechaza; con el chofer o el vehículo **dado de baja** se rechaza indicando cuál y arranca después de reasignar; y con documentación vencida o el vehículo fuera de servicio **arranca igual**, porque eso se controló al asignar (FR-025, US4 esc. 2, 11, 14 y 15)
- [X] T082 [P] [US4] Test de integración de la rendición en `backend/tests/GT.IntegrationTests/Viajes/RendicionTests.cs`: con importe mayor a cero rinde directo; con importe en cero **el primer intento responde `409` y no cambia nada**, y rinde con `confirmado: true`; y al rendir, las dos unidades quedan libres **conservando la asignación** (FR-037, FR-038, SC-007a, US4 esc. 5, 6 y 7)
- [X] T083 [P] [US4] Test de integración de la inmutabilidad en `backend/tests/GT.IntegrationTests/Viajes/ViajeRendidoInmutableTests.cs`: los **cinco caminos** de escritura sobre un viaje `rendido` —editar, asignar, poner en curso, rendir de nuevo y anular— se rechazan, y los cinco también con rol *Administrador del sistema* (FR-018, SC-013, US4 esc. 8 y 9, `quickstart.md` §Tests)
- [X] T084 [P] [US4] Test de integración del **historial del ciclo completo** en `backend/tests/GT.IntegrationTests/Viajes/HistorialDelCicloTests.cs`: recorrer `alta → en curso → rendido` con **dos usuarios distintos** y afirmar que quedaron **tres** filas, en orden, con el `estadoAnterior` correcto en cada una —`null` sólo en la del alta—, el `estadoNuevo` correcto, **el usuario que produjo cada cambio** y un instante en UTC. Repetir el camino `alta → en curso → anulado` y verificar lo mismo. Es lo que hace cierto el 100% de SC-006, que hasta acá sólo estaba probado para la fila del alta (FR-035, SC-006, US4 esc. 13)

### Implementación

- [X] T085 [US4] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioViajes.cs` el registro del cambio de estado —viaje e historial en la **misma transacción**— y la traducción de la violación de los índices de exclusividad a excepciones de la capa de aplicación (FR-035, research §2)
- [X] T086 [US4] Implementar `PonerViajeEnCurso` en `backend/src/GT.Application/Viajes/PonerViajeEnCurso.cs`: valida la transición, exige las dos unidades asignadas **y activas**, verifica ocupación con la consulta previa, y **no revalida** documentación ni estado operativo (FR-025, FR-026, FR-033)
- [X] T087 [US4] Implementar `RendirViaje` en `backend/src/GT.Application/Viajes/RendirViaje.cs`, con la **confirmación previa obligatoria** cuando el importe es cero: el primer intento no aplica el cambio y responde el código de confirmación pendiente (FR-038, FR-015a)
- [X] T088 [US4] Exponer `POST /api/viajes/{id:int}/en-curso` y `POST /api/viajes/{id:int}/rendicion` en `backend/src/GT.Api/Viajes/CicloDeVidaEndpoints.cs` (trampa 1), leyendo el usuario de la sesión con `ClaimsSesion.ObtenerIdUsuario` y pasándolo **por parámetro** al caso de uso (FR-034, FR-035, research §7)
- [X] T089 [P] [US4] Agregar los códigos y textos del ciclo de vida a `backend/src/GT.Application/Viajes/Mensajes.cs`, incluidos `unidad_dada_de_baja`, `chofer_ocupado` y `vehiculo_ocupado` con `viajeQueOcupa` en el cuerpo, y `rendicion_requiere_confirmacion` (FR-026, FR-038)
- [X] T090 [P] [US4] Agregar el historial al DTO de la ficha en `backend/src/GT.Application/Viajes/Dtos.cs`, con el nombre de usuario y el instante en UTC **con la `Z`**, que la convención [002] ya garantiza (FR-035, FR-045)
- [X] T091 [P] [US4] Agregar las llamadas de los tres cambios de estado a `frontend/src/modules/viajes/servicios/servicioViajes.ts`
- [X] T092 [US4] Agregar a `frontend/src/modules/viajes/paginas/FichaViaje.tsx` las acciones por estado de `contracts/README.md` —ninguna en `rendido` ni en `anulado`, con el texto que explica por qué— y el motivo deshabilitado de *Poner en curso* cuando falta asignar (FR-018, FR-025, US4 esc. 8)
- [X] T093 [US4] Mostrar el historial en `frontend/src/modules/viajes/paginas/FichaViaje.tsx` con estado anterior, nuevo, usuario e instante, formateado con `formatearInstante` de `compartido/fechas` (FR-035, SC-006, US4 esc. 13)
- [X] T094 [US4] Crear el diálogo de rendición sin importe en `frontend/src/modules/viajes/componentes/ConfirmacionRendicion.tsx`, disparado por el `409`, con el texto de `contracts/README.md` y **sin efecto al cancelar** (FR-038, US4 esc. 6 y 7)
- [X] T095 [P] [US4] Test de frontend en `frontend/src/modules/viajes/componentes/ConfirmacionRendicion.test.tsx`: que el diálogo aparece ante el `409`, que cancelar no dispara una segunda petición, y que confirmar la manda con `confirmado: true`

**Checkpoint**: el ciclo de vida completo funciona y ninguna unidad está en dos viajes a la vez

---

## Phase 7: User Story 5 - Consultar, buscar y filtrar viajes (Priority: P1)

**Goal**: responder una consulta sin levantarse — filtros combinados, búsqueda sin acentos y la ficha
completa.

**Independent Test**: cargar viajes de distintos clientes, fechas, estados y transportistas, aplicar
combinaciones de filtros y búsquedas, y comprobar que el listado muestra exactamente lo esperado.

### Tests de la historia

- [X] T096 [P] [US5] Test de integración de los filtros en `backend/tests/GT.IntegrationTests/Viajes/FiltrosViajesTests.cs`: los cuatro combinados devuelven sólo los que cumplen **todas** las condiciones; **sin filtro de estado los anulados no aparecen** y con el filtro `anulado` sí, con su motivo; y el filtro por transportista usa el registrado en el viaje (FR-041, FR-044, SC-010, US5 esc. 2, 5, 9 y 10)
- [X] T097 [P] [US5] Test de integración de la búsqueda en `backend/tests/GT.IntegrationTests/Viajes/BusquedaViajesTests.cs`: `cordoba` encuentra `Córdoba` y `CÓRDOBA` encuentra `córdoba`, sobre origen, destino y razón social, combinable con los filtros (FR-042, US5 esc. 3 y 4)
- [X] T098 [P] [US5] Test de integración de la paginación y el orden en `backend/tests/GT.IntegrationTests/Viajes/PaginacionViajesTests.cs`: 20 filas por página, `total` con las coincidencias completas, y que dos viajes del mismo día **no se intercambian entre páginas** (FR-043, US5 esc. 7)
- [X] T099 [P] [US5] Test de integración de las señales derivadas en `backend/tests/GT.IntegrationTests/Viajes/SenialesDerivadasTests.cs`: `demorado` sale del historial y coincide con lo que devuelve la regla en C# sobre el mismo dato —la comparación que pide la convención [003]—, y `esRetroactivo` se calcula contra el día en curso en Argentina (FR-016, FR-039, `quickstart.md` §Tests)

### Implementación

- [X] T100 [US5] Agregar los cuatro filtros y la búsqueda a `backend/src/GT.Infrastructure/Persistencia/RepositorioViajes.cs`, con la exclusión de anulados como **predicado único** de la consulta y la búsqueda con `EF.Functions.Collate(..., "Latin1_General_CI_AI")` sobre origen, destino y razón social (FR-042, FR-044, research §8, §12)
- [X] T101 [US5] Verificar que la consulta del listado **sigue traduciéndose entera a SQL** una vez combinados los filtros y la búsqueda de T100 con la subconsulta de `demorado` de T052, y que no cae a evaluación en memoria: registrar el SQL generado en un test de `backend/tests/GT.IntegrationTests/Viajes/TraduccionConsultaTests.cs` y comprobar que la derivación viaja como subconsulta correlacionada y no como recorrido de filas (FR-039, FR-043, convención [003])
- [X] T102 [P] [US5] Extender `ConsultarViajes` en `backend/src/GT.Application/Viajes/ConsultarViajes.cs` con los cuatro filtros y la búsqueda —las dos señales derivadas ya vienen de T052 y T055— y `ConsultarFichaViaje` con el motivo de anulación (FR-041, FR-045)
- [X] T103 [US5] Agregar los parámetros de query del contrato a `backend/src/GT.Api/Viajes/ViajesEndpoints.cs`, ignorando los valores de estado desconocidos en vez de romper (convención [003])
- [X] T104 [US5] Crear los filtros en `frontend/src/modules/viajes/componentes/FiltrosViajes.tsx`, con la opción por defecto llamada **`Todos menos anulados`** para que ninguna fila quede oculta en silencio; el filtro por cliente es el que hace directo el camino de SC-011 (FR-044, FR-049, SC-011, US5 esc. 9)
- [X] T105 [US5] Agregar la búsqueda, la paginación y los estados vacíos a `frontend/src/modules/viajes/paginas/ListadoViajes.tsx`, con los textos de `contracts/README.md`. **El listado NO lleva fila de total de importes**: los totales viven únicamente en su pantalla, y sumar la página en curso daría un número que no es el del período (FR-042, FR-043, FR-046a, FR-048, US5 esc. 8)
- [X] T106 [US5] Mostrar en `frontend/src/modules/viajes/paginas/ListadoViajes.tsx` las cuatro señales por fila —estado, `Demorado`, `Carga retroactiva` y `(inactivo)` en cliente, chofer y vehículo—, **todas con palabra y no sólo con color** (FR-008, FR-016, FR-030, FR-039, FR-049)
- [X] T107 [P] [US5] Crear la paginación en `frontend/src/modules/viajes/componentes/Paginacion.tsx` siguiendo la del Módulo 4
- [X] T108 [P] [US5] Test de frontend en `frontend/src/modules/viajes/paginas/ListadoViajes.test.tsx`: el mensaje de sin resultados, la etiqueta `Demorado` junto al estado `En curso`, que el control de estado dice qué está mostrando, y que **la tabla no tiene fila de total** —ni pie de tabla ni celda con la suma de los importes de la página— (FR-039, FR-046a, FR-048, FR-049)

**Checkpoint**: se responde cualquier consulta desde el listado sin salir del módulo

---

## Phase 8: User Story 6 - Anular un viaje que no se hizo (Priority: P2)

**Goal**: que un viaje que no se hizo deje de contar como trabajo realizado sin desaparecer de la
historia.

**Independent Test**: anular un viaje `pendiente`, comprobar que sin motivo la confirmación no se
habilita, que al cancelar nada cambia, y que al confirmar desaparece del listado sin filtros pero
reaparece con su motivo al filtrar por `anulado`.

### Tests de la historia

- [X] T109 [P] [US6] Test de integración de la anulación en `backend/tests/GT.IntegrationTests/Viajes/AnulacionTests.cs`: sin motivo se rechaza; con motivo el viaje queda `anulado`, el historial lo registra y **las dos unidades quedan libres** conservando la asignación; procede desde `pendiente` y desde `en curso`; y **no** desde `rendido` (FR-036, FR-037, SC-007, US6 esc. 1, 4 y 6)
- [X] T110 [P] [US6] Test de integración del viaje anulado en `backend/tests/GT.IntegrationTests/Viajes/ViajeAnuladoTests.cs`: no existe transición de vuelta a `pendiente` ni a `en curso`, sus datos no se editan, su importe **no figura en ningún total**, y su número **no se reutiliza** (FR-011, FR-017, FR-047, US6 esc. 5 y 7)

### Implementación

- [X] T111 [US6] Implementar `AnularViaje` en `backend/src/GT.Application/Viajes/AnularViaje.cs`, con el motivo obligatorio de hasta 500 caracteres escrito en la misma operación que el estado, y la liberación de las unidades por el solo cambio de estado (FR-036, FR-037)
- [X] T112 [US6] Exponer `POST /api/viajes/{id:int}/anulacion` en `backend/src/GT.Api/Viajes/CicloDeVidaEndpoints.cs` (trampa 1)
- [X] T113 [P] [US6] Agregar el código y el texto de `motivo_requerido` a `backend/src/GT.Application/Viajes/Mensajes.cs`
- [X] T114 [US6] Crear el diálogo en `frontend/src/modules/viajes/componentes/ConfirmacionAnulacion.tsx` con el campo de motivo obligatorio, el botón de confirmar **deshabilitado mientras el motivo esté vacío**, y sin efecto al cancelar (FR-036, SC-007, US6 esc. 2 y 3)
- [X] T115 [US6] Mostrar el motivo de anulación en `frontend/src/modules/viajes/paginas/FichaViaje.tsx` y en las filas del listado filtrado por `anulado` (FR-036, FR-045, US6 esc. 5)
- [X] T116 [P] [US6] Test de frontend en `frontend/src/modules/viajes/componentes/ConfirmacionAnulacion.test.tsx`: que sin motivo el botón no se habilita y que cancelar no dispara ninguna petición

**Checkpoint**: los viajes que no se hicieron dejan de contar sin perderse de la historia

---

## Phase 9: User Story 7 - Ver totales por cliente y por transportista (Priority: P3)

**Goal**: el resumen del período que Administración le arma a Gerencia, con datos y no de memoria.

**Independent Test**: cargar viajes de dos clientes y dos transportistas dentro y fuera de un rango,
con alguno anulado, y comprobar que los totales cuentan sólo los del rango y ninguno de los anulados.

### Tests de la historia

- [X] T117 [P] [US7] Test de integración de los totales en `backend/tests/GT.IntegrationTests/Viajes/TotalesTests.cs`: sin rango responde `400` con `rango_de_fechas_requerido`; la fecha de corte es **la del viaje**; un cliente con 10 viajes de los cuales 2 están anulados figura con **8**; y los viajes sin transportista no aparecen en ese cuadro (FR-046, FR-046a, FR-047, US7 esc. 1, 2 y 3)
- [X] T118 [P] [US7] Test de integración de SC-008 en `backend/tests/GT.IntegrationTests/Viajes/CoincidenciaTotalesListadoTests.cs`: la suma de los importes de las filas del listado filtrado por cliente y rango **coincide** con el total de ese cliente, porque las dos consultas excluyen los anulados con el mismo predicado (SC-008, US7 esc. 4)

### Implementación

- [X] T119 [US7] Implementar `ConsultarTotales` en `backend/src/GT.Application/Viajes/ConsultarTotales.cs` con las dos agregaciones sobre el mismo predicado, el rango obligatorio y la exclusión de anulados escrita **en la consulta** (FR-046, FR-046a, FR-047)
- [X] T120 [US7] Exponer `GET /api/viajes/totales` en `backend/src/GT.Api/Viajes/TotalesEndpoints.cs` bajo `viajes.consultar`, con `desde` y `hasta` obligatorios; verificar que la ruta literal no quede capturada por la de identificador (trampa 1)
- [X] T121 [P] [US7] Agregar la llamada de totales a `frontend/src/modules/viajes/servicios/servicioViajes.ts`
- [X] T122 [US7] Crear la pantalla en `frontend/src/modules/viajes/paginas/TotalesPeriodo.tsx` con el selector de rango, los **dos cuadros**, el mensaje de rango faltante, el de sin resultados, y los importes con `formatearPesos`; registrar su ruta `/viajes/totales` en `frontend/src/App.tsx` (FR-046, FR-046a, FR-048)
- [X] T123 [P] [US7] Test de frontend en `frontend/src/modules/viajes/paginas/TotalesPeriodo.test.tsx`: que **sin rango elegido no se calcula ni se muestra ningún total** y que el mensaje lo dice (US7 esc. 2)

**Checkpoint**: las siete historias funcionan

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T124 [P] Verificar que ningún estado ni ninguna señal se comuniquen sólo por color en `frontend/src/modules/viajes/`, y que todo elemento atenuado lleve además la palabra que lo explica —`Inactivo`, `Demorado`, `Carga retroactiva`, `Anulado`— (FR-049, convención [003])
- [X] T125 [P] Anunciar con `role="status"` el resultado del guardado, el cambio de página, el cambio de estado y las advertencias que llegan con el resultado, en `frontend/src/modules/viajes/`. No sale de ningún FR: es la convención `[003]` de `AGENTS.md`, que rige desde el Módulo 3 y quedó escrita ahí para dejar de ser alcance tácito
- [X] T126 [P] Verificar que todas las fechas se formateen con `formatearFecha` y `formatearInstante` de `frontend/src/compartido/fechas.ts`, y que no quede ningún `new Date(iso).toLocaleDateString()` en `frontend/src/modules/viajes/` (convención [003])
- [X] T127 [P] Verificar que todos los importes se formateen con `formatearPesos` de `frontend/src/compartido/moneda.ts` y que no quede ningún `toFixed(2)` ni `Intl` armado a mano en `frontend/src/modules/viajes/` (Principio II, research §11)
- [X] T128 [P] Revisar que los textos de `backend/src/GT.Application/Viajes/Mensajes.cs` y los de `frontend/src/modules/viajes/` estén en español rioplatense y coincidan **palabra por palabra** con `contracts/README.md`
- [X] T129 Revisar que ningún endpoint de `backend/src/GT.Api/Viajes/` quede sin su permiso, que los `GET` exijan `viajes.consultar` y las escrituras `viajes.gestionar`, que ninguno decida por rol, y que **todas las rutas con identificador lleven `{id:int}`** (FR-050, FR-052, SC-012, trampa 1)
- [X] T130 Verificar que **ningún archivo de `GT.Domain/Choferes/`, `GT.Domain/Flota/`, sus repositorios, sus endpoints ni sus pantallas** haya quedado modificado, comparando contra `main` con `git diff --stat` (spec §Assumptions, plan §Project Structure)
- [ ] T131 Correr el recorrido completo de `specs/005-gestion-viajes/quickstart.md` con las tres cuentas: `admin`, un usuario de *Tráfico* y uno de *Gerencia* (SC-001, SC-011)
- [X] T132 Correr `dotnet test` en `backend/` y `npm test` en `frontend/`, y dejar ambos en verde
- [X] T133 [P] Actualizar `specs/README.md` con el estado del Módulo 5 y con lo que el recorrido manual haya encontrado
- [X] T134 Actualizar `AGENTS.md` con las decisiones transversales de esta feature, una línea por decisión y con la referencia `[005]`, tomando como punto de partida las seis candidatas de `plan.md` §Mantenimiento y descartando las que al implementar resulten no ser transversales

---

## Dependencies

### Entre fases

```text
Phase 1 (Setup)
   └─▶ Phase 2 (Foundational)  ⚠ bloqueante para todo
          ├─▶ Phase 3  US1  Clientes          🎯 MVP
          │      └─▶ Phase 4  US2  Registrar viaje
          │             ├─▶ Phase 5  US3  Asignar
          │             │      └─▶ Phase 6  US4  Ciclo de vida
          │             │             └─▶ Phase 8  US6  Anular
          │             ├─▶ Phase 7  US5  Consultar y filtrar
          │             └─▶ Phase 9  US7  Totales
          └─▶ Phase 10 (Polish)
```

### Por qué estas historias no son del todo independientes

La spec las ordena por prioridad, pero el dominio impone un orden real y conviene tenerlo a la vista:

- **US2 necesita US1**: todo viaje pertenece obligatoriamente a un cliente, y el padrón arranca vacío.
- **US3, US5 y US7 necesitan US2**: sin viajes no hay a qué asignar, qué filtrar ni qué totalizar.
- **US4 necesita US3**: un viaje no arranca sin chofer y vehículo asignados (FR-025).
- **US6 necesita US4** sólo para el caso `en curso → anulado`; desde `pendiente` funciona con US2.

**Una dependencia cruzada que vale marcar**: el chequeo de ocupación al reasignar (FR-026a) se
**implementa en US3** (T070) pero se **prueba en US4** (T080), porque el estado `en curso` que el test
necesita recién existe ahí.

### Dentro de cada fase

- Los `[P]` de una misma sección tocan archivos distintos y no dependen entre sí.
- Los tests de cada historia se pueden escribir antes que su implementación; no hace falta que pasen
  para seguir, pero sí antes del checkpoint.
- Todo lo que toca `frontend/src/App.tsx` es secuencial: son siete rutas sobre el mismo archivo.

---

## Parallel Execution Examples

**Fase 2 — arranque**: T003 a T007 (cinco archivos de dominio distintos), después T008 y T009, después
T010 a T012 con T013 a T015 (tests unitarios y configuraciones EF, sin relación entre sí).

**Fase 3 — US1**: T029, T030 y T031 en paralelo; después T034 a T038, que son cinco casos de uso en
archivos separados; y al final T044 y T045.

**Fase 4 — US2**: los cinco tests T046 a T050 en paralelo; después T055 y T056 juntos, que consultan y
no escriben.

**Fase 6 — US4**: los ocho tests T077 a T084 en paralelo — es la fase con más tests y ninguno comparte
archivo.

**Fase 10**: T124 a T128 en paralelo, y T133 mientras se corren los tests de T132.

---

## Implementation Strategy

### MVP

**Fases 1, 2 y 3** — el padrón de clientes funcionando de punta a punta. Es entregable por sí solo:
alguien de Tráfico puede cargar el padrón real mientras el resto del módulo se construye, y ese
trabajo no se tira.

### Incrementos siguientes

1. **+ US2** — ya se registran viajes y se los consulta. Es el primer incremento que responde la
   pregunta que hoy vive en planillas.
2. **+ US3** — aparece el control que justifica el módulo: ninguna unidad sin papeles en regla a la
   fecha del viaje.
3. **+ US4** — el ciclo de vida completo. Con esto el módulo ya reemplaza a la planilla.
4. **+ US5** — la consulta deja de ser mirar una lista y pasa a responder preguntas.
5. **+ US6 y US7** — anulación y totales, que son P2 y P3 y no bloquean la operación diaria.

### Dos cosas para no dejar para el final

- **T018** (los índices filtrados) va en la Fase 2 y no en una historia: es la garantía de la que
  dependen SC-003 y SC-005, y descubrir tarde que un índice está mal escrito obliga a rehacer la
  migración.
- **T131** (el recorrido manual del quickstart) encontró en los cuatro módulos anteriores cosas que
  ningún test veía, incluidas dos en las que **la spec pedía lo que no había que hacer**. No es un
  trámite de cierre.
