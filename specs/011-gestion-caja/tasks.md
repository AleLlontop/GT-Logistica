---

description: "Lista de tareas de implementación del Módulo 11"
---

# Tasks: Gestión de caja (Módulo 11)

**Input**: Documentos de diseño de `/specs/011-gestion-caja/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/README.md`,
`quickstart.md`

**Tests**: se incluyen. El plan los pide explícitamente (`Technical Context` → *Testing*) y
`quickstart.md` §"Lo que no se puede verificar a mano" nombra lo que sólo un test cubre: la carrera de doble
apertura, la del cierre simultáneo, la del movimiento contra el cierre, los `CHECK` y la invocación directa.

**Organization**: las tareas se agrupan por historia de usuario, para poder implementar y validar cada
una por separado.

## Format: `[ID] [P?] [Story] Descripción`

- **[P]**: puede ejecutarse en paralelo (archivo distinto, sin dependencias pendientes)
- **[Story]**: a qué historia pertenece (US1 … US5)
- Cada tarea lleva la ruta exacta del archivo

## Path Conventions

Aplicación web con backend y frontend separados (plan.md → *Project Structure*):

- Backend: `backend/src/GT.Api/Caja/`, `backend/src/GT.Application/Caja/`, `backend/src/GT.Domain/Caja/`,
  `backend/src/GT.Infrastructure/Persistencia/`
- Tests de backend: `backend/tests/GT.UnitTests/Caja/`, `backend/tests/GT.IntegrationTests/Caja/`
- Frontend: `frontend/src/modules/caja/`

## Decisiones que estas tareas fijan y los documentos de diseño dejaban abiertas

Anotadas acá para que la revisión las cuente; T084 las lleva a `contracts/README.md`.

1. **La caja es personal también en el servidor** (FR-035, spec §Assumptions): registrar y cerrar exigen que el
   usuario en sesión sea el responsable. El `UPDATE` que toma el lock suma
   `AND UsuarioResponsableId = @usuario`; con cero filas se relee y, si la caja existe y es de otro,
   responde `409 caja_ajena` — *"Esta caja es de otro empleado. Sólo quien la abrió puede registrar
   movimientos y cerrarla."*. Sin esto, cualquier usuario con `caja.gestionar` opera la caja de otro
   invocando la acción directamente.
2. **El detalle de la caja trae `totalIngresos`, `totalEgresos` y `saldoActual`**, calculados al leer
   con `ReglasDeCaja` (convención [003]), y `puedeOperar` —abierta y propia— decidido por el servidor.
   Es lo que exige SC-007: responder cuánto entró y cuánto salió sin sumar a mano.
3. **Las dos entradas de menú son de consulta**: *Caja* (`/caja`) y *Movimientos de caja*
   (`/movimientos-caja`), las dos por `caja.consultar`. *Abrir caja* se llega desde `/caja`, que muestra
   el botón sólo con `caja.gestionar`.
4. **El rango de fechas del filtro son días de Argentina** sobre un instante UTC: *desde* es
   `desde 00:00 −03:00` y *hasta* es `< hasta+1 00:00 −03:00`, los dos convertidos a UTC antes de
   consultar. Un movimiento del 16/09 a las 23:30 de Argentina —17/09 02:30 UTC— entra con
   *hasta* = 16/09.
5. **La pastilla de estado** agrega `abierta` y `cerrada` a `TONO_POR_VALOR` de
   `frontend/src/compartido/ui/Estado.tsx`, como hizo el Módulo 10: sin eso caen en `neutro`. No es un
   componente ni un token nuevo; es el noveno archivo modificado que enumera el plan.

---

## Phase 1: Setup (infraestructura compartida)

**Purpose**: dejar las carpetas del módulo. **No hay dependencias, variables de entorno ni cambios de
`Dockerfile` ni de `docker-compose.yml`** (plan §Technical Context).

- [X] T001 [P] Crear las carpetas del backend del módulo: `backend/src/GT.Domain/Caja/`, `backend/src/GT.Application/Caja/`, `backend/src/GT.Api/Caja/`, `backend/tests/GT.UnitTests/Caja/` y `backend/tests/GT.IntegrationTests/Caja/`
- [X] T002 [P] Crear el esqueleto del módulo de frontend `frontend/src/modules/caja/` con las subcarpetas `paginas/`, `componentes/` y `servicios/`
- [X] T003 **Contar las copias** (convención [010]): antes de escribir código, buscar en `frontend/src/modules/` cuántas copias locales hay de lo que este módulo va a necesitar —`esSinPermiso`, `detalleDeError`, `MENSAJE_ACCION_SIN_PERMISO` y el aviso de pantalla sin permiso— y anotar el número en el comentario de cabecera de `frontend/src/modules/caja/servicios/servicioCaja.ts`. Si alguna llega a la **tercera** copia con este módulo, se lleva a `compartido` en esta feature según [009] —suites tocadas sin modificar— y se suma como tarea antes de seguir; si no, se copia desde `frontend/src/modules/adelantos/servicios/servicioAdelantos.ts`

---

## Phase 2: Foundational (prerequisitos bloqueantes)

**Purpose**: esquema, entidades, reglas puras, permisos, menú, las piezas de aplicación que usan todas
las escrituras y las piezas compartidas del frontend.

**⚠️ CRÍTICO**: ninguna historia puede empezar hasta que esta fase esté completa.

### Dominio

- [X] T004 [P] Crear `EstadoCaja : byte` (`Abierta = 0`, `Cerrada = 1`) en `backend/src/GT.Domain/Caja/EstadoCaja.cs`, con el comentario ⚠ de que `CK_Cajas_CierreConsistente` e `IX_Cajas_UsuarioResponsable_Abierta` llevan `0` y `1` escritos a mano y reordenar el enum no falla al compilar (data-model §Enumeraciones)
- [X] T005 [P] Crear `TipoMovimientoCaja : byte` (`Ingreso = 0`, `Egreso = 1`) en `backend/src/GT.Domain/Caja/TipoMovimientoCaja.cs`, con el comentario ⚠ de `CK_MovimientosDeCaja_Referencia` (research §6)
- [X] T006 [P] Crear la entidad `Caja` en `backend/src/GT.Domain/Caja/Caja.cs`: `Id`, `SaldoInicial` (`decimal`), `FechaApertura` (`DateTime`, instante UTC de `TimeProvider`), `UsuarioResponsableId` + navegación a `Usuario` (**sin colección inversa en `Usuario`**), `Estado` que **nace `Abierta`**, `FechaCierre` y `SaldoFinal` anulables, y la colección `Movimientos`. Documentar que no se reabre ni se modifica después de cerrada y que no tiene número visible (spec §Assumptions)
- [X] T007 [P] Crear `MovimientoDeCaja` en `backend/src/GT.Domain/Caja/MovimientoDeCaja.cs` con `init`: `Id`, `CajaId` + navegación, `Tipo`, `Importe` (`decimal`), `Concepto`, `UsuarioId` + navegación a `Usuario`, `Fecha` (`DateTime` UTC), `FacturaId` + navegación a `FacturaCliente` y `OrdenDePagoId` + navegación a `OrdenDePago`, anulables y **sin colección inversa** en ninguna de las dos. Documentar que no se edita, no se anula y no se borra (FR-014) y que la referencia es sólo informativa (FR-012)
- [X] T008 [P] Crear `ReglasDeCaja` en `backend/src/GT.Domain/Caja/ReglasDeCaja.cs` con funciones puras: `SaldoFinal(saldoInicial, totalIngresos, totalEgresos)` = inicial + ingresos − egresos, en `decimal` (RN6); `SaldoInicialValido(importe)` —`>= 0` y `decimal.Round(importe, 2) == importe`— (RN4, FR-002); `ImporteValido(importe)` —`> 0` y a lo sumo dos decimales— (RN3, FR-008); `ConceptoValido(concepto)` —no nulo, no vacío tras `Trim()`, a lo sumo 200 (FR-009)—; y `ReferenciaAdmitida(tipo, facturaId, ordenDePagoId)` que devuelve el motivo si un ingreso trae orden o un egreso trae factura (RN8). **Ninguna lee el reloj** (convención [005])
- [X] T009 [P] Agregar `CajaGestionar = "caja.gestionar"` y `CajaConsultar = "caja.consultar"`, con su comentario de reparto (research §9), en `CodigosPermiso` de `backend/src/GT.Domain/Usuarios/Rol.cs`

### Tests de reglas puras

- [X] T010 [P] Escribir `backend/tests/GT.UnitTests/Caja/ReglasDeCajaTests.cs`: `SaldoFinal(10000, 3500, 2000)` es `11500` (RN6); sin movimientos es igual al inicial (CL1); con egresos mayores da negativo y no falla (FR-015); `SaldoInicialValido` con `-1000`, `0`, `10000` y `0,001`; `ImporteValido` con `0`, `-200`, `0,01`, `3500` y `150,555`; `ConceptoValido` con `null`, `""`, `"   "`, `"cobro flete"` y 201 caracteres; `ReferenciaAdmitida` en las cuatro combinaciones de tipo y referencia, más las dos sin referencia

### Persistencia

- [X] T011 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/CajaConfiguracion.cs`: tabla `Cajas`; `decimal(18,2)` para `SaldoInicial` y `SaldoFinal`; `Estado` con `HasConversion<byte>()` **sin `HasDefaultValue`** (la entidad ya nace `Abierta`; convención [009] sobre `HasSentinel`); FK a `Usuarios` en `Restrict` con `WithMany()`; `CK_Cajas_SaldoInicial` y `CK_Cajas_CierreConsistente` con los literales de data-model §Restricciones; el índice único filtrado `IX_Cajas_UsuarioResponsable_Abierta` sobre `UsuarioResponsableId` con `HasFilter("[Estado] = 0")`; e `IX_Cajas_FechaApertura` (`FechaApertura` e `Id` descendentes) para el listado. Exponer los nombres de los `CHECK` y del índice como constantes, con el comentario ⚠ de los enums escritos a mano
- [X] T012 [P] Crear `backend/src/GT.Infrastructure/Persistencia/Configuraciones/MovimientoDeCajaConfiguracion.cs`: tabla `MovimientosDeCaja`; `decimal(18,2)` para `Importe`; `Concepto` `nvarchar(200)` (FR-009); `Tipo` con `HasConversion<byte>()`; FK a `Cajas`, `Usuarios`, `Facturas` y `OrdenesDePago` en `Restrict`, las dos últimas anulables y **sin índice único** (FR-012); `CK_MovimientosDeCaja_Importe`, `CK_MovimientosDeCaja_Concepto` (`LEN(LTRIM(RTRIM([Concepto]))) > 0`, research §7) y `CK_MovimientosDeCaja_Referencia`; `IX_MovimientosDeCaja_Caja_Fecha` e `IX_MovimientosDeCaja_Fecha`. Nombres de restricciones como constantes
- [X] T013 Agregar `DbSet<Caja> Cajas` y `DbSet<MovimientoDeCaja> MovimientosDeCaja`, bajo un encabezado `Módulo 11`, en `backend/src/GT.Infrastructure/Persistencia/GtDbContext.cs`
- [X] T014 Generar y revisar la migración `Modulo11Caja` en `backend/src/GT.Infrastructure/Persistencia/Migraciones/`, verificando que **cree sólo `Cajas` y `MovimientosDeCaja`**, que **no contenga ningún `ALTER` sobre tablas existentes** —ni `Facturas`, ni `OrdenesDePago`, ni `Usuarios`—, que no cree secuencias, y que los `CHECK` y el filtro del índice lleven los literales de data-model
- [X] T015 Sembrar los dos permisos (módulo `Caja`) y su reparto —`caja.gestionar` a *Administración de la empresa* y *Administrador del sistema*; `caja.consultar` a esos dos más *Gerencia*— en `backend/src/GT.Infrastructure/DatosIniciales/SembradorInicial.cs`, de forma idempotente y sumando el párrafo del Módulo 11 al comentario de `PermisosPorRol` (FR-032)
- [X] T016 Agregar las dos entradas —`consultar-caja` / `Caja` / `/caja` y `consultar-movimientos-caja` / `Movimientos de caja` / `/movimientos-caja`, las dos por `CodigosPermiso.CajaConsultar`— con su comentario de reparto, en `backend/src/GT.Application/Autenticacion/CatalogoOpcionesMenu.cs` (decisión 3)
- [X] T017 **Test del Módulo 2**: en `backend/tests/GT.IntegrationTests/Usuarios/AsignarRolesTests.cs`, dentro de `Gerencia_RecibeSuPrimerPermisoConElModulo5`, agregar la aserción de que el módulo `Caja` de Gerencia trae **sólo** `caja.consultar`, con el comentario *"El Módulo 11 sumó el quinto"*, y cambiar `Assert.Equal(4, gerencia.PermisosPorModulo.Count)` por `5` con su comentario. **No tocar ninguna otra aserción**; si `Assert.Equal(4, roles.Count)` de la línea 130 cuenta roles y no permisos, queda igual
- [X] T018 [P] Crear `backend/tests/GT.IntegrationTests/Caja/DatosDePruebaCaja.cs` con los ayudantes de escenario: dos usuarios con *Administración de la empresa* y sus clientes autenticados (reutilizando `backend/tests/GT.IntegrationTests/Usuarios/DatosDePrueba.cs`), una factura `Pendiente`, una `Pagada` y una `Anulada` (reutilizando los ayudantes del Módulo 6), una orden de pago (reutilizando `backend/tests/GT.IntegrationTests/Liquidaciones/DatosDePruebaLiquidaciones.cs`), y un ayudante que **inserta cajas y movimientos directo por `GtDbContext`** con cualquier estado e instante, para los escenarios de listado con fechas pasadas
- [X] T019 Escribir `backend/tests/GT.IntegrationTests/Caja/RestriccionesDeCajaTests.cs`, con **cada fila inválida violando una sola restricción** (convención [009]): una caja abierta y una cerrada válidas se aceptan; se rechazan `SaldoInicial = -1`, abierta con `FechaCierre`, abierta con `SaldoFinal`, cerrada sin `FechaCierre` y cerrada sin `SaldoFinal`; `IX_Cajas_UsuarioResponsable_Abierta` rechaza una segunda abierta del mismo usuario y **acepta** una abierta más cualquier cantidad de cerradas del mismo usuario, y dos abiertas de usuarios distintos; movimientos con `Importe = 0`, `Concepto = '   '`, ingreso con `OrdenDePagoId` y egreso con `FacturaId` rechazados; ingreso con factura, egreso con orden y cada tipo sin referencia aceptados; dos movimientos con la misma factura aceptados (FR-012)

### Capa de aplicación: piezas compartidas

- [X] T020 [P] Crear `backend/src/GT.Application/Caja/NombresDeEstadoCaja.cs`: camelCase en el JSON para `EstadoCaja` (`abierta`, `cerrada`) y `TipoMovimientoCaja` (`ingreso`, `egreso`); lectura estricta del tipo del cuerpo —ausente o desconocido es inválido— (convención [003])
- [X] T021 [P] Crear `backend/src/GT.Application/Caja/Mensajes.cs` con los códigos de error —`datos_invalidos`, `referencia_invalida`, `rango_invalido`, `caja_ya_abierta`, `caja_cerrada`, `caja_ajena`, `confirmacion_requerida`, `cierre_desactualizado`, `caja_no_encontrada`— y **los textos exactos** de `contracts/README.md` §Textos y §Cuerpos de error, más los de campo de plan §UI Design Check (`Escribí el saldo inicial.`, `El saldo inicial no puede ser negativo.`, `Elegí si es un ingreso o un egreso.`, `Escribí un importe mayor que cero.`, `Escribí el importe con hasta dos decimales.`, `Escribí el concepto del movimiento.`, `Una orden de pago sólo puede asociarse a un egreso.`, `Una factura sólo puede asociarse a un ingreso.`, `La factura elegida ya no está pendiente de cobro.`, `La orden de pago elegida no existe.`, `La fecha desde es posterior a la hasta.`) y el de `caja_ajena` de la decisión 1
- [X] T022 [P] Crear `backend/src/GT.Application/Caja/Dtos.cs`: `AbrirCajaRequest(SaldoInicial?)`; `RegistrarMovimientoRequest(Tipo?, Importe?, Concepto?, FacturaId?, OrdenDePagoId?)`; `CierreRequest(Confirmado?, SaldoFinalConfirmado?)`; `UsuarioResumen(Id, Nombre)`; `CajaListado(Id, Responsable, FechaApertura, Estado, FechaCierre?, SaldoFinal?)`; `CajaDetalle` con lo mismo más `SaldoInicial`, `TotalIngresos`, `TotalEgresos`, `SaldoActual` y `PuedeOperar` (decisión 2); `MovimientoListado(Id, CajaId, Fecha, Tipo, Importe, Concepto, Responsable, Referencia?)` con la referencia **armada por el backend** —`Factura {NumeroComprobante} · {ClienteRazonSocial}` u `OP-{Numero}`— (convención [009]); `ResumenDeCierre(SaldoInicial, TotalIngresos, TotalEgresos, Movimientos, SaldoFinal)`; `OpcionDeReferencia(Id, Texto)`; `FiltrosDeMovimientos(Desde?, Hasta?, CajaId?, Pagina?)`; y la paginación `{ items, total, pagina, tamanioPagina }` reusando `PaginaDe<T>` (convención [003])
- [X] T023 [P] Crear `backend/src/GT.Application/Caja/ResultadoCaja.cs`: el enum `ErrorCaja` con un valor por código de T021 —los de `400` arriba y los de `409` debajo de una línea, como `ErrorLiquidacion`— y el récord de resultado con el valor, el campo marcado, el mensaje y el `ResumenDeCierre` actualizado para `confirmacion_requerida` y `cierre_desactualizado`
- [X] T024 Crear `backend/src/GT.Application/Caja/IRepositorioCaja.cs` con las firmas de las consultas y las tres transacciones de data-model §Transacciones; los instantes llegan por parámetro y ninguna consulta lee el reloj; el usuario en sesión llega por parámetro a toda escritura (decisión 1)
- [X] T025 Crear `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs` con `ObtenerDetalleAsync(id, usuarioId)`: la caja con su responsable y los **totales sumados en SQL** por tipo (`SumAsync` sobre los movimientos de esa caja), `SaldoActual` con `ReglasDeCaja.SaldoFinal` y `PuedeOperar = Estado == Abierta && UsuarioResponsableId == usuarioId`, sin rastrear; y `ObtenerCajaAbiertaDeAsync(usuarioId)`
- [X] T026 Implementar `backend/src/GT.Application/Caja/ConsultarDetalleCaja.cs`, que devuelve el `CajaDetalle` o `caja_no_encontrada`. Es también la relectura de apertura y cierre (convención [006])
- [X] T027 Registrar `IRepositorioCaja` → `RepositorioCaja` y `ConsultarDetalleCaja` bajo un bloque `Módulo 11` en `backend/src/GT.Api/Program.cs`, y agregar `CodigosPermiso.CajaGestionar` y `CodigosPermiso.CajaConsultar` a `AgregarPoliticasDePermisos`
- [X] T028 Crear `backend/src/GT.Api/Caja/RespuestasDeCaja.cs`: la traducción `ResultadoCaja` → HTTP en un solo lugar —`400` para lo tipeado o elegido (`datos_invalidos`, `referencia_invalida`, `rango_invalido`) con `campo`, `409` para el estado (`caja_ya_abierta`, `caja_cerrada`, `caja_ajena`, y `confirmacion_requerida` y `cierre_desactualizado` con el resumen en el cuerpo), `404` para `caja_no_encontrada`— con los cuerpos de `contracts/README.md` §Cuerpos de error (convención [005]). Un error sin código propio cae en `datos_invalidos`, nunca en `500`

### Frontend compartido

- [X] T029 [P] Agregar `cajaGestionar: 'caja.gestionar'` y `cajaConsultar: 'caja.consultar'`, con su comentario, a `Permisos` en `frontend/src/modules/autenticacion/servicios/sesion.ts`
- [X] T030 [P] Agregar `'consultar-caja': 'Operación'` y `'consultar-movimientos-caja': 'Operación'` a `SECCION_POR_CODIGO` en `frontend/src/compartido/seccionesDeMenu.ts`, **sin tocar** `seccionesDeMenu.test.ts`
- [X] T031 [P] Agregar a `TONO_POR_VALOR` de `frontend/src/compartido/ui/Estado.tsx`, bajo un comentario `Caja (Módulo 11)`, `abierta: 'pendiente'` y `cerrada: 'rendido'`, con la línea de por qué: abierta es un proceso en curso y cerrada es final, y la palabra los distingue (FR-029, decisión 5)
- [X] T032 Crear `frontend/src/modules/caja/servicios/servicioCaja.ts` con los tipos del contrato (`EstadoCaja`, `TipoMovimientoCaja`, `CajaListado`, `CajaDetalle`, `MovimientoListado`, `ResumenDeCierre`, `OpcionDeReferencia`, `PaginaDeCajas`, `PaginaDeMovimientos`, los cuerpos de error), el objeto `CodigosErrorCaja`, `NOMBRES_DE_ESTADO` (`Abierta`, `Cerrada`), `NOMBRES_DE_TIPO` (`Ingreso`, `Egreso`), `detalleDeError`, `esSinPermiso` y `MENSAJE_ACCION_SIN_PERMISO` según lo decidido en T003. Apoyado en `compartido/clienteHttp`; **las rutas no llevan `/api`**

**Checkpoint**: la base migra, arranca, siembra los permisos y muestra las dos entradas en *Operación* según el rol. Las historias pueden empezar.

---

## Phase 3: User Story 1 - Abrir la caja (Priority: P1) 🎯 MVP

**Goal**: abrir una caja con saldo inicial, quedar como responsable y verla `abierta`; nunca dos abiertas
por empleado.

**Independent Test**: con dos usuarios administrativos, el primero abre su caja con $10.000 y queda
responsable; una segunda apertura se rechaza con el mensaje de cerrar la actual; el segundo abre la suya
en paralelo.

### Tests para User Story 1

- [X] T033 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Caja/AperturaTests.cs`: `201` con `Location`, estado `abierta`, saldo inicial, el usuario en sesión como responsable y una fecha de apertura con `Z` (CA1, FR-005); `saldoInicial` `0` aceptado; `-1000`, con tres decimales y ausente responden `400 datos_invalidos` con `campo = saldoInicial` **sin crear filas** (RN4, RN5); una segunda apertura del mismo usuario responde `409 caja_ya_abierta` con el texto del contrato y **no crea nada** (CA2); otro usuario abre la suya con `201` (FR-004); después de cerrarla por el ayudante de T018, el mismo usuario puede abrir otra; `GET /api/caja/abierta` devuelve la propia y `204` sin ninguna; `GET /api/caja/{id}` trae responsable, apertura, estado, `saldoActual` igual al inicial y `puedeOperar` verdadero para el dueño y falso para otro usuario
- [X] T034 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Caja/AperturaConcurrenteTests.cs`: dos `POST /api/caja` simultáneos del mismo usuario —dos alcances de servicio distintos y `Task.WhenAll`— terminan con **exactamente un** `201` y un `409 caja_ya_abierta`, y **una sola** fila en `Cajas` (CL3, SC-002)
- [X] T035 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Caja/RutasCajaTests.cs`: `GET /api/caja/abierta` responde su propio cuerpo o `204`, y no `404` ni un error de conversión, lo que prueba que `{id:int}` no la captura (convención [005]). `facturas-pendientes` y `ordenes-de-pago` se suman en US2
- [X] T036 [P] [US1] Escribir `backend/tests/GT.IntegrationTests/Caja/ConsultaCajasTests.cs`: el listado trae cajas de todos los responsables, con responsable, apertura, estado, y cierre y saldo final sólo en las cerradas; orden `FechaApertura DESC, Id DESC`, sin repetidos ni faltantes entre páginas con 25 cajas (convención [003])

### Implementación de User Story 1

- [X] T037 [US1] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs`: `ExisteCajaAbiertaAsync(usuarioId)`, `AbrirAsync(caja)` que inserta y **traduce la violación de `IX_Cajas_UsuarioResponsable_Abierta` a `CajaYaAbiertaException`** con el patrón de `GuardarTraduciendoIndiceAsync` de `RepositorioLiquidaciones.cs` (convención [003], data-model §Abrir), y `ConsultarCajasAsync(pagina)` con el orden total y la proyección de `CajaListado`, contando y paginando en SQL
- [X] T038 [US1] Implementar `backend/src/GT.Application/Caja/AbrirCaja.cs`: `SaldoInicialValido` —ausente con `Escribí el saldo inicial.`, negativo o con más de dos decimales con su texto—, consulta previa `ExisteCajaAbiertaAsync` que responde `caja_ya_abierta`, `AbrirAsync` con el instante de `TimeProvider` y el usuario por parámetro, `CajaYaAbiertaException` traducida al **mismo** `caja_ya_abierta`, y **relee el detalle**
- [X] T039 [P] [US1] Implementar `backend/src/GT.Application/Caja/ConsultarCajas.cs`, que devuelve la página de `CajaListado`
- [X] T040 [US1] Implementar `backend/src/GT.Api/Caja/CajaEndpoints.cs` con `GET /api/caja/abierta` (gestionar; `200` o `204`), `GET /api/caja` (consultar), `GET /api/caja/{id:int}` (consultar) y `POST /api/caja` (gestionar, `201` con `Location`), con el usuario de la sesión por parámetro; registrar `AbrirCaja` y `ConsultarCajas` y mapear el grupo en `backend/src/GT.Api/Program.cs`
- [X] T041 [US1] Agregar a `frontend/src/modules/caja/servicios/servicioCaja.ts`: `obtenerMiCajaAbierta()` —`null` con `204`—, `listarCajas(pagina)`, `obtenerCaja(id)` y `abrirCaja(saldoInicial)`
- [X] T042 [US1] Implementar `frontend/src/modules/caja/paginas/ConsultaDeCajas.tsx`, con la prop `puedeGestionar`, según `contracts/README.md` y plan §UI Design Check: título `Cajas`; con `puedeGestionar` consulta `obtenerMiCajaAbierta` y muestra el primario *Abrir caja* sólo si no tiene una, o un enlace terciario a su caja abierta si la tiene; tabla con responsable, apertura con `formatearInstante`, estado con la pastilla de `Estado`, cierre y saldo final con `formatearPesos` sólo en las cerradas, y la fila navegable a `/caja/:id` con `FilaNavegable` y el enlace como destino real (convención [008]); `Paginacion`; estados de carga, vacío, error y **sin permiso** —un `403` al cargar, detectado con `esSinPermiso`, dibuja sólo el título y el aviso de que falta permiso y a quién pedírselo, sin tabla y sin "volvé a intentar"— (FR-033, convención [010])
- [X] T043 [US1] Implementar `frontend/src/modules/caja/paginas/AbrirCaja.tsx`, con la prop `puedeGestionar`: sin ella dibuja sólo el título y el aviso sin permiso, sin formulario (FR-033); con ella, *Saldo inicial* con `InputImporte` de `frontend/src/compartido/ui/InputImporte.tsx` (convención [011]), `corto`, obligatorio marcado por `required` (convención [008]), con sus dos errores al perder el foco o al apretar *Abrir caja*; *Cancelar* vuelve a `/caja`; *Abrir caja* primario **no deshabilitado** salvo mientras envía; al guardar navega a `/caja/:id` con el mensaje `Caja abierta con éxito.` (convención [005]: el formulario no queda en pantalla); un `409 caja_ya_abierta` se muestra en `role="alert"` con enlace a su caja abierta; un `403` muestra el texto de acción sin permiso
- [X] T044 [US1] Implementar `frontend/src/modules/caja/paginas/DetalleDeCaja.tsx` con la prop `puedeGestionar`, en su primera versión: encabezado `Caja` con la pastilla de estado y el contexto `Responsable · abierta el dd/mm/aaaa hh:mm`; tarjeta con saldo inicial, total de ingresos, total de egresos y saldo actual con `formatearPesos` (decisión 2); si está cerrada, fecha de cierre y saldo final, sin acciones y con el terciario *Volver a cajas*; el mensaje que llega por navegación se anuncia con `role="status"` en el `<p>` que lo contiene (convención [008]); estados de carga, inexistente y **sin permiso** por `403`. Las acciones *Registrar movimiento* y *Cerrar caja* y la tabla de movimientos se suman en US2 y US3
- [X] T045 [US1] Registrar en `frontend/src/App.tsx`, bajo un comentario `Rutas del Módulo 11`, `/caja`, `/caja/nueva` y `/caja/:id` —las literales antes que la de identificador—, con la misma protección de sesión y `Layout` que el resto, pasando `puedeGestionar` calculada con `tienePermiso(sesion, Permisos.cajaGestionar)`
- [X] T046 [P] [US1] Escribir `frontend/src/modules/caja/paginas/AbrirCaja.test.tsx` con los servicios mockeados: guardar vacío marca el campo y **no llama** a `abrirCaja`; `-1000` marca el error de negativo; `0` se envía; éxito navega a la caja con el mensaje; `409 caja_ya_abierta` en `role="alert"` conservando lo cargado; `403` al guardar muestra el texto de acción sin permiso; sin `puedeGestionar` muestra el aviso y ningún campo
- [X] T047 [P] [US1] Escribir `frontend/src/modules/caja/paginas/ConsultaDeCajas.test.tsx`: columnas de una abierta y una cerrada, con la palabra del estado y el saldo final sólo en la cerrada; *Abrir caja* con `puedeGestionar` y sin caja propia, enlace a la propia si la tiene, y ningún botón para quien sólo consulta (US4 esc. 7); un `403` muestra el aviso sin permiso y no el error de carga
- [X] T048 [P] [US1] Escribir `frontend/src/modules/caja/paginas/DetalleDeCaja.test.tsx`: datos, totales y saldo actual; una cerrada muestra cierre y saldo final sin acciones; anuncio del mensaje recibido por navegación; un `403` al cargar muestra el aviso sin permiso

**Checkpoint**: un empleado abre su caja y la ve `abierta` con su nombre; no puede abrir una segunda.

---

## Phase 4: User Story 5 - Acceso restringido (Priority: P1)

**Goal**: que sólo quien corresponde opere o consulte, con la restricción en el servidor.

**Independent Test**: intentar cada ruta y cada endpoint sin sesión, con *Tráfico*, con *Gerencia* y con
*Administración de la empresa*.

### Tests para User Story 5

- [X] T049 [P] [US5] Escribir `backend/tests/GT.IntegrationTests/Caja/PermisosCajaTests.cs` con los endpoints que ya existen —`GET /abierta`, `GET /`, `GET /{id}`, `POST /`—: sin sesión `401`; *Tráfico* `403` en todos; *Gerencia* `200` en `GET /` y `GET /{id}` y `403` en `GET /abierta` y `POST /` (US5 esc. 3, SC-009); *Administración de la empresa* y *Administrador del sistema* acceden a todo; el menú de la sesión trae las dos entradas para los tres roles con consulta y ninguna para *Tráfico* (FR-031 a FR-034). Los endpoints de las historias siguientes se suman a este test en la tarea que los implementa

### Implementación de User Story 5

- [X] T050 [US5] Correr `frontend/src/compartido/seccionesDeMenu.test.ts` sin modificarlo y confirmar que sigue en verde con los dos códigos nuevos mapeados a *Operación*

**Checkpoint**: la apertura está protegida por permiso y sólo quien corresponde ve el módulo en el menú.

---

## Phase 5: User Story 2 - Registrar ingresos y egresos (Priority: P1)

**Goal**: registrar ingresos y egresos con concepto y referencia opcional sobre la caja abierta propia,
con fecha y responsable.

**Independent Test**: sobre una caja abierta, un ingreso de $3.500 con una factura pendiente y un egreso
de $2.000 con una orden de pago quedan con fecha, responsable y referencia; sobre una caja cerrada, el
intento se rechaza con el aviso.

### Tests para User Story 2

- [X] T051 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Caja/MovimientosTests.cs`: `201` para un ingreso de `3500` con factura `Pendiente` y un egreso de `2000` con orden de pago, cada uno con el usuario en sesión, un instante con `Z`, el concepto recortado y la referencia armada (`Factura … · …` y `OP-…`) (US2 esc. 2 y 3, RN7); un movimiento sin referencia se registra (RN8); `400 datos_invalidos` con `campo` para tipo ausente o desconocido, importe ausente, `0`, `-200` o con tres decimales, y concepto ausente, vacío, de sólo espacios o de 201 caracteres (CA4, RN5, FR-009); `400 referencia_invalida` con `campo` para un ingreso con orden de pago, un egreso con factura, una factura `Pagada`, una `Anulada`, una inexistente y una orden inexistente, **invocando directo** (US2 esc. 6 y 8, FR-011); sobre una caja cerrada `409 caja_cerrada` (CA8); sobre la caja de otro empleado `409 caja_ajena` (FR-035, decisión 1); sobre una inexistente `404`; **ningún rechazo crea filas**; un egreso mayor que el saldo actual se registra y el detalle muestra `saldoActual` negativo (FR-015); la factura y la orden de pago referenciadas quedan con su estado intacto y la misma factura se referencia desde dos movimientos (FR-012); `GET /api/caja/{id}/movimientos` los trae paginados por `Fecha` e `Id` descendentes con todas las columnas (CA5)
- [X] T052 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Caja/ReferenciasTests.cs`: `GET /api/caja/facturas-pendientes` trae sólo las `Pendiente` —ni `Pagada` ni `Anulada`— con el texto armado; `GET /api/caja/ordenes-de-pago`, con 51 órdenes, trae **las 50 más recientes** por número descendente sin filtro de estado y no la más vieja, y esa orden más vieja **se acepta igual** al guardar un egreso invocando directo (research §4, FR-011); una factura que pasa a `Pagada` después de listada se rechaza al guardar con `referencia_invalida` (US2 esc. 8, edge case de la referencia que deja de estar pendiente)
- [X] T053 [P] [US2] Escribir `backend/tests/GT.IntegrationTests/Caja/MovimientosConcurrentesTests.cs`: dos movimientos simultáneos sobre la misma caja abierta se registran **los dos** (edge case de dos pestañas)

### Implementación de User Story 2

- [X] T054 [US2] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs`: `RegistrarMovimientoAsync` con la transacción de data-model §Registrar movimiento —`BEGIN TRAN`, el `UPDATE` que no cambia nada con `ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, c => c.Estado))` y `WHERE Id = @cajaId AND Estado = Abierta AND UsuarioResponsableId = @usuario` (research §2, decisión 1), **verificación de una fila afectada** antes de seguir, relectura de la factura `Pendiente` con el lock tomado, `INSERT` y `COMMIT`; con cero filas `rollback` y devolver si la caja no existe, es ajena o está cerrada—; `ConsultarMovimientosDeCajaAsync(cajaId, pagina)`; `ConsultarFacturasPendientesAsync` con `Estado == EstadoFactura.Pendiente` **en el árbol de la consulta** (research §5, convención [003]); y `ConsultarOrdenesDePagoAsync` sin filtro de estado, con `OrderByDescending(Numero).Take(50)` en SQL (research §4, FR-011). La proyección de `MovimientoListado` va en un `Expression<>` compartido para que el listado de la caja, el resumen de cierre y la consulta global lean igual (convención [010])
- [X] T055 [P] [US2] Implementar `backend/src/GT.Application/Caja/RegistrarMovimiento.cs` con el orden: tipo presente y conocido, `ImporteValido`, `ConceptoValido` sobre el concepto recortado, `ReferenciaAdmitida`, existencia de la orden de pago, y `RegistrarMovimientoAsync` con el instante de `TimeProvider` y el usuario por parámetro, traduciendo cada resultado de la transacción a `caja_no_encontrada`, `caja_ajena`, `caja_cerrada` o `referencia_invalida`
- [X] T056 [P] [US2] Implementar `backend/src/GT.Application/Caja/ConsultarFacturasPendientes.cs` y `backend/src/GT.Application/Caja/ConsultarOrdenesDePago.cs`, que devuelven `OpcionDeReferencia[]` sin paginar
- [X] T057 [US2] Implementar `backend/src/GT.Api/Caja/MovimientosDeCajaEndpoints.cs` con `POST /api/caja/{id:int}/movimientos` (gestionar, `201`) y `GET /api/caja/{id:int}/movimientos` (consultar); agregar `GET /api/caja/facturas-pendientes` y `GET /api/caja/ordenes-de-pago` (gestionar) a `backend/src/GT.Api/Caja/CajaEndpoints.cs`; registrar los casos de uso y el grupo en `backend/src/GT.Api/Program.cs`; sumar los cuatro endpoints a `backend/tests/GT.IntegrationTests/Caja/PermisosCajaTests.cs` —*Gerencia* `200` en `GET /{id}/movimientos` y `403` en los otros tres— y las dos rutas literales a `backend/tests/GT.IntegrationTests/Caja/RutasCajaTests.cs`
- [X] T058 [US2] Agregar `registrarMovimiento(cajaId, peticion)`, `listarMovimientosDeCaja(cajaId, pagina)`, `listarFacturasPendientes()` y `listarOrdenesDePago()` a `frontend/src/modules/caja/servicios/servicioCaja.ts`
- [X] T059 [US2] Implementar `frontend/src/modules/caja/paginas/RegistrarMovimiento.tsx`, con la prop `puedeGestionar`, según plan §UI Design Check: sin ella, el aviso sin permiso; al cargar lee la caja y, si **no está abierta o no es propia** (`puedeOperar` falso), dibuja sólo el aviso `No hay una caja abierta. Abrí una caja para poder registrar movimientos.` con enlace a `/caja`, sin formulario (CA3); con ella, *Tipo* como dos botones *toggle* `Ingreso` / `Egreso` con `aria-pressed`, ninguno preseleccionado; *Importe* con `InputImporte`, `corto`; *Concepto* de hasta 200; *Referencia* como `<select>` nativo (convención [007]) `largo`, que se carga según el tipo —facturas pendientes para ingreso, órdenes de pago para egreso—, con `Sin referencia` como primera opción, con *Egreso* la línea `Se muestran las 50 órdenes de pago más recientes.` debajo del campo (FR-011), y que al cambiar el tipo **vuelve a `Sin referencia`** conservando importe y concepto; los errores de campo al perder el foco o al apretar *Guardar movimiento*, conservando lo cargado (FR-010); *Cancelar* vuelve a la caja; *Guardar movimiento* primario no deshabilitado salvo mientras envía; al guardar navega a `/caja/:id` con el mensaje de confirmación; `referencia_invalida` marca la referencia con su texto; `404`, `caja_cerrada` y `caja_ajena` al guardar muestran el aviso de CA3 en `role="alert"` sin perder lo cargado (contracts §Registrar movimiento sin caja operable); un `403` muestra el texto de acción sin permiso
- [X] T060 [US2] Sumar a `frontend/src/modules/caja/paginas/DetalleDeCaja.tsx`: con `puedeGestionar` y `puedeOperar`, el primario *Registrar movimiento* a `/caja/:id/movimientos/nuevo`; la tabla de movimientos con fecha (`formatearInstante`), tipo en palabra, importe a la derecha con `formatearPesos`, concepto, responsable y referencia si la tiene (CA5), `Paginacion`, y el estado vacío `Esta caja no tiene movimientos.`
- [X] T061 [US2] Registrar en `frontend/src/App.tsx` la ruta `/caja/:id/movimientos/nuevo`, pasándole `puedeGestionar`
- [X] T062 [P] [US2] Escribir `frontend/src/modules/caja/paginas/RegistrarMovimiento.test.tsx`: guardar vacío marca tipo, importe y concepto y **no llama** a `registrarMovimiento`; importe `0` y con tres decimales marcados; concepto de sólo espacios marcado; elegir *Ingreso* ofrece facturas y *Egreso* órdenes, con la línea del tope de 50 sólo en *Egreso*; cambiar el tipo vuelve a `Sin referencia` y conserva importe y concepto; `referencia_invalida` marca la referencia; una caja cerrada muestra el aviso de caja no abierta y ningún campo (CA3); un `409 caja_cerrada` al guardar muestra el mismo aviso y conserva lo cargado; un `403` muestra el texto de acción sin permiso; éxito navega a la caja
- [X] T063 [US2] Sumar a `frontend/src/modules/caja/paginas/DetalleDeCaja.test.tsx`: la tabla muestra las seis columnas con la referencia; *Registrar movimiento* sólo con `puedeGestionar` y `puedeOperar`; una caja de otro empleado se ve sin acciones

**Checkpoint**: los movimientos del día se registran con fecha, responsable y referencia, y el saldo actual se lee en la caja.

---

## Phase 6: User Story 3 - Cerrar la caja con su resumen (Priority: P1)

**Goal**: ver el resumen con el saldo final calculado, y confirmar o cancelar el cierre sin cerrar nunca
con un número que no se vio.

**Independent Test**: caja con $10.000, ingresos por $3.500 y egresos por $2.000 → el resumen muestra
$11.500; confirmar la deja `cerrada` con ese saldo y sin admitir movimientos; cancelar otra la deja
`abierta`.

### Tests para User Story 3

- [X] T064 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Caja/CierreTests.cs`: `GET /api/caja/{id}/cierre` con $10.000, $3.500 y $2.000 trae saldo inicial, los dos movimientos, los totales y `saldoFinal` `11500` (CA6, RN6); sin movimientos, lista vacía y saldo final igual al inicial (CL1); una caja con movimientos de un día anterior los incluye todos (FR-022); `POST` **sin `confirmado`** responde `409 confirmacion_requerida` con el resumen y la caja sigue `abierta` (US3 esc. 6); con `confirmado` y `saldoFinalConfirmado` igual queda `cerrada` con fecha de cierre con `Z` y ese saldo final (CA8); con `saldoFinalConfirmado` distinto —un movimiento registrado entre el `GET` y el `POST`— responde `409 cierre_desactualizado` con el resumen **nuevo** y no cierra (US3 esc. 7); cerrar una caja cerrada responde `409 caja_cerrada`; la de otro empleado `409 caja_ajena`; después de cerrar, `POST /movimientos` responde `409 caja_cerrada` (US3 esc. 5) y el mismo empleado puede abrir otra; el saldo final registrado ya no cambia (SC-008)
- [X] T065 [P] [US3] Escribir `backend/tests/GT.IntegrationTests/Caja/CierreConcurrenteTests.cs`, con dos alcances de servicio y `Task.WhenAll`: dos cierres confirmados simultáneos terminan con **exactamente uno** `200` y el otro `409 caja_cerrada` (FR-021); un movimiento y un cierre simultáneos, repetidos varias veces, terminan siempre en uno de los dos órdenes válidos —movimiento `201` y cierre `409 cierre_desactualizado` con la caja abierta, o cierre `200` y movimiento `409 caja_cerrada`— y **nunca** con una caja cerrada cuyo `SaldoFinal` difiere de saldo inicial + ingresos − egresos de sus movimientos (edge case, research §2)

### Implementación de User Story 3

- [X] T066 [US3] Agregar a `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs`: `ConsultarResumenAsync(cajaId)` sin transacción —una foto— con los movimientos en orden cronológico por la proyección compartida de T054 y los totales sumados en SQL; y `CerrarAsync` con la transacción de data-model §Cerrar —el mismo `UPDATE` que no cambia nada con el `WHERE` de T054, verificación de una fila, relectura de los totales **con el lock tomado**, comparación con `saldoFinalConfirmado`, y recién ahí el `UPDATE` a `Cerrada` con `FechaCierre` y `SaldoFinal`—; con cero filas o con discrepancia, `rollback` y devolver el motivo
- [X] T067 [P] [US3] Implementar `backend/src/GT.Application/Caja/ConsultarResumenDeCierre.cs`: existencia, propia y abierta —si no, `caja_no_encontrada`, `caja_ajena` o `caja_cerrada`— y el `ResumenDeCierre` con `ReglasDeCaja.SaldoFinal`
- [X] T068 [P] [US3] Implementar `backend/src/GT.Application/Caja/CerrarCaja.cs`: sin `confirmado = true` o sin `saldoFinalConfirmado` responde `confirmacion_requerida` con el resumen actual **sin tocar nada**; si no, `CerrarAsync` con el instante de `TimeProvider`; ante discrepancia `cierre_desactualizado` con el resumen releído; ante cero filas **relee** para dar `caja_cerrada` o `caja_ajena`; si cerró, relee el detalle (convención [006])
- [X] T069 [US3] Implementar `backend/src/GT.Api/Caja/CierreDeCajaEndpoints.cs` con `GET /api/caja/{id:int}/cierre` y `POST /api/caja/{id:int}/cierre`, los dos por gestionar; registrar los casos de uso y el grupo en `backend/src/GT.Api/Program.cs`, y sumar los dos a `backend/tests/GT.IntegrationTests/Caja/PermisosCajaTests.cs` —*Gerencia* `403`—
- [X] T070 [US3] Agregar `obtenerResumenDeCierre(cajaId)` y `cerrarCaja(cajaId, saldoFinalConfirmado)` —que envía `confirmado: true` porque la pantalla de resumen es la confirmación, y **devuelve los `409 confirmacion_requerida` y `cierre_desactualizado` como resultado con el resumen, no como excepción** (convención [009])— a `frontend/src/modules/caja/servicios/servicioCaja.ts`
- [X] T071 [US3] Implementar `frontend/src/modules/caja/paginas/ResumenDeCierre.tsx` según plan §UI Design Check: el aviso sin permiso lo decide el **`403` de su carga** (`GET /api/caja/{id}/cierre`, que exige gestionar), detectado con `esSinPermiso`, y no la prop (convención [010], contracts §Pantallas); título `Cerrar caja`; saldo inicial, total de ingresos, total de egresos y la cifra destacada del **saldo final**, todos con `formatearPesos`; la tabla de movimientos con las columnas de CA5 o, vacía, `EstadoVacio` con `Esta caja no tiene movimientos. El saldo final es igual al saldo inicial.` (CL1); **sin campos de entrada**; *Cancelar* secundario vuelve a `/caja/:id` **sin llamar al servidor** (CA7); *Confirmar cierre* primario, deshabilitado sólo mientras envía, manda el saldo final que se está mostrando; un `cierre_desactualizado` —o un `confirmacion_requerida`, tratado igual— **reemplaza el resumen en pantalla** por el recibido y anuncia en `role="status"` el texto del contrato de ese código, para confirmar de nuevo (US3 esc. 7); al cerrar navega a `/caja/:id` con el mensaje de confirmación; `caja_cerrada` en `role="alert"`; una caja que al cargar ya no está abierta o no es propia muestra el aviso correspondiente sin *Confirmar cierre*
- [X] T072 [US3] Sumar a `frontend/src/modules/caja/paginas/DetalleDeCaja.tsx` la acción **secundaria** *Cerrar caja* a `/caja/:id/cierre`, con `puedeGestionar` y `puedeOperar`; y registrar en `frontend/src/App.tsx` la ruta `/caja/:id/cierre`, sin prop de permiso (lo decide el `403` de su carga, T071)
- [X] T073 [P] [US3] Escribir `frontend/src/modules/caja/paginas/ResumenDeCierre.test.tsx`: saldo inicial, totales y saldo final `$ 11.500,00` con dos movimientos (CA6); sin movimientos, el texto de CL1; *Cancelar* no llama a `cerrarCaja`; *Confirmar cierre* envía el saldo mostrado; un `cierre_desactualizado` reemplaza los números en pantalla, anuncia el texto y **no navega**, y el segundo *Confirmar cierre* envía el saldo nuevo; un `confirmacion_requerida` se trata igual; éxito navega a la caja; un `403` **al cargar** muestra el aviso sin permiso sin resumen ni *Confirmar cierre*, y no el error de carga
- [X] T074 [US3] Sumar a `frontend/src/modules/caja/paginas/DetalleDeCaja.test.tsx`: *Cerrar caja* es secundaria y sólo aparece con `puedeGestionar` y `puedeOperar`; una caja cerrada no la muestra

**Checkpoint**: el ciclo del día —abrir, registrar, cerrar— funciona de punta a punta.

---

## Phase 7: User Story 4 - Consultar movimientos por período o por caja (Priority: P2)

**Goal**: consultar los movimientos por rango de fechas o por caja, y que el gerente revise cajas y
movimientos sin operar.

**Independent Test**: con dos cajas cerradas de días distintos y una abierta, filtrar por rango y por
caja, verificar las columnas, y que un período sin movimientos muestra el mensaje informativo.

### Tests para User Story 4

- [X] T075 [P] [US4] Escribir `backend/tests/GT.IntegrationTests/Caja/ConsultaMovimientosTests.cs`, con el ayudante de inserción directa de T018: sin filtros trae todos; con movimientos del 14/09, 15/09 y 16/09 de 2026 y el rango 15/09–16/09 trae los dos últimos y no el primero (US4 esc. 2); con sólo *desde* o sólo *hasta* aplica un extremo; **un movimiento del 16/09 a las 23:30 de Argentina entra con *hasta* = 16/09 y uno del 15/09 a las 00:30 de Argentina entra con *desde* = 15/09** (decisión 4); `desde > hasta` responde `400 rango_invalido` (US4 esc. 3); por `cajaId` trae sólo esa caja (US4 esc. 5); rango y caja combinados; un período sin movimientos devuelve `total` `0` sin fallar; orden `Fecha DESC, Id DESC` sin repetidos ni faltantes entre páginas; cada fila con las seis columnas (CA5)

### Implementación de User Story 4

- [X] T076 [US4] Agregar `ConsultarMovimientosAsync(filtros)` a `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs`: los filtros de `CajaId` y del rango —convertido a instantes UTC según la decisión 4— **en el árbol, antes de contar y paginar** (research §8), orden total y la proyección compartida de T054
- [X] T077 [P] [US4] Implementar `backend/src/GT.Application/Caja/ConsultarMovimientos.cs`: rechaza `desde > hasta` con `rango_invalido` sin consultar (FR-023), convierte los días de Argentina a instantes UTC con un desplazamiento fijo de −3 como `FechaHoyArgentina`, y devuelve la página
- [X] T078 [US4] Agregar `GET /api/movimientos-caja` (consultar; `desde` y `hasta` como `DateOnly?`, `cajaId` y `pagina` anulables) a `backend/src/GT.Api/Caja/MovimientosDeCajaEndpoints.cs`; registrar el caso de uso en `backend/src/GT.Api/Program.cs`; sumarlo a `backend/tests/GT.IntegrationTests/Caja/PermisosCajaTests.cs` —*Gerencia* `200`—
- [X] T079 [US4] Agregar `listarMovimientos(filtros, pagina)` y `FILTROS_MOVIMIENTOS_INICIALES` a `frontend/src/modules/caja/servicios/servicioCaja.ts`
- [X] T080 [P] [US4] Implementar `frontend/src/modules/caja/componentes/FiltrosDeMovimientos.tsx` sobre `compartido/ui/Filtros`: *Desde*, *Hasta* y *Caja* —desplegable compacto con `Todas las cajas` y cada caja como `Responsable · dd/mm/aaaa`, cargado con `listarCajas`—, con el borde más marcado en el filtro que tiene valor; el rango invertido marca los dos campos con `aria-invalid` y muestra `La fecha desde es posterior a la hasta.` debajo de *Hasta*
- [X] T081 [US4] Implementar `frontend/src/modules/caja/paginas/ConsultaDeMovimientos.tsx`: título `Movimientos de caja`; filtros de T080 con la declaración de lo aplicado en `role="status"` (FR-027, FR-030); con el rango invertido **no consulta**; tabla con fecha, tipo en palabra, importe, concepto, responsable y referencia (CA5); sin resultados, `No existen movimientos para los filtros aplicados.` en lugar de la tabla, sea el filtro por rango, por caja o los dos (CA9, CL2, FR-025); `Paginacion` y cualquier cambio de filtro vuelve a la página 1; admite `?cajaId=` en la dirección para llegar filtrado desde una caja; ninguna acción de escritura (pantalla de sólo lectura); estados de carga, error y **sin permiso** por `403`
- [X] T082 [US4] Sumar a `frontend/src/modules/caja/paginas/DetalleDeCaja.tsx` un enlace terciario *Ver en movimientos de caja* a `/movimientos-caja?cajaId=:id`, y registrar en `frontend/src/App.tsx` la ruta `/movimientos-caja`
- [X] T083 [P] [US4] Escribir `frontend/src/modules/caja/paginas/ConsultaDeMovimientos.test.tsx`: las seis columnas con y sin referencia; declaración del filtro aplicado; el rango invertido marca los campos y **no llama** a `listarMovimientos`; un período vacío muestra el mensaje y no una tabla vacía; `?cajaId=` llega filtrado; ningún botón de abrir, registrar ni cerrar; un `403` muestra el aviso sin permiso y no el error de carga

**Checkpoint**: las cinco historias funcionan de punta a punta; el gerente revisa cajas y movimientos sin operar.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: la suite entera, el recorrido manual y el mantenimiento.

- [X] T084 [P] Llevar a `specs/011-gestion-caja/contracts/README.md` las cinco decisiones de la sección *Decisiones que estas tareas fijan*: el código `caja_ajena` con su texto, los campos nuevos de `CajaDetalle`, las dos entradas de menú, el corte del rango en días de Argentina y la pastilla de estado
- [X] T085 Revisar en `frontend/src/App.tsx` que las seis rutas usen la misma protección de sesión que el resto —sin sesión redirige a `/ingresar` (FR-034)— y dejar anotado en el comentario del bloque el reparto consultar/gestionar de cada una, como el del Módulo 10
- [X] T086 Correr `cd backend && dotnet test` y `cd frontend && npm test`, más el build y el lint del frontend, y dejar todo en verde; confirmar que `AsignarRolesTests` sólo cambió lo de T017, que `seccionesDeMenu.test.ts` no se tocó, y que la migración no altera tablas de otros módulos
- [ ] T087 Recorrer los 19 pasos de `specs/011-gestion-caja/quickstart.md` con las cuentas `admin`, `admin.empresa`, una segunda cuenta de *Administración de la empresa*, `gerencia` y `trafico`, incluido el paso 12 de dos pestañas, y anotar lo que el recorrido encuentre
- [X] T088 [P] Agregar la fila del Módulo 11 con su estado y conteo de tareas, y su entrada en *Qué queda abierto* —el responsable dado de baja con la caja abierta, que nadie puede cerrar (spec §Assumptions)— y en *Lo que cada módulo dejó como precedente*, en `specs/README.md`
- [X] T089 Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (`[011] ...`), en *Decisiones transversales ya tomadas*. Partir de las candidatas de `plan.md` §Mantenimiento al cerrar la feature —y evaluar también el corte de un rango de días locales sobre instantes UTC (decisión 4)— y **no incluir entradas por incluir**: sólo las que sean información transversal y relevante para futuras features

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias. T003 va antes de T032
- **Foundational (Phase 2)**: depende de Setup — **bloquea todas las historias**
- **User Stories (Phase 3 a 7)**: dependen de Foundational
- **Polish (Phase 8)**: depende de todas las historias

### Dentro de Foundational

- T013 depende de T011 y T012; T014 de T013; T019 de T014 y T018
- T024 depende de T022 y T023; T025 de T024; T026 de T025; T027 de T026; T028 de T023
- T032 depende de T003

### User Story Dependencies

- **US1 — Abrir (P1)**: sólo Foundational. Incluye la consulta de cajas (`/caja`), que es la entrada del módulo y donde vive *Abrir caja*, y la primera versión de la pantalla de la caja, que es a donde lleva la apertura
- **US5 — Acceso (P1)**: necesita los endpoints de US1 (T040); su test suma los de cada historia a medida que existen
- **US2 — Movimientos (P1)**: necesita US1 —la caja abierta y su pantalla—. Sus tests de backend arman cajas con el ayudante de T018
- **US3 — Cierre (P1)**: necesita US2 —el `UPDATE` que toma el lock y la proyección compartida de T054— y la pantalla de la caja
- **US4 — Consulta (P2)**: necesita la proyección de T054; en backend puede avanzar en paralelo con US3

### Archivos que tocan varias historias

No llevan `[P]` entre sí y se hacen en el orden de las fases:

- `backend/src/GT.Infrastructure/Persistencia/RepositorioCaja.cs` — T025, T037, T054, T066, T076
- `backend/src/GT.Api/Program.cs` — T027, T040, T057, T069, T078
- `backend/src/GT.Api/Caja/CajaEndpoints.cs` — T040, T057
- `backend/src/GT.Api/Caja/MovimientosDeCajaEndpoints.cs` — T057, T078
- `backend/tests/GT.IntegrationTests/Caja/PermisosCajaTests.cs` — T049, T057, T069, T078
- `backend/tests/GT.IntegrationTests/Caja/RutasCajaTests.cs` — T035, T057
- `frontend/src/modules/caja/servicios/servicioCaja.ts` — T032, T041, T058, T070, T079
- `frontend/src/App.tsx` — T045, T061, T072, T082, T085
- `frontend/src/modules/caja/paginas/DetalleDeCaja.tsx` — T044, T060, T072, T082
- `frontend/src/modules/caja/paginas/DetalleDeCaja.test.tsx` — T048, T063, T074

### Within Each User Story

- Los tests se escriben primero y tienen que fallar antes de implementar
- Repositorio → caso de uso → endpoint → servicio del frontend → página → ruta

### Parallel Opportunities

- Setup: T001 y T002
- Foundational: el dominio T004–T009, el test T010, las configuraciones T011–T012, las piezas de aplicación T020–T023, el ayudante T018 y el frontend T029–T031
- Dentro de cada historia: todos sus tests `[P]`, y los casos de uso que no comparten archivo (T055 y T056; T067 y T068)
- Con US2 terminada, US3 y el backend de US4 pueden avanzar a la vez

---

## Parallel Example: User Story 2

```bash
# Los tres tests de backend de la historia, juntos:
Task: "MovimientosTests en backend/tests/GT.IntegrationTests/Caja/MovimientosTests.cs"
Task: "ReferenciasTests en backend/tests/GT.IntegrationTests/Caja/ReferenciasTests.cs"
Task: "MovimientosConcurrentesTests en backend/tests/GT.IntegrationTests/Caja/MovimientosConcurrentesTests.cs"

# Con T054 terminada, los casos de uso juntos:
Task: "RegistrarMovimiento en backend/src/GT.Application/Caja/RegistrarMovimiento.cs"
Task: "ConsultarFacturasPendientes y ConsultarOrdenesDePago en backend/src/GT.Application/Caja/"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 y Phase 2
2. Phase 3 (US1): abrir la caja, verla, y el rechazo de la segunda
3. **Parar y validar**: pasos 1 a 4 de `quickstart.md`

### Incremental Delivery

1. Setup + Foundational → base lista
2. US1 → abrir (MVP)
3. US5 → acceso, que protege lo construido antes de sumar escrituras
4. US2 → registrar movimientos
5. US3 → cerrar: con esto el ciclo del día está completo, que es lo que el negocio necesita para usar el módulo
6. US4 → consulta para el gerente

---

## Notes

- `[P]` = archivo distinto, sin dependencias pendientes
- Ningún archivo de negocio de los Módulos 6 o 9 se modifica: facturas y órdenes de pago sólo se leen (FR-012)
- Los importes van en `decimal` en el backend y se muestran con `compartido/moneda` (convención [005]); las fechas con `compartido/fechas` (convención [003])
- Commit por tarea o por grupo lógico, sin trailer de coautoría
