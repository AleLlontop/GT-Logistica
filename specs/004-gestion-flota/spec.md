# Feature Specification: Gestión de flota (Módulo 4)

**Feature Branch**: `004-gestion-flota`

**Created**: 2026-08-08

**Status**: Draft

**Input**: User description: "Gestión de flota (Módulo 4) v1. G&T Logística S.A. necesita saber con qué vehículos cuenta, cuáles están en condiciones de salir a la ruta y cuáles tienen documentación vencida. Hoy eso se lleva en planillas sueltas. Este módulo mantiene el padrón de vehículos, su estado operativo y su documentación obligatoria con aviso de vencimientos. RF1: registrar, consultar, modificar y dar de baja lógica vehículos con patente única, marca, modelo, tipo de vehículo y estado operativo. RF2: asociar documentos a un vehículo (tipo, número, fecha de emisión, fecha de vencimiento y archivo adjunto) y calcular solo su estado de vigencia. RF3: listado de la flota con filtros por tipo, estado del vehículo y estado de documentación. RN1: patente única y normalizada. RN2: el estado de cada documento lo calcula el sistema. RN3: un vehículo con al menos un documento vencido no puede quedar disponible. Fuera de alcance: asignación a viajes y choferes, kilometraje, mantenimiento, taller, combustible, GPS, notificaciones, validación contra organismos externos y auditoría."

## Clarifications

### Session 2026-08-08

- Q: Los tipos de documentación de vehículo (VTV, seguro, RUTA, cédula verde), ¿salen del catálogo
  `DocumentacionTipo` que ya tiene el Módulo 3 o de uno propio de flota? → A: Del mismo catálogo, que
  se extiende con un campo que indica a qué se aplica cada tipo: chofer o vehículo. Cada módulo
  ofrece únicamente los tipos de su ámbito, y la baja de un tipo cuenta los documentos de ambos
  lados. No se duplica el ABM ni la regla de días de aviso.
- Q: Extender `DocumentacionTipo` obliga a migrar los tipos ya cargados por el Módulo 3. ¿Se acepta
  esa migración dentro de este módulo? → A: Sí. Los tipos cargados son pocos y se migran a ámbito
  chofer sin excepciones ni tratamiento manual (FR-017c).
- Q: RN3 dice que al vencer el seguro "el sistema lo pasa a `fuera de servicio`". ¿Se sobrescribe el
  estado guardado o se deriva al consultarlo? → A: Se deriva al consultarlo. El operador elige el
  estado operativo, pero un vehículo con documentación `vencida` o `sin documentación` se muestra y
  se filtra como `fuera de servicio` aunque tenga `disponible` guardado. Al renovar el documento
  vuelve a estar disponible solo, sin que nadie lo edite y sin proceso nocturno que mantenga la
  columna al día.
- Q: ¿Qué valores exactos toma el estado operativo del vehículo? → A: Exactamente dos: `disponible` y
  `fuera de servicio`. No hay estado intermedio: una unidad parada por reparación se marca
  `fuera de servicio`, igual que una inhabilitada por documentación.
- Q: Faltaba la relación `Transportista 1 — * Vehiculo`. ¿Cómo entra en este módulo? → A: Todo
  vehículo pertenece obligatoriamente a un transportista activo, que se elige al registrarlo y se
  puede reasignar después. Se reutiliza el `Transportista` del Módulo 3 —incluida G&T Logística S.A.
  como transportista propio—, sin crear un padrón paralelo ni un ABM nuevo. El transportista se
  muestra en el listado y en la ficha, y suma un cuarto filtro al listado de flota. La baja de un
  transportista, que hoy sólo mira sus choferes activos, pasa a mirar también sus vehículos activos.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mantener el catálogo de tipos de vehículo (Priority: P1)

El Administrador del sistema administra los tipos de vehículo con los que trabaja la empresa
(tractor, semirremolque, chasis, utilitario, entre otros), para que Tráfico pueda clasificar cada
unidad del padrón al registrarla.

**Why this priority**: Todo vehículo pertenece obligatoriamente a un tipo y el catálogo arranca
vacío: sin al menos un tipo cargado no se puede registrar ninguna unidad. Es el punto de partida del
módulo.

**Independent Test**: Se puede verificar de forma completa e independiente abriendo la pantalla de
tipos de vehículo con el catálogo vacío, cargando dos tipos, y comprobando que ambos quedan
disponibles para elegir al registrar un vehículo.

**Acceptance Scenarios**:

1. **Given** el catálogo de tipos de vehículo vacío, **When** el Administrador abre la pantalla,
   **Then** ve un mensaje explícito de que todavía no hay tipos cargados, en vez de una tabla vacía
   sin explicación.
2. **Given** un nombre que no existe en el catálogo, **When** el Administrador guarda, **Then** el
   tipo queda registrado y disponible para elegir al registrar un vehículo.
3. **Given** un nombre que ya existe en el catálogo, **When** se intenta guardar, **Then** el sistema
   informa el duplicado y no crea ningún tipo.
4. **Given** un tipo sin ningún vehículo asociado, **When** el Administrador lo da de baja, **Then**
   el tipo queda inactivo, deja de ofrecerse al registrar vehículos y su registro no se borra.
5. **Given** un tipo con vehículos asociados, **When** se intenta darlo de baja, **Then** el sistema
   lo rechaza e informa cuántos vehículos lo están usando.

---

### User Story 2 - Registrar un vehículo en el padrón de flota (Priority: P1)

El responsable de Tráfico registra una unidad con su patente, marca, modelo y tipo de vehículo, y
elige obligatoriamente el transportista dueño de esa unidad: G&T Logística S.A. si es un vehículo
propio, o el transportista terciarizado correspondiente. La unidad queda incorporada al padrón de
flota con su estado operativo.

**Why this priority**: Es el objetivo central del módulo. Sin el padrón de vehículos no hay a qué
asociarle documentación ni sobre qué informar disponibilidad, y sin el transportista no se puede
distinguir la flota propia de la contratada.

**Independent Test**: Se puede verificar de forma independiente con al menos un tipo de vehículo y un
transportista activo, completando el formulario con una patente nueva y datos válidos, guardando, y
comprobando que la unidad aparece en el listado con su tipo, su transportista y su estado;
repitiendo la misma patente escrita con espacios y en minúsculas, y comprobando que el sistema la
rechaza como duplicada.

**Acceptance Scenarios**:

1. **Given** al menos un tipo de vehículo activo, al menos un transportista activo y una patente que
   no existe en la flota, **When** el responsable de Tráfico completa marca, modelo, tipo y
   transportista y guarda, **Then** el vehículo queda registrado, activo y visible en el listado con
   su tipo, su transportista y su estado operativo.
2. **Given** el vehículo `AB123CD` ya registrado, **When** se intenta registrar `ab 123 cd` o
   `AB-123-CD`, **Then** el sistema informa que esa patente ya está registrada y no crea ningún
   vehículo.
3. **Given** el formulario sin tipo de vehículo elegido, **When** se intenta guardar, **Then** el
   sistema informa que el tipo es obligatorio y no crea ningún vehículo.
4. **Given** el formulario sin transportista elegido, **When** se intenta guardar, **Then** el
   sistema informa que el transportista es obligatorio y no crea ningún vehículo.
5. **Given** el formulario con la patente vacía o con un formato que no corresponde a una patente
   argentina, **When** se intenta guardar, **Then** el sistema marca ese campo con el motivo puntual
   y no envía el formulario.
6. **Given** el catálogo de tipos de vehículo sin ninguno activo, **When** el responsable de Tráfico
   abre el formulario de vehículo, **Then** el sistema le informa que primero debe cargar un tipo de
   vehículo y no le permite completar el alta.
7. **Given** el padrón de transportistas sin ninguno activo, **When** el responsable de Tráfico abre
   el formulario de vehículo, **Then** el sistema le informa que primero debe registrar un
   transportista y no le permite completar el alta.
8. **Given** un vehículo recién registrado sin ningún documento cargado, **When** se lo ve en el
   listado, **Then** figura con estado general de documentación `sin documentación` y no puede
   quedar en estado operativo `disponible`.

---

### User Story 3 - Cargar la documentación de un vehículo (Priority: P1)

El responsable de Tráfico abre la ficha de un vehículo, agrega un documento eligiendo su tipo (VTV,
seguro, RUTA, cédula verde, u otro del catálogo), completa el número, la fecha de emisión y la fecha
de vencimiento, y adjunta el archivo escaneado. El sistema calcula solo si ese documento está
vigente, próximo a vencer o vencido. Si se equivocó al cargarlo, corrige sus datos, y si el documento
no debería estar ahí lo elimina.

**Why this priority**: La documentación al día es el requisito legal que habilita a una unidad a
circular; es el motivo por el que existe el seguimiento de flota y no puede posponerse.

**Independent Test**: Con un vehículo ya registrado (User Story 2) y al menos un tipo de
documentación disponible, se verifica cargando tres documentos con vencimientos lejano, cercano y
pasado, y comprobando que el sistema los muestra como `vigente`, `proximaAvencer` y `vencida`
respectivamente, sin que nadie haya elegido el estado y sin que el campo de estado sea editable.

**Acceptance Scenarios**:

1. **Given** un vehículo registrado y un tipo de documentación disponible, **When** el responsable de
   Tráfico carga número, fecha de emisión, fecha de vencimiento y archivo, **Then** el documento
   queda asociado a ese vehículo y aparece en su ficha con el estado calculado por el sistema.
2. **Given** una fecha de vencimiento anterior o igual a la fecha de emisión, **When** se intenta
   guardar, **Then** el sistema lo rechaza informando que el vencimiento debe ser posterior a la
   emisión.
3. **Given** un documento cuya fecha de vencimiento es posterior a hoy por más días que los días de
   aviso de su tipo, **When** se consulta, **Then** su estado es `vigente`.
4. **Given** un documento cuya fecha de vencimiento cae dentro de los días de aviso de su tipo
   contados desde hoy, **When** se consulta, **Then** su estado es `proximaAvencer`.
5. **Given** un documento cuya fecha de vencimiento ya pasó, **When** se consulta, **Then** su estado
   es `vencida`.
6. **Given** el formulario de documentación, **When** el responsable de Tráfico lo completa, **Then**
   en ningún momento puede elegir ni editar el estado del documento.
7. **Given** un vehículo que ya tiene un documento vigente de un tipo determinado, **When** se carga
   otro documento del mismo tipo con vencimiento posterior, **Then** el sistema lo acepta como
   renovación, el documento anterior queda en el historial del vehículo y deja de contar para el
   estado general y las alertas.
8. **Given** un archivo que no es PDF, JPG ni PNG, o que pesa más de 10 MB, **When** el responsable
   de Tráfico intenta adjuntarlo, **Then** el sistema lo rechaza indicando el motivo y no guarda el
   documento.
9. **Given** un documento cargado con un dato equivocado, **When** el responsable de Tráfico corrige
   su tipo, número, fechas o archivo y guarda, **Then** el documento queda actualizado con las mismas
   validaciones que rigen el alta, y su estado se recalcula con los datos nuevos.
10. **Given** un documento cargado por error o duplicado, **When** el responsable de Tráfico pide
    eliminarlo, **Then** el sistema pide una confirmación explícita advirtiendo que la eliminación no
    se puede deshacer, y al confirmar el documento y su archivo adjunto desaparecen de la ficha.
11. **Given** el pedido de confirmación de eliminación, **When** el responsable de Tráfico cancela,
    **Then** nada cambia.
12. **Given** el catálogo con tipos de ámbito chofer (licencia, psicofísico) y de ámbito vehículo
    (VTV, seguro), **When** el responsable de Tráfico elige el tipo de un documento de vehículo,
    **Then** sólo se le ofrecen los de ámbito vehículo.

---

### User Story 4 - Consultar la flota y el estado de su documentación (Priority: P1)

El responsable de Tráfico consulta el listado de la flota filtrando por transportista, tipo de
vehículo, estado del vehículo y estado de documentación, y abre la ficha de cualquier unidad para ver
sus datos y la lista completa de sus documentos con el estado de cada uno.

**Why this priority**: Es la operación que más se repite: antes de asignar un viaje hay que saber qué
unidad está en condiciones de salir. Sin consulta, el registro de datos no sirve para decidir.

**Independent Test**: Se puede verificar de forma independiente cargando vehículos de distintos tipos
y transportistas con documentación en los tres estados, aplicando combinaciones de filtros y
comprobando que el listado y la ficha muestran exactamente lo esperado.

**Acceptance Scenarios**:

1. **Given** una flota registrada, **When** el responsable de Tráfico abre el listado, **Then** ve
   para cada unidad la patente, la marca, el modelo, el tipo de vehículo, el transportista al que
   pertenece, el estado operativo y un indicador del estado general de su documentación.
2. **Given** el listado de flota, **When** se aplican filtros combinados por transportista, tipo de
   vehículo, estado del vehículo y estado de documentación, **Then** el listado muestra únicamente
   los vehículos que cumplen todas las condiciones a la vez.
3. **Given** una flota con unidades propias y de terceros, **When** el responsable de Tráfico filtra
   por un transportista terciarizado, **Then** ve únicamente las unidades de ese transportista, y
   filtrando por G&T Logística S.A. ve únicamente la flota propia.
4. **Given** el listado de flota, **When** el responsable de Tráfico filtra por "disponible",
   **Then** ningún vehículo con documentación vencida ni sin documentación aparece en el resultado.
5. **Given** un vehículo del listado, **When** el responsable de Tráfico lo selecciona, **Then** ve
   su ficha completa con patente, marca, modelo, tipo, transportista, estado operativo y todos sus
   documentos con tipo, número, fecha de emisión, fecha de vencimiento y estado.
6. **Given** un documento con archivo adjunto, **When** el responsable de Tráfico lo abre desde la
   ficha, **Then** accede al archivo cargado.
7. **Given** un filtro que no coincide con ningún vehículo, **When** se aplica, **Then** el sistema
   muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.
8. **Given** un vehículo con la VTV en regla y el seguro vencido, **When** se lo ve en el listado,
   **Then** su estado general de documentación es `vencida`, porque se muestra el peor estado entre
   sus documentos vigentes de cada tipo.
9. **Given** más de 20 vehículos que cumplen los filtros aplicados, **When** el responsable de
   Tráfico consulta el listado, **Then** ve la primera página con 20 filas, el total de coincidencias
   y la forma de avanzar a las páginas siguientes.
10. **Given** el listado con un filtro de estado aplicado, **When** el responsable de Tráfico lo mira,
   **Then** el control de filtro muestra explícitamente qué estado está filtrando, de modo que
   ninguna fila quede oculta en silencio.
11. **Given** un vehículo guardado como `disponible` cuyo seguro venció ayer, **When** el responsable
    de Tráfico abre el listado, **Then** la unidad figura como `fuera de servicio` sin que nadie haya
    editado nada, y vuelve a figurar como `disponible` apenas se carga la renovación.

---

### User Story 5 - Detectar documentación próxima a vencer o vencida (Priority: P2)

El responsable de Tráfico entra al módulo y ve de inmediato qué vehículos tienen documentación
próxima a vencer o ya vencida, para gestionar la renovación antes de que la unidad quede
inhabilitada para circular.

**Why this priority**: Es el valor concreto que justifica cargar los vencimientos, pero depende de
que ya existan vehículos y documentos cargados (User Stories 2 y 3).

**Independent Test**: Se puede verificar de forma independiente cargando documentos con vencimiento
dentro y fuera de la ventana de aviso de su tipo, y comprobando que solo los primeros aparecen en el
panel de vencimientos.

**Acceptance Scenarios**:

1. **Given** vehículos activos con documentación en distintos estados, **When** el responsable de
   Tráfico abre el módulo, **Then** ve un panel con los vehículos activos que tienen al menos un
   documento `proximaAvencer` o `vencida`, indicando de qué documento se trata y en cuántos días
   vence o cuántos hace que venció.
2. **Given** el panel de vencimientos, **When** el responsable de Tráfico selecciona un vehículo
   alertado, **Then** llega directamente a su ficha con la documentación en cuestión visible.
3. **Given** un documento alertado, **When** se carga su renovación con un vencimiento futuro fuera
   de la ventana de aviso, **Then** el vehículo deja de aparecer en el panel por ese documento.
4. **Given** vehículos con documentación vencida, **When** se los busca en el panel de vencimientos,
   **Then** todos ellos figuran allí, incluidos los que quedaron fuera del filtro "disponible" por
   esa misma causa.
5. **Given** ningún documento próximo a vencer ni vencido, **When** se abre el panel, **Then** el
   sistema informa explícitamente que no hay vencimientos pendientes.

---

### User Story 6 - Modificar, reasignar y dar de baja vehículos (Priority: P3)

El responsable de Tráfico corrige los datos de un vehículo cuando cambian, cambia su estado
operativo, lo reasigna a otro transportista cuando la unidad cambia de dueño —por ejemplo, un
vehículo terciarizado que pasa a ser propio de G&T Logística— y lo da de baja lógicamente cuando deja
de formar parte de la flota.

**Why this priority**: Es necesario para mantener el padrón fiel a la realidad, pero es menos
frecuente que el alta y la consulta, y su ausencia no impide operar el resto del módulo.

**Independent Test**: Se puede verificar de forma independiente editando la marca y el modelo de un
vehículo, reasignándolo a otro transportista, cambiando su estado operativo, dándolo de baja y
comprobando que deja de figurar en el listado sin filtros pero reaparece al filtrar por estado
inactivo, con su registro y su documentación intactos.

**Acceptance Scenarios**:

1. **Given** un vehículo registrado, **When** el responsable de Tráfico corrige sus datos y guarda,
   **Then** el registro queda actualizado y el sistema confirma la operación.
2. **Given** una patente que ya pertenece a otro vehículo, **When** se intenta guardar como nuevo
   valor, **Then** el sistema informa el conflicto y no guarda; conservar la propia patente del
   vehículo no genera ningún conflicto.
3. **Given** un vehículo asignado a un transportista terciarizado, **When** el responsable de Tráfico
   lo reasigna a G&T Logística S.A. y guarda, **Then** el cambio queda registrado, la unidad pasa a
   figurar en la flota propia y su documentación cargada se conserva sin cambios.
4. **Given** el formulario de edición, **When** el responsable de Tráfico intenta dejar el vehículo
   sin transportista o asignarlo a uno inactivo, **Then** el sistema lo rechaza y no guarda.
5. **Given** un vehículo registrado, **When** el responsable de Tráfico pide darlo de baja, **Then**
   el sistema pide una confirmación explícita, y al confirmar el vehículo queda inactivo, desaparece
   del listado sin filtros, vuelve a verse al filtrar por estado inactivo y su registro no se borra.
6. **Given** el pedido de confirmación de baja, **When** el responsable de Tráfico cancela, **Then**
   nada cambia.
7. **Given** un vehículo con al menos un documento `vencida`, **When** se intenta dejarlo en estado
   operativo `disponible`, **Then** el sistema lo impide e informa qué documentación se lo impide.
8. **Given** un vehículo dado de baja, **When** se consulta su ficha filtrando por estado inactivo,
   **Then** su documentación y sus archivos adjuntos siguen intactos.
9. **Given** un transportista con al menos un vehículo activo asociado, **When** se intenta darlo de
   baja desde el Módulo 3, **Then** el sistema lo rechaza e informa cuántos vehículos activos
   dependen de él, además de sus choferes activos.

---

### Edge Cases

- Vehículo sin ningún documento cargado: es válido, la ficha muestra la sección vacía con un mensaje
  explícito, el vehículo figura con estado general `sin documentación` —nunca `en regla`— y no puede
  quedar en estado operativo `disponible` (cubierto en User Story 2 y User Story 3).
- Documento que vence exactamente hoy: se considera `proximaAvencer`, no `vencida`; pasa a `vencida`
  recién al día siguiente (cubierto en User Story 3).
- Tipo de documentación con días de aviso en cero: el documento pasa de `vigente` a `vencida` sin
  período de aviso intermedio (cubierto en User Story 3).
- Se intenta dar de baja un tipo de vehículo que está en uso: se rechaza informando cuántos vehículos
  dependen de él; la baja del tipo y la del vehículo son siempre lógicas, nunca se borra el registro
  (cubierto en User Story 1 y User Story 6).
- Dos operadores registran la misma patente al mismo tiempo: la unicidad se garantiza a nivel de base
  de datos, no solo con la validación previa; quien llega segundo recibe el error de duplicado
  (cubierto en User Story 2).
- Patente escrita con espacios, guiones o en minúsculas: se normaliza a mayúsculas sin separadores
  antes de validar unicidad, para que `ab 123 cd` y `AB123CD` no convivan como registros distintos
  (cubierto en User Story 2).
- Vehículo `disponible` cuyo seguro vence de un día para el otro: deja de estar disponible sin que
  nadie toque nada, porque el estado se calcula al consultarlo (cubierto en User Story 4 y
  User Story 5).
- Vehículo con toda su documentación vencida que renueva un solo documento: sigue sin estar
  disponible mientras le quede al menos un documento `vencida` (cubierto en User Story 4).
- Se elimina el documento vigente de un tipo que tenía renovaciones anteriores: el más reciente de
  los que quedan vuelve a ser el vigente de ese tipo, y el estado del vehículo se recalcula solo
  (cubierto en User Story 3).
- Se elimina el único documento de un vehículo: vuelve a figurar con estado general
  `sin documentación`, no `en regla`, y deja de estar disponible (cubierto en User Story 3 y
  User Story 4).
- Vehículo inactivo con documentación vencida: no aparece en el panel de vencimientos ni en el
  listado sin filtros, porque ya no forma parte de la flota operativa; su documentación se conserva y
  se consulta filtrando por estado inactivo (cubierto en User Story 5 y User Story 6).
- Falla el almacenamiento del archivo adjunto a mitad de la carga: no queda un documento a medias ni
  un archivo huérfano; la operación completa no se aplica y el operador puede reintentar sin volver a
  tipear (cubierto en User Story 3).
- Vehículo terciarizado que pasa a ser propio de G&T Logística: se resuelve reasignándolo a G&T
  Logística S.A. desde su edición, sin volver a cargarlo ni perder su documentación (cubierto en
  User Story 6).
- Se intenta dar de baja un transportista que sólo tiene vehículos ya inactivos: la baja procede,
  porque la restricción alcanza únicamente a los vehículos activos, igual que con los choferes
  (cubierto en User Story 6).
- Se intenta dar de baja un transportista sin choferes activos pero con vehículos activos: se
  rechaza, aunque la regla original del Módulo 3 sólo miraba choferes (cubierto en User Story 6).
- El transportista de un vehículo se da de baja mientras la unidad sigue activa: no puede ocurrir,
  porque la baja se rechaza antes (FR-008d); un vehículo nunca queda apuntando a un transportista
  inactivo.

## Requirements *(mandatory)*

### Functional Requirements

#### Padrón de vehículos

- **FR-001**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica
  vehículos, con patente, marca, modelo, tipo de vehículo, transportista y estado operativo; NO DEBE
  borrarlos físicamente.
- **FR-002**: El sistema DEBE exigir que la patente de un vehículo sea única en toda la flota,
  garantizada con una restricción de unicidad en la base de datos; en una modificación, la
  comparación DEBE excluir al propio vehículo.
- **FR-003**: El sistema DEBE normalizar la patente a mayúsculas y sin espacios, guiones ni puntos
  antes de validar su unicidad y antes de guardarla, tanto al crear como al modificar, de modo que
  `ab 123 cd`, `AB-123-CD` y `AB123CD` sean la misma patente.
- **FR-004**: El sistema DEBE validar que la patente tenga formato de patente argentina, aceptando
  tanto el formato viejo (tres letras y tres dígitos) como el Mercosur (dos letras, tres dígitos y
  dos letras), y DEBE rechazar con un motivo puntual cualquier otro valor.
- **FR-005**: El sistema DEBE exigir que todo vehículo pertenezca a exactamente un tipo de vehículo
  activo, y DEBE impedir el alta o la modificación de un vehículo sin tipo asignado.
- **FR-006**: El sistema DEBE exigir marca y modelo como texto obligatorio de hasta 50 caracteres
  cada uno.
- **FR-007**: La baja de un vehículo DEBE pedir una confirmación explícita antes de ejecutarse, y
  cancelar esa confirmación NO DEBE modificar nada.
- **FR-008**: La baja de un vehículo NO DEBE alterar su documentación: sus documentos y sus archivos
  adjuntos se conservan intactos y siguen visibles en su ficha.

#### Pertenencia a un transportista

- **FR-008a**: El sistema DEBE exigir que todo vehículo pertenezca a exactamente un transportista
  activo, y DEBE impedir el alta o la modificación de un vehículo sin transportista asignado o con
  uno inactivo.
- **FR-008b**: El transportista de un vehículo DEBE ser el mismo `Transportista` del Módulo 3,
  incluida G&T Logística S.A. como transportista propio. Este módulo NO DEBE crear un padrón paralelo
  de transportistas ni un ABM propio: los consume tal como los administra el Módulo 3.
- **FR-008c**: El sistema DEBE permitir reasignar un vehículo a otro transportista activo sin afectar
  su documentación ya cargada ni su estado operativo.
- **FR-008d**: La regla de baja de un transportista del Módulo 3 DEBE extenderse para contemplar
  también su flota: el sistema DEBE rechazar la baja de un transportista que tenga al menos un
  vehículo activo asociado, informando cuántos son junto con sus choferes activos. La baja DEBE
  proceder cuando todos sus choferes y todos sus vehículos están inactivos, o cuando no tiene
  ninguno.

#### Catálogo de tipos de vehículo

- **FR-009**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica tipos de
  vehículo, con nombre único; NO DEBE borrarlos físicamente.
- **FR-010**: El sistema DEBE rechazar la baja de un tipo de vehículo que tenga vehículos asociados,
  informando cuántos son.
- **FR-011**: El sistema NO DEBE ofrecer los tipos de vehículo inactivos al registrar o modificar un
  vehículo; los vehículos ya registrados con un tipo dado de baja DEBEN conservarlo y seguir
  mostrándolo.

#### Estado operativo

- **FR-012**: El estado operativo de un vehículo DEBE tomar exactamente uno de estos dos valores:
  `disponible` y `fuera de servicio`. NO DEBE haber ningún valor intermedio: una unidad parada por
  reparación se registra como `fuera de servicio`.
- **FR-013**: Un vehículo con al menos un documento `vencida` entre los documentos más recientes de
  cada tipo NO DEBE quedar en estado operativo `disponible`, y un vehículo sin ningún documento
  cargado tampoco DEBE quedar `disponible`.
- **FR-014**: El estado operativo lo elige el operador y se guarda tal como lo eligió, pero el estado
  operativo que el sistema muestra y por el que filtra DEBE derivarse al momento de consultarlo: si
  el estado general de documentación del vehículo es `vencida` o `sin documentación`, el sistema DEBE
  mostrarlo y tratarlo como `fuera de servicio` aunque tenga `disponible` guardado. El sistema NO
  DEBE sobrescribir el valor guardado ni depender de un proceso programado que lo mantenga al día:
  cuando el documento se renueva, la unidad DEBE volver a estar disponible por sí sola.
- **FR-014a**: Al guardar un vehículo, el sistema DEBE rechazar el estado `disponible` cuando su
  documentación es `vencida` o `sin documentación`, informando qué documentación se lo impide. Es una
  validación de formulario que le explica el motivo al operador; la regla que gobierna el listado y
  los filtros con el paso del tiempo es la derivación de FR-014.
- **FR-015**: El filtro de estado operativo `disponible` NO DEBE devolver ningún vehículo con
  documentación `vencida` ni `sin documentación`, y esos vehículos DEBEN figurar todos en el panel de
  vencimientos.

#### Documentación del vehículo

- **FR-016**: El sistema DEBE permitir cargar documentos asociados a un vehículo, con tipo de
  documentación, número, fecha de emisión, fecha de vencimiento y archivo adjunto. El número DEBE ser
  obligatorio y de hasta 50 caracteres, y NO DEBE exigirse único: dos documentos del mismo vehículo y
  del mismo tipo pueden compartirlo, porque una póliza conserva su número al renovarse.
- **FR-017**: Este módulo DEBE usar el mismo catálogo de tipos de documentación del Módulo 3, que
  DEBE extenderse con un campo obligatorio que indique a qué se aplica cada tipo: chofer o vehículo.
  NO DEBE crearse un catálogo paralelo ni duplicarse el ABM de tipos ni la regla de días de aviso.
- **FR-017a**: Al cargar o modificar un documento de un vehículo, el sistema DEBE ofrecer únicamente
  los tipos de documentación activos cuyo ámbito es vehículo; los tipos de ámbito chofer NO DEBEN
  aparecer en esa lista, ni los de vehículo en la del Módulo 3.
- **FR-017b**: La baja de un tipo de documentación DEBE rechazarse si tiene documentos asociados,
  contando tanto los documentos de choferes como los de vehículos, e informando cuántos son.
- **FR-017c**: Los tipos de documentación ya cargados por el Módulo 3 DEBEN quedar con ámbito chofer
  al incorporarse el campo nuevo, de modo que ningún documento existente cambie de comportamiento.
- **FR-018**: El sistema DEBE exigir que la fecha de vencimiento de un documento sea posterior a su
  fecha de emisión.
- **FR-019**: El sistema DEBE calcular automáticamente el estado de cada documento, con exactamente
  tres valores posibles: `vigente` cuando faltan más días para el vencimiento que los días de aviso
  de su tipo, `proximaAvencer` cuando el vencimiento cae entre hoy inclusive y esa ventana de aviso,
  y `vencida` cuando la fecha de vencimiento ya pasó.
- **FR-020**: "Hoy" DEBE entenderse como el día en curso en la hora de Argentina (UTC−3),
  independientemente de la zona horaria del servidor o del navegador. Es lo que define el borde de un
  documento que vence exactamente hoy y el momento en que un documento pasa por sí solo al estado
  siguiente.
- **FR-021**: El sistema NO DEBE permitir que ningún usuario elija ni edite manualmente el estado de
  un documento; el campo de estado NO DEBE ser editable en el formulario.
- **FR-022**: El sistema DEBE recalcular el estado de los documentos frente al día en curso, de modo
  que un documento pase por sí solo a `proximaAvencer` y luego a `vencida` sin intervención de nadie.
- **FR-023**: El sistema DEBE permitir que un vehículo tenga varios documentos del mismo tipo,
  conservando los anteriores como historial cuando se carga una renovación.
- **FR-024**: Para cada tipo de documentación, el sistema DEBE considerar vigente únicamente al
  documento más reciente del vehículo para ese tipo, entendiendo por más reciente el de fecha de
  vencimiento más lejana. Solo ese documento DEBE determinar el estado general del vehículo, la
  restricción de FR-013 y las alertas; los anteriores DEBEN quedar como historial visible en la
  ficha.
- **FR-025**: El archivo adjunto DEBE subirse desde el formulario del documento y quedar guardado
  bajo el resguardo del sistema; el sistema DEBE aceptar únicamente archivos PDF, JPG y PNG de hasta
  10 MB, y DEBE rechazar cualquier otro formato o tamaño mayor informando el motivo puntual sin
  guardar el documento.
- **FR-026**: El sistema DEBE permitir modificar un documento ya cargado —su tipo, número, fechas y
  archivo adjunto— aplicando las mismas validaciones que rigen el alta, y DEBE recalcular su estado
  con los datos corregidos.
- **FR-027**: El sistema DEBE permitir eliminar un documento. La eliminación DEBE pedir una
  confirmación explícita que advierta que no se puede deshacer; al confirmarla, el registro del
  documento y su archivo adjunto DEBEN borrarse definitivamente, sin quedar inactivos ni
  recuperables. Cancelar la confirmación NO DEBE modificar nada.
- **FR-028**: El documento es la única entidad de este módulo que se borra físicamente. Los vehículos
  y los tipos de vehículo se dan de baja de forma lógica y NO DEBEN borrarse (FR-001, FR-009).
- **FR-029**: La carga de un documento con archivo DEBE ser todo o nada: si el archivo no llega a
  almacenarse, el sistema NO DEBE guardar el documento, DEBE informar que la carga falló y DEBE
  conservar los datos ya tipeados para reintentar. Al reemplazar el archivo de un documento
  existente, si el archivo nuevo no llega a almacenarse, el documento NO DEBE quedar modificado ni
  perder el archivo que ya tenía.

  > **Nota sobre su verificación**: a diferencia del resto de los requisitos, éste no se puede
  > comprobar operando la aplicación, porque describe una falla que nadie puede provocar desde la
  > pantalla. Su verificación queda delegada a un test automatizado que sustituye el almacén de
  > archivos por uno que falla. Por eso no tiene escenario de aceptación: no sería ejecutable por
  > quien valida el sistema.

#### Listado, ficha y alertas

- **FR-030**: El listado de flota DEBE mostrar patente, marca, modelo, tipo de vehículo,
  transportista, estado operativo y estado general de documentación —calculado sobre los documentos
  más recientes de cada tipo según FR-024—, y DEBE permitir filtrar por transportista, tipo de
  vehículo, estado del vehículo y estado de documentación en cualquier combinación. Los cuatro
  filtros DEBEN ser una selección exacta entre las opciones ya cargadas en el sistema.
- **FR-031**: Sin filtros aplicados, el listado DEBE mostrar únicamente los vehículos activos; los
  dados de baja DEBEN aparecer al elegir ese estado en el filtro.
- **FR-032**: El listado de flota DEBE paginarse del lado del servidor, con 20 filas por página. Los
  filtros DEBEN aplicarse sobre toda la flota antes de paginar, y el sistema DEBE mostrar el total de
  vehículos que cumplen los filtros junto con la página en curso.
- **FR-033**: El estado general de documentación de un vehículo DEBE tomar exactamente uno de estos
  cuatro valores: `sin documentación` cuando no tiene ningún documento cargado, y en caso contrario
  el peor estado entre los documentos más recientes de cada tipo (FR-024), con el orden `vencida` >
  `próxima a vencer` > `en regla`. Los cuatro valores DEBEN estar disponibles como opciones del
  filtro de estado de documentación.
- **FR-034**: El estado general DEBE informar únicamente sobre los documentos que el vehículo tiene
  cargados. Ningún tipo de documentación es obligatorio en este módulo: el sistema NO DEBE inferir
  que a un vehículo le falta un documento que nunca se cargó.
- **FR-035**: El sistema DEBE mostrar un panel con los vehículos activos que tengan al menos un
  documento `proximaAvencer` o `vencida` entre los documentos más recientes de cada tipo, indicando
  el documento afectado y los días que faltan o que pasaron desde el vencimiento. El panel NO DEBE
  incluir vehículos dados de baja ni generar alertas por documentos ya reemplazados por una
  renovación.
- **FR-036**: El sistema DEBE mostrar un mensaje explícito de "sin resultados" o de padrón vacío
  cuando un listado no tiene filas, en vez de una tabla vacía sin explicación.
- **FR-037**: Cuando el listado esté filtrando por un estado, el control DEBE mostrar explícitamente
  cuál: ninguna fila DEBE quedar oculta sin que la pantalla lo diga.
- **FR-038**: La ficha de un vehículo DEBE mostrar patente, marca, modelo, tipo, transportista,
  estado operativo y todos sus documentos con tipo, número, fecha de emisión, fecha de vencimiento y
  estado, y DEBE
  permitir abrir el archivo adjunto de cada documento que lo tenga. El acceso al archivo DEBE quedar
  restringido a los mismos roles habilitados para el módulo (FR-039).
- **FR-039**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados con el rol
  *Tráfico* o *Administrador del sistema*, según el esquema de roles del Módulo 2. El mantenimiento
  del catálogo de tipos de vehículo DEBE quedar restringido al rol *Administrador del sistema*.

### Key Entities *(include if feature involves data)*

- **Vehiculo**: unidad de la flota. Incluye patente (única y normalizada), marca, modelo, tipo de
  vehículo, transportista al que pertenece, estado operativo y estado activo/inactivo. Es la entidad
  principal del módulo: se registra, consulta, modifica y da de baja lógicamente desde aquí, y
  concentra la documentación obligatoria de la unidad.
- **Transportista**: empresa o persona dueña de los vehículos, sea G&T Logística S.A. con su flota
  propia o un tercero contratado. Es la misma entidad que administra el Módulo 3, con su nombre o
  razón social, CUIT, teléfono, email y tipo de persona; este módulo la consume para asignar cada
  vehículo y no la administra. Un transportista agrupa muchos vehículos; cada vehículo pertenece a
  uno solo.
- **TipoVehiculo**: categoría de unidad con la que trabaja la empresa (tractor, semirremolque,
  chasis, utilitario, entre otros). Incluye nombre único. Se administra desde este módulo; un tipo
  agrupa muchos vehículos y cada vehículo pertenece a uno solo.
- **Documentacion del vehículo**: documento obligatorio de una unidad (VTV, seguro, RUTA, cédula
  verde, entre otros). Incluye número, fecha de emisión, fecha de vencimiento, estado calculado
  (`vigente`/`proximaAvencer`/`vencida`) y el archivo escaneado. Pertenece a un único vehículo y a un
  único tipo de documentación. Un vehículo puede tener muchos documentos, incluso varios del mismo
  tipo cuando hay renovaciones.
- **DocumentacionTipo**: categoría de documento que el sistema controla, con nombre único, días de
  anticipación con los que avisa del vencimiento y el ámbito al que se aplica (chofer o vehículo).
  Es el mismo catálogo que administra el Módulo 3, extendido con el ámbito; determina el cálculo del
  estado de los documentos de ese tipo y qué tipos se ofrecen en cada módulo.

### Enumerations

- **VehiculoEstado** (estado operativo del vehículo): `disponible`, `fuera de servicio`. Lo elige el
  operador, pero el valor que se muestra y por el que se filtra se deriva al consultarlo según
  FR-014.
- **DocumentacionAmbito**: `chofer`, `vehiculo`. Aplica al tipo de documentación y define en qué
  módulo se ofrece.
- **DocumentacionEstado**: `vigente`, `proximaAvencer`, `vencida`. Aplica al documento y lo calcula
  el sistema.
- **Estado general de documentación del vehículo** (derivado, no se almacena): `sin documentación`,
  `vencida`, `próxima a vencer`, `en regla`. Aplica al vehículo en el listado y su filtro; lo calcula
  el sistema según FR-033.

### Relationships

- **Transportista 1 — * Vehiculo**: todo vehículo pertenece obligatoriamente a un transportista; un
  transportista puede tener muchos vehículos o ninguno. Es lo que distingue la flota propia de la
  contratada.
- **Vehiculo * — 1 TipoVehiculo**: todo vehículo pertenece obligatoriamente a un tipo; un tipo puede
  tener muchos vehículos o ninguno.
- **Vehiculo 1 — * Documentacion**: un vehículo puede tener muchos documentos o ninguno; todo
  documento pertenece a exactamente un vehículo.
- **Documentacion * — 1 DocumentacionTipo**: todo documento pertenece a exactamente un tipo; un tipo
  puede tener muchos documentos o ninguno.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Partiendo de un padrón vacío, el responsable de Tráfico puede cargar un tipo de
  vehículo, registrar una unidad y cargar su documentación completa sin intervención técnica.
- **SC-002**: El 100% de los intentos de registrar una patente ya existente —escrita en cualquier
  combinación de mayúsculas, espacios o guiones— es rechazado con un mensaje que identifica la causa
  exacta, y ninguno crea un vehículo.
- **SC-003**: El 100% de los vehículos registrados tiene exactamente un tipo de vehículo asignado; el
  sistema rechaza todo intento de dejar una unidad sin tipo.
- **SC-003a**: El 100% de los vehículos registrados tiene exactamente un transportista activo
  asignado; el sistema rechaza todo intento de dejar una unidad sin transportista.
- **SC-003b**: Filtrando la flota por un transportista, el responsable de Tráfico obtiene todas sus
  unidades y ninguna ajena, de modo que puede separar la flota propia de la contratada en un solo
  paso.
- **SC-003c**: El 100% de los vehículos reasignados de transportista conserva íntegra su
  documentación previamente cargada.
- **SC-004**: El 100% de los documentos cargados muestra un estado calculado por el sistema, y ningún
  usuario puede modificarlo manualmente desde ninguna pantalla.
- **SC-005**: El 100% de los documentos que entran en la ventana de aviso de su tipo aparece en el
  panel de vencimientos el mismo día en que corresponde, sin que nadie ejecute ninguna acción.
- **SC-006**: Al filtrar la flota por "disponible", el 0% de los resultados tiene documentación
  vencida o ausente, y el 100% de los vehículos excluidos por esa causa figura en el panel de
  vencimientos.
- **SC-007**: El responsable de Tráfico puede identificar todos los vehículos con documentación
  vencida o próxima a vencer en menos de 3 pasos desde el ingreso al módulo.
- **SC-008**: El 100% de los intentos de dar de baja un tipo de vehículo en uso, o un transportista
  con vehículos activos, es rechazado con el detalle de cuántos registros dependen de él, y ningún
  registro se borra físicamente.
- **SC-009**: El 100% de las bajas de vehículo y de las eliminaciones de documento requiere una
  confirmación explícita previa, y ninguna operación cancelada produce cambios en los datos.
- **SC-010**: El 100% de los vehículos que renuevan un documento deja de figurar en el panel de
  vencimientos por ese documento apenas se carga la renovación, sin que nadie tenga que borrar ni
  editar el documento anterior.
- **SC-011**: El 100% de los archivos adjuntos queda accesible únicamente para usuarios con sesión
  iniciada y rol habilitado en el módulo; ningún archivo se abre desde fuera del sistema.

## Assumptions

- La autenticación y el catálogo de roles (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) ya existen (Módulos 1 y 2); este módulo solo consume esos roles para
  restringir el acceso.
- Gerencia no opera la aplicación: recibe el estado de flota a través de Tráfico, así que este módulo
  no incorpora pantallas ni exportaciones propias para ese rol.
- El mecanismo de carga y resguardo de archivos adjuntos definido en el Módulo 3 se reutiliza tal
  cual: mismos formatos aceptados (PDF, JPG, PNG), mismo límite de 10 MB, mismo acceso restringido
  por endpoint autorizado.
- Este módulo modifica dos cosas del Módulo 3, y son sus únicos cambios fuera del propio alcance (ver
  Clarificaciones, sesión 2026-08-08):
  1. El catálogo `DocumentacionTipo` gana el campo de ámbito (chofer / vehículo) y su pantalla de
     mantenimiento pasa a pedirlo (FR-017). Los tipos ya cargados quedan con ámbito chofer, así que
     ningún documento existente cambia de comportamiento.
  2. La regla de baja de `Transportista` pasa a contar también los vehículos activos, no sólo los
     choferes activos (FR-008d).
- El padrón de transportistas y su ABM provienen del Módulo 3, incluida G&T Logística S.A. cargada
  como un transportista más. Este módulo sólo los consume para asignar vehículos: no agrega pantallas
  de transportista ni les cambia los datos.
- Se asume que al empezar este módulo ya hay transportistas cargados por el Módulo 3. Si el padrón
  estuviera vacío, el alta de vehículos queda bloqueada con un mensaje explícito hasta que se cargue
  al menos uno (User Story 2, escenario 7).
- El cálculo del estado de un documento y del estado general por entidad replica el ya definido en el
  Módulo 3 para choferes, incluidas la ventana de aviso por tipo, la regla del documento más reciente
  por tipo y los cuatro valores del estado general.
- El catálogo de tipos de vehículo arranca vacío y se completa desde la pantalla de tipos de este
  módulo; no se precarga por migración.
- La paginación de 20 filas por página y el orden total del listado siguen la convención ya adoptada
  en el Módulo 3.
- El listado de flota se filtra por tipo, estado del vehículo y estado de documentación, tal como
  indica RF3. La búsqueda por patente, marca o modelo NO forma parte de esta versión; queda anotada
  como candidata para una spec futura.
- La única asignación que incluye este módulo es la del vehículo a su transportista dueño (FR-008a a
  FR-008d). La asignación de vehículos a viajes y a choferes, el control de kilometraje, el mantenimiento
  preventivo, las órdenes de taller, el consumo de combustible, el seguimiento GPS, las
  notificaciones por email o push de vencimientos, la validación de la documentación contra
  organismos externos (CNRT, VTV, aseguradoras) y la auditoría de cambios sobre la flota quedan fuera
  del alcance de este módulo.
- La definición de qué documentación es obligatoria para habilitar a un vehículo a circular queda
  fuera del alcance (FR-034), igual que en el Módulo 3: el estado general refleja lo cargado, no lo
  que falta.
