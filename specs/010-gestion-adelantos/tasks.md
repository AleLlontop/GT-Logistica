---

description: "Lista de tareas de implementación del Módulo 10"
---

# Tasks: Gestión de adelantos de sueldo (Módulo 10)

**Input**: Documentos de diseño de `/specs/010-gestion-adelantos/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/README.md`,
`contracts/adelantos-api.yaml`, `quickstart.md`

**Tests**: se incluyen. El plan los pide explícitamente (`Technical Context` → *Testing*) y
`quickstart.md` y research §11 nombran las clases de test que cubren lo que una persona no puede verificar
a mano: `ResolucionConcurrenteTests`, `AnulacionConcurrenteTests`, `RestriccionesDeAdelantoTests`,
`AprobacionYRechazoTests`, `AnulacionTests`, `RegistroTests`, `ElegibilidadDeBeneficiarioTests`,
`BeneficiariosTests`, `ReglasDeAdelantoTests` y `RutasAdelantosTests`.

**Organization**: las tareas se agrupan por historia de usuario, para poder implementar y validar cada
una por separado.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: puede ejecutarse en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1 … US6)
- Cada tarea lleva la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados (plan.md → *Project Structure*):

- Backend: `backend/src/GT.Api/`, `backend/src/GT.Application/`, `backend/src/GT.Domain/`,
  `backend/src/GT.Infrastructure/`
- Tests de backend: `backend/tests/GT.UnitTests/`, `backend/tests/GT.IntegrationTests/`
- Frontend: `frontend/src/modules/adelantos/` y `frontend/src/compartido/`

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar las carpetas del módulo. **No hay dependencias, variables de entorno ni cambios de
`Dockerfile`** (research §10).

- [X] T001 [P] Crear las carpetas del backend del módulo: `backend/src/GT.Domain/Adelantos/`, `backend/src/GT.Application/Adelantos/`, `backend/src/GT.Api/Adelantos/`, `backend/tests/GT.UnitTests/Adelantos/` y `backend/tests/GT.IntegrationTests/Adelantos/`
- [X] T002 [P] Crear el esqueleto del módulo de frontend `frontend/src/modules/adelantos/` con las subcarpetas `paginas/`, `componentes/` y `servicios/`

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: esquema, entidades, reglas puras, permisos, menú, la relectura del detalle —que usan las
cuatro escrituras—, los tres cambios enumerados a los Módulos 6 y 9 y las piezas compartidas del frontend.
Nada de esto pertenece a una historia sola.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa.

### Dominio

- [X] T003 [P] Crear `EstadoAdelanto : byte` (`Pendiente = 0`, `Aprobado = 1`, `Rechazado = 2`, `Anulado = 3`) en `backend/src/GT.Domain/Adelantos/EstadoAdelanto.cs`, con el comentario ⚠ de que `CK_Adelantos_Estado` lleva `0` a `3` escritos a mano y reordenar el enum no falla al compilar (data-model §Enumeraciones, research §9.1)
- [X] T004 [P] Crear `OperacionDeAdelanto : byte` (`Registro = 0`, `Aprobacion = 1`, `Rechazo = 2`, `Anulacion = 3`) en `backend/src/GT.Domain/Adelantos/OperacionDeAdelanto.cs`
- [X] T005 [P] Crear `TipoBeneficiario : byte` (`Chofer = 1`, `Empleado = 2`) en `backend/src/GT.Domain/Adelantos/TipoBeneficiario.cs`, con el comentario ⚠ de `CK_Adelantos_TipoBeneficiario` y el de por qué **no es `TipoIntegrante`**: aquél es informativo del padrón y éste lo decide la ficha de chofer (research §1)
- [X] T006 [P] Crear la entidad `Adelanto` en `backend/src/GT.Domain/Adelantos/Adelanto.cs`: `Id`, `PersonaId` + navegación a `Persona` (**sin colección inversa en `Persona`**), `TipoBeneficiario`, `Fecha` (`DateOnly`), `Motivo`, `Importe` (`decimal`), `Estado` que **nace `Pendiente`**, `MotivoRechazo` y `MotivoAnulacion` anulables y la colección de cambios. Documentar que no se borra ni se modifica, sólo cambia de estado por `UPDATE` condicional (FR-035, data-model §Tabla `Adelantos`)
- [X] T007 [P] Crear `CambioDeAdelanto` (`Id`, `AdelantoId` + navegación, `Operacion`, `UsuarioId` + navegación a `Usuario`, `OcurridoEn` `DateTime`) en `backend/src/GT.Domain/Adelantos/CambioDeAdelanto.cs`, con `init` como `CambioDeEstadoViaje`; **sin columna de motivo**: se lee del adelanto (data-model §Tabla `CambiosDeAdelanto`)
- [X] T008 [P] Crear `ReglasDeAdelanto` en `backend/src/GT.Domain/Adelantos/ReglasDeAdelanto.cs` con las funciones puras de data-model §`ReglasDeAdelanto`: `PrimeraFechaAdmitida(hoy)` —`new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1)`, nunca restando uno al mes a mano (research §9.9)—, `FechaAdmitida(fecha, hoy)` con los dos límites incluidos, `ImporteValido(importe)` —`> 0`, `decimal.Round(importe, 2) == importe` y a lo sumo `9999999999999999.99` (research §9.8)—, `MotivoNoResoluble(estado)` y `MotivoNoAnulable(estado)`. **Ninguna lee el reloj** (convención [005])
- [X] T009 [P] Crear `ElegibilidadDeBeneficiario` en `backend/src/GT.Domain/Adelantos/ElegibilidadDeBeneficiario.cs`: el récord `PersonaParaAdelanto(Id, Apellido, Nombre, Dni, Activa, TipoIntegrante Tipo, TieneFicha, FichaActiva, CuitTransportista)`, el enum `MotivoNoElegible` (`Inexistente`, `Inactiva`, `EmpresaEmisoraNoConfigurada`, `TipoDistinto`, `ChoferExterno`) y `Evaluar(tipo, persona?, cuitEmpresaEmisora?)` con **exactamente** los siete pasos y el orden de la tabla de data-model §`ElegibilidadDeBeneficiario`. Documentar que es la única escritura de FR-002 y FR-009 (research §1)
- [X] T010 [P] Agregar `AdelantosGestionar = "adelantos.gestionar"` y `AdelantosConsultar = "adelantos.consultar"` con su comentario de reparto (research §7) en `CodigosPermiso` de `backend/src/GT.Domain/Usuarios/Rol.cs`

### Tests de reglas puras

- [X] T011 [P] Escribir `backend/tests/GT.UnitTests/Adelantos/ReglasDeAdelantoTests.cs`: `PrimeraFechaAdmitida` del 14/09/2026 es 01/08/2026, del 01/01/2027 es 01/12/2026 y del 31/03/2026 es 01/02/2026; `FechaAdmitida` con el piso, un día antes, hoy y mañana; `ImporteValido` con `0`, `-20000`, `0,01`, `150000,555`, `9999999999999999,99` y `10000000000000000`; `MotivoNoResoluble` y `MotivoNoAnulable` en los cuatro estados
- [X] T012 [P] Escribir `backend/tests/GT.UnitTests/Adelantos/ElegibilidadDeBeneficiarioTests.cs`: un caso por fila de la tabla de data-model —persona nula, inactiva, chofer sin emisora, chofer sin ficha, ficha inactiva, chofer externo, empleado con ficha, persona de tipo chofer sin ficha pedida como empleado, y los dos elegibles—; la **precedencia** (persona inactiva y chofer externo a la vez da `Inactiva`); un empleado es elegible **sin** empresa emisora; una persona de `Tipo = Empleado` con ficha propia activa es elegible como chofer y no como empleado

### Persistencia

- [X] T013 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/AdelantoConfiguracion.cs`: `decimal(18,2)` para `Importe`; `Motivo` `nvarchar(200)`; los dos motivos de cierre `nvarchar(500)`; `Estado` y `TipoBeneficiario` con `HasConversion<byte>()` **sin `HasDefaultValue`**; los `CHECK` de data-model —`CK_Adelantos_TipoBeneficiario`, `CK_Adelantos_Importe` (`> 0`), `CK_Adelantos_Motivo` (`LEN([Motivo]) > 0`) y `CK_Adelantos_Estado` con **cada `LEN` detrás de su `IS NOT NULL`** (research §9.2)—, con el comentario ⚠ de los enums escritos a mano; FK a `Personas` en `Restrict` con `WithMany()`; los tres índices `IX_Adelantos_Fecha` (`Fecha` y `Id` descendentes), `IX_Adelantos_PersonaId` e `IX_Adelantos_Estado`. Exponer los nombres de los `CHECK` como constantes
- [X] T014 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CambioDeAdelantoConfiguracion.cs`: `Operacion` con `HasConversion<byte>()`; `IX_CambiosDeAdelanto_Operacion` único sobre `(AdelantoId, Operacion)` **sin filtro**; FK al adelanto y a `Usuarios` en `Restrict`
- [X] T015 Agregar `DbSet<Adelanto> Adelantos` y `DbSet<CambioDeAdelanto> CambiosDeAdelanto`, bajo un encabezado `Módulo 10`, en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs`
- [X] T016 Generar y revisar la migración `Modulo10Adelantos` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, verificando que **no contenga ningún `ALTER` sobre tablas existentes** —ni `Personas`, ni `Choferes`, ni `Transportistas`, ni `EmpresaEmisora`— y que los `CHECK` lleven los literales de data-model, con los `IS NOT NULL`
- [X] T017 Sembrar los dos permisos (módulo `Adelantos`) y su reparto —gestionar a *Administración de la empresa* y *Administrador del sistema*; consultar a esos dos más *Gerencia*— en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`, de forma idempotente y sumando el párrafo del Módulo 10 al comentario de `PermisosPorRol`
- [X] T018 Agregar las dos entradas —`consultar-adelanto` / `Consultar adelanto` / `/adelantos` por `adelantos.consultar`, y `registrar-adelanto` / `Registrar adelanto` / `/adelantos/nuevo` por `adelantos.gestionar`— en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs` (research §7)
- [X] T019 **Test del Módulo 2**: en `backend/tests/GT.IntegrationTests/Usuarios/AsignarRolesTests.cs`, dentro de `Gerencia_RecibeSuPrimerPermisoConElModulo5`, agregar la aserción de que el módulo `Adelantos` de Gerencia trae **sólo** `adelantos.consultar`, con el comentario del cuarto módulo, y cambiar `Assert.Equal(3, …)` por `4`. **No tocar ninguna otra aserción**: el test sigue protegiendo que Gerencia no recibe permisos de gestión (research §7)
- [X] T020 [P] Crear `backend/tests/GT.IntegrationTests/Adelantos/DatosDePruebaAdelantos.cs` con los ayudantes de escenario: empresa emisora con un CUIT (reutilizando el de `backend/tests/GT.IntegrationTests/Liquidaciones/DatosDePruebaLiquidaciones.cs`), transportista propio con ese CUIT y uno externo, personas con cada combinación de tipo, actividad y ficha (reutilizando `backend/tests/GT.IntegrationTests/Usuarios/DatosDePrueba.cs` y `backend/tests/GT.IntegrationTests/Choferes/DatosDePrueba.cs`), y un ayudante que **inserta un adelanto directo por `GtDbContext`** en cualquier estado y con cualquier fecha —con su historial—, para los escenarios de listado que la regla de fecha no deja cargar por la API
- [X] T021 Escribir `backend/tests/GT.IntegrationTests/Adelantos/RestriccionesDeAdelantoTests.cs`, con **cada fila inválida violando una sola restricción** (convención [009]): por cada rama de `CK_Adelantos_Estado` una fila válida se acepta; se rechazan pendiente con `MotivoRechazo`, aprobado con `MotivoAnulacion`, **rechazado con `MotivoRechazo` en `NULL`** y con `'   '`, anulado con `MotivoAnulacion` en `NULL` y con `'   '`, y anulado con los dos motivos; `TipoBeneficiario = 3` rechazado; `Importe = 0` rechazado; `Motivo = '   '` rechazado; `IX_CambiosDeAdelanto_Operacion` rechaza una segunda entrada `aprobacion` del mismo adelanto

### Capa de aplicación: piezas compartidas

- [X] T022 [P] Crear `backend/src/GT.Application/Adelantos/NombresDeEstadoAdelanto.cs`: camelCase en el JSON para `EstadoAdelanto`, `OperacionDeAdelanto`, `TipoBeneficiario` y `MotivoNoElegible`; lectura tolerante del filtro de estado —un valor desconocido devuelve `null`—; lectura estricta del tipo —un valor desconocido es inválido—; y la traducción al español para las oraciones (`aprobado`, `Chofer`) (convención [003])
- [X] T023 [P] Crear `backend/src/GT.Application/Adelantos/Mensajes.cs` con los códigos de error de research §6 —`datos_invalidos`, `fecha_fuera_de_rango`, `empresa_emisora_no_configurada`, `beneficiario_no_elegible`, `motivo_requerido`, `rango_invalido`, `adelanto_no_resoluble`, `adelanto_no_anulable`, `rechazo_requiere_confirmacion`, `anulacion_requiere_confirmacion`, `adelanto_no_encontrado`— y **los textos exactos** de `contracts/README.md`, con la persona como `Apellido, Nombre`, los importes en pesos y las fechas `dd/MM/yyyy`
- [X] T024 [P] Crear `backend/src/GT.Application/Adelantos/Dtos.cs` con los esquemas de `adelantos-api.yaml`: `PersonaResumen`, `Beneficiarios`, `RegistroRequest`, `RechazoRequest` y `AnulacionRequest` con `Confirmado` opcional, `AdelantoListado`, `PaginaDeAdelantos` (`items`, `total`, `pagina`, `tamanioPagina`, `totalAdelantado`), `CambioDeAdelanto` con `motivo` anulable, `AdelantoDetalle` con `motivoRechazo`, `motivoAnulacion`, `puedeResolverse` y `puedeAnularse`, y `FiltrosDeAdelantos`
- [X] T025 [P] Crear `backend/src/GT.Application/Adelantos/ResultadoAdelanto.cs`: el enum `ErrorAdelanto` con un valor por código de T023 —los de `400` arriba y los de `409` debajo de una línea, como `ErrorLiquidacion`— y el récord de resultado con el detalle releído, el campo marcado, el mensaje, el `MotivoNoElegible`, el `EstadoAdelanto` actual y `desde`/`hasta`
- [X] T026 Crear `backend/src/GT.Application/Adelantos/IRepositorioAdelantos.cs` con las firmas de todas las consultas y las cuatro transacciones de data-model §Consultas y §Transacciones; los instantes llegan por parámetro y ninguna consulta lee el reloj
- [X] T027 Crear `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs` con `ObtenerDetalleAsync`: el adelanto con su persona y su historial con el usuario, ordenado por `OcurridoEn` e `Id`, sin rastrear
- [X] T028 Implementar `backend/src/GT.Application/Adelantos/ConsultarDetalleAdelanto.cs`: arma `AdelantoDetalle` con apellido, nombre y DNI **del padrón vigente**, el tipo guardado, cada entrada de `rechazo` y `anulacion` con el motivo **leído de la columna** del adelanto, y `puedeResolverse` / `puedeAnularse` desde `ReglasDeAdelanto`. Es también la relectura de las cuatro escrituras (convención [006])
- [X] T029 Registrar `IRepositorioAdelantos` → `RepositorioAdelantos` y `ConsultarDetalleAdelanto` bajo un bloque `Módulo 10` en `backend/src/GT.Api/Program.cs`, y agregar `CodigosPermiso.AdelantosGestionar` y `CodigosPermiso.AdelantosConsultar` a `AgregarPoliticasDePermisos`
- [X] T030 Crear `backend/src/GT.Api/Adelantos/RespuestasDeAdelanto.cs`: la traducción `ResultadoAdelanto` → HTTP en un solo lugar, siguiendo research §6 —`400` para lo tipeado o elegido, `409` para el estado y para las dos confirmaciones, `404` para el inexistente—, con los cuerpos `ErrorDeFecha` (`desde`, `hasta`), `ErrorDeBeneficiario` (`motivo`) y `ErrorDeEstado` (`estado`) del contrato. Un error sin código propio cae en `datos_invalidos`, nunca en `500`

### Frontend compartido y cambios a los Módulos 6 y 9 (spec §Assumptions, cambios 1 a 3)

- [X] T031 [P] Agregar a `frontend/src/compartido/fechas.ts` `enIso(fecha: Date): string` —año, mes y día **locales** con ceros a la izquierda, **sin pasar por `toISOString()`**— y `hoyEnIso(): string`, con el comentario de por qué (research §10b); y a `frontend/src/compartido/fechas.test.ts` los casos `enIso(new Date(2026, 0, 5))` → `2026-01-05` y `hoyEnIso()` con el reloj fijado en `new Date(2026, 8, 14, 23, 30)` → `2026-09-14`
- [X] T032 **Cambio 1 al Módulo 6**, en dos pasos y en este orden: (1) agregar a `frontend/src/modules/facturacion/paginas/FichaFactura.test.tsx` un caso **nuevo** que, con el reloj fijado en `new Date(2026, 8, 14, 23, 30)` —falseando sólo `Date`—, abra el registro del cobro y verifique que *Fecha de cobro* tiene `2026-09-14`, y correrlo en verde con la función local; (2) borrar `hoyEnIso` local de `frontend/src/modules/facturacion/paginas/FichaFactura.tsx`, importar la de `compartido/fechas`, y volver a correr la suite **sin modificar ningún caso existente**
- [X] T033 **Cambio 2 al Módulo 6**, en el mismo orden: (1) agregar a `frontend/src/modules/facturacion/paginas/AltaFactura.test.tsx` un caso nuevo que, con el reloj fijado igual, verifique que *Fecha de facturación* tiene `2026-09-14`, y verlo en verde; (2) borrar `enIso` local de `frontend/src/modules/facturacion/paginas/AltaFactura.tsx` e importar la compartida —`sumarDias` la sigue usando—, y volver a correr la suite sin modificar ningún caso existente
- [X] T034 **Cambio 3 al Módulo 9**, en el mismo orden: (1) agregar a `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.test.tsx` un caso nuevo que, con el reloj fijado igual, abra *Registrar orden de pago* sobre una liquidación pendiente con `puedeGestionar` y verifique que *Fecha de pago* tiene `2026-09-14`, y verlo en verde; (2) borrar `hoyEnIso` local de `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx`, importar la compartida, y volver a correr la suite sin modificar ningún caso existente
- [X] T035 [P] Agregar `adelantosGestionar: 'adelantos.gestionar'` y `adelantosConsultar: 'adelantos.consultar'`, con su comentario, a `Permisos` en `frontend/src/modules/autenticacion/servicios/sesion.ts`
- [X] T036 [P] Agregar `'consultar-adelanto': 'Operación'` y `'registrar-adelanto': 'Operación'` a `SECCION_POR_CODIGO` en `frontend/src/compartido/seccionesDeMenu.ts`, **sin tocar** `seccionesDeMenu.test.ts`
- [X] T037 [P] Agregar a `TONO_POR_VALOR` de `frontend/src/compartido/ui/Estado.tsx`, bajo un comentario `Adelanto (Módulo 10)`, `aprobado: 'rendido'` y `rechazado: 'anulado'`; `pendiente` y `anulado` ya están (research §10)
- [X] T038 Crear `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts` con los tipos del contrato (`EstadoAdelanto`, `TipoBeneficiario`, `PersonaResumen`, `AdelantoListado`, `PaginaDeAdelantos`, `AdelantoDetalle`, `CambioDeAdelanto`, los cuerpos de error), el objeto `CodigosErrorAdelantos`, `NOMBRES_DE_ESTADO`, `NOMBRES_DE_TIPO`, `formatearPersona(persona) → "Pérez, Juan"`, `detalleDeError` como el del Módulo 9, `esSinPermiso(fallo)` —un `ErrorHttp` con estado `403`, para distinguir la falta de permiso de un error de carga (FR-043, research §7)—, y **`primeraFechaAdmitida(hoy: Date = new Date()): string`** con `startOfMonth(subMonths(hoy, 1))` de `date-fns` y `enIso` de `compartido/fechas`. Apoyado en `compartido/clienteHttp`; **las rutas no llevan `/api`**
- [X] T039 [P] Escribir `frontend/src/modules/adelantos/servicios/servicioAdelantos.test.ts`: `primeraFechaAdmitida` del 14/09/2026 es `2026-08-01`, del 01/01/2027 es `2026-12-01` y del 31/03/2026 es `2026-02-01`

**Checkpoint**: la base migra, arranca, siembra los permisos, muestra las dos entradas en *Operación* según el rol, y las pantallas de factura y de liquidación siguen iguales con la fecha compartida. Las historias pueden empezar.

---

## Phase 3: User Story 1 - Registrar un adelanto (Priority: P1) 🎯 MVP

**Goal**: elegir el tipo y la persona elegible, cargar fecha, motivo e importe, y dejar el adelanto
`pendiente`.

**Independent Test**: con un padrón de dos choferes propios activos, uno propio dado de baja, uno activo de
un transportista externo y dos empleados activos, al elegir *Chofer* aparecen sólo los dos propios
activos, al elegir *Empleado* sólo los dos empleados, y al guardar un adelanto de $150.000 queda
`pendiente` sobre la persona elegida.

### Tests para User Story 1

- [X] T040 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Adelantos/BeneficiariosTests.cs` sobre un padrón con dos choferes propios activos, un propio con ficha dada de baja, uno activo de externo, dos empleados activos, un empleado dado de baja, una persona de tipo chofer sin ficha y una de tipo empleado con ficha propia activa: `tipo=chofer` trae los dos propios **y** la de tipo empleado con ficha; `tipo=empleado` trae sólo los dos empleados; orden por apellido, nombre e `Id`; sin empresa emisora `tipo=chofer` responde `empresaEmisoraConfigurada: false` y lista vacía y `tipo=empleado` sigue trayendo a los empleados; tipo ausente o desconocido `400`. Y el caso que prueba que **es la misma regla**: para cada persona del padrón y cada tipo, aparece en `/beneficiarios` si y sólo si `POST /adelantos` con esa persona y ese tipo responde `201` (research §1)
- [X] T041 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Adelantos/RegistroTests.cs`: `201` con `Location`, estado `pendiente`, el tipo elegido, motivo recortado y **una** entrada `registro` con el usuario y un instante con `Z`; la fecha de hoy y la del piso —calculadas con `FechaHoyArgentina.Hoy()` y `ReglasDeAdelanto`— se aceptan, y mañana y un día antes del piso responden `400 fecha_fuera_de_rango` con `desde` y `hasta`; `datos_invalidos` con `campo` para tipo ausente o desconocido, persona ausente, fecha ausente, motivo vacío, de sólo espacios o de 201 caracteres, e importe `0`, negativo, con tres decimales o fuera de `decimal(18,2)`; `400 empresa_emisora_no_configurada` con tipo chofer; `400 beneficiario_no_elegible` con cada motivo —`inexistente`, `inactiva` (persona dada de baja y ficha dada de baja), `tipoDistinto` (un chofer como empleado y un empleado como chofer) y `choferExterno`— **invocando directo** (US1 esc. 11); **ningún rechazo crea filas**; dos adelantos de la misma persona en el mismo mes se registran los dos (FR-012); la persona y su ficha quedan intactas (FR-037)
- [X] T042 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Adelantos/RutasAdelantosTests.cs`: `GET /api/adelantos/beneficiarios?tipo=empleado` responde su propio cuerpo y no `404` ni un error de conversión, lo que prueba que `{id:int}` no la captura (convención [005]). `GET /personas` se suma en US2

### Implementación de User Story 1

- [X] T043 [US1] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs`: `ObtenerCuitEmpresaEmisoraAsync` —`null` sin fila o con CUIT vacío, como el Módulo 9—; `ConsultarPersonasActivasParaAdelantoAsync` con la proyección `PersonaParaAdelanto` de data-model §Beneficiarios (`Activa`, `TieneFicha`, `FichaActiva`, `CuitTransportista`) ordenada por apellido, nombre e `Id`; `ObtenerPersonaParaAdelantoAsync(id)` con **la misma proyección sin la condición de `Activa`**; y `RegistrarAsync` con la transacción de data-model §Registrar: inserta el adelanto `pendiente` y la entrada `registro` y devuelve el `Id`
- [X] T044 [P] [US1] Implementar `backend/src/GT.Application/Adelantos/ConsultarBeneficiarios.cs`: lee el CUIT de la emisora y las personas activas, y **filtra en memoria** con `ElegibilidadDeBeneficiario.Evaluar(tipo, persona, cuit) is null`; devuelve `empresaEmisoraConfigurada` (research §1)
- [X] T045 [US1] Implementar `backend/src/GT.Application/Adelantos/RegistrarAdelanto.cs` con el orden de validación de data-model §Registrar: campos presentes, `FechaAdmitida` contra `FechaHoyArgentina.Desde(reloj.GetUtcNow())`, motivo recortado de hasta 200, `ImporteValido`, y la elegibilidad con **la misma** `ElegibilidadDeBeneficiario` sobre `ObtenerPersonaParaAdelantoAsync`, traduciendo `EmpresaEmisoraNoConfigurada` a su propio código y el resto a `beneficiario_no_elegible` con el mensaje que nombra a la persona; llama a `RegistrarAsync` con el instante de `TimeProvider` y **relee el detalle**
- [X] T046 [US1] Implementar `backend/src/GT.Api/Adelantos/AdelantosEndpoints.cs` con `GET /api/adelantos/beneficiarios` (gestionar), `POST /api/adelantos` (gestionar, `201` con `Location`, usuario de la sesión por parámetro) y `GET /api/adelantos/{id:int}` (consultar, `404` con `adelanto_no_encontrado`); registrar `ConsultarBeneficiarios` y `RegistrarAdelanto` y mapear el grupo en `backend/src/GT.Api/Program.cs`
- [X] T047 [US1] Agregar a `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts`: `listarBeneficiarios(tipo)`, `registrarAdelanto(peticion)` y `obtenerAdelanto(id)`
- [X] T048 [US1] Implementar `frontend/src/modules/adelantos/paginas/RegistrarAdelanto.tsx`, con la prop `puedeGestionar`, según `contracts/README.md` §Registrar adelanto y plan §UI Design Check: sin `puedeGestionar` dibuja sólo el título y el aviso sin permiso de `contracts/README.md` §Pantallas, sin formulario (FR-043, US6 esc. 5); con él, sección 1 con *Tipo de persona* y *Persona* —**no deshabilitada**, sólo con su texto vacío sin tipo—; al elegir o cambiar el tipo vacía la persona, consulta `listarBeneficiarios`, conserva fecha, motivo e importe y anuncia en `role="status"` cuántas hay o que no hay ninguna (FR-003, FR-004, FR-040); callout de empresa emisora sin configurar con enlace a `/facturacion/empresa` cuando el tipo es chofer; sección 2 con *Fecha* propuesta con `hoyEnIso()`, `min` en `primeraFechaAdmitida()` y `max` en hoy, y la ayuda con el piso formateado; *Motivo* de hasta 200; *Importe* con el mismo control y la misma lectura de coma decimal que `frontend/src/modules/liquidaciones/componentes/DialogoOrdenDePago.tsx`; los errores de campo del contrato al perder el foco o al guardar, con el motivo recortado; barra con *Cancelar* y *Guardar adelanto* primario **no deshabilitado** salvo mientras envía; al guardar navega a `/adelantos/:id` con el mensaje de confirmación; rechazos del servidor en `role="alert"` sin perder lo cargado, y `fecha_fuera_de_rango` marcando la fecha con el `desde` recibido; un `403` al guardar muestra el texto de acción sin permiso del contrato
- [X] T049 [US1] Registrar en `frontend/src/App.tsx` la ruta `/adelantos/nuevo` con la misma protección de sesión y `Layout` que el resto, bajo un comentario `Rutas del Módulo 10`, pasándole a `RegistrarAdelanto` la prop `puedeGestionar` calculada con `tienePermiso(sesion, Permisos.adelantosGestionar)`
- [X] T050 [P] [US1] Escribir `frontend/src/modules/adelantos/paginas/RegistrarAdelanto.test.tsx` con los servicios mockeados: *Fecha* propuesta con el día fijado; *Persona* sin tipo no ofrece a nadie; elegir *Chofer* consulta el servicio y anuncia; cambiar el tipo vacía la persona y conserva fecha, motivo e importe; callout con emisora sin configurar; mensaje sin personas del tipo; guardar vacío marca los cinco campos y **no llama** a `registrarAdelanto`; importe `0` y con tres decimales marcados; fecha fuera de rango marcada con el piso; rechazo `beneficiario_no_elegible` en `role="alert"` conservando lo cargado; un `403` al guardar muestra el texto de acción sin permiso; sin `puedeGestionar` muestra el aviso sin permiso y ningún campo; éxito navega al detalle

**Checkpoint**: se registra un adelanto de punta a punta. La navegación final lleva al detalle, que se completa en US3.

---

## Phase 4: User Story 6 - Acceso restringido (Priority: P1)

**Goal**: que sólo quien corresponde opere o consulte, con la restricción en el servidor.

**Independent Test**: intentar cada ruta y cada endpoint sin sesión, con *Tráfico*, con *Gerencia* y con
*Administración de la empresa*.

### Tests para User Story 6

- [X] T051 [P] [US6] Escribir `backend/tests/GT.IntegrationTests/Adelantos/PermisosAdelantosTests.cs` con los endpoints que ya existen —`GET /beneficiarios`, `POST /`, `GET /{id}`—: sin sesión `401`; *Tráfico* `403`; *Gerencia* `200` en `GET /{id}` y `403` en `GET /beneficiarios` y `POST /`; *Administración de la empresa* y *Administrador del sistema* acceden a todo; el menú de la sesión trae las dos entradas, sólo *Consultar adelanto* o ninguna según el rol (FR-041 a FR-044, SC-010). Los endpoints de las historias siguientes se suman a este test en la tarea que los implementa

### Implementación de User Story 6

- [X] T052 [US6] Correr `frontend/src/compartido/seccionesDeMenu.test.ts` sin modificarlo y confirmar que sigue en verde con los dos códigos nuevos mapeados a *Operación*

**Checkpoint**: el registro está protegido por permiso y sólo quien corresponde lo ve en el menú.

---

## Phase 5: User Story 3 - Ver el detalle de un adelanto (Priority: P2)

**Goal**: ver a quién se le dio, cuándo, por qué, por cuánto, en qué estado está y qué pasó con él. Va
antes que el listado porque el registro termina navegando acá.

**Independent Test**: abrir un adelanto registrado, aprobado y anulado —armado con el ayudante de T020— y
comprobar sus datos, el motivo de la anulación y las tres operaciones con usuario e instante.

### Tests para User Story 3

- [X] T053 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Adelantos/DetalleAdelantoTests.cs`: persona con apellido, nombre y DNI, tipo, fecha, motivo, importe y estado; un rechazado trae `motivoRechazo` y su entrada `rechazo` con el motivo, y un anulado lo mismo con la anulación; historial de registro, aprobación y anulación en orden cronológico con usuario e instante con `Z`; **apellido vigente** después de corregirlo en el padrón (FR-020); **tipo `chofer`** después de dar de baja la ficha (US3 esc. 5); `puedeResolverse` y `puedeAnularse` en los cuatro estados; `404 adelanto_no_encontrado`

### Implementación de User Story 3

- [X] T054 [US3] Implementar `frontend/src/modules/adelantos/paginas/DetalleAdelanto.tsx` según `contracts/README.md` §Detalle: encabezado `Adelanto` con la pastilla y el contexto `Apellido, Nombre · Tipo · dd/mm/aaaa`; terciario *Volver a adelantos*; callouts de pendiente, rechazado y anulado **sin la última oración para quien sólo consulta**; tarjeta *Datos del adelanto*; tarjeta *Historial* con `LineaDeTiempo`, `Registrado/Aprobado/Rechazado/Anulado por …`, el motivo debajo en rechazo y anulación y el instante con `formatearInstante`; aside con la cifra destacada del importe y la oración según estado; estados de carga, de inexistente y **sin permiso** —un `403` al cargar, detectado con `esSinPermiso`, dibuja sólo el aviso del contrato con `role="alert"`, sin datos ni acciones (FR-043, US6 esc. 5)—; el mensaje que llega por navegación se anuncia con `role="status"` en el `<p>` que lo contiene (convención [008]). Las acciones se dibujan desde `puedeResolverse` / `puedeAnularse` **y** la prop `puedeGestionar`, pero se conectan en US4 y US5
- [X] T055 [US3] Registrar en `frontend/src/App.tsx` la ruta `/adelantos/:id` **después** de `/adelantos/nuevo`, pasándole a `DetalleAdelanto` la prop `puedeGestionar` calculada con `tienePermiso(sesion, Permisos.adelantosGestionar)`, igual que `DetalleLiquidacion`
- [X] T056 [P] [US3] Escribir `frontend/src/modules/adelantos/paginas/DetalleAdelanto.test.tsx`: datos y tipo; historial con los motivos; callout y aside de cada estado; quien sólo consulta no ve acciones ni la oración de instrucción; un pendiente con `puedeGestionar` muestra *Aprobar adelanto* y *Rechazar adelanto*, y un aprobado sólo *Anular adelanto*; anuncio del mensaje recibido por navegación; un `403` al cargar muestra el aviso sin permiso y no el de inexistente

**Checkpoint**: US1 queda completa de punta a punta: registrar lleva a un detalle correcto.

---

## Phase 6: User Story 2 - Consultar y filtrar los adelantos (Priority: P2)

**Goal**: responder rápido cuánto se le adelantó a una persona en un período.

**Independent Test**: con adelantos de dos personas en agosto y septiembre en los cuatro estados, aplicar
cada filtro por separado y combinados y comprobar las filas y que el total adelantado suma sólo los
aprobados.

### Tests para User Story 2

- [X] T057 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Adelantos/ConsultaAdelantosTests.cs`, cargando los escenarios con el ayudante de inserción directa de T020: sin filtro trae **todos, incluidos rechazados y anulados**; filtro por persona; con adelantos del 31/08, 01/09, 30/09 y 01/10 y el rango 01/09–30/09 trae sólo los dos del medio, y con sólo *desde* o sólo *hasta* aplica un extremo; `desde > hasta` responde `400 rango_invalido`; cada estado devuelve sólo los suyos y ninguno sale bajo dos; un estado desconocido se ignora; los tres filtros combinados; **total adelantado `200000`** con $150.000 y $50.000 aprobados, $30.000 pendiente, $20.000 rechazado y $40.000 anulado (US2 esc. 7); con filtro `pendiente` el total es **`0`** y no falla (research §9.3); con 25 aprobados de $1.000 la primera página trae 20 filas y el total es `25000`; orden por fecha y a igual fecha por `Id`, ambos descendentes, sin repetidos ni faltantes entre páginas; persona con el apellido vigente y el tipo guardado
- [X] T058 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Adelantos/PersonasConAdelantosTests.cs`: ofrece a un chofer propio con un adelanto `rechazado` que después se dio de baja, y **no** ofrece ni a un empleado activo sin adelantos ni a un chofer externo (US2 esc. 12); orden por apellido, nombre e `Id`

### Implementación de User Story 2

- [X] T059 [US2] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs`: `ConsultarAsync` con los cuatro filtros de data-model §Listado **antes** de contar, sumar y paginar, `totalAdelantado` con `Where(Estado == Aprobado).SumAsync(Importe)` sobre la misma consulta, orden `Fecha DESC, Id DESC` y proyección con la persona del padrón; y `ConsultarPersonasConAdelantosAsync` con la subconsulta de data-model §Personas con adelantos
- [X] T060 [P] [US2] Implementar `backend/src/GT.Application/Adelantos/ConsultarAdelantos.cs`: rechaza `desde > hasta` con `rango_invalido` sin consultar (FR-015) y devuelve `PaginaDeAdelantos`
- [X] T061 [P] [US2] Implementar `backend/src/GT.Application/Adelantos/ConsultarPersonasConAdelantos.cs`
- [X] T062 [US2] Agregar `GET /api/adelantos` (consultar; `personaId` y `pagina` anulables, `desde` y `hasta` como `DateOnly?`, `estado` como texto leído con `NombresDeEstadoAdelanto`) y `GET /api/adelantos/personas` (consultar) a `backend/src/GT.Api/Adelantos/AdelantosEndpoints.cs`; registrar los dos casos de uso en `backend/src/GT.Api/Program.cs`; sumar los dos endpoints a `backend/tests/GT.IntegrationTests/Adelantos/PermisosAdelantosTests.cs` —*Gerencia* `200` en los dos— y `GET /personas` a `backend/tests/GT.IntegrationTests/Adelantos/RutasAdelantosTests.cs`
- [X] T063 [US2] Agregar `listarAdelantos(filtros, pagina)`, `listarPersonasConAdelantos()`, `FILTROS_ADELANTOS_INICIALES` y `ESTADO_EN_ORACION` (`pendientes`, `aprobados`, `rechazados`, `anulados`) a `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts`
- [X] T064 [P] [US2] Implementar `frontend/src/modules/adelantos/componentes/FiltrosAdelantos.tsx` sobre `compartido/ui/Filtros`: persona (`Apellido, Nombre — DNI`, con `Todas las personas`), *Desde*, *Hasta* y estado (`Todos los estados`), con el borde más marcado en el filtro que tiene valor; el rango invertido marca los dos campos con `aria-invalid` y muestra debajo de *Hasta* el texto del contrato al cambiar cualquiera de los dos; más las ranuras de declaración y resumen
- [X] T065 [US2] Implementar `frontend/src/modules/adelantos/paginas/ListadoAdelantos.tsx` según `contracts/README.md` §Listado: título `Adelantos de sueldo`; primario *Registrar adelanto* sólo con `puedeGestionar`; declaración del filtro de estado en `role="status"`; resumen con cantidad, **`Total adelantado`** con `formatearPesos`, `Suma sólo los aprobados` y el criterio de orden; con el rango invertido **no consulta** y muestra `Corregí el rango de fechas para ver los adelantos.`; columnas con `FilaNavegable` y el enlace de la persona como destino real, con nombre accesible `Ver adelanto de Apellido, Nombre del dd/mm/aaaa`, DNI en mono debajo, motivo en una línea con `truncate`, importe a la derecha en negrita y estado en pastilla; filas `rechazado` y `anulado` con la clase `atenuada`; estados de carga, vacío —la segunda oración sólo con `puedeGestionar`—, sin coincidencias, error y **sin permiso** —un `403` al cargar, detectado con `esSinPermiso`, dibuja sólo el título y el aviso del contrato, sin filtros, resumen ni tabla, y no el texto de error de carga (FR-043, US6 esc. 5)—; `Paginacion`; cualquier cambio de filtro vuelve a la página 1
- [X] T066 [US2] Registrar en `frontend/src/App.tsx` la ruta `/adelantos`, pasándole a `ListadoAdelantos` la prop `puedeGestionar`, igual que `ListadoLiquidaciones`
- [X] T067 [P] [US2] Escribir `frontend/src/modules/adelantos/paginas/ListadoAdelantos.test.tsx`: columnas de un pendiente y de un rechazado y un anulado atenuados con su palabra; nombre accesible del enlace con la fecha; declaración con y sin filtro de estado; total adelantado del resumen; el rango invertido marca los campos y **no llama** a `listarAdelantos`; opciones del filtro de persona desde el servicio; mensajes de vacío y de sin coincidencias; un `403` muestra el aviso sin permiso sin filtros ni tabla, y no el error de carga; sin *Registrar adelanto* para quien sólo consulta

**Checkpoint**: los adelantos se consultan y filtran; el total adelantado de una persona en un mes se lee sin sumar a mano.

---

## Phase 7: User Story 4 - Aprobar o rechazar un adelanto (Priority: P2)

**Goal**: resolver un `pendiente` sin volver a cargar nada; rechazar con motivo y confirmación.

**Independent Test**: sobre dos adelantos `pendiente`, aprobar uno y rechazar el otro con motivo, y
comprobar los estados, el motivo visible y que ninguno vuelve a ofrecer aprobar ni rechazar.

### Tests para User Story 4

- [X] T068 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Adelantos/AprobacionYRechazoTests.cs`: aprobar un `pendiente` lo deja `aprobado` con una entrada `aprobacion`, también si lo registró el mismo usuario (FR-022) y si la persona se dio de baja después; aprobar o rechazar un `aprobado`, `rechazado` o `anulado` responde `409 adelanto_no_resoluble` con su `estado` **sin cambiar nada**; orden de validación del rechazo —`404`, `409 adelanto_no_resoluble` antes que el motivo, `400 motivo_requerido` (vacío y sólo espacios) y `datos_invalidos` (501 caracteres) antes que la confirmación—; **sin `confirmado` responde `409 rechazo_requiere_confirmacion` y el adelanto sigue `pendiente` sin entrada nueva** (US4 esc. 8); con `confirmado` queda `rechazado` con el motivo recortado y una entrada `rechazo`; la persona queda intacta (FR-037)
- [X] T069 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Adelantos/ResolucionConcurrenteTests.cs`: una aprobación y un rechazo confirmado simultáneos sobre el mismo `pendiente` —con dos alcances de servicio distintos y `Task.WhenAll`— terminan con **exactamente una** operación aplicada, la otra con `409 adelanto_no_resoluble` y el estado en que quedó, y **una sola** entrada de resolución en el historial; y lo mismo con dos aprobaciones simultáneas (FR-026, research §2)

### Implementación de User Story 4

- [X] T070 [US4] Agregar `AprobarAsync` y `RechazarAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs` con las transacciones de data-model §Aprobar y §Rechazar: `UPDATE` condicional con `ExecuteUpdateAsync` sobre `Estado = Pendiente` —el rechazo fija también `MotivoRechazo`—, verificación de **una fila afectada** antes de insertar la entrada del historial, y `false` con `rollback` si no afectó ninguna
- [X] T071 [P] [US4] Implementar `backend/src/GT.Application/Adelantos/AprobarAdelanto.cs`: existencia, `MotivoNoResoluble` con la consulta previa, `AprobarAsync` con el instante de `TimeProvider`; si no afectó filas **relee** y responde `adelanto_no_resoluble` con el estado actual; si no, relee el detalle
- [X] T072 [P] [US4] Implementar `backend/src/GT.Application/Adelantos/RechazarAdelanto.cs` con el orden existencia, estado, motivo recortado de hasta 500, `confirmado = true` —sin él `rechazo_requiere_confirmacion`—, y la misma relectura ante cero filas que T071
- [X] T073 [US4] Implementar `backend/src/GT.Api/Adelantos/CicloDeVidaAdelantoEndpoints.cs` con `POST /api/adelantos/{id:int}/aprobacion` (sin cuerpo) y `POST /api/adelantos/{id:int}/rechazo`, los dos por gestionar y `200` con el detalle releído; registrar los casos de uso y el grupo en `backend/src/GT.Api/Program.cs`, y sumar los dos endpoints a `backend/tests/GT.IntegrationTests/Adelantos/PermisosAdelantosTests.cs` —*Gerencia* `403`—
- [X] T074 [US4] Agregar `aprobarAdelanto(id)` y `rechazarAdelanto(id, motivo)` —que **siempre envía `confirmado: true`** porque el diálogo es la confirmación (research §4)— a `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts`
- [X] T075 [P] [US4] Implementar `frontend/src/modules/adelantos/componentes/DialogoRechazo.tsx` sobre `compartido/ui/Dialogo` según `contracts/README.md` §Rechazar adelanto: subtítulo con persona e importe, texto que avisa que no se puede deshacer, *Motivo* obligatorio de hasta 500 con su error al perder el foco o al enviar —sólo espacios cuenta como vacío—, *Volver* secundario que no llama al servidor y *Rechazar adelanto* **primario**, no deshabilitado salvo mientras envía; el rechazo del servidor se muestra adentro con `role="alert"`, y un `403` con el texto de acción sin permiso. Tomar como molde `frontend/src/modules/liquidaciones/componentes/DialogoAnulacion.tsx`
- [X] T076 [US4] Conectar en `frontend/src/modules/adelantos/paginas/DetalleAdelanto.tsx` *Aprobar adelanto* —deshabilitado mientras espera, relee y anuncia `Se aprobó el adelanto de … para …`; un `409` se muestra en `role="alert"` y **relee el detalle** para mostrar el estado actual, y un `403` muestra el texto de acción sin permiso— y *Rechazar adelanto* al diálogo, que al terminar cierra, relee y anuncia `Se rechazó el adelanto de … para …`
- [X] T077 [P] [US4] Escribir `frontend/src/modules/adelantos/componentes/DialogoRechazo.test.tsx`: motivo vacío y de sólo espacios marcan el campo y no envían; *Volver* no envía; confirmar envía el motivo recortado; el rechazo del servidor aparece adentro del diálogo; un `403` muestra el texto de acción sin permiso
- [X] T078 [US4] Sumar a `frontend/src/modules/adelantos/paginas/DetalleAdelanto.test.tsx` los casos de US4: *Aprobar adelanto* llama al servicio, anuncia y muestra el estado releído; un `409 adelanto_no_resoluble` muestra su texto y relee; un `403` al aprobar muestra el texto de acción sin permiso; el rechazo por el diálogo anuncia el resultado

**Checkpoint**: un pendiente se aprueba o se rechaza con motivo y confirmación, sin pisarse entre dos usuarios.

---

## Phase 8: User Story 5 - Anular un adelanto aprobado (Priority: P3)

**Goal**: anular con motivo y confirmación un adelanto aprobado, dejando constancia sin borrarlo.

**Independent Test**: anular un aprobado con motivo y comprobar que queda `anulado` con su motivo visible y
sigue en el listado; cancelar la anulación de otro y comprobar que sigue `aprobado`.

### Tests para User Story 5

- [X] T079 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Adelantos/AnulacionTests.cs`: orden de validación —`404`, `409 adelanto_no_anulable` con su `estado` para `pendiente`, `rechazado` y `anulado` antes que el motivo, `400 motivo_requerido` y `datos_invalidos` antes que la confirmación—; **sin `confirmado` responde `409 anulacion_requiere_confirmacion` sin cambiar nada** (US5 esc. 7); con `confirmado` queda `anulado` con el motivo recortado y una entrada `anulacion`; el anulado sigue en el listado y **deja de sumar** en el total adelantado; la persona queda intacta (FR-037)
- [X] T080 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Adelantos/AnulacionConcurrenteTests.cs`: dos anulaciones confirmadas simultáneas del mismo `aprobado` terminan con **exactamente una** aplicada, la otra con `409 adelanto_no_anulable` y estado `anulado`, y una sola entrada `anulacion` (FR-032)

### Implementación de User Story 5

- [X] T081 [US5] Agregar `AnularAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs` con la transacción de data-model §Anular: `UPDATE` condicional sobre `Estado = Aprobado` que fija `Estado = Anulado` y `MotivoAnulacion`, verificación de una fila afectada y entrada `anulacion`
- [X] T082 [US5] Implementar `backend/src/GT.Application/Adelantos/AnularAdelanto.cs` con el orden existencia, `MotivoNoAnulable`, motivo recortado de hasta 500 y `confirmado = true`, y la relectura ante cero filas para responder con el estado actual
- [X] T083 [US5] Agregar `POST /api/adelantos/{id:int}/anulacion` por gestionar a `backend/src/GT.Api/Adelantos/CicloDeVidaAdelantoEndpoints.cs`; registrar el caso de uso en `backend/src/GT.Api/Program.cs` y sumar el endpoint a `backend/tests/GT.IntegrationTests/Adelantos/PermisosAdelantosTests.cs`
- [X] T084 [US5] Agregar `anularAdelanto(id, motivo)` —que siempre envía `confirmado: true`— a `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts`
- [X] T085 [P] [US5] Implementar `frontend/src/modules/adelantos/componentes/DialogoAnulacion.tsx` sobre `compartido/ui/Dialogo` según `contracts/README.md` §Anular adelanto: texto con importe y persona que avisa que deja de sumar y no se deshace, *Motivo* obligatorio de hasta 500 con su error, *Volver* secundario y *Anular adelanto* **destructivo, sin primario**; el rechazo del servidor adentro con `role="alert"`, y un `403` con el texto de acción sin permiso
- [X] T086 [US5] Conectar en `frontend/src/modules/adelantos/paginas/DetalleAdelanto.tsx` la acción destructiva *Anular adelanto* al diálogo: al terminar cierra, relee el detalle y anuncia `Se anuló el adelanto de … para …`
- [X] T087 [P] [US5] Escribir `frontend/src/modules/adelantos/componentes/DialogoAnulacion.test.tsx`: motivo vacío y de sólo espacios marcan el campo y no envían; *Volver* no envía; confirmar envía el motivo recortado; el rechazo `adelanto_no_anulable` aparece adentro del diálogo; un `403` muestra el texto de acción sin permiso
- [X] T088 [US5] Sumar a `frontend/src/modules/adelantos/paginas/DetalleAdelanto.test.tsx` los casos de US5: *Anular adelanto* abre el diálogo; confirmar la anulación cierra el diálogo, relee el detalle, muestra el estado `anulado` y anuncia `Se anuló el adelanto de … para …`; *Volver* deja el detalle sin cambios

**Checkpoint**: las seis historias funcionan de punta a punta.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: la suite entera, el recorrido manual y el mantenimiento.

- [X] T089 Revisar en `frontend/src/App.tsx` que `/adelantos`, `/adelantos/nuevo` y `/adelantos/:id` usen la misma protección de sesión que el resto —sin sesión redirige a `/ingresar` (FR-044)— y dejar anotado en el comentario del bloque el reparto consultar/gestionar de las tres, como el del Módulo 9. La verificación la hace el paso 1 del quickstart (T091)
- [X] T090 Correr `cd backend && dotnet test` y `cd frontend && npm test`, más el build y el lint del frontend, y dejar todo en verde; confirmar que las suites de `FichaFactura`, `AltaFactura` y `DetalleLiquidacion` pasan **sin haber modificado ningún caso existente** —cada una suma sólo su caso de T032 a T034— y que `AsignarRolesTests` sólo cambió lo de T019 (spec §Assumptions)
- [ ] T091 Recorrer los 43 pasos de `specs/010-gestion-adelantos/quickstart.md` con las cuatro cuentas (`admin`, `admin.empresa`, `gerencia`, `trafico`), incluidos los dos pasos de dos navegadores (13 y 20) y los tres de los Módulos 6 y 9 (41 a 43), y anotar lo que el recorrido encuentre
- [X] T092 [P] Agregar la fila del Módulo 10 con su estado y conteo de tareas, y su entrada en *Qué queda abierto* y *Lo que cada módulo dejó como precedente*, en `specs/README.md`
- [X] T093 Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (`[010] ...`), en *Decisiones transversales ya tomadas*. Partir de las candidatas de `plan.md` §Mantenimiento al cerrar la feature y **no incluir entradas por incluir**: sólo las que sean información transversal y relevante para futuras features

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup — **bloquea todas las historias**
- **User Stories (Phase 3 a 8)**: dependen de Foundational
- **Polish (Phase 9)**: depende de todas las historias

### Dentro de Foundational

- T032, T033 y T034 dependen de T031 —la función compartida tiene que existir— y entre sí son independientes
- T038 depende de T031 (`enIso`); T039 depende de T038
- T015 depende de T013 y T014; T016 de T015; T021 de T016 y T020
- T027 depende de T026; T028 de T027; T030 de T025

### User Story Dependencies

- **US1 — Registrar (P1)**: sólo Foundational. Su navegación final necesita la pantalla de US3 para verse completa, pero el backend y el formulario se prueban solos
- **US6 — Acceso (P1)**: necesita US1 —T051 prueba los endpoints que crea T046—; su test suma los endpoints de cada historia a medida que existen
- **US3 — Detalle (P2)**: necesita US1 —el caso de uso del detalle está en T027 y T028, pero `GET /api/adelantos/{id:int}` lo expone T046 y `obtenerAdelanto` lo agrega T047—. Además cierra el recorrido del registro
- **US2 — Listado (P2)**: sólo Foundational
- **US4 — Aprobar y rechazar (P2)**: necesita la pantalla de detalle de US3, donde viven sus acciones
- **US5 — Anular (P3)**: necesita US3 (acción en el detalle); sus escenarios de backend parten de un `aprobado`, que el ayudante de T020 inserta sin depender de US4

### Archivos que tocan varias historias

No llevan `[P]` entre sí y se hacen en el orden de las fases:

- `backend/src/GT.Infrastructure/Persistencia/RepositorioAdelantos.cs` — T027, T043, T059, T070, T081
- `backend/src/GT.Api/Program.cs` — T029, T046, T062, T073, T083
- `backend/src/GT.Api/Adelantos/AdelantosEndpoints.cs` — T046, T062
- `backend/src/GT.Api/Adelantos/CicloDeVidaAdelantoEndpoints.cs` — T073, T083
- `backend/tests/GT.IntegrationTests/Adelantos/PermisosAdelantosTests.cs` — T051, T062, T073, T083
- `backend/tests/GT.IntegrationTests/Adelantos/RutasAdelantosTests.cs` — T042, T062
- `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts` — T038, T047, T063, T074, T084
- `frontend/src/App.tsx` — T049, T055, T066, T089
- `frontend/src/modules/adelantos/paginas/DetalleAdelanto.tsx` — T054, T076, T086
- `frontend/src/modules/adelantos/paginas/DetalleAdelanto.test.tsx` — T056, T078, T088

### Within Each User Story

- Los tests se escriben primero y tienen que fallar antes de implementar. **Excepción declarada**: T053 prueba un detalle que ya implementan T027, T028 y T046, así que nace en verde; lo que protege es que el detalle siga cumpliendo FR-019 y FR-020
- Repositorio → caso de uso → endpoint → servicio del frontend → componente → página → ruta

### Parallel Opportunities

- Setup: T001 y T002
- Foundational: el dominio T003–T010, los tests T011–T012, las configuraciones T013–T014, las piezas de aplicación T022–T025 y el frontend T031, T035, T036, T037; con T031 terminada, T032, T033 y T034 a la vez
- Dentro de cada historia: todos sus tests `[P]`, y los componentes que no son la página
- Con Foundational terminado, US1 y US2 pueden avanzar a la vez en backend; US6 y US3 esperan a los endpoints y al servicio de US1 (T046, T047), y US4 y US5 a la pantalla de detalle

---

## Parallel Example: User Story 1

```bash
# Los tres tests de la historia, juntos:
Task: "BeneficiariosTests en backend/tests/GT.IntegrationTests/Adelantos/BeneficiariosTests.cs"
Task: "RegistroTests en backend/tests/GT.IntegrationTests/Adelantos/RegistroTests.cs"
Task: "RutasAdelantosTests en backend/tests/GT.IntegrationTests/Adelantos/RutasAdelantosTests.cs"

# Después del repositorio (T043), el caso de uso de consulta y el test de pantalla:
Task: "ConsultarBeneficiarios en backend/src/GT.Application/Adelantos/ConsultarBeneficiarios.cs"
Task: "RegistrarAdelanto.test en frontend/src/modules/adelantos/paginas/RegistrarAdelanto.test.tsx"
```

## Parallel Example: Foundational — los cambios a los Módulos 6 y 9

```bash
# Con T031 terminada, los tres refactores tocan archivos distintos:
Task: "Cambio 1 en frontend/src/modules/facturacion/paginas/FichaFactura.tsx (+ su test)"
Task: "Cambio 2 en frontend/src/modules/facturacion/paginas/AltaFactura.tsx (+ su test)"
Task: "Cambio 3 en frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx (+ su test)"
```

---

## Implementation Strategy

### MVP First

1. Phase 1: Setup
2. Phase 2: Foundational (**bloquea todo**)
3. Phase 3: US1 — Registrar
4. Phase 4: US6 — Acceso
5. Phase 5: US3 — Detalle, que cierra el recorrido del registro
6. **Parar y validar**: pasos 1 a 14, 26 y 28 de `quickstart.md`. El 27 abre el adelanto **D**, que se registra en el paso 15 y se rechaza en el 19: se valida recién con US4

El MVP es **registrar, ver y estar protegido**: con eso los adelantos dejan de anotarse sueltos y quedan
sobre la persona, con quién los cargó y cuándo.

### Incremental Delivery

1. Setup + Foundational → la base está lista y las pantallas de los Módulos 6 y 9 siguen iguales
2. US1 + US6 + US3 → MVP
3. US2 → cuánto se le adelantó a cada persona en el mes, sin sumar a mano
4. US4 → el circuito pendiente / aprobado / rechazado
5. US5 → se anula un aprobado por error
6. Polish → suite entera, recorrido manual, `specs/README.md` y `AGENTS.md`

---

## Notes

- `[P]` = archivo distinto y sin dependencias pendientes
- `[Story]` vincula la tarea a su historia para la trazabilidad
- Cada historia se puede completar y probar sola, con las dependencias de pantalla declaradas arriba
- **Trampas conocidas** (research §9): valores de enum escritos a mano en los `CHECK`; `LEN(NULL)` deja pasar la fila si no va detrás de `IS NOT NULL`; `SUM` sin filas es `NULL`; rutas literales antes de `{id:int}`; `ExecuteUpdateAsync` no pasa por el rastreador, así que toda escritura relee el detalle; "hoy" es el de Argentina en el servidor; el motivo se recorta antes de medirlo; un importe fuera de `decimal(18,2)` es formato inválido y no un `500`; el piso de la fecha cruza el año
- **El desplegable de beneficiarios filtra en memoria a propósito** (research §1): no "arreglarlo" moviendo el predicado a SQL, porque eso separa la regla del guardado
- Commit después de cada tarea o grupo lógico, sin firmar como coautor
