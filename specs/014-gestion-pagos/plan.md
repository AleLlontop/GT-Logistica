# Plan de implementación — 014 Gestión de pagos

**Feature**: `014-gestion-pagos` | **Fecha**: 2026-10-18 | **Spec autoritativa**: [spec.md](spec.md)

Estado: diseño técnico listo para revisión previa a `/speckit.tasks`, con riesgo documental UI explicitado. No es implementación ni evidencia de pruebas ejecutadas. AC-01–AC-14 permanecen cerradas; spec sin modificaciones.

## Resumen

Añadir módulo Pagos con OPP independiente, bancos, medios y ciclo explícito de cheque; confirmar/anular como transacciones SQL Server únicas con resultado idempotente. Reutilizar primitivas reales de 009 y 011 sin invocar sus commits desde otro caso de uso. Un vínculo vigente por factura con índice filtrado conserva historia de órdenes anuladas. Montos exactos por cadena JSON/BigInt siguiendo 013. Extensiones cerradas sobre módulos previos enumeradas abajo.

## Contexto técnico comprobado

C#/.NET 10, SDK solicitado por global.json 10.0.302; EF Core SQL Server 10.0.10. ASP.NET Core minimal endpoints, GtDbContext compartido scoped, TimeProvider y FechaHoyArgentina.Desde. Frontend React 19.2.8, Router 7.18.2, TypeScript 6.0.2, Tailwind 4, Radix Dialog y Node 24 según manifiesto. xUnit/WebApplicationFactory, SQL Server real compose; Vitest 4/Testing Library. Sin nuevas bibliotecas ni servicios. SQL Server 2022, desarrollo Linux; compose común Docker/Podman. Alcance cinco recorridos US1–US5, 45 FR; sin objetivo de latencia nuevo inventado. Consultas paginadas 20 filas y sin traer padrones completos.

Artefactos: [research](research.md), [modelo/transacciones](data-model.md), [contratos](contracts/README.md), [validación](quickstart.md). Este comando no crea tasks.md. Contexto local activo corregido: `.specify/feature.json` contiene `specs/014-gestion-pagos`. El resolver compartido de Spec Kit obtiene de allí spec, plan, research, modelo, contratos, quickstart y destino tasks de 014; no se ejecutó `/speckit.tasks`. Verificación estática y límites del entorno documentados en research §4.

## Constitution Check, antes y después del diseño

| Principio | Resultado del diseño |
|---|---|
| Simplicidad | Sin infraestructura distribuida, TPH para medios, una transacción y locks SQL existentes; resultado idempotente necesario por AC-05/09 |
| Español/ARS | Textos argentinos; importes exactos, campos/formateadores existentes |
| Cero alcance fantasma | Cada decisión mapeada a FR/AC; sin conciliación, PDF nuevo, clientes ni impuestos; longitudes técnicas de 200 ratificadas por el usuario |
| Aceptación no técnica | US/SC conservados; quickstart manual independiente de SQL para negocio |
| Datos con respeto | Sin secretos nuevos ni adjuntos; replay acotado a usuario autorizado y operación |
| Diseño gobernado | Tablas UI completas abajo sobre tokens/primitivas reales. Falta fuente documental local gt-ui: riesgo R1; cotejo documental pendiente antes de aceptar UI, no se afirma cumplimiento documental pleno |

No se usa Sites: se extiende la app React existente en el repositorio. No se aplica skill de generación de sitios.

## Estructura prevista

- `backend/src/GT.Domain/Pagos/`: OrdenPagoProveedor, vinculo factura, MedioPagoProveedor (Cheque/Transferencia/Efectivo TPH), Banco, CambioEstadoCheque, ResultadoOperacionPago, reglas puras/estados/números.
- `backend/src/GT.Application/Pagos/`: casos de uso Generar/Anular/consultas/Bancos/estado cheque; DTOs, errores y `IRepositorioPagos` transaccional; proyección común de elegibilidad.
- `backend/src/GT.Infrastructure/Persistencia/`: RepositorioPagos, configuraciones y migración incremental, primitivas de escritura compartidas de Caja/Liquidaciones dentro de transacción caller-owned. Nada de repositorio genérico o nueva capa.
- `backend/src/GT.Api/Pagos/`: endpoints, validación/binding, respuestas exactas locales. Registro de DI/políticas/rutas en arranque existente.
- `frontend/src/modules/pagos/{paginas,componentes,servicios}/`: GenerarOrden, ConsultarOrdenes, DetalleOrden, ChequesPorVencer, DetalleCheque, Bancos, FormularioBanco, DetalleBanco; anulación/cambio estado en diálogos dentro de detalles. Tipos de cadenas monetarias y cliente HTTP actual.
- `backend/tests/{GT.UnitTests,GT.IntegrationTests}/Pagos/` y tests de regresión nuevos en 009/011/013; frontend tests junto a pantallas.

## Decisiones centrales y contraste con spec

| Decisión | Contrato funcional satisfecho | Validación clave |
|---|---|---|
| OPP/OP separadas, secuencia OPP nueva | FR-006/041/042, AC-11 | OP-25 y OPP-25 distintos, FK equivocada no válida |
| Tabla vínculo con Vigente e índice único filtrado | FR-026/027, AC-04/09 | dos órdenes simultáneas, anular y nueva intención sin perder historia |
| Medios TPH + CHECKs y cheque BancoId/número Latin1_General_100_BIN2 | FR-003/008/012, AC-03/07/14 | campos cruzados/nulos, letras/espacios, duplicado concurrente |
| Resultado JSON inmutable por TipoOperacion + ClaveIdempotencia, usuario sólo auditoría/autorización, applock global por operación/clave | FR-028/040, AC-05/06/09 | replay tras commit incierto/anulación sin doble pago |
| Orden global de locks: idempotencia → OPP → bancos → facturas → liquidaciones → OP 009 → cheques → caja; Id ascendente por tipo | FR-019/033/040 | conflicto entre transición y anulación, no doble historial |
| Lock Caja existente antes de suma | FR-010/011/019 | único egreso, saldo concurrente y cierre, compensación única |
| OP con origen y vigencia, eventos nuevos sin tocar valores legacy | FR-025/039/041 | pago/reversión/nuevo pago, manual histórico intacto |
| OP automática FechaPago=hoy de Generar | FR-006/025 y regla vigente 009, AC-14 | OPP retroactiva no altera fechas de instrumentos/caja y respeta rango 009 |
| Proveedor inactivo permitido; snapshot tipo factura | FR-002/045, AC-13 | baja durante armado no rechaza; liquidez/eligibilidad sí se revalidan |
| Siete políticas, lecturas mínimas propias de Pagos | FR-001/020/043/044 | permiso aislado, sesión revalidada, no acceso lateral a PDF/módulos |
| Cadenas exactas y centavos BigInt | FR-017/018 | máximo decimal y diferencias de un centavo sin Number |


Longitudes técnicas ratificadas: Banco.Nombre y Cheque.NumeroCheque máximo 200 después de trim exterior; servidor valida, persistencia limita y frontend muestra error de campo sin truncar. D2 reemplaza el riesgo abierto R2. Idempotencia y orden global detallados en research §§2–3 y aplicados en data-model/contratos.

### Cobertura completa de requisitos

| FR | Artefacto/recorrido que concreta el requisito | Prueba prevista |
|---|---|---|
| 001,020,043,044 | Contratos: siete permisos, referencias mínimas, menú/semillas | Matriz SQL/API por permiso aislado y rol; sesión revalidada |
| 002,003,004,005,024,025,026,027 | Modelo: Generar, vínculo vigente, elegibilidad/medios; US1 | Positivos/negativos, mismo proveedor, centavo, carrera y rollback |
| 006,007 | OPP/secuencia/Fecha/detalle, estados Pendiente/Anulada | Defaults Argentina, retroactividad, OP vs OPP, datos inmutables |
| 008,009,010,011,012 | Modelo medios y Caja; contratos unión estricta | Cheque/CBU/efectivo, sin huérfanos, único egreso |
| 013,014,015,016 | GP_05/06, DTOs y filtros SQL | Orden total/páginas, límites temporales, estados finales |
| 017,018 | Converter localizado y BigInt | decimal máximo, overflow y escala sin Number ni IVA nuevo |
| 019,028,029 | Transacción exterior e idempotencia Generar | Fallos por punto de escritura, replay concurrente/commit incierto |
| 021,022,045 | Snapshots/trazabilidad y tablas UI | Proveedor Inactivo, historial, teclado/contraste, storage vacío |
| 023,037,038,039,040 | Confirmación UI, conflictos 409 y transacción Anular | Confirmación/motivo, compensación, transferencias, caja cerrada |
| 030,031,032 | Bancos/índice/recursos estado | Duplicados, baja concurrente, historial y permisos |
| 033,034,035,036 | Cheque/lock padre/historial/estado derivado | Todas las aristas, no reversión por rechazo individual |
| 041,042 | OP con origen y vigencia; FK OPP en Caja | Ciclos pago/reversión/pago, referencias tipadas, legacy intacto |

## Extensiones acotadas de módulos previos (inventario obligatorio)

1. **013**: EstadoFacturaProveedor Pagada y proyección/filtro real hoy hardcodeados; restauración por anulación. Nuevas consultas de elegibilidad viven en Pagos. No cambiar carga, reglas fiscales, PDFs, identidad/estado proveedor ni endpoints de permisos existentes. Datos históricos factura alimentan OPP, más snapshot activo y dato actual separado.
2. **009 escritura**: extraer primitivas sin Begin/Commit que usa Pagos y que el repositorio histórico puede llamar dentro de su transacción. Mantener condiciones y errores del flujo manual. Añadir origen y vigencia/reversión sólo para OP de OPP, ajustar agregado/sumas y conteo de bloqueos a pagos vigentes, sin permitir revertir pagos manuales. Recalcular Estado coherente con CHECK.
3. **009 historia/lectura**: eventos PagoPorOPP/ReversionPagoPorOPP por OP y filtro de índice histórico compatible; mostrar origen/revertido sin borrar historia. Sin edición OP histórica general. Nuevas proyecciones no cambian moneda numérica legacy.
4. **011 escritura**: primitiva lock de caja reutilizable en transacción de Pagos. FK OPP nueva y CHECK separado; índices por OPP/tipo. No tocar FK OP ni admitir campo OPP en movimientos manuales. No impedir saldo negativo del recorrido manual existente.
5. **011 lectura**: referencia OPP y concepto exacto en detalle/listado/resumen que ya proyectan movimientos. Un egreso y un eventual ingreso, no borrar ni reescribir originales. Acceso general Caja conserva sus permisos.
6. **Infraestructura común**: siete códigos/políticas y semillas explícitas para roles reales Administración/AdministradorSistema; cuatro pares catálogo permiso/pantalla. `pagos.anular` no implica consulta. Menú frontend agrupa códigos nuevos preservando sección de desconocidos. Añadir rutas App y resultado OPP en memoria por sesión; parser monetario exacto reutilizable sólo si se extrae con pruebas de 013 intactas.

## UI Design Check

Fuente disponible: `frontend/src/index.css`, `compartido/ui/`, pantallas 013/009/011. Referencias `.claude/skills/gt-ui` ausentes, R1. No cambiar tokens; ink/ink-soft sobre canvas; muted sólo superficies permitidas; 4,5:1 texto en fondo real, anillo y errores accesibles. `Button` variante obligatoria, `<select>` nativo, `InputImporte`, diálogos existentes con foco al contenedor, Escape y retorno al disparador. Nombres sr-only completos; filas navegables con enlace sin tabIndex en tr; acciones siempre accesibles. Alert/status en párrafo, no tarjeta. Ninguna copia de formularios/secretos en storage/history.

### Acción primaria por pantalla

| Ruta/pantalla | Primaria | Secundarias/terciarias | Destructiva |
|---|---|---|---|
| `/pagos/nueva` | Generar | Cancelar, agregar/editar/quitar medios | — |
| `/pagos/ordenes` | ninguna | filtros, limpiar, páginas, enlaces | — |
| `/pagos/ordenes/:id` | ninguna | volver, enlaces autorizados | Anular si permiso y elegible |
| Diálogo anulación | ninguna | Cancelar | Confirmar anulación |
| `/pagos/cheques` | ninguna | filtros, limpiar, páginas | — |
| `/pagos/cheques/:id` | ninguna | Cambiar estado si permiso, volver, OPP autorizada | — |
| Diálogo cambio estado | Guardar cambio | Cancelar | — |
| `/pagos/bancos` | Nuevo banco si gestionar | páginas, abrir | Desactivar en ficha |
| `/pagos/bancos/nuevo`, `/:id/editar` | Guardar | Cancelar | — |
| `/pagos/bancos/:id` | ninguna | Editar, Activar, volver | Desactivar con confirmación |
| Editor temporal de medio | Agregar/Guardar medio | Cancelar | Quitar renglón como acción local |

### Estados de campo

Cada grupo enumera todos sus campos; comparten exactamente estados de la fila. Error sólo tras blur tocado o submit, nunca primer render. `aria-invalid`/`aria-describedby`; foco con anillo brand visible; leyenda obligatoria desde :required. Fechas formateadas con utilidades compartidas, no parseo UTC del día.

| Pantalla | Campos | Reposo | Foco | Error | Vacío |
|---|---|---|---|---|---|
| Generar | Proveedor autocomplete | razón/CUIT y marca Inactivo, ancho de texto | combobox teclado flechas/Enter/Escape, anillo | elegir coincidencia elegible | buscar proveedor |
| Generar | Fecha | date nativo default hoy Argentina | anillo y teclado nativo | requerida/futura | requerida, no inventar fecha al enviar |
| Generar | Detalle | textarea opcional | anillo | sólo error servidor si lo hubiera | válido, null tras trim |
| Generar | selección de facturas | checkbox por factura en tabla paginada, selección persiste entre páginas | foco nativo | al menos una, no disponibles identificadas | total cero informativo y error al Generar |
| Editor medio | Tipo | select sólo cheques para Fletero | anillo | tipo no admitido | elegir medio |
| Cheque | Banco | select bancos activos | anillo | obligatorio/no disponible | elegir banco, vacío informa cargar banco sólo si autorizado |
| Cheque | NumeroCheque | input texto, conserva contenido interno | anillo | obligatorio/duplicado/>200 después del trim exterior | requerido |
| Cheque | Emisión, vencimiento | dates nativos | anillo | emisión futura, vencimiento anterior/igual Fletero | requeridos |
| Todos medios | Importe | InputImporte AR, string | anillo | positivo/escala/rango; suma global exacta | requerido |
| Cheque | Detalle | textarea opcional | anillo | error servidor vinculado | válido |
| Transferencia | Fecha | date nativo | anillo | requerida/futura | requerida |
| Transferencia | CBU origen, CBU destino | texto inputMode numeric sin parsing | anillo | exactamente 22 dígitos | requeridos |
| Transferencia | Número comprobante | input texto | anillo | obligatorio | requerido |
| Transferencia | Observaciones | textarea | anillo | error servidor vinculado | válido |
| Efectivo | Fecha | texto de hoy servidor, sólo lectura | no control adicional | actualizada al Generar | no editable |
| GP_05 | Desde, hasta, proveedor autocomplete, estado | dates/select/autocomplete | anillo | rango invertido/dato inválido | sin filtro |
| GP_06 | Banco, estado, desde/hasta vencimiento | selects/date, banco histórico inactivo visible | anillo | rango/código inválido | sin filtro, alerta base siempre explícita |
| Cambio cheque | Estado nuevo | select con aristas, previo visible | anillo | estado cambió/ya final, refrescar | requerido |
| Anular | Motivo | textarea | anillo | obligatorio tras trim | requerido |
| Banco alta/edición | Nombre | input texto | anillo | obligatorio/duplicado/>200 después del trim exterior/versión | requerido |

El formulario captura fecha default con reloj servido al abrir; no DateTime.Now disperso. Generar se envía al revisar y ejecutar explícitamente la acción; Anular se envía tras ingresar motivo y confirmar el diálogo. La petición intencional incluye confirmado=true, validado en servidor como entrada (400 si falta/es false), sin handshake de confirmación mediante 409. HTTP 409 corresponde exclusivamente a conflictos reales de estado/concurrencia; edición de medio sólo temporal. Ante red incierta conservar payload/clave, bloquear nueva intención involuntaria; mostrar reintentar misma confirmación. Exitoso navega a resultado en memoria; refresh sin consultar devuelve 403. Cheque fuera del universo de alerta sigue accesible por detalle desde OPP, sin ampliar listado.

## Fases de entrega planificadas (sin tasks)

1. **Fundamentos y base**: reglas puras, DTOs/errores, entidades/configuraciones/índices/CHECK, migración incremental; permisos/semillas y consultas mínimas. Verificar SQL real upgrade legacy y constraints, conversión monetaria, normalización y fechas.
2. **GP_01 y Bancos**: ABM, elegibilidad SQL y guardado; primitivas transaccionales 009/011; Generar idempotente, contratos y frontend de armado completo. Demostrar selección misma factura y saldo concurrentes, duplicado cheque/banco, rollback en cada límite. No habilitar menú de pantallas aún inexistentes.
3. **Consultas y cheques**: GP_05/06, trazabilidad en 009/011/013, historial y transiciones con lock padre; frontend y filtros, accesos aislados. Probar estados vs situación y límites de día.
4. **Anulación y cierre**: transacción de compensación, idempotencia independiente, historial/vigencia de OP, reversión y nueva intención, frontend de confirmación. Regresión completa, aceptación manual, revisión de permisos y DI, coherencia de sumas y migración. No cambiar aserciones antiguas para ocultar alteraciones; nuevas aserciones sólo cuando el cambio esté enumerado en esta spec y explícitamente justificado.
5. **Último paso de la fase final — mantenimiento**: Actualizar `AGENTS.md` con las decisiones de diseño y convenciones nuevas de esta feature, una línea por decisión, con referencia a la spec (p. ej. “[014] ...”). No incluir entradas por incluir: sólo información transversal relevante que puedan aprovechar futuras features. Este paso se ejecutará en implementación/cierre, no en este comando de planificación.

## Riesgos reales y puertas de validación

- **R1**: falta skill/referencias UI; no bloquear el diseño de datos por ello, pero no aprobar el cotejo documental UI hasta recuperar fuentes. Tablas de UI preparadas, no nuevas reglas de negocio.
- **D2 — decisión ratificada, no riesgo abierto**: Banco.Nombre y Cheque.NumeroCheque admiten hasta 200 caracteres después de trim exterior. Validación servidor y error de campo frontend; `nvarchar(200)` para ambos campos. NumeroCheque usa `Latin1_General_100_BIN2` e índice único BancoId+número; Banco.Nombre hereda la comparación textual de la base, como los nombres de TiposVehiculo, con índice único incluidos inactivos. Sin truncado, parsing ni otras equivalencias. Probar bordes 200/201 normalizados y Unicode según research; la comprobación SQL es validación de implementación.
- **R3**: CHECK de sumas intertabla no existe; usar transacción y test SQL. Única garantía estructural pedida de unicidad efectiva es índice filtrado. No afirmar que un booleano solo ata estado de OPP y vínculo ante SQL arbitrario.
- **R4**: rollback/reintento tras commit incierto debe consultar clave bloqueante; no devolver 409 de factura pagada antes de buscar replay. Sin este orden idempotencia no funciona.
- **R5**: reversión OP altera modelo/historial 009 hoy inmutable y su índice; migración debe preservar pagos manuales. Probar `pagar → anular OPP → generar OPP → pagar` repetido y operación manual posterior sin bloqueo por historia revertida.
- **R6**: módulos actuales no permiten transacción anidada segura para esta coordinación; nunca componer casos de uso con commits propios. Refactor mínimo de primitivas con suites legacy antes/después.
- **R7**: spec autoriza compensación con caja original abierta aunque anulador no sea responsable. No reutilizar sin ajuste el método privado que exige usuario responsable; nueva operación interna específica autorizada por pagos.anular sin modificar la manual. Caja abierta sigue condición innegociable.

Revisión final: matriz anterior y contratos cubren FR-001–FR-045 y AC-01–AC-14 sin reabrir negocio. Las limitaciones técnicas están aquí, no se cambió spec. Tests y aceptación previstos en quickstart; no se declara ninguno ejecutado durante planificación.
