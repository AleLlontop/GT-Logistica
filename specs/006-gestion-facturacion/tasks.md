# Tasks: Gestión de facturación (Módulo 6)

**Input**: Documentos de diseño de `/specs/006-gestion-facturacion/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/README.md`,
`contracts/facturacion-api.yaml`, `quickstart.md`

**Tests**: se incluyen. El plan los pide explícitamente (`Technical Context` → *Testing*) y el
`quickstart.md` nombra cuatro clases de test como la única forma de verificar lo que una persona no
puede comprobar a mano: `ArmadorDocumentoFacturaTests`, `EmisionConcurrenteTests`, `VistaPreviaTests`,
`DerivacionVencidaTests`, más `IndicesDeFacturaTests`.

**Organization**: las tareas se agrupan por historia de usuario, para poder implementar y validar cada
una por separado.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: puede ejecutarse en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1 … US7)
- Cada tarea lleva la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados (plan.md → *Project Structure*):

- Backend: `backend/src/GT.Api/`, `backend/src/GT.Application/`, `backend/src/GT.Domain/`,
  `backend/src/GT.Infrastructure/`
- Tests de backend: `backend/tests/GT.UnitTests/`, `backend/tests/GT.IntegrationTests/`
- Frontend: `frontend/src/modules/facturacion/`

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar el proyecto en condiciones de compilar y de generar un PDF. Es la única fase con
cambios de infraestructura, y el `Dockerfile` es el punto que falla en producción sin fallar antes
(research §1, research §15.3).

- [X] T001 Agregar la dependencia `QuestPDF` versión `2026.7.3` en `backend/src/GT.Infrastructure/GT.Infrastructure.csproj`, y anotar en un comentario la condición de licencia (gratuita bajo USD 1M de facturación anual, research §1)
- [X] T002 Fijar `QuestPDF.Settings.License = LicenseType.Community` al registrar los servicios de infraestructura en `backend/src/GT.Api/Program.cs`; sin eso el armador tira excepción en la primera invocación
- [X] T003 Instalar `libfontconfig1` y `libfreetype6` con `apt-get` en la **etapa de ejecución** de `backend/Dockerfile`; sin ellos el backend compila, arranca y falla recién al emitir la primera factura (research §1, §15.3)
- [X] T004 [P] Crear las carpetas del backend del módulo: `backend/src/GT.Domain/Facturacion/`, `backend/src/GT.Application/Facturacion/` (con subcarpeta `EmpresaEmisora/`), `backend/src/GT.Api/Facturacion/`, `backend/src/GT.Infrastructure/Documentos/`, `backend/tests/GT.UnitTests/Facturacion/` y `backend/tests/GT.IntegrationTests/Facturacion/`
- [X] T005 [P] Crear el esqueleto del módulo de frontend `frontend/src/modules/facturacion/` con las subcarpetas `paginas/`, `componentes/` y `servicios/`

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: esquema, entidades, permisos y el armador del documento. Nada de esto pertenece a una
historia sola: las siete lo usan.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa.

### Dominio: enumeraciones y entidades

- [X] T006 [P] Crear `EstadoFactura` (`Pendiente = 0`, `Pagada = 1`, `Anulada = 2`) en `backend/src/GT.Domain/Facturacion/EstadoFactura.cs`, con el comentario ⚠ que advierte que `IX_Facturas_Numero` lleva el `<> 2` escrito a mano y reordenar el enum no falla al compilar (data-model §Enumeraciones, research §15.2)
- [X] T007 [P] Crear `EstadoFacturaVisible` (`Pendiente`, `Vencida`, `Pagada`, `Anulada`) en `backend/src/GT.Domain/Facturacion/EstadoFacturaVisible.cs`, documentando que no es columna de ninguna tabla
- [X] T008 [P] Crear `TipoComprobante`, `TipoFacturacion` y `CondicionDeVenta` en `backend/src/GT.Domain/Facturacion/TiposDeFactura.cs` con los valores numéricos de data-model §Enumeraciones
- [X] T009 [P] Crear la entidad `EmpresaEmisora` con sus catorce columnas en `backend/src/GT.Domain/Facturacion/EmpresaEmisora.cs` (data-model §Tabla `EmpresaEmisora`)
- [X] T010 [P] Crear la entidad `FacturaCliente` en `backend/src/GT.Domain/Facturacion/FacturaCliente.cs`: identificación, las trece columnas congeladas (diez del emisor, tres del cliente), importes `decimal`, CAE, estado, refacturación y `DocumentoRuta` no anulable (data-model §Tabla `Facturas`)
- [X] T011 [P] Crear la entidad `CambioDeEstadoFactura` en `backend/src/GT.Domain/Facturacion/CambioDeEstadoFactura.cs`, con `EstadoAnterior` y `EstadoNuevo` anulables y el comentario de que **una entrada es una corrección cuando `EstadoNuevo` es `null`** (FR-037, data-model)

### Dominio: los cambios al Módulo 5 (FR-051 a FR-053)

- [X] T012 Agregar `Facturado = 4` **al final** de `backend/src/GT.Domain/Viajes/EstadoViaje.cs`, sin reordenar los cuatro valores existentes, con el comentario de por qué (los tres índices filtrados de `Viajes` llevan `1` y `3` escritos a mano)
- [X] T013 Agregar los pares `rendido → facturado` y `facturado → rendido` e incorporar `Facturado` a `EsTerminal` en `backend/src/GT.Domain/Viajes/TransicionesDeViaje.cs`, documentando la trampa de research §8.2: que el par exista no abre ningún camino HTTP nuevo porque los tres endpoints de ciclo de vida del Módulo 5 tienen el estado destino fijo
- [X] T014 Agregar `FacturaId` anulable y la navegación a `backend/src/GT.Domain/Viajes/Viaje.cs`
- [X] T015 [P] Agregar los tres códigos de permiso —`facturacion.gestionar`, `facturacion.consultar`, `facturacion.anular`— en `backend/src/GT.Domain/Usuarios/Rol.cs`

### Persistencia

- [X] T016 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/EmpresaEmisoraConfiguracion.cs` con `CHECK ([Id] = 1)` y los largos de columna de data-model (research §12)
- [X] T017 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/FacturaConfiguracion.cs`: columnas, `decimal(18,2)` para los tres importes, `CHECK ([Total] = [Neto] + [Iva])`, `CHECK` de `PeriodoMes` entre 1 y 12, FK `ClienteId` y `FacturaReemplazadaId` en `Restrict`, y los cinco índices de data-model §Índices —incluidos los dos únicos filtrados
- [X] T018 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CambioDeEstadoFacturaConfiguracion.cs`
- [X] T019 Agregar `FacturaId` con FK en `Restrict` e `IX_Viajes_FacturaId` (no único) en `backend/src/GT.Infrastructure/Persistencia/Configuraciones/ViajeConfiguracion.cs`, sin tocar los tres índices filtrados existentes
- [X] T020 Agregar los tres `DbSet` —`EmpresaEmisora`, `Facturas`, `CambiosDeEstadoFactura`— en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs`
- [X] T021 Generar y revisar la migración `Modulo6Facturacion` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, verificando que **no** contenga migración de datos: `FacturaId` nace nula y ningún viaje cambia de estado (data-model §Tabla `Viajes`)
- [X] T022 Sembrar los tres permisos y su reparto por rol —gestionar y anular según research §7— en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`, de forma idempotente
- [X] T023 Agregar las cuatro entradas de menú —*Facturas*, *Vencimientos*, *Totales facturados* por `facturacion.consultar`; *Empresa emisora* por `facturacion.gestionar`— en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`, con el nombre `Totales facturados` para no chocar con la entrada `Totales` del Módulo 5 (contracts/README §Pantallas)

### Capa de aplicación: piezas compartidas

- [X] T024 [P] Crear `backend/src/GT.Application/Facturacion/NombresDeEstadoFactura.cs` con la traducción al español de los cuatro estados visibles, los tres tipos de comprobante, los dos tipos de facturación y las cuatro condiciones de venta (convención [003]: camelCase en el JSON)
- [X] T025 [P] Crear `backend/src/GT.Application/Facturacion/Mensajes.cs` con los textos en es-AR y los códigos de error de `contracts/README.md` (`numero_duplicado`, `viaje_ya_facturado`, `viaje_sin_remito`, `cliente_sin_domicilio`, `cliente_inactivo`, `anulada_ya_reemplazada`, `refacturacion_sin_reemplazada`, `vencimiento_pago_anterior`, `cae_vencimiento_anterior`, `sin_viajes_seleccionados`, `numero_invalido`)
- [X] T026 [P] Crear `backend/src/GT.Application/Facturacion/Dtos.cs` con los contratos de `facturacion-api.yaml`; **los campos `neto`, `iva` y `total` no existen en el cuerpo del `POST`** (FR-024, research §9)
- [X] T027 [P] Crear `backend/src/GT.Application/Facturacion/IRepositorioFacturas.cs` e `IRepositorioEmpresaEmisora.cs`

### El armador del documento (research §1, §2)

- [X] T028 Crear `backend/src/GT.Application/Facturacion/IArmadorDocumentoFactura.cs` y `DatosDelDocumento.cs`, con el **mapeo único desde la entidad `FacturaCliente`** —la que existe en memoria y la que ya está guardada usan la misma función— más el logo vigente leído de la configuración (FR-033, FR-034)
- [X] T029 Implementar `backend/src/GT.Infrastructure/Documentos/ArmadorDocumentoFacturaQuestPdf.cs` con los nueve bloques de FR-031 en orden: banda de ejemplar, bloque del emisor (con el logo opcional, FR-031g), recuadro de letra con su código (FR-031i), bloque de identificación con el período `MM/AAAA`, banda de vencimiento de pago, banda de CBU —omitida si está vacío—, bloque del cliente con `Responsable Inscripto` y el campo `Remito` vacío (FR-031h), tabla de detalle con una fila por viaje y sus nueve columnas (FR-031e), y pie de importes con `Observaciones` a la izquierda —omitido entero cuando el detalle está vacío—. Misma disposición para los tres tipos (FR-031j); importes y fechas con el formato del resto del sistema
- [X] T030 [P] Escribir `backend/tests/GT.IntegrationTests/Facturacion/ArmadorDocumentoFacturaTests.cs`: genera un PDF **de verdad** a partir de una `FacturaCliente` en memoria y verifica que no sea vacío. Es el test que detecta la falta de `libfontconfig1` en CI y el primer paso del `quickstart.md`

### Frontend y API: andamiaje

- [X] T031 Crear `backend/src/GT.Api/Facturacion/RespuestasDeFactura.cs` con la traducción resultado → HTTP en un solo lugar, siguiendo la tabla de research §11 (`400` para lo que se tipeó, `409` para el estado de lo compartido y para la confirmación pendiente)
- [X] T032 Registrar el grupo de endpoints del módulo en `backend/src/GT.Api/Program.cs` con las políticas de los tres permisos y **`{id:int}` en la ruta de identificador**: sin la restricción, `facturables`, `vista-previa`, `anuladas-sin-reemplazo`, `vencimientos` y `totales` quedan inalcanzables y no falla ni al compilar ni al arrancar (convención [005], research §15.1)
- [X] T033 [P] Crear `frontend/src/modules/facturacion/servicios/api.ts` con los tipos del contrato y el cliente HTTP compartido, y registrar las siete rutas del módulo en `frontend/src/App.tsx` (contracts/README §Pantallas)
- [X] T034 [P] Escribir `backend/tests/GT.IntegrationTests/Facturacion/IndicesDeFacturaTests.cs`: inserta una fila en cada estado y verifica dónde `IX_Facturas_Numero` y `IX_Facturas_FacturaReemplazada` aceptan y dónde rechazan, para que reordenar `EstadoFactura` no deje los índices protegiendo el estado equivocado

**Checkpoint**: la base compila, migra, arranca, siembra los permisos y genera un PDF. Las historias pueden empezar.

---

## Phase 3: User Story 1 - Configurar la empresa emisora (Priority: P1) 🎯 MVP

**Goal**: cargar una sola vez los datos con los que sale toda factura, más el logo, para que ninguna
factura nueva los pida.

**Independent Test**: entrar con la configuración vacía y comprobar que el alta de factura informa qué
datos faltan y no deja continuar; cargar después los datos y el logo y comprobar que la configuración
queda guardada y que la vista previa de una factura nueva los muestra sin haberlos escrito ahí.

### Tests para User Story 1

- [X] T035 [P] [US1] Escribir `backend/tests/GT.UnitTests/Facturacion/EmpresaEmisoraTests.cs`: normalización y validación del CUIT con `NormalizadorDocumentoNumerico` y `ValidadorCuit` del Módulo 3, formato de email, y la lista de los cuatro obligatorios faltantes que devuelve el `GET` sin fila
- [X] T036 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Facturacion/EmpresaEmisoraEndpointsTests.cs`: `GET` sin fila responde `configurada: false` con los campos faltantes; el `PUT` crea la fila la primera vez y la actualiza después; el `CHECK ([Id] = 1)` impide una segunda fila; el logo se valida por **firma** y rechaza PDF; los cinco endpoints exigen `facturacion.gestionar`

### Implementación de User Story 1

- [X] T037 [US1] Implementar `backend/src/GT.Infrastructure/Persistencia/RepositorioEmpresaEmisora.cs` (leer la única fila, crearla o actualizarla)
- [X] T038 [P] [US1] Implementar `backend/src/GT.Application/Facturacion/EmpresaEmisora/ConsultarEmpresaEmisora.cs`: devuelve `configurada: false` con los obligatorios faltantes cuando la fila no existe (research §12, US1 esc. 1)
- [X] T039 [US1] Implementar `backend/src/GT.Application/Facturacion/EmpresaEmisora/GuardarEmpresaEmisora.cs` con las validaciones de FR-002 y el `Trim` al guardar
- [X] T040 [US1] Implementar `backend/src/GT.Application/Facturacion/EmpresaEmisora/GestionarLogo.cs`: subir, reemplazar y quitar con `IAlmacenDeArchivos` y `ValidadorArchivo` del Módulo 3, **admitiendo sólo JPG y PNG** —el caso de uso decide, el validador deduce el tipo de la firma— y sirviendo el archivo con `ResultadoArchivo.EnLinea` (FR-003, research §6)
- [X] T041 [US1] Implementar `backend/src/GT.Api/Facturacion/EmpresaEmisoraEndpoints.cs` con los cinco endpoints de `facturacion-api.yaml`: `GET`/`PUT` de la configuración y `GET`/`PUT`/`DELETE` del logo
- [X] T042 [P] [US1] Implementar `frontend/src/modules/facturacion/servicios/servicioEmpresaEmisora.ts`
- [X] T043 [US1] Implementar `frontend/src/modules/facturacion/paginas/EmpresaEmisora.tsx`: formulario de diez campos con el aviso de "todavía no está configurada" arriba cuando corresponde, el guardado que **no cambia de pantalla** y se anuncia con `role="status"`, y los mensajes exactos de `contracts/README.md`
- [X] T044 [US1] Implementar `frontend/src/modules/facturacion/componentes/CargaDeLogo.tsx`: zona propia dentro de la misma pantalla, textos de "sin logo", ayuda `JPG o PNG, hasta 10 MB.`, botones *Reemplazar* y *Quitar* —quitar **no** pide confirmación aparte (precedente [004])
- [X] T045 [P] [US1] Escribir `frontend/src/modules/facturacion/paginas/EmpresaEmisora.test.tsx`: mensaje de sin configurar, marcado del CUIT inválido, anuncio del guardado, rechazo del archivo no admitido

**Checkpoint**: la empresa emisora se configura y se consulta de forma independiente.

---

## Phase 4: User Story 2 - Emitir una factura agrupando viajes rendidos (Priority: P1)

**Goal**: elegir cliente y período, seleccionar los viajes rendidos sin facturar, ver los importes
calcularse solos, revisar la vista previa y emitir, dejando los viajes en `facturado`.

**Independent Test**: con la empresa emisora configurada, un cliente con tres viajes rendidos del mismo
mes y uno `en curso`, comprobar que se ofrecen sólo los tres, que el neto es la suma exacta de sus
importes, que después de confirmar los tres figuran `facturado` y que al rearmar la factura del mismo
cliente y período ya no aparecen.

### Reglas puras del dominio

- [X] T046 [P] [US2] Implementar `backend/src/GT.Domain/Facturacion/AlicuotasIva.cs`: `21 / 21 / 0` fijas por tipo de comprobante, sin configuración de ninguna pantalla (FR-023)
- [X] T047 [P] [US2] Implementar `backend/src/GT.Domain/Facturacion/CalculadorImportes.cs`: neto como suma exacta, IVA con `Math.Round(neto * alícuota, 2, MidpointRounding.AwayFromZero)` y total = neto + IVA, todo en `decimal` (FR-022, FR-023, research §9)
- [X] T048 [P] [US2] Implementar `backend/src/GT.Domain/Facturacion/NumeroDeComprobante.cs`: validación del formato `0000-00000000` (FR-027)

### Tests para User Story 2

- [X] T049 [P] [US2] Escribir `backend/tests/GT.UnitTests/Facturacion/CalculadorImportesTests.cs` con el ejemplo de la propia spec (`82.644,63` → IVA `17.355,37` → total `100.000,00`), el caso de `Factura C` (IVA `0,00`, total igual al neto) y **un caso armado para que la suma de los subtotales por fila difiera del total**, verificando que manda el pie (FR-031f)
- [X] T050 [P] [US2] Escribir `backend/tests/GT.UnitTests/Facturacion/NumeroDeComprobanteTests.cs` con formatos válidos e inválidos
- [X] T051 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Facturacion/EmisionFacturaTests.cs`: la emisión deja los viajes en `facturado` con su `FacturaId`, escribe la entrada de emisión en el historial de la factura y una línea de `CambioDeEstadoViaje` por viaje; el número duplicado se rechaza identificando la factura que lo usa; el viaje sin remito, el cliente sin domicilio y la empresa emisora incompleta se rechazan con `400` sin crear nada; si el documento no se puede generar **no se crea nada y el número queda libre**
- [X] T052 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Facturacion/EmisionConcurrenteTests.cs`: lanza en paralelo dos emisiones que comparten un viaje contra el SQL Server del compose y verifica que se crea exactamente una, que la otra recibe el rechazo nombrando el viaje y el comprobante, y que no queda ninguna factura con viajes sin marcar (SC-005)
- [X] T053 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Facturacion/VistaPreviaTests.cs`: genera el PDF de la vista previa y el guardado al emitir sobre la misma factura y los compara **byte a byte**; verifica además que pedir la vista previa no crea la factura ni escribe ningún archivo (SC-007b, FR-033)

### Implementación de User Story 2

- [X] T054 [US2] Implementar `backend/src/GT.Application/Facturacion/ConsultarFacturables.cs`: viajes del cliente, en estado `rendido`, con fecha dentro del mes y año elegidos y sin factura vigente; los que no tienen remito **se devuelven igualmente, marcados**, para que la pantalla los muestre con la palabra que lo explica (FR-015 a FR-019a)
- [X] T055 [US2] Implementar `backend/src/GT.Application/Facturacion/VistaPreviaFactura.cs`: arma la entidad `FacturaCliente` **en memoria**, la pasa por `DatosDelDocumento` y el armador, y devuelve el PDF sin persistir nada (FR-033, research §2)
- [X] T056 [US2] Implementar `backend/src/GT.Application/Facturacion/EmitirFactura.cs`: validaciones de FR-006, FR-011, FR-011a, FR-013, FR-019a, FR-027 a FR-030; las **dos confirmaciones previas de FR-032** como `409` sin crear nada, reintentables con `confirmado: true`; congelamiento de los diez datos del emisor y los tres del cliente; cálculo de los importes **a partir de los viajes leídos de la base, nunca del cuerpo** (FR-024); armado y escritura del PDF **antes** de abrir la transacción y borrado del archivo si algo falla (research §6)
- [X] T057 [US2] Implementar la parte de emisión de `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturas.cs`: la transacción de data-model §Emitir, con el **`UPDATE` condicional** sobre `Viajes` cuyo número de filas afectadas se verifica —`Estado = rendido AND FacturaId IS NULL`— y la traducción de la violación de `IX_Facturas_Numero` a excepción de la capa de aplicación (research §4, convención [003])
- [X] T058 [US2] Implementar `backend/src/GT.Api/Facturacion/ArmadoEndpoints.cs` con `GET /api/facturas/facturables` y `POST /api/facturas/vista-previa` (devuelve `application/pdf`)
- [X] T059 [US2] Implementar `POST /api/facturas` en `backend/src/GT.Api/Facturacion/FacturasEndpoints.cs`, con el permiso `facturacion.gestionar` y los códigos de research §11

### Cambios al Módulo 5 que entrega esta historia (FR-055, FR-055a)

- [X] T060 [US2] Exigir el número de remito en `backend/src/GT.Application/Viajes/RendirViaje.cs`: la transición `en curso → rendido` se rechaza con `400 remito_requerido` si falta, marcando ese campo (FR-055a). Es el único cambio de comportamiento sobre una operación existente del Módulo 5
- [X] T061 [US2] Incorporar `factura: { numero, fecha } | null` a la fila del listado y `factura: { id, numero, fecha } | null` a la ficha en `backend/src/GT.Application/Viajes/Dtos.cs`, `ConsultarViajes.cs` y `ConsultarFichaViaje.cs`, resolviéndolo por la navegación `FacturaId` y no por columnas copiadas; el filtro de estado del listado acepta `facturado` (FR-055)
- [X] T062 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Viajes/ViajeFacturadoTests.cs`: rendir sin remito se rechaza y con remito procede; un viaje `facturado` rechaza los cinco caminos de escritura del Módulo 5 —editar, asignar, poner en curso, rendir, anular— para todos los roles (FR-052, SC-013)

### Frontend de User Story 2

- [X] T063 [P] [US2] Implementar `frontend/src/modules/facturacion/servicios/servicioFacturas.ts` con facturables, vista previa (respuesta `Blob`) y emisión
- [X] T064 [US2] Implementar el bloque 1 de `frontend/src/modules/facturacion/paginas/AltaFactura.tsx`: los trece campos de `contracts/README.md`, con clientes **sólo activos**, mes `01`–`12`, año `2025`/`2026`, fecha de facturación propuesta en hoy, vencimiento de pago propuesto en fecha + 30 días, punto de venta propuesto, y los mensajes de "sin clientes activos" y de empresa emisora incompleta
- [X] T065 [US2] Implementar `frontend/src/modules/facturacion/componentes/SelectorDeViajes.tsx`: número, fecha, remito, origen, destino e importe por viaje, con la casilla deshabilitada y la leyenda `Sin remito — no se puede facturar` para los que no lo tienen, y el mensaje explícito cuando no hay facturables (FR-019a, FR-021)
- [X] T066 [US2] Implementar `frontend/src/modules/facturacion/componentes/ResumenDeImportes.tsx`: cantidad seleccionada, neto, IVA con su alícuota y total, recalculados en cada cambio de selección y de tipo de comprobante, **de sólo lectura y sin ningún campo donde escribirlos**, formateados con `compartido/moneda` (FR-020, FR-024, FR-025)
- [X] T067 [US2] Implementar `frontend/src/modules/facturacion/componentes/VistaPreviaDocumento.tsx`: pide el PDF al servidor y lo muestra en un `<iframe>` sobre una URL de `Blob`, con el texto de advertencia de `contracts/README.md`. **No es una maqueta dibujada en React** (research §2)
- [X] T068 [US2] Implementar `frontend/src/modules/facturacion/componentes/ConfirmacionesDeEmision.tsx`: los dos diálogos de FR-032 disparados por el `409` del servidor, con los textos exactos y el reintento con `confirmado: true`
- [X] T069 [US2] Cerrar el alta en `AltaFactura.tsx`: al emitir se navega a la ficha de la factura recién creada y **el formulario no queda en pantalla**; la confirmación viaja con la navegación y se anuncia con `role="status"` (FR-014, convención [005])
- [X] T070 [P] [US2] Escribir `frontend/src/modules/facturacion/paginas/AltaFactura.test.tsx` y `frontend/src/modules/facturacion/componentes/SelectorDeViajes.test.tsx`: recálculo de importes al cambiar el tipo, viaje sin remito no seleccionable, confirmaciones previas, navegación posterior a la emisión
- [X] T071 [US2] Agregar el estado `Facturado` a la columna y al filtro de estado, y la leyenda `Facturado en {número}, del {fecha}`, en `frontend/src/modules/viajes/paginas/ListadoViajes.tsx`, `FichaViaje.tsx` y `frontend/src/modules/viajes/componentes/FiltrosViajes.tsx`; un viaje `facturado` **no ofrece ninguna acción de escritura** (FR-052, FR-055)
- [X] T072 [US2] Mostrar el mensaje `Cargá el número de remito antes de rendir el viaje: sale impreso en el detalle de la factura.` en `frontend/src/modules/viajes/componentes/ConfirmacionRendicion.tsx` y marcar el campo (FR-055a)

**Checkpoint**: se emite una factura de punta a punta, con su documento, y los viajes quedan facturados.

---

## Phase 5: User Story 3 - Consultar, buscar y filtrar facturas (Priority: P1)

**Goal**: encontrar cualquier factura por cliente, fechas, período, estado o tipo, y abrir su ficha
completa con el documento y el historial.

**Independent Test**: emitir facturas de dos clientes, dos períodos, dos tipos y distintos estados;
aplicar combinaciones de filtros y comprobar que el listado muestra exactamente lo esperado y que la
ficha lo detalla.

### Tests para User Story 3

- [X] T073 [P] [US3] Escribir `backend/tests/GT.UnitTests/Facturacion/DerivadorEstadoFacturaTests.cs`: la regla recibe la fecha por parámetro y no lee el reloj; `pagada` y `anulada` mandan sobre el vencimiento; el vencimiento del **CAE** no influye (FR-041, US5 esc. 10)
- [X] T074 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Facturacion/DerivacionVencidaTests.cs`: evalúa el predicado de la consulta y la regla del dominio **sobre el mismo conjunto** de facturas y compara los resultados (FR-058a, convención [003])
- [X] T075 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Facturacion/ConsultaFacturasTests.cs`: filtros combinados, exclusividad de `pendiente` y `vencida`, paginación de 20 con el total, filtros aplicados **antes** de paginar y orden `fecha DESC, número DESC`

### Implementación de User Story 3

- [X] T076 [US3] Implementar `backend/src/GT.Domain/Facturacion/DerivadorEstadoFactura.cs` como función pura que recibe `hoy` por parámetro (data-model §Reglas derivadas)
- [X] T077 [US3] Implementar `backend/src/GT.Application/Facturacion/ConsultarFacturas.cs`: los cinco filtros de FR-058, página de 20 y respuesta `{ items, total, pagina, tamanioPagina }`
- [X] T078 [US3] Implementar la consulta paginada en `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturas.cs`, con la derivación de `vencida` **escrita en el árbol de la consulta** —no extraída a un método propio, porque eso rompe la traducción de EF Core y la evalúa en memoria (research §15.4)— y el orden total de FR-059
- [X] T079 [US3] Implementar `backend/src/GT.Application/Facturacion/ConsultarFichaFactura.cs`: los datos congelados del emisor y del cliente, los viajes incluidos con su importe, el historial completo y **las dos direcciones** de la referencia de refacturación, la segunda resuelta por consulta sobre `FacturaReemplazadaId` y no por columna espejo (FR-060, FR-050)
- [X] T080 [US3] Implementar `backend/src/GT.Application/Facturacion/ServirDocumentoFactura.cs` con `ResultadoArchivo.EnLinea`, nombre que identifica la factura y el permiso `facturacion.consultar` (FR-031a, convención [003])
- [X] T081 [US3] Agregar `GET /api/facturas`, `GET /api/facturas/{id:int}` y `GET /api/facturas/{id:int}/documento` a `backend/src/GT.Api/Facturacion/FacturasEndpoints.cs`
- [X] T082 [US3] Implementar `frontend/src/modules/facturacion/paginas/ListadoFacturas.tsx`: las ocho columnas de FR-057, los cuatro estados **con la palabra y no sólo con color**, la fila anulada atenuada con su motivo, el cliente inactivo señalado, y los dos textos de estado vacío
- [X] T083 [US3] Implementar `frontend/src/modules/facturacion/componentes/FiltrosFacturas.tsx` y `Paginacion.tsx`: los cinco filtros combinables y el aviso permanente de qué estados está mostrando el listado (FR-064)
- [X] T084 [US3] Implementar `frontend/src/modules/facturacion/paginas/FichaFactura.tsx`: todos los datos de FR-060, el aviso de datos congelados, el botón *Ver el documento* con su nota de que no es el comprobante fiscal, y el historial con `Corrección de datos` para las entradas sin estado nuevo
- [X] T085 [P] [US3] Escribir `frontend/src/modules/facturacion/paginas/ListadoFacturas.test.tsx` y `FichaFactura.test.tsx`

**Checkpoint**: US1, US2 y US3 funcionan juntas — el MVP del módulo está completo.

---

## Phase 6: User Story 4 - Corregir los datos de una factura emitida (Priority: P2)

**Goal**: corregir el detalle, el CAE, su vencimiento y el vencimiento de pago, regenerando el
documento para que diga lo mismo que la ficha.

**Independent Test**: emitir una factura con el CAE mal cargado, corregirlo desde su ficha, abrir el
documento y comprobar que ya trae el CAE bueno, y que el cliente, los viajes y los importes no ofrecen
ninguna forma de editarse.

- [X] T086 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Facturacion/CorreccionFacturaTests.cs`: se corrigen los cuatro campos y ninguno más —intentar cambiar cliente, viajes o importes se rechaza aunque se invoque la acción directamente—; el documento se regenera y el anterior no queda; una entrada de corrección se agrega al historial con `EstadoNuevo = null`; corregir una factura `pagada` **no le toca el estado ni la fecha de cobro**; una `anulada` rechaza la corrección; si el documento no se puede regenerar, la corrección no queda guardada
- [X] T087 [US4] Implementar `backend/src/GT.Application/Facturacion/CorregirFactura.cs` con las mismas validaciones del alta, el rechazo de vaciar el CAE o su vencimiento, la regeneración del documento y la entrada de historial (FR-035 a FR-038, FR-031b)
- [X] T088 [US4] Implementar en `backend/src/GT.Infrastructure/Persistencia/RepositorioFacturas.cs` la transacción de corrección de data-model §Corregir: se escribe el PDF nuevo, se confirma, y **recién después** se borra el anterior; nunca se sobreescribe en el lugar (research §6)
- [X] T089 [US4] Agregar `PUT /api/facturas/{id:int}` a `backend/src/GT.Api/Facturacion/FacturasEndpoints.cs`, verificando que **no pueda tocar el estado ni la fecha de cobro** (FR-044, research §15.5)
- [X] T090 [US4] Implementar `frontend/src/modules/facturacion/paginas/CorreccionFactura.tsx`: sólo cuatro campos editables, el resto de sólo lectura con el aviso de `contracts/README.md`, y el mensaje de guardado que menciona la regeneración del documento
- [X] T091 [P] [US4] Escribir `frontend/src/modules/facturacion/paginas/CorreccionFactura.test.tsx`

**Checkpoint**: US1–US4 funcionan de forma independiente.

---

## Phase 7: User Story 5 - Registrar el cobro y seguir los vencimientos (Priority: P2)

**Goal**: marcar la factura como cobrada y ver en un panel qué está vencido y qué vence en los
próximos siete días.

**Independent Test**: emitir una factura con vencimiento pasado y otra con vencimiento próximo,
comprobar que la primera figura `vencida` sin que nadie haya tocado nada, registrar el cobro de la
segunda y comprobar que pasa a `pagada` y deja el panel.

- [X] T092 [P] [US5] Implementar `backend/src/GT.Domain/Facturacion/TransicionesDeFactura.cs`: sólo `pendiente | vencida → pagada` y `pendiente | vencida → anulada`; `pagada` y `anulada` son terminales (FR-043)
- [X] T093 [P] [US5] Escribir `backend/tests/GT.UnitTests/Facturacion/TransicionesDeFacturaTests.cs`, incluida la ausencia de todo camino de retroceso
- [X] T094 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Facturacion/CobroYVencimientosTests.cs`: el cobro deja la factura `pagada` con su fecha y su entrada de historial; la fecha anterior a la de facturación se rechaza; una factura `anulada` no admite cobro; el panel devuelve las `vencida` y las que vencen dentro de los 7 días corridos y **excluye** las `pagada` y `anulada`
- [X] T095 [US5] Implementar `backend/src/GT.Application/Facturacion/RegistrarCobro.cs` (FR-042)
- [X] T096 [US5] Implementar `backend/src/GT.Application/Facturacion/ConsultarVencimientos.cs` con la ventana de 7 días corridos y los días de atraso o de plazo calculados en la consulta (FR-063)
- [X] T097 [US5] Implementar `backend/src/GT.Api/Facturacion/CicloDeVidaFacturaEndpoints.cs` con `POST /api/facturas/{id:int}/cobro` — recurso propio, nunca un campo del `PUT` de edición (FR-044)
- [X] T098 [US5] Implementar `backend/src/GT.Api/Facturacion/ReportesFacturacionEndpoints.cs` con `GET /api/facturas/vencimientos`
- [X] T099 [US5] Implementar `frontend/src/modules/facturacion/componentes/RegistrarCobro.tsx`: formulario chico dentro de la ficha con la fecha propuesta en hoy y el texto que aclara que el paso no se revierte
- [X] T100 [US5] Implementar `frontend/src/modules/facturacion/paginas/PanelVencimientos.tsx`: las cinco columnas, la situación **en palabras** (`Vencida hace {n} días`, `Vence en {n} días`, `Vence hoy`) y el texto de panel vacío
- [X] T101 [P] [US5] Escribir `frontend/src/modules/facturacion/paginas/PanelVencimientos.test.tsx` y `frontend/src/modules/facturacion/componentes/RegistrarCobro.test.tsx`

**Checkpoint**: US1–US5 funcionan de forma independiente.

---

## Phase 8: User Story 6 - Anular una factura y refacturar (Priority: P2)

**Goal**: anular con motivo escrito devolviendo los viajes a `rendido`, y emitir una Refacturación que
referencie a la anulada, con las dos fichas mostrándose una a la otra.

**Independent Test**: anular una factura de tres viajes, comprobar que sin motivo escrito la
confirmación no se habilita, que al cancelar nada cambia, que al confirmar los tres viajes vuelven a
ofrecerse, y emitir después una Refacturación que la referencia.

- [X] T102 [P] [US6] Escribir `backend/tests/GT.IntegrationTests/Facturacion/AnulacionFacturaTests.cs`: la anulación deja la factura `anulada` con su motivo, escribe el historial, devuelve **todos** los viajes a `rendido` con `FacturaId` en nulo y una línea de `CambioDeEstadoViaje` por viaje; si el documento no se puede regenerar, **nada queda aplicado**; anular una `pagada` responde `409` informando desde qué fecha está cobrada; sin el permiso `facturacion.anular` responde `403`
- [X] T103 [P] [US6] Escribir `backend/tests/GT.IntegrationTests/Facturacion/RefacturacionTests.cs`: `Refacturación` sin factura reemplazada se rechaza; `Original` con referencia se rechaza; una anulada ya reemplazada responde `409` nombrando la Refacturación que la reemplaza, y el índice único lo sostiene ante dos operadores simultáneos (FR-049a)
- [X] T104 [US6] Implementar `backend/src/GT.Application/Facturacion/AnularFactura.cs` y su transacción en `RepositorioFacturas.cs` según data-model §Anular: estado, historial, viajes a `rendido`, **regeneración del documento dentro de la transacción** y borrado del anterior recién después de confirmar (FR-046 a FR-048, FR-031b)
- [X] T105 [US6] Imprimir la leyenda de anulada y el motivo en `backend/src/GT.Infrastructure/Documentos/ArmadorDocumentoFacturaQuestPdf.cs`, en el mismo armador y **no al servir el archivo** (FR-031d)
- [X] T106 [US6] Implementar `backend/src/GT.Application/Facturacion/ConsultarAnuladasSinReemplazo.cs`: sólo las anuladas de ese cliente que todavía nadie refacturó (FR-049)
- [X] T107 [US6] Agregar a `backend/src/GT.Application/Facturacion/EmitirFactura.cs` la validación de la referencia de refacturación —obligatoria con `Refacturación`, prohibida con `Original`, rechazo `409` si la anulada ya fue reemplazada— y traducir la violación de `IX_Facturas_FacturaReemplazada` en `RepositorioFacturas.cs`
- [X] T108 [US6] Agregar `POST /api/facturas/{id:int}/anulacion` a `backend/src/GT.Api/Facturacion/CicloDeVidaFacturaEndpoints.cs` y `GET /api/facturas/anuladas-sin-reemplazo` a `backend/src/GT.Api/Facturacion/ArmadoEndpoints.cs`
- [X] T109 [US6] Implementar `frontend/src/modules/facturacion/componentes/ConfirmacionAnulacion.tsx`: motivo obligatorio de hasta 500 caracteres, botón deshabilitado sin motivo, texto exacto del diálogo y cancelación que no modifica nada
- [X] T110 [US6] Agregar el desplegable *Factura que reemplaza* —visible sólo con `Refacturación`— a `frontend/src/modules/facturacion/paginas/AltaFactura.tsx`
- [X] T111 [US6] Mostrar en `frontend/src/modules/facturacion/paginas/FichaFactura.tsx` el motivo de anulación y las dos frases de referencia de refacturación, y ocultar las acciones según estado y permiso (FR-050, contracts/README §Acciones)
- [X] T112 [P] [US6] Escribir `frontend/src/modules/facturacion/componentes/ConfirmacionAnulacion.test.tsx`

**Checkpoint**: US1–US6 funcionan de forma independiente.

---

## Phase 9: User Story 7 - Ver lo facturado y lo cobrado por cliente en un período (Priority: P3)

**Goal**: ver entre dos fechas cuánto se le facturó a cada cliente, cuánto se cobró y cuánto queda
pendiente, sin que ninguna anulada sume.

**Independent Test**: emitir facturas de dos clientes dentro y fuera de un rango, cobrar algunas y
anular una, y comprobar que los totales cuentan sólo las del rango y ninguna anulada.

- [X] T113 [P] [US7] Escribir `backend/tests/GT.IntegrationTests/Facturacion/TotalesFacturacionTests.cs`: el rango es obligatorio; la fecha de corte es la de facturación; las anuladas **no suman en ninguna columna** y la exclusión está escrita como predicado de la consulta; la suma de los importes del listado filtrado coincide con la columna *facturado* (FR-061, FR-062, SC-011)
- [X] T114 [US7] Implementar `backend/src/GT.Application/Facturacion/ConsultarTotalesFacturacion.cs`: facturado, cobrado y pendiente por cliente, agregados **dentro de la consulta SQL**
- [X] T115 [US7] Agregar `GET /api/facturas/totales` a `backend/src/GT.Api/Facturacion/ReportesFacturacionEndpoints.cs`
- [X] T116 [US7] Implementar `frontend/src/modules/facturacion/paginas/TotalesFacturados.tsx`: sin rango elegido no calcula ni muestra nada y lo dice; las cinco columnas, la nota de que las anuladas no suman y el texto de sin resultados
- [X] T117 [P] [US7] Escribir `frontend/src/modules/facturacion/paginas/TotalesFacturados.test.tsx`
- [X] T118 [P] [US7] Escribir `backend/tests/GT.IntegrationTests/Facturacion/PermisosFacturacionTests.cs`: `facturacion.consultar` a secas recibe `403` en emitir, corregir, cobrar y anular; `facturacion.gestionar` sin `facturacion.anular` recibe `403` sólo en anular; el menú devuelve las entradas que corresponden a cada permiso (FR-066 a FR-068, SC-014)

**Checkpoint**: las siete historias funcionan de forma independiente.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [X] T119 [P] Revisar que todos los textos de pantalla coincidan **palabra por palabra** con `specs/006-gestion-facturacion/contracts/README.md`, en español rioplatense con voseo, recorriendo `frontend/src/modules/facturacion/`
- [X] T120 [P] Verificar en `frontend/src/modules/facturacion/` que todo resultado que aparece sin que la pantalla cambie —guardado, carga de archivo, cambio de página, cambio de estado— se anuncie con `role="status"`, que ningún estado se comunique sólo por color y que todo elemento atenuado lleve la palabra que lo explica (FR-065, convención [003])
- [X] T121 [P] Verificar que ningún importe use `toFixed(2)` y ninguna fecha use `new Date(iso).toLocaleDateString()` en `frontend/src/modules/facturacion/`: van con `compartido/moneda` y `compartido/fechas` (convenciones [003] y [005])
- [X] T122 Comprobar el rendimiento de los listados con volumen: filtro por estado derivado, exclusión de anuladas, panel de vencimientos y totales resueltos **dentro de la consulta**, con los cinco índices de data-model §Índices en uso
- [X] T123 Ejecutar `cd backend && dotnet test` y `cd frontend && npm test` y dejar la suite entera en verde
- [X] T124 Recorrer a mano los 46 pasos de `specs/006-gestion-facturacion/quickstart.md`, empezando por la verificación de humo del generador de PDF, y anotar cualquier discrepancia
- [X] T125 Actualizar `AGENTS.md` con las decisiones transversales de esta feature en la sección *Decisiones transversales ya tomadas*, una línea por decisión con la referencia `[006]`. Confirmar contra lo realmente implementado las seis candidatas que anota `plan.md` §Mantenimiento —el armador único sobre la misma entrada, el congelamiento con copia más referencia, la convención que nombra el objetivo y no el mecanismo, el estado derivado que además se filtra, la dependencia que sólo falla en tiempo de ejecución y la modificación acotada de un módulo anterior— y **descartar las que no resulten transversales**: no se agregan entradas por completar la lista

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Fase 1)**: sin dependencias, arranca de inmediato
- **Foundational (Fase 2)**: depende de la Fase 1 — **bloquea todas las historias**
- **US1 (Fase 3)**, **US2 (Fase 4)**, **US3 (Fase 5)**: dependen sólo de la Fase 2
- **US4 (Fase 6)**, **US5 (Fase 7)**, **US6 (Fase 8)**, **US7 (Fase 9)**: dependen de la Fase 2; para
  **validarlas** hace falta al menos una factura emitida, o sea US2 corriendo
- **Polish (Fase 10)**: depende de las historias que se quieran entregar

### User Story Dependencies

- **US1 (P1)**: independiente. Su verificación completa —"la vista previa muestra los datos sin
  haberlos escrito"— necesita la vista previa de US2; el resto se valida sola
- **US2 (P1)**: independiente en implementación; en operación **exige US1 configurada**, que es
  exactamente lo que FR-006 describe
- **US3 (P1)**: independiente en implementación; se valida con facturas emitidas por US2
- **US4, US5, US6 (P2)** y **US7 (P3)**: independientes entre sí; todas se validan sobre facturas
  emitidas por US2. US6 toca `EmitirFactura.cs` (T107) y `AltaFactura.tsx` (T110), que son de US2:
  hay que coordinar si las dos se hacen en paralelo

### Dentro de cada historia

- Los tests se escriben primero y tienen que fallar antes de implementar
- Reglas puras del dominio → entidades → repositorios → casos de uso → endpoints → frontend

### Parallel Opportunities

- **Fase 1**: T004 y T005 en paralelo
- **Fase 2**: T006–T011 (seis archivos de dominio nuevos), T016–T018 (tres configuraciones), y
  T024–T027 (cuatro archivos de aplicación) son tres tandas paralelas. T030, T033 y T034 también
- **Fase 3 (US1)**: T035 y T036 en paralelo; T042 y T045 en paralelo
- **Fase 4 (US2)**: T046–T048 en paralelo; T049–T053 en paralelo; T063 y T070 en paralelo
- **Fase 5 (US3)**: T073–T075 en paralelo
- **Entre historias**: con la Fase 2 terminada, un equipo puede tomar US1, otro US2 y otro US3

---

## Parallel Example: User Story 2

```bash
# Las tres reglas puras del dominio, juntas:
Task: "Implementar AlicuotasIva.cs en backend/src/GT.Domain/Facturacion/AlicuotasIva.cs"
Task: "Implementar CalculadorImportes.cs en backend/src/GT.Domain/Facturacion/CalculadorImportes.cs"
Task: "Implementar NumeroDeComprobante.cs en backend/src/GT.Domain/Facturacion/NumeroDeComprobante.cs"

# Los cinco tests de la historia, juntos:
Task: "CalculadorImportesTests en backend/tests/GT.UnitTests/Facturacion/CalculadorImportesTests.cs"
Task: "NumeroDeComprobanteTests en backend/tests/GT.UnitTests/Facturacion/NumeroDeComprobanteTests.cs"
Task: "EmisionFacturaTests en backend/tests/GT.IntegrationTests/Facturacion/EmisionFacturaTests.cs"
Task: "EmisionConcurrenteTests en backend/tests/GT.IntegrationTests/Facturacion/EmisionConcurrenteTests.cs"
Task: "VistaPreviaTests en backend/tests/GT.IntegrationTests/Facturacion/VistaPreviaTests.cs"
```

---

## Implementation Strategy

### MVP primero

El MVP del módulo son **tres historias P1, no una**: la configuración sin emisión no sirve, y la
emisión sin listado deja las facturas sin dónde encontrarse.

1. Fase 1: Setup — con el `Dockerfile` y el test de humo del PDF resueltos antes que nada
2. Fase 2: Foundational (CRÍTICA — bloquea todo)
3. Fase 3: US1 — configurar la empresa emisora
4. Fase 4: US2 — emitir
5. Fase 5: US3 — consultar y filtrar
6. **PARAR Y VALIDAR**: pasos 1 a 26 del `quickstart.md`

### Entrega incremental

1. Setup + Foundational → base lista
2. US1 + US2 + US3 → MVP: se emite, se guarda el documento y se encuentra la factura
3. US4 → corregir un CAE mal tipeado sin anular
4. US5 → cobranzas: cobro y panel de vencimientos
5. US6 → anulación y refacturación
6. US7 → totales por cliente

### Estrategia con varios desarrolladores

Con la Fase 2 terminada: A toma US1, B toma US2 y C toma US3. Después, A toma US4, B toma US6 —
coordinando con quien hizo US2 por T107 y T110 — y C toma US5 y US7.

---

## Notes

- `[P]` = archivo distinto, sin dependencias pendientes
- Las cinco trampas de research §15 están repartidas en T032 (`{id:int}`), T006 (`EstadoFactura` y sus
  índices), T003 (`libfontconfig1`), T078 (la expresión en el árbol de la consulta) y T089 (el `PUT`
  que no puede tocar el estado)
- Los seis cambios al Módulo 5 son T012, T013, T014, T019, T060 y T061 — **seis y ninguno más**
  (FR-056). La única línea que se agrega sin estar en la lista es la entrada de `CambioDeEstadoViaje`
  al facturar y al desfacturar (T057, T104), y se justifica contra FR-035 del Módulo 5, ya vigente
- Confirmar después de cada tarea o de cada grupo lógico
- Se puede parar en cualquier checkpoint para validar la historia por separado
