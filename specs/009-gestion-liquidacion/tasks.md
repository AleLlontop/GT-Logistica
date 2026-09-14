---

description: "Lista de tareas de implementación del Módulo 9"
---

# Tasks: Gestión de liquidación a transportistas (Módulo 9)

**Input**: Documentos de diseño de `/specs/009-gestion-liquidacion/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/README.md`,
`contracts/liquidaciones-api.yaml`, `quickstart.md`

**Tests**: se incluyen. El plan los pide explícitamente (`Technical Context` → *Testing*) y
`quickstart.md` nombra las clases de test que cubren lo que una persona no puede verificar a mano:
`GeneracionConcurrenteTests`, `PagoConcurrenteTests`, `EdicionConcurrenteTests`, `AnulacionConcurrenteTests`,
`CoherenciaDeLiquidacionTests`, `RestriccionesDeLiquidacionTests`, `AnulacionTests`, `OrdenDePagoTests`
y `RutasLiquidacionesTests`.

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
- Frontend: `frontend/src/modules/liquidaciones/` y `frontend/src/compartido/`

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar las carpetas del módulo. **No hay dependencias, variables de entorno ni cambios de
`Dockerfile`** (research §11).

- [ ] T001 [P] Crear las carpetas del backend del módulo: `backend/src/GT.Domain/Liquidaciones/`, `backend/src/GT.Application/Liquidaciones/`, `backend/src/GT.Api/Liquidaciones/`, `backend/tests/GT.UnitTests/Liquidaciones/` y `backend/tests/GT.IntegrationTests/Liquidaciones/`
- [ ] T002 [P] Crear el esqueleto del módulo de frontend `frontend/src/modules/liquidaciones/` con las subcarpetas `paginas/`, `componentes/` y `servicios/`

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: esquema, entidades, reglas puras, permisos, menú, la relectura del detalle —que usan las
cuatro escrituras— y las piezas compartidas del frontend. Nada de esto pertenece a una historia sola.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa.

### Dominio

- [ ] T003 [P] Crear `EstadoLiquidacion : byte` (`Pendiente = 0`, `Pagada = 1`, `Anulada = 2`) en `backend/src/GT.Domain/Liquidaciones/EstadoLiquidacion.cs`, con el comentario ⚠ de que `CK_Liquidaciones_Estado` lleva `0`, `1` y `2` escritos a mano y reordenar el enum no falla al compilar (data-model §Enumeraciones, research §12.1)
- [ ] T004 [P] Crear `OperacionDeLiquidacion : byte` (`Generacion = 0`, `Edicion = 1`, `Anulacion = 2`, `Pagada = 3`) en `backend/src/GT.Domain/Liquidaciones/OperacionDeLiquidacion.cs`, con el comentario ⚠ de que `IX_CambiosDeLiquidacion_Unica` lleva el `1` escrito a mano
- [ ] T005 [P] Crear la entidad `Liquidacion` en `backend/src/GT.Domain/Liquidaciones/Liquidacion.cs`: `Id`, **`Numero` sin `required` y con `private set`** (lo pone la secuencia, research §12.2), `TransportistaId` + navegación, `PeriodoMes` (`byte`), `PeriodoAnio` (`short`), `ImporteTotal` y `ImportePagado` (`decimal`), `Estado` con `Pendiente` por defecto, `MotivoAnulacion`, `Version` (`int`, sube sólo con cada edición guardada) y las colecciones de vínculos, órdenes de pago y cambios (data-model §Tabla `Liquidaciones`)
- [ ] T006 [P] Crear `LiquidacionViaje` (`LiquidacionId`, `ViajeId` + navegación a `Viaje`, `Vigente` con `true` por defecto) en `backend/src/GT.Domain/Liquidaciones/LiquidacionViaje.cs`, documentando que `Vigente` vale `false` exactamente cuando la liquidación está anulada (research §1)
- [ ] T007 [P] Crear `OrdenDePago` (`Id`, `Numero` con `private set`, `LiquidacionId`, `FechaPago` `DateOnly`, `Importe` `decimal`, `UsuarioId` + navegación, `RegistradaEn` `DateTime`) en `backend/src/GT.Domain/Liquidaciones/OrdenDePago.cs`
- [ ] T008 [P] Crear `CambioDeLiquidacion` (`Id`, `LiquidacionId`, `Operacion`, `UsuarioId` + navegación, `OcurridoEn`, colección de viajes cambiados) en `backend/src/GT.Domain/Liquidaciones/CambioDeLiquidacion.cs`; **sin columna de motivo**: el de la anulación se lee de la liquidación (data-model §Tabla `CambiosDeLiquidacion`)
- [ ] T009 [P] Crear `CambioDeLiquidacionViaje` (`CambioDeLiquidacionId`, `ViajeId` + navegación, `Agregado` `bool`) en `backend/src/GT.Domain/Liquidaciones/CambioDeLiquidacionViaje.cs` (research §3b)
- [ ] T010 [P] Crear `ReglasDeLiquidacion` en `backend/src/GT.Domain/Liquidaciones/ReglasDeLiquidacion.cs` con las seis funciones puras de data-model §Reglas de dominio: `PeriodoValido(mes, anio)` (1–12 y 2025/2026), `FechaDePagoValida(fecha, fechaGeneracion, hoy)` con los dos límites incluidos, `RestaPagar(total, pagado)`, `EstadoTrasPago(total, pagado, importe)`, `MotivoNoEditable(estado, pagado, transportistaLiquidable)` —con el motivo `transportistaNoLiquidable` de FR-045— y `MotivoNoAnulable(estado, pagado)`. **Ninguna lee el reloj** (convención [005])
- [ ] T011 [P] Crear `NumerosVisibles` con `Liquidacion(int) → "LQ-{n}"` y `OrdenDePago(int) → "OP-{n}"` en `backend/src/GT.Domain/Liquidaciones/NumerosVisibles.cs`: el único lugar donde se arma el formato (research §4)
- [ ] T012 [P] Agregar `LiquidacionesGestionar = "liquidaciones.gestionar"` y `LiquidacionesConsultar = "liquidaciones.consultar"` con su comentario de reparto (research §9) en `CodigosPermiso` de `backend/src/GT.Domain/Usuarios/Rol.cs`

### Tests de reglas puras

- [ ] T013 [P] Escribir `backend/tests/GT.UnitTests/Liquidaciones/ReglasDeLiquidacionTests.cs`: período en los bordes (mes 0, 1, 12, 13; año 2024, 2025, 2026, 2027); fecha de pago igual a la generación, igual a hoy, un día antes y un día después; `EstadoTrasPago` con pago parcial, pago exacto y centavos (`355000,00 − 354999,99`); los cuatro motivos de no edición —incluido el transportista que ya no se puede liquidar— y los tres de no anulación
- [ ] T014 [P] Escribir `backend/tests/GT.UnitTests/Liquidaciones/NumerosVisiblesTests.cs`: `LQ-1`, `LQ-12`, `OP-3`, sin ceros a la izquierda

### Persistencia

- [ ] T015 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/LiquidacionConfiguracion.cs`: `Numero` con `HasDefaultValueSql("NEXT VALUE FOR dbo.NumeroDeLiquidacion")` y `ValueGeneratedOnAdd`; `decimal(18,2)` para los dos importes; `Version` con default `0`; los `CHECK` de data-model —`PeriodoMes` 1–12, `ImporteTotal > 0`, `ImportePagado` entre 0 y `ImporteTotal`, y `CK_Liquidaciones_Estado` con sus tres ramas—; FK a `Transportistas` en `Restrict` **sin navegación inversa**; los cuatro índices, con `IX_Liquidaciones_Numero` único y descendente. Exponer como constantes el nombre de la secuencia y del índice único
- [ ] T016 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/LiquidacionViajeConfiguracion.cs`: PK compuesta, `Vigente` con default `true`, FK a `Viajes` en `Restrict` con `WithMany()` —**`Viaje` no gana colección**—, y `IX_LiquidacionViajes_ViajeVigente` único con filtro `[Vigente] = 1`, con su nombre como constante pública (research §1, §12.7)
- [ ] T017 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/OrdenDePagoConfiguracion.cs`: `Numero` desde `dbo.NumeroDeOrdenDePago`, `CHECK ([Importe] > 0)`, índice único por número e índice por liquidación, FK a `Usuarios` en `Restrict`
- [ ] T018 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CambioDeLiquidacionConfiguracion.cs` con `IX_CambiosDeLiquidacion_Unica` sobre `(LiquidacionId, Operacion)` filtrado `[Operacion] <> 1`
- [ ] T019 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CambioDeLiquidacionViajeConfiguracion.cs`: PK compuesta, FK a `CambiosDeLiquidacion` y a `Viajes` en `Restrict`, sin navegación inversa desde `Viaje`
- [ ] T020 Agregar los cinco `DbSet` —`Liquidaciones`, `LiquidacionViajes`, `OrdenesDePago`, `CambiosDeLiquidacion`, `CambiosDeLiquidacionViajes`— y las dos secuencias con `HasSequence<int>(…).StartsAt(1).IncrementsBy(1)`, junto a la de `NumeroDeViaje`, en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs`
- [ ] T021 Generar y revisar la migración `Modulo9Liquidaciones` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, verificando que **no contenga ningún `ALTER` sobre tablas existentes** —ni `Viajes`, ni `Transportistas`, ni `EmpresaEmisora`— y que los `CHECK` y filtros lleven los literales de data-model
- [ ] T022 Sembrar los dos permisos (módulo `Liquidaciones`) y su reparto —gestionar a *Administración de la empresa* y *Administrador del sistema*; consultar a esos dos más *Gerencia*— en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`, de forma idempotente y con el comentario del reparto
- [ ] T023 Agregar las dos entradas —`consultar-liquidacion` / `Consultar liquidación` / `/liquidaciones` por `liquidaciones.consultar`, y `generar-liquidacion` / `Generar liquidación` / `/liquidaciones/nueva` por `liquidaciones.gestionar`— en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs`, con el comentario de por qué los códigos no son `liquidaciones` (research §9)
- [ ] T024 [P] Crear `backend/tests/GT.IntegrationTests/Liquidaciones/DatosDePruebaLiquidaciones.cs` con los ayudantes de escenario: empresa emisora con un CUIT, transportista propio con ese mismo CUIT y dos externos, y viajes de un transportista en un período dado en estado `rendido`, `facturado`, `en curso`, `pendiente` y `anulado`, reutilizando los ayudantes de `backend/tests/GT.IntegrationTests/Viajes/DatosDePruebaViajes.cs`
- [ ] T025 Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/RestriccionesDeLiquidacionTests.cs`: por cada rama de `CK_Liquidaciones_Estado`, una fila válida se acepta y una inválida se rechaza (pagada con saldo, pendiente sin saldo, anulada con pagos, anulada sin motivo, pendiente con motivo); `ImportePagado > ImporteTotal` rechazado; `IX_LiquidacionViajes_ViajeVigente` rechaza dos vínculos vigentes del mismo viaje y acepta uno vigente más varios no vigentes; la secuencia asigna números distintos sin que la entidad los ponga

### Capa de aplicación: piezas compartidas

- [ ] T026 [P] Crear `backend/src/GT.Application/Liquidaciones/NombresDeEstadoLiquidacion.cs`: camelCase en el JSON para `EstadoLiquidacion`, `OperacionDeLiquidacion` y `EstadoViaje` (`rendido`, `facturado`), lectura tolerante del filtro de estado —un valor desconocido devuelve `null`— y la traducción al español en plural para las oraciones del listado (convención [003])
- [ ] T027 [P] Crear `backend/src/GT.Application/Liquidaciones/Mensajes.cs` con los códigos de error de research §8 —`datos_invalidos`, `periodo_invalido`, `empresa_emisora_no_configurada`, `transportista_no_liquidable`, `sin_viajes`, `total_en_cero`, `viaje_no_liquidable`, `fecha_de_pago_fuera_de_rango`, `importe_supera_saldo`, `motivo_requerido`, `viaje_ya_liquidado`, `liquidacion_no_editable`, `liquidacion_modificada`, `liquidacion_no_anulable`, `liquidacion_no_pagable`, `pago_requiere_confirmacion`, `anulacion_requiere_confirmacion`, `liquidacion_no_encontrada`— y **los textos exactos** de `contracts/README.md`, con los importes formateados en pesos y las fechas `dd/MM/yyyy`
- [ ] T028 [P] Crear `backend/src/GT.Application/Liquidaciones/Dtos.cs` con los esquemas de `liquidaciones-api.yaml`: `GeneracionRequest` y `EdicionRequest` **sin importes**, `EdicionRequest` con `Version`, `AnulacionRequest` y `OrdenDePagoRequest` con `Confirmado` opcional, `LiquidacionListado` y `LiquidacionDetalle` con `numero` como texto `LQ-…`, `restaPagar` anulable, `version` y las banderas `puedeEditarse`, `puedeAnularse`, `puedeRegistrarPago`; `CambioDeLiquidacion` con `viajesQuitados` y `viajesAgregados`
- [ ] T029 [P] Crear `backend/src/GT.Application/Liquidaciones/ResultadoLiquidacion.cs`: el enum `ErrorLiquidacion` con un valor por código de T027 y el récord de resultado con el detalle releído, el campo marcado, los viajes en conflicto (con motivo y liquidación que lo tiene), el motivo de estado, cantidad y suma de órdenes, `restaPagar`, `desde`/`hasta` y los cuatro importes de la confirmación de pago
- [ ] T030 Crear `backend/src/GT.Application/Liquidaciones/IRepositorioLiquidaciones.cs` con las firmas de todas las consultas y las cuatro transacciones de data-model §Transacciones; los instantes y el `hoy` llegan por parámetro
- [ ] T031 Crear `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs` con `ObtenerDetalleAsync` —la liquidación con transportista, **todos** sus vínculos vigentes o no con sus viajes, órdenes de pago con usuario ordenadas por fecha de pago y número, e historial con usuario y viajes cambiados en orden cronológico— y el método privado que traduce las violaciones de `IX_LiquidacionViajes_ViajeVigente` a una excepción de la capa de aplicación **distinguiendo el índice por su nombre** (patrón de `RepositorioFacturas`, research §12.7)
- [ ] T032 Implementar `backend/src/GT.Application/Liquidaciones/ConsultarDetalleLiquidacion.cs`: arma `LiquidacionDetalle` con `numero` desde `NumerosVisibles`, **`fechaGeneracion` como `FechaHoyArgentina.Desde(instante de la entrada generacion)`** (research §12.8), `restaPagar` nulo si está anulada, las tres banderas desde `ReglasDeLiquidacion` —`puedeEditarse` mira además que el transportista siga activo y con CUIT distinto del de la empresa emisora (FR-045)— y los números de viaje de cada edición. Es también la relectura de las cuatro escrituras (convención [006])
- [ ] T033 Registrar `ConsultarDetalleLiquidacion`, `IRepositorioLiquidaciones` → `RepositorioLiquidaciones` y las políticas de los dos permisos en `AgregarPoliticasDePermisos` de `backend/src/GT.Api/Program.cs`
- [ ] T034 Crear `backend/src/GT.Api/Liquidaciones/RespuestasDeLiquidacion.cs`: la traducción `ResultadoLiquidacion` → HTTP en un solo lugar, siguiendo research §8 —`400` para lo tipeado o elegido, `409` para el estado de algo compartido y para las dos confirmaciones pendientes—, con los cuerpos `ErrorConViajes`, `ErrorDeEstado`, `ErrorDePago` y `ConfirmacionDePago` del contrato. Un error sin código propio cae en `datos_invalidos`, nunca en `500`

### Frontend compartido (spec §Assumptions, cambios 1 y 2)

- [ ] T035 [P] Crear `frontend/src/compartido/cuit.ts` con `formatearCuit(cuit: string): string` —once caracteres salen `XX-XXXXXXXX-X`, **cualquier otro largo sale sin tocar**— y `frontend/src/compartido/cuit.test.ts` con los dos casos (research §11b)
- [ ] T036 **Cambio 1 al Módulo 3**: borrar la función local `formatearCuit` de `frontend/src/modules/choferes/transportistas/ListadoTransportistas.tsx` e importar la de `compartido/cuit`. **No modificar** `ListadoTransportistas.test.tsx` si existe: seguir en verde sin tocarlo es la prueba de que no cambió nada (convención [007])
- [ ] T037 **Cambio 2 al Módulo 5**, en dos pasos y en este orden: (1) agregar a `frontend/src/modules/viajes/clientes/ListadoClientes.test.tsx` un caso **nuevo** que busque el CUIT de un cliente con guiones —hoy ningún caso lo mira— y correrlo en verde con la función local; (2) borrar la función local `formatearCuit` de `frontend/src/modules/viajes/clientes/ListadoClientes.tsx`, importar la de `compartido/cuit`, y volver a correr la suite **sin modificar ningún caso existente**
- [ ] T038 [P] Agregar `liquidacionesGestionar: 'liquidaciones.gestionar'` y `liquidacionesConsultar: 'liquidaciones.consultar'` a `Permisos` en `frontend/src/modules/autenticacion/servicios/sesion.ts`
- [ ] T039 [P] Agregar `'consultar-liquidacion': 'Operación'` y `'generar-liquidacion': 'Operación'` a `SECCION_POR_CODIGO` en `frontend/src/compartido/seccionesDeMenu.ts`, **sin tocar** `seccionesDeMenu.test.ts`, que usa `liquidaciones` como ejemplo de código desconocido (research §9)
- [ ] T040 Crear `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts` con los tipos del contrato (`EstadoLiquidacion`, `TransportistaResumen`, `ViajeDisponible`, `LiquidacionListado`, `LiquidacionDetalle`, `CambioDeLiquidacion`, los cuerpos de error y de confirmación) y el objeto `CodigosErrorLiquidaciones`, apoyado en `compartido/clienteHttp`. **Las rutas no llevan `/api`**: se lo antepone `peticion` (precedente del Módulo 3)

**Checkpoint**: la base migra, arranca, siembra los permisos, muestra las dos entradas en *Operación* según el rol, y las pantallas de *Transportistas* y *Clientes* siguen iguales. Las historias pueden empezar.

---

## Phase 3: User Story 1 - Generar la liquidación de un fletero para un período (Priority: P1) 🎯 MVP

**Goal**: elegir un transportista externo y un período, ver sus viajes rendidos o facturados sin
liquidar con el total, y guardar la liquidación en `pendiente`.

**Independent Test**: cargar un fletero con cinco viajes en 07/2026 —tres rendidos, uno en curso y uno
pendiente—, generar su liquidación y comprobar que agrupa los tres con total igual a la suma, que queda
`pendiente` y que al volver a pedir los viajes del mismo fletero y período no aparece ninguno.

### Tests para User Story 1

- [ ] T041 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/TransportistasLiquidablesTests.cs`: excluye al transportista con el CUIT de la empresa emisora; sin `incluirInactivos` sólo activos y con él también los dados de baja; sin empresa emisora responde `empresaEmisoraConfigurada: false` y lista vacía; orden por razón social
- [ ] T042 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/DisponiblesTests.cs`: trae `rendido` y `facturado` y no `pendiente`, `en curso` ni `anulado`; filtra por mes y año de la **fecha del viaje**; usa el transportista **registrado en el viaje** aunque el chofer haya cambiado de transportista (FR-005); excluye viajes con vínculo vigente e **incluye** los que sólo tienen vínculos no vigentes; ordena por fecha y número; rechaza período fuera de las opciones, transportista propio o inactivo y empresa emisora sin configurar
- [ ] T043 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/GeneracionTests.cs`: `201` con total `355000,00` para `120000 + 95000 + 140000`, estado `pendiente`, `ImportePagado` en cero, número `LQ-…`, una entrada `generacion` en el historial y `Location`; **una lista parcial de disponibles se acepta** y los que quedan afuera siguen disponibles (FR-008); rechazos `sin_viajes`, `total_en_cero` (todos los viajes en cero), `viaje_no_liquidable` con cada motivo, `transportista_no_liquidable`, `empresa_emisora_no_configurada`, `periodo_invalido`, y `409 viaje_ya_liquidado` nombrando la liquidación que lo tiene. Ningún rechazo crea filas; y después de generar, el viaje `facturado` sigue `facturado` con su factura y el `rendido` sigue `rendido`, con todos sus datos intactos (FR-020)
- [ ] T044 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/GeneracionConcurrenteTests.cs`: dos generaciones simultáneas con un viaje en común —con dos alcances de servicio distintos y `Task.WhenAll`— terminan con **exactamente** una liquidación creada, la otra rechazada con `viaje_ya_liquidado`, y ningún vínculo ni entrada de historial huérfano (SC-002)
- [ ] T045 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/RutasLiquidacionesTests.cs`: `GET /api/liquidaciones/transportistas` y `GET /api/liquidaciones/disponibles` responden sus propios cuerpos y no `404` ni un error de conversión, lo que prueba que `{id:int}` no las captura (convención [005], research §12.3)

### Implementación de User Story 1

- [ ] T046 [US1] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs`: `ObtenerCuitEmpresaEmisoraAsync`, `ConsultarTransportistasLiquidablesAsync` (research §5), `ConsultarDisponiblesAsync` con el predicado **escrito en el árbol** de research §6, `ObtenerViajesAsync` sin rastrear, y `GenerarAsync` con la transacción de data-model §Generar: inserta liquidación, vínculos y entrada de historial; ante la violación del índice hace `rollback` **y** `ChangeTracker.Clear()` y averigua qué viaje y en qué liquidación quedó (research §12.5)
- [ ] T047 [P] [US1] Implementar `backend/src/GT.Application/Liquidaciones/ConsultarTransportistasLiquidables.cs`
- [ ] T048 [P] [US1] Implementar `backend/src/GT.Application/Liquidaciones/ConsultarViajesDisponibles.cs`: valida período, empresa emisora configurada y transportista externo y activo antes de consultar
- [ ] T049 [US1] Implementar `backend/src/GT.Application/Liquidaciones/ValidadorDeViajes.cs`, compartido por generar y editar: para cada viaje pedido verifica que exista, sea del transportista, del período, esté `rendido` o `facturado` y no tenga vínculo vigente con **otra** liquidación; devuelve los viajes en conflicto con su motivo y calcula el total. **No exige que estén todos los disponibles** (FR-008)
- [ ] T050 [US1] Implementar `backend/src/GT.Application/Liquidaciones/GenerarLiquidacion.cs`: valida en el orden de data-model §Generar, rechaza total en cero, llama a `GenerarAsync` con el instante de `TimeProvider`, traduce la carrera a `viaje_ya_liquidado` y **relee el detalle** para responder
- [ ] T051 [US1] Implementar `backend/src/GT.Api/Liquidaciones/ArmadoLiquidacionEndpoints.cs` con `GET /api/liquidaciones/transportistas` (`incluirInactivos` como `bool?` con `?? false`, convención [003]) por `liquidaciones.consultar` y `GET /api/liquidaciones/disponibles` por `liquidaciones.gestionar`
- [ ] T052 [US1] Implementar `backend/src/GT.Api/Liquidaciones/LiquidacionesEndpoints.cs` con `POST /api/liquidaciones` (gestionar, `201` con `Location`) y `GET /api/liquidaciones/{id:int}` (consultar), leyendo el usuario de la sesión y pasándolo por parámetro; registrar en `backend/src/GT.Api/Program.cs` los casos de uso de esta historia y los dos grupos, **mapeando el de armado antes** que el de `{id:int}`
- [ ] T053 [US1] Agregar a `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts`: `listarTransportistas(incluirInactivos)`, `listarDisponibles(transportistaId, mes, anio)`, `generarLiquidacion(peticion)` y `obtenerLiquidacion(id)`, más `formatearPeriodo(mes, anio) → "07/2026"`
- [ ] T054 [P] [US1] Implementar `frontend/src/modules/liquidaciones/componentes/TablaDeViajes.tsx`: columnas *Viaje* (`TokenDeIdentificador` `#13`), *Fecha* (`formatearFecha`), *Ruta* (`Origen → Destino`), *Estado* opcional (`Estado` en pastilla) e *Importe* a la derecha en negrita con `formatearPesos`; pie con `{n} viajes` e `Importe total`; y una propiedad opcional de acción por fila con nombre accesible que nombra el viaje (la usa US5)
- [ ] T055 [US1] Implementar `frontend/src/modules/liquidaciones/paginas/GenerarLiquidacion.tsx` según `contracts/README.md` §Generar liquidación y plan §UI Design Check: callout sin empresa emisora con enlace a `/facturacion/empresa`; estado vacío sin transportistas; sección 1 con los tres desplegables (`formatearCuit` en las opciones) y *Buscar viajes* secundario que marca los vacíos y no busca; limpiar la lista y anunciar al cambiar la selección (FR-010); sección 2 con los tres estados de la lista, `TablaDeViajes`, anuncio `Se encontraron…` en `role="status"` y callout de total en cero; barra con *Cancelar* y *Guardar liquidación* habilitado sólo con viajes y total > 0; al guardar navega al detalle con el mensaje de confirmación; rechazos en `role="alert"` con los textos del contrato
- [ ] T056 [US1] Registrar en `frontend/src/App.tsx` la ruta `/liquidaciones/nueva` exigiendo `Permisos.liquidacionesGestionar`, siguiendo el patrón de protección de las rutas del Módulo 6
- [ ] T057 [P] [US1] Escribir `frontend/src/modules/liquidaciones/paginas/GenerarLiquidacion.test.tsx` con los servicios mockeados: callout sin empresa emisora; *Buscar viajes* con campos vacíos marca los tres y no llama al servicio; lista con total `$ 355.000,00`; cambiar el mes vacía la lista y deshabilita *Guardar liquidación*; mensaje sin viajes; total en cero deshabilita; rechazo `viaje_ya_liquidado` con su texto

**Checkpoint**: se genera una liquidación de punta a punta. La navegación final lleva al detalle, que se completa en US3.

---

## Phase 4: User Story 7 - Acceso restringido (Priority: P1)

**Goal**: que sólo quien corresponde opere o consulte, con la restricción en el servidor.

**Independent Test**: intentar cada ruta y cada endpoint sin sesión, con *Tráfico*, con *Gerencia* y con
*Administración de la empresa*.

### Tests para User Story 7

- [ ] T058 [P] [US7] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/PermisosLiquidacionesTests.cs`: sin sesión `401` en los ocho endpoints; *Tráfico* `403` en los ocho; *Gerencia* `200` en `GET /`, `GET /{id}` y `GET /transportistas`, y `403` en `GET /disponibles`, `POST /`, `PUT /{id}`, anulación y órdenes de pago; *Administración de la empresa* y *Administrador del sistema* acceden a todo; el menú de la sesión trae las dos entradas, sólo *Consultar liquidación* o ninguna según el rol (FR-063 a FR-066, SC-012). Los endpoints que todavía no existan en esta fase se agregan a este test en la historia que los implementa

### Implementación de User Story 7

- [ ] T060 [US7] Correr `frontend/src/compartido/seccionesDeMenu.test.ts` sin modificarlo y confirmar que sigue en verde con los dos códigos nuevos mapeados a *Operación*

**Checkpoint**: la generación está protegida por permiso y sólo quien corresponde la ve en el menú.

---

## Phase 5: User Story 3 - Ver el detalle de una liquidación (Priority: P2)

**Goal**: ver qué viajes componen la liquidación, qué órdenes de pago tiene, cuánto resta y su historial.
Va antes que el listado porque la generación termina navegando acá.

**Independent Test**: abrir una liquidación recién generada de tres viajes y comprobar los tres viajes
con sus importes, el total igual a la suma, órdenes de pago vacías y resto igual al total.

### Tests para User Story 3

- [ ] T061 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/DetalleLiquidacionTests.cs`: una anulada **sigue listando sus viajes** (vínculos no vigentes, FR-028) y `restaPagar` es nulo; historial en orden cronológico con los viajes de cada edición insertados a mano en el escenario; `fechaGeneracion` es la fecha de Argentina —una entrada a las `02:30Z` del 01/08 es del 31/07—; datos del transportista leídos del padrón después de corregirle la razón social; las tres banderas en cada combinación de estado y pagos; `404 liquidacion_no_encontrada`

### Implementación de User Story 3

- [ ] T062 [US3] Implementar `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx` según `contracts/README.md` §Detalle: encabezado con token, pastilla y contexto `Transportista · MM/AAAA · Generada el …`; terciario *Volver a liquidaciones*; callouts de pendiente con pagos, pendiente con transportista que ya no se puede liquidar, pagada y anulada —sin la instrucción para quien sólo consulta—; tarjeta de viajes con `TablaDeViajes` y título según estado; tarjeta de órdenes de pago con su estado vacío; historial con `LineaDeTiempo`, motivo en la anulación, `Quitó … · Agregó …` en las ediciones y `Pagada por …` en el paso a pagada; aside con la cifra destacada según estado, pagado, total y transportista con `formatearCuit` e `Inactivo`; el mensaje que llega por navegación se anuncia con `role="status"` en el `<p>` que lo contiene (convención [008]). Las acciones se dibujan desde las banderas **y** el permiso, pero sus diálogos se conectan en US4 a US6
- [ ] T063 [US3] Registrar en `frontend/src/App.tsx` la ruta `/liquidaciones/:id` exigiendo `Permisos.liquidacionesConsultar`, **después** de `/liquidaciones/nueva`, y pasándole a `DetalleLiquidacion` la prop `puedeGestionar` calculada con `tienePermiso(sesion, Permisos.liquidacionesGestionar)`, igual que `FichaFactura`
- [ ] T064 [P] [US3] Escribir `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.test.tsx`: detalle pendiente con órdenes vacías y resta igual al total; anulada con *Viajes que agrupaba*, motivo y sin acciones; pagada con su callout; historial con una edición que muestra `Quitó #13 · Agregó #21`; quien sólo consulta no ve acciones; la tarjeta de órdenes de pago no ofrece ninguna acción por fila, ni siquiera con `puedeGestionar` (FR-044); anuncio del mensaje recibido por navegación

**Checkpoint**: US1 queda completa de punta a punta: generar lleva a un detalle correcto.

---

## Phase 6: User Story 2 - Consultar y filtrar las liquidaciones (Priority: P2)

**Goal**: encontrar rápido las pendientes y saber cuánto se le debe a cada fletero.

**Independent Test**: generar liquidaciones de dos transportistas en dos períodos, pagar una y anular
otra, y aplicar cada filtro por separado y los tres combinados.

### Tests para User Story 2

- [ ] T065 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/ConsultaLiquidacionesTests.cs`: sin filtro trae **todas, incluidas las anuladas**; cada estado devuelve sólo las suyas y ninguna sale bajo dos; transportista, mes y año filtran por separado y combinados; orden por número descendente; página de 20 con `total` de coincidencias y sin repetidos entre páginas; `restaPagar` nulo en las anuladas y `0` en las pagadas; transportista dado de baja con `activo: false`; un estado desconocido se ignora

### Implementación de User Story 2

- [ ] T066 [US2] Agregar `ConsultarAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs`: los cuatro filtros **antes** de paginar, orden `Numero DESC`, proyección con `ImporteTotal − ImportePagado` calculado en la consulta y el `Activo` del padrón; el formato `LQ-…` se aplica sobre las 20 filas ya traídas
- [ ] T067 [P] [US2] Implementar `backend/src/GT.Application/Liquidaciones/ConsultarLiquidaciones.cs`
- [ ] T068 [US2] Agregar `GET /api/liquidaciones` a `backend/src/GT.Api/Liquidaciones/LiquidacionesEndpoints.cs` con `transportistaId`, `mes`, `anio` y `pagina` anulables y `estado` como texto leído con `NombresDeEstadoLiquidacion`; registrar el caso de uso en `backend/src/GT.Api/Program.cs`, y sumar el endpoint a `PermisosLiquidacionesTests`
- [ ] T069 [US2] Agregar `listarLiquidaciones(filtros, pagina)` y `FILTROS_LIQUIDACIONES_INICIALES` a `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts`
- [ ] T070 [P] [US2] Implementar `frontend/src/modules/liquidaciones/componentes/FiltrosLiquidaciones.tsx` sobre `compartido/ui/Filtros`: transportista (con `incluirInactivos`), mes, año y estado con sus textos `Todos …`, más las ranuras de declaración y resumen
- [ ] T071 [US2] Implementar `frontend/src/modules/liquidaciones/paginas/ListadoLiquidaciones.tsx` según `contracts/README.md` §Listado: primario *Generar liquidación* sólo con gestionar; declaración del filtro de estado en `role="status"`; resumen con cantidad y criterio de orden; columnas con `FilaNavegable` y token como destino real, transportista con `formatearCuit` e `Inactivo`, importes a la derecha, *Resta pagar* con `No corresponde` en `faint` para las anuladas, estado con el motivo como detalle y fila `atenuada`; estados de carga, vacío y sin coincidencias; `Paginacion`; cualquier cambio de filtro vuelve a la página 1
- [ ] T072 [US2] Registrar en `frontend/src/App.tsx` la ruta `/liquidaciones` exigiendo `Permisos.liquidacionesConsultar`, pasándole a `ListadoLiquidaciones` la prop `puedeGestionar`, igual que `ListadoFacturas`
- [ ] T073 [P] [US2] Escribir `frontend/src/modules/liquidaciones/paginas/ListadoLiquidaciones.test.tsx`: columnas y resta pagar de una pendiente, una pagada y una anulada atenuada con su palabra; declaración con y sin filtro de estado; mensajes de vacío y de sin coincidencias; sin el botón *Generar liquidación* para quien sólo consulta

**Checkpoint**: las liquidaciones se consultan y filtran; Gerencia lee lo que se debe desde el listado.

---

## Phase 7: User Story 4 - Registrar una orden de pago (Priority: P2)

**Goal**: registrar pagos parciales o totales, con confirmación del backend, y que la liquidación pase
sola a `pagada`.

**Independent Test**: sobre una liquidación de $355.000, registrar $200.000 (queda `pendiente` con
$155.000) y después $155.000 (queda `pagada`).

### Tests para User Story 4

- [ ] T074 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/OrdenDePagoTests.cs`: sin `confirmado` responde `409 pago_requiere_confirmacion` con `importe`, `restaPagarAntes`, `restaPagarDespues` y `quedaPagada`, **sin registrar nada**; con `confirmado` registra `OP-…` con usuario e instante y deja `pendiente`; el pago exacto deja `pagada` con **una** entrada `pagada` en el historial, con el usuario y el instante de esa orden, y los pagos parciales no agregan ninguna; la fecha igual a la generación y la de hoy se aceptan, un día antes y un día después se rechazan con `desde` y `hasta`; importe cero rechazado; importe mayor al saldo rechazado con `restaPagar`; `409 liquidacion_no_pagable` en pagada y en anulada; los viajes y la factura del escenario quedan intactos después de pagar (FR-020)
- [ ] T075 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/PagoConcurrenteTests.cs`: dos órdenes simultáneas que juntas superan el saldo terminan con **exactamente una** registrada y `ImportePagado` nunca mayor que el total; dos órdenes que juntas lo igualan dejan la liquidación `pagada` (SC-007). Los cruces con edición y con anulación viven en `EdicionConcurrenteTests` (T084) y `AnulacionConcurrenteTests` (T092): cada historia escribe sólo sus archivos

### Implementación de User Story 4

- [ ] T076 [US4] Agregar `RegistrarPagoAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs` con el `UPDATE` condicional de data-model §Pagar —`ImportePagado = ImportePagado + @importe`, `Estado` con el `CASE`, condición `Estado = Pendiente AND ImportePagado + @importe <= ImporteTotal`— hecho con `ExecuteUpdateAsync`, verificando **una fila afectada** antes de insertar la orden, y si el pago dejó la liquidación `pagada`, insertando además la entrada `pagada` del historial —todo en una transacción (FR-033)—
- [ ] T077 [US4] Implementar `backend/src/GT.Application/Liquidaciones/RegistrarOrdenDePago.cs` con el orden de validación de data-model §Pagar, el `hoy` desde `FechaHoyArgentina.Desde(reloj.GetUtcNow())`, la confirmación con los importes calculados por el servidor, y al fallar el `UPDATE` relee para distinguir `liquidacion_no_pagable` de `importe_supera_saldo`
- [ ] T078 [US4] Implementar `backend/src/GT.Api/Liquidaciones/CicloDeVidaLiquidacionEndpoints.cs` con `POST /api/liquidaciones/{id:int}/ordenes-de-pago` por gestionar (`201` con el detalle releído); registrar el caso de uso y el grupo en `backend/src/GT.Api/Program.cs`, y sumar el endpoint a `PermisosLiquidacionesTests`
- [ ] T079 [US4] Agregar `registrarOrdenDePago(id, peticion)` a `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts`, devolviendo el `409` de confirmación como un resultado distinguible y no como excepción genérica
- [ ] T080 [US4] Implementar `frontend/src/modules/liquidaciones/componentes/DialogoOrdenDePago.tsx` sobre `compartido/ui/Dialogo` según `contracts/README.md` §Registrar orden de pago: paso 1 con fecha (propuesta hoy, ayuda con el rango) e importe (propuesto lo que resta, ayuda con el máximo), errores al perder el foco o al enviar; enviar sin `confirmado`; paso 2 con el texto de confirmación armado **con los importes del `409`** y su variante `queda pagada`; *Volver* conserva los datos; *Confirmar orden de pago* reenvía con `confirmado: true`
- [ ] T081 [US4] Conectar en `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx` la acción primaria *Registrar orden de pago* al diálogo: al terminar relee el detalle y anuncia `Se registró la orden de pago OP-…` —con `La liquidación quedó pagada.` cuando corresponde—; los rechazos se muestran dentro del diálogo
- [ ] T082 [P] [US4] Escribir `frontend/src/modules/liquidaciones/componentes/DialogoOrdenDePago.test.tsx`: valores propuestos; errores de fecha e importe; el `409` lleva al paso 2 con los importes recibidos; *Volver* conserva los datos; confirmar reenvía con `confirmado: true`

**Checkpoint**: una liquidación se paga en una o varias órdenes y queda `pagada` sola.

---

## Phase 8: User Story 5 - Editar una liquidación (Priority: P3)

**Goal**: quitar y agregar viajes de una liquidación pendiente sin pagos, con historial de qué cambió y
sin que dos ediciones se pisen.

**Independent Test**: sobre una liquidación de tres viajes, quitar uno y agregar uno rendido después;
comprobar el total, el historial con los dos viajes y que el quitado vuelve a ofrecerse. Abrir dos
ediciones a la vez y comprobar que la segunda se rechaza.

### Tests para User Story 5

- [ ] T083 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/EdicionTests.cs`: quitar y agregar recalcula `ImporteTotal`, sube `Version`, borra el vínculo quitado e inserta el agregado; la entrada `edicion` registra **exactamente** los viajes quitados y agregados en `CambiosDeLiquidacionViajes`, y **ninguna** entrada `generacion` o `anulacion` tiene filas ahí; el viaje quitado vuelve a disponibles; conjunto igual al actual no escribe nada ni sube la versión; `version` distinta responde `409 liquidacion_modificada`; `409 liquidacion_no_editable` con motivo `pagada`, `anulada`, `conOrdenesDePago` y `transportistaNoLiquidable` —transportista dado de baja, y transportista cuyo CUIT pasó a coincidir con el de la empresa emisora—; `sin_viajes` y `total_en_cero`; un agregado con vínculo vigente en otra responde `409 viaje_ya_liquidado`; número, fecha de generación, estado, transportista y período quedan iguales antes y después de editar (FR-052); los viajes quitados y agregados conservan su estado y su factura (FR-020)
- [ ] T084 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/EdicionConcurrenteTests.cs`: dos ediciones simultáneas con la misma `version` —una quita un viaje y la otra agrega otro— terminan con **exactamente una** aplicada, la otra rechazada con `liquidacion_modificada`, y `ImporteTotal` igual a la suma de los vínculos vigentes; y, en el mismo archivo, una edición y un pago simultáneos nunca dejan una liquidación con pagos editada

### Implementación de User Story 5

- [ ] T085 [US5] Agregar `EditarAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs` con la transacción de data-model §Editar en ese orden: `UPDATE` condicional **primero** —con `Version = @versionAbierta` y `Version = Version + 1`— y verificación de una fila; borrado de quitados; inserción de agregados con la traducción del índice; entrada `edicion` con sus filas de `CambiosDeLiquidacionViajes`; ante cualquier rechazo, `rollback` y `ChangeTracker.Clear()`
- [ ] T086 [US5] Implementar `backend/src/GT.Application/Liquidaciones/EditarLiquidacion.cs`: rechaza con `transportistaNoLiquidable` si el transportista de la liquidación se dio de baja o ya no es externo (FR-045); valida con `ValidadorDeViajes` contra el transportista y el período **de la liquidación** —excluyendo del chequeo de vínculo vigente a los viajes que ya le pertenecen—, calcula quitados y agregados contra la composición leída, no escribe nada si el conjunto es igual, y al fallar el `UPDATE` relee para distinguir `liquidacion_no_editable` de `liquidacion_modificada`
- [ ] T087 [US5] Agregar `PUT /api/liquidaciones/{id:int}` a `backend/src/GT.Api/Liquidaciones/LiquidacionesEndpoints.cs` por gestionar; registrar el caso de uso en `backend/src/GT.Api/Program.cs` y sumar el endpoint a `PermisosLiquidacionesTests`
- [ ] T088 [US5] Agregar `editarLiquidacion(id, viajeIds, version)` a `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts`
- [ ] T089 [US5] Implementar `frontend/src/modules/liquidaciones/paginas/EditarLiquidacion.tsx` según `contracts/README.md` §Editar liquidación: carga el detalle y sus disponibles con `listarDisponibles`; callout sin formulario si al abrir no es editable; sección de sólo lectura; sección de incluidos y de disponibles con `TablaDeViajes` y los botones *Quitar* / *Agregar* **siempre en el DOM**, con nombre accesible `Quitar viaje #13` (convención [008]); mover filas sin llamar al servidor; total recalculado y anunciado; *Guardar cambios* deshabilitado sin viajes, con total en cero o sin cambios; al guardar manda la `version` con que se abrió y navega al detalle con el mensaje; rechazos con los textos del contrato, incluido `liquidacion_modificada`
- [ ] T090 [US5] Registrar en `frontend/src/App.tsx` la ruta `/liquidaciones/:id/editar` exigiendo `Permisos.liquidacionesGestionar`, y conectar la acción secundaria *Editar liquidación* de `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx` a esa ruta
- [ ] T091 [P] [US5] Escribir `frontend/src/modules/liquidaciones/paginas/EditarLiquidacion.test.tsx`: quitar y agregar mueven filas y anuncian el total; *Guardar cambios* deshabilitado sin viajes y sin cambios; se envía la `version` abierta; `liquidacion_modificada` muestra su texto; callout cuando no es editable al abrir

**Checkpoint**: una liquidación sin pagos se corrige quitando y agregando viajes, con historial y sin pisadas.

---

## Phase 9: User Story 6 - Anular una liquidación (Priority: P3)

**Goal**: anular con motivo una liquidación pendiente sin pagos, liberando sus viajes.

**Independent Test**: anular una liquidación de tres viajes con motivo y comprobar que queda `anulada`,
que sigue mostrando sus viajes y que los tres vuelven a ofrecerse al generar.

### Tests para User Story 6

- [ ] T092 [P] [US6] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/AnulacionTests.cs`: orden de validación —`404`, `409 liquidacion_no_anulable` antes que el motivo, `400 motivo_requerido` antes que la confirmación—; sin `confirmado` responde `409 anulacion_requiere_confirmacion` **sin cambiar nada**; con `confirmado` deja `anulada` con motivo, **todos** los vínculos en `Vigente = 0` y una entrada `anulacion`; los viajes vuelven a disponibles y se pueden generar en otra liquidación con otro número; con órdenes de pago informa cantidad y suma; pagada y ya anulada rechazadas; los viajes liberados conservan su estado —un `facturado` sigue `facturado` con su factura— (FR-020). Y en `backend/tests/GT.IntegrationTests/Liquidaciones/AnulacionConcurrenteTests.cs`, una anulación y un pago simultáneos nunca dejan un pago sobre una anulada

### Implementación de User Story 6

- [ ] T093 [US6] Agregar `AnularAsync` a `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs` con la transacción de data-model §Anular: `UPDATE` condicional de estado y motivo, verificación de una fila, `UPDATE` de `LiquidacionViajes` a `Vigente = 0` y entrada `anulacion`
- [ ] T094 [US6] Implementar `backend/src/GT.Application/Liquidaciones/AnularLiquidacion.cs` con el orden de validación de data-model §Anular, motivo recortado de hasta 500 caracteres, rechazo con cantidad y suma de órdenes (convención [004]) y relectura del detalle
- [ ] T095 [US6] Agregar `POST /api/liquidaciones/{id:int}/anulacion` por gestionar a `backend/src/GT.Api/Liquidaciones/CicloDeVidaLiquidacionEndpoints.cs`; registrar el caso de uso en `backend/src/GT.Api/Program.cs` y sumar el endpoint a `PermisosLiquidacionesTests`
- [ ] T096 [US6] Agregar `anularLiquidacion(id, motivo)` —que siempre envía `confirmado: true` porque el diálogo es la confirmación (research §7)— a `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts`
- [ ] T097 [US6] Implementar `frontend/src/modules/liquidaciones/componentes/DialogoAnulacion.tsx` sobre `compartido/ui/Dialogo` según `contracts/README.md` §Anular liquidación: texto con la cantidad de viajes que se liberan, campo *Motivo* obligatorio de hasta 500 con su error, *Volver* secundario y *Anular liquidación* destructivo, **sin primario**
- [ ] T098 [US6] Conectar en `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx` la acción destructiva *Anular liquidación* al diálogo: al terminar relee el detalle y anuncia `Se anuló la liquidación LQ-…`; los rechazos se muestran dentro del diálogo con sus textos
- [ ] T099 [P] [US6] Escribir `frontend/src/modules/liquidaciones/componentes/DialogoAnulacion.test.tsx`: motivo vacío marca el campo y no envía; *Volver* no envía; confirmar envía el motivo; rechazo con órdenes de pago muestra cantidad y suma

**Checkpoint**: las siete historias funcionan de punta a punta.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: la coherencia de los agregados, la suite entera, el recorrido manual y el mantenimiento.

- [ ] T100 [P] Escribir `backend/tests/GT.IntegrationTests/Liquidaciones/CoherenciaDeLiquidacionTests.cs`: después de generar, editar, pagar parcial y total y anular, en cada liquidación `ImporteTotal` es igual a la suma de los importes de sus vínculos vigentes —o de todos, si está anulada—, `ImportePagado` a la suma de sus órdenes, `Vigente` coincide con `Estado <> Anulada` fila por fila, y `ReglasDeLiquidacion.EstadoTrasPago` da el mismo estado que dejó el `CASE` del `UPDATE` sobre el mismo dato (research §2, convención [003])
- [ ] T059 **(movida desde US7; conserva su ID y se ejecuta acá, cuando ya existen las cuatro rutas)** Revisar en `frontend/src/App.tsx` que `/liquidaciones`, `/liquidaciones/nueva`, `/liquidaciones/:id` y `/liquidaciones/:id/editar` usen la misma protección de sesión que el resto —sin sesión redirige a `/ingresar` (FR-066)— y dejar anotado en un comentario el reparto consultar/gestionar de las cuatro, como el bloque del Módulo 6. La verificación la hace el paso 1 del quickstart (T102)
- [ ] T101 Correr `cd backend && dotnet test` y `cd frontend && npm test`, más el build y el lint del frontend, y dejar todo en verde; confirmar que las suites de `ListadoTransportistas` y `ListadoClientes` pasan **sin haber modificado ningún caso existente** —`ListadoClientes` suma sólo el caso de T037— (spec §Assumptions)
- [ ] T102 Recorrer los 43 pasos de `specs/009-gestion-liquidacion/quickstart.md` con las cuatro cuentas (`admin`, `admin.empresa`, `gerencia`, `trafico`), incluidos los dos pasos de dos navegadores (15 y 25), y anotar lo que el recorrido encuentre
- [ ] T103 [P] Recorrer `specs/009-gestion-liquidacion/checklists/ciclo-de-vida-y-pagos.md` contra lo implementado y tildar los ítems que el diseño o el código ya responden, anotando los que queden como deuda de spec
- [ ] T104 [P] Agregar la fila del Módulo 9 con su estado y conteo de tareas, y su entrada en *Qué queda abierto* y *Lo que cada módulo dejó como precedente*, en `specs/README.md`
- [ ] T105 Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (`[009] ...`), en *Decisiones transversales ya tomadas*. Partir de las candidatas de `plan.md` §Mantenimiento al cerrar la feature y **no incluir entradas por incluir**: sólo las que sean información transversal y relevante para futuras features

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias
- **Foundational (Phase 2)**: depende de Setup — **bloquea todas las historias**
- **User Stories (Phase 3 a 9)**: dependen de Foundational
- **Polish (Phase 10)**: depende de todas las historias

### User Story Dependencies

- **US1 — Generar (P1)**: sólo Foundational. Su navegación final necesita la pantalla de US3 para verse completa, pero el backend y el formulario se prueban solos
- **US7 — Acceso (P1)**: sólo Foundational; su test suma los endpoints de cada historia a medida que existen
- **US3 — Detalle (P2)**: sólo Foundational —el detalle del backend ya está en T031 y T032—. Se ordena después de US1 porque cierra su recorrido
- **US2 — Listado (P2)**: sólo Foundational
- **US4 — Órdenes de pago (P2)**: necesita la pantalla de detalle de US3, donde vive su diálogo
- **US5 — Editar (P3)**: necesita US3 (acción en el detalle) y reutiliza `ValidadorDeViajes` y `TablaDeViajes` de US1
- **US6 — Anular (P3)**: necesita US3 (acción en el detalle)

### Archivos que tocan varias historias

No llevan `[P]` entre sí y se hacen en el orden de las fases:

- `backend/src/GT.Infrastructure/Persistencia/RepositorioLiquidaciones.cs` — T031, T046, T066, T076, T085, T093
- `backend/src/GT.Api/Program.cs` — T033, T052, T068, T078, T087, T095
- `backend/src/GT.Api/Liquidaciones/LiquidacionesEndpoints.cs` — T052, T068, T087
- `backend/src/GT.Api/Liquidaciones/CicloDeVidaLiquidacionEndpoints.cs` — T078, T095
- `frontend/src/modules/liquidaciones/servicios/servicioLiquidaciones.ts` — T040, T053, T069, T079, T088, T096
- `frontend/src/App.tsx` — T056, T059, T063, T072, T090
- `frontend/src/modules/liquidaciones/paginas/DetalleLiquidacion.tsx` — T062, T081, T090, T098
- `backend/tests/GT.IntegrationTests/Liquidaciones/PermisosLiquidacionesTests.cs` — T058, T068, T078, T087, T095

### Within Each User Story

- Los tests se escriben primero y tienen que fallar antes de implementar
- Repositorio → caso de uso → endpoint → servicio del frontend → componente → página → ruta

### Parallel Opportunities

- Setup: T001 y T002
- Foundational: el dominio T003–T012, los tests T013–T014, las configuraciones T015–T019, las piezas de aplicación T026–T029 y el frontend T035, T038, T039
- Dentro de cada historia: todos sus tests `[P]`, y los componentes que no son la página
- Con Foundational terminado, US1, US7, US3 y US2 pueden avanzar a la vez en backend; US4, US5 y US6 esperan a la pantalla de detalle

---

## Parallel Example: User Story 1

```bash
# Los cinco tests de la historia, juntos:
Task: "TransportistasLiquidablesTests en backend/tests/GT.IntegrationTests/Liquidaciones/TransportistasLiquidablesTests.cs"
Task: "DisponiblesTests en backend/tests/GT.IntegrationTests/Liquidaciones/DisponiblesTests.cs"
Task: "GeneracionTests en backend/tests/GT.IntegrationTests/Liquidaciones/GeneracionTests.cs"
Task: "GeneracionConcurrenteTests en backend/tests/GT.IntegrationTests/Liquidaciones/GeneracionConcurrenteTests.cs"
Task: "RutasLiquidacionesTests en backend/tests/GT.IntegrationTests/Liquidaciones/RutasLiquidacionesTests.cs"

# Después del repositorio (T046), los dos casos de uso de consulta y el componente de tabla:
Task: "ConsultarTransportistasLiquidables en backend/src/GT.Application/Liquidaciones/ConsultarTransportistasLiquidables.cs"
Task: "ConsultarViajesDisponibles en backend/src/GT.Application/Liquidaciones/ConsultarViajesDisponibles.cs"
Task: "TablaDeViajes en frontend/src/modules/liquidaciones/componentes/TablaDeViajes.tsx"
```

---

## Implementation Strategy

### MVP First

1. Phase 1: Setup
2. Phase 2: Foundational (**bloquea todo**)
3. Phase 3: US1 — Generar
4. Phase 4: US7 — Acceso
5. Phase 5: US3 — Detalle, que cierra el recorrido de la generación
6. **Parar y validar**: pasos 1 a 19 de `quickstart.md`

El MVP es **generar, ver y estar protegido**: con eso ya no se liquida dos veces un viaje ni se olvida
uno, que es el problema del enunciado.

### Incremental Delivery

1. Setup + Foundational → la base está lista y las pantallas de los Módulos 3 y 5 siguen iguales
2. US1 + US7 + US3 → MVP
3. US2 → se encuentran las pendientes y Gerencia ve lo que se debe
4. US4 → se paga
5. US5 → se corrige una liquidación sin pagos
6. US6 → se anula una liquidación mal generada
7. Polish → coherencia, suite entera, recorrido manual, `specs/README.md` y `AGENTS.md`

---

## Notes

- `[P]` = archivo distinto y sin dependencias pendientes
- `[Story]` vincula la tarea a su historia para la trazabilidad
- Cada historia se puede completar y probar sola, con las dependencias de pantalla declaradas arriba
- **Trampas conocidas** (research §12): valores de enum escritos a mano en `CHECK` y filtros; `Numero` asignado por la secuencia y nunca por la entidad; rutas literales antes de `{id:int}`; `ExecuteUpdateAsync` no pasa por el rastreador, así que toda escritura relee el detalle; `rollback` más `ChangeTracker.Clear()`; `Fecha.Month` y `Fecha.Year` sólo en el árbol de la consulta; traducir la violación de índice por su nombre; la fecha de generación en Argentina
- Commit después de cada tarea o grupo lógico, sin firmar como coautor
