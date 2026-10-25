# Feature Specification: Gestionar empleados (Módulo 16)

**Feature Branch**: `fix/revision-general`

**Created**: 2026-10-24

**Status**: Implementado — spec retroactiva

**Input**: User description: "Módulo 16 — Gestión de empleados (documentar lo ya implementado). ABM de empleados no choferes sobre la Persona del padrón del Módulo 2 (tipo empleado): listado con búsqueda por nombre, apellido o DNI que muestra activos y dados de baja, alta que fuerza tipo empleado, modificación, baja lógica (rechazada si la persona está vinculada a un usuario, FR-028 del Módulo 2), reactivación con confirmación desde el listado, ficha con datos personales e historial completo de adelantos (Módulo 10) y de cajas a cargo del usuario vinculado (Módulo 11). Permisos empleados.consultar (Administrador del sistema, Administración, Gerencia) y empleados.gestionar (Administrador del sistema, Administración); los historiales exigen además adelantos.consultar y caja.consultar. Formato de nombre, apellido y teléfono según FR-026a del Módulo 2; alta con DNI de un empleado dado de baja se rechaza y manda a reactivarlo desde Empleados."

> **Spec retroactiva.** El módulo se integró a `main` el 2026-10-22 (PR #1) sin spec, y por eso el
> código lo llamaba "Módulo 14", número que ya es de Gestión de pagos. Esta spec describe el
> comportamiento vigente después de la revisión del 2026-10-24 y pasa a ser la referencia del módulo:
> lo que no esté acá no forma parte de su alcance (Principio III).

## Clarifications

### Session 2026-10-24 (revisión del módulo ya implementado)

- Q: ¿Empleados es un padrón propio o una vista del padrón de personas del Módulo 2? → A: Una vista. Un empleado es una Persona del Módulo 2 de tipo *empleado*; el módulo no crea una entidad nueva ni duplica el DNI. Las reglas del padrón (unicidad del DNI, baja lógica, FR-026a, FR-028) rigen igual desde acá.
- Q: ¿Por qué un módulo aparte, si Personas ya tiene ABM? → A: Porque Personas exige `usuarios.gestionar`, que sólo tiene el Administrador del sistema. Administración necesita gestionar al personal administrativo y Gerencia consultarlo sin acceso al resto del Módulo 2; Empleados les da esa pantalla con permisos propios ([sin spec] de AGENTS.md: permiso de lectura propio en vez de otorgar el `gestionar` ajeno).
- Q: ¿El listado muestra a los empleados dados de baja? → A: Sí, marcados como *Dado de baja*, igual que el padrón de personas. Ocultarlos siempre dejaba sin forma de encontrarlos para reactivarlos.
- Q: Un empleado que vuelve a la empresa, ¿cómo se maneja? → A: Se lo reactiva desde el listado de empleados, con confirmación. Es la reactivación de FR-022b del Módulo 2 sobre la misma persona, ofrecida acá para quien gestiona empleados sin acceso a Personas. Registrarlo de nuevo con su DNI se rechaza y manda a reactivarlo.
- Q: ¿Hace falta un control propio para no dar de baja a un empleado con una caja abierta? → A: No. Una caja tiene siempre un usuario responsable, así que ese empleado está vinculado a un usuario y la baja ya la rechaza FR-028 del Módulo 2.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar el padrón de empleados (Priority: P1)

Alguien de Administración, de Gerencia o el Administrador del sistema abre *Empleados* y ve al
personal que no es chofer, activo y dado de baja, ordenado por apellido y nombre. Puede buscar por
nombre, apellido o DNI y abrir la ficha de cualquiera.

**Why this priority**: Es la entrada a todo el módulo y lo único que necesita Gerencia, que sólo
consulta.

**Independent Test**: Con empleados y choferes cargados en el padrón, abrir *Empleados*, comprobar
que aparecen sólo los empleados, con su estado, y que la búsqueda filtra por nombre, apellido o DNI.

**Acceptance Scenarios**:

1. **Given** personas de tipo empleado y de tipo chofer en el padrón, **When** se abre *Empleados*,
   **Then** el listado muestra sólo a los empleados —activos y dados de baja— con nombre, DNI,
   teléfono, email y estado.
2. **Given** el listado abierto, **When** se escribe parte de un nombre, un apellido o un DNI,
   **Then** el listado muestra sólo los empleados que coinciden, sin distinguir mayúsculas.
3. **Given** ningún empleado cargado, **When** se abre *Empleados*, **Then** se ve
   `Todavía no hay empleados registrados.`; y si la búsqueda no encuentra a nadie,
   `No hay empleados que coincidan con la búsqueda.`
4. **Given** una búsqueda escrita rápido, **When** una respuesta anterior llega después de la última,
   **Then** el listado muestra el resultado de lo último que se escribió.
5. **Given** una persona de tipo chofer, **When** se intenta abrir su ficha como empleado, **Then** el
   sistema responde que no encuentra ese empleado.

---

### User Story 2 - Registrar y modificar un empleado (Priority: P1)

Quien gestiona empleados registra a alguien nuevo con sus datos personales y los corrige cuando
cambian. El tipo no se elige: todo lo que se carga desde este módulo es empleado.

**Why this priority**: Sin alta no hay empleados que consultar, asociar a un usuario ni elegir como
beneficiarios de un adelanto.

**Independent Test**: Registrar un empleado desde *Registrar empleado*, comprobar que aparece en el
listado y en *Personas* como empleado, y corregir uno de sus datos.

**Acceptance Scenarios**:

1. **Given** nombre, apellido, DNI, fecha de nacimiento, teléfono y email válidos de alguien que no
   está en el padrón, **When** se guarda, **Then** queda registrado como empleado y se vuelve al
   listado.
2. **Given** un pedido que intenta registrar a la persona como chofer, **When** se guarda desde este
   módulo, **Then** queda registrada igual como empleado.
3. **Given** un nombre con números o un teléfono con letras, **When** se intenta guardar, **Then** la
   pantalla marca el campo con el motivo, sin enviar nada (FR-026a del Módulo 2).
4. **Given** el DNI de una persona activa del padrón, **When** se intenta registrar, **Then** se
   informa `Ese DNI ya está registrado en el padrón.`
5. **Given** el DNI de un empleado dado de baja, **When** se intenta registrar, **Then** se rechaza
   con `Ese DNI es de un empleado dado de baja. Reactivalo desde el listado de empleados en vez de
   cargarlo de nuevo.` y el empleado sigue dado de baja, con sus datos intactos.
6. **Given** un empleado registrado, **When** se corrigen sus datos y se guarda, **Then** el cambio
   queda guardado y sigue siendo empleado aunque el pedido diga otro tipo.

---

### User Story 3 - Dar de baja y reactivar un empleado (Priority: P2)

Quien gestiona empleados da de baja a quien deja la empresa y lo reactiva si vuelve, sin perder sus
datos ni su historial.

**Why this priority**: Mantiene el padrón fiel a la realidad, pero un empleado activo de más no
impide operar.

**Independent Test**: Dar de baja a un empleado sin usuario, comprobar que sigue en el listado como
*Dado de baja*, reactivarlo y comprobar que vuelve a figurar como *Activo* con los mismos datos.

**Acceptance Scenarios**:

1. **Given** un empleado activo sin usuario asociado, **When** se elige *Dar de baja* y se confirma
   `¿Confirmás la baja de {apellido}, {nombre}? El registro se conservará y quedará inactivo.`,
   **Then** queda dado de baja, sigue en el listado como *Dado de baja* y su registro no se borra.
2. **Given** un empleado vinculado a un usuario —tenga o no una caja abierta—, **When** se intenta
   darlo de baja, **Then** se rechaza informando a qué usuario está asociado (FR-028 del Módulo 2) y
   sigue activo.
3. **Given** un empleado dado de baja, **When** se mira su fila, **Then** ofrece *Reactivar* en lugar
   de *Dar de baja*: no conviven.
4. **Given** un empleado dado de baja, **When** se elige *Reactivar* y se confirma
   `¿Confirmás la reactivación de {apellido}, {nombre}?`, **Then** vuelve a estar activo con los
   datos que tenía y se anuncia `{apellido}, {nombre} volvió a estar activo.`
5. **Given** el diálogo de baja o de reactivación abierto, **When** se cancela, **Then** no cambia
   nada.

---

### User Story 4 - Ver la ficha con los historiales (Priority: P2)

Desde el listado se abre la ficha de un empleado: sus datos personales, todos sus adelantos y todas
las cajas que tuvo a cargo su usuario.

**Why this priority**: Concentra en un lugar lo que hoy hay que buscar en tres módulos, pero el
padrón funciona sin ella.

**Independent Test**: Con un empleado que tiene adelantos y un usuario con cajas, abrir su ficha y
comprobar que muestra sólo los suyos.

**Acceptance Scenarios**:

1. **Given** un empleado con adelantos, **When** se abre su ficha, **Then** el historial de adelantos
   muestra todos los suyos y ninguno de otra persona, con fecha, motivo, importe en pesos y estado
   con su nombre (*Pendiente*, *Aprobado*, *Rechazado*, *Anulado*).
2. **Given** un empleado vinculado a un usuario con cajas, **When** se abre su ficha, **Then** *Cajas
   a cargo* muestra todas las cajas de ese usuario y ninguna de otro, de la más reciente a la más
   antigua, con apertura, responsable, estado, cierre y saldo final.
3. **Given** un empleado sin usuario asociado, **When** se abre su ficha, **Then** *Cajas a cargo*
   dice `No hay cajas asociadas al usuario de este empleado.`
4. **Given** que alguno de los dos historiales no se pudo traer, **When** se abre la ficha, **Then**
   la pantalla muestra un error en vez de presentarlo como una lista vacía.

---

### Edge Cases

- **Sin permiso, llegando por la dirección**: el listado y la ficha dicen que falta permiso y a quién
  pedírselo (`… Si lo necesitás, pedíselo a quien administra los usuarios del sistema.`), no
  "volvé a intentar" ([010]). El formulario, sin `empleados.gestionar`, dice `No tenés permiso para
  registrar ni modificar empleados.`
- **Ficha de un empleado dado de baja**: se abre igual y muestra *Dado de baja*; los historiales se
  conservan.
- **Reactivar a alguien que ya está activo** (otra sesión lo reactivó antes): se rechaza con
  `Esa persona ya está activa. Actualizá la lista.`
- **DNI de una persona dada de baja que no es empleado**: el alta se rechaza y manda a reactivarla
  desde el padrón de personas (FR-022b del Módulo 2).
- **Persona cargada antes de FR-026a con otro formato**: se la corrige al editarla.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE tratar al empleado como una Persona del padrón del Módulo 2 de tipo
  *empleado*, sin entidad ni padrón propios. Toda persona de otro tipo DEBE responder como
  inexistente en las operaciones de este módulo.
- **FR-002**: El listado DEBE mostrar a los empleados activos y dados de baja, ordenados por apellido
  y nombre, con nombre, DNI, teléfono, email y estado, y permitir buscar por nombre, apellido o DNI
  con coincidencia parcial y sin distinguir mayúsculas. DEBE poder pedirse sólo los activos.
- **FR-003**: El listado DEBE distinguir el padrón vacío de la búsqueda sin coincidencias, con los
  textos de User Story 1, y DEBE mostrar sólo el resultado de la última búsqueda pedida.
- **FR-004**: El sistema DEBE permitir registrar y modificar un empleado con nombre, apellido, DNI,
  fecha de nacimiento, teléfono y email, todos obligatorios, y NO DEBE pedir el tipo: lo fija en
  *empleado* aunque el pedido diga otro.
- **FR-005**: El alta y la modificación DEBEN aplicar las reglas del padrón: DNI de 7 a 15 dígitos y
  único (FR-027 del Módulo 2), email válido y formato de nombre, apellido y teléfono (FR-026a del
  Módulo 2), validados en la pantalla antes de enviar y en el servidor.
- **FR-006**: Un alta con el DNI de un empleado dado de baja DEBE rechazarse indicando que hay que
  reactivarlo desde el listado de empleados, sin reactivarlo ni modificar sus datos.
- **FR-007**: El sistema DEBE permitir dar de baja lógica a un empleado, con confirmación explícita
  previa, y NO DEBE borrarlo. DEBE rechazar la baja de un empleado vinculado a un usuario, informando
  a cuál (FR-028 del Módulo 2).
- **FR-008**: El sistema DEBE permitir reactivar a un empleado dado de baja desde el listado, con
  confirmación explícita previa, conservando sus datos, y anunciar el resultado. *Dar de baja* y
  *Reactivar* NO DEBEN ofrecerse a la vez sobre el mismo empleado. Reactivar uno activo DEBE
  rechazarse como un conflicto.
- **FR-009**: La ficha DEBE mostrar los datos personales y el estado del empleado, el historial
  completo de sus adelantos y el de las cajas a cargo de su usuario, sin mezclar registros de otras
  personas. Si un historial no se pudo obtener, DEBE mostrar un error y no una lista vacía.
- **FR-010**: El sistema DEBE exigir `empleados.consultar` para el listado y la ficha, y
  `empleados.gestionar` para registrar, modificar, dar de baja y reactivar. El historial de
  adelantos DEBE exigir además `adelantos.consultar`, y el de cajas, `caja.consultar`.
- **FR-011**: `empleados.consultar` DEBE otorgarse a Administrador del sistema, Administración y
  Gerencia; `empleados.gestionar`, a Administrador del sistema y Administración. Los dos se siembran
  por separado: `gestionar` no implica `consultar`.
- **FR-012**: El menú DEBE ofrecer *Empleados* a quien tenga `empleados.consultar`, en la sección de
  personas, y NO DEBE ofrecerla a quien sólo tenga `empleados.gestionar`.
- **FR-013**: Quien no tenga `empleados.gestionar` NO DEBE ver las acciones de registrar, modificar,
  dar de baja ni reactivar. La restricción efectiva DEBE ser la del servidor.

### Key Entities

- **Empleado**: una Persona del padrón del Módulo 2 con tipo *empleado*. Toma de ahí nombre,
  apellido, DNI, fecha de nacimiento, teléfono, email y estado (activa o dada de baja).
- **Usuario vinculado**: el usuario del Módulo 2 asociado a esa persona, si existe. Es el responsable
  de las cajas que muestra la ficha.
- **Adelanto** (Módulo 10) y **Caja** (Módulo 11): se leen, no se modifican desde este módulo.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Alguien de Administración registra un empleado nuevo y lo encuentra en el listado en
  menos de 2 minutos, sin ayuda.
- **SC-002**: Ninguna persona de tipo chofer aparece en el listado ni se puede abrir como empleado.
- **SC-003**: Un empleado dado de baja se encuentra en el listado y se reactiva con un clic más la
  confirmación, y vuelve con los mismos datos que tenía.
- **SC-004**: La ficha de un empleado muestra el 100% de sus adelantos y de las cajas de su usuario, y
  ningún registro de otra persona.
- **SC-005**: Un usuario de Gerencia puede abrir el listado y las fichas, y no ve ninguna acción para
  registrar, modificar, dar de baja ni reactivar.

## Assumptions

- El padrón de personas, su unicidad de DNI, la baja lógica, la reactivación (FR-022b) y el formato
  de los datos (FR-026a) son los del Módulo 2; este módulo no los redefine.
- El listado no se pagina: el personal no chofer de G&T Logística es una lista chica.
- Una caja siempre tiene un usuario responsable (Módulo 11); un empleado sin usuario no tiene cajas.
- Quedan fuera de alcance: datos laborales propios del empleado (legajo, sueldo, fecha de ingreso,
  puesto), convertir un chofer en empleado o al revés, y operar adelantos o cajas desde la ficha.
