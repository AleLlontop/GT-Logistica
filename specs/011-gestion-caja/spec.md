# Feature Specification: Gestión de caja (Módulo 11)

**Feature Branch**: `011-gestion-caja`

**Created**: 2026-09-17

**Status**: Draft

**Input**: User description: "Módulo Gestión de Caja v1.0. Controla el efectivo diario de la empresa: se abre la caja con un saldo inicial, se registran los ingresos y egresos del día (vinculados a facturas de clientes u órdenes de pago), y se cierra calculando el saldo final. Importa porque da trazabilidad (quién registró cada movimiento y cuándo), evita pérdidas y permite verificar que el efectivo real coincida con el calculado. En la vida real: al comenzar el día, el administrativo abre la caja con el cambio disponible; durante la jornada registra cobros a clientes y pagos a proveedores; al finalizar, revisa el resumen, confirma el cierre y verifica el saldo. Usuarios: el empleado administrativo (usuario directo, autenticado) abre y cierra la caja, registra movimientos y consulta el historial; el gerente (destinatario indirecto) revisa los resultados de cierres y movimientos sin operar necesariamente la aplicación. Historias: HU1 abrir la caja registrando el saldo inicial quedando como responsable; HU2 registrar ingresos y egresos con concepto y referencia (factura u orden de pago) para mantener el saldo actualizado y trazable; HU3 cerrar la caja viendo un resumen con el saldo final calculado; HU4 consultar movimientos por período o por caja. Requisitos: RF1 abrir una caja registrando saldo inicial, fecha de apertura, estado 'abierta' y el empleado responsable; RF2 impedir que un mismo empleado tenga más de una caja abierta simultáneamente; RF3 registrar movimientos de tipo ingreso o egreso, con importe, concepto, y asociación opcional a una orden de pago pendiente (egresos) o a una factura pendiente de cobro (ingresos); RF4 impedir registrar movimientos cuando no existe una caja abierta; RF5 registrar en cada movimiento el empleado responsable y la fecha; RF6 mostrar, previo al cierre, el resumen de movimientos del día y el saldo final calculado; RF7 confirmar o cancelar el cierre; al confirmar, registra estado 'cerrada', fecha de cierre y saldo final; RF8 consultar movimientos por rango de fechas o por caja específica, mostrando fecha, tipo, importe, concepto, responsable y referencia; RF9 informar cuando no existen movimientos en el período consultado. Reglas: RN1 una sola caja abierta por empleado (si Ana abrió caja a las 8:00 con $10.000 e intenta abrir otra a las 10:00, el sistema lo rechaza e indica que debe cerrar la actual primero); RN2 sin caja abierta no hay movimientos; RN3 importes estrictamente positivos; RN4 saldo inicial no negativo; RN5 campos obligatorios completos (tipo, importe, concepto), el campo faltante queda marcado; RN6 saldo final = saldo inicial + Σ ingresos − Σ egresos (apertura $10.000, ingresos $3.500, egresos $2.000 → saldo final $11.500); RN7 trazabilidad obligatoria: cada movimiento con la fecha y el nombre de quien lo registró, cada caja con su responsable de apertura; RN8 referencias según tipo: una orden de pago solo puede asociarse a un egreso, una factura de cliente solo a un ingreso, y la asociación es opcional. Criterios de aceptación: CA1 al abrir caja con saldo válido existe una caja 'abierta' con la fecha de hoy y mi nombre como responsable; CA2 si ya tengo una caja abierta, al intentar abrir otra se muestra el mensaje indicando que debo cerrar la actual y no se crea nada; CA3 sin caja abierta, 'Registrar Movimiento' muestra el aviso correspondiente y no guarda nada; CA4 un movimiento con importe ≤ 0 o sin concepto no se guarda y el campo erróneo queda marcado; CA5 cada movimiento del listado muestra fecha, tipo, importe, concepto, responsable y su referencia si la tiene; CA6 en la pantalla de cierre, el saldo final mostrado coincide con saldo inicial + ingresos − egresos de los movimientos listados; CA7 si cancelo el cierre, la caja permanece abierta y sin cambios; CA8 al confirmar el cierre, la caja pasa a 'cerrada' con fecha de cierre y saldo final registrados, y ya no admite nuevos movimientos; CA9 una consulta sobre un período sin movimientos muestra el mensaje informativo 'no existen movimientos'. Casos límite: CL1 cerrar una caja sin movimientos es válido, el resumen aparece vacío y el saldo final es igual al saldo inicial; CL2 consultar un período sin movimientos informa 'no existen movimientos' (no un listado vacío sin aviso); CL3 doble confirmación al abrir (doble clic): la validación 'no debe existir caja abierta' se re-verifica al momento de crear, para que nunca se generen dos cajas. Fuera de alcance: edición, anulación o eliminación de movimientos ya registrados; reapertura de cajas cerradas; arqueo físico (conteo de billetes/monedas) ni registro de diferencias entre efectivo contado y saldo calculado; transferencias entre cajas de distintos empleados ni manejo multi-sucursal; gestión de medios de pago diferenciados (cheques, tarjetas, transferencias): se tratan como movimientos genéricos (este es otro módulo); roles adicionales (supervisor que cierre cajas ajenas, aprobaciones); exportación de reportes (PDF/Excel) e impresión de comprobantes; integración contable automática (generación de asientos)."

## Clarifications

### Session 2026-09-17

- Q: Cuando el empleado que abrió una caja se olvida de cerrarla y llega el día siguiente, ¿la caja sigue operativa para nuevos movimientos, o queda bloqueada hasta cerrarla? → A: Sigue operativa: los movimientos se registran sobre ella sin importar cuántos días lleva abierta; el resumen de cierre incluye todo desde la apertura.
- Q: Cuando un egreso supera el saldo actual de la caja, ¿el sistema lo permite igual o lo rechaza por falta de efectivo? → A: Se permite igual: el sistema no controla el efectivo físico, la diferencia se detecta al cerrar viendo un saldo final bajo o negativo.
- Q: Cuando un movimiento de caja se asocia a una factura pendiente de cobro o a una orden de pago pendiente, ¿ese registro marca la factura/orden como cobrada/pagada en su módulo de origen, o es sólo una referencia informativa? → A: Sólo referencia: registrar el movimiento no cambia nada en Módulo 6 ni en Módulo 9; una misma factura/orden puede referenciarse desde más de un movimiento.

### Session 2026-09-18

- Q: La orden de pago del Módulo 9 es el registro de un pago ya hecho y no tiene ningún estado de "pendiente". ¿Qué órdenes se pueden asociar a un egreso? → A: Cualquier orden de pago **existente**, sin filtro de estado; no se le inventa un estado al Módulo 9. El desplegable ofrece las 50 más recientes y lo avisa debajo del campo, para no ocultar filas en silencio.
- Q: ¿Puede un empleado con permiso de gestión registrar movimientos o cerrar la caja de otro empleado? → A: No: sólo el responsable de la caja la opera, también cuando se invoca la acción directamente (FR-035).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Abrir la caja (Priority: P1)

Al comenzar el día, el empleado administrativo abre la caja con el cambio disponible: registra el saldo
inicial y queda como responsable. Desde ese momento la caja está `abierta` y admite movimientos.

**Why this priority**: es la puerta de entrada del módulo: sin caja abierta no se registra ningún
movimiento (RN2), así que nada de lo demás puede ocurrir antes.

**Independent Test**: se prueba con dos usuarios administrativos: uno abre su caja con $10.000 y queda
como responsable; al intentar abrir una segunda, el sistema lo rechaza indicando que debe cerrar la
actual; el otro puede abrir la suya en paralelo, porque la restricción es por empleado.

**Acceptance Scenarios**:

1. **Given** un empleado administrativo sin caja abierta, **When** abre la caja con saldo inicial
   $10.000, **Then** existe una caja en estado `abierta`, con la fecha de apertura del día en curso y
   su nombre como responsable (CA1).
2. **Given** un empleado con una caja `abierta`, **When** intenta abrir otra, **Then** el sistema
   muestra el mensaje indicando que debe cerrar la actual primero y no se crea nada (CA2, RN1).
3. **Given** un empleado sin caja abierta, **When** intenta abrirla con saldo inicial −$1.000,
   **Then** el sistema marca el saldo inicial informando que no puede ser negativo y no crea nada
   (RN4); con $0 la apertura se acepta.
4. **Given** un empleado sin caja abierta, **When** confirma la apertura dos veces casi al mismo
   tiempo —doble clic o dos pestañas—, **Then** se crea exactamente una caja: la validación de que no
   existe otra caja abierta se re-verifica al momento de crear (CL3).
5. **Given** un empleado con su caja `abierta`, **When** otro empleado administrativo abre la suya,
   **Then** la apertura se acepta: la restricción es una caja abierta por empleado, no una para toda
   la empresa (RN1).
6. **Given** el formulario de apertura sin saldo inicial cargado, **When** presiona el botón de
   apertura, **Then** el sistema marca el campo como obligatorio y no crea nada (RN5).

---

### User Story 2 - Registrar ingresos y egresos (Priority: P1)

Durante la jornada, el empleado administrativo registra los cobros a clientes y los pagos a
proveedores: tipo (ingreso o egreso), importe, concepto y, si corresponde, la referencia —una factura
pendiente de cobro para un ingreso, una orden de pago existente para un egreso—. Cada movimiento
queda con la fecha y el nombre de quien lo registró.

**Why this priority**: es la operación central del día y lo que mantiene el saldo actualizado y
trazable; sin movimientos, la caja no controla nada.

**Independent Test**: se prueba con una caja abierta registrando un ingreso de $3.500 con referencia
a una factura pendiente y un egreso de $2.000 con referencia a una orden de pago existente, y
verificando que ambos quedan con fecha, responsable y referencia; y con la caja cerrada, que el
intento de registrar se rechaza con el aviso correspondiente.

**Acceptance Scenarios**:

1. **Given** un empleado sin caja `abierta`, **When** intenta registrar un egreso de $500, **Then**
   el sistema muestra el aviso de que no hay una caja abierta y no guarda nada (CA3, RN2).
2. **Given** una caja `abierta`, **When** registra un ingreso de $3.500 con concepto "cobro flete" y
   una factura pendiente de cobro como referencia, **Then** el movimiento queda registrado con su
   fecha, el nombre de quien lo cargó y la referencia a la factura (RN7, RF5).
3. **Given** una caja `abierta`, **When** registra un egreso de $2.000 con concepto "pago a
   proveedor" y una orden de pago existente como referencia, **Then** el movimiento queda registrado
   con su fecha, responsable y referencia.
4. **Given** un movimiento con importe $0 o −$200, **When** intenta guardarlo, **Then** el sistema
   marca el importe informando que tiene que ser mayor a cero y no guarda nada (RN3, CA4).
5. **Given** un movimiento sin tipo, sin importe, sin concepto o con un concepto de sólo espacios,
   **When** intenta guardarlo, **Then** el sistema marca cada campo faltante, no guarda nada y
   conserva lo que ya estaba cargado (RN5, CA4).
6. **Given** un ingreso, **When** intenta asociarle una orden de pago, o un egreso al que intenta
   asociarle una factura de cliente, **Then** el sistema lo rechaza: una orden de pago sólo se
   asocia a un egreso y una factura sólo a un ingreso (RN8).
7. **Given** un movimiento sin referencia, **When** se guarda con tipo, importe y concepto válidos,
   **Then** se registra igual: la asociación es opcional (RN8).
8. **Given** una referencia a una factura inexistente o que ya no está pendiente de cobro, o a una
   orden de pago inexistente,
   **When** intenta guardar el movimiento —también invocando la acción directamente, sin pasar por la
   pantalla—, **Then** el sistema lo rechaza informando el motivo y no registra nada.
9. **Given** el formulario de movimiento abierto sobre una caja que mientras tanto se cerró —la misma
   persona en otra pestaña—, **When** confirma el guardado, **Then** el sistema re-verifica que exista
   una caja `abierta` al momento de guardar y lo rechaza con el aviso correspondiente (RN2).
10. **Given** un importe con más de dos decimales, **When** intenta guardarlo, **Then** el sistema lo
    marca como formato incorrecto y no registra.

---

### User Story 3 - Cerrar la caja con su resumen (Priority: P1)

Al finalizar el día, el empleado administrativo revisa el resumen: los movimientos registrados y el
saldo final calculado. Confirma el cierre y la caja pasa a `cerrada` con su fecha de cierre y el
saldo final registrado; o lo cancela y todo queda como estaba.

**Why this priority**: es la verificación del efectivo antes de terminar el día, el momento en que el
control se completa; sin cierre, la caja queda abierta para siempre y al día siguiente no se puede
abrir otra (RN1).

**Independent Test**: se prueba abriendo una caja con $10.000, registrando ingresos por $3.500 y
egresos por $2.000, y comprobando que el resumen previo muestra saldo final $11.500; al confirmar, la
caja queda `cerrada` con ese saldo y ya no admite movimientos; y sobre otra caja, que cancelar el
cierre la deja `abierta` y sin cambios.

**Acceptance Scenarios**:

1. **Given** una caja `abierta` con saldo inicial $10.000, ingresos por $3.500 y egresos por $2.000,
   **When** el empleado entra a cerrarla, **Then** el resumen muestra el saldo inicial, los
   movimientos del día y el saldo final calculado $11.500, que coincide con saldo inicial + ingresos
   − egresos de los movimientos listados (CA6, RN6).
2. **Given** el resumen de cierre en pantalla, **When** cancela el cierre, **Then** la caja permanece
   `abierta` y sin cambios (CA7).
3. **Given** el resumen de cierre en pantalla, **When** confirma el cierre, **Then** la caja pasa a
   `cerrada` con la fecha de cierre y el saldo final registrados, y ya no admite nuevos movimientos
   (CA8).
4. **Given** una caja `abierta` sin ningún movimiento, **When** entra a cerrarla, **Then** el resumen
   aparece vacío, el saldo final es igual al saldo inicial y el cierre es válido (CL1).
5. **Given** una caja `cerrada`, **When** se intenta registrar un movimiento sobre ella —también
   invocando la acción directamente—, **Then** el sistema lo rechaza informando que la caja está
   cerrada y no guarda nada (CA8).
6. **Given** un pedido de cierre sin confirmación explícita —invocando la acción directamente, sin
   pasar por la pantalla—, **When** el sistema lo recibe, **Then** no cierra nada y la caja sigue
   `abierta`.
7. **Given** el resumen de cierre mostrado y un movimiento nuevo registrado entre el resumen y la
   confirmación —la misma persona en otra pestaña—, **When** confirma el cierre, **Then** el sistema
   no cierra con un saldo final que no vio: le muestra el resumen actualizado y le pide confirmar de
   nuevo.
8. **Given** dos pedidos de cierre sobre la misma caja casi al mismo tiempo, **When** el sistema los
   procesa, **Then** exactamente uno se completa y el otro se rechaza informando que la caja ya está
   cerrada.

---

### User Story 4 - Consultar movimientos por período o por caja (Priority: P2)

El empleado administrativo consulta los movimientos filtrando por rango de fechas o por una caja
específica, y ve de cada uno la fecha, el tipo, el importe, el concepto, quién lo registró y su
referencia si la tiene. El gerente revisa los resultados de los cierres y los movimientos con la
misma consulta, sin operar.

**Why this priority**: es lo que permite revisar la actividad y detectar diferencias después del día
a día, y la vía de control del gerente; es posterior a registrar y cerrar.

**Independent Test**: se prueba con dos cajas cerradas de días distintos y una abierta, consultando
por rango de fechas y por caja, verificando las columnas de cada movimiento y que un período sin
movimientos muestra el mensaje informativo en lugar de una tabla vacía.

**Acceptance Scenarios**:

1. **Given** movimientos registrados, **When** se consulta por rango de fechas o por caja, **Then**
   cada movimiento del listado muestra fecha, tipo, importe, concepto, responsable y su referencia si
   la tiene (CA5, RF8).
2. **Given** movimientos del 14/09/2026, del 15/09/2026 y del 16/09/2026, **When** se filtra desde el
   15/09/2026 hasta el 16/09/2026, **Then** el listado muestra los del 15/09 y del 16/09 y no el del
   14/09: los dos extremos del rango se incluyen.
3. **Given** una fecha *desde* posterior a la fecha *hasta*, **When** aplica el filtro, **Then** el
   sistema marca el rango como inválido y no filtra.
4. **Given** un período sin movimientos, **When** se consulta, **Then** el sistema muestra el mensaje
   informativo "no existen movimientos" en lugar de un listado vacío sin aviso (CA9, CL2, RF9).
5. **Given** varias cajas, **When** se consulta por una caja específica, **Then** el listado muestra
   únicamente los movimientos de esa caja.
6. **Given** un filtro aplicado, **When** se mira el listado, **Then** los controles muestran qué
   filtro está aplicado.
7. **Given** un usuario con rol *Gerencia*, **When** abre la consulta, **Then** puede revisar las
   cajas con sus saldos y sus movimientos, y no ve ninguna acción de abrir, registrar ni cerrar.

---

### User Story 5 - Acceso restringido (Priority: P1)

Sólo quien corresponde entra al módulo: el empleado administrativo abre, registra, cierra y consulta;
el gerente sólo consulta; nadie sin sesión entra a ninguna opción.

**Why this priority**: la caja es el efectivo de la empresa; que cualquiera pueda abrirla, cargar
movimientos o cerrarla es un riesgo directo de pérdida.

**Independent Test**: se prueba intentando entrar a las opciones del módulo sin sesión, con un
usuario de *Tráfico*, con uno de *Gerencia* y con uno de *Administración de la empresa*.

**Acceptance Scenarios**:

1. **Given** un usuario no autenticado, **When** intenta acceder a cualquiera de las opciones del
   módulo, **Then** es redirigido al ingreso.
2. **Given** un usuario con rol *Administración de la empresa* o *Administrador del sistema*,
   **When** ingresa, **Then** ve las opciones del módulo y puede abrir la caja, registrar
   movimientos, cerrarla y consultar.
3. **Given** un usuario con rol *Gerencia*, **When** ingresa, **Then** ve sólo la consulta, no ve
   ninguna acción de escritura, y si invoca directamente cualquiera de ellas el sistema la rechaza.
4. **Given** un usuario sin ninguno de los permisos del módulo, **When** ingresa, **Then** no ve
   ninguna de las opciones en el menú y el sistema rechaza cualquier intento de invocarlas
   directamente.
5. **Given** un usuario con sesión pero sin el permiso de la pantalla, **When** escribe su dirección
   directamente, **Then** la pantalla informa que no tiene permiso y a quién pedírselo, no muestra
   los datos ni el formulario, y no sugiere volver a intentar.

---

### Edge Cases

- Cerrar una caja sin movimientos: el resumen aparece vacío, el saldo final es igual al saldo inicial
  y el cierre es válido (CL1).
- Consultar un período sin movimientos: se informa "no existen movimientos", no un listado vacío sin
  aviso (CL2).
- Doble confirmación al abrir —doble clic o dos pestañas—: la validación "no debe existir caja
  abierta" se re-verifica al momento de crear, y nunca se generan dos cajas (CL3).
- Una caja que quedó `abierta` de un día anterior —el empleado se fue sin cerrarla—: sigue operativa;
  los movimientos se registran sobre ella hasta que se cierre y el resumen del cierre incluye todos
  desde la apertura, sin importar cuántos días lleva abierta.
- Un egreso que supera el saldo actual de la caja —saldo inicial $10.000, sin ingresos, egreso de
  $12.000—: se permite; el sistema no controla el efectivo físico y la diferencia se detecta al
  cerrar viendo un saldo final bajo o negativo.
- Una factura u orden de pago asociada a un movimiento: la asociación es sólo una referencia; no
  cambia el estado de la factura en el Módulo 6 ni el de la orden de pago en el Módulo 9. Una misma
  factura u orden puede asociarse a más de un movimiento.
- Un movimiento cargado con un importe o concepto equivocado: no se edita, se anula ni se elimina
  (fuera de alcance); queda registrado como se cargó, y la corrección se hace por fuera del módulo o
  con un movimiento inverso con su concepto aclaratorio, que es un movimiento como cualquier otro.
- El concepto es un texto de sólo espacios: se trata como vacío y queda marcado (RN5).
- Un empleado intenta registrar un movimiento o cerrar la caja de otro empleado —invocando la acción
  directamente—: se rechaza informando que sólo quien la abrió puede operarla (FR-035).
- El importe tiene más de dos decimales: se marca como formato incorrecto y no se registra.
- La factura elegida deja de estar pendiente de cobro mientras el formulario está abierto —se cobró
  por su ficha en otra pantalla—: el guardado la re-verifica y rechaza el movimiento informando el
  motivo. La orden de pago no tiene estado que re-verificar: basta con que exista.
- Dos movimientos confirmados al mismo tiempo sobre la misma caja —la misma persona en dos
  pestañas—: los dos se registran y el saldo final del cierre los suma a los dos.
- Un movimiento confirmado en el mismo instante en que se confirma el cierre: si el movimiento entra
  primero, el resumen cambió y el cierre pide confirmar de nuevo con los números actualizados; si el
  cierre entra primero, el movimiento se rechaza porque la caja ya está cerrada. Nunca queda un
  movimiento fuera del resumen de una caja cerrada.
- El filtro por rango tiene sólo la fecha *desde* o sólo la fecha *hasta*: se aplica el extremo
  cargado y el otro queda abierto.
- El empleado responsable de una caja es dado de baja del sistema con la caja `abierta`: la caja
  sigue existiendo con su responsable visible; como nadie más puede operarla —las cajas ajenas están
  fuera de alcance—, su cierre queda como situación a resolver por fuera del sistema en esta versión.

## Requirements *(mandatory)*

### Functional Requirements

#### Apertura

- **FR-001**: El sistema DEBE permitir abrir una caja registrando el **saldo inicial**, la **fecha de
  apertura**, el estado `abierta` y el **empleado responsable**, que es el usuario que la abre (RF1).
- **FR-002**: El saldo inicial DEBE ser un importe en pesos **no negativo**, con a lo sumo dos
  decimales. Un saldo negativo DEBE rechazarse marcando el campo (RN4); $0 se acepta.
- **FR-003**: El sistema DEBE impedir que un mismo empleado tenga más de una caja `abierta`
  simultáneamente (RF2, RN1). Al intentarlo, DEBE mostrar el mensaje indicando que debe cerrar la
  actual primero y NO DEBE crear nada (CA2). La validación DEBE re-verificarse al momento de crear la
  caja, de modo que dos pedidos simultáneos del mismo empleado generen exactamente una (CL3).
- **FR-004**: Empleados distintos DEBEN poder tener cada uno su caja `abierta` al mismo tiempo: la
  restricción de FR-003 es por empleado, no global (RN1).
- **FR-005**: La fecha de apertura DEBE ser el instante en que se abre, registrado por el sistema; NO
  DEBE ser un dato que se tipea.

#### Movimientos

- **FR-006**: El sistema DEBE permitir registrar movimientos de tipo **ingreso** o **egreso**, con
  **importe**, **concepto** y **referencia opcional** (RF3).
- **FR-007**: El sistema DEBE impedir registrar movimientos cuando el empleado no tiene una caja
  `abierta` (RF4, RN2), mostrando el aviso correspondiente y sin guardar nada (CA3). La existencia de
  la caja `abierta` DEBE re-verificarse al momento de guardar.
- **FR-008**: El importe del movimiento DEBE ser estrictamente **mayor a cero**, en pesos y con a lo
  sumo dos decimales (RN3). Un importe de $0, negativo o con más de dos decimales DEBE rechazarse
  marcando el campo (CA4).
- **FR-009**: El **concepto** DEBE ser obligatorio y admite **hasta 200 caracteres**; un concepto de
  sólo espacios DEBE tratarse como vacío (RN5). El **tipo** DEBE ser obligatorio.
- **FR-010**: Al guardar con campos vacíos o con formato incorrecto, el sistema DEBE marcar cada
  campo con su problema, NO DEBE guardar el movimiento y DEBE conservar lo que ya estaba cargado
  (RN5, CA4).
- **FR-011**: La referencia DEBE seguir el tipo del movimiento (RN8): a un **egreso** sólo se le puede
  asociar una **orden de pago existente** del Módulo 9, que no tiene estado de "pendiente"; a un
  **ingreso** sólo se le puede asociar una **factura pendiente de cobro** del Módulo 6. La asociación
  es **opcional**. El sistema DEBE verificar al guardar —también cuando se invoca la acción
  directamente— que la referencia exista, corresponda al tipo y, si es una factura, siga pendiente de
  cobro, y rechazar el movimiento informando el motivo si no se cumple. El desplegable de egresos
  ofrece las **50 órdenes de pago más recientes** y lo avisa debajo del campo.
- **FR-012**: La asociación de un movimiento a una factura o a una orden de pago es sólo una
  referencia: registrar el movimiento NO DEBE modificar el estado de la factura en el Módulo 6 ni el
  de la orden de pago en el Módulo 9. Una misma factura u orden de pago PUEDE asociarse a más de un
  movimiento.
- **FR-013**: El sistema DEBE registrar en cada movimiento el **empleado responsable** que lo cargó y
  la **fecha** —el instante registrado por el sistema, no un dato tipeado— (RF5, RN7).
- **FR-014**: Un movimiento registrado NO DEBE poder editarse, anularse ni eliminarse (fuera de
  alcance del enunciado).
- **FR-015**: Un egreso que supera el saldo actual de la caja DEBE permitirse igual: el sistema no
  controla el efectivo físico, y la diferencia se detecta al cerrar viendo un saldo final bajo o
  negativo.

#### Cierre

- **FR-016**: Previo al cierre, el sistema DEBE mostrar el **resumen** de la caja: el saldo inicial,
  los movimientos registrados y el **saldo final calculado** como saldo inicial + Σ ingresos −
  Σ egresos (RF6, RN6, CA6).
- **FR-017**: El sistema DEBE permitir **confirmar o cancelar** el cierre (RF7). Cancelar DEBE dejar
  la caja `abierta` y sin cambios (CA7).
- **FR-018**: El cierre DEBE exigir una **confirmación explícita**, también cuando se invoca la
  acción directamente sin pasar por la pantalla; sin ella NO DEBE ejecutarse. Si entre el resumen
  mostrado y la confirmación se registró un movimiento nuevo, el sistema NO DEBE cerrar con el saldo
  anterior: DEBE mostrar el resumen actualizado y pedir confirmar de nuevo.
- **FR-019**: Al confirmar, el sistema DEBE registrar el estado `cerrada`, la **fecha de cierre** y
  el **saldo final** (RF7, CA8). Una caja `cerrada` NO DEBE admitir nuevos movimientos: el intento
  DEBE rechazarse informando que la caja está cerrada (CA8).
- **FR-020**: Cerrar una caja **sin movimientos** DEBE ser válido: el resumen aparece vacío y el
  saldo final es igual al saldo inicial (CL1).
- **FR-021**: Cuando dos pedidos de cierre llegan al mismo tiempo sobre la misma caja, exactamente
  uno DEBE completarse y el otro DEBE rechazarse informando que la caja ya está cerrada.
- **FR-022**: Una caja `abierta` de un día anterior sigue operativa: los movimientos se registran
  sobre ella hasta que se cierre y el resumen incluye todos los movimientos desde la apertura, sin
  importar cuántos días lleva abierta.

#### Consulta

- **FR-023**: El sistema DEBE permitir consultar movimientos por **rango de fechas** o por **caja
  específica** (RF8). El rango DEBE incluir sus dos extremos y admitir uno solo de ellos; un rango
  con *desde* posterior a *hasta* DEBE marcarse como inválido sin filtrar.
- **FR-024**: El listado de movimientos DEBE mostrar de cada uno su **fecha, tipo, importe, concepto,
  responsable y referencia** si la tiene (RF8, CA5).
- **FR-025**: Cuando no existen movimientos en el período o la caja consultados, el sistema DEBE
  mostrar el mensaje informativo "no existen movimientos" en lugar de un listado vacío sin aviso
  (RF9, CA9, CL2).
- **FR-026**: El sistema DEBE ofrecer una consulta de **cajas** que muestre de cada una su
  responsable, fecha de apertura, estado, y —si está cerrada— fecha de cierre y saldo final, desde la
  cual se accede a sus movimientos. Es la vía con la que el gerente revisa los resultados de los
  cierres.
- **FR-027**: La consulta DEBE mostrar qué filtros están aplicados; ningún listado DEBE ocultar filas
  en silencio.

#### Presentación

- **FR-028**: Todo importe DEBE mostrarse en pesos argentinos con símbolo `$`, punto como separador
  de miles y coma decimal, y DEBE calcularse sin error de redondeo.
- **FR-029**: Ningún estado DEBE comunicarse sólo por color.
- **FR-030**: Todo resultado que aparezca sin que la pantalla cambie —el guardado de un movimiento,
  la apertura o el cierre de la caja, un filtro aplicado— DEBE anunciarse de forma accesible.

#### Acceso

- **FR-031**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados y DEBE
  resolverlo por permiso y nunca por rol, con un permiso de **gestión** —abrir la caja, registrar
  movimientos y cerrarla— y uno de **consulta** —cajas y movimientos—. El menú DEBE resolver sus
  opciones según lo que el servidor decide para cada usuario, sin que la pantalla evalúe permisos por
  su cuenta.
- **FR-032**: El permiso de gestión DEBE corresponder a los roles *Administración de la empresa* y
  *Administrador del sistema*. El de consulta DEBE corresponder a esos dos roles y además a
  *Gerencia*.
- **FR-033**: Quien no tenga el permiso correspondiente NO DEBE ver la opción ni la acción, y el
  sistema DEBE rechazarla igual si se la invoca directamente. Si llega a una pantalla del módulo
  escribiendo su dirección, la pantalla DEBE informar que no tiene permiso y a quién pedírselo, sin
  mostrar sus datos ni su formulario y sin sugerir que vuelva a intentar; lo mismo cuando una acción
  se rechaza por falta de permiso.
- **FR-034**: Un usuario no autenticado que intente acceder a cualquiera de las opciones del módulo
  DEBE ser redirigido al ingreso.
- **FR-035**: Sólo el **empleado responsable** de una caja DEBE poder registrar movimientos en ella y
  cerrarla; el sistema DEBE rechazar el intento de cualquier otro usuario, aunque tenga el permiso de
  gestión y aunque invoque la acción directamente, informando que sólo quien la abrió puede operarla.
  Consultarla sigue abierto a todo el que tenga el permiso de consulta.

### Key Entities *(include if feature involves data)*

- **Caja**: el control de efectivo de un empleado durante una jornada. Incluye el saldo inicial, la
  fecha de apertura, el empleado responsable, el estado (`abierta`/`cerrada`) y, al cerrarse, la
  fecha de cierre y el saldo final. Es la entidad principal del módulo; una vez cerrada no se
  reabre ni se modifica.
- **MovimientoDeCaja**: un ingreso o egreso registrado sobre una caja. Incluye el tipo, el importe en
  pesos, el concepto, la referencia opcional —una factura pendiente de cobro o una orden de pago
  existente—, el empleado que lo registró y la fecha. Pertenece a una única caja; no se edita, no se
  anula y no se borra.
- **Factura**: la factura de cliente del Módulo 6. Este módulo la consume como referencia opcional de
  los ingresos, eligiendo sólo entre las pendientes de cobro, y no la administra.
- **OrdenDePago**: la orden de pago del Módulo 9. Este módulo la consume como referencia opcional de
  los egresos —es el registro de un pago ya hecho y no tiene estado de "pendiente"—, y no la
  administra.
- **Usuario**: el empleado administrativo que abre la caja y registra movimientos, del Módulo 1. Este
  módulo lo registra como responsable de la caja y de cada movimiento, y no lo administra.

### Enumerations

- **EstadoCaja**: `abierta`, `cerrada`, excluyentes. Toda caja nace `abierta`; `cerrada` es final: no
  se reabre (fuera de alcance).
- **TipoMovimientoCaja**: `ingreso`, `egreso`. Determina qué referencia admite el movimiento
  (FR-011).

### Relationships

- **Usuario 1 — * Caja**: toda caja pertenece al empleado que la abrió; un empleado puede tener
  muchas cajas a lo largo del tiempo, pero a lo sumo una `abierta` (FR-003).
- **Caja 1 — * MovimientoDeCaja**: todo movimiento pertenece a exactamente una caja; una caja puede
  tener muchos movimientos o ninguno (CL1).
- **MovimientoDeCaja * — 1 Usuario**: todo movimiento queda asociado al empleado que lo registró
  (FR-013, RN7).
- **MovimientoDeCaja * — 0..1 Factura**: un ingreso puede referenciar a lo sumo una factura
  pendiente de cobro; una factura puede ser referenciada por movimientos o por ninguno.
- **MovimientoDeCaja * — 0..1 OrdenDePago**: un egreso puede referenciar a lo sumo una orden de pago
  existente; una orden puede ser referenciada por movimientos o por ninguno.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El empleado administrativo puede abrir la caja, registrar un cobro y un pago, cerrarla
  y verificar el saldo final sin intervención técnica y sin anotar nada fuera del sistema.
- **SC-002**: El 0% de los empleados llega a tener dos cajas abiertas a la vez, incluidos los
  intentos simultáneos; el 100% de los intentos de abrir una segunda es rechazado con el mensaje que
  indica cerrar la actual.
- **SC-003**: El 0% de los movimientos se registra sin caja abierta, con importe cero o negativo, sin
  concepto, o con una referencia que no corresponde al tipo, no existe o —si es una factura— no está
  pendiente de cobro; el 100% de esos
  intentos es rechazado sin guardar nada y sin perder lo cargado.
- **SC-004**: El 100% de los movimientos muestra la fecha y el nombre de quien lo registró, y el
  100% de las cajas muestra su responsable de apertura.
- **SC-005**: En el 100% de los cierres, el saldo final mostrado y registrado es exactamente saldo
  inicial + ingresos − egresos de los movimientos de la caja, sin error de redondeo.
- **SC-006**: El 100% de las consultas sobre períodos o cajas sin movimientos muestra el mensaje
  informativo; el 0% muestra una tabla vacía sin explicación.
- **SC-007**: Una persona no técnica puede responder cuánto entró y cuánto salió de una caja en menos
  de 30 segundos, desde la consulta y sin sumar a mano.
- **SC-008**: El 0% de las cajas cerradas admite un movimiento nuevo, se reabre o cambia su saldo
  final registrado.
- **SC-009**: El 100% de los intentos de operar sobre el módulo sin sesión o sin el permiso
  correspondiente es rechazado; un usuario sólo con el permiso de consulta no puede abrir la caja,
  registrar movimientos ni cerrarla.

## Assumptions

- La autenticación, el catálogo de roles (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) y el esquema de permisos con menú resuelto por el servidor ya existen
  (Módulos 1 y 2); este módulo agrega sus permisos y los asigna a los roles.
- El **empleado administrativo** del enunciado es el rol *Administración de la empresa* que ya
  existe, igual que en los Módulos 6, 9 y 10. El **gerente** es el rol *Gerencia*, con el permiso de
  consulta. El *Administrador del sistema* recibe todos los permisos del módulo, como en los
  anteriores.
- **La caja es personal**: la abre, la opera y la cierra el mismo empleado (RN1). Nadie registra
  movimientos ni cierra la caja de otro (FR-035): el enunciado deja fuera al supervisor que cierra
  cajas ajenas, y no se agrega ningún mecanismo para operar la caja de otro.
- **La fecha de apertura, la de cada movimiento y la de cierre son instantes que registra el
  sistema** (RF1, RF5, RF7), en la hora de Argentina; no se tipean ni se corrigen. El enunciado pide
  trazabilidad de "quién registró cada movimiento y cuándo", y eso sólo lo garantiza el reloj del
  servidor.
- **El saldo final es el calculado** (RN6): no hay arqueo físico ni registro de diferencias entre el
  efectivo contado y el calculado —está fuera de alcance—, así que al cerrar no se tipea ningún
  conteo: se verifica el resumen y se confirma.
- **Un movimiento equivocado no se corrige dentro del módulo**: edición, anulación y eliminación
  están fuera de alcance. Registrar un movimiento inverso con un concepto aclaratorio es un
  movimiento como cualquier otro y no necesita nada adicional.
- **Las referencias son las entidades que ya existen**: la factura pendiente de cobro es la del
  Módulo 6 y la orden de pago es la del Módulo 9, tal cual existe hoy —sin estado de "pendiente"—. Este módulo las consume como referencia
  informativa y **no les agrega pantallas, campos ni estados**, ni modifica su estado en su módulo de
  origen (FR-012); una misma factura u orden de pago puede asociarse a más de un movimiento.
- **La consulta incluye un listado de cajas** (FR-026): el enunciado pide consultar movimientos "por
  caja específica" y que el gerente revise "los resultados de cierres", y las dos cosas necesitan
  poder elegir una caja y ver su resultado.
- **La caja no se identifica con un número visible propio**: se la reconoce por su responsable, su
  fecha de apertura y su estado.
- **El responsable dado de baja con la caja abierta** es un caso sin salida dentro del sistema en
  esta versión: nadie más puede cerrarla. Se anota como limitación conocida; si ocurre, se resuelve
  por fuera del sistema.
- Las pantallas siguen el sistema de diseño `gt-ui` (Principio VI de la constitución).
- Quedan fuera del alcance de este módulo, como dice el enunciado: la edición, anulación o
  eliminación de movimientos ya registrados; la reapertura de cajas cerradas; el arqueo físico y el
  registro de diferencias; las transferencias entre cajas de distintos empleados y el manejo
  multi-sucursal; la gestión de medios de pago diferenciados —cheques, tarjetas, transferencias—, que
  es otro módulo; los roles adicionales —supervisor que cierre cajas ajenas, aprobaciones—; la
  exportación de reportes y la impresión de comprobantes; y la integración contable automática.
