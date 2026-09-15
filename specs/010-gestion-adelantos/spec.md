# Feature Specification: Gestión de adelantos de sueldo (Módulo 10)

**Feature Branch**: `010-gestion-adelantos`

**Created**: 2026-09-14

**Status**: Draft

**Input**: User description: "Módulo gestión de adelanto de sueldo. Registrar los adelantos de sueldo que se le otorgan a choferes y empleados, con un circuito de aprobación y un historial de quién pidió qué, cuándo, por qué motivo y en qué estado quedó. El adelanto queda asociado a la persona para después descontarse de su liquidación de haberes. El problema que resuelve: hoy los adelantos se anotan sueltos y se pierde el rastro de cuánto se le adelantó a cada persona en el mes; con el circuito pendiente / aprobado / rechazado queda claro qué adelantos están firmes y cuáles no, y la anulación deja constancia del motivo en lugar de borrar el registro. Usuarios: el empleado administrativo registra, consulta, aprueba, rechaza y anula; el chofer o empleado es el beneficiario y no opera el sistema; Gerencia consulta los adelantos otorgados por persona y período. Requisitos: acceso sólo al empleado administrativo autenticado; desplegable obligatorio de tipo de persona (chofer o empleado) y desplegable obligatorio de personas dependiente del tipo; fecha propuesta en el día actual y modificable; motivo obligatorio; importe mayor a 0; persona registrada y activa; el adelanto nace pendiente; marcar campos con formato incorrecto sin perder lo cargado; listado con fecha, persona, motivo, importe y estado; filtros por persona, rango de fechas o estado; detalle; aprobar o rechazar sólo un pendiente; motivo obligatorio al rechazar; anular un aprobado no aplicado a una liquidación de haberes, con confirmación y motivo, cancelable; registrar la operación de cada cambio de estado. Reglas: importe mayor a 0; sólo personas registradas y activas; el desplegable de personas depende del tipo; todo adelanto nace pendiente; sólo se aprueba o rechaza un pendiente; el rechazo exige motivo; sólo se anula un aprobado no aplicado; la anulación exige confirmación y motivo; los adelantos no se borran. Puntos a definir: quién aprueba; el estado anulado en listado y filtros; dónde se marca un adelanto como aplicado a la liquidación de haberes; si la entrega del dinero genera un egreso de caja; topes y acumulación; si un rechazado se corrige o se carga uno nuevo."

## Clarifications

### Session 2026-09-14

- Q: El padrón de choferes del Módulo 3 incluye a los de G&T Logística S.A. y a los de los
  transportistas externos, a quienes les paga su fletero. ¿Los choferes de transportistas externos
  reciben adelantos de sueldo? → A: **No, sólo los choferes propios.** Es propio el chofer cuyo
  transportista tiene el CUIT de la empresa emisora configurada en el Módulo 6, la misma regla con la
  que el Módulo 9 reconoce a un fletero. Sin empresa emisora configurada no se ofrece ningún chofer; los
  empleados no dependen de eso (FR-002, FR-002a).
- Q: El enunciado pone al mismo empleado administrativo a registrar y a aprobar, sin control por
  oposición. ¿Quién aprueba y rechaza? → A: **El empleado administrativo**, con el mismo permiso de
  gestión con el que registra. Quien registra un adelanto puede aprobarlo o rechazarlo; no se agrega un
  permiso ni una regla de oposición (FR-022, FR-041).
- Q: La anulación sólo procede sobre un adelanto no aplicado a una liquidación de haberes, pero ningún
  módulo liquida haberes ni descuenta adelantos. ¿Dónde se marca un adelanto como aplicado? → A: **En
  ningún lado en esta versión.** Un adelanto `aprobado` siempre se puede anular. Cuando exista la
  liquidación de haberes, su spec agrega la regla como cambio enumerado sobre este módulo (FR-027,
  FR-028).
- Q: ¿El tipo que muestra el detalle de un adelanto —Chofer o Empleado— es el elegido al registrarlo o
  se recalcula con los datos vigentes de la persona? → A: **El elegido al registrarlo.** Se guarda en el
  adelanto, validado contra el padrón al guardar, y no cambia; apellido, nombre y DNI se siguen leyendo
  del padrón vigente (FR-010, FR-019, FR-020, FR-035).
- Q: ¿La fecha de un adelanto tiene un límite hacia atrás, o se acepta cualquier día anterior a hoy? →
  A: **No puede ser anterior al primer día del mes anterior**, derivado del día en curso y nunca escrito
  literal: permite cargar a principios de mes un adelanto de fines del mes pasado y rechaza un año mal
  tipeado (FR-005).
- Q: ¿Qué personas ofrece el filtro por persona del listado de adelantos? → A: **Sólo las que tienen al
  menos un adelanto registrado**, en cualquier estado y aunque hoy estén dadas de baja o hayan dejado de
  ser chofer propio (FR-014).
- Q: ¿Registrar o aprobar un adelanto tiene que dejar asentado que se le entregó el dinero a la
  persona? → A: **No.** No se registra ningún movimiento de dinero ni una marca de entregado: `aprobado`
  significa firme, no pagado. El egreso de caja queda como candidato para una spec futura (FR-023,
  FR-037).
- Q: ¿El sistema tiene que limitar o avisar cuando a una misma persona se le cargan varios adelantos o
  un importe alto en el mismo mes? → A: **Ni tope ni aviso.** El sistema no conoce los sueldos; el
  control lo hace quien aprueba con el listado filtrado por persona y mes y su total adelantado (FR-012,
  FR-016).
- Q: (decisión de alcance, durante la planificación) *Registrar adelanto* propone la fecha de hoy, y
  armar "hoy" en el formato con el que viaja al servidor ya existe copiado como función local en dos
  pantallas del Módulo 6 y en una del Módulo 9. ¿Se escribe una cuarta copia o se lleva a un lugar
  compartido? → A: **Se lleva a un lugar compartido en este módulo**, y las tres pantallas anteriores
  pasan a usarlo. Es la única excepción a que este módulo no toca módulos anteriores, se acota a tres
  cambios enumerados y **no cambia nada de lo que esas pantallas muestran** (*Assumptions*).
- Q: (decisión de alcance, durante la planificación) El enunciado pide confirmación explícita sólo para
  la anulación, pero `rechazado` también es un estado final. ¿El rechazo se confirma? → A: **Sí, igual que
  la anulación**: pide motivo y confirmación explícita en el mismo paso, y el sistema no rechaza sin la
  confirmación aunque se invoque la acción directamente (FR-024, US4 esc. 8).
- Q: (decisión de alcance, durante el análisis) Quien tiene sesión pero no el permiso y escribe la
  dirección de una pantalla del módulo ve el error de carga genérico —"Volvé a intentar en unos
  minutos"—, que es falso: por más que reintente, no va a entrar. ¿Qué tiene que ver? → A: **Un aviso de
  que no tiene permiso y a quién pedírselo**, en lugar de la pantalla, y lo mismo cuando una acción se
  rechaza por falta de permiso. Vale sólo para este módulo: los Módulos 5, 6 y 9 conservan su mensaje
  genérico, porque cambiarlos no está entre los tres cambios enumerados, y queda como candidato para una
  spec futura (FR-043, US6 esc. 5).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar un adelanto (Priority: P1)

El empleado administrativo elige primero si el beneficiario es chofer o empleado, y el desplegable de
personas le muestra sólo las de ese tipo que están activas. Elige a la persona, deja la fecha de hoy o la
cambia, escribe el motivo y el importe, y guarda. El adelanto queda registrado sobre esa persona en estado
`pendiente`: todavía no se considera otorgado.

**Why this priority**: es el valor de partida del módulo y lo que reemplaza las anotaciones sueltas. Sin
registro no hay nada que aprobar, consultar ni anular.

**Independent Test**: se prueba con un padrón de dos choferes propios activos, uno propio dado de baja,
uno activo de un transportista externo y dos empleados activos: al elegir *Chofer* aparecen sólo los dos
choferes propios activos, al elegir *Empleado* sólo los dos empleados, y al guardar un adelanto de
$150.000 queda `pendiente` sobre la persona elegida.

**Acceptance Scenarios**:

1. **Given** un padrón con choferes activos de G&T Logística S.A., un chofer propio dado de baja, un
   chofer activo de un transportista externo y empleados activos, **When** el empleado administrativo
   elige el tipo *Chofer*, **Then** el desplegable de personas muestra únicamente los choferes activos de
   G&T Logística S.A., identificados por apellido, nombre y DNI; ni el dado de baja ni el del
   transportista externo aparecen.
2. **Given** el mismo padrón, **When** elige el tipo *Empleado*, **Then** el desplegable de personas
   muestra únicamente los empleados activos.
3. **Given** el formulario sin tipo de persona elegido, **When** el empleado administrativo mira el
   desplegable de personas, **Then** no ofrece ninguna persona hasta que se elija el tipo.
4. **Given** una persona ya elegida, **When** el empleado administrativo cambia el tipo de persona,
   **Then** la persona elegida se vacía, el desplegable se vuelve a llenar con el nuevo tipo, y la fecha,
   el motivo y el importe cargados se conservan.
5. **Given** el formulario de registro recién abierto, **When** el empleado administrativo lo mira,
   **Then** el campo fecha viene completado con el día en curso y lo puede modificar.
6. **Given** el tipo de persona, la persona, la fecha, el motivo o el importe vacíos, **When** presiona
   *Guardar*, **Then** el sistema marca como obligatorio cada campo vacío, no registra el adelanto y
   conserva todo lo que ya estaba cargado.
7. **Given** un importe de $0 o de -$20.000, **When** presiona *Guardar*, **Then** el sistema marca el
   importe informando que tiene que ser mayor a cero y no registra el adelanto.
8. **Given** el día en curso 14/09/2026 y una fecha 15/09/2026, o una fecha 31/07/2026, **When** presiona
   *Guardar*, **Then** el sistema marca la fecha informando que no puede ser posterior a hoy ni anterior
   al 01/08/2026, y no registra el adelanto; con 01/08/2026 o 14/09/2026 la fecha se acepta.
9. **Given** el formulario completo para el chofer Juan Pérez, con fecha 14/09/2026, motivo "gastos
   médicos" e importe $150.000, **When** presiona *Guardar*, **Then** el adelanto queda registrado sobre
   Juan Pérez en estado `pendiente`, el sistema lo lleva al detalle del adelanto con la confirmación del
   guardado, y el historial muestra el registro con el usuario que lo cargó y el instante.
10. **Given** el formulario completo con una persona que otro usuario dio de baja mientras tanto, **When**
    presiona *Guardar*, **Then** el sistema rechaza el registro informando que la persona ya no está
    activa, no registra nada y conserva los datos cargados.
11. **Given** un pedido de registro con una persona inexistente, inactiva, que no es del tipo indicado o que es
    chofer de un transportista externo
    —invocando la acción directamente, sin pasar por la pantalla—, **When** el sistema lo recibe,
    **Then** lo rechaza informando el motivo y no registra nada.
12. **Given** la empresa emisora sin configurar, **When** el empleado administrativo elige el tipo
    *Chofer*, **Then** el sistema informa que primero hay que configurar la empresa emisora y dónde se
    hace, y no ofrece choferes; al elegir *Empleado* se siguen ofreciendo los empleados activos.

---

### User Story 2 - Consultar y filtrar los adelantos (Priority: P2)

El empleado administrativo entra a *Consultar adelanto* y ve cada adelanto con su fecha, la persona, el
motivo, el importe y el estado. Filtra por persona, por rango de fechas o por estado —o combinándolos—
para ver rápido cuánto se le adelantó a un chofer en el mes. Gerencia usa el mismo listado para controlar
cuánto se está adelantando sobre los sueldos.

**Why this priority**: es lo que resuelve el problema declarado —que se pierde el rastro de cuánto se le
adelantó a cada persona— y lo que usa Gerencia.

**Independent Test**: se prueba registrando adelantos de dos personas en agosto y septiembre, aprobando,
rechazando y anulando algunos, y aplicando cada filtro por separado y combinados, comprobando que el
listado muestra únicamente los que cumplen y que el total adelantado suma sólo los aprobados.

**Acceptance Scenarios**:

1. **Given** adelantos registrados, **When** el empleado administrativo entra a *Consultar adelanto*,
   **Then** cada fila muestra la fecha, la persona, el motivo, el importe en pesos y el estado.
2. **Given** adelantos de varias personas, **When** filtra por una persona, **Then** el listado muestra
   únicamente los adelantos de esa persona.
3. **Given** adelantos del 31/08/2026, del 01/09/2026, del 30/09/2026 y del 01/10/2026, **When** filtra
   desde el 01/09/2026 hasta el 30/09/2026, **Then** el listado muestra los del 01/09 y del 30/09 y no
   los otros dos: los dos extremos del rango se incluyen.
4. **Given** una fecha *desde* posterior a la fecha *hasta*, **When** aplica el filtro, **Then** el
   sistema marca el rango como inválido y no filtra.
5. **Given** adelantos `pendiente`, `aprobado`, `rechazado` y `anulado`, **When** filtra por cada estado,
   **Then** el listado muestra únicamente los de ese estado, y ningún adelanto aparece bajo dos estados.
6. **Given** adelantos de varias personas, fechas y estados, **When** combina los filtros de persona,
   rango de fechas y estado, **Then** el listado muestra únicamente los que cumplen todos a la vez.
7. **Given** a Juan Pérez con adelantos en septiembre de $150.000 y $50.000 aprobados, $30.000
   pendiente, $20.000 rechazado y $40.000 anulado, **When** se filtra por Juan Pérez y del 01/09/2026 al
   30/09/2026, **Then** el listado muestra los cinco y el total adelantado es $200.000: no suma
   pendientes, rechazados ni anulados.
8. **Given** el listado sin filtro de estado, **When** se lo mira, **Then** los adelantos `rechazado` y
   `anulado` siguen apareciendo, con la palabra de su estado y no sólo con un color distinto.
9. **Given** un filtro aplicado, **When** se mira el listado, **Then** los controles muestran qué filtro
   está aplicado, y un filtro sin resultados lo informa en lugar de mostrar una tabla vacía sin
   explicación.
10. **Given** más de 20 adelantos que cumplen los filtros, **When** se consulta el listado, **Then** se
    muestran de a 20 por página, y cambiar de página no repite ni saltea ninguno.
11. **Given** un usuario con rol *Gerencia*, **When** abre el listado y el detalle, **Then** los puede
    consultar y no ve ninguna acción de registrar, aprobar, rechazar ni anular.
12. **Given** un chofer propio con un adelanto `rechazado` que después fue dado de baja, un empleado
    activo sin adelantos y un chofer de un transportista externo, **When** se abre el filtro de persona,
    **Then** ofrece al chofer dado de baja y no ofrece ni al empleado sin adelantos ni al chofer externo.

---

### User Story 3 - Ver el detalle de un adelanto (Priority: P2)

El empleado administrativo abre un adelanto y ve a quién se le dio, cuándo, por qué, por cuánto, en qué
estado está y qué pasó con él: quién lo registró, quién lo aprobó, rechazó o anuló, cuándo y con qué
motivo.

**Why this priority**: es el historial que el objetivo pide —quién pidió qué, cuándo, por qué y en qué
estado quedó— y la pantalla desde la que se resuelve el adelanto.

**Independent Test**: se prueba abriendo un adelanto registrado, aprobado y anulado, y comprobando que
muestra sus datos, el motivo de la anulación y las tres operaciones con usuario e instante.

**Acceptance Scenarios**:

1. **Given** un adelanto del listado, **When** el empleado administrativo lo selecciona, **Then** ve la
   persona con su apellido, nombre y DNI, el tipo con el que se registró el adelanto, la fecha, el
   motivo, el importe y el estado.
2. **Given** un adelanto `rechazado` o `anulado`, **When** se abre su detalle, **Then** muestra el motivo
   del rechazo o de la anulación, además del motivo del adelanto.
3. **Given** un adelanto registrado, aprobado y anulado, **When** se abre su detalle, **Then** el
   historial muestra el registro, la aprobación y la anulación, cada una con el usuario que la hizo y el
   instante en que ocurrió, y la anulación con su motivo.
4. **Given** una persona a la que le corrigieron el apellido en el padrón después de registrado el
   adelanto, **When** se abre el detalle, **Then** muestra el apellido vigente del padrón.
5. **Given** un adelanto registrado a un chofer propio al que después le dieron de baja la ficha de
   chofer, **When** se abre el detalle, **Then** sigue mostrando el tipo *Chofer* con el que se registró.

---

### User Story 4 - Aprobar o rechazar un adelanto (Priority: P2)

Desde el detalle de un adelanto `pendiente`, se lo aprueba o se lo rechaza sin volver a cargar nada. Al
rechazar, el sistema pide el motivo para que después se pueda explicar la decisión.

**Why this priority**: es lo que separa un adelanto cargado de uno firme, que es el circuito que el
módulo existe para dejar claro.

**Independent Test**: se prueba sobre dos adelantos `pendiente`: se aprueba uno y se rechaza el otro con
motivo, comprobando los estados, el motivo visible y que ninguno de los dos vuelve a ofrecer aprobar ni
rechazar.

**Acceptance Scenarios**:

1. **Given** un adelanto `pendiente`, **When** el empleado administrativo entra a
   su detalle, **Then** están disponibles las acciones de aprobar y rechazar.
2. **Given** un adelanto `pendiente`, **When** lo aprueba, **Then** el estado pasa a `aprobado` y el
   historial registra la aprobación con el usuario y el instante.
3. **Given** un adelanto `pendiente`, **When** lo rechaza sin ingresar motivo, o con un motivo de sólo
   espacios, **Then** el sistema marca el motivo como obligatorio y no guarda el cambio.
4. **Given** un adelanto `pendiente`, **When** lo rechaza ingresando el motivo "ya tiene un adelanto
   pendiente del mes anterior", **Then** el estado pasa a `rechazado`, el motivo queda visible en el
   detalle y el historial registra el rechazo con el usuario, el instante y el motivo.
5. **Given** el pedido de rechazo abierto, **When** lo cancela, **Then** el adelanto sigue `pendiente` y
   no se registra nada.
6. **Given** un adelanto `aprobado`, `rechazado` o `anulado`, **When** se abre su detalle, **Then** no se
   ofrecen las acciones de aprobar ni rechazar, y si se las invoca directamente el sistema las rechaza
   informando el estado en que está.
7. **Given** un adelanto `pendiente` con el detalle abierto por dos usuarios, **When** uno lo aprueba y
   el otro lo rechaza al mismo tiempo, **Then** exactamente una operación se completa y la otra se
   rechaza informando el estado en que quedó el adelanto.
8. **Given** un pedido de rechazo con motivo pero sin confirmación explícita —invocando la acción
   directamente—, **When** el sistema lo recibe, **Then** no rechaza nada y el adelanto sigue `pendiente`.

---

### User Story 5 - Anular un adelanto aprobado (Priority: P3)

Un adelanto se aprobó por error —la persona equivocada, un importe mal tipeado—. El empleado
administrativo lo anula con un motivo: el adelanto no se borra, queda `anulado` y deja constancia de por
qué.

**Why this priority**: es la corrección de un error sobre un adelanto ya firme; es menos frecuente que
registrar, consultar y resolver.

**Independent Test**: se prueba anulando un adelanto aprobado con motivo, comprobando que queda
`anulado` con su motivo visible y sigue en el listado, y cancelando la anulación de otro, comprobando
que sigue `aprobado`.

**Acceptance Scenarios**:

1. **Given** un adelanto `aprobado`, **When** el empleado
   administrativo elige *Anular adelanto*, **Then** el sistema le pide un motivo y una confirmación
   explícita antes de anular.
2. **Given** el pedido de anulación abierto, **When** lo cancela, **Then** el adelanto conserva su estado
   `aprobado` y no se registra ningún cambio.
3. **Given** el pedido de anulación, **When** confirma sin motivo o con un motivo de sólo espacios,
   **Then** el sistema marca el motivo como obligatorio y no anula.
4. **Given** el motivo "cargado sobre la persona equivocada", **When** confirma la anulación, **Then** el
   adelanto queda `anulado`, el motivo queda visible en el detalle y el historial registra la anulación
   con el usuario, el instante y el motivo.
5. **Given** un adelanto `anulado`, **When** se mira el listado, **Then** sigue apareciendo con su estado.
6. **Given** un adelanto `pendiente`, `rechazado` o `anulado`, **When** se abre su detalle, **Then** no
   está la acción *Anular adelanto*, y si se la invoca directamente el sistema la rechaza informando el
   estado en que está.
7. **Given** un pedido de anulación con motivo pero sin confirmación explícita —invocando la acción
   directamente—, **When** el sistema lo recibe, **Then** no anula nada.

---

### User Story 6 - Acceso restringido (Priority: P1)

Sólo quien corresponde entra al módulo: el empleado administrativo registra, resuelve, anula y consulta;
Gerencia sólo consulta; nadie sin sesión entra a ninguna opción.

**Why this priority**: los adelantos son dinero que la empresa entrega a cuenta del sueldo; registrarlos,
aprobarlos o anularlos por error, o dejarlos a la vista de cualquiera, es un riesgo directo.

**Independent Test**: se prueba intentando entrar a *Registrar adelanto* y *Consultar adelanto* y operar
sobre un adelanto sin sesión, con un usuario de *Tráfico*, con uno de *Gerencia* y con uno de
*Administración de la empresa*.

**Acceptance Scenarios**:

1. **Given** un usuario no autenticado, **When** intenta acceder a cualquiera de las opciones del módulo,
   **Then** es redirigido al ingreso.
2. **Given** un usuario con rol *Administración de la empresa* o *Administrador del sistema*, **When**
   ingresa, **Then** ve en el menú *Registrar adelanto* y *Consultar adelanto* y puede registrar,
   consultar, aprobar, rechazar y anular.
3. **Given** un usuario con rol *Gerencia*, **When** ingresa, **Then** ve sólo *Consultar adelanto*, no ve
   ninguna acción de escritura, y si invoca directamente cualquiera de ellas el sistema la rechaza.
4. **Given** un usuario sin ninguno de los permisos del módulo, **When** ingresa, **Then** no ve ninguna
   de las opciones en el menú y el sistema rechaza cualquier intento de invocarlas directamente.
5. **Given** un usuario con sesión pero sin el permiso de la pantalla —*Tráfico* en cualquiera de las tres,
   o *Gerencia* en *Registrar adelanto*—, **When** escribe su dirección directamente, **Then** la pantalla
   informa que no tiene permiso y a quién pedírselo, no muestra el listado, el detalle ni el formulario, y
   no sugiere volver a intentar.

---

### Edge Cases

- Una persona es dada de baja después de registrado su adelanto: el adelanto no cambia, sigue en el
  listado y en el detalle, y se puede resolver y anular según su estado. La baja sólo impide registrarle
  adelantos nuevos.
- Una persona del padrón es de tipo chofer pero no tiene ficha de chofer, o su ficha de chofer está dada
  de baja: no aparece ni como chofer ni como empleado, y no puede recibir adelantos hasta que se la
  reactive como chofer.
- Una persona cargada como empleado se registra después como chofer en el Módulo 3: desde ese momento
  deja de aparecer bajo *Empleado*, aparece bajo *Chofer* sólo si su transportista es G&T Logística
  S.A., y sus adelantos anteriores no cambian: siguen mostrando el tipo *Empleado* con el que se
  registraron.
- Un chofer propio se reasigna a un transportista externo (FR-009 del Módulo 3): desde ese momento deja de
  ofrecerse bajo *Chofer*; sus adelantos anteriores no cambian y se siguen resolviendo y anulando según su
  estado.
- La empresa emisora no está configurada: no se ofrece ningún chofer y se informa dónde configurarla; los
  empleados se siguen ofreciendo.
- Le cambian el CUIT a la empresa emisora, o G&T Logística S.A. está cargada en el padrón con un CUIT
  distinto: cambian los choferes que se ofrecen a partir de ese momento y los adelantos existentes no
  cambian. El módulo no puede detectar el error; se corrige el CUIT en el padrón o en la configuración.
- El padrón no tiene ninguna persona activa del tipo elegido: el desplegable de personas lo informa en
  lugar de mostrarse vacío sin explicación, y no se puede guardar.
- Se registran varios adelantos sobre la misma persona en el mismo mes, pendientes o aprobados: se
  permite, sin tope ni aviso. El control se hace consultando el listado filtrado.
- El importe tiene más de dos decimales: el sistema lo marca como formato incorrecto y no registra.
- La fecha del adelanto se deja en un día anterior, por ejemplo para registrar un adelanto que se dio la
  semana pasada: se permite desde el primer día del mes anterior. No se permite una fecha anterior a ese
  día —un año mal tipeado se rechaza— ni posterior al día en curso.
- Se registra el 01/01/2027 un adelanto dado el 28/12/2026: se permite, porque el piso es el 01/12/2026.
  El mes anterior se calcula cruzando el año.
- Un adelanto rechazado por un error de carga: no se corrige ni se vuelve a presentar; se registra un
  adelanto nuevo, y el rechazado queda como constancia.
- Un adelanto pendiente cargado por error: se rechaza con motivo; no se anula, porque la anulación es
  sólo para adelantos aprobados.
- Dos usuarios resuelven a la vez el mismo adelanto —aprobar contra rechazar, dos aprobaciones, o una
  anulación contra otra—: exactamente una operación se completa y la otra se rechaza informando el
  estado en que quedó.
- El filtro por rango tiene sólo la fecha *desde* o sólo la fecha *hasta*: se aplica el extremo cargado
  y el otro queda abierto.

## Requirements *(mandatory)*

### Functional Requirements

#### Registro

- **FR-001**: El sistema DEBE ofrecer al registrar un adelanto un desplegable obligatorio de **tipo de
  persona** con exactamente dos valores: *Chofer* y *Empleado*.
- **FR-002**: El sistema DEBE ofrecer un desplegable obligatorio de **personas** que depende del tipo
  elegido y que no ofrece ninguna persona mientras no haya tipo elegido. Con *Chofer* DEBE listar las
  personas con ficha de chofer activa del Módulo 3 **cuyo transportista es G&T Logística S.A.**: aquel
  cuyo CUIT coincide con el de la empresa emisora configurada en el Módulo 6, la misma regla con la que
  el Módulo 9 reconoce a un fletero (FR-001 del Módulo 9). Los choferes de transportistas externos NO
  DEBEN ofrecerse. Con *Empleado* DEBE listar las personas activas del padrón del Módulo 2 de tipo
  empleado que no tienen ficha de chofer. Cada persona DEBE identificarse por apellido, nombre y DNI.
- **FR-002a**: Si la empresa emisora no está configurada, el sistema NO DEBE ofrecer ningún chofer y DEBE
  informar que primero hay que configurarla y dónde se hace. Los empleados DEBEN seguir ofreciéndose.
- **FR-003**: Al cambiar el tipo de persona, el sistema DEBE vaciar la persona elegida y volver a llenar
  el desplegable con el nuevo tipo, conservando la fecha, el motivo y el importe cargados.
- **FR-004**: Cuando no hay ninguna persona activa del tipo elegido, el sistema DEBE informarlo.
- **FR-005**: El sistema DEBE proponer como **fecha** del adelanto el día en curso de quien usa la
  pantalla, y DEBE permitir modificarla. La fecha es obligatoria, NO DEBE ser posterior al día en curso
  y NO DEBE ser anterior al primer día del mes anterior al día en curso. Ese piso DEBE derivarse del día
  en curso, en el servidor y en la pantalla, y nunca escribirse como fecha literal (convención [009]). El
  servidor DEBE evaluar los dos límites con el día en curso en la hora de Argentina; si difiere del de la
  pantalla, manda el servidor.
- **FR-006**: El sistema DEBE exigir un **motivo** del adelanto en texto libre. Un motivo de sólo
  espacios DEBE tratarse como vacío.
- **FR-007**: El sistema DEBE exigir un **importe** en pesos mayor a cero, con a lo sumo dos decimales.
- **FR-008**: Al guardar con campos vacíos o con formato incorrecto, el sistema DEBE marcar cada campo con
  su problema, NO DEBE registrar el adelanto y DEBE conservar todos los datos ya cargados.
- **FR-009**: Al guardar, el sistema DEBE verificar que la persona exista, esté activa y corresponda al
  tipo indicado según FR-002 —un chofer, además, de G&T Logística S.A.—, también cuando se invoca la acción directamente sin pasar por la pantalla.
  Si no se cumple, DEBE rechazar el registro informando el motivo y NO DEBE registrar nada.
- **FR-010**: Todo adelanto DEBE registrarse asociado a una única persona, con el **tipo de persona**
  elegido al registrarlo y validado según FR-009, y en estado `pendiente`.
- **FR-011**: Tras un registro exitoso, el sistema DEBE llevar al detalle del adelanto creado con la
  confirmación del guardado, y el formulario de registro NO DEBE quedar en pantalla.
- **FR-012**: El sistema NO DEBE limitar la cantidad ni el importe acumulado de adelantos de una misma
  persona, ni pendientes ni aprobados, y NO DEBE mostrar avisos por acumulación al registrar ni al
  aprobar. El control se hace con el listado filtrado y su total adelantado (FR-016).

#### Consulta

- **FR-013**: El listado DEBE mostrar de cada adelanto su fecha, la persona, el motivo, el importe en
  pesos y el estado.
- **FR-014**: El listado DEBE permitir filtrar por **persona**, por **rango de fechas** y por **estado**,
  en cualquier combinación. El filtro de persona DEBE ofrecer **sólo a las personas que tienen al menos
  un adelanto registrado**, en cualquier estado, estén activas o dadas de baja, identificadas por
  apellido, nombre y DNI; una persona sin adelantos NO DEBE ofrecerse. El rango DEBE incluir sus dos extremos y admitir uno solo de ellos; el de
  estado DEBE ofrecer `pendiente`, `aprobado`, `rechazado` y `anulado`.
- **FR-015**: El sistema DEBE rechazar un rango cuya fecha *desde* sea posterior a la fecha *hasta*,
  marcándolo, sin filtrar.
- **FR-016**: El listado DEBE mostrar el **total adelantado** de los adelantos que cumplen los filtros
  aplicados, sumando sólo los que están en estado `aprobado`, en todas las páginas y no sólo en la
  visible.
- **FR-017**: El listado DEBE paginarse de a 20 filas, ordenado del adelanto de fecha más reciente al más
  antiguo con un criterio que no permita que una fila se repita o se pierda entre páginas.
- **FR-018**: El listado DEBE mostrar qué filtros están aplicados e informar con un mensaje cuando ningún
  adelanto los cumple.
- **FR-019**: El detalle DEBE mostrar la persona con su apellido, nombre y DNI, el tipo con el que se
  registró el adelanto (FR-010), la fecha, el motivo, el importe, el estado, el motivo del rechazo o de la
  anulación cuando corresponde, y el historial.
- **FR-020**: El apellido, el nombre y el DNI de la persona que muestran el listado y el detalle DEBEN
  leerse del padrón vigente. El tipo NO DEBE recalcularse: es el guardado al registrar, aunque la persona
  haya dejado de ser chofer o empleado.

#### Aprobación y rechazo

- **FR-021**: El sistema DEBE permitir aprobar o rechazar un adelanto **sólo** mientras esté `pendiente`.
  En cualquier otro estado NO DEBE ofrecer las acciones y DEBE rechazarlas si se las invoca directamente,
  informando el estado en que está el adelanto.
- **FR-022**: Aprobar y rechazar DEBEN requerir el permiso de gestión (FR-041), el mismo con el que se
  registra. El sistema NO DEBE impedir que quien registró un adelanto sea quien lo aprueba o lo rechaza.
- **FR-023**: Aprobar DEBE pasar el adelanto a `aprobado` sin pedir datos adicionales. `aprobado`
  significa que el adelanto está firme, no que el dinero se entregó: el sistema NO DEBE registrar la
  entrega.
- **FR-024**: Rechazar DEBE exigir un motivo escrito y una confirmación explícita previa, también cuando
  se lo invoca directamente sin pasar por la pantalla; sin cualquiera de los dos NO DEBE cambiar nada. Al
  rechazar, el adelanto DEBE pasar a `rechazado` con su motivo.
- **FR-025**: Cancelar el pedido de rechazo DEBE dejar el adelanto `pendiente` sin registrar nada.
- **FR-026**: Cuando dos usuarios aprueban o rechazan el mismo adelanto al mismo tiempo, exactamente una
  operación DEBE completarse, y la otra DEBE rechazarse informando el estado en que quedó el adelanto.

#### Anulación

- **FR-027**: El sistema DEBE permitir anular un adelanto **sólo** mientras esté `aprobado`. En cualquier
  otro estado NO DEBE ofrecer la acción *Anular adelanto* y DEBE rechazarla si se la invoca directamente,
  informando el estado en que está.
- **FR-028**: En esta versión, ninguna condición además del estado DEBE impedir anular un adelanto
  `aprobado`: la liquidación de haberes no existe, así que un adelanto no puede estar aplicado a una.
- **FR-029**: La anulación DEBE exigir un motivo escrito y una confirmación explícita previa, también
  cuando se la invoca directamente sin pasar por la pantalla; sin cualquiera de los dos NO DEBE
  ejecutarse.
- **FR-030**: Cancelar la anulación DEBE dejar el adelanto `aprobado` y NO DEBE registrar ningún cambio.
- **FR-031**: Al anular, el adelanto DEBE pasar a `anulado` con su motivo.
- **FR-032**: Cuando dos usuarios anulan el mismo adelanto al mismo tiempo, exactamente una operación DEBE
  completarse, y la otra DEBE rechazarse informando el estado en que quedó el adelanto.

#### Estados e historial

- **FR-033**: El adelanto DEBE tener exactamente los estados `pendiente`, `aprobado`, `rechazado` y
  `anulado`, **excluyentes**: el filtro por estado DEBE devolver exactamente los adelantos que la fila
  muestra con ese estado.
- **FR-034**: Las únicas transiciones DEBEN ser `pendiente` → `aprobado`, `pendiente` → `rechazado` y
  `aprobado` → `anulado`. `rechazado` y `anulado` DEBEN ser **estados finales**.
- **FR-035**: Un adelanto NO DEBE borrarse ni modificarse: su persona, tipo, fecha, motivo e importe quedan
  como se registraron, y los `rechazado` y `anulado` DEBEN seguir en el listado y en el detalle.
- **FR-036**: El sistema DEBE registrar en el adelanto un historial con el registro, la aprobación, el
  rechazo y la anulación, cada uno con el usuario que la hizo y el instante en que ocurrió; el rechazo y
  la anulación DEBEN mostrar además su motivo. El historial NO DEBE poder editarse ni borrarse, y DEBE
  leerse desde el detalle.
- **FR-037**: Registrar, aprobar, rechazar o anular un adelanto NO DEBE cambiar datos de la persona ni de
  ningún otro módulo, y NO DEBE generar ningún movimiento de dinero ni de caja.

#### Presentación

- **FR-038**: Todo importe DEBE mostrarse en pesos argentinos con símbolo `$`, punto como separador de
  miles y coma decimal, y DEBE calcularse sin error de redondeo.
- **FR-039**: Ningún estado DEBE comunicarse sólo por color.
- **FR-040**: Todo resultado que aparezca sin que la pantalla cambie —el desplegable de personas que se
  vuelve a llenar, un filtro aplicado, un cambio de página, un cambio de estado— DEBE anunciarse de forma
  accesible.

#### Acceso

- **FR-041**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados y DEBE
  resolverlo por permiso y nunca por rol, con un permiso de **gestión** —registrar, aprobar, rechazar y anular— y uno de
  **consulta** —listado y detalle—. El menú DEBE resolver sus opciones *Registrar adelanto* y *Consultar
  adelanto* según lo que el servidor decide para cada usuario, sin que la pantalla evalúe permisos por su
  cuenta.
- **FR-042**: El permiso de gestión DEBE corresponder a los roles *Administración de la empresa* y
  *Administrador del sistema*. El de consulta DEBE corresponder a esos dos roles y además a *Gerencia*.
- **FR-043**: Quien no tenga el permiso correspondiente NO DEBE ver la opción ni la acción, y el sistema
  DEBE rechazarla igual si se la invoca directamente. Si llega a una pantalla del módulo escribiendo su
  dirección, la pantalla DEBE informar que no tiene permiso y a quién pedírselo, sin mostrar sus datos ni
  su formulario y sin sugerir que vuelva a intentar; lo mismo cuando una acción se rechaza por falta de
  permiso.
- **FR-044**: Un usuario no autenticado que intente acceder a cualquiera de las opciones del módulo DEBE
  ser redirigido al ingreso.

### Key Entities *(include if feature involves data)*

- **Adelanto**: dinero que G&T Logística le entrega a un chofer o empleado a cuenta de su sueldo. Incluye
  la persona, el tipo de persona con el que se registró, la fecha, el motivo, el importe en pesos, el
  estado y el motivo de rechazo o de anulación cuando corresponde. Es la entidad principal del módulo; no se borra ni se modifica, sólo cambia de
  estado.
- **CambioDeAdelanto**: registro de una operación sobre un adelanto —registro, aprobación, rechazo o
  anulación—, con el usuario que la hizo, el instante y el motivo cuando es un rechazo o una anulación.
  Pertenece a un único adelanto; todo adelanto tiene al menos uno, el de su registro. No se edita ni se
  borra.
- **Persona**: chofer o empleado del padrón del Módulo 2. Este módulo la consume para elegir al
  beneficiario, filtrar y mostrar, y no la administra.
- **Chofer**: ficha de chofer del Módulo 3 sobre una persona. Este módulo la consume para distinguir a
  los choferes de los empleados y saber si el chofer está activo y a qué transportista pertenece, y no la administra.
- **Transportista**: la misma entidad del Módulo 3. Este módulo sólo lee su CUIT para saber si un chofer
  es de G&T Logística S.A., y no la administra.
- **EmpresaEmisora**: la configuración única de G&T Logística S.A. del Módulo 6. Este módulo sólo lee su
  CUIT para distinguir a los choferes propios de los externos, y no la administra.

### Enumerations

- **EstadoAdelanto**: `pendiente`, `aprobado`, `rechazado`, `anulado`, excluyentes (FR-033). Todo
  adelanto nace `pendiente`; `rechazado` y `anulado` son finales (FR-034).
- **OperacionDeAdelanto**: `registro`, `aprobacion`, `rechazo`, `anulacion`. Clasifica cada entrada del
  historial (FR-036).
- **TipoBeneficiario**: `chofer`, `empleado`. Es el primer desplegable del registro (FR-001) y queda
  guardado en el adelanto tal como se eligió (FR-010, FR-020).

### Relationships

- **Persona 1 — * Adelanto**: todo adelanto pertenece a exactamente una persona; una persona puede tener
  muchos adelantos o ninguno, también varios en el mismo mes (FR-012).
- **Adelanto 1 — * CambioDeAdelanto**: todo adelanto tiene al menos el registro de su alta y a lo sumo
  tres entradas —registro, resolución y anulación—.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El empleado administrativo puede registrar un adelanto, aprobarlo y verlo como adelantado en
  el listado filtrado por persona y mes sin intervención técnica y sin anotar nada fuera del sistema.
- **SC-002**: El 100% de los adelantos registrados nace `pendiente`, y ninguno cuenta como adelantado
  hasta que se aprueba.
- **SC-003**: El 0% de los adelantos se registra con importe en cero o negativo, sin motivo, con fecha
  posterior al día en curso o anterior al primer día del mes anterior, o sobre una persona inactiva, del
  tipo equivocado o chofer de un transportista externo; el 100% de esos intentos es rechazado sin
  registrar nada y sin perder lo cargado.
- **SC-004**: Para cualquier combinación de filtros de persona, rango de fechas y estado, el listado
  muestra el 100% de los adelantos que la cumplen y ninguno que no, y el total adelantado es exactamente
  la suma de los aprobados entre ellos.
- **SC-005**: Una persona no técnica puede responder cuánto se le adelantó a una persona en un mes en
  menos de 30 segundos, desde el listado y sin sumar a mano.
- **SC-006**: El 100% de los rechazos y de las anulaciones tiene un motivo escrito visible en el detalle,
  y el 100% de los rechazos y de las anulaciones tuvo una confirmación explícita previa.
- **SC-007**: El 0% de los adelantos desaparece del listado: los rechazados y anulados siguen visibles con
  su estado.
- **SC-008**: El 100% de los intentos de aprobar o rechazar un adelanto que no está `pendiente`, o de
  anular uno que no está `aprobado`, es rechazado con un mensaje que dice por qué, y
  ninguno cambia nada.
- **SC-009**: Desde el detalle de cualquier adelanto, una persona no técnica puede decir a quién se le dio,
  cuándo, por qué, por cuánto, en qué estado está y quién lo registró, lo resolvió o lo anuló y cuándo, sin
  consultar otra pantalla.
- **SC-010**: El 100% de los intentos de operar sobre el módulo sin sesión o sin el permiso
  correspondiente es rechazado; un usuario sólo con el permiso de consulta no puede registrar, aprobar,
  rechazar ni anular.

## Assumptions

- La autenticación, el catálogo de roles (Tráfico, Administración de la empresa, Gerencia, Administrador
  del sistema) y el esquema de permisos con menú resuelto por el servidor ya existen (Módulos 1 y 2);
  este módulo agrega sus permisos y los asigna a los roles.
- El **empleado administrativo** del enunciado es el rol *Administración de la empresa* que ya existe,
  igual que en los Módulos 6 y 9. El *Administrador del sistema* recibe todos los permisos del módulo,
  como en los anteriores.
- El enunciado dice "acceso únicamente al empleado administrativo" y a la vez que **Gerencia consulta**
  los adelantos por persona y período. Se resuelve con dos permisos, como en el Módulo 9: operar es del
  administrativo; consultar, también de Gerencia.
- **Quién es chofer y quién es empleado** se decide con los datos que ya existen: es chofer la persona
  que tiene ficha de chofer activa en el Módulo 3 —la ficha es la única fuente de verdad sobre quién es
  chofer (research §1 del Módulo 3)—, y es empleado la persona activa del padrón del Módulo 2 de tipo
  empleado sin ficha de chofer. Así nadie aparece bajo los dos tipos. El módulo no agrega campos al
  padrón.
- **Sólo los choferes propios reciben adelantos** (FR-002): a los de un transportista externo les paga su
  fletero, no G&T. **Limitación conocida**: depende de que G&T Logística S.A. esté cargada en el padrón
  con el mismo CUIT que en la configuración de la empresa emisora, igual que en el Módulo 9; sin empresa
  emisora configurada sólo se pueden registrar adelantos a empleados.
- **No hay control por oposición** (FR-022): quien registra un adelanto puede aprobarlo. Si se prefiere
  que apruebe Gerencia u otra persona distinta de quien lo cargó, es un permiso aparte, y queda anotado
  como candidato para una spec futura.
- **Un adelanto no puede estar "aplicado a una liquidación de haberes"** en esta versión, porque ese
  módulo no existe: un `aprobado` siempre se puede anular (FR-028). Cuando se especifique la liquidación
  de haberes, la regla del enunciado vuelve como cambio enumerado sobre este módulo (convención [006] de
  `AGENTS.md`), y esa spec decide dónde se marca un adelanto como aplicado.
- **El estado `anulado` se agrega al listado y a los filtros**, resolviendo la diferencia entre la
  consulta y la anulación del enunciado: los estados son cuatro.
- **La entrega del dinero no genera ningún movimiento de caja.** El sistema no tiene un módulo de caja, y
  registrar un egreso sería alcance que el enunciado no pide. Queda anotado como candidato para una spec
  futura.
- **No hay topes ni control de acumulación**: el sistema no limita el importe respecto del sueldo —no
  conoce sueldos— ni impide varios adelantos de la misma persona en el mismo mes (FR-012). El control lo
  hace quien aprueba mirando el listado filtrado.
- **Un adelanto rechazado no se corrige ni se vuelve a presentar**: `rechazado` es final y el caso se
  resuelve registrando un adelanto nuevo. Tampoco se edita un adelanto `pendiente`: si se cargó mal, se
  rechaza con motivo y se registra otro. Así lo que se aprobó es siempre exactamente lo que se registró.
- **Sólo se anula un adelanto `aprobado`**, como dice el enunciado. Un `pendiente` mal cargado se rechaza.
- El **total adelantado** del listado (FR-016) sale del objetivo y de la historia de usuario —"ver rápido
  cuánto se le adelantó a un chofer en el mes"— y de lo que consulta Gerencia; sin él, esa pregunta se
  responde sumando a mano. Suma sólo los `aprobado`, porque los pendientes no están firmes y los
  rechazados y anulados no se entregaron.
- La **fecha** del adelanto es el día en que se otorga o se pide, y admite días anteriores para cargar un
  adelanto dado antes, desde el primer día del mes anterior: alcanza para cerrar el mes pasado en los
  primeros días del siguiente, y deja afuera un año mal tipeado que contaría en un mes que nadie mira.
  No admite días posteriores al día en curso, porque un adelanto no se registra por adelantado. El rango de fechas del listado se evalúa sobre esa fecha, no sobre el instante de carga.
- **La aprobación no pide confirmación aparte**: es la acción que el enunciado pide resolver "sin tener
  que cargar nada de nuevo", y un aprobado por error tiene salida con la anulación, que sí pide motivo y
  confirmación.
- **El rechazo y la anulación se confirman también en el servidor**, porque no se deshacen: `rechazado` y
  `anulado` son finales (convención [005] de `AGENTS.md`). Los dos piden el motivo y la confirmación en el
  mismo paso.
- La aprobación no vuelve a verificar que la persona siga activa: la regla de persona activa del enunciado
  es del registro. Un adelanto pendiente de alguien dado de baja se puede aprobar o rechazar.
- El adelanto no lleva un número visible propio: el enunciado lo identifica por su fecha, persona, motivo e
  importe, y el detalle se abre desde la fila.
- El listado no tiene un filtro por tipo de persona; el filtro de persona ofrece a choferes y empleados
  juntos, identificados por apellido, nombre y DNI, y sólo a quienes tienen algún adelanto: elegir a
  alguien sin adelantos siempre daría un listado vacío.
- La paginación de 20 filas, el orden total del listado y el formato de respuesta paginada siguen la
  convención ya adoptada desde el Módulo 3.
- Las pantallas siguen el sistema de diseño `gt-ui` (Principio VI de la constitución).
- El padrón de personas proviene del Módulo 2, las fichas de chofer y los transportistas del Módulo 3 y la
  empresa emisora del Módulo 6; este módulo los consume tal como están y **no les agrega pantallas,
  campos, estados ni reglas**. La única excepción son **tres cambios** a pantallas de los Módulos 6 y 9,
  que no alteran nada de lo que muestran ni de cómo se comportan:
  1. La ficha de factura (Módulo 6) deja de armar por su cuenta la fecha de hoy que propone al registrar
     el cobro y usa la compartida que este módulo incorpora.
  2. El alta de factura (Módulo 6), lo mismo con la fecha de facturación que propone.
  3. El detalle de liquidación (Módulo 9), lo mismo con la fecha de pago que propone la orden de pago.

  Las tres copias locales arman la fecha con el año, el mes y el día locales, sin pasar por UTC, y la
  compartida conserva exactamente eso. Que las pruebas existentes de esas tres pantallas sigan pasando
  sin modificarse es la verificación de que el comportamiento no cambió. Ninguna miraba la fecha
  propuesta, así que antes de cada cambio se le agrega un caso que la busca, sin tocar los que ya tiene.
- Quedan fuera del alcance de este módulo: la liquidación de haberes y el descuento del adelanto sobre el
  sueldo, el movimiento de caja por la entrega del dinero, los topes de importe y de acumulación, la
  edición de un adelanto, la corrección y nueva presentación de un rechazado, un comprobante imprimible,
  la notificación al beneficiario, un portal para choferes y empleados y la exportación a archivo.
