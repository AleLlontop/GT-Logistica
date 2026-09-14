# Feature Specification: Gestión de liquidación a transportistas (Módulo 9)

**Feature Branch**: `009-gestion-liquidacion`

**Created**: 2026-09-14

**Status**: Draft

**Input**: User description: "Gestión de liquidación. Generar la liquidación mensual a transportistas externos (fleteros) agrupando automáticamente los viajes rendidos del período, en lugar de armarla a mano juntando planillas. El sistema calcula el importe total y deja trazable qué viajes componen cada liquidación y qué órdenes de pago la cancelan. El problema que resuelve: hoy el riesgo es liquidar dos veces el mismo viaje u olvidarse alguno. Usuarios: el empleado administrativo genera y consulta las liquidaciones; el transportista externo es el sujeto de la liquidación y recibe el detalle de qué viajes se le liquidan, con qué importe cada uno y el total; Gerencia consulta cuánto se le debe a cada fletero por período, según el estado pendiente o pagada. Requisitos: acceso sólo al empleado administrativo autenticado; desplegable obligatorio con los transportistas externos registrados; período mes/año obligatorio; listado de los viajes rendidos del transportista en el período con su importe; cálculo del importe total; validar al menos un viaje rendido antes de guardar; validar que ningún viaje agrupado pertenezca a otra liquidación; informar y bloquear si no hay viajes rendidos; crear la liquidación en estado pendiente; listar liquidaciones con período, transportista, importe total y estado; filtrar por transportista, período y estado de forma combinable; detalle con los viajes agrupados y las órdenes de pago asociadas. Reglas: sólo se agrupan viajes rendidos; un viaje pertenece a una sola liquidación; el importe total es la suma de los importes de los viajes; una liquidación corresponde a un único transportista y un único período; sin viajes rendidos no hay liquidación; toda liquidación nace pendiente."

## Clarifications

### Session 2026-09-14

- Q: El Módulo 6 pasa a `facturado` todo viaje incluido en una factura, y la regla del enunciado dice
  que sólo se agrupan viajes `rendido`. ¿Un viaje ya facturado al cliente se puede liquidar al
  transportista? → A: **Sí**. Se liquidan los viajes `rendido` y los `facturado`: cobrarle al cliente
  y pagarle al fletero son independientes, y con la regla literal un viaje facturado antes de
  liquidarse no se liquidaría nunca, que es justo el olvido que el módulo existe para evitar. La
  liquidación no cambia el estado del viaje ni toca el Módulo 6 (FR-004, FR-020).
- Q: El detalle muestra las órdenes de pago y cuánto resta pagar, pero ningún módulo construido las
  registra ni pasa una liquidación a `pagada`. ¿Quién las registra? → A: **Este módulo**, con un alta
  simple de orden de pago —fecha e importe— sobre una liquidación. Cuando lo pagado alcanza el total,
  la liquidación pasa a `pagada` sola, sin acción aparte (User Story 4, FR-036 a FR-044).
- Q: El padrón del Módulo 3 no distingue transportistas propios de externos: G&T Logística S.A. es un
  transportista más (FR-004 del Módulo 3). ¿Cómo se reconoce a un fletero? → A: **Es externo todo
  transportista cuyo CUIT no coincide con el de la empresa emisora** configurada en el Módulo 6. No
  toca ningún módulo anterior; sin empresa emisora configurada no se puede generar (FR-001, FR-001a).
- Q: (decisión de alcance) ¿Entran en esta versión la edición y la anulación de una liquidación? →
  A: **Sí, las dos.** Editar permite quitar viajes y agregar los que estén disponibles del mismo
  transportista y período, sin cambiar ni el transportista ni el período. Anular exige motivo y
  confirmación previa, deja la liquidación `anulada` y libera sus viajes para liquidarse de nuevo. Las
  dos operan sólo sobre una liquidación `pendiente` sin órdenes de pago: una vez que se pagó algo, la
  liquidación ya no se toca (User Stories 5 y 6, FR-045 a FR-060).
- Q: ¿Se puede generar la liquidación de un mes que todavía no terminó? → A: **Sí**. Se acepta
  cualquier período de las listas, también el mes en curso, sin aviso. Los viajes que se rindan
  después se agregan editando la liquidación mientras siga editable, o van a una segunda liquidación
  del mismo período (FR-002, Edge Cases).
- Q: ¿El listado muestra también cuánto resta pagar de cada liquidación, además del importe total? →
  A: **Sí**, con la columna *Resta pagar* después del importe total. Con pagos parciales el importe
  total ya no es lo que se debe, y es la columna con la que Gerencia responde "cuánto se le debe a
  cada fletero" sin abrir cada liquidación. En una `anulada` no se muestra importe por pagar, porque
  no se debe nada (FR-021, FR-027).
- Q: ¿Una orden de pago puede tener fecha de pago anterior al día en que se generó la liquidación? →
  A: **No**. La fecha de pago DEBE estar entre la fecha de generación de la liquidación y el día en
  curso, los dos incluidos. Un pago que cancela una liquidación no puede ser anterior a ella, y como
  la orden de pago no se modifica, un error de tipeo en la fecha no tendría corrección (FR-038).
- Q: (decisión de alcance, durante la planificación) El CUIT con guiones se muestra en tres lugares de
  este módulo, y el formato existe copiado como función local en una pantalla del Módulo 3 y en otra
  del Módulo 5. ¿Se escribe una tercera copia o se lleva a un lugar compartido? → A: **Se lleva a un
  lugar compartido en este módulo**, y las dos pantallas anteriores pasan a usarlo. Es la única
  excepción a que este módulo no toca módulos anteriores, se acota a dos cambios enumerados y **no
  cambia nada de lo que esas pantallas muestran** (*Assumptions*).
- Q: Si dos personas tienen abierta la edición de la misma liquidación y las dos guardan, ¿qué pasa con
  la segunda? → A: **Se rechaza**, informando que otro usuario modificó la liquidación mientras tanto, y
  no cambia nada: hay que volver a abrir la edición y rehacer los cambios sobre la versión actual. Es el
  mismo criterio con el que se rechaza un viaje que otro ya tomó, y evita que una edición pise a otra
  sin aviso o deje el total distinto de la suma de sus viajes (FR-048, US5 esc. 9).
- Q: Si alguien pide generar una liquidación mandando menos viajes de los disponibles para ese
  transportista y período, sin pasar por la pantalla, ¿el sistema la acepta? → A: **Sí**. La lista
  enviada manda: el sistema verifica que cada viaje esté disponible (FR-011), pero no que estén todos.
  Agrupar todos los viajes es una regla **de la pantalla de generación**, que no ofrece selección
  individual; invocando la acción directamente se puede generar con un subconjunto, y los viajes que
  quedan afuera siguen disponibles (FR-008).
- Q: Cuando se edita una liquidación, ¿el historial registra qué viajes se quitaron y cuáles se
  agregaron, o alcanza con quién y cuándo? → A: **Registra también los viajes.** Cada entrada de
  edición guarda, además del usuario y el instante, los números de los viajes quitados y los agregados.
  Una edición cambia cuánto se le paga a un tercero, y con eso la composición de la liquidación se
  puede reconstruir en cualquier momento —que es lo que el objetivo pide trazar— sin copiar importes
  ni composiciones enteras (FR-033, SC-011).
- Q: (tras `/speckit-analyze`) El diálogo de orden de pago propone la fecha de hoy y lo que resta
  pagar, y ningún requisito lo pedía. ¿Se escribe en la spec o se quita? → A: **Se escribe en la
  spec**: el formulario propone los dos valores y los dos se pueden cambiar. La confirmación previa del
  backend ya muestra el saldo resultante antes de registrar, así que el valor propuesto no convierte un
  pago parcial en total sin que nadie lo vea (FR-036).
- Q: (tras `/speckit-analyze`) La liquidación de un fletero dado de baja —o que dejó de ser externo
  porque cambió un CUIT— ¿se sigue pudiendo editar? → A: **No.** Se sigue viendo, pagando y anulando,
  pero la edición se rechaza informando que el transportista ya no se puede liquidar. Agregar viajes a
  un fletero que ya no opera con la empresa no tiene caso de uso, y una liquidación mal armada de ese
  fletero se corrige anulándola (FR-045, US5 esc. 7).
- Q: (tras `/speckit-analyze`) Si mientras alguien edita otro usuario registra un pago o anula la
  liquidación, ¿la edición se rechaza con "cambió mientras tanto"? → A: **No, con su motivo propio.**
  "Otro usuario guardó cambios" queda sólo para otra edición; un pago, una anulación o un transportista
  que ya no se puede liquidar informan el motivo de FR-045, que es lo que le dice a quien edita qué
  hacer (FR-048).
- Q: (tras `/speckit-analyze`) ¿El paso a `pagada` queda registrado en el historial? → A: **Sí**, con
  una entrada propia: quién registró la orden de pago que dejó el saldo en cero y cuándo, escrita en la
  misma operación. Las órdenes parciales no agregan entradas, porque ya las lista su propia sección del
  detalle (FR-033, FR-041).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generar la liquidación de un fletero para un período (Priority: P1)

El empleado administrativo elige el transportista externo y el período —mes y año—, pide los viajes
y el sistema le trae exactamente los viajes rendidos de ese transportista en ese período que todavía
no están en ninguna liquidación vigente, cada uno con su importe, y el total. Controla ese total
contra lo que le reclama el fletero y guarda: la liquidación queda creada en estado `pendiente`, sin
órdenes de pago, y esos viajes no vuelven a ofrecerse mientras la liquidación esté vigente.

**Why this priority**: es el valor central del módulo y lo que elimina los dos riesgos que el
enunciado nombra: liquidar dos veces el mismo viaje y olvidarse alguno. Sin esta historia no hay
nada que consultar, pagar, editar ni anular.

**Independent Test**: se prueba cargando un fletero con cinco viajes en 07/2026 —tres rendidos y dos
en curso—, generando su liquidación y comprobando que agrupa exactamente los tres rendidos con un
total igual a la suma de sus importes, que queda `pendiente` y que al volver a pedir los viajes de
ese fletero y período ya no aparece ninguno.

**Acceptance Scenarios**:

1. **Given** el fletero "Transportes Díaz" con tres viajes rendidos en 07/2026, **When** el empleado
   administrativo lo elige junto con ese período y pide los viajes, **Then** el sistema lista esos
   tres viajes, cada uno con su número, su fecha, su origen, su destino y su importe, y muestra el
   importe total.
2. **Given** "Transportes Díaz" con cinco viajes en 07/2026 —tres rendidos y dos en curso—, **When**
   se piden los viajes de ese período, **Then** la lista muestra únicamente los tres rendidos.
3. **Given** "Transportes Díaz" con un viaje `rendido` y otro ya `facturado` al cliente en 07/2026,
   ninguno liquidado, **When** se piden los viajes de ese período, **Then** la lista muestra los dos.
4. **Given** tres viajes listados de $120.000, $95.000 y $140.000, **When** el empleado administrativo
   mira el total, **Then** el importe total es $355.000, y no hay forma de escribir un total distinto.
5. **Given** la empresa emisora configurada con el CUIT de G&T Logística S.A. y el padrón con G&T
   Logística S.A. y dos fleteros activos, **When** el empleado administrativo despliega la lista de
   transportistas, **Then** ve únicamente los dos fleteros, identificados por su razón social y CUIT.
6. **Given** la empresa emisora sin configurar, **When** el empleado administrativo abre la
   generación, **Then** el sistema le informa que primero hay que configurar la empresa emisora y
   dónde se hace, y no le ofrece transportistas.
7. **Given** el formulario de generación, **When** el empleado administrativo intenta pedir los
   viajes sin elegir transportista, sin mes o sin año, **Then** el sistema marca como obligatorio cada
   campo vacío y no avanza.
8. **Given** un fletero sin ningún viaje rendido en 08/2026, **When** el empleado administrativo pide
   los viajes de ese período, **Then** el sistema informa que no hay viajes rendidos para liquidar en
   ese período y la acción *Guardar* no está habilitada.
9. **Given** el viaje V-102 de "Transportes Díaz" ya incluido en la liquidación vigente LQ-2026-05,
   **When** se piden los viajes del mismo fletero y del mismo período, **Then** V-102 no aparece en la
   lista ni suma en el total.
10. **Given** viajes rendidos de otro transportista en el mismo período, **When** se elige
    "Transportes Díaz", **Then** ninguno de esos viajes aparece en la lista.
11. **Given** viajes rendidos de "Transportes Díaz" en 06/2026 y en 07/2026, **When** se elige
    07/2026, **Then** la lista muestra sólo los de 07/2026: una liquidación nunca mezcla períodos.
12. **Given** la lista con total $355.000, **When** el empleado administrativo presiona *Guardar*,
    **Then** se crea la liquidación en estado `pendiente` con importe total $355.000, sin órdenes de
    pago asociadas, y el sistema lo lleva al detalle de la liquidación recién creada con la
    confirmación del guardado.
13. **Given** una liquidación recién guardada, **When** se vuelven a pedir los viajes del mismo
    fletero y del mismo período, **Then** ninguno de los viajes que agrupó aparece.
14. **Given** una lista de viajes ya mostrada, **When** otro usuario guarda antes una liquidación que
    incluye alguno de esos viajes y el primero presiona *Guardar*, **Then** el sistema rechaza la
    operación indicando qué viaje ya está liquidado y en qué liquidación, y no crea nada.
15. **Given** una lista de viajes ya mostrada, **When** el empleado administrativo cambia el
    transportista, el mes o el año, **Then** la lista y el total se vacían y *Guardar* deja de estar
    habilitado hasta que vuelva a pedir los viajes.
16. **Given** un fletero cuyos únicos viajes disponibles en el período tienen importe en cero, **When**
    se piden los viajes, **Then** la lista los muestra con total $0,00, informa que no hay importe a
    liquidar y *Guardar* no está habilitado.
17. **Given** un pedido de guardado que incluye un viaje que no está `rendido` ni `facturado`, que es
    de otro transportista, que es de otro período o que ya pertenece a otra liquidación vigente
    —invocando la acción directamente, sin pasar por la pantalla—, **When** el sistema lo recibe,
    **Then** lo rechaza nombrando el viaje y el motivo, y no crea nada.

---

### User Story 2 - Consultar y filtrar las liquidaciones (Priority: P2)

El empleado administrativo abre *Consultar liquidación* y encuentra rápido las pendientes de pago:
cada fila muestra período, transportista, importe total y estado, y puede filtrar por transportista,
período y estado, combinando los tres. Gerencia usa el mismo listado para saber cuánto se le debe a
cada fletero en un período.

**Why this priority**: sin esta consulta las liquidaciones generadas quedan guardadas pero no se
pueden seguir; es lo que convierte la generación en control de lo que se debe.

**Independent Test**: se prueba generando liquidaciones de dos transportistas en dos períodos, pagando
una y anulando otra, y aplicando cada filtro por separado y los tres combinados, comprobando que el
listado muestra únicamente las que cumplen.

**Acceptance Scenarios**:

1. **Given** liquidaciones generadas, **When** el empleado administrativo entra a *Consultar
   liquidación*, **Then** cada fila muestra el número de liquidación, el período en formato
   `MM/AAAA`, el transportista, el importe total en pesos, lo que resta pagar y el estado.
2. **Given** liquidaciones `pendiente`, `pagada` y `anulada`, **When** se filtra por estado
   `pendiente`, **Then** el listado muestra únicamente las pendientes; lo mismo con `pagada` y con
   `anulada`, y ninguna liquidación aparece bajo dos estados.
3. **Given** liquidaciones de varios transportistas, **When** se filtra por un transportista,
   **Then** el listado muestra únicamente las de ese transportista.
4. **Given** liquidaciones de varios períodos, **When** se filtra por un período, **Then** el listado
   muestra únicamente las de ese mes y año.
5. **Given** liquidaciones de varios transportistas, períodos y estados, **When** se combinan los tres
   filtros, **Then** el listado muestra únicamente las que cumplen los tres a la vez.
6. **Given** el listado sin filtro de estado, **When** se lo mira, **Then** las liquidaciones
   `anulada` aparecen atenuadas y con la palabra de su estado, no sólo con un color distinto.
7. **Given** un filtro aplicado, **When** el empleado administrativo mira el listado, **Then** los
   controles muestran qué filtro está aplicado, y un filtro que no devuelve nada lo informa en lugar de
   mostrar una tabla vacía sin explicación.
8. **Given** más de 20 liquidaciones que cumplen los filtros, **When** se consulta el listado,
   **Then** se muestran de a 20 por página, y cambiar de página no repite ni saltea ninguna.
9. **Given** un usuario con rol *Gerencia*, **When** abre el listado y el detalle, **Then** los puede
   consultar, y no ve ninguna acción de generar, editar, anular ni registrar órdenes de pago.
10. **Given** una liquidación de $355.000 con una orden de pago de $200.000, otra `pagada` y otra
    `anulada`, **When** se mira el listado, **Then** la primera muestra $155.000 por pagar, la
    `pagada` $0,00 y la `anulada` ningún importe por pagar.

---

### User Story 3 - Ver el detalle de una liquidación (Priority: P2)

El empleado administrativo abre una liquidación y ve qué viajes la componen, con el importe de cada
uno, qué órdenes de pago la cancelan y cuánto resta pagar. Es además el detalle explícito que se le
puede dar al fletero: qué viajes se le liquidan, con qué importe y el total.

**Why this priority**: es lo que deja trazable la relación viaje ↔ liquidación ↔ orden de pago, que
es el objetivo declarado del módulo.

**Independent Test**: se prueba abriendo una liquidación de tres viajes con una orden de pago parcial
y comprobando que muestra los tres viajes con sus importes, un total igual a su suma, la orden de pago
y un resto a pagar igual al total menos lo pagado.

**Acceptance Scenarios**:

1. **Given** una liquidación del listado, **When** el empleado administrativo la selecciona, **Then**
   ve su número, el transportista con su razón social y CUIT, el período, la fecha en que se generó,
   el estado, el importe total, lo pagado y lo que resta pagar.
2. **Given** el detalle de una liquidación de tres viajes, **When** se lo mira, **Then** lista los
   tres viajes con su número, fecha, origen, destino e importe, y la suma de esos importes coincide
   con el importe total.
3. **Given** una liquidación recién generada, **When** se abre su detalle, **Then** la sección de
   órdenes de pago informa que no tiene ninguna, y lo que resta pagar es igual al importe total.
4. **Given** una liquidación de $355.000 con una orden de pago de $200.000, **When** se abre su
   detalle, **Then** la sección de órdenes de pago lista esa orden con su número, fecha e importe, y el
   detalle muestra $200.000 pagados y $155.000 por pagar.
5. **Given** una liquidación editada o anulada, **When** se abre su detalle, **Then** el historial
   muestra cada generación, edición, anulación y paso a `pagada` con el usuario que la hizo y el instante en que
   ocurrió, y la anulación con su motivo.
6. **Given** una liquidación `anulada`, **When** se abre su detalle, **Then** muestra el motivo de la
   anulación y los viajes que agrupaba, no muestra importe por pagar y no ofrece ninguna acción.
7. **Given** un transportista al que le corrigieron la razón social en el padrón después de generada
   la liquidación, **When** se abre el detalle, **Then** muestra la razón social vigente del padrón.

---

### User Story 4 - Registrar una orden de pago (Priority: P2)

El día que se le paga al fletero, el empleado administrativo abre la liquidación y registra la orden
de pago con su fecha y su importe. Puede pagar en una sola vez o en varias; cuando lo pagado alcanza
el total, la liquidación queda `pagada` sin que nadie tenga que marcarla.

**Why this priority**: sin órdenes de pago nunca se sabe cuánto resta pagar ni una liquidación llega a
`pagada`, y Gerencia no puede distinguir lo que se debe de lo que ya se pagó.

**Independent Test**: se prueba sobre una liquidación de $355.000 registrando una orden de $200.000,
comprobando que queda `pendiente` con $155.000 por pagar, y otra de $155.000, comprobando que queda
`pagada` sin ninguna acción adicional.

**Acceptance Scenarios**:

1. **Given** una liquidación `pendiente` de $355.000 sin órdenes de pago, **When** el empleado
   administrativo registra una orden de pago con fecha de hoy e importe $200.000 y confirma, **Then**
   la orden queda asociada con un número asignado por el sistema, la liquidación sigue `pendiente` y
   resta pagar $155.000.
2. **Given** esa misma liquidación con $155.000 por pagar, **When** se registra una orden de $155.000
   y se confirma, **Then** la liquidación pasa a `pagada`, resta pagar $0,00 y el historial suma el paso a `pagada` con el usuario y el instante.
3. **Given** una liquidación con $155.000 por pagar, **When** se intenta registrar una orden de
   $160.000, **Then** el sistema la rechaza informando que el importe supera lo que resta pagar, e
   indica cuánto es, y no registra nada.
4. **Given** el formulario de orden de pago, **When** se intenta confirmar sin fecha, sin importe, con
   importe en cero o negativo, con una fecha posterior al día en curso o con una fecha anterior a la
   generación de la liquidación, **Then** el sistema marca el campo con el problema y no registra
   nada.
5. **Given** el formulario completo, **When** el empleado administrativo pide registrar, **Then** el
   sistema le muestra el importe de la orden y lo que va a restar pagar después de ella, y le pide
   confirmación explícita; si cancela, no se registra nada.
6. **Given** una liquidación `pagada` o `anulada`, **When** se abre su detalle, **Then** no está la
   acción de registrar una orden de pago, y si se la invoca directamente el sistema la rechaza.
7. **Given** una liquidación con $155.000 por pagar, **When** dos usuarios registran al mismo tiempo
   órdenes de $100.000, **Then** exactamente una se registra y la otra es rechazada informando lo que
   resta pagar; en ningún caso lo pagado supera el total.
8. **Given** una orden de pago registrada, **When** se abre el detalle de la liquidación, **Then** no
   existe ninguna acción para modificarla ni eliminarla.

---

### User Story 5 - Editar una liquidación (Priority: P3)

El fletero reclama que un viaje no le corresponde a esta liquidación, o se rinde tarde un viaje del
mismo período. Mientras la liquidación esté `pendiente` y sin pagos, el empleado administrativo la
edita: quita los viajes que no van y agrega los que estén disponibles del mismo transportista y
período, y el total se recalcula.

**Why this priority**: sin edición, un error de armado sólo se corrige anulando y generando de nuevo,
que es más lento pero posible; por eso va después de generar, consultar y pagar.

**Independent Test**: se prueba sobre una liquidación de tres viajes quitando uno y agregando un viaje
rendido después, comprobando el total recalculado y que el viaje quitado vuelve a ofrecerse al
generar.

**Acceptance Scenarios**:

1. **Given** una liquidación `pendiente` sin órdenes de pago, **When** el empleado administrativo abre
   su edición, **Then** ve el transportista y el período sin poder cambiarlos, los viajes que agrupa
   con la opción de quitar cada uno, y los viajes disponibles de ese mismo transportista y período con
   la opción de agregar cada uno.
2. **Given** una liquidación de $355.000 con tres viajes, **When** se quita el viaje de $95.000 y se
   guarda, **Then** la liquidación queda con dos viajes y total $260.000, y el sistema lleva al detalle
   con la confirmación del guardado.
3. **Given** un viaje de $80.000 rendido después de generada la liquidación, del mismo transportista y
   período, **When** se lo agrega en la edición y se guarda, **Then** la liquidación suma el viaje y su
   importe al total.
4. **Given** un viaje quitado de una liquidación, **When** se piden los viajes de ese transportista y
   período para generar, **Then** ese viaje vuelve a aparecer.
5. **Given** la edición de una liquidación, **When** se intenta guardar sin ningún viaje o con total
   $0,00, **Then** el sistema lo rechaza informando el motivo y no cambia nada.
6. **Given** la edición abierta, **When** otro usuario guarda antes una liquidación con uno de los
   viajes que se están agregando, **Then** al guardar el sistema rechaza la edición nombrando el viaje
   y la liquidación que lo tiene, y no cambia nada.
7. **Given** una liquidación `pagada`, `anulada`, `pendiente` con al menos una orden de pago, o
   `pendiente` de un transportista dado de baja o que dejó de ser externo, **When** se abre su detalle,
   **Then** no está la acción de editar, y si se la invoca directamente el sistema la rechaza
   informando por qué.
8. **Given** una edición guardada, **When** se abre el historial de la liquidación, **Then** figura la
   edición con el usuario, el instante y los números de los viajes que quitó y que agregó.
9. **Given** dos usuarios con la edición de la misma liquidación abierta, **When** el primero quita un
   viaje y guarda, y después el segundo agrega otro y guarda, **Then** la edición del primero queda
   guardada, la del segundo se rechaza informando que la liquidación cambió mientras tanto, y la
   liquidación queda exactamente como la dejó el primero.

---

### User Story 6 - Anular una liquidación (Priority: P3)

Una liquidación se generó por error —el fletero equivocado, un período que no correspondía—. Mientras
esté `pendiente` y sin pagos, el empleado administrativo la anula con un motivo, y sus viajes vuelven
a estar disponibles para liquidarse otra vez.

**Why this priority**: es la salida para un error que la edición no cubre, porque el transportista y el
período no se editan.

**Independent Test**: se prueba anulando una liquidación de tres viajes con un motivo, comprobando que
queda `anulada` con su motivo visible y que los tres viajes vuelven a ofrecerse al generar.

**Acceptance Scenarios**:

1. **Given** una liquidación `pendiente` sin órdenes de pago, **When** el empleado administrativo pide
   anularla, **Then** el sistema le exige un motivo escrito y una confirmación explícita antes de
   anular.
2. **Given** el pedido de anulación, **When** se cancela o se confirma sin motivo, **Then** no se anula
   nada y la liquidación queda exactamente como estaba.
3. **Given** el motivo escrito y la confirmación, **When** se confirma, **Then** la liquidación queda
   `anulada` con el motivo, y sus viajes dejan de pertenecerle.
4. **Given** una liquidación anulada de tres viajes, **When** se piden los viajes de ese transportista
   y período para generar, **Then** los tres vuelven a aparecer y pueden agruparse en una liquidación
   nueva, que recibe un número distinto.
5. **Given** una liquidación `pendiente` con órdenes de pago, **When** se intenta anularla, **Then** el
   sistema lo rechaza informando cuántas órdenes de pago tiene y por cuánto, y no cambia nada.
6. **Given** una liquidación `pagada`, **When** se abre su detalle, **Then** no está la acción de
   anular, y si se la invoca directamente el sistema la rechaza informando que está pagada.
7. **Given** una liquidación `anulada`, **When** se intenta editarla, anularla de nuevo o registrarle
   una orden de pago, **Then** el sistema lo rechaza: `anulada` es un estado final.

---

### User Story 7 - Acceso restringido (Priority: P1)

Sólo quien corresponde entra al módulo: el empleado administrativo genera, edita, anula, registra
pagos y consulta; Gerencia sólo consulta; nadie sin sesión entra a ninguna opción.

**Why this priority**: la liquidación es dinero que la empresa le debe a terceros; generar, anular o
pagar por error, o dejarla a la vista de cualquiera, es un riesgo directo.

**Independent Test**: se prueba intentando entrar a *Generar liquidación* y *Consultar liquidación* y
operar sobre una liquidación sin sesión, con un usuario de *Tráfico*, con uno de *Gerencia* y con uno
de *Administración de la empresa*.

**Acceptance Scenarios**:

1. **Given** un usuario no autenticado, **When** intenta acceder a *Generar liquidación* o a
   *Consultar liquidación*, **Then** es redirigido al ingreso.
2. **Given** un usuario con rol *Administración de la empresa* o *Administrador del sistema*, **When**
   ingresa, **Then** ve en el menú las dos opciones y puede generar, consultar, editar, anular y
   registrar órdenes de pago.
3. **Given** un usuario con rol *Gerencia*, **When** ingresa, **Then** ve sólo *Consultar liquidación*,
   no ve ninguna acción de escritura, y si invoca directamente cualquiera de ellas el sistema la
   rechaza.
4. **Given** un usuario sin ninguno de los permisos del módulo, **When** ingresa, **Then** no ve
   ninguna de las dos opciones en el menú y el sistema rechaza cualquier intento de invocarlas
   directamente.

---

### Edge Cases

- Un viaje se rinde después de generada la liquidación de su período: al volver a pedir los viajes
  de ese transportista y período aparece solo, y se puede agregar a la liquidación existente si sigue
  editable (User Story 5) o generar una segunda liquidación del mismo transportista y período. La
  regla es que ningún viaje esté en dos liquidaciones vigentes, no que haya una sola liquidación por
  transportista y período.
- Se genera la liquidación del mes en curso antes de que termine: se permite (FR-002) y agrupa los
  viajes disponibles a ese momento. Los que se rindan después del mismo mes se agregan editándola
  mientras siga editable, o van a una segunda liquidación del mismo período.
- Todos los viajes disponibles del período ya están liquidados: se informa igual que cuando no hay
  viajes y *Guardar* no se habilita.
- Un viaje rendido con importe en cero (el Módulo 5 lo admite con confirmación): se lista con $0,00 y
  se agrupa como cualquier otro cuando la liquidación tiene otros viajes con importe. Si todos los
  viajes disponibles están en cero, no se puede guardar: no hay liquidación en $0 (FR-012a).
- Un viaje liquidado se factura después al cliente, o una factura que lo incluía se anula en el
  Módulo 6 y el viaje vuelve a `rendido`: la liquidación no se entera ni cambia, porque pagarle al
  fletero no depende de lo que pase con la factura.
- Dos empleados administrativos guardan al mismo tiempo liquidaciones —o una generación y una
  edición— que comparten un viaje: exactamente una operación se completa y la otra es rechazada
  nombrando el viaje y la liquidación que lo tiene.
- Entre que se mostró la lista y se presiona *Guardar*, se rinde otro viaje del mismo transportista y
  período: no entra en la liquidación que se está guardando, porque se guarda exactamente lo que se
  revisó; aparece la próxima vez que se pidan los viajes.
- Se registra una orden de pago mientras otro usuario tiene abierta la edición de esa liquidación: al
  guardar la edición, el sistema la rechaza porque la liquidación ya tiene órdenes de pago.
- Dos usuarios editan a la vez la misma liquidación: gana la primera edición que se guarda; la segunda
  se rechaza porque la liquidación cambió desde que se abrió, sin combinar ni pisar cambios (FR-048).
- El chofer de un viaje cambió de transportista después de hacerlo: el viaje se liquida al
  transportista que quedó registrado en el viaje al asignarlo (FR-028 del Módulo 5), no al actual del
  chofer.
- Un viaje sin chofer asignado no tiene transportista y no puede estar rendido, así que nunca se
  ofrece.
- Un transportista externo dado de baja: no se ofrece para generar liquidaciones nuevas, y sus
  liquidaciones siguen visibles, pagables y anulables según su estado, pero **no editables** (FR-045);
  el filtro del listado lo sigue ofreciendo.
- Al transportista G&T Logística S.A. le cargaron en el padrón un CUIT distinto del de la empresa
  emisora: aparece como externo. El módulo no puede detectarlo; se corrige el CUIT en el padrón o en
  la configuración de la empresa emisora.
- Le cambian el CUIT a la empresa emisora después de generadas liquidaciones: las liquidaciones
  existentes no cambian; cambia qué transportistas se ofrecen para generar a partir de ese momento, y
  las liquidaciones de un transportista que dejó de ser externo dejan de poder editarse (FR-045).

## Requirements *(mandatory)*

### Functional Requirements

#### Generación

- **FR-001**: El sistema DEBE ofrecer para generar una liquidación un desplegable obligatorio con los
  transportistas **externos activos** del padrón del Módulo 3, identificados por su razón social y su
  CUIT. Un transportista es **externo** cuando su CUIT no coincide con el CUIT de la empresa emisora
  configurada en el Módulo 6.
- **FR-001a**: Si la empresa emisora no está configurada, el sistema NO DEBE permitir generar
  liquidaciones y DEBE informar que primero hay que configurarla y dónde se hace.
- **FR-002**: El sistema DEBE pedir el período en **dos listas desplegables separadas**, las dos
  obligatorias: el mes con los doce valores `01` a `12` y el año de `2025` al año en curso, propuesto
  en el en curso, las mismas opciones que fija el Módulo 6 (FR-010 del Módulo 6). El sistema DEBE rechazar un período
  fuera de esas opciones aunque se lo invoque directamente. Dentro de esas opciones, el sistema DEBE
  aceptar cualquier período, también el mes en curso o uno posterior, sin aviso ni restricción.
- **FR-003**: Al pedir los viajes con algún campo vacío, el sistema DEBE marcar como obligatorio cada
  campo faltante y NO DEBE buscar viajes.
- **FR-004**: El sistema DEBE listar los **viajes disponibles** del transportista elegido en el
  período: los que tienen su **fecha de viaje** dentro del mes y el año elegidos, están en estado
  `rendido` o `facturado` y no pertenecen a ninguna liquidación vigente —una liquidación vigente es
  una no `anulada`—. NO DEBE listar viajes en estado `pendiente`, `en curso` ni `anulado`, ni viajes de
  otro transportista o de otro período. En este módulo, **"viaje rendido" incluye a los `facturado`**:
  facturado es un estado posterior a rendido (FR-051 del Módulo 6), y los textos que hablan de "viajes
  rendidos" se refieren a los dos.
- **FR-005**: El transportista de un viaje DEBE ser el que quedó registrado en el viaje al asignarle
  el chofer (FR-028 del Módulo 5), nunca el transportista actual del chofer.
- **FR-006**: Cada viaje listado DEBE mostrar su número, su fecha, su origen, su destino y su importe
  en pesos.
- **FR-007**: El sistema DEBE calcular y mostrar el importe total como la suma exacta de los importes
  de los viajes de la liquidación. El importe total NO DEBE poder escribirse ni modificarse desde
  ninguna pantalla ni invocando la acción directamente.
- **FR-008**: La pantalla de generación DEBE agrupar **todos** los viajes disponibles listados para el
  transportista y el período elegidos, y NO DEBE ofrecer selección de viajes individuales. El sistema
  DEBE aceptar la lista de viajes que recibe siempre que cada uno cumpla FR-011, sin exigir que estén
  todos los disponibles: los que queden afuera siguen disponibles para otra liquidación.
- **FR-009**: Cuando no haya viajes disponibles, el sistema DEBE informarlo con un mensaje que nombre
  al transportista y el período, y NO DEBE habilitar *Guardar*.
- **FR-010**: Si el empleado administrativo cambia el transportista, el mes o el año después de pedir
  los viajes, el sistema DEBE vaciar la lista y el total y NO DEBE habilitar *Guardar* hasta que se
  vuelvan a pedir.
- **FR-011**: Al guardar, el sistema DEBE crear la liquidación con exactamente los viajes que se
  revisaron, y DEBE volver a verificar cada uno contra FR-004. Si alguno falla, DEBE rechazar la
  operación entera nombrando el viaje y el motivo —y, cuando ya está liquidado, en qué liquidación—, y
  NO DEBE crear nada.
- **FR-012**: El sistema DEBE rechazar guardar una liquidación sin al menos un viaje.
- **FR-012a**: El sistema DEBE rechazar guardar una liquidación cuyo importe total sea $0,00,
  informando que no hay importe a liquidar, y NO DEBE habilitar *Guardar* en ese caso.
- **FR-013**: Una liquidación DEBE corresponder a **un único transportista y un único período**; el
  sistema NO DEBE aceptar viajes de más de un transportista ni de más de un período en la misma
  liquidación.
- **FR-014**: Un viaje DEBE pertenecer a **lo sumo a una liquidación vigente**, y esa garantía DEBE
  sostenerse también cuando dos usuarios generan o editan al mismo tiempo liquidaciones que comparten
  un viaje: exactamente una operación se completa.
- **FR-015**: Toda liquidación DEBE crearse en estado `pendiente` y sin órdenes de pago asociadas.
- **FR-016**: Toda liquidación DEBE recibir un número único generado por el sistema, no editable y no
  reutilizable, ni siquiera cuando la liquidación se anula.
- **FR-017**: El sistema DEBE registrar la fecha en que se generó cada liquidación y mostrarla en su
  detalle.
- **FR-018**: Guardar una liquidación DEBE ser todo o nada: o se crea con todos sus viajes asociados,
  o no se crea y ningún viaje queda asociado.
- **FR-019**: Tras un guardado exitoso, el sistema DEBE llevar al usuario al detalle de la liquidación
  creada con la confirmación del guardado, y el formulario de generación NO DEBE quedar en pantalla.
- **FR-020**: Generar, editar, anular o pagar una liquidación NO DEBE cambiar el estado, los datos ni
  el comportamiento de ningún viaje ni de ninguna factura en los Módulos 5 y 6.

#### Consulta

- **FR-021**: El listado DEBE mostrar de cada liquidación su número, el período en formato `MM/AAAA`,
  el transportista, el importe total en pesos, lo que resta pagar en pesos (FR-027) y el estado.
- **FR-022**: El listado DEBE permitir filtrar por transportista, período y estado, en cualquier
  combinación. Los tres DEBEN ser una selección exacta entre las opciones existentes; el filtro de
  transportista DEBE ofrecer a los transportistas externos activos y dados de baja, y el de período
  DEBE usar las mismas dos listas de FR-002.
- **FR-023**: El listado DEBE paginarse de a 20 filas, ordenado de la liquidación más reciente a la
  más antigua con un criterio que no permita que una fila se repita o se pierda entre páginas.
- **FR-024**: El listado DEBE mostrar qué filtros están aplicados, e informar con un mensaje cuando
  ninguna liquidación los cumple.
- **FR-025**: El detalle DEBE mostrar el número, el transportista con su razón social y CUIT, el
  período, la fecha de generación, el estado, el importe total, lo pagado, lo que resta pagar, los
  viajes agrupados —con número, fecha, origen, destino e importe de cada uno—, las órdenes de pago
  —con número, fecha e importe de cada una, informando cuando no hay ninguna—, el motivo de anulación
  cuando corresponde y el historial.
- **FR-026**: Los datos del transportista que muestran el listado y el detalle DEBEN leerse del padrón
  del Módulo 3, igual que los muestra el viaje.
- **FR-027**: Lo pagado DEBE ser la suma de los importes de las órdenes de pago de la liquidación, y
  lo que resta pagar DEBE ser el importe total menos lo pagado. En una liquidación `anulada` el
  listado y el detalle NO DEBEN mostrar importe por pagar (FR-059).
- **FR-028**: Una liquidación `anulada` DEBE seguir mostrando los viajes que agrupaba al anularse,
  aunque esos viajes ya no le pertenezcan y puedan estar en otra liquidación.

#### Estados

- **FR-029**: La liquidación DEBE tener exactamente los estados `pendiente`, `pagada` y `anulada`, que
  son **excluyentes**: ninguna liquidación está en dos a la vez, y el filtro por estado DEBE devolver
  exactamente las liquidaciones que la fila muestra con ese estado.
- **FR-030**: Una liquidación DEBE estar `pagada` cuando lo que resta pagar es $0,00 y no está
  anulada, sin que nadie tenga que marcarla; y `pendiente` mientras reste algo por pagar y no esté
  anulada.
- **FR-031**: `pagada` y `anulada` DEBEN ser **estados finales**: ninguna operación del módulo saca a
  una liquidación de ellos.
- **FR-032**: Ningún estado DEBE comunicarse sólo por color, y toda liquidación `anulada` que se
  muestre atenuada DEBE llevar además la palabra de su estado.

#### Historial

- **FR-033**: El sistema DEBE registrar en la liquidación un historial con cada generación, edición,
  anulación y **paso a `pagada`**, con el usuario que la hizo y el instante en que ocurrió —en el paso a
  `pagada`, quien registró la orden de pago que dejó el saldo en cero—; la entrada de la anulación DEBE
  mostrar además su motivo, y **cada edición los números de los viajes que quitó y los que agregó**. El
  historial NO DEBE poder editarse ni borrarse.
- **FR-034**: Cada orden de pago DEBE registrar el usuario que la cargó y el instante en que se cargó,
  además de su fecha de pago.
- **FR-035**: El historial y las órdenes de pago DEBEN poder leerse desde el detalle de la liquidación.

#### Órdenes de pago

- **FR-036**: El sistema DEBE permitir registrar una orden de pago sobre una liquidación `pendiente`,
  con **fecha de pago** e **importe** en pesos, los dos obligatorios. El formulario DEBE proponer como
  fecha el día en curso y como importe lo que resta pagar, y los dos DEBEN poder cambiarse.
- **FR-037**: El importe de una orden de pago DEBE ser mayor que cero y NO DEBE superar lo que resta
  pagar de la liquidación al momento de registrarla; al rechazarlo, el sistema DEBE informar cuánto
  resta pagar.
- **FR-038**: La fecha de pago NO DEBE ser posterior al día en curso ni anterior a la fecha de
  generación de la liquidación; las dos fechas límite se aceptan. Al rechazarla, el sistema DEBE
  informar el rango permitido.
- **FR-039**: Toda orden de pago DEBE recibir un número único generado por el sistema, no editable y no
  reutilizable.
- **FR-040**: Antes de registrar una orden de pago, el sistema DEBE mostrar su importe y lo que va a
  restar pagar después de ella, y DEBE pedir una confirmación explícita; cancelar NO DEBE registrar
  nada.
- **FR-041**: Cuando una orden de pago deja la liquidación sin nada por pagar, la liquidación DEBE
  pasar a `pagada` en la misma operación.
- **FR-042**: El sistema NO DEBE permitir registrar órdenes de pago sobre una liquidación `pagada` ni
  `anulada`, ni desde la pantalla ni invocando la acción directamente.
- **FR-043**: La suma de las órdenes de pago de una liquidación NUNCA DEBE superar su importe total,
  incluso cuando dos usuarios registran órdenes al mismo tiempo: si las dos juntas lo superan,
  exactamente una se registra.
- **FR-044**: Una orden de pago registrada NO DEBE poder modificarse ni eliminarse.

#### Edición

- **FR-045**: El sistema DEBE permitir editar una liquidación **sólo** mientras esté `pendiente`, no
  tenga ninguna orden de pago y su transportista siga siendo **externo y activo** (FR-001). En cualquier
  otro caso NO DEBE ofrecer la acción y DEBE rechazarla si se la invoca directamente, informando por
  qué: que está pagada, que está anulada, que ya tiene órdenes de pago o que el transportista ya no se
  puede liquidar.
- **FR-046**: La edición DEBE permitir quitar viajes de la liquidación y agregar viajes disponibles
  (FR-004) del mismo transportista y del mismo período. El transportista y el período NO DEBEN poder
  cambiarse.
- **FR-047**: La edición DEBE mostrar el importe total recalculado a medida que se quitan y agregan
  viajes.
- **FR-048**: Al guardar la edición, el sistema DEBE volver a verificar que la liquidación siga
  editable (FR-045), que **ningún otro usuario haya guardado otra edición desde que se abrió** y que
  cada viaje agregado siga disponible (FR-004). Si algo falla, DEBE rechazar la edición entera
  informando el motivo concreto —que otro usuario guardó cambios y hay que volver a abrir la edición, o
  el motivo de FR-045 si mientras tanto se registró un pago, se anuló o el transportista dejó de poder
  liquidarse— y NO DEBE cambiar nada.
- **FR-049**: La edición DEBE respetar las mismas reglas que la generación: al menos un viaje
  (FR-012), total mayor que cero (FR-012a), un único transportista y período (FR-013) y cada viaje en
  a lo sumo una liquidación vigente (FR-014).
- **FR-050**: Un viaje quitado de una liquidación DEBE volver a estar disponible para liquidarse.
- **FR-051**: Guardar la edición DEBE ser todo o nada, y tras un guardado exitoso el sistema DEBE
  llevar al detalle con la confirmación del guardado.
- **FR-052**: La edición NO DEBE cambiar el número, la fecha de generación ni el estado de la
  liquidación.

#### Anulación

- **FR-053**: El sistema DEBE permitir anular una liquidación **sólo** mientras esté `pendiente` y no
  tenga ninguna orden de pago. En cualquier otro caso NO DEBE ofrecer la acción y DEBE rechazarla si se
  la invoca directamente.
- **FR-054**: Al rechazar la anulación de una liquidación con órdenes de pago, el sistema DEBE informar
  cuántas órdenes tiene y la suma de sus importes; al rechazar la de una `pagada`, DEBE informar que
  está pagada.
- **FR-055**: La anulación DEBE exigir un motivo escrito, y NO DEBE ejecutarse sin él.
- **FR-056**: La anulación DEBE exigir una confirmación explícita previa, también cuando se la invoca
  directamente sin pasar por la pantalla; cancelar DEBE dejar la liquidación exactamente como estaba.
- **FR-057**: Al anular, la liquidación DEBE quedar `anulada` con su motivo, y sus viajes DEBEN dejar
  de pertenecerle y volver a estar disponibles para liquidarse, en la misma operación.
- **FR-058**: Una liquidación `anulada` NO DEBE borrarse: DEBE seguir en el listado y en el detalle.
- **FR-059**: Una liquidación `anulada` NO DEBE sumarse a lo que se le debe a un transportista en
  ninguna pantalla del módulo.
- **FR-060**: Anular DEBE ser todo o nada: o la liquidación queda anulada y todos sus viajes liberados,
  o no cambia nada.

#### Presentación

- **FR-061**: Todo importe DEBE mostrarse en pesos argentinos con símbolo `$`, punto como separador de
  miles y coma decimal, y DEBE calcularse sin error de redondeo.
- **FR-062**: Todo resultado que aparezca sin que la pantalla cambie —el mensaje de que no hay viajes,
  el total recalculado en la edición, un cambio de página, un filtro aplicado— DEBE anunciarse de
  forma accesible.

#### Acceso

- **FR-063**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados y DEBE
  resolverlo con **dos permisos**: uno de **gestión** de liquidaciones —generar, editar, anular y
  registrar órdenes de pago— y uno de **consulta** —listado y detalle—. La autorización DEBE evaluarse
  por permiso y nunca por rol, y el menú DEBE resolver sus dos opciones, *Generar liquidación* y
  *Consultar liquidación*, **sin agregar lógica de permisos en el frontend**: el servidor decide qué
  opciones existen para cada usuario, y ubicarlas en una sección del menú es presentación.
- **FR-064**: El permiso de gestión DEBE corresponder a los roles *Administración de la empresa* y
  *Administrador del sistema*. El de consulta DEBE corresponder a esos dos roles y además a
  *Gerencia*.
- **FR-065**: Quien no tenga el permiso correspondiente NO DEBE ver la opción ni la acción, ni en el
  listado ni en el detalle, y el sistema DEBE rechazarla igual si se la invoca directamente.
- **FR-066**: Un usuario no autenticado que intente acceder a cualquiera de las dos opciones DEBE ser
  redirigido al ingreso.

### Key Entities *(include if feature involves data)*

- **Liquidacion**: lo que G&T Logística S.A. le debe a un transportista externo por los viajes de un
  período. Incluye número (único, generado por el sistema, no reutilizable), período (mes y año),
  fecha de generación, importe total en pesos, estado y motivo de anulación cuando corresponde. Es la
  entidad principal del módulo: pertenece a exactamente un transportista, agrupa uno o más viajes y
  tiene cero o más órdenes de pago.
- **OrdenDePago**: pago que cancela total o parcialmente una liquidación. Incluye número (único,
  generado por el sistema), fecha de pago, importe en pesos, y el usuario y el instante en que se
  registró. Pertenece a una única liquidación. No se edita ni se borra.
- **CambioDeLiquidacion**: registro de una generación, una edición, una anulación o el paso a `pagada`
  de una liquidación.
  Incluye qué operación fue, el usuario que la produjo, el instante en que ocurrió, el motivo cuando
  es una anulación y los viajes quitados y agregados cuando es una edición. Pertenece a una única liquidación; una liquidación tiene al menos uno, el de su
  generación. No se edita ni se borra.
- **Viaje**: unidad de trabajo liquidada. Es la misma entidad del Módulo 5, con el estado `facturado`
  que le agregó el Módulo 6; este módulo la consume para armar la liquidación y registra a qué
  liquidación vigente pertenece. No la administra ni le cambia el estado.
- **Transportista**: empresa o persona que aporta choferes. Es la misma entidad del Módulo 3; este
  módulo la consume para elegir a quién se liquida, filtrar y mostrar, y no la administra.
- **EmpresaEmisora**: la configuración única de G&T Logística S.A. del Módulo 6. Este módulo sólo lee
  su CUIT para distinguir a los transportistas externos del propio, y no la administra.

### Enumerations

- **EstadoLiquidacion**: `pendiente`, `pagada`, `anulada`, excluyentes. Toda liquidación nace
  `pendiente` (FR-015); pasa a `pagada` cuando no resta nada por pagar (FR-030) y a `anulada` por la
  anulación (FR-057). `pagada` y `anulada` son finales (FR-031).
- **OperacionDeLiquidacion**: `generacion`, `edicion`, `anulacion`, `pagada`. Clasifica cada entrada del
  historial (FR-033).

### Relationships

- **Transportista 1 — * Liquidacion**: toda liquidación pertenece a exactamente un transportista
  externo; un transportista puede tener muchas liquidaciones o ninguna, incluso varias del mismo
  período.
- **Liquidacion 1 — * Viaje**: una liquidación vigente agrupa uno o más viajes, todos del mismo
  transportista y del mismo período; un viaje pertenece a lo sumo a una liquidación vigente (FR-014).
  Al anular la liquidación o al quitar un viaje en la edición, el viaje deja de pertenecerle y queda
  disponible (FR-050, FR-057).
- **Liquidacion 1 — * OrdenDePago**: una liquidación puede tener cero o más órdenes de pago, cuya suma
  nunca supera su importe total (FR-043); una orden de pago cancela una única liquidación.
- **Liquidacion 1 — * CambioDeLiquidacion**: toda liquidación tiene al menos un registro —el de su
  generación— y acumula uno por cada edición y por la anulación.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Partiendo de un fletero con viajes rendidos, el empleado administrativo puede generar su
  liquidación mensual, registrar su pago y verla `pagada` en el listado sin intervención técnica y sin
  juntar ninguna planilla.
- **SC-002**: El 0% de los viajes figura en más de una liquidación vigente, incluso cuando dos usuarios
  generan o editan al mismo tiempo liquidaciones que comparten un viaje.
- **SC-003**: El 100% de los viajes `rendido` o `facturado` de un transportista externo en un período
  que no están en una liquidación vigente aparece en la lista al pedirlos: ningún viaje liquidable
  queda afuera, tampoco los ya facturados al cliente ni los liberados por una edición o una anulación.
- **SC-004**: El 100% de las liquidaciones tiene un importe total exactamente igual a la suma de los
  importes de sus viajes, también después de editarlas, y ningún usuario puede escribir un total
  distinto desde ninguna pantalla ni invocando la acción directamente.
- **SC-005**: El 0% de las liquidaciones guardadas tiene cero viajes, total en $0,00, mezcla
  transportistas o mezcla períodos; el 100% de los intentos es rechazado sin crear ni cambiar nada.
- **SC-006**: El 100% de las liquidaciones recién generadas queda en estado `pendiente` y sin órdenes
  de pago asociadas.
- **SC-007**: En el 0% de las liquidaciones lo pagado supera el importe total, incluso con dos órdenes
  de pago registradas al mismo tiempo; y el 100% de las liquidaciones cuyo resto a pagar llega a $0,00
  figura como `pagada` sin que nadie la haya marcado.
- **SC-008**: El 100% de los intentos de editar o anular una liquidación `pagada`, `anulada` o con
  órdenes de pago es rechazado con un mensaje que dice por qué, y ninguno cambia nada.
- **SC-009**: El 100% de las anulaciones tiene un motivo escrito y una confirmación explícita previa,
  libera todos los viajes de la liquidación, y ninguna anulación cancelada produce cambios.
- **SC-010**: Para cualquier combinación de filtros de transportista, período y estado, el listado
  muestra el 100% de las liquidaciones que la cumplen y ninguna que no la cumpla, y ninguna liquidación
  aparece bajo dos estados.
- **SC-011**: Desde el detalle de cualquier liquidación, una persona no técnica puede decir qué viajes
  la componen, con qué importe cada uno, qué órdenes de pago tiene, cuánto resta pagar y quién la
  generó, editó, anuló o dejó pagada y cuándo —con los viajes que quitó y agregó cada edición—, sin consultar otra
  pantalla.
- **SC-012**: El 100% de los intentos de generar, editar, anular o registrar una orden de pago sin
  sesión o sin el permiso de gestión es rechazado; un usuario sólo con el permiso de consulta no puede
  hacer ninguna de esas operaciones.
- **SC-013**: El empleado administrativo puede controlar el total de un fletero contra su reclamo
  antes de guardar: el total y el detalle por viaje están a la vista en la misma pantalla donde se
  guarda, tanto al generar como al editar.
- **SC-014**: El 0% de las liquidaciones generadas corresponde al transportista cuyo CUIT es el de la
  empresa emisora.

## Assumptions

- La autenticación, el catálogo de roles (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) y el esquema de permisos con menú resuelto por el servidor ya existen
  (Módulos 1 y 2); este módulo sólo agrega sus dos permisos y los asigna a los roles.
- El **empleado administrativo** del enunciado es el rol *Administración de la empresa* que ya existe
  en el sistema, igual que en el Módulo 6; no se crea un rol nuevo. El *Administrador del sistema*
  recibe los dos permisos, como en todos los módulos anteriores.
- El enunciado dice "acceso únicamente al empleado administrativo" y a la vez que **Gerencia consulta**
  cuánto se le debe a cada fletero. Se resuelven con los dos permisos: operar es sólo del
  administrativo; consultar, también de Gerencia (FR-064).
- **Anular no lleva un permiso aparte**, a diferencia de la factura del Módulo 6, que la reserva al
  *Administrador del sistema*. La anulación de una liquidación sólo procede sin pagos registrados,
  libera los viajes para volver a liquidarlos y queda en el historial con motivo y usuario; el
  enunciado pone al empleado administrativo a cargo de las liquidaciones y no pide separarla. Si se
  prefiere el criterio del Módulo 6, es un tercer permiso.
- Gerencia responde "cuánto se le debe a cada fletero por período" con el listado filtrado por
  transportista, período y estado, leyendo la columna *Resta pagar* de cada fila. No hay un cuadro de totales
  aparte ni exportación, porque el enunciado no los pide.
- El padrón de transportistas proviene del Módulo 3, los viajes del Módulo 5 y la empresa emisora del
  Módulo 6; este módulo los consume tal como están y **no les agrega pantallas, campos, estados ni
  reglas**. La única excepción son **dos cambios** a pantallas de los Módulos 3 y 5, que no alteran
  nada de lo que muestran ni de cómo se comportan:
  1. El listado de *Transportistas* (Módulo 3) deja de tener su propio formato de CUIT con guiones y
     usa el compartido que este módulo incorpora.
  2. El listado de *Clientes* (Módulo 5), lo mismo.

  Las dos copias locales son idénticas: el CUIT de once dígitos sale como `20-12345678-6` y cualquier
  otro valor sale sin tocar. Que las pruebas existentes de esas dos pantallas sigan pasando sin
  modificarse es la verificación de que el comportamiento no cambió. La de *Clientes* no miraba el
  CUIT, así que antes del cambio 2 se le agrega un caso que lo busca, sin tocar los que ya tiene.
- **Transportista externo** es todo transportista cuyo CUIT no es el de la empresa emisora. Depende de
  que G&T Logística S.A. esté cargada en el padrón con el mismo CUIT que en la configuración, como
  piden FR-004 del Módulo 3 ("con sus datos reales") y la configuración del Módulo 6.
- La regla "sólo se agrupan viajes rendidos" del enunciado se lee como "viajes ya rendidos": incluye
  los que después pasaron a `facturado` en el Módulo 6, que es un estado posterior a `rendido`
  (FR-051 del Módulo 6).
- El período se evalúa contra la **fecha del viaje**, igual que en la facturación (FR-016 del
  Módulo 6), y no contra la fecha en que se rindió.
- El importe que se liquida por cada viaje es el **importe del viaje** tal como quedó en el Módulo 5.
  El módulo no calcula tarifas del fletero, comisiones, retenciones ni descuentos: el enunciado define
  el total como la suma de los importes de los viajes.
- Los importes de los viajes liquidables no cambian, porque un viaje `rendido` o `facturado` es
  inmutable (FR-018 del Módulo 5 y FR-052 del Módulo 6): el total de una liquidación sigue siendo la
  suma de sus viajes en el tiempo.
- La **generación** agrupa todos los viajes disponibles, sin marcar y desmarcar: es la "agrupación
  automática" del enunciado y es lo que impide olvidarse alguno. Quitar un viaje puntual se hace
  después, en la **edición**, que es un paso explícito y queda en el historial. La regla es **de la
  pantalla**: el sistema acepta una lista parcial invocado directamente (FR-008), y un viaje que queda
  afuera no se pierde, porque sigue ofreciéndose en la próxima generación del mismo período.
- "No se crea una liquidación en $0" se toma literal: además de exigir al menos un viaje, se rechaza
  el total en cero (FR-012a). Un viaje en cero se liquida junto con otros viajes con importe.
- La **edición y la anulación sólo proceden sin órdenes de pago**. Una vez que se le pagó algo al
  fletero, cambiar los viajes podría dejar lo pagado por encima del total, y anular dejaría un pago
  sin liquidación. La corrección de una liquidación ya pagada, total o parcialmente, queda fuera de
  esta versión.
- La edición cambia **qué viajes** agrupa la liquidación, no **a quién ni de cuándo**: el transportista
  y el período no se editan. Una liquidación del fletero o del período equivocado se anula y se genera
  de nuevo.
- La edición no pide confirmación aparte, porque se deshace: un viaje quitado se puede volver a
  agregar mientras siga disponible. **Anular y registrar una orden de pago sí la piden**, porque no se
  deshacen: `anulada` y `pagada` son estados finales y una orden de pago no se modifica ni se elimina.
- El historial registra **quién y cuándo** de cada generación, edición, anulación y paso a `pagada`, con el motivo de
  la anulación y, **en cada edición, los números de los viajes quitados y agregados**. Es más que el
  historial de la factura del Módulo 6, que sólo guarda quién y cuándo, y la diferencia es a propósito:
  la factura no cambia de viajes después de emitida, la liquidación sí. No guarda importes ni la
  composición completa: con la composición actual y los cambios de cada edición, la anterior se
  reconstruye.
- La **orden de pago** tiene sólo fecha e importe, más su número y quién la cargó. No registra medio de
  pago, cuenta bancaria, comprobante adjunto ni retenciones, porque ninguna se pide. Tampoco se
  modifica ni se anula en esta versión: una orden de pago mal cargada no tiene corrección, y
  habilitarla queda anotada como candidata para una spec futura.
- Se permite pagar una liquidación en varias órdenes, porque "qué órdenes de pago la cancelan" está en
  plural y "cuánto resta pagar" sólo tiene sentido con pagos parciales.
- Los números del enunciado —LQ-2026-05, LQ-2026-07, V-102— son ilustrativos. El formato del número de
  liquidación y del de orden de pago se fija en el plan.
- Se permite más de una liquidación del mismo transportista y período, siempre con viajes distintos.
- El desplegable de generación ofrece sólo transportistas **activos**, con el mismo criterio con el que
  el Módulo 6 ofrece sólo clientes activos. **Limitación conocida**: un fletero dado de baja con viajes
  sin liquidar no puede recibir una liquidación nueva; las que ya tiene se siguen pagando y anulando,
  pero no se editan (FR-045).
- La liquidación no genera un documento imprimible ni se envía al fletero: el detalle en pantalla es
  lo que se le puede mostrar. Un comprobante de liquidación queda anotado como candidato para una spec
  futura.
- La paginación de 20 filas, el orden total del listado y el formato de respuesta paginada siguen la
  convención ya adoptada desde el Módulo 3.
- Las pantallas siguen el sistema de diseño `gt-ui` (Principio VI de la constitución).
- Quedan fuera del alcance de este módulo: la corrección de liquidaciones con pagos, la modificación y
  anulación de órdenes de pago, cambiar el transportista o el período de una liquidación, el cálculo de
  tarifas, retenciones o comisiones del fletero, el documento imprimible de la liquidación y de la
  orden de pago, el envío por correo, el portal del transportista, los totales por transportista en
  pantalla propia y la exportación a archivo.
