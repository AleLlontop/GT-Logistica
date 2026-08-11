# Feature Specification: Gestión de viajes (Módulo 5)

**Feature Branch**: `005-gestion-viajes`

**Created**: 2026-08-10

**Status**: Draft

**Input**: User description: "Gestión de viajes (Módulo 5) v1. El viaje es la unidad de trabajo de G&T Logística S.A.: un cliente pide llevar una carga de un origen a un destino y la empresa asigna un chofer y un vehículo. Hoy eso vive en planillas y remitos en papel, se pierde el rastro de qué viajes están en curso y cuáles se rindieron, y aparecen viajes asignados a choferes o vehículos sin documentación en regla. Este módulo registra cada viaje con su cliente, origen, destino, remito, carga e importe; controla que el chofer y el vehículo asignados estén habilitados; y sigue el ciclo de vida del viaje desde que se planifica hasta que se rinde. Incluye el padrón de clientes (razón social, CUIT, teléfono, email, dirección), la asignación de chofer y vehículo con bloqueo por documentación vencida y aviso por documentación próxima a vencer, los estados pendiente / en curso / rendido / anulado con historial de quién y cuándo, el listado con filtros por cliente, fecha, estado y transportista, y los totales de cantidad e importe por cliente y por transportista en un período. Fuera de alcance: facturación al cliente, liquidación al transportista, cálculo automático de tarifas, cotizaciones, GPS, hojas de ruta con paradas, combustible y gastos, notificaciones, portal de cliente y app de chofer, digitalización del remito firmado, y el ABM de choferes y vehículos, que se consumen de los Módulos 3 y 4."

## Clarifications

### Session 2026-08-10

- Q: CL11 dice que la corrección de un viaje ya rendido "la habilita sólo el Administrador del
  sistema", pero CA8 dice que en la ficha de un viaje rendido no hay ninguna acción para volverlo
  atrás ni anularlo, y RN7 que no se edita en sus datos económicos. ¿Hasta dónde llega esa excepción?
  → A: No hay excepción. Un viaje `rendido` es inmutable para todos los roles, incluido el
  Administrador del sistema: no se editan sus datos, no se lo anula y no se lo devuelve a un estado
  anterior. CL11 se cae de esta versión y la corrección de un viaje mal rendido queda anotada como
  candidata para una spec futura. Es lo más fiel a RN6 y RN7, y evita agregar un camino de escape que
  ningún criterio de aceptación describe (FR-018).
- Q: El estado operativo `disponible` del vehículo se deriva contra el día en curso (Módulo 4), pero
  RN12 y CL4 piden validar contra la fecha del viaje, que puede ser pasada. ¿Con qué fecha se arma la
  lista de choferes y vehículos asignables? → A: Todo contra la fecha del viaje. La lista ofrece
  choferes activos y vehículos activos cuyo **estado operativo guardado** es `disponible`, y toda la
  evaluación de documentación —el bloqueo y la advertencia— corre contra la fecha del viaje. Así un
  viaje retroactivo se puede cargar con la unidad que realmente lo hizo aunque hoy esté vencida, y
  RN12 queda como la única regla de fecha del módulo (FR-021, FR-024).
- Q: CL9 pide destacar el viaje que quedó `en curso` "durante muchos días sin rendirse". ¿Cuántos? →
  A: Cinco días corridos desde que el viaje pasó a `en curso`. Cubre holgadamente un viaje de larga
  distancia dentro del país con su regreso, así que lo que aparece destacado es casi siempre una
  rendición olvidada y no un viaje que sigue andando (FR-039).
- Q: ¿El transportista del viaje es una referencia al padrón del Módulo 3 o una copia de su nombre
  congelada al asignar? → A: Una referencia al padrón. El viaje guarda **a qué transportista
  pertenecía el chofer en el momento de asignarlo**, y esa referencia no se mueve si después el chofer
  cambia de transportista. Lo que sí sigue al padrón son los datos del transportista: si le corrigen
  la razón social, el viaje muestra la corregida. Es lo que RN13 quiere proteger —que el viaje no
  cambie de dueño cuando el chofer se muda—, y deja el filtro y el agrupamiento del reporte apoyados
  en el mismo padrón de siempre (FR-028, FR-041, FR-046).
- Q: Si a un viaje ya asignado le cambian la fecha y con la fecha nueva el chofer o el vehículo
  quedarían bloqueados por documentación vencida, ¿qué hace el sistema? → A: Rechaza el cambio de
  fecha. Revalida la asignación contra la fecha nueva y, si queda bloqueada, no guarda nada e informa
  qué documento de qué unidad lo impide; el operador decide si mueve la fecha a otro día o si cambia
  primero la unidad. Deja una sola regla en pie —nunca hay una asignación bloqueada guardada— en vez
  de una para el alta y otra para la edición (FR-022a).
- Q: Las advertencias que no bloquean —origen igual a destino, documentación próxima a vencer al
  asignar, importe en cero al rendir—, ¿se muestran antes de guardar pidiendo confirmación o llegan
  con el resultado? → A: Depende de si el paso se puede deshacer. **Rendir con importe en cero pide
  confirmación previa**, porque FR-018 deja el viaje inmutable y después ya no hay forma de corregir
  el importe. **Origen igual a destino y documentación próxima a vencer llegan con el resultado**, sin
  frenar el guardado, porque los dos se arreglan editando el viaje. El criterio es la reversibilidad
  del paso, no la gravedad del aviso (FR-015a).
- Q: ¿Asignar chofer y vehículo es una acción propia sobre el viaje o son dos campos más del
  formulario de edición? → A: Una acción propia, con su pantalla, y no forma parte del alta: primero
  se registra el viaje y después se le asigna. Es el mismo razonamiento del precedente [004] sobre el
  cambio de estado —corregir el destino de un viaje no puede tocar quién lo maneja—, y además la
  asignación es la única operación del módulo que devuelve bloqueos y advertencias por documentación,
  así que sacarla del guardado de datos deja las dos respuestas limpias (FR-019a).
- Q: ¿Dónde viven los totales por cliente y por transportista, y el listado lleva además su propio
  total? → A: En una pantalla propia del módulo, con dos cuadros —uno por cliente y otro por
  transportista— y **rango de fechas obligatorio**: sin rango elegido no calcula nada. El listado de
  viajes queda como está, sin fila de total. CA12 se verifica comparando las dos pantallas, que es lo
  que mide SC-008 (FR-046, FR-046a).

### Session 2026-08-10 (revisión de checklist)

Seis correcciones que salieron de auditar la calidad de los requisitos —no de una pregunta nueva de
negocio— con `checklists/ciclo-de-vida-e-integracion.md`. Cada una cerraba un hueco por el que dos
implementaciones distintas eran igual de defendibles.

- **CHK038 — La baja de cliente prohibía justo el caso que la historia pedía.** FR-006 rechazaba la
  baja de un cliente con "al menos un viaje no anulado", y un viaje rendido lo es: el único cliente
  dado de baja posible era el que nunca había operado, mientras US1 justifica la baja con "el que dejó
  de operar con la empresa". La restricción pasa a mirar sólo los viajes `pendiente` y `en curso`
  (FR-006, SC-009).
- **CHK030 — SC-004 prometía más de lo que el módulo controla.** Decía "en ningún momento existe un
  viaje guardado con documentación vencida a su fecha", pero un documento corregido o eliminado desde
  los Módulos 3 o 4 produce ese estado sin que este módulo intervenga. La garantía se acota a las
  operaciones propias (FR-022a, SC-004).
- **CHK021 — No hay asignación parcial.** El chofer y el vehículo se asignan juntos; un viaje tiene
  los dos o no tiene ninguno. US4 esc. 2 suponía un viaje "sin chofer o sin vehículo", estado que sólo
  se alcanzaba con asignación parcial (FR-019b).
- **CHK022 — La exclusividad se verificaba en un solo camino.** FR-026 estaba descrita alrededor del
  pase a `en curso`, y FR-019 permitía reasignar un viaje ya `en curso` sin mencionarla: reasignarle
  un chofer ocupado dejaba dos viajes andando con la misma persona. La verificación corre ahora en los
  dos caminos (FR-026a).
- **CHK016 — SC-005 nombraba mal el momento de la carrera.** Hablaba de "dos operadores que intentan
  la misma asignación", pero por FR-027 la asignación no ocupa a nadie: la exclusividad nace del pase
  a `en curso`, y ahora también de la reasignación de un viaje ya en curso (SC-005).
- **CHK003 — Faltaba decir qué se exige del chofer y del vehículo al arrancar.** FR-025 pedía sólo que
  estuvieran asignados, y FR-030 admite que la asignación sobreviva a la baja de la unidad. Los dos
  tienen que estar **activos** para poner el viaje `en curso`; lo que no frena es la documentación ni
  el estado operativo del vehículo, que se controlaron al asignar (FR-025).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mantener el padrón de clientes (Priority: P1)

El responsable de Tráfico administra el padrón de clientes de la empresa —razón social, CUIT,
teléfono, email y dirección— para poder asociarle viajes a cada uno. Corrige sus datos cuando
cambian, da de baja al que dejó de operar con la empresa y lo reactiva si vuelve.

**Why this priority**: Todo viaje pertenece obligatoriamente a un cliente y el padrón arranca vacío:
sin al menos un cliente cargado no se puede registrar ningún viaje. Es el punto de partida del
módulo.

**Independent Test**: Se puede verificar de forma completa e independiente abriendo la pantalla de
clientes con el padrón vacío, cargando dos clientes, y comprobando que ambos quedan disponibles para
elegir al registrar un viaje.

**Acceptance Scenarios**:

1. **Given** el padrón de clientes vacío, **When** el responsable de Tráfico abre la pantalla,
   **Then** ve un mensaje explícito de que todavía no hay clientes cargados, en vez de una tabla
   vacía sin explicación.
2. **Given** un CUIT que no existe en el padrón, **When** el responsable de Tráfico completa razón
   social, CUIT, teléfono y email y guarda, **Then** el cliente queda registrado, activo y
   disponible para elegir al registrar un viaje.
3. **Given** un CUIT que ya pertenece a otro cliente activo, **When** se intenta guardar, **Then** el
   sistema informa el duplicado y no crea ningún cliente.
4. **Given** un CUIT mal formado —con menos de once dígitos o con dígito verificador incorrecto—,
   **When** se intenta guardar, **Then** el sistema marca ese campo con el motivo puntual y no crea
   nada.
5. **Given** un cliente registrado, **When** el responsable de Tráfico corrige su razón social y
   guarda, **Then** el registro queda actualizado; conservar su propio CUIT no genera ningún
   conflicto.
6. **Given** un cliente sin ningún viaje `pendiente` ni `en curso` —aunque tenga viajes rendidos o
   anulados—, **When** el responsable de Tráfico pide darlo de baja, **Then** el sistema pide una
   confirmación explícita, y al confirmar el cliente queda inactivo, deja de ofrecerse al registrar
   viajes y su registro no se borra.
7. **Given** el pedido de confirmación de baja, **When** el responsable de Tráfico cancela, **Then**
   nada cambia.
8. **Given** un cliente con al menos un viaje `pendiente` o `en curso`, **When** se intenta darlo de
   baja, **Then** el sistema lo rechaza e informa cuántos viajes en curso o pendientes dependen de
   él.
9. **Given** un cliente dado de baja que vuelve a operar, **When** el responsable de Tráfico lo da de
   alta de nuevo, **Then** vuelve a aparecer en el listado por defecto y a ofrecerse al registrar
   viajes, con todos sus viajes históricos intactos.
10. **Given** un CUIT que pertenece a un cliente dado de baja, **When** se intenta registrarlo como
    cliente nuevo, **Then** el sistema lo rechaza e indica que hay que dar de alta de nuevo al
    cliente existente.

---

### User Story 2 - Registrar un viaje (Priority: P1)

El responsable de Tráfico registra el trabajo comprometido: elige el cliente, escribe el origen y el
destino, pone la fecha, y completa el número de remito, el detalle de la carga y el importe cuando
los tiene. El sistema le asigna un número de viaje propio y lo deja en estado `pendiente`.

**Why this priority**: Es el objetivo central del módulo. Sin el registro del viaje no hay a qué
asignarle chofer y vehículo, ni sobre qué informar estado ni totales.

**Independent Test**: Se puede verificar de forma independiente con al menos un cliente cargado,
completando el formulario con cliente, origen, destino y fecha, guardando, y comprobando que el
viaje aparece en el listado con estado `pendiente` y un número que el sistema asignó y que nadie
puede editar.

**Acceptance Scenarios**:

1. **Given** al menos un cliente activo, **When** el responsable de Tráfico completa cliente, origen,
   destino y fecha y guarda, **Then** el viaje queda registrado en estado `pendiente`, con un número
   asignado por el sistema, y aparece en el listado.
2. **Given** el formulario sin cliente, sin origen, sin destino o sin fecha, **When** se intenta
   guardar, **Then** el sistema marca el campo faltante con el motivo puntual y no crea nada.
3. **Given** el padrón de clientes sin ninguno activo, **When** el responsable de Tráfico abre el
   formulario de viaje, **Then** el sistema le informa que primero debe cargar un cliente y no le
   permite completar el alta.
4. **Given** un viaje registrado, **When** el responsable de Tráfico lo consulta o lo edita, **Then**
   el número de viaje se muestra pero nunca es editable.
5. **Given** el viaje 1041 anulado, **When** se registra el viaje siguiente, **Then** su número es
   1042: el número del viaje anulado no se reutiliza.
6. **Given** el campo de importe, **When** el responsable de Tráfico escribe un valor negativo y
   guarda, **Then** el sistema lo rechaza indicando que el importe no puede ser negativo.
7. **Given** un viaje cuyo importe todavía no está definido, **When** se guarda con importe en cero,
   **Then** el sistema lo acepta y el viaje queda registrado.
8. **Given** el remito 5567 ya cargado en el viaje 1039, **When** se intenta guardar otro viaje con
   ese mismo remito, **Then** el sistema informa el duplicado indicando el número del viaje que ya lo
   usa, y no guarda.
9. **Given** un viaje sin número de remito al momento del alta, **When** se guarda, **Then** el
   sistema lo acepta; el remito puede cargarse después mientras el viaje no esté rendido.
10. **Given** el mismo texto en origen y en destino, **When** se guarda, **Then** el viaje queda
    registrado y el sistema muestra la advertencia junto con la confirmación del guardado, sin pedir
    ningún paso extra, porque existen servicios dentro de la misma localidad.
11. **Given** una fecha anterior a hoy, **When** se guarda, **Then** el sistema acepta el viaje como
    carga retroactiva y lo señala explícitamente.
12. **Given** una fecha muy posterior a hoy, **When** se guarda, **Then** el sistema acepta el viaje
    como planificado, en estado `pendiente`.
13. **Given** un viaje en estado `pendiente` o `en curso`, **When** el responsable de Tráfico corrige
    sus datos y guarda, **Then** el registro queda actualizado con las mismas validaciones que rigen
    el alta.
14. **Given** un viaje ya asignado a un vehículo cuya VTV vence el 20/08, **When** el responsable de
    Tráfico intenta mover la fecha del viaje al 25/08, **Then** el sistema rechaza el cambio de fecha
    indicando qué documento de qué unidad lo impide, y el viaje queda con su fecha y su asignación
    anteriores.

---

### User Story 3 - Asignar un chofer y un vehículo habilitados (Priority: P1)

El responsable de Tráfico abre un viaje y elige quién lo hace y con qué unidad. El sistema le ofrece
únicamente choferes y vehículos en condiciones, le impide asignar uno con documentación vencida a la
fecha del viaje explicándole qué documento lo bloquea, y le avisa —sin frenarlo— cuando algún
documento está por vencer.

**Why this priority**: Es el control que justifica el módulo: hoy salen a la ruta unidades sin
documentación en regla porque nadie lo verifica al momento de asignar. Sin esto el viaje queda
registrado pero el problema original sigue.

**Independent Test**: Con un viaje ya registrado (User Story 2), un chofer con documentación en
regla, un chofer con un documento vencido y un vehículo con un documento por vencer, se verifica
comprobando que el primero se asigna sin objeción, el segundo es rechazado con el nombre del
documento que lo bloquea, y el tercero se asigna mostrando la advertencia.

**Acceptance Scenarios**:

1. **Given** un viaje registrado, un chofer activo y un vehículo disponible, ambos con documentación
   en regla a la fecha del viaje, **When** el responsable de Tráfico los asigna y guarda, **Then** el
   viaje queda con su chofer y su vehículo, y ambos se ven en el listado y en la ficha.
2. **Given** la pantalla de asignación, **When** el responsable de Tráfico despliega la lista de
   choferes, **Then** no aparece ningún chofer dado de baja, aunque tenga viajes históricos.
3. **Given** la pantalla de asignación, **When** el responsable de Tráfico despliega la lista de
   vehículos, **Then** no aparece ningún vehículo dado de baja ni ninguno en estado operativo
   `fuera de servicio`.
4. **Given** un vehículo con la VTV vencida a la fecha del viaje, **When** se intenta asignarlo,
   **Then** el sistema lo rechaza indicando qué documento lo bloquea y no guarda la asignación.
5. **Given** un chofer con un documento que todavía está vigente a la fecha del viaje pero dentro de
   la ventana de aviso de su tipo, **When** se lo asigna, **Then** la asignación queda guardada y el
   sistema muestra, junto con el resultado, una advertencia que nombra el documento afectado.
6. **Given** un viaje con fecha del mes que viene y un vehículo cuya VTV vence antes de esa fecha,
   **When** se intenta asignarlo, **Then** el sistema lo rechaza: la validación corre contra la fecha
   del viaje, no contra la fecha en que se carga.
7. **Given** un viaje `pendiente` o `en curso` ya asignado, **When** el responsable de Tráfico cambia
   el chofer o el vehículo por otro habilitado, **Then** la reasignación queda guardada.
8. **Given** un viaje `rendido` o `anulado`, **When** se lo consulta, **Then** no existe ninguna
   acción para reasignar su chofer ni su vehículo.
9. **Given** un chofer del Módulo 3 que pertenece a "Transporte Sur", **When** se lo asigna a un
   viaje, **Then** el viaje queda registrado con "Transporte Sur" como transportista.
10. **Given** ese mismo viaje ya asignado, **When** el chofer pasa después a G&T Logística S.A.,
    **Then** el viaje sigue figurando bajo "Transporte Sur", tanto en el listado como al filtrar por
    transportista.
11. **Given** un viaje con chofer asignado, **When** ese chofer se da de baja en el Módulo 3,
    **Then** el viaje conserva la asignación y la muestra señalada como inactiva; no se borra ni se
    reasigna sola.
12. **Given** un chofer sin ningún viaje `en curso`, **When** se lo asigna a dos viajes `pendiente`
    con la misma fecha, **Then** el sistema lo acepta: un viaje `pendiente` no ocupa al chofer.
13. **Given** un viaje con fecha del mes pasado y un vehículo cuya VTV estaba vigente en esa fecha
    pero venció después, **When** se intenta asignarlo, **Then** el sistema lo acepta, porque la
    documentación se evalúa contra la fecha del viaje y no contra el día en curso.
14. **Given** el formulario de alta de un viaje, **When** el responsable de Tráfico lo completa,
    **Then** no encuentra ahí dónde elegir chofer ni vehículo: el viaje se registra primero y se
    asigna después, desde su propia acción.
15. **Given** un viaje con chofer y vehículo ya asignados, **When** el responsable de Tráfico corrige
    su destino desde el formulario de datos y guarda, **Then** la asignación queda intacta, porque el
    formulario de datos no incluye chofer ni vehículo.
16. **Given** el viaje 1041 `en curso` con el chofer Gómez y el viaje 1042 también `en curso`,
    **When** el responsable de Tráfico intenta reasignarle el chofer Gómez al viaje 1042, **Then** el
    sistema lo rechaza e indica que Gómez ya está en el viaje 1041: la exclusividad se verifica
    también al reasignar, no sólo al poner en curso.
17. **Given** la pantalla de asignación, **When** el responsable de Tráfico elige sólo el chofer o
    sólo el vehículo, **Then** el sistema no habilita la acción: los dos se asignan juntos y un viaje
    nunca queda con uno solo de los dos.

---

### User Story 4 - Avanzar el viaje de pendiente a rendido (Priority: P1)

El responsable de Tráfico pone el viaje `en curso` cuando la unidad sale, y lo pasa a `rendido`
cuando el trabajo se cerró. El sistema le impide saltear pasos, le exige chofer y vehículo para
arrancar, no deja que la misma unidad esté en dos viajes a la vez, y deja registrado quién hizo cada
cambio y cuándo.

**Why this priority**: Es lo que hoy no se puede responder sin preguntar: en qué instancia está cada
viaje. Sin el ciclo de vida, el módulo es una planilla más.

**Independent Test**: Con un viaje asignado (User Story 3), se verifica pasándolo a `en curso`,
comprobando que un segundo viaje con el mismo chofer es rechazado, pasando el primero a `rendido`, y
comprobando que recién entonces el segundo puede ponerse `en curso`.

**Acceptance Scenarios**:

1. **Given** un viaje `pendiente` con chofer y vehículo asignados, **When** el responsable de Tráfico
   lo pone `en curso`, **Then** el viaje cambia de estado y el historial registra quién lo hizo y
   cuándo.
2. **Given** un viaje `pendiente` sin chofer ni vehículo asignados, **When** se intenta ponerlo
   `en curso`, **Then** la acción no está disponible o se rechaza indicando que falta asignar.
3. **Given** el viaje 1041 `en curso` con el chofer Gómez, **When** se intenta poner `en curso` el
   viaje 1042 con el mismo chofer, **Then** el sistema lo rechaza e indica el número del viaje que lo
   ocupa.
4. **Given** el viaje 1041 `en curso` con un vehículo, **When** se intenta poner `en curso` otro
   viaje con ese mismo vehículo, **Then** el sistema lo rechaza e indica el número del viaje que lo
   ocupa.
5. **Given** un viaje `en curso`, **When** el responsable de Tráfico lo pasa a `rendido`, **Then** el
   viaje cambia de estado, el historial lo registra, y su chofer y su vehículo quedan libres para
   otro viaje conservando la asignación en la ficha.
6. **Given** un viaje `en curso` con importe en cero, **When** se lo intenta pasar a `rendido`,
   **Then** el sistema todavía no lo rinde: advierte que el viaje quedará cerrado y sin importe y que
   después no se podrá corregir, y lo rinde recién cuando el responsable de Tráfico confirma.
7. **Given** esa advertencia, **When** el responsable de Tráfico cancela, **Then** el viaje sigue
   `en curso` con su importe en cero y puede completarlo antes de volver a rendirlo.
8. **Given** un viaje `rendido`, **When** se abre su ficha, **Then** no existe ninguna acción para
   editarlo, para reasignarlo, para volverlo a `en curso` ni a `pendiente`, ni para anularlo.
9. **Given** un viaje `rendido` y un usuario con rol *Administrador del sistema*, **When** intenta
   corregirle el importe, **Then** el sistema lo rechaza igual que a cualquier otro usuario e informa
   que el viaje está cerrado: un viaje rendido es inmutable para todos.
10. **Given** un viaje `pendiente`, **When** se intenta pasarlo directamente a `rendido`, **Then** el
    sistema lo rechaza: la acción no se ofrece y el guardado la rechaza igual si se la invoca.
11. **Given** un chofer con un documento que vence mientras su viaje ya está `en curso`, **When**
    pasa la fecha de vencimiento, **Then** el viaje sigue su curso normal y el sistema no lo
    interrumpe: el bloqueo se aplica al asignar, no en el medio del recorrido.
12. **Given** un viaje que pasó a `en curso` hace más de 5 días corridos y sigue sin rendirse,
    **When** el responsable de Tráfico abre el listado, **Then** el viaje figura destacado como
    demorado, con la palabra que lo explica y no sólo con un color, y su estado sigue siendo
    `en curso`.
13. **Given** cualquier viaje, **When** se abre su ficha, **Then** el historial muestra cada cambio
    de estado con el estado anterior, el nuevo, el usuario que lo produjo y la fecha y hora, empezando
    por el alta.
14. **Given** un viaje `pendiente` cuyo chofer o cuyo vehículo se dio de baja después de asignarlo,
    **When** se intenta ponerlo `en curso`, **Then** el sistema lo rechaza indicando cuál de los dos
    está dado de baja, y sólo arranca después de reasignarlo.
15. **Given** un viaje `pendiente` cuyo vehículo pasó a tener documentación vencida o quedó fuera de
    servicio después de asignarlo, **When** se lo pone `en curso`, **Then** el sistema lo acepta: la
    documentación se controla al asignar y no vuelve a frenar el viaje después.

---

### User Story 5 - Consultar, buscar y filtrar viajes (Priority: P1)

El responsable de Tráfico responde una consulta sin levantarse: filtra los viajes por cliente, rango
de fechas, estado y transportista, busca por origen, destino o razón social, y abre la ficha completa
de cualquier viaje con su chofer, su vehículo, su transportista y su historial de estados.

**Why this priority**: Es la operación que más se repite. Un registro que no se puede consultar no
resuelve el problema de "se pierde el rastro de qué viajes están en curso".

**Independent Test**: Se puede verificar de forma independiente cargando viajes de distintos
clientes, fechas, estados y transportistas, aplicando combinaciones de filtros y búsquedas, y
comprobando que el listado muestra exactamente lo esperado y la ficha completa lo detalla.

**Acceptance Scenarios**:

1. **Given** viajes registrados, **When** el responsable de Tráfico abre el listado, **Then** ve para
   cada viaje el número, la fecha, el cliente, el origen, el destino, el chofer, el vehículo, el
   transportista, el estado y el importe.
2. **Given** el listado de viajes, **When** se aplican filtros combinados por cliente, rango de
   fechas, estado y transportista, **Then** el listado muestra únicamente los viajes que cumplen
   todas las condiciones a la vez.
3. **Given** el listado de viajes, **When** el responsable de Tráfico escribe una parte del nombre de
   una localidad en la búsqueda, **Then** aparecen los viajes cuyo origen o destino la contienen, sin
   distinguir mayúsculas ni acentos.
4. **Given** el listado de viajes, **When** el responsable de Tráfico escribe una parte de la razón
   social de un cliente, **Then** aparecen los viajes de ese cliente.
5. **Given** un chofer que hizo viajes bajo "Transporte Sur" y hoy pertenece a G&T Logística S.A.,
   **When** se filtra por "Transporte Sur", **Then** esos viajes aparecen, porque el filtro usa el
   transportista que quedó registrado en el viaje.
6. **Given** un viaje del listado, **When** el responsable de Tráfico lo selecciona, **Then** ve su
   ficha completa con número, cliente, origen, destino, fecha, remito, detalle de carga, importe,
   estado, chofer, vehículo, transportista, motivo de anulación si corresponde, e historial de
   estados.
7. **Given** más de 20 viajes que cumplen los filtros aplicados, **When** el responsable de Tráfico
   consulta el listado, **Then** ve la primera página con 20 filas, el total de coincidencias y la
   forma de avanzar a las páginas siguientes.
8. **Given** un filtro o una búsqueda que no coincide con ningún viaje, **When** se aplica, **Then**
   el sistema muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin
   explicación.
9. **Given** el listado sin filtros aplicados, **When** el responsable de Tráfico lo mira, **Then**
   no ve los viajes anulados, y el control de filtro dice explícitamente qué estados está mostrando,
   de modo que ninguna fila quede oculta en silencio.
10. **Given** el listado, **When** el responsable de Tráfico filtra por estado `anulado`, **Then** ve
    los viajes anulados con su motivo.

---

### User Story 6 - Anular un viaje que no se hizo (Priority: P2)

El responsable de Tráfico anula un viaje que finalmente no se hizo, escribiendo por qué. El viaje
deja de contar como trabajo realizado pero no desaparece: queda en la historia con su motivo, y su
chofer y su vehículo vuelven a estar libres.

**Why this priority**: Es necesario para que los totales sean fieles, pero depende de que ya existan
viajes cargados y no impide operar el resto del módulo.

**Independent Test**: Se puede verificar de forma independiente anulando un viaje `pendiente`,
comprobando que sin motivo escrito la confirmación no se habilita, que al cancelar la confirmación
nada cambia, y que al confirmar el viaje desaparece del listado sin filtros pero reaparece con su
motivo al filtrar por `anulado`.

**Acceptance Scenarios**:

1. **Given** un viaje `pendiente` o `en curso`, **When** el responsable de Tráfico pide anularlo,
   **Then** el sistema le pide un motivo escrito y una confirmación explícita.
2. **Given** el formulario de anulación sin motivo escrito, **When** el responsable de Tráfico
   intenta confirmar, **Then** el sistema no habilita la confirmación y no anula nada.
3. **Given** el pedido de confirmación de anulación, **When** el responsable de Tráfico cancela,
   **Then** el viaje queda exactamente igual que antes, con su estado, su asignación y su historial
   sin cambios.
4. **Given** el motivo escrito y la confirmación aceptada, **When** se ejecuta la anulación, **Then**
   el viaje queda en estado `anulado`, el historial registra quién lo anuló y cuándo, y su chofer y
   su vehículo quedan libres para otro viaje.
5. **Given** un viaje anulado, **When** se lo busca filtrando por estado `anulado`, **Then** aparece
   en el listado con su motivo visible, y su importe no figura en ningún total del período.
6. **Given** un viaje `rendido`, **When** se abre su ficha, **Then** la acción de anular no está
   disponible.
7. **Given** un viaje anulado, **When** se lo consulta, **Then** no existe ninguna acción para
   devolverlo a `pendiente` ni a `en curso`.

---

### User Story 7 - Ver totales por cliente y por transportista en un período (Priority: P3)

Administración arma para Gerencia el resumen del período: cuántos viajes hizo cada cliente y cada
transportista entre dos fechas, y cuánto suman sus importes, para negociar tarifas y decidir
contrataciones con datos y no de memoria.

**Why this priority**: Es el uso que le da valor a los datos cargados, pero depende de que el resto
del módulo ya esté operando y no bloquea la operación diaria.

**Independent Test**: Se puede verificar de forma independiente cargando viajes de dos clientes y dos
transportistas dentro y fuera de un rango de fechas, con alguno anulado, y comprobando que los
totales cuentan sólo los del rango y ninguno de los anulados.

**Acceptance Scenarios**:

1. **Given** viajes cargados en distintas fechas, **When** Administración elige un rango de fechas,
   **Then** ve dos cuadros en la pantalla de totales: uno con la cantidad de viajes y el importe
   acumulado de cada cliente, y otro con lo mismo de cada transportista.
2. **Given** la pantalla de totales recién abierta, **When** todavía no se eligió un rango de fechas,
   **Then** el sistema no calcula ni muestra ningún total y dice explícitamente que falta elegir el
   rango.
3. **Given** un cliente con 10 viajes en el período, de los cuales 2 están anulados, **When** se mira
   su total, **Then** figura con 8 viajes y con la suma de los importes de esos 8.
4. **Given** el listado filtrado por cliente y rango de fechas, **When** se compara con el total de
   ese cliente, **Then** la suma de los importes de las filas mostradas coincide con el total.
5. **Given** un rango de fechas sin ningún viaje, **When** se consulta, **Then** el sistema muestra
   un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.
6. **Given** un usuario con rol Gerencia, **When** abre el listado y las fichas, **Then** puede
   consultarlos, pero no ve las acciones de alta, modificación, asignación, cambio de estado ni
   anulación.
7. **Given** ese mismo usuario, **When** se invoca directamente una acción de modificación, **Then**
   el sistema la rechaza: la restricción no vive sólo en la pantalla.

---

### Edge Cases

- No hay clientes cargados: el formulario de alta de viaje informa explícitamente que primero hay que
  cargar un cliente y no deja crear el viaje a medias (cubierto en User Story 2).
- No hay choferes activos o no hay vehículos disponibles: el viaje se puede registrar igual y queda
  `pendiente` sin asignar; la pantalla de asignación informa explícitamente qué falta cargar y el
  viaje no puede pasar a `en curso` hasta tener los dos (cubierto en User Story 3 y User Story 4).
- El chofer o el vehículo asignado se da de baja después de asignado: el viaje conserva la asignación
  histórica y la muestra señalada como inactiva; no se borra ni se reasigna sola (cubierto en User
  Story 3). Si el viaje todavía no arrancó, hay que reasignarlo antes de ponerlo `en curso`; si ya
  estaba `en curso`, sigue su camino normal hasta rendirse (FR-025, cubierto en User Story 4).
- Un documento del chofer o del vehículo vence mientras el viaje ya está `en curso`: el viaje sigue su
  curso normal, porque el bloqueo se aplica al momento de asignar (cubierto en User Story 4).
- Se mueve la fecha de un viaje ya asignado a un día en el que la unidad quedaría con documentación
  vencida: el cambio de fecha se rechaza con el motivo puntual y no se guarda nada. La asignación y la
  fecha vieja quedan intactas, y el operador elige si mueve la fecha a otro día o si cambia primero la
  unidad (FR-022a, cubierto en User Story 2).
- Viaje cargado con fecha pasada: se acepta como carga retroactiva de operación real, el sistema lo
  señala, y la documentación se valida contra esa fecha y no contra hoy (cubierto en User Story 2 y
  User Story 3).
- Viaje cargado con fecha muy futura: se acepta como viaje planificado en estado `pendiente`, y no
  ocupa al chofer ni al vehículo hasta que pase a `en curso` (cubierto en User Story 2 y User
  Story 3).
- Origen y destino iguales: se acepta con una advertencia, porque existen servicios dentro de la misma
  localidad (cubierto en User Story 2).
- Viaje sin número de remito al momento del alta: es válido; el remito se carga después mientras el
  viaje no esté rendido (cubierto en User Story 2).
- Dos operadores intentan poner en curso el mismo chofer al mismo tiempo: el primero gana y el segundo
  recibe el error de chofer ocupado, porque la exclusividad se garantiza en el guardado y en la base
  de datos, no sólo en la pantalla (cubierto en User Story 4).
- Dos operadores registran el mismo número de remito al mismo tiempo: igual que arriba, quien llega
  segundo recibe el error de duplicado con el número del viaje que ya lo usa (cubierto en User
  Story 2).
- Viaje `en curso` durante muchos días sin rendirse: pasados 5 días corridos desde que arrancó, el
  sistema lo destaca en el listado como demorado, pero no le cambia el estado por sí solo (cubierto en
  User Story 4).
- Se cargó mal un viaje que ya se rindió: no hay corrección posible en esta versión, para ningún rol.
  El viaje `rendido` es inmutable y el error queda registrado tal como está; habilitar una corrección
  auditada es una spec aparte (FR-018, cubierto en User Story 4).
- Importe todavía sin definir al dar de alta: se admite cargarlo en cero y completarlo antes de la
  rendición. Al rendir un viaje con importe en cero el sistema no lo rinde de una: advierte que
  quedará cerrado y sin importe, y recién lo rinde al confirmar, porque después FR-018 no deja
  corregirlo (cubierto en User Story 2 y User Story 4).
- Un chofer cambia de transportista después de hacer el viaje: el viaje conserva el transportista que
  tenía registrado, y el filtro por transportista lo sigue encontrando ahí (cubierto en User Story 3
  y User Story 5).
- El chofer asignado pertenece a un transportista y el vehículo asignado a otro: se acepta sin
  objeción. El transportista del viaje sale siempre del chofer, y el del vehículo no se compara
  (FR-029).
- Un viaje anulado que ya tenía chofer y vehículo asignados: conserva la asignación en la ficha —para
  saber a quién se le había encargado— pero deja de ocuparlos (cubierto en User Story 6).
- Se anula un viaje `en curso`: el chofer y el vehículo quedan libres inmediatamente, igual que al
  rendirlo (cubierto en User Story 6).
- Cliente con viajes rendidos o anulados solamente: la baja procede, porque la restricción alcanza
  únicamente a los viajes `pendiente` y `en curso`. Es el caso normal del cliente que dejó de operar
  con la empresa, que por definición tiene historial (cubierto en User Story 1).
- Viaje cuyo cliente se dio de baja después: el viaje conserva su cliente y lo sigue mostrando,
  señalado como inactivo; el cliente inactivo no se ofrece para viajes nuevos (FR-008).
- Se busca por un texto con acentos escrito sin ellos, o al revés: encuentra igual, porque la búsqueda
  no distingue mayúsculas ni acentos (cubierto en User Story 5).

## Requirements *(mandatory)*

### Functional Requirements

#### Padrón de clientes

- **FR-001**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica clientes,
  con razón social, CUIT, teléfono, email y dirección; NO DEBE borrarlos físicamente.
- **FR-002**: El sistema DEBE exigir razón social (hasta 100 caracteres), CUIT, teléfono y email; la
  dirección DEBE ser opcional (hasta 200 caracteres).
- **FR-003**: El sistema DEBE exigir que el CUIT de un cliente sea único en todo el padrón, incluidos
  los clientes dados de baja, garantizado con una restricción de unicidad en la base de datos y no
  sólo con la validación previa; en una modificación, la comparación DEBE excluir al propio cliente.
- **FR-004**: El sistema DEBE validar el CUIT con la misma regla del Módulo 3 —once dígitos con
  dígito verificador válido, normalizado a sólo dígitos antes de validar y de guardar— y DEBE
  rechazar con un motivo puntual cualquier otro valor. El email DEBE tener formato válido y NO DEBE
  tener restricción de unicidad.
- **FR-005**: La baja de un cliente DEBE pedir una confirmación explícita antes de ejecutarse, y
  cancelar esa confirmación NO DEBE modificar nada.
- **FR-006**: El sistema DEBE rechazar la baja de un cliente que tenga al menos un viaje en estado
  `pendiente` o `en curso`, informando cuántos son en el mensaje y en el cuerpo del error. Los viajes
  `rendido` y `anulado` NO DEBEN impedir la baja: son historia, no trabajo comprometido, y un cliente
  que dejó de operar con la empresa por definición los tiene. La restricción protege lo único que hay
  que proteger —que no quede trabajo pendiente colgando de un cliente inactivo— y los viajes
  históricos conservan su cliente y lo siguen mostrando (FR-008).
- **FR-007**: El sistema DEBE permitir dar de alta de nuevo un cliente dado de baja. El alta DEBE ser
  un recurso propio y no un campo del formulario de edición, NO DEBE pedir confirmación aparte y DEBE
  ser idempotente. El sistema NO DEBE ofrecer registrar de nuevo un CUIT que ya pertenece a un cliente
  dado de baja: DEBE rechazar el alta indicando que hay que dar de alta de nuevo al cliente existente.
- **FR-008**: El sistema NO DEBE ofrecer los clientes inactivos al registrar o modificar un viaje; los
  viajes ya registrados con un cliente dado de baja DEBEN conservarlo y seguir mostrándolo, señalado
  como inactivo con la palabra que lo explica y no sólo con un color.
- **FR-009**: El listado de clientes DEBE paginarse del lado del servidor con 20 filas por página, con
  un orden total que termine en el identificador del cliente, informando el total de coincidencias, y
  DEBE mostrar un mensaje explícito de padrón vacío o de "sin resultados" cuando no tiene filas.

#### Alta y datos del viaje

- **FR-010**: El sistema DEBE permitir registrar viajes con número de viaje, cliente, origen, destino,
  fecha, número de remito, detalle de carga e importe.
- **FR-011**: El sistema DEBE asignar a cada viaje un número único e irrepetible, generado por el
  sistema, no editable por ningún usuario y garantizado con una restricción de unicidad en la base de
  datos. El número NO DEBE reutilizarse nunca, ni siquiera cuando el viaje que lo tenía se anula.
- **FR-012**: El sistema DEBE exigir cliente, origen, destino y fecha para dar de alta un viaje.
  Origen y destino DEBEN ser texto libre obligatorio de hasta 100 caracteres cada uno; el detalle de
  carga DEBE ser opcional, de hasta 500 caracteres.
- **FR-013**: El importe DEBE expresarse en pesos argentinos, NO DEBE aceptar valores negativos y DEBE
  admitir cero para viajes sin cargo o con importe todavía sin definir.
- **FR-014**: El número de remito DEBE ser opcional y, cuando se carga, DEBE ser único entre los
  viajes no anulados, garantizado con una restricción de unicidad en la base de datos que excluya los
  anulados. El rechazo DEBE identificar el número del viaje que ya lo usa.
- **FR-015**: Que el origen y el destino sean iguales NO DEBE ser un error: existen servicios dentro
  de la misma localidad. Se trata como advertencia **reversible** según FR-015a, que fija el mecanismo.
- **FR-015a**: Las advertencias que no bloquean se entregan de dos maneras, según si el paso que
  advierten se puede deshacer:
  - Cuando el paso es **reversible**, el sistema DEBE ejecutar la operación y mostrar la advertencia
    junto con el resultado, sin pedirle al operador ningún paso extra. Es el caso de origen igual a
    destino (FR-015) y de la documentación próxima a vencer al asignar (FR-023): los dos se corrigen
    editando el viaje.
  - Cuando el paso **no se puede deshacer**, el sistema NO DEBE ejecutarlo al primer intento: DEBE
    mostrar la advertencia sin cambiar nada y ejecutar la operación únicamente después de que el
    operador confirme. Es el caso de rendir un viaje con importe en cero (FR-038), porque FR-018 deja
    el viaje inmutable y después ya no hay forma de corregir el importe.

  Una advertencia NUNCA DEBE comunicarse sólo por color, y siempre DEBE nombrar el motivo puntual: qué
  documento vence, qué campos coinciden, qué dato falta completar.
- **FR-016**: El sistema DEBE aceptar tanto una fecha anterior a hoy —carga retroactiva, que DEBE
  señalar explícitamente— como una fecha posterior a hoy —viaje planificado—, sin límite de
  antigüedad ni de anticipación.
- **FR-017**: El sistema DEBE permitir modificar los datos de un viaje mientras esté en estado
  `pendiente` o `en curso`, aplicando las mismas validaciones que rigen el alta. El número de viaje NO
  DEBE ser editable en ningún estado.
- **FR-018**: Un viaje `rendido` DEBE ser inmutable para **todos** los roles, incluido el
  *Administrador del sistema*: el sistema NO DEBE permitir modificar ninguno de sus datos, NO DEBE
  ofrecer la acción y DEBE rechazarla igual si se la invoca directamente, informando que el viaje
  está cerrado. No hay ningún camino de corrección de un viaje rendido en esta versión.

#### Asignación de chofer y vehículo

- **FR-019**: El sistema DEBE permitir asignar a un viaje un chofer y un vehículo del padrón, y DEBE
  permitir reasignarlos mientras el viaje esté en estado `pendiente` o `en curso`. La asignación NO
  DEBE ser obligatoria para dar de alta el viaje.
- **FR-019a**: La asignación DEBE ser una **acción propia** sobre el viaje, con su pantalla, y NO DEBE
  formar parte del alta ni del formulario de edición de datos: primero se registra el viaje y después
  se le asigna. El chofer y el vehículo NO DEBEN poder cambiarse guardando el formulario de datos, de
  modo que corregir un origen, un destino o un importe no pueda tocar quién hace el viaje. El bloqueo
  de FR-022 y la advertencia de FR-023 viven únicamente en esta acción y en la revalidación por cambio
  de fecha de FR-022a.
- **FR-019b**: El chofer y el vehículo DEBEN asignarse **juntos, en una sola operación**. El sistema
  NO DEBE admitir la asignación parcial: un viaje tiene los dos asignados o no tiene ninguno, y nunca
  queda con uno solo de los dos. Reasignar DEBE volver a exigir los dos.
- **FR-020**: El sistema NO DEBE permitir asignar ni reasignar el chofer o el vehículo de un viaje
  `rendido` o `anulado`.
- **FR-021**: El sistema DEBE ofrecer para asignar únicamente choferes activos y vehículos activos
  cuyo **estado operativo guardado** sea `disponible`, según los padrones de los Módulos 3 y 4. La
  lista NO DEBE usar el estado operativo derivado contra el día en curso: la habilitación por
  documentación se resuelve entera contra la fecha del viaje (FR-022, FR-023, FR-024), de modo que un
  viaje retroactivo pueda cargarse con la unidad que efectivamente lo hizo.
- **FR-022**: El sistema DEBE impedir la asignación de un chofer o de un vehículo que tenga al menos
  un documento vencido a la fecha del viaje, informando qué documento —tipo y número— lo bloquea, y
  NO DEBE guardar la asignación.
- **FR-022a**: Cambiar la fecha de un viaje que ya tiene chofer o vehículo asignados DEBE revalidar
  esa asignación contra la fecha nueva con la regla de FR-022. Si el chofer o el vehículo quedaran
  bloqueados a esa fecha, el sistema NO DEBE guardar el cambio de fecha y DEBE informar qué documento
  de qué unidad lo impide. **Ninguna operación de este módulo** —asignar, reasignar o mover la fecha—
  DEBE dejar guardado un viaje cuya asignación esté bloqueada a su propia fecha. Un cambio posterior
  en la documentación hecho desde los Módulos 3 o 4 —corregir un vencimiento, eliminar un documento—
  puede producir ese estado, y queda fuera de lo que este módulo controla.
- **FR-023**: El sistema DEBE advertir, sin bloquear, cuando el chofer o el vehículo que se asigna
  tenga al menos un documento próximo a vencer a la fecha del viaje, nombrando el documento afectado.
  La asignación DEBE guardarse y la advertencia DEBE llegar con el resultado (FR-015a), porque
  reasignar es reversible mientras el viaje no esté rendido ni anulado.
- **FR-024**: El estado de la documentación DEBE evaluarse con las mismas reglas de los Módulos 3 y 4
  —un documento vigente por tipo, el de vencimiento más lejano; días de aviso corridos definidos por
  el tipo—, tomando **la fecha del viaje** en lugar del día en curso como fecha de referencia. Un
  chofer o un vehículo sin ningún documento cargado NO DEBE bloquear la asignación, porque el sistema
  informa sobre lo que está cargado y no infiere lo que falta.
- **FR-025**: El sistema DEBE exigir chofer y vehículo asignados, y **activos en sus padrones**, para
  permitir que un viaje pase a `en curso`. Si alguno de los dos se dio de baja después de asignarlo,
  el sistema DEBE rechazar el arranque indicando cuál es y DEBE exigir que se reasigne primero. En
  cambio, la documentación y el estado operativo del vehículo NO DEBEN frenar el pase a `en curso`:
  eso se controló al asignar (FR-022, FR-024) y volver a evaluarlo acá dejaría en tierra un viaje que
  ya se planificó con la unidad en regla.
- **FR-026**: El sistema DEBE impedir que un mismo chofer esté asignado a dos viajes `en curso` al
  mismo tiempo, y lo mismo para un mismo vehículo. La verificación DEBE hacerse en el guardado y estar
  garantizada con una restricción en la base de datos, no sólo en la pantalla; el rechazo DEBE indicar
  el número del viaje que lo ocupa.
- **FR-026a**: La verificación de FR-026 DEBE correr en **los dos caminos** que llevan a ese estado:
  al pasar un viaje a `en curso` y al **reasignar** el chofer o el vehículo de un viaje que ya está
  `en curso`. Nunca DEBEN existir dos viajes `en curso` con la misma unidad, cualquiera sea el camino
  por el que se llegó. La reasignación de un viaje `pendiente` NO DEBE verificar ocupación, porque un
  viaje `pendiente` no ocupa a nadie (FR-027).
- **FR-027**: Un viaje en estado `pendiente` NO DEBE ocupar a su chofer ni a su vehículo, cualquiera
  sea su fecha: la exclusividad de FR-026 alcanza únicamente a los viajes `en curso`.
- **FR-028**: Al asignar un chofer, el sistema DEBE registrar en el viaje una **referencia al
  transportista** del padrón al que ese chofer pertenece en ese momento, y NO DEBE moverla si después
  el chofer cambia de transportista. Reasignar el chofer DEBE volver a tomar el transportista del
  chofer nuevo. Los datos del transportista referenciado —razón social, CUIT, contacto— NO se
  congelan: si se los corrige en el Módulo 3, el viaje muestra los corregidos.
- **FR-029**: El sistema NO DEBE comparar el transportista del vehículo con el del chofer: el
  transportista del viaje sale siempre del chofer, y un chofer de un transportista puede manejar un
  vehículo de otro.
- **FR-030**: Cuando el chofer o el vehículo asignado a un viaje se da de baja en su padrón, el viaje
  DEBE conservar la asignación y mostrarla señalada como inactiva, con la palabra que lo explica; el
  sistema NO DEBE borrarla ni reasignarla por su cuenta.

#### Ciclo de vida y estados

- **FR-031**: El estado de un viaje DEBE tomar exactamente uno de estos cuatro valores: `pendiente`,
  `en curso`, `rendido` y `anulado`.
- **FR-032**: Todo viaje nuevo DEBE crearse en estado `pendiente`.
- **FR-033**: El sistema DEBE permitir únicamente las transiciones `pendiente → en curso`,
  `en curso → rendido`, `pendiente → anulado` y `en curso → anulado`. Cualquier otra transición DEBE
  rechazarse, y la pantalla NO DEBE ofrecerla.
- **FR-034**: Cada cambio de estado DEBE ser un recurso propio y nunca un campo del formulario de
  edición del viaje, de modo que corregir un dato no pueda avanzar ni anular un viaje en silencio.
- **FR-035**: El sistema DEBE registrar el historial de estados de cada viaje, con el estado anterior,
  el estado nuevo, el usuario que produjo el cambio y la fecha y hora en que ocurrió, empezando por el
  alta del viaje. El historial NO DEBE ser editable ni borrable.
- **FR-036**: La anulación de un viaje DEBE exigir un motivo escrito obligatorio de hasta 500
  caracteres y una confirmación explícita; sin motivo, la confirmación NO DEBE habilitarse. Cancelar
  la confirmación NO DEBE modificar nada. El motivo DEBE quedar visible en la ficha del viaje y en el
  listado filtrado por estado `anulado`.
- **FR-037**: Al pasar un viaje a `rendido` o a `anulado`, su chofer y su vehículo DEBEN dejar de
  estar ocupados y quedar disponibles para otros viajes. La asignación DEBE conservarse en el viaje y
  seguir viéndose en el listado y en la ficha: liberar es dejar de ocupar, nunca borrar el dato.
- **FR-038**: Cuando se pide pasar a `rendido` un viaje con importe en cero, el sistema NO DEBE
  aplicar el cambio al primer intento: DEBE mostrar la advertencia sin rendir nada y rendir el viaje
  únicamente después de que el operador confirme (FR-015a). La advertencia DEBE decir que el viaje
  quedará cerrado y sin importe, y que después no se podrá corregir (FR-018). El sistema NO DEBE
  exigir que se complete el importe: el viaje sin cargo es válido y se rinde igual.
- **FR-039**: El sistema DEBE destacar en el listado, como **demorados**, los viajes que llevan
  `en curso` más de **5 días corridos** contados desde el instante en que pasaron a ese estado. El
  destaque DEBE llevar la palabra que lo explica y no comunicarse sólo por color, y el sistema NO DEBE
  cambiarle el estado a ningún viaje por sí solo: `demorado` es una señal derivada al leer, nunca un
  estado guardado ni un quinto valor de `EstadoViaje`.

#### Consulta y reportes

- **FR-040**: El listado de viajes DEBE mostrar número, fecha, cliente, origen, destino, chofer,
  vehículo, transportista, estado e importe.
- **FR-041**: El listado DEBE permitir filtrar por cliente, rango de fechas, estado y transportista,
  en cualquier combinación. Cliente, estado y transportista DEBEN ser una selección exacta entre las
  opciones ya cargadas en el sistema.
- **FR-042**: El listado DEBE permitir buscar por coincidencia parcial, sin distinguir mayúsculas ni
  acentos, sobre el origen, el destino y la razón social del cliente, combinable con los filtros de
  FR-041.
- **FR-043**: El listado de viajes DEBE paginarse del lado del servidor con 20 filas por página. Los
  filtros y la búsqueda DEBEN aplicarse sobre todos los viajes antes de paginar, y el sistema DEBE
  mostrar el total de viajes que cumplen los filtros junto con la página en curso. El orden DEBE ser
  **fecha del viaje descendente y, a igual fecha, número de viaje descendente**: lo más reciente
  primero, con un criterio total que no permita que dos viajes del mismo día se intercambien entre
  páginas.
- **FR-044**: Sin filtro de estado aplicado, el listado NO DEBE mostrar los viajes `anulado`; DEBE
  mostrarlos al elegir ese estado en el filtro. La exclusión DEBE escribirse como predicado único de
  la consulta y no como un filtrado posterior.
- **FR-045**: La ficha de un viaje DEBE mostrar su número, cliente, origen, destino, fecha, número de
  remito, detalle de carga, importe, estado, chofer, vehículo, transportista, motivo de anulación
  cuando corresponda, y el historial completo de cambios de estado de FR-035.
- **FR-046**: El sistema DEBE ofrecer una pantalla propia de totales, distinta del listado de viajes,
  con dos cuadros: uno con la cantidad de viajes y el importe acumulado **por cliente**, y otro con lo
  mismo **por transportista**.
- **FR-046a**: El rango de fechas de esa pantalla DEBE ser obligatorio: mientras no haya un rango
  elegido, el sistema NO DEBE calcular ni mostrar totales, y DEBE decir que falta elegirlo. La fecha
  de corte DEBE ser la fecha del viaje. El listado de viajes NO DEBE incorporar una fila de total
  propia: los totales viven únicamente en esta pantalla.
- **FR-047**: Los viajes en estado `anulado` DEBEN excluirse de toda cantidad y de todo importe
  acumulado de FR-046, y esa exclusión DEBE escribirse como predicado de la consulta.
- **FR-048**: El sistema DEBE mostrar un mensaje explícito de "sin resultados" o de padrón vacío
  cuando un listado o un cuadro de totales no tiene filas, en vez de una tabla vacía sin explicación.
- **FR-049**: Cuando el listado esté filtrando por un estado, el control DEBE mostrar explícitamente
  cuál: ninguna fila DEBE quedar oculta sin que la pantalla lo diga. Ningún estado DEBE comunicarse
  sólo por color, y todo elemento atenuado DEBE llevar además la palabra que lo explica.

#### Acceso

- **FR-050**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados y DEBE
  resolverlo con **dos permisos**: uno de gestión de viajes y uno de consulta de viajes. La
  autorización DEBE evaluarse por permiso y nunca por rol, y el menú DEBE resolver cada entrada sin
  código nuevo.
- **FR-051**: El permiso de gestión DEBE corresponder a los roles *Tráfico* y *Administrador del
  sistema*, y el de consulta a *Administración de la empresa* y *Gerencia*, además de los dos
  anteriores.
- **FR-052**: Quien tenga únicamente el permiso de consulta NO DEBE ver las acciones de alta,
  modificación, asignación, cambio de estado ni anulación, ni en el listado ni en la ficha, y el
  sistema DEBE rechazarlas igual si se las invoca directamente.
- **FR-053**: El padrón de clientes DEBE administrarse con el permiso de gestión de viajes, y DEBE
  poder consultarse con el permiso de consulta.

### Key Entities *(include if feature involves data)*

- **Viaje**: unidad de trabajo de la empresa. Incluye número de viaje (único, generado por el
  sistema, no reutilizable), fecha, origen, destino, número de remito (opcional y único entre los no
  anulados), detalle de carga, importe en pesos, estado, motivo de anulación cuando corresponde y el
  transportista registrado al asignar el chofer. Es la entidad principal del módulo: pertenece a
  exactamente un cliente y puede tener asignados un chofer y un vehículo.
- **Cliente**: empresa o persona que contrata el servicio de transporte. Incluye razón social, CUIT
  (único y normalizado a sólo dígitos), teléfono, email, dirección opcional y estado activo/inactivo.
  Se registra, consulta, modifica y da de baja lógicamente desde este módulo. Un cliente agrupa muchos
  viajes; cada viaje pertenece a uno solo.
- **CambioDeEstadoViaje**: registro de un cambio de estado de un viaje. Incluye el estado anterior, el
  estado nuevo, el usuario que lo produjo y el instante en que ocurrió. Pertenece a un único viaje; un
  viaje tiene muchos, empezando por el de su alta. No se edita ni se borra.
- **Chofer**: quien maneja la unidad. Es la misma entidad que administra el Módulo 3, con su
  documentación y su transportista; este módulo la consume para asignar y para evaluar habilitación, y
  no la administra.
- **Vehiculo**: unidad con la que se cubre el viaje. Es la misma entidad que administra el Módulo 4,
  con su documentación y su estado operativo; este módulo la consume para asignar y para evaluar
  habilitación, y no la administra.
- **Transportista**: empresa o persona a la que pertenece el chofer, sea G&T Logística S.A. o un
  tercero contratado. Es la misma entidad del Módulo 3, y este módulo no la administra. Cada viaje
  guarda una referencia a cuál era el transportista del chofer al momento de asignarlo, para que el
  viaje no cambie de dueño cuando el chofer sí lo hace. Lo que queda fijo es a quién apunta el viaje,
  no los datos de ese transportista, que se leen siempre del padrón.

### Enumerations

- **EstadoViaje**: `pendiente`, `enCurso`, `rendido`, `anulado`. Lo determina el ciclo de vida del
  viaje según FR-033; ningún usuario lo edita como un campo más (FR-034). Viaja en el JSON en
  camelCase, con su traducción al español en la capa de nombres de estado.
- **Estado de habilitación de una asignación** (derivado, no se almacena): `habilitado`,
  `conAdvertencia`, `bloqueado`. Aplica al chofer o al vehículo que se está por asignar, evaluado
  contra la fecha del viaje según FR-024; el sistema lo calcula al momento de asignar y no lo guarda.
- **Demorado** (derivado, no se almacena): señal booleana del listado, verdadera cuando el viaje está
  `en curso` desde hace más de 5 días corridos (FR-039). NO es un quinto valor de `EstadoViaje` ni una
  columna: se calcula al leer, igual que los estados de vencimiento de los Módulos 3 y 4.

### Relationships

- **Cliente 1 — * Viaje**: todo viaje pertenece obligatoriamente a exactamente un cliente; un cliente
  puede tener muchos viajes o ninguno. La baja del cliente se rechaza si tiene al menos un viaje no
  anulado (FR-006).
- **Viaje * — 0..1 Chofer**: un viaje puede tener un chofer asignado o ninguno; un chofer puede estar
  en muchos viajes, pero en un solo viaje `en curso` a la vez (FR-026).
- **Viaje * — 0..1 Vehiculo**: un viaje puede tener un vehículo asignado o ninguno; un vehículo puede
  estar en muchos viajes, pero en un solo viaje `en curso` a la vez (FR-026).
- **Viaje * — 0..1 Transportista**: el transportista queda registrado en el viaje al asignar el chofer
  y no cambia después (FR-028); un viaje sin chofer asignado todavía no tiene transportista.
- **Viaje 1 — * CambioDeEstadoViaje**: todo viaje tiene al menos un cambio de estado —el de su alta— y
  acumula uno por cada transición; cada registro pertenece a un único viaje.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Partiendo de un padrón vacío, el responsable de Tráfico puede cargar un cliente,
  registrar un viaje, asignarle chofer y vehículo y llevarlo hasta `rendido` sin intervención técnica.
- **SC-002**: El 100% de los viajes registrados tiene exactamente un cliente y un número asignado por
  el sistema; ningún usuario puede editar ese número desde ninguna pantalla.
- **SC-003**: El 100% de los intentos de registrar un CUIT de cliente ya existente, o un número de
  remito ya usado por un viaje no anulado, es rechazado con un mensaje que identifica el registro en
  conflicto, y ninguno crea nada.
- **SC-004**: Ninguna operación de este módulo —asignar, reasignar o mover la fecha del viaje— deja
  guardado un viaje cuyo chofer o vehículo tenga documentación vencida a la fecha de ese viaje, y el
  100% de los intentos bloqueados nombra el documento que lo impide. Un cambio hecho después desde los
  Módulos 3 o 4 sobre esa documentación puede producir ese estado y queda fuera del alcance de este
  módulo, que no los administra.
- **SC-005**: En cualquier momento, el 0% de los choferes y el 0% de los vehículos figura en más de un
  viaje `en curso`, incluso cuando dos operadores intentan al mismo tiempo poner en curso la misma
  unidad, o reasignarla a un viaje que ya está `en curso`.
- **SC-006**: El 100% de los cambios de estado queda registrado con el usuario que lo produjo y el
  instante en que ocurrió, y ese historial se puede leer desde la ficha del viaje sin consultas
  técnicas.
- **SC-007**: El 100% de las anulaciones tiene un motivo escrito y una confirmación explícita previa,
  y ninguna anulación cancelada produce cambios en los datos.
- **SC-007a**: Ningún paso irreversible del módulo —rendir un viaje con importe en cero, anular,
  dar de baja un cliente— se ejecuta sin una confirmación explícita previa, y cancelar cualquiera de
  ellos deja los datos exactamente como estaban.
- **SC-008**: El 0% de los importes de viajes anulados figura en los totales por cliente y por
  transportista, y para cualquier filtro aplicado la suma de los importes de las filas mostradas
  coincide con el total informado.
- **SC-009**: El 100% de los intentos de dar de baja un cliente con viajes `pendiente` o `en curso` es
  rechazado con el detalle de cuántos viajes dependen de él; el 100% de las bajas de clientes cuyos
  viajes están todos `rendido` o `anulado` procede; y ningún cliente se borra físicamente.
- **SC-010**: Filtrando por un transportista, el responsable de Tráfico obtiene el 100% de los viajes
  que ese transportista hizo y ninguno ajeno, aunque los choferes hayan cambiado de transportista
  después.
- **SC-011**: Un **paso** es una acción del operador sobre la interfaz: elegir una entrada del menú,
  aplicar un filtro o abrir una fila. Desde el ingreso al sistema, el responsable de Tráfico llega al
  estado de un viaje concreto de un cliente concreto por el camino *Viajes → filtrar por cliente →
  leer el estado en la fila*, sin pasar por ninguna otra pantalla y sin necesitar el número del viaje.
  Lo que se verifica es que **ese camino exista y sea directo**; la cantidad exacta de pasos no es un
  objetivo del módulo.
- **SC-012**: Un usuario con el permiso de consulta únicamente no puede modificar ningún dato de
  ningún viaje ni cliente, ni desde la pantalla ni invocando la acción directamente.
- **SC-013**: El 100% de los intentos de editar, reasignar, anular o retroceder un viaje `rendido` es
  rechazado, cualquiera sea el rol de quien lo intente, incluido el *Administrador del sistema*.
- **SC-014**: El 100% de los viajes cargados con fecha pasada puede asignarse a la unidad que
  realmente lo hizo cuando su documentación estaba en regla a esa fecha, aunque hoy esté vencida.

## Assumptions

- La autenticación, el catálogo de roles (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) y el esquema de permisos con menú resuelto por el servidor ya existen
  (Módulos 1 y 2); este módulo sólo agrega sus dos permisos y los asigna a los roles.
- El padrón de choferes con su documentación y su transportista proviene del Módulo 3, y el de
  vehículos con su documentación y su estado operativo, del Módulo 4. Este módulo los consume tal como
  están: no agrega pantallas de alta, edición o baja de choferes, vehículos, transportistas,
  documentos ni tipos de documentación, y no les cambia los datos.
- Este módulo NO modifica nada de los Módulos 3 y 4. En particular, la baja de un chofer o de un
  vehículo sigue con las reglas que ya tiene: un chofer o un vehículo con viajes asociados se puede
  dar de baja, y sus viajes conservan la asignación señalada como inactiva (FR-030). Si se prefiriera
  rechazar esa baja, es una spec aparte.
- Como corolario de lo anterior, **este módulo no puede garantizar nada sobre lo que pase en los
  Módulos 3 y 4 después de una asignación**. Un documento corregido o eliminado allá puede dejar un
  viaje ya guardado con documentación vencida a su fecha, y ningún control de este módulo lo va a
  impedir ni lo va a detectar. Lo que sí se garantiza es que ninguna operación de acá produzca ese
  estado (FR-022a, SC-004). Revisar los viajes afectados cuando cambia un documento sería una spec
  aparte, y exigiría tocar los Módulos 3 y 4.
- El transportista del viaje se toma del chofer en el momento de asignarlo y queda guardado en el
  viaje como referencia al padrón. No se reconstruye "a qué transportista pertenecía a la fecha del
  viaje" porque el Módulo 3 no historiza esa pertenencia: guarda la actual. Fijarla al asignar es la
  lectura de RN13 —"queda congelado al momento de la asignación"— y la única implementable sin
  agregar historización al Módulo 3.
- "Documentación próxima a vencer" en la asignación (FR-023) significa que el documento **todavía está
  vigente** a la fecha del viaje pero cae dentro de la ventana de aviso de su tipo contada desde esa
  fecha. Un documento que vence antes de la fecha del viaje no es "próximo a vencer": está vencido a
  esa fecha y bloquea (FR-022).
- La lista de vehículos asignables usa el estado operativo **guardado** del Módulo 4, no el derivado
  contra el día en curso. Es la única forma de que la carga retroactiva de CL4 funcione: una unidad
  hoy inhabilitada por documentación vencida pudo estar perfectamente en regla el día del viaje que se
  está asentando, y quien lo carga necesita poder decir la verdad. El control real de habilitación no
  se pierde: lo hace FR-022 contra la fecha del viaje.
- La corrección de un viaje ya `rendido` queda **fuera de esta versión** para todos los roles
  (FR-018). Un viaje mal rendido no se edita ni se anula; el enunciado la pedía para el Administrador
  del sistema (CL11), pero contradecía CA8 y RN7 y agregaba un camino de escape que ningún criterio de
  aceptación describe. Habilitarla con registro de quién y cuándo queda anotado como candidato para
  una spec futura.
- El importe se carga a mano en pesos argentinos. No hay cálculo automático por kilómetro, distancia
  ni peso, ni cotización previa.
- La dirección del cliente es opcional porque el módulo no la usa para operar —el origen y el destino
  viven en el viaje— y su uso natural, la factura, está fuera de alcance. Razón social, CUIT, teléfono
  y email son obligatorios, igual que en el `Transportista` del Módulo 3.
- El origen y el destino son texto libre. No hay catálogo de localidades ni validación contra uno; esa
  normalización queda anotada como candidata para una spec futura.
- La paginación de 20 filas por página, el orden total del listado y el formato de respuesta paginada
  siguen la convención ya adoptada desde el Módulo 3.
- Los totales por cliente y por transportista viven en una pantalla propia del módulo, con rango de
  fechas obligatorio (FR-046, FR-046a). No hay exportación a archivo ni envío por correo: Gerencia los
  recibe armados desde Administración, como indica el enunciado.
- Un viaje sin chofer asignado no tiene transportista, así que el filtro por transportista no lo
  devuelve. Es el comportamiento esperado: todavía no se sabe quién lo va a hacer.
- La numeración de viajes arranca en 1 y avanza de a uno. Los números del enunciado (1041, 1042) son
  ilustrativos, no un punto de partida a precargar.
- Quedan fuera del alcance de este módulo, tal como indica el enunciado: la facturación al cliente
  —incluida cualquier referencia del viaje a una factura—, la liquidación al transportista, las
  cotizaciones y presupuestos, el seguimiento GPS, las hojas de ruta con paradas intermedias, los
  viajes con múltiples destinos o cargas consolidadas, la gestión de combustible, peajes, viáticos y
  gastos, las notificaciones automáticas por email, SMS o push, el portal de autoconsulta del cliente
  y la app del chofer, y la digitalización del remito firmado o comprobante de entrega.
