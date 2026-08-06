# Feature Specification: Gestionar usuarios y roles (Módulo 2)

**Feature Branch**: `002-gestion-usuarios-roles`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "Módulo 2 — Sistema Integral de Gestión, G&T Logística S.A. Objetivo: que el responsable de sistemas pueda crear un nuevo usuario y asignarle al menos un rol (Tráfico, Administración de la empresa, Gerencia, Administrador del sistema), y luego consultarlo, modificarlo, darlo de baja y ajustar sus roles sin ayuda técnica. Alcance: GU_01 Crear usuario, GU_02 Consultar usuario, GU_03 Modificar usuario (incluye restablecer contraseña), GU_04 Eliminar usuario (baja lógica), GU_05 Asignar roles y permisos (solo lectura de permisos). Entidades: Usuario, Rol, Permiso, Persona (chofer/empleado, asociación opcional 1 a 1). Incluye reglas de negocio RN-01 a RN-11, criterios de éxito CA-01.1 a CA-05.4, casos límite (unicidad concurrente, protección del último administrador, persona ya vinculada, email inexistente en restablecimiento, reactivación, sesión cortada al dar de baja, normalización de username/email) y fuera de alcance (gestión del catálogo de roles/permisos, autoregistro, recuperación de contraseña por el propio usuario, SSO/2FA, auditoría, permisos por registro/sucursal, alta de personas, borrado físico)."

## Clarifications

### Session 2026-08-05

- Q: Cuando el responsable de sistemas quiere asociar una persona (chofer o empleado) a un usuario nuevo, ¿de dónde sale esa lista de personas, si el alta y la gestión de personas están declaradas fuera de alcance y ese módulo todavía no existe? → A: No hay personas precargadas por migración. Este módulo incorpora un ABM completo de Persona con pantalla propia (listado, alta, modificación y baja lógica), y el formulario de usuario elige una persona ya registrada. El alta y la gestión de personas dejan de estar fuera de alcance.
- Q: ¿Qué datos se cargan de una persona (chofer o empleado) y cuál de ellos la identifica de forma única para evitar duplicados en el padrón? → A: Nombre, apellido, DNI, tipo (chofer o empleado), teléfono, email y fecha de nacimiento. El DNI es el único dato con restricción de unicidad; no se pide fecha de ingreso a la empresa.
- Q: Si se da de baja a un usuario que tenía una persona asociada, ¿esa persona queda libre para asociarse a otro usuario, o sigue ocupada? → A: Sigue ocupada mientras el usuario exista, sin importar su estado. Para liberarla hay que desasociarla explícitamente editando ese usuario, y dar de baja una persona vinculada a cualquier usuario se rechaza.
- Q: Al crear un usuario nuevo, ¿el sistema le manda un email con sus credenciales, o el responsable de sistemas se las comunica por fuera? → A: La creación no envía ningún email; el responsable de sistemas comunica las credenciales por su cuenta. El único correo que emite este módulo es el del restablecimiento de contraseña.
- Q: (decisión de planificación) El sistema no tiene todavía ningún mecanismo de correo saliente, aunque esta spec lo daba por existente. ¿Se construye en este módulo o se difiere? → A: Se construye acá, por SMTP, como parte del restablecimiento de contraseña.
- Q: (decisión de planificación) La contraseña temporal vence a las 24 horas y no hay ninguna pantalla donde el usuario pueda cambiarla, así que quien no ingresa en ese plazo queda sin acceso. ¿Se agrega esa pantalla a este módulo? → A: Sí. Se agrega el cambio de contraseña propia, disponible para **cualquier** usuario autenticado, no sólo para el responsable de sistemas. Es la única pantalla del módulo que no exige el rol *Administrador del sistema*.
- Q: Cuando el responsable de sistemas escribe algo en el filtro de username o de email del listado, ¿el sistema busca coincidencias parciales o exige el valor completo? → A: Coincidencia parcial en cualquier parte del texto y sin distinguir mayúsculas. Los filtros de rol y estado siguen siendo una selección exacta de una lista.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear un usuario nuevo con al menos un rol (Priority: P1)

El responsable de sistemas completa un formulario con los datos de un integrante del personal
(username, email, contraseña inicial, estado, opcionalmente la persona asociada) y marca al menos
uno de los cuatro roles del sistema. Al guardar, el usuario queda creado y disponible para
autenticarse.

**Why this priority**: Sin esta funcionalidad no existe forma de dar de alta a nadie en el
sistema; es el punto de partida obligatorio de todo el módulo y una dependencia directa del
módulo de autenticación (Módulo 1).

**Independent Test**: Se puede verificar de forma completa e independiente completando el
formulario con datos válidos y un rol marcado, guardando, y comprobando que el usuario aparece en
el listado con estado `activo`, el rol elegido y `fechaAlta` igual a la fecha del alta.

**Acceptance Scenarios**:

1. **Given** un username y un email que no existen en el sistema, una contraseña de 8 o más
   caracteres, un estado elegido y al menos un rol marcado, **When** el responsable de sistemas
   guarda el formulario, **Then** el usuario queda creado con `fechaAlta` igual a la fecha actual,
   `ultimoAcceso` vacío, y el sistema muestra una confirmación.
2. **Given** el formulario de creación recién abierto, **When** se muestra por primera vez,
   **Then** el campo estado aparece precargado en `activo`.
3. **Given** un campo con formato inválido (por ejemplo un email mal escrito o una contraseña de
   menos de 8 caracteres), **When** el responsable de sistemas intenta guardar, **Then** el
   sistema marca ese campo en rojo con el motivo puntual y no envía el formulario.
4. **Given** un username o un email que ya pertenecen a otro usuario, **When** se intenta guardar,
   **Then** el sistema informa cuál de los dos está duplicado y no crea ningún usuario.
5. **Given** el formulario sin ningún rol marcado, **When** se intenta guardar, **Then** el sistema
   informa que todo usuario debe tener al menos un rol asignado y no crea ningún usuario.

---

### User Story 2 - Consultar usuarios existentes (Priority: P1)

El responsable de sistemas busca y filtra usuarios por username, email, rol y estado, y abre el
detalle de cualquiera de ellos para ver sus datos completos, sus roles y la persona asociada, si
tiene una.

**Why this priority**: Es la operación que más se repite en el uso diario del módulo: antes de
modificar, dar de baja o reasignar roles, el responsable de sistemas necesita encontrar y revisar
al usuario correcto. Sin esto, el resto de las operaciones son inutilizables en la práctica.

**Independent Test**: Se puede verificar de forma independiente cargando usuarios de prueba con
distintos roles y estados, aplicando combinaciones de filtros, y comprobando que el listado y el
detalle muestran exactamente lo esperado.

**Acceptance Scenarios**:

1. **Given** una lista de usuarios existentes, **When** el responsable de sistemas abre el
   listado, **Then** ve para cada uno el username, el email, el estado, los roles asignados, la
   fecha de alta y el último acceso.
2. **Given** el listado de usuarios, **When** se aplican filtros combinados por username, email,
   rol y estado, **Then** el listado muestra únicamente los usuarios que cumplen todas las
   condiciones combinadas.
3. **Given** el listado de usuarios, **When** se escribe un fragmento en el filtro de username o de
   email (por ejemplo "juan"), **Then** aparecen todos los usuarios cuyo username o email contenga
   ese texto en cualquier posición, sin distinguir mayúsculas.
4. **Given** un usuario del listado, **When** el responsable de sistemas lo selecciona, **Then** ve
   su detalle completo, incluida la persona asociada si la tiene, y en ningún momento se muestra
   la contraseña.
5. **Given** un filtro que no coincide con ningún usuario, **When** se aplica, **Then** el sistema
   muestra un mensaje explícito de "sin resultados" en vez de una tabla vacía sin explicación.

---

### User Story 3 - Modificar datos de un usuario y restablecer su contraseña (Priority: P2)

El responsable de sistemas abre el registro de un usuario existente, corrige sus datos (por
ejemplo email, estado o persona asociada) y guarda los cambios; o bien pide un restablecimiento de
contraseña, que genera una temporal y se la envía al usuario por email sin que el responsable de
sistemas la vea.

**Why this priority**: Mantener los datos al día y poder recuperar el acceso de alguien que perdió
su contraseña es necesario para la operación continua, pero depende de que ya existan usuarios
creados (User Story 1) y de poder encontrarlos (User Story 2).

**Independent Test**: Se puede verificar de forma independiente abriendo un usuario existente,
cambiando un dato válido y guardando, y por separado pidiendo un restablecimiento de contraseña y
comprobando que se confirma el envío sin exponer la contraseña en ningún momento.

**Acceptance Scenarios**:

1. **Given** un usuario existente, **When** el responsable de sistemas abre su formulario de
   edición, **Then** ve los datos actuales cargados, sin ningún campo de contraseña.
2. **Given** cambios válidos sobre un usuario existente, **When** se guardan, **Then** el registro
   queda actualizado y el sistema confirma la operación.
3. **Given** un username o email que ya pertenece a otro usuario, **When** se intenta guardar como
   nuevo valor, **Then** el sistema informa el conflicto y no guarda; conservar el propio username
   o email del usuario no genera ningún conflicto.
4. **Given** el detalle de un usuario, **When** el responsable de sistemas pide "Restablecer
   contraseña", **Then** el sistema genera una contraseña temporal, la envía por email al usuario y
   confirma el envío sin mostrarla en pantalla en ningún momento.
5. **Given** un usuario al que se le cambia el estado a `inactivo` o `bloqueado`, **When** el
   cambio se guarda, **Then** ese usuario deja de poder autenticarse desde ese momento.
6. **Given** un usuario con una sesión abierta en otro navegador, **When** el responsable de
   sistemas le restablece la contraseña, **Then** esa sesión queda cortada en la siguiente
   operación que intente, y sólo puede volver a entrar con la contraseña temporal que recibió.

---

### User Story 4 - Asignar roles a un usuario y consultar sus permisos (Priority: P2)

El responsable de sistemas abre el panel de roles de un usuario, ve los cuatro roles disponibles
con los que ya tiene marcados, ajusta la selección y guarda; también puede abrir cualquier rol
para ver, en modo lectura, los permisos que otorga agrupados por módulo.

**Why this priority**: Ajustar roles después del alta inicial es una necesidad frecuente (cambios
de puesto, promociones, correcciones), pero es una operación secundaria respecto de tener el
usuario creado y ubicable.

**Independent Test**: Se puede verificar de forma independiente abriendo el panel de roles de un
usuario existente, cambiando la selección de roles, guardando, y comprobando que los roles
efectivos del usuario coinciden exactamente con lo marcado; y por separado abriendo un rol para ver
sus permisos agrupados por módulo.

**Acceptance Scenarios**:

1. **Given** un usuario existente, **When** el responsable de sistemas abre su panel de roles,
   **Then** ve los cuatro roles del sistema con los ya asignados marcados.
2. **Given** una nueva combinación de roles marcados, **When** se guarda, **Then** los roles del
   usuario quedan exactamente como se dejaron marcados, ni más ni menos.
3. **Given** el panel de roles con todos los roles desmarcados, **When** se intenta guardar,
   **Then** el sistema informa que todo usuario debe tener al menos un rol asignado y no guarda el
   cambio.
4. **Given** cualquiera de los cuatro roles, **When** el responsable de sistemas lo abre, **Then**
   ve sus permisos agrupados por módulo, en modo de solo lectura.

---

### User Story 5 - Dar de baja un usuario (Priority: P3)

El responsable de sistemas elige un usuario, confirma explícitamente la baja, y el usuario pasa a
estado `inactivo`: sigue visible en el listado con ese estado, pero ya no puede autenticarse.

**Why this priority**: Es una operación necesaria para desvincular personal, pero de menor
frecuencia relativa que crear, consultar o modificar, y su ausencia no impide operar el resto del
módulo.

**Independent Test**: Se puede verificar de forma independiente seleccionando un usuario de
prueba, confirmando la baja, y comprobando que queda `inactivo` en el listado y ya no puede
iniciar sesión; y por separado cancelando la confirmación y comprobando que nada cambió.

**Acceptance Scenarios**:

1. **Given** un usuario existente, **When** el responsable de sistemas pide eliminarlo, **Then** el
   sistema pide una confirmación explícita antes de ejecutar cualquier cambio.
2. **Given** la confirmación de baja, **When** se confirma, **Then** el usuario pasa a estado
   `inactivo`, sigue apareciendo en el listado con ese estado, y su registro no se borra.
3. **Given** el pedido de confirmación de baja, **When** el responsable de sistemas cancela,
   **Then** el usuario no sufre ningún cambio.

---

### User Story 6 - Gestionar el padrón de personas (Priority: P2)

El responsable de sistemas abre la pantalla de personas, registra a un chofer o empleado, corrige
sus datos cuando cambian y lo da de baja lógicamente cuando deja de ser necesario. Las personas
registradas y activas son las que después puede elegir al asociar una persona a un usuario.

**Why this priority**: El padrón arranca vacío y no se precarga por migración, así que sin esta
pantalla la asociación entre usuario y persona (User Story 1 y User Story 3) no tiene ninguna
opción para elegir. Es prerrequisito de esa asociación, pero no de crear o consultar usuarios.

**Independent Test**: Se puede verificar de forma independiente registrando una persona desde su
pantalla, comprobando que aparece en el listado, corrigiendo un dato, dándola de baja, y
comprobando que deja de ofrecerse como opción al asociar una persona a un usuario.

**Acceptance Scenarios**:

1. **Given** el padrón sin ninguna persona registrada, **When** el responsable de sistemas abre la
   pantalla de personas, **Then** ve un mensaje explícito de que todavía no hay personas cargadas,
   en vez de una tabla vacía sin explicación.
2. **Given** el nombre, el apellido, el DNI, el tipo (chofer o empleado), el teléfono, el email y
   la fecha de nacimiento de alguien que no está en el padrón, **When** el responsable de sistemas
   los guarda, **Then** la persona queda registrada, aparece en el listado y pasa a estar
   disponible para asociarse a un usuario.
3. **Given** un DNI que ya pertenece a otra persona del padrón, **When** se intenta guardar,
   **Then** el sistema informa que ese DNI ya está registrado y no crea ninguna persona.
4. **Given** una persona ya registrada, **When** el responsable de sistemas corrige sus datos y
   guarda, **Then** el registro queda actualizado y el cambio se refleja en el usuario que la tenga
   asociada.
5. **Given** una persona registrada que no está asociada a ningún usuario, **When** el responsable
   de sistemas la da de baja, **Then** la persona queda inactiva, su registro no se borra, y deja
   de ofrecerse entre las opciones para asociar a un usuario.
6. **Given** una persona vinculada a un usuario, **When** el responsable de sistemas intenta darla
   de baja, **Then** el sistema lo rechaza e informa a qué usuario está vinculada, sin importar el
   estado de ese usuario.

---

### User Story 7 - Cambiar mi propia contraseña (Priority: P2)

Cualquier integrante del personal con sesión abierta —tenga el rol que tenga— entra a su cuenta,
escribe su contraseña actual y una nueva, y la cambia sin depender de nadie.

**Why this priority**: es lo que vuelve utilizable el restablecimiento. La contraseña temporal que
recibe por email vence a las 24 horas, así que sin esta pantalla quien no ingresa dentro de ese plazo
queda sin acceso y necesita pedir otro restablecimiento, indefinidamente. Depende de que el
restablecimiento exista (User Story 3), pero sin ella ese circuito nunca se cierra.

**Independent Test**: Se puede verificar de forma independiente ingresando con cualquier cuenta,
cambiando la contraseña, cerrando sesión y comprobando que se entra con la nueva y no con la
anterior.

**Acceptance Scenarios**:

1. **Given** cualquier usuario con sesión abierta, sin importar sus roles, **When** abre el cambio de
   contraseña, **Then** ve tres campos vacíos y enmascarados —contraseña actual, nueva y repetición
   de la nueva— y ninguno viene precargado.
2. **Given** la contraseña actual correcta y una nueva de 8 o más caracteres repetida igual en los
   dos campos, **When** guarda, **Then** la contraseña queda cambiada, el sistema lo confirma y la
   sesión sigue abierta.
3. **Given** una contraseña actual incorrecta, **When** intenta guardar, **Then** el sistema lo
   rechaza y no cambia nada.
4. **Given** una contraseña nueva de menos de 8 caracteres, o dos repeticiones que no coinciden,
   **When** intenta guardar, **Then** el campo se marca en rojo con el motivo y no se envía nada.
5. **Given** un usuario que ingresó con una contraseña temporal, **When** la cambia por una propia,
   **Then** la nueva es definitiva y deja de vencer a las 24 horas.

---

### Edge Cases

- Dos responsables de sistemas crean el mismo username al mismo tiempo: la unicidad se garantiza a
  nivel de base de datos, no solo con la validación previa; quien llega segundo recibe el error de
  duplicado (cubierto en User Story 1).
- El responsable de sistemas intenta pasar su propia cuenta a `inactivo` o `bloqueado`, o quitarse
  el rol *Administrador del sistema*, o dar de baja al único usuario activo con ese rol: el sistema
  lo rechaza en los tres casos, porque siempre debe existir al menos un usuario activo con el rol
  *Administrador del sistema* (cubierto en User Story 3, User Story 4 y User Story 5).
- Usuario sin persona asociada: es válido, el detalle muestra ese campo vacío (cubierto en User
  Story 1 y User Story 2).
- Se elige una persona que ya está vinculada a otro usuario: el sistema lo informa y no guarda el
  cambio, aunque ese otro usuario esté `inactivo` o `bloqueado`; la única forma de liberar la
  persona es desasociarla editando ese usuario (cubierto en User Story 1 y User Story 3).
- Se intenta dar de baja una persona que está vinculada a un usuario: el sistema lo rechaza e
  informa a qué usuario pertenece, sin importar el estado de ese usuario (cubierto en User
  Story 6).
- Email con formato válido pero inexistente: la creación del usuario se completa igual (no envía
  correo), pero un restablecimiento de contraseña posterior no puede notificarse; el sistema
  informa que el envío falló (cubierto en User Story 1 y User Story 3).
- Reactivación de un usuario dado de baja: se hace cambiando su estado a `activo` desde la edición,
  y debe seguir cumpliendo la regla de tener al menos un rol asignado (cubierto en User Story 3).
- Usuario dado de baja o bloqueado mientras tiene una sesión abierta: esa sesión se corta en la
  siguiente operación que intente, no sigue viva hasta que expire por sí sola (cubierto en User
  Story 3 y User Story 5).
- Usuario con sesión abierta al que le restablecen la contraseña: esa sesión también se corta, por
  la misma vía. Una contraseña que dejó de ser válida no puede seguir sosteniendo una sesión viva.
  El caso simétrico es distinto: quien cambia su **propia** contraseña conserva la sesión desde la
  que la cambió, y pierde las demás que tuviera abiertas (cubierto en User Story 3 y User Story 7).
- Username o email escritos con mayúsculas o espacios de más: se normalizan (recorte de espacios,
  email en minúsculas) antes de validar unicidad, para que variantes como "Juan" y "juan" no
  convivan como usuarios distintos (cubierto en User Story 1 y User Story 3).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE exigir al menos un rol marcado para crear un usuario, y rechazar la
  creación si no hay ninguno marcado.
- **FR-002**: El sistema DEBE exigir que el username sea único en todo el sistema, garantizado con
  una restricción de unicidad en la base de datos; en una modificación, la comparación DEBE excluir
  al propio usuario.
- **FR-003**: El sistema DEBE exigir que el email sea único en todo el sistema y tenga formato
  válido; en una modificación, la comparación DEBE excluir al propio usuario.
- **FR-004**: El sistema DEBE exigir una contraseña de al menos 8 caracteres al crear un usuario, y
  DEBE almacenarla siempre de forma hasheada, sin mostrarla ni devolverla en ninguna respuesta o
  vista.
- **FR-005**: El sistema DEBE ofrecer los estados `activo`, `inactivo` y `bloqueado` para un
  usuario, con `activo` precargado por defecto al crear.
- **FR-006**: El sistema NO DEBE borrar físicamente ningún usuario; la operación de eliminar DEBE
  cambiar su estado a `inactivo`, conservando el registro y su visibilidad en el listado.
- **FR-007**: El sistema DEBE restringir el acceso a este módulo únicamente a usuarios autenticados
  con el rol *Administrador del sistema*, con una sola excepción: el cambio de contraseña propia
  (FR-029), que DEBE estar disponible para cualquier usuario autenticado.
- **FR-008**: El sistema DEBE permitir asociar opcionalmente una persona (chofer o empleado) a un
  usuario, y DEBE impedir que una misma persona quede asociada a más de un usuario a la vez,
  cualquiera sea el estado de ese usuario (`activo`, `inactivo` o `bloqueado`). La única forma de
  liberar una persona DEBE ser desasociarla explícitamente desde la edición del usuario que la
  tiene.
- **FR-009**: El sistema DEBE permitir pedir el restablecimiento de la contraseña de un usuario,
  generando una contraseña temporal, enviándola por email al usuario, y confirmando el envío sin
  exponer la contraseña al responsable de sistemas en ningún momento. El restablecimiento DEBE
  cortar las sesiones que ese usuario tuviera abiertas (FR-032).
- **FR-010**: El sistema DEBE mostrar los permisos de cada rol agrupados por módulo, en modo de
  solo lectura desde este módulo; no DEBE permitir crear, editar ni eliminar roles ni permisos
  desde aquí.
- **FR-011**: El listado de usuarios DEBE mostrar username, email, estado, roles asignados, fecha
  de alta y último acceso, y DEBE permitir filtrar por username, email, rol y estado en cualquier
  combinación. Los filtros de username y email DEBEN buscar coincidencias parciales en cualquier
  parte del texto, sin distinguir mayúsculas; los de rol y estado DEBEN ser una selección exacta
  entre las opciones disponibles.
- **FR-012**: El sistema DEBE mostrar un mensaje explícito de "sin resultados" cuando un filtro no
  coincide con ningún usuario, en vez de una tabla vacía sin explicación.
- **FR-013**: El detalle de un usuario DEBE mostrar sus datos completos, incluida la persona
  asociada si tiene una, y NO DEBE mostrar la contraseña en ninguna circunstancia.
- **FR-014**: El formulario de edición DEBE abrir con los datos actuales del usuario cargados, sin
  incluir ningún campo de contraseña.
- **FR-015**: Cuando el username o el email nuevo de una edición ya pertenece a otro usuario, el
  sistema DEBE informar el conflicto y no guardar el cambio.
- **FR-016**: Cuando el estado de un usuario cambia a `inactivo` o `bloqueado`, el sistema DEBE
  impedir que ese usuario vuelva a autenticarse desde ese momento, incluso si tenía una sesión
  abierta.
- **FR-017**: La eliminación de un usuario DEBE pedir una confirmación explícita antes de
  ejecutarse, y cancelar esa confirmación NO DEBE modificar nada.
- **FR-018**: Al guardar los roles de un usuario, el sistema DEBE dejar sus roles exactamente como
  quedaron marcados, sin agregar ni conservar roles no seleccionados.
- **FR-019**: El sistema DEBE rechazar cualquier operación (cambio de estado, quitar el rol
  *Administrador del sistema*, o baja) que deje al sistema sin ningún usuario activo con el rol
  *Administrador del sistema*, incluyendo cuando la afectada es la propia cuenta de quien realiza
  la operación.
- **FR-020**: El sistema DEBE normalizar el username (recorte de espacios, sin distinguir
  mayúsculas) y el email (recorte de espacios, minúsculas) antes de validar su unicidad, tanto al
  crear como al modificar.
- **FR-021**: Cuando el email de un usuario tiene formato válido pero el envío de la contraseña
  temporal falla, el sistema DEBE informar que el envío no pudo completarse, sin revertir el
  restablecimiento ya registrado. La creación de un usuario NO DEBE enviar ningún correo: el único
  correo que emite este módulo es el del restablecimiento de contraseña.
- **FR-022**: El sistema DEBE permitir registrar, consultar, modificar y dar de baja lógica personas
  (choferes y empleados) desde una pantalla propia de este módulo; NO DEBE borrarlas físicamente.
- **FR-023**: El formulario de usuario DEBE permitir elegir la persona a asociar únicamente entre
  las personas ya registradas y activas en el padrón, y NO DEBE permitir crear una persona nueva
  desde ese formulario.
- **FR-024**: El sistema NO DEBE precargar personas por migración: el padrón arranca vacío y se
  completa exclusivamente desde la pantalla de personas.
- **FR-025**: El listado de personas DEBE mostrar un mensaje explícito cuando no hay ninguna
  persona registrada o ningún resultado, en vez de una tabla vacía sin explicación.
- **FR-026**: El sistema DEBE registrar de cada persona el nombre, el apellido, el DNI, el tipo
  (chofer o empleado), el teléfono, el email y la fecha de nacimiento, y NO DEBE pedir otros datos.
- **FR-027**: El sistema DEBE exigir que el DNI sea único en todo el padrón de personas,
  garantizado con una restricción de unicidad en la base de datos; en una modificación, la
  comparación DEBE excluir a la propia persona. Ningún otro dato de la persona lleva restricción de
  unicidad.
- **FR-028**: El sistema DEBE rechazar la baja de una persona que esté vinculada a un usuario,
  informando a qué usuario pertenece, sin importar el estado de ese usuario; la baja sólo DEBE
  proceder si la persona no está vinculada a ninguno.
- **FR-029**: El sistema DEBE permitir que cualquier usuario autenticado cambie su propia contraseña,
  sin importar sus roles y sin intervención del responsable de sistemas. Un usuario sólo DEBE poder
  cambiar la suya, nunca la de otro.
- **FR-030**: El cambio de contraseña propia DEBE exigir la contraseña actual correcta y una nueva de
  al menos 8 caracteres, escrita dos veces; DEBE rechazar el cambio si la contraseña actual no
  coincide, y NO DEBE mostrar ni precargar ninguna de las dos.
- **FR-031**: Cuando un usuario cambia su propia contraseña, la nueva DEBE quedar como definitiva:
  si la anterior era temporal, DEBE dejar de estar sujeta al vencimiento de 24 horas.
- **FR-032**: Cuando se restablece la contraseña de un usuario, el sistema DEBE cortar todas las
  sesiones que ese usuario tuviera abiertas, a más tardar en la siguiente operación que cada una
  intente. Cuando es el propio usuario quien cambia su contraseña, la sesión desde la que hizo el
  cambio DEBE seguir abierta, y las demás sesiones suyas DEBEN cortarse igual.

### Key Entities *(include if feature involves data)*

- **Usuario**: cuenta de acceso al sistema para un integrante del personal de G&T Logística.
  Incluye username, email, contraseña hasheada, estado (`activo`/`inactivo`/`bloqueado`),
  `fechaAlta`, `ultimoAcceso`, uno o más roles asignados y, opcionalmente, una persona asociada.
  Es la entidad principal de este módulo: se crea, consulta, modifica y da de baja lógicamente
  desde aquí.
- **Rol**: agrupación fija de permisos (Tráfico, Administración de la empresa, Gerencia,
  Administrador del sistema) que se asigna a uno o más usuarios. El catálogo de roles es fijo en
  esta versión; este módulo solo asigna y desasigna roles a usuarios, no los crea ni edita.
- **Permiso**: autorización concreta sobre una funcionalidad del sistema, agrupada por módulo de
  negocio y otorgada a través de un rol. Se consulta en modo lectura desde este módulo para mostrar
  qué habilita cada rol.
- **Persona**: chofer o empleado de G&T Logística, registrado y mantenido desde la pantalla de
  personas de este mismo módulo (alta, consulta, modificación y baja lógica). Incluye nombre,
  apellido, DNI (único en el padrón), tipo (`chofer`/`empleado`), teléfono, email y fecha de
  nacimiento. Puede asociarse opcionalmente a un único usuario a la vez. El padrón no se precarga:
  arranca vacío.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los usuarios creados con datos válidos y al menos un rol queda disponible
  para autenticarse de inmediato, sin intervención técnica.
- **SC-002**: El 100% de los intentos de crear o modificar un usuario con username o email
  duplicado, o sin ningún rol marcado, es rechazado con un mensaje que identifica la causa exacta.
- **SC-003**: El responsable de sistemas puede encontrar cualquier usuario existente combinando
  filtros de username, email, rol y estado, en menos de 3 pasos desde el listado.
- **SC-004**: El 100% de los restablecimientos de contraseña exitosos llega al usuario por email sin
  que la contraseña temporal quede expuesta en ninguna pantalla del sistema.
- **SC-005**: El 100% de los intentos de dejar al sistema sin ningún usuario activo con el rol
  *Administrador del sistema* (por baja, cambio de estado o desasignación del rol) es rechazado.
- **SC-006**: El 100% de los usuarios dados de baja o bloqueados pierde la capacidad de
  autenticarse a más tardar en su siguiente intento de uso del sistema, incluso con una sesión ya
  abierta.
- **SC-007**: El 100% de las bajas de usuario requiere una confirmación explícita previa, y ninguna
  baja cancelada produce cambios en los datos.
- **SC-008**: Partiendo de un padrón vacío, el responsable de sistemas puede registrar una persona
  y asociarla a un usuario nuevo sin intervención técnica ni datos precargados.
- **SC-009**: El 100% de los usuarios que recibe una contraseña temporal puede convertirla en una
  contraseña propia y definitiva por su cuenta, sin volver a pedirle nada al responsable de sistemas
  y sin importar qué roles tenga.
- **SC-010**: El 100% de los restablecimientos de contraseña deja sin efecto las sesiones que ese
  usuario tuviera abiertas, a más tardar en la siguiente operación que cada una intente.

## Assumptions

- El catálogo de los cuatro roles (Tráfico, Administración de la empresa, Gerencia, Administrador
  del sistema) y sus permisos ya existen en el sistema, cargados por migración; este módulo no los
  crea ni los edita.
- El sistema no tenía todavía ningún mecanismo de correo saliente: lo construye este módulo, por
  SMTP, como parte del restablecimiento de contraseña. Si el envío falla, la operación que lo generó
  no se revierte, pero se informa el fallo del envío. La creación de un usuario no dispara ningún
  correo.
- Existe siempre, desde la instalación del sistema, al menos un usuario activo con el rol
  *Administrador del sistema* (ver Módulo 1); este módulo se limita a impedir que esa condición se
  rompa por acciones posteriores.
- La relación entre Usuario y Persona es opcional y de uno a uno: un usuario puede no tener persona
  asociada, y una persona no puede asociarse a más de un usuario a la vez.
- El padrón de personas no se precarga por migración; se completa desde la pantalla de personas de
  este módulo (ver Clarificaciones, sesión 2026-08-05).
- Sigue fuera de alcance la **recuperación** de contraseña iniciada por el propio usuario: la
  pantalla de "olvidé mi contraseña", que se usa **sin** haber podido ingresar. Lo que este módulo sí
  incorpora (FR-029) es el **cambio** de contraseña propia, que se hace con la sesión ya abierta. Son
  dos cosas distintas: quien no puede entrar sigue dependiendo de un restablecimiento pedido al
  responsable de sistemas.
- La creación, edición y baja de roles y permisos, el autoregistro, la recuperación de contraseña
  iniciada por el propio usuario, el login externo (Google/Active Directory), el doble factor, la
  política de expiración de contraseñas, la auditoría de cambios sobre usuarios y roles, y los
  permisos por registro o sucursal quedan fuera de alcance de este módulo, tal como indica la
  especificación de origen. El alta y la gestión de personas figuraban también fuera de alcance en
  la especificación de origen, pero se incorporaron a este módulo (ver Clarificaciones).
