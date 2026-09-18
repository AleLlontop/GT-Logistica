# Research: Gestión de caja (Módulo 11)

Decisiones técnicas con su alternativa descartada. El detalle de tablas y transacciones está en
[data-model.md](./data-model.md); acá sólo el porqué.

## §1. La exclusividad de RN1 es un índice único filtrado, no una columna en `Usuario`

**Decisión**: `IX_Cajas_UsuarioResponsable_Abierta UNIQUE (UsuarioResponsableId) WHERE Estado = 0`.

**Rationale**: es la misma convención [005] que ya usa la unidad de un viaje del Módulo 5 y el número de
factura del Módulo 6: la exclusividad vive en la tabla que ya tiene el estado, no en una tabla de
"ocupaciones" aparte que puede desincronizarse. La consulta previa (`ExisteCajaAbiertaAsync`) da el
mensaje bueno de CA2; el índice cierra la carrera de CL3 (doble clic) sin que la aplicación tenga que
saberlo.

**Alternativa descartada**: una columna `CajaAbiertaId` en `Usuario`. Descartada: duplicaría un dato que
ya existe en `Cajas` y exigiría tocar el Módulo 2 para agregar una columna que sólo usa este módulo.

## §2. La carrera del cierre y la del movimiento simultáneo se cierran con la misma fila como candado

**Decisión**: tanto `CerrarAsync` como `RegistrarMovimientoAsync` **empiezan** su transacción con un
`ExecuteUpdateAsync` sobre `Cajas` que se auto-asigna (`SetProperty(c => c.Estado, c => c.Estado)`) con
`WHERE Id = @id AND Estado = Abierta`. Es un `UPDATE` que no cambia ningún valor, pero **toma el mismo
lock de fila** que toma cualquier `UPDATE` real (research de convención [009], repetido acá porque el
candado protege una operación distinta: un `INSERT` en la tabla hija, no una segunda escritura sobre la
misma fila).

Con eso:

- Dos cierres simultáneos: el segundo espera a que el primero libere el lock: al desbloquearse, su propio
  `WHERE Estado = Abierta` ya no matchea (el primero la dejó `Cerrada`) → `afectadas != 1` → se relee y
  se informa "ya está cerrada" (FR-021).
- Un movimiento y un cierre simultáneos sobre la misma caja: cualquiera de los dos que tome el lock
  primero determina el orden real. Si el movimiento entra primero, su transacción libera el lock **después**
  de insertar y confirmar; cuando el cierre toma el lock, su relectura de movimientos —que ocurre **después**
  de tomar el lock, dentro de la misma transacción— ya ve el movimiento nuevo. Si el cierre entra primero,
  el movimiento se bloquea, y al desbloquearse encuentra `Estado = Cerrada` → se rechaza (RN2, FR-019).
  Nunca queda un movimiento fuera del resumen de una caja cerrada, que es exactamente lo que pide el
  edge case de la spec.

**Alternativa descartada**: una columna `Version` en `Cajas`, como en el Módulo 9. Descartada: la
condición del Módulo 9 hacía falta porque dos *ediciones* podían ver el mismo estado y dejarlo igual (dos
`Pendiente` no se distinguen entre sí). Acá cada operación sobre la caja cambia su estado o dispara una
inserción en otra tabla; no hay dos escrituras que dejen la fila en el mismo estado sin que el `WHERE`
ya las distinga. Es la convención [010] en su forma más simple, con una vuelta nueva: el candado no
protege una escritura sobre la fila misma, protege un `INSERT` en la tabla hija — de ahí el `UPDATE`
que no cambia nada.

## §3. La confirmación del cierre viaja con el número que confirma, no sólo con `confirmado: true`

**Decisión**: `POST /api/caja/{id}/cierre` recibe `{ confirmado: true, saldoFinalConfirmado: decimal }`.
El backend recalcula el saldo final **dentro** de la transacción (después de tomar el lock de §2) y lo
compara contra `saldoFinalConfirmado`. Si `confirmado` no vino, responde `409 confirmacion_requerida`;
si vino y el saldo no coincide, `409 cierre_desactualizado`. Los dos llevan el resumen actual (saldo
inicial, movimientos, saldo final) en el cuerpo y ninguno cierra nada. Son dos códigos porque son dos
causas distintas —nadie confirmó, o se confirmó otro número—, aunque la pantalla las trate igual.

**Rationale**: en el patrón de `RegistrarOrdenDePago` del Módulo 9 (research de ese módulo, §7), lo que
se confirma es un número que **el usuario eligió** (el importe) contra un tope que el servidor calculó.
Acá no hay ningún dato que el usuario elija: todo el resumen es calculado por el servidor, así que
`confirmado: true` a secas no alcanza para saber **qué** resumen se está confirmando. El escenario 7 de
US3 ("un movimiento nuevo entre el resumen y la confirmación") exige distinguir "confirmo este saldo" de
"confirmo cualquier saldo que haya ahora", y sólo la primera lectura cumple la spec. Devolver el mismo
resumen que ya se le mostró es la forma de que la pantalla lo muestre de nuevo sin otra llamada.

**Alternativa descartada**: `confirmado: true` sin más, recalculando siempre y cerrando con el número que
haya en ese momento. Descartada explícitamente: FR-018 dice que un movimiento nuevo entre medio **no
debe** cerrar con un saldo que la pantalla no vio, tiene que **pedir confirmar de nuevo** — cerrar
directamente con el número recalculado sería exactamente lo que la spec prohíbe.

## §4. La referencia del egreso a `OrdenDePago` no lleva ningún filtro de "pendiente"

**Decisión**: `MovimientoDeCaja.OrdenDePagoId` es un `FK` opcional simple a `OrdenesDePago.Id`, sin
ninguna condición de estado. El desplegable de egresos ofrece las **50 órdenes de pago más recientes**
(número, liquidación e importe), ordenadas por número descendente, con la línea *"Se muestran las 50
órdenes de pago más recientes."* debajo del campo.

**Por qué un tope**: a diferencia de la factura, la orden no tiene un estado que acote el conjunto, así
que el desplegable crecería sin límite con cada pago de liquidación —el mismo argumento de §5 para las
facturas—. Las recientes son las que un egreso de caja del día referencia en la práctica. **Por qué el
aviso**: un tope sin decirlo es un listado que oculta filas en silencio (convención [003]); la línea
hace visible el corte. El guardado **no** aplica el tope: una orden fuera de las 50 que llega invocando
la acción directamente se acepta si existe.

**Rationale**: `GT.Domain.Liquidaciones.OrdenDePago` (Módulo 9) es un registro de un pago **ya
ejecutado** —se crea recién al registrar el pago, en la misma operación que reduce la deuda de la
liquidación (`RegistrarPagoAsync`)— y no tiene columna de estado, ni `Activo`, ni ninguna otra bandera:
nunca se edita ni se borra (FR-044 del Módulo 9). No existe ningún significado de "pendiente" para esa
tabla hoy. La spec de este módulo usa la palabra "pendiente" reutilizando el vocabulario de la factura
(que sí tiene ese estado), pero aplicada a `OrdenDePago` no hay nada que filtrar: **se decidió con el
usuario** que la entidad se trata tal cual existe hoy, sin inventarle un estado que el Módulo 9 no
define, porque "va a estar más completa en otro módulo" (fuera del alcance de este).

Con la factura (Módulo 6) sí hay un estado real que filtrar: `EstadoFactura.Pendiente` es "pendiente de
cobro" tal cual lo nombra la spec, así que `MovimientoDeCaja.FacturaId` sí valida
`Estado == EstadoFactura.Pendiente` al guardar (FR-011) y el desplegable de ingresos sólo ofrece esas.

**Alternativa descartada**: inventar un criterio de "pendiente" para `OrdenDePago` —por ejemplo, excluir
las de una liquidación después anulada—. Descartada por Principio III (cero alcance fantasma): la spec
de Caja no define ese criterio, y el Módulo 9 no lo tiene; inventarlo acá sería una regla de negocio que
no está escrita en ninguna spec.

## §5. El desplegable de facturas pendientes va a SQL, no en memoria

**Decisión**: `ConsultarFacturasPendientes` filtra `Estado == EstadoFactura.Pendiente` en el árbol de la
consulta (convención [003]), no en memoria.

**Rationale**: a diferencia del desplegable de beneficiarios del Módulo 10 —decenas de personas, sin
paginar—, las facturas de G&T pueden acumularse por años de operación; filtrar en memoria traería toda la
tabla al proceso cada vez que se abre el formulario de un ingreso. Acá no hay ninguna regla de
elegibilidad que también tenga que dar un motivo de rechazo al guardar (como sí pasaba con
`ElegibilidadDeBeneficiario`): `Estado == Pendiente` es la única condición, se escribe una vez en el
predicado de la consulta, y el guardado la reevalúa con la misma expresión (research §1 del Módulo 10,
pero acá aplicada al lado donde sí corresponde ir a SQL).

## §6. `EstadoCaja` y `TipoMovimientoCaja` llevan sus valores fijos, protegidos por `CHECK`

**Decisión**: dos `CHECK` nuevos, con los mismos números del enum escritos a mano en el `WHERE`
(convención de `EstadoFactura`, research §4 del Módulo 6):

- `CHECK ([Estado] = 0 AND [FechaCierre] IS NULL AND [SaldoFinal] IS NULL) OR ([Estado] = 1 AND
  [FechaCierre] IS NOT NULL AND [SaldoFinal] IS NOT NULL)` en `Cajas`: ata el estado a las dos columnas
  de las que depende, siguiendo la convención [009] ("un estado que podría derivarse se puede guardar si
  un `CHECK` lo ata a las columnas de las que depende"). Un cierre a medio hacer —`Cerrada` sin
  `SaldoFinal`, o `Abierta` con `FechaCierre` puesta— es imposible en la base, no sólo en el código.
- `CHECK ([Tipo] = 0 AND [OrdenDePagoId] IS NULL) OR ([Tipo] = 1 AND [FacturaId] IS NULL)` en
  `MovimientosDeCaja`: cierra RN8 en la base. Un ingreso con una orden de pago, o un egreso con una
  factura, no puede insertarse aunque alguien invoque el repositorio sin pasar por el caso de uso.

**Rationale**: son la misma garantía que ya usan los motivos de rechazo/anulación de los Módulos 9 y 10
(convención [009] y [010]): la aplicación da el mensaje, la base cierra el resto. Reordenar el enum
rompería el `CHECK` sin fallar al compilar, así que un test de restricciones —como
`IndicesDeFacturaTests`— inserta una fila por combinación válida e inválida.

## §7. `Concepto` lleva un `CHECK` de no-vacío-tras-`TRIM`, además de la validación de la aplicación

**Decisión**: `CHECK (LEN(LTRIM(RTRIM([Concepto]))) > 0)`.

**Rationale**: convención [010]: en un `CHECK`, toda comparación sobre texto que puede llegar en blanco
se escribe explícita — acá no hay columna anulable (`Concepto` es `NOT NULL`), así que no hace falta el
`IS NOT NULL` de esa convención, pero el riesgo que cierra es el mismo: un movimiento nunca se edita
después de creado (FR-014), así que si un concepto de sólo espacios lograra pasar por un camino que no
sea el formulario —una invocación directa que no pase por la validación de la aplicación—, quedaría
así **para siempre**. El `CHECK` es la garantía de que eso no puede pasar, sin importar por dónde se
llegue al `INSERT`.

## §8. Consulta de movimientos: los filtros van antes de paginar, el estado de la caja no se recalcula

**Decisión**: `ConsultarMovimientos` arma el `IQueryable` con los filtros de fecha y de caja en el árbol
de la consulta, cuenta, pagina y recién ahí proyecta a `MovimientoListado` (mismo patrón de
`RepositorioLiquidaciones.ConsultarAsync`). El listado de cajas (FR-026) es una consulta separada y más
simple: no tiene más filtro que la paginación, porque la spec no pide filtrar cajas por nada.

**Rationale**: convención [003] de punta a punta; es el precedente ya usado en los Módulos 9 y 10 y no
hay ninguna razón para apartarse acá.

## §9. El permiso de gestión de caja no distingue niveles adentro

**Decisión**: un solo permiso de escritura, `caja.gestionar`, cubre abrir, registrar movimiento y cerrar.
`caja.consultar` cubre el listado de cajas y de movimientos. Reparto: `caja.gestionar` a *Administración
de la empresa* y *Administrador del sistema*; `caja.consultar` a esos dos más *Gerencia* (FR-031,
FR-032), replicando el reparto de dos niveles de los Módulos 9 y 10.

**Rationale**: a diferencia de la Facturación (Módulo 6), que distingue *anular* como su propio permiso
porque revierte algo firme, acá no hay ninguna acción de esa naturaleza —no hay anulación ni reversión de
nada— así que un único permiso de escritura no pierde ninguna granularidad que la spec pida.

## §10. Estructura de carpetas: `Caja` en el backend, `caja` en el frontend

**Decisión**: `GT.Domain/Caja/`, `GT.Application/Caja/`, `GT.Api/Caja/`, `frontend/src/modules/caja/`
—singular, no `Cajas`—.

**Rationale**: los precedentes pluralizan la **entidad principal listada** (`Domain/Liquidaciones` para
`Liquidacion`, `Domain/Adelantos` para `Adelanto`). Acá la entidad principal es `Caja`, pero el módulo
tiene **dos** entidades de primer nivel —`Caja` y `MovimientoDeCaja`— y ninguna de las dos se llama
"cajas" en el lenguaje del negocio: se habla de "la caja" (una por empleado, por día) y de "los
movimientos". `Facturacion` sienta el precedente de nombrar la carpeta por el **proceso** cuando no hay
una única entidad que pluralizar limpiamente; "Caja" sigue esa misma lógica y coincide con el nombre de
la spec ("Gestión de caja") y de la rama (`011-gestion-caja`).
