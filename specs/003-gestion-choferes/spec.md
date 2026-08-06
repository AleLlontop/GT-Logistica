# Feature Specification: Gestionar choferes y su documentación (Módulo 3)

**Feature Branch**: `003-gestion-choferes`

**Created**: 2026-08-05

**Status**: Draft

**Input**: User description: "Módulo de choferes del Sistema Integral de Gestión de G&T Logística S.A. Objetivo: que el sector de Tráfico pueda registrar a cada chofer, indicar a qué transportista pertenece (propio de G&T Logística o terciarizado) y mantener al día su documentación obligatoria, sabiendo en todo momento qué documentos están vigentes, próximos a vencer o vencidos. Entidades: Chofer (especialización de Persona), Transportista (nombre, cuit, telefono, email, tipo: TipoPersona), Documentacion (numero, fechaEmision, fechaVencimiento, estado: DocumentacionEstado, archivoUrl) y DocumentacionTipo (nombre, diasAvisoVencimiento). Enumeraciones: TipoPersona {fisica, juridica}, DocumentacionEstado {vigente, proximaAvencer, vencida}. Relaciones: Chofer * — 1 Transportista; Chofer 1 — * Documentacion."

## Clarifications

### Session 2026-08-05

- Q: El Módulo 2 ya define la entidad Persona con tipo chofer/empleado. ¿Cómo se resuelve el solapamiento con la entidad Chofer del diagrama de clases? → A: Chofer especializa a Persona: reutiliza los datos del padrón del Módulo 2 (nombre, apellido, dni, teléfono, email, fecha de nacimiento) y agrega los datos propios de chofer (CUIL y transportista al que pertenece). No se duplica el padrón ni el DNI.
- Q: El diagrama de clases incluye DocumentacionTipo (nombre, diasAvisoVencimiento), que no figuraba en la descripción inicial. ¿Se incluye en este módulo? → A: Sí, con ABM propio dentro de este módulo. Cada documento pertenece a un tipo, y el tipo define a cuántos días del vencimiento el sistema empieza a avisar. DocumentacionEstado tiene exactamente tres valores: vigente, proximaAvencer y vencida.
- Q: El estado de la documentación (vigente / proximaAvencer / vencida), ¿cómo se determina? → A: Automático por fecha. El sistema lo calcula comparando la fechaVencimiento con la fecha actual y los diasAvisoVencimiento del tipo. No es editable a mano.
- Q: ¿Qué alcance tiene Transportista dentro de este spec? → A: ABM completo en este módulo (alta, consulta, modificación y baja lógica), incluyendo a G&T Logística S.A. como un transportista propio más, cargado con sus datos reales. Sin transportistas cargados no hay a quién asignarle los choferes.

### Session 2026-08-06

- Q: Cuando un chofer tiene varios documentos del mismo tipo por renovación, ¿qué documentos cuentan
  para el estado general de documentación del chofer y para el panel de vencimientos? → A: Solo el
  documento más reciente de cada tipo (el de fecha de vencimiento más lejana). Los anteriores quedan
  como historial visible en la ficha, sin afectar el estado general ni generar alertas.
- Q: ¿Cómo llega al sistema el archivo escaneado de un documento: lo sube el operador o pega un
  enlace externo? → A: Lo sube el operador desde el formulario y el sistema lo guarda y custodia.
  Se aceptan PDF, JPG y PNG de hasta 10 MB, y solo los roles habilitados en el módulo pueden abrir
  el archivo. No existe un mecanismo de archivos previo en los Módulos 1 y 2: este módulo lo
  incorpora.
- Q: ¿Los choferes dados de baja aparecen en el panel de vencimientos y en el listado sin filtros? →
  A: No. El panel de vencimientos considera únicamente choferes activos, y el listado muestra por
  defecto solo los activos; los inactivos se consultan eligiendo ese estado en el filtro.
- Q: ¿Qué valores toma el estado general de documentación del chofer en el listado y su filtro? →
  A: Cuatro: `sin documentación`, `vencida`, `próxima a vencer` y `en regla`. Se calcula como el peor
  estado entre los documentos vigentes de cada tipo, y es `sin documentación` cuando el chofer no
  tiene ningún documento cargado. Los cuatro valores están disponibles en el filtro.
- Q: ¿El listado de choferes muestra todos los resultados de una vez o se divide en páginas? → A:
  Paginado del lado del servidor, 20 filas por página, con los filtros aplicados antes de paginar y
  el total de coincidencias a la vista.

### Session 2026-08-06 (revisión de checklist)

- Q: El caso límite del documento sin adjunto decía que el chofer "figura con documentación no
  respaldada", pero el estado general del chofer tiene cuatro valores y ninguno expresa eso. ¿Se
  agrega un valor o se saca la afirmación? → A: Se saca. La distinción entre documento con y sin
  archivo existe **a nivel del documento**, no del chofer. El estado general queda con los cuatro
  valores de FR-029 y el adjunto no lo altera.
- Q: La spec no dice qué documentación necesita tener un chofer para estar en condiciones, así que un
  chofer con un solo documento cargado puede figurar `en regla`. ¿Se define la documentación
  obligatoria? → A: No, queda fuera del alcance de este módulo. Ningún tipo del catálogo es
  obligatorio: el estado general informa sobre los documentos cargados y el sistema no infiere que
  falte uno que nunca se cargó. Queda anotado como candidato para una spec futura.

### Session 2026-08-06 (corrección de documentos)

- Q: ¿Cómo se corrige un documento cargado con un dato equivocado —fecha mal tipeada, tipo que no
  era, papel duplicado—? → A: Se puede corregir y también eliminar. La corrección modifica sus datos
  con las mismas validaciones del alta. La eliminación pide confirmación explícita y **borra el
  documento de verdad**, junto con su archivo adjunto: no queda inactivo ni recuperable. Es la única
  entidad del módulo que se borra físicamente.
- Q: Si el documento se guarda pero su archivo adjunto no se puede almacenar, ¿qué pasa? → A: Todo o
  nada. Si el archivo no queda guardado, el documento tampoco se guarda: el sistema informa que la
  carga falló y conserva lo tipeado para reintentar. Lo mismo vale al reemplazar el archivo de un
  documento existente, que no debe quedar alterado.

### Session 2026-08-06 (cierre de checklist)

- Q: Al dar de baja a un chofer, ¿qué pasa con sus archivos adjuntos? → A: Se conservan intactos. La
  baja es lógica y no toca la documentación: los documentos y sus archivos siguen disponibles en la
  ficha, a la que se llega filtrando por inactivo.
- Q: Un chofer dado de baja que vuelve a trabajar, ¿cómo se maneja? → A: Se lo reactiva desde su
  ficha, con confirmación. Vuelve al listado y al panel de vencimientos con su documentación. No se
  lo registra de nuevo: el DNI es único y esa persona ya es chofer.
- Q: ¿Hay plazo de retención para los archivos escaneados? → A: No. El archivo vive mientras exista
  su documento y se borra al eliminarlo (FR-015c). No hay depuración automática y queda fuera de
  alcance.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrar un transportista (Priority: P1)

El responsable de Tráfico abre la pantalla de transportistas y da de alta a una empresa o a un
transportista unipersonal con su nombre o razón social, CUIT, teléfono, email y tipo de persona
(física o jurídica). Entre ellos se carga también G&T Logística S.A., que es el transportista al
que pertenecen los choferes propios de la empresa.

**Why this priority**: Todo chofer pertenece obligatoriamente a un transportista, así que sin al
menos un transportista cargado no se puede registrar ningún chofer. Es el punto de partida del
módulo.

**Independent Test**: Se puede verificar de forma completa e independiente abriendo la pantalla de
transportistas con el padrón vacío, cargando G&T Logística S.A. y un transportista terciarizado, y
comprobando que ambos aparecen en el listado y quedan disponibles para asignar choferes.

**Acceptance Scenarios**:

1. **Given** el padrón de transportistas vacío, **When** el responsable de Tráfico abre la
   pantalla, **Then** ve un mensaje explícito de que todavía no hay transportistas cargados, en vez
   de una tabla vacía sin explicación.
2. **Given** un nombre o razón social, un CUIT que no existe en el padrón, un teléfono, un email
   con formato válido y un tipo de persona elegido, **When** el responsable de Tráfico guarda,
   **Then** el transportista queda registrado, aparece en el listado y pasa a estar disponible para
   asignarle choferes.
3. **Given** un CUIT que ya pertenece a otro transportista, **When** se intenta guardar, **Then** el
   sistema informa que ese CUIT ya está registrado y no crea ningún transportista.
4. **Given** un CUIT con formato inválido o un email mal escrito, **When** se intenta guardar,
   **Then** el sistema marca ese campo con el motivo puntual y no envía el formulario.
5. **Given** el formulario sin tipo de persona elegido, **When** se intenta guardar, **Then** el
   sistema informa que el tipo (física o jurídica) es obligatorio y no crea ningún transportista.

---

### User Story 2 - Registrar un chofer y asignarlo a su transportista (Priority: P1)

El responsable de Tráfico registra a un chofer con sus datos personales (nombre, apellido, DNI,
CUIL, fecha de nacimiento, teléfono y email) y elige obligatoriamente el transportista al que
pertenece: G&T Logística S.A. si es un chofer propio, o el transportista terciarizado
correspondiente.

**Why this priority**: Es el objetivo central del módulo. Sin el registro del chofer no hay a quién
asociarle documentación ni a quién asignar en un viaje, y la distinción entre chofer propio y
terciarizado es la razón de ser de la relación con Transportista.

**Independent Test**: Se puede verificar de forma independiente con al menos un transportista
cargado, completando el formulario de chofer con datos válidos, guardando, y comprobando que
aparece en el listado con el transportista elegido y estado activo.

**Acceptance Scenarios**:

1. **Given** al menos un transportista activo en el padrón y datos personales válidos con un DNI y
   un CUIL que no existen, **When** el responsable de Tráfico guarda, **Then** el chofer queda
   registrado, activo, asociado al transportista elegido y visible en el listado.
2. **Given** el formulario de chofer sin transportista elegido, **When** se intenta guardar,
   **Then** el sistema informa que el transportista es obligatorio y no crea ningún chofer.
3. **Given** un DNI o un CUIL que ya pertenece a otra persona del padrón, **When** se intenta
   guardar, **Then** el sistema informa cuál de los dos está duplicado y no crea ningún chofer.
4. **Given** el padrón de transportistas sin ninguno activo, **When** el responsable de Tráfico
   abre el formulario de chofer, **Then** el sistema le informa que primero debe registrar un
   transportista y no le permite completar el alta.
5. **Given** una fecha de nacimiento que implica menos de 18 años, **When** se intenta guardar,
   **Then** el sistema lo rechaza informando que un chofer debe ser mayor de edad.

---

### User Story 3 - Cargar la documentación de un chofer (Priority: P1)

El responsable de Tráfico abre la ficha de un chofer, agrega un documento eligiendo su tipo
(licencia de conducir, LiNTI, psicofísico, ART, u otro del catálogo), completa el número, la fecha
de emisión y la fecha de vencimiento, y adjunta el archivo escaneado. El sistema calcula solo si
ese documento está vigente, próximo a vencer o vencido. Si se equivocó al cargarlo, corrige sus
datos, y si el documento no debería estar ahí —un duplicado, un papel cargado en el chofer
equivocado— lo elimina.

**Why this priority**: La documentación al día es el requisito legal que habilita a un chofer a
salir a la ruta; es el motivo por el que existe el seguimiento de choferes y no puede posponerse.

**Independent Test**: Con dos precondiciones —un chofer ya registrado (User Story 2) y al menos un
tipo cargado en el catálogo (User Story 6)—, se verifica cargando tres documentos con vencimientos
lejano, cercano y pasado, y comprobando que el sistema los muestra como `vigente`, `proximaAvencer` y
`vencida` respectivamente sin que nadie haya elegido el estado.

**Precondición sobre User Story 6**: aunque el mantenimiento del catálogo es P2, **su alta tiene que
existir antes que esta historia**, porque todo documento pertenece a un tipo y el catálogo arranca
vacío. Esta historia no es independiente de aquella.

**Acceptance Scenarios**:

1. **Given** un chofer registrado y un tipo de documentación del catálogo, **When** el responsable
   de Tráfico carga número, fecha de emisión, fecha de vencimiento y archivo, **Then** el documento
   queda asociado a ese chofer y aparece en su ficha con el estado calculado por el sistema.
2. **Given** una fecha de vencimiento anterior o igual a la fecha de emisión, **When** se intenta
   guardar, **Then** el sistema lo rechaza informando que el vencimiento debe ser posterior a la
   emisión.
3. **Given** un documento cuya fecha de vencimiento es posterior a hoy por más días que los
   `diasAvisoVencimiento` de su tipo, **When** se consulta, **Then** su estado es `vigente`.
4. **Given** un documento cuya fecha de vencimiento cae dentro de los `diasAvisoVencimiento` de su
   tipo contados desde hoy, **When** se consulta, **Then** su estado es `proximaAvencer`.
5. **Given** un documento cuya fecha de vencimiento ya pasó, **When** se consulta, **Then** su
   estado es `vencida`.
6. **Given** el formulario de documentación, **When** el responsable de Tráfico lo completa,
   **Then** en ningún momento puede elegir ni editar el estado del documento.
7. **Given** un chofer que ya tiene un documento vigente de un tipo determinado, **When** se carga
   otro documento del mismo tipo con vencimiento posterior, **Then** el sistema lo acepta como
   renovación, el documento anterior queda en el historial del chofer y deja de contar para el
   estado general y las alertas: solo el de vencimiento más lejano queda como el vigente de ese
   tipo.
8. **Given** un archivo que no es PDF, JPG ni PNG, o que pesa más de 10 MB, **When** el responsable
   de Tráfico intenta adjuntarlo, **Then** el sistema lo rechaza indicando el motivo y no guarda el
   documento.
9. **Given** un documento cargado con un dato equivocado, **When** el responsable de Tráfico corrige
   su tipo, número, fechas o archivo y guarda, **Then** el documento queda actualizado con las
   mismas validaciones que rigen el alta, y su estado se recalcula con los datos nuevos.
10. **Given** un documento cargado por error o duplicado, **When** el responsable de Tráfico pide
    eliminarlo, **Then** el sistema pide una confirmación explícita advirtiendo que la eliminación
    no se puede deshacer, y al confirmar el documento y su archivo adjunto desaparecen de la ficha.
11. **Given** el pedido de confirmación de eliminación, **When** el responsable de Tráfico cancela,
    **Then** nada cambia.
12. **Given** un chofer con un documento vigente y una renovación anterior del mismo tipo en el
    historial, **When** se elimina el documento vigente, **Then** el anterior vuelve a ser el
    vigente de ese tipo y el estado del chofer se recalcula en consecuencia.

---

### User Story 4 - Consultar choferes y el estado de su documentación (Priority: P1)

El responsable de Tráfico busca choferes filtrando por apellido, DNI, transportista, estado del
chofer y estado de documentación, y abre la ficha de cualquiera para ver sus datos, su
transportista y la lista completa de sus documentos con el estado de cada uno.

**Why this priority**: Es la operación que más se repite: antes de asignar un viaje hay que saber
si el chofer está en condiciones. Sin consulta, el registro de datos no sirve para tomar
decisiones.

**Independent Test**: Se puede verificar de forma independiente cargando choferes de distintos
transportistas con documentación en los tres estados, aplicando combinaciones de filtros y
comprobando que el listado y la ficha muestran exactamente lo esperado.

**Acceptance Scenarios**:

1. **Given** una lista de choferes registrados, **When** el responsable de Tráfico abre el listado,
   **Then** ve para cada uno el apellido y nombre, el DNI, el transportista al que pertenece, el
   estado del chofer y un indicador del estado general de su documentación.
2. **Given** el listado de choferes, **When** se aplican filtros combinados por apellido, DNI,
   transportista, estado del chofer y estado de documentación, **Then** el listado muestra
   únicamente los choferes que cumplen todas las condiciones a la vez.
3. **Given** el listado de choferes, **When** se escribe un fragmento en el filtro de apellido o de
   DNI, **Then** aparecen todos los choferes cuyo apellido o DNI contenga ese texto en cualquier
   posición, sin distinguir mayúsculas.
4. **Given** un chofer del listado, **When** el responsable de Tráfico lo selecciona, **Then** ve su
   ficha completa con sus datos personales, su transportista y todos sus documentos con tipo,
   número, fecha de emisión, fecha de vencimiento y estado.
5. **Given** un documento con archivo adjunto, **When** el responsable de Tráfico lo abre desde la
   ficha, **Then** accede al archivo cargado.
6. **Given** un filtro que no coincide con ningún chofer, **When** se aplica, **Then** el sistema
   muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.
7. **Given** un chofer con la licencia en regla y el psicofísico vencido, **When** se lo ve en el
   listado, **Then** su estado general de documentación es `vencida`, porque se muestra el peor
   estado entre sus documentos vigentes de cada tipo.
8. **Given** más de 20 choferes que cumplen los filtros aplicados, **When** el responsable de
   Tráfico consulta el listado, **Then** ve la primera página con 20 filas, el total de coincidencias
   y la forma de avanzar a las páginas siguientes.

---

### User Story 5 - Detectar documentación próxima a vencer o vencida (Priority: P2)

El responsable de Tráfico entra al módulo y ve de inmediato qué choferes tienen documentación
próxima a vencer o ya vencida, para gestionar la renovación antes de que el chofer quede
inhabilitado.

**Why this priority**: Es el valor concreto que justifica cargar los vencimientos, pero depende de
que ya existan choferes y documentos cargados (User Stories 2 y 3).

**Independent Test**: Se puede verificar de forma independiente cargando documentos con vencimiento
dentro y fuera de la ventana de aviso de su tipo, y comprobando que solo los primeros aparecen en el
panel de alertas.

**Acceptance Scenarios**:

1. **Given** choferes activos con documentación en distintos estados, **When** el responsable de
   Tráfico abre el módulo, **Then** ve un panel con los choferes activos que tienen al menos un
   documento
   `proximaAvencer` o `vencida`, indicando de qué documento se trata y en cuántos días vence o
   cuántos hace que venció.
2. **Given** el panel de alertas, **When** el responsable de Tráfico selecciona un chofer alertado,
   **Then** llega directamente a su ficha con la documentación en cuestión visible.
3. **Given** un documento alertado, **When** se carga su renovación con un vencimiento futuro fuera
   de la ventana de aviso, **Then** el chofer deja de aparecer en el panel por ese documento.
4. **Given** ningún documento próximo a vencer ni vencido, **When** se abre el panel, **Then** el
   sistema informa explícitamente que no hay vencimientos pendientes.

---

### User Story 6 - Mantener el catálogo de tipos de documentación (Priority: P2)

El responsable de Tráfico administra los tipos de documentación que el sistema controla, indicando
para cada uno su nombre y con cuántos días de anticipación quiere que el sistema avise del
vencimiento.

**Why this priority**: Cada documento debe pertenecer a un tipo y la ventana de aviso sale de ahí,
pero el catálogo se configura una vez y cambia poco, así que **su mantenimiento** —modificar los días
de aviso, dar de baja un tipo— es secundario frente a registrar choferes y documentos.

**Salvedad**: el alta de tipos no es secundaria. El catálogo arranca vacío y sin al menos un tipo
cargado no se puede ejercer nada de la User Story 3, que es P1. La prioridad P2 califica al
mantenimiento del catálogo, no a su creación, que hay que construir antes que la carga de
documentación.

**Independent Test**: Se puede verificar de forma independiente creando un tipo con 30 días de
aviso, cargando un documento de ese tipo que vence en 20 días, comprobando que sale
`proximaAvencer`, cambiando el tipo a 10 días de aviso y comprobando que pasa a `vigente`.

**Acceptance Scenarios**:

1. **Given** un nombre que no existe en el catálogo y una cantidad de días de aviso mayor a cero,
   **When** el responsable de Tráfico guarda, **Then** el tipo queda registrado y disponible para
   elegir al cargar documentación.
2. **Given** un nombre que ya existe en el catálogo, **When** se intenta guardar, **Then** el
   sistema informa el duplicado y no crea ningún tipo.
3. **Given** una cantidad de días de aviso negativa o vacía, **When** se intenta guardar, **Then**
   el sistema lo rechaza indicando que debe ser un número mayor o igual a cero.
4. **Given** un tipo existente con documentos ya cargados, **When** el responsable de Tráfico
   cambia sus días de aviso, **Then** el estado de esos documentos se recalcula con el nuevo valor
   la próxima vez que se consultan.
5. **Given** un tipo sin ningún documento asociado, **When** el responsable de Tráfico lo da de
   baja, **Then** el tipo queda inactivo, deja de ofrecerse al cargar documentación y su registro no
   se borra.
6. **Given** un tipo con documentos asociados, **When** se intenta darlo de baja, **Then** el
   sistema lo rechaza e informa cuántos documentos lo están usando.

---

### User Story 7 - Modificar y dar de baja choferes y transportistas (Priority: P3)

El responsable de Tráfico corrige los datos de un chofer o de un transportista cuando cambian, lo
reasigna a otro transportista si el chofer cambia de empresa, y lo da de baja lógicamente cuando
deja de trabajar con G&T Logística.

**Why this priority**: Es necesario para mantener el padrón fiel a la realidad, pero es menos
frecuente que el alta y la consulta, y su ausencia no impide operar el resto del módulo.

**Independent Test**: Se puede verificar de forma independiente editando el teléfono de un chofer,
reasignándolo a otro transportista, dándolo de baja y comprobando que deja de figurar en el listado
sin filtros pero reaparece al filtrar por estado inactivo, con su registro intacto.

**Acceptance Scenarios**:

1. **Given** un chofer registrado, **When** el responsable de Tráfico corrige sus datos y guarda,
   **Then** el registro queda actualizado y el sistema confirma la operación.
2. **Given** un chofer registrado, **When** se lo reasigna a otro transportista activo, **Then** el
   cambio queda guardado y su documentación cargada se conserva sin cambios.
3. **Given** un DNI o CUIL que ya pertenece a otra persona, **When** se intenta guardar como nuevo
   valor, **Then** el sistema informa el conflicto y no guarda; conservar el propio DNI o CUIL del
   chofer no genera ningún conflicto.
4. **Given** un chofer registrado, **When** el responsable de Tráfico pide darlo de baja, **Then**
   el sistema pide una confirmación explícita, y al confirmar el chofer queda inactivo, desaparece
   del listado sin filtros, vuelve a verse al filtrar por estado inactivo y su registro no se borra.
5. **Given** el pedido de confirmación de baja, **When** el responsable de Tráfico cancela, **Then**
   nada cambia.
6. **Given** un transportista sin ningún chofer activo asociado, **When** se lo da de baja, **Then**
   queda inactivo y deja de ofrecerse al registrar o reasignar choferes.
7. **Given** un transportista con al menos un chofer activo asociado, **When** se intenta darlo de
   baja, **Then** el sistema lo rechaza e informa cuántos choferes activos dependen de él.
8. **Given** un chofer inactivo que vuelve a trabajar, **When** el responsable de Tráfico lo reactiva
   desde su ficha y confirma, **Then** vuelve a aparecer en el listado por defecto y en el panel de
   vencimientos si corresponde, con toda su documentación y sus archivos intactos.
9. **Given** una persona que ya es chofer pero está inactiva, **When** se intenta registrarla como
   chofer nuevo, **Then** el sistema lo rechaza e indica que hay que reactivar al chofer existente.

---

### Edge Cases

- Se intenta registrar como chofer a alguien que ya está en el padrón de personas como empleado: el
  sistema reutiliza esa persona en vez de crear un duplicado, porque el DNI es único en todo el
  padrón (cubierto en User Story 2).
- Dos operadores registran el mismo DNI o el mismo CUIT al mismo tiempo: la unicidad se garantiza a
  nivel de base de datos, no solo con la validación previa; quien llega segundo recibe el error de
  duplicado (cubierto en User Story 1 y User Story 2).
- Chofer sin ningún documento cargado: es válido, la ficha muestra la sección vacía con un mensaje
  explícito, y el chofer figura en el listado con estado general `sin documentación`, nunca como
  `en regla` (cubierto en User Story 3 y User Story 4).
- Documento que vence exactamente hoy: se considera `proximaAvencer`, no `vencida`; pasa a
  `vencida` recién al día siguiente (cubierto en User Story 3).
- Tipo de documentación con `diasAvisoVencimiento` en cero: el documento pasa de `vigente` a
  `vencida` sin período de aviso intermedio (cubierto en User Story 3 y User Story 6).
- Falla el almacenamiento del archivo adjunto a mitad de la carga: no queda un documento a medias ni
  un archivo huérfano; la operación completa no se aplica y el operador puede reintentar sin volver a
  tipear (cubierto en User Story 3).
- Se elimina el documento vigente de un tipo que tenía renovaciones anteriores: el más reciente de
  los que quedan vuelve a ser el vigente, y el estado del chofer se recalcula solo (cubierto en
  User Story 3).
- Se elimina el único documento de un chofer: vuelve a figurar con estado general `sin
  documentación`, no `en regla` (cubierto en User Story 3 y User Story 4).
- Chofer reactivado con documentación vencida: vuelve a alertar en el panel apenas se lo reactiva,
  sin que nadie recargue nada, porque el estado se calcula al consultarlo (cubierto en User Story 7).
- Chofer inactivo con documentación vencida: no aparece en el panel de vencimientos ni en el listado
  sin filtros, porque ya no puede salir a la ruta; su documentación se conserva y se consulta
  filtrando por estado inactivo (cubierto en User Story 4 y User Story 5).
- Se intenta dar de baja un transportista que solo tiene choferes ya inactivos: la baja procede,
  porque la restricción alcanza únicamente a los choferes activos (cubierto en User Story 7).
- Se intenta dar de baja a G&T Logística S.A. como transportista: se rechaza mientras tenga choferes
  propios activos, igual que cualquier otro transportista; no recibe trato especial (cubierto en
  User Story 7).
- Chofer terciarizado que pasa a ser propio de G&T Logística: se resuelve reasignándolo a G&T
  Logística S.A. desde su edición, sin volver a cargarlo ni perder su documentación (cubierto en
  User Story 7).
- Documento cargado sin archivo adjunto: es válido, y el sistema lo distingue de un documento con
  archivo **a nivel del documento**, no del chofer. La falta de adjunto NO cambia el estado general
  del chofer, que toma exactamente los cuatro valores de FR-029 (cubierto en User Story 3 y
  User Story 4).
- CUIT o DNI escritos con guiones, puntos o espacios: se normalizan a solo dígitos antes de validar
  unicidad, para que "20-12345678-3" y "20123456783" no convivan como registros distintos (cubierto
  en User Story 1 y User Story 2).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica
  transportistas, con nombre o razón social, CUIT, teléfono, email y tipo de persona; NO DEBE
  borrarlos físicamente.
- **FR-002**: El sistema DEBE ofrecer para el tipo de persona de un transportista exactamente los
  valores `fisica` y `juridica`, y DEBE exigir que se elija uno.
- **FR-003**: El sistema DEBE exigir que el CUIT de un transportista sea único en todo el padrón,
  garantizado con una restricción de unicidad en la base de datos, y tenga formato válido; en una
  modificación, la comparación DEBE excluir al propio transportista.
- **FR-004**: El sistema DEBE tratar a G&T Logística S.A. como un transportista más del padrón, con
  sus datos reales, para poder distinguir a los choferes propios de los terciarizados.
- **FR-005**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica
  choferes, con nombre, apellido, DNI, CUIL, fecha de nacimiento, teléfono y email; NO DEBE
  borrarlos físicamente.
- **FR-005a**: La baja de un chofer NO DEBE alterar su documentación: sus documentos y sus archivos
  adjuntos se conservan intactos y siguen visibles en su ficha.
- **FR-005b**: El sistema DEBE permitir reactivar a un chofer inactivo desde su ficha, con
  confirmación explícita previa. Al reactivarlo, DEBE volver al listado por defecto y al panel de
  vencimientos, con su documentación contando de nuevo. NO DEBE ofrecerse registrar de nuevo a una
  persona que ya es chofer, aunque esté inactiva.
- **FR-006**: El sistema DEBE tratar al chofer como una especialización de la Persona del padrón
  del Módulo 2: los datos personales viven en Persona y NO DEBEN duplicarse, y el DNI DEBE seguir
  siendo único en todo el padrón, incluyendo choferes y empleados.
- **FR-007**: El sistema DEBE exigir que el CUIL de un chofer sea único en todo el padrón,
  garantizado con una restricción de unicidad en la base de datos; en una modificación, la
  comparación DEBE excluir al propio chofer.
- **FR-008**: El sistema DEBE exigir que todo chofer pertenezca a exactamente un transportista
  activo, y DEBE impedir el alta o la modificación de un chofer sin transportista asignado.
- **FR-009**: El sistema DEBE permitir reasignar un chofer a otro transportista activo sin afectar
  su documentación ya cargada.
- **FR-010**: El sistema DEBE rechazar la baja de un transportista que tenga al menos un chofer
  activo asociado, informando cuántos son; la baja DEBE proceder si todos sus choferes están
  inactivos o no tiene ninguno.
- **FR-011**: El sistema DEBE rechazar el registro de un chofer menor de 18 años a la fecha del
  alta.
- **FR-012**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica tipos de
  documentación, con nombre único y días de aviso de vencimiento; NO DEBE borrarlos físicamente.
- **FR-013**: El sistema DEBE exigir que los días de aviso de vencimiento de un tipo sean un número
  entero mayor o igual a cero.
- **FR-014**: El sistema DEBE rechazar la baja de un tipo de documentación que tenga documentos
  asociados, informando cuántos son.
- **FR-015**: El sistema DEBE permitir cargar documentos asociados a un chofer, con tipo de
  documentación, número, fecha de emisión, fecha de vencimiento y archivo adjunto opcional. El número
  DEBE ser obligatorio y de hasta 50 caracteres, y NO DEBE exigirse único: dos documentos del mismo
  chofer y del mismo tipo pueden compartirlo, porque una licencia de conducir conserva su número al
  renovarse.
- **FR-015a**: El archivo adjunto DEBE subirse desde el formulario del documento y quedar guardado
  bajo el resguardo del sistema; el sistema DEBE aceptar únicamente archivos PDF, JPG y PNG de hasta
  10 MB, y DEBE rechazar cualquier otro formato o tamaño mayor informando el motivo puntual sin
  guardar el documento.
- **FR-015b**: El sistema DEBE permitir modificar un documento ya cargado —su tipo de documentación,
  número, fechas y archivo adjunto— aplicando las mismas validaciones que rigen el alta, y DEBE
  recalcular su estado con los datos corregidos.
- **FR-015c**: El sistema DEBE permitir eliminar un documento. La eliminación DEBE pedir una
  confirmación explícita que advierta que no se puede deshacer; al confirmarla, el registro del
  documento y su archivo adjunto DEBEN borrarse definitivamente, sin quedar inactivos ni
  recuperables. Cancelar la confirmación NO DEBE modificar nada.
- **FR-015d**: El documento es la única entidad de este módulo que se borra físicamente. Los
  transportistas, los choferes y los tipos de documentación se dan de baja de forma lógica y NO
  DEBEN borrarse (FR-001, FR-005, FR-012); esa regla NO aplica al documento.
- **FR-015e**: La carga de un documento con archivo DEBE ser todo o nada: si el archivo no llega a
  almacenarse, el sistema NO DEBE guardar el documento, DEBE informar que la carga falló y DEBE
  conservar los datos ya tipeados para reintentar sin volver a completarlos. Al reemplazar el archivo
  de un documento existente, si el archivo nuevo no llega a almacenarse, el documento NO DEBE quedar
  modificado ni perder el archivo que ya tenía.

  > **Nota sobre su verificación**: a diferencia del resto de los requisitos, éste no se puede
  > comprobar operando la aplicación, porque describe una falla que nadie puede provocar desde la
  > pantalla. Su verificación queda delegada a un test automatizado que sustituye el almacén de
  > archivos por uno que falla. Por eso no tiene escenario de aceptación: no sería ejecutable por
  > quien valida el sistema.
- **FR-016**: El sistema DEBE exigir que la fecha de vencimiento de un documento sea posterior a su
  fecha de emisión.
- **FR-017**: El sistema DEBE calcular automáticamente el estado de cada documento, con exactamente
  tres valores posibles: `vigente` cuando faltan más días para el vencimiento que los
  `diasAvisoVencimiento` de su tipo, `proximaAvencer` cuando el vencimiento cae entre hoy inclusive
  y esa ventana de aviso, y `vencida` cuando la fecha de vencimiento ya pasó.
- **FR-017a**: "Hoy" DEBE entenderse como el día en curso en la hora de Argentina (UTC−3),
  independientemente de la zona horaria del servidor o del navegador. Es lo que define el borde de un
  documento que vence exactamente hoy y el momento en que un documento pasa por sí solo al estado
  siguiente (FR-019).
- **FR-018**: El sistema NO DEBE permitir que ningún usuario elija ni edite manualmente el estado de
  un documento.
- **FR-019**: El sistema DEBE recalcular el estado de los documentos frente al día en curso, de modo
  que un documento pase por sí solo a `proximaAvencer` y luego a `vencida` sin intervención de
  nadie.
- **FR-020**: El sistema DEBE permitir que un chofer tenga varios documentos del mismo tipo,
  conservando los anteriores como historial cuando se carga una renovación.
- **FR-020a**: Para cada tipo de documentación, el sistema DEBE considerar vigente únicamente al
  documento más reciente del chofer para ese tipo, entendiendo por más reciente el de fecha de
  vencimiento más lejana. Solo ese documento DEBE determinar el estado general del chofer y las
  alertas; los anteriores DEBEN quedar como historial visible en la ficha, sin afectar el estado
  general ni generar alertas.
- **FR-021**: El sistema DEBE mostrar un panel con los choferes activos que tengan al menos un
  documento `proximaAvencer` o `vencida` entre los documentos más recientes de cada tipo (FR-020a),
  indicando el documento afectado y los días que faltan o que pasaron desde el vencimiento. El panel
  NO DEBE incluir a los choferes inactivos, cualquiera sea el estado de su documentación, ni generar
  alertas por documentos ya reemplazados por una renovación.
- **FR-022**: El listado de choferes DEBE mostrar apellido y nombre, DNI, transportista, estado del
  chofer y estado general de su documentación —calculado sobre los documentos más recientes de cada
  tipo según FR-020a—, y DEBE permitir filtrar por apellido, DNI, transportista, estado del chofer y
  estado de documentación en cualquier combinación. Sin filtros aplicados, el listado DEBE mostrar
  únicamente los choferes activos; los inactivos DEBEN aparecer al elegir ese estado en el filtro.
  Los filtros de apellido y DNI DEBEN buscar coincidencias parciales sin distinguir mayúsculas; los
  de transportista y estados DEBEN ser una selección exacta entre las opciones disponibles.
- **FR-023**: El sistema DEBE mostrar un mensaje explícito de "sin resultados" o de padrón vacío
  cuando un listado no tiene filas, en vez de una tabla vacía sin explicación.
- **FR-024**: La ficha de un chofer DEBE mostrar sus datos personales, su transportista y todos sus
  documentos con tipo, número, fecha de emisión, fecha de vencimiento y estado, y DEBE permitir
  abrir el archivo adjunto de cada documento que lo tenga. El acceso al archivo DEBE quedar
  restringido a los mismos roles habilitados para el módulo (FR-027): no DEBE poder abrirse sin una
  sesión con esos permisos.
- **FR-025**: El sistema DEBE normalizar el DNI, el CUIL y el CUIT a solo dígitos, recortando
  espacios, guiones y puntos, antes de validar su unicidad, tanto al crear como al modificar.
- **FR-026**: La baja de un chofer o de un transportista DEBE pedir una confirmación explícita antes
  de ejecutarse, y cancelar esa confirmación NO DEBE modificar nada.
- **FR-027**: El sistema DEBE restringir el acceso a este módulo a usuarios autenticados con el rol
  *Tráfico* o *Administrador del sistema*, según el esquema de roles del Módulo 2.
- **FR-028**: El sistema DEBE distinguir un chofer sin documentación cargada de un chofer con
  documentación vigente, y NO DEBE mostrarlo como en regla por ausencia de documentos.
- **FR-029**: El estado general de documentación de un chofer DEBE tomar exactamente uno de estos
  cuatro valores: `sin documentación` cuando no tiene ningún documento cargado, y en caso contrario
  el peor estado entre los documentos más recientes de cada tipo (FR-020a), con el orden
  `vencida` > `próxima a vencer` > `en regla`. Los cuatro valores DEBEN estar disponibles como
  opciones del filtro de estado de documentación del listado. No hay ningún quinto valor: la
  presencia o ausencia del archivo adjunto de un documento NO DEBE alterar este estado.
- **FR-029a**: El estado general DEBE informar únicamente sobre los documentos que el chofer tiene
  cargados. Ningún tipo del catálogo es obligatorio: el sistema NO DEBE inferir que a un chofer le
  falta un documento que nunca se cargó, ni marcarlo por esa causa. Un chofer con un solo documento
  al día figura `en regla` respecto de lo que tiene cargado.
- **FR-030**: El listado de choferes DEBE paginarse del lado del servidor, con 20 filas por página.
  Los filtros DEBEN aplicarse sobre todo el padrón antes de paginar, y el sistema DEBE mostrar el
  total de choferes que cumplen los filtros junto con la página en curso.

### Key Entities *(include if feature involves data)*

- **Chofer**: persona habilitada para conducir en nombre de un transportista. Especializa a la
  entidad Persona del Módulo 2, de la que toma nombre, apellido, DNI (único en todo el padrón),
  teléfono, email y fecha de nacimiento, y agrega el CUIL (único) y el transportista al que
  pertenece. Es la entidad principal de este módulo: se registra, consulta, modifica y da de baja
  lógicamente desde aquí, y concentra la documentación obligatoria.
- **Transportista**: empresa o persona que aporta choferes a la operación, sea G&T Logística S.A.
  con sus choferes propios o un tercero contratado. Incluye nombre o razón social, CUIT (único),
  teléfono, email y tipo de persona (`fisica`/`juridica`). Un transportista agrupa muchos choferes;
  cada chofer pertenece a uno solo.
- **Documentacion**: documento obligatorio de un chofer. Incluye número, fecha de emisión, fecha de
  vencimiento, estado calculado (`vigente`/`proximaAvencer`/`vencida`) y la URL del archivo
  escaneado. Pertenece a un único chofer y a un único tipo de documentación. Un chofer puede tener
  muchos documentos, incluso varios del mismo tipo cuando hay renovaciones.
- **DocumentacionTipo**: categoría de documento que el sistema controla (licencia de conducir,
  LiNTI, psicofísico, ART, entre otros). Incluye nombre único y los días de anticipación con los que
  el sistema debe avisar del vencimiento. Se administra desde este módulo y determina el cálculo del
  estado de los documentos de ese tipo.

### Enumerations

- **TipoPersona**: `fisica`, `juridica`. Aplica al transportista.
- **DocumentacionEstado**: `vigente`, `proximaAvencer`, `vencida`. Aplica al documento y lo calcula
  el sistema.
- **Estado general de documentación del chofer** (derivado, no se almacena): `sin documentación`,
  `vencida`, `próxima a vencer`, `en regla`. Aplica al chofer en el listado y su filtro; lo calcula
  el sistema según FR-029.

### Relationships

- **Chofer * — 1 Transportista**: todo chofer pertenece obligatoriamente a un transportista; un
  transportista puede tener muchos choferes o ninguno.
- **Chofer 1 — * Documentacion**: un chofer puede tener muchos documentos o ninguno; todo documento
  pertenece a exactamente un chofer.
- **Documentacion * — 1 DocumentacionTipo**: todo documento pertenece a exactamente un tipo; un tipo
  puede tener muchos documentos o ninguno.
- **Chofer — Persona (especialización)**: el chofer es una Persona del padrón del Módulo 2 con datos
  adicionales; no duplica sus datos personales ni su DNI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Partiendo de un padrón vacío, el responsable de Tráfico puede registrar un
  transportista, un chofer y su documentación completa sin intervención técnica.
- **SC-002**: El 100% de los choferes registrados tiene exactamente un transportista asignado; el
  sistema rechaza todo intento de dejar un chofer sin transportista.
- **SC-003**: El 100% de los intentos de registrar un DNI, un CUIL o un CUIT duplicado es rechazado
  con un mensaje que identifica la causa exacta.
- **SC-004**: El 100% de los documentos cargados muestra un estado calculado por el sistema, y
  ningún usuario puede modificarlo manualmente.
- **SC-005**: El 100% de los documentos que entran en la ventana de aviso de su tipo aparece en el
  panel de vencimientos el mismo día en que corresponde, sin que nadie ejecute ninguna acción.
- **SC-006**: El responsable de Tráfico puede identificar todos los choferes con documentación
  vencida o próxima a vencer en menos de 3 pasos desde el ingreso al módulo.
- **SC-007**: El 100% de los intentos de dar de baja un transportista con choferes activos, o un
  tipo de documentación con documentos asociados, es rechazado con el detalle de qué lo impide.
- **SC-008**: El 100% de las bajas de chofer o transportista y de las eliminaciones de documento
  requiere una confirmación explícita previa, y ninguna operación cancelada produce cambios en los
  datos.
- **SC-009**: El 100% de los choferes reasignados de transportista conserva íntegra su documentación
  previamente cargada.
- **SC-010**: El 100% de los choferes que renuevan un documento deja de figurar en el panel de
  vencimientos por ese documento apenas se carga la renovación, sin que nadie tenga que borrar ni
  editar el documento anterior.
- **SC-011**: El 100% de los archivos adjuntos queda accesible únicamente para usuarios con sesión
  iniciada y rol habilitado en el módulo; ningún archivo se abre desde fuera del sistema.

## Assumptions

- El padrón de Persona y la unicidad de DNI provienen del Módulo 2 (Gestionar usuarios y roles);
  este módulo extiende ese padrón con los datos propios del chofer y no crea un padrón paralelo (ver
  Clarificaciones, sesión 2026-08-05).
- El catálogo de roles (Tráfico, Administración de la empresa, Gerencia, Administrador del sistema)
  y la autenticación ya existen (Módulos 1 y 2); este módulo solo consume esos roles para restringir
  el acceso.
- Los Módulos 1 y 2 no incorporaron ningún mecanismo de carga de archivos, así que el resguardo de
  adjuntos se define en este módulo: el operador sube el archivo desde el formulario, el sistema lo
  guarda bajo su control y la entidad conserva la URL para recuperarlo (ver Clarificaciones, sesión
  2026-08-06).
- El catálogo de tipos de documentación arranca vacío y se completa desde la pantalla de tipos de
  este módulo; no se precarga por migración.
- El transportista G&T Logística S.A. se carga como un registro más del padrón de transportistas, sin
  trato especial en las reglas de baja ni de asignación.
- La asignación de choferes a viajes o vehículos, el bloqueo automático de un chofer con
  documentación vencida al momento de asignarlo, la notificación por email o push de los
  vencimientos, la liquidación de sueldos y adelantos, y la auditoría de cambios sobre choferes y
  documentación quedan fuera del alcance de este módulo.
- La verificación de la autenticidad de los documentos frente a organismos externos (ANSES, CNRT,
  AFIP) queda fuera de alcance: el sistema registra lo que el operador carga.
- No hay plazo de retención ni depuración automática de los archivos adjuntos: cada archivo vive
  mientras exista su documento y se borra al eliminarlo (FR-015c). Una política de retención por
  antigüedad queda fuera del alcance de este módulo (ver Clarificaciones, sesión 2026-08-06, cierre
  de checklist).
- La definición de qué documentación es obligatoria para habilitar a un chofer queda fuera del
  alcance de este módulo (FR-029a). El catálogo de tipos no distingue obligatorios de opcionales, y
  el estado general refleja lo cargado, no lo que falta. Es un candidato explícito para una spec
  futura, junto con el bloqueo del chofer con documentación vencida al asignarlo a un viaje (ver
  Clarificaciones, sesión 2026-08-06, revisión de checklist).
