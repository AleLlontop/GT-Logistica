# Feature Specification: Autenticación de usuarios (Módulo 1)

**Feature Branch**: `001-autenticacion-usuarios`

**Created**: 2026-08-04

**Status**: Draft

**Input**: User description: "Módulo 1 — Sistema Integral de Gestión, G&T Logística S.A. Objetivo: que cualquier integrante del personal de G&T Logística pueda entrar al sistema con su nombre de usuario y contraseña, y acceder únicamente a las funcionalidades que le corresponden según sus roles. Alcance: AU_01 Autenticar usuario (pantalla de inicio de sesión, validación de credenciales, inicio de sesión, registro del último acceso, cierre de sesión). Entidades de solo lectura (salvo `ultimoAcceso`): Usuario, Rol, Permiso. Incluye reglas de negocio RN-01 a RN-10, criterios de éxito CA-01.1 a CA-01.9, casos límite y fuera de alcance (alta de usuarios, recuperación de contraseña, cambio de contraseña, 2FA/SSO, bloqueo automático, auditoría, catálogo de roles — todo eso es del módulo 2)."

## Clarifications

### Session 2026-08-04

- Q: ¿Qué datos iniciales debe dejar cargados el Módulo 1 al instalar el sistema, dado que el Módulo 2 (alta de usuarios) los da por existentes pero no los crea? → A: No se cargan datos de prueba ni cuentas de ejemplo; el sistema queda instalado con un único usuario administrador inicial (estado `activo`, rol *Administrador del sistema*) y el catálogo fijo de roles y permisos necesario para autorizarlo. Todas las demás cuentas se crean desde el Módulo 2.
- Q: Después de un inicio de sesión exitoso, ¿a qué pantalla llega el usuario y qué opciones muestra el menú, si por ahora sólo existen el Módulo 1 y el Módulo 2? → A: Llega a una pantalla de inicio que muestra su nombre de usuario, sus roles y el botón de cerrar sesión. El menú lista únicamente las opciones ya implementadas y autorizadas por sus roles (hoy: *Gestión de usuarios*, sólo para *Administrador del sistema*); los módulos futuros se irán agregando al menú a medida que se implementen, sin anunciarse antes.
- Q: ¿Por cuánto tiempo sigue siendo válida la contraseña temporal que el Módulo 2 le envía por email a un usuario que perdió la suya? → A: Vence a las 24 horas de generada. Pasado ese plazo el ingreso se rechaza con el mensaje genérico de credenciales no válidas y hay que pedir un nuevo restablecimiento desde el Módulo 2.
- Q: ¿El sistema debe limitar la cantidad de intentos de inicio de sesión seguidos que puede hacer un mismo origen, o acepta intentos ilimitados? → A: Límite por origen y cuenta. Tras 5 intentos fallidos contra la misma cuenta desde el mismo origen dentro de 5 minutos, el sistema rechaza temporalmente esa combinación durante 1 minuto, con un mensaje en lenguaje llano. Ninguna cuenta cambia de estado ni queda bloqueada, y no hace falta que intervenga el responsable de sistemas. *(La respuesta original contaba sólo por origen; se ajustó al detectar que toda la oficina sale por una única conexión y quedaría bloqueada en conjunto.)*
- Q: Si un usuario cierra el navegador sin cerrar sesión y lo vuelve a abrir al rato, ¿tiene que volver a escribir sus credenciales o retoma la sesión donde estaba? → A: Tiene que autenticarse de nuevo. La sesión termina al cerrar el navegador, aunque no hayan pasado las 8 horas de inactividad; no hay opción de "mantener la sesión iniciada".

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iniciar sesión con credenciales válidas (Priority: P1)

Un integrante del personal de G&T Logística (administrativo, tráfico, gerencia o el responsable
de sistemas) abre el sistema, escribe su nombre de usuario y contraseña, y accede al sistema
viendo únicamente el menú de opciones que corresponde a sus roles.

**Why this priority**: Es la funcionalidad central del módulo. Sin esto nadie puede usar ninguna
otra parte del Sistema Integral de Gestión — es el punto de entrada obligatorio de toda la
aplicación.

**Independent Test**: Con una cuenta `activa` y credenciales correctas, se puede verificar
completamente iniciando sesión y comprobando que se accede al sistema y que el menú mostrado
coincide con los roles asignados a esa cuenta.

**Acceptance Scenarios**:

1. **Given** un usuario en estado `activo` con username y contraseña correctos, **When** envía el
   formulario de inicio de sesión, **Then** llega a la pantalla de inicio, que muestra su nombre de
   usuario, sus roles y el botón de cerrar sesión, y ve el menú con las opciones ya implementadas
   que sus roles autorizan.
2. **Given** un inicio de sesión exitoso, **When** se completa el ingreso, **Then** el campo
   `ultimoAcceso` del usuario queda actualizado con la fecha y hora del momento.
3. **Given** un usuario que escribe su username con mayúsculas o con espacios de más al costado,
   **When** las credenciales por lo demás son correctas, **Then** el sistema lo reconoce igual
   (normaliza el username con recorte de espacios y sin distinguir mayúsculas) y le permite
   ingresar.
4. **Given** un usuario con el rol *Administrador del sistema* y otro sin ese rol, **When** cada uno
   inicia sesión, **Then** sólo el primero ve la opción *Gestión de usuarios* en su menú, y el
   segundo llega igual a la pantalla de inicio, con el menú sin esa opción.

---

### User Story 2 - Proteger funcionalidades sin sesión activa o sin permisos (Priority: P1)

Cualquier funcionalidad del sistema, salvo la propia pantalla de inicio de sesión, exige una
sesión activa y verifica en el servidor que los roles del usuario autoricen esa operación —
independientemente de si la opción aparece o no en su menú.

**Why this priority**: Es el control de seguridad que hace que el resto del sistema sea confiable.
Sin esto, ocultar una opción del menú sería la única protección, algo insuficiente y fácil de
sortear.

**Independent Test**: Se puede verificar de forma independiente intentando abrir por URL directa
una funcionalidad (a) sin haber iniciado sesión, y (b) habiendo iniciado sesión pero sin el rol
requerido, y comprobando en ambos casos que el servidor rechaza la operación.

**Acceptance Scenarios**:

1. **Given** ningún usuario con sesión activa, **When** se intenta acceder a cualquier URL del
   sistema, **Then** el sistema redirige a la pantalla de inicio de sesión.
2. **Given** un usuario sin sesión que abrió el enlace directo a una funcionalidad que sus roles
   autorizan, **When** se autentica correctamente en la pantalla de ingreso, **Then** llega a esa
   funcionalidad y no a la pantalla de inicio.
3. **Given** un usuario autenticado cuyos roles no incluyen una funcionalidad determinada, **When**
   intenta acceder a esa funcionalidad por URL directa (sin pasar por el menú), **Then** el
   servidor rechaza la operación, sin importar que la opción no estuviera visible en su menú.
4. **Given** un usuario con sesión abierta al que se le quita el único rol que le daba acceso a una
   funcionalidad, **When** intenta usar esa funcionalidad nuevamente, **Then** el servidor la
   rechaza porque los permisos efectivos ya reflejan los roles actuales, no los del momento del
   ingreso.
5. **Given** un usuario cuya cuenta deja de estar `activa` (dado de baja o bloqueada) mientras tiene
   sesión abierta, **When** intenta cualquier operación siguiente, **Then** el sistema la rechaza y
   lo lleva a la pantalla de inicio de sesión.

---

### User Story 3 - Recibir un rechazo claro con credenciales inválidas (Priority: P2)

Un usuario que se equivoca al escribir su nombre de usuario o su contraseña recibe un mensaje que
le informa que las credenciales no son válidas, sin indicar cuál de los dos datos falló, y puede
reintentar de inmediato.

**Why this priority**: Es un flujo de error frecuente (apuro, tipeo, mayúsculas) que debe resolverse
sin ambigüedad y sin filtrar información que ayude a adivinar cuentas existentes.

**Independent Test**: Se puede verificar de forma independiente enviando el formulario con un
username inexistente, y por separado con un username válido y contraseña incorrecta, comprobando
en ambos casos el mismo mensaje genérico y que la pantalla queda lista para reintentar.

**Acceptance Scenarios**:

1. **Given** un username que no existe en el sistema, **When** se envía el formulario de inicio de
   sesión, **Then** el sistema informa que las credenciales no son válidas y no revela que el
   usuario no existe.
2. **Given** un username válido con una contraseña incorrecta, **When** se envía el formulario,
   **Then** el sistema informa el mismo mensaje de credenciales no válidas, sin indicar que el
   username era correcto.
3. **Given** el formulario de inicio de sesión, **When** se envía con el username o la contraseña
   vacíos, **Then** el sistema los marca como obligatorios en la propia pantalla y no llega a
   consultar al servidor.
4. **Given** una contraseña temporal generada hace más de 24 horas, **When** el usuario la usa para
   ingresar, **Then** el sistema muestra el mismo mensaje genérico de credenciales no válidas y no
   inicia sesión.
5. **Given** 5 intentos fallidos contra la misma cuenta desde el mismo equipo en menos de 5 minutos,
   **When** se envía un sexto intento, **Then** el sistema informa en lenguaje llano que hay que
   esperar antes de reintentar, y un minuto después el mismo equipo puede volver a intentarlo con
   normalidad, con la cuenta todavía en estado `activo`.
6. **Given** una cuenta ya frenada por intentos fallidos, **When** otra persona ingresa desde el
   mismo equipo o la misma conexión con su propia cuenta y sus credenciales correctas, **Then**
   accede sin ninguna demora.
7. **Given** cualquier interacción con el formulario de inicio de sesión, **When** se escribe,
   envía o falla la contraseña, **Then** esta nunca se muestra en pantalla, ni queda en la URL, ni
   aparece en ningún registro del sistema.

---

### User Story 4 - Rechazar el ingreso de una cuenta no habilitada (Priority: P2)

Un usuario cuya cuenta existe y cuya contraseña es correcta, pero cuyo estado es `inactivo` o
`bloqueado`, recibe un mensaje distinto que le explica que su cuenta no está habilitada y que debe
contactar al responsable de sistemas — y no logra iniciar sesión.

**Why this priority**: Distingue un problema de cuenta (que el usuario no puede resolver solo) de
un error de tipeo (que sí puede resolver solo), evitando que confunda ambas situaciones y pierda
tiempo reintentando algo que nunca va a funcionar.

**Independent Test**: Se puede verificar de forma independiente con una cuenta en estado `inactivo`
o `bloqueado` cuya contraseña se sabe correcta, comprobando que aparece el mensaje específico de
cuenta no habilitada y que no se inicia sesión.

**Acceptance Scenarios**:

1. **Given** una cuenta en estado `inactivo` con contraseña correcta, **When** se envía el
   formulario de inicio de sesión, **Then** el sistema informa que la cuenta no está habilitada,
   indica contactar al responsable de sistemas, y no inicia sesión.
2. **Given** una cuenta en estado `bloqueado` con contraseña correcta, **When** se envía el
   formulario, **Then** el sistema muestra el mismo mensaje de cuenta no habilitada y no inicia
   sesión.
3. **Given** una cuenta en estado `inactivo` o `bloqueado` con contraseña incorrecta, **When** se
   envía el formulario, **Then** el sistema muestra el mensaje genérico de credenciales no válidas
   (no el de cuenta no habilitada), para no confirmar que la cuenta existe.

---

### User Story 5 - Cerrar sesión de forma definitiva (Priority: P3)

Un usuario que terminó de usar el sistema cierra sesión desde cualquier pantalla, y a partir de ese
momento ni él ni nadie que use ese mismo navegador puede retomar el acceso sin volver a
autenticarse.

**Why this priority**: Es la contraparte necesaria del ingreso, requerida por la spec, pero de
menor criticidad relativa porque su ausencia no impide operar el sistema — sólo debilita el cierre
del ciclo de sesión.

**Independent Test**: Se puede verificar de forma independiente iniciando sesión, cerrándola, y
comprobando que usar el botón "atrás" del navegador o reintentar una operación protegida no
recupera el acceso.

**Acceptance Scenarios**:

1. **Given** un usuario con sesión activa, **When** cierra sesión, **Then** la sesión queda
   invalidada de inmediato.
2. **Given** una sesión recién cerrada, **When** el usuario presiona "atrás" en el navegador,
   **Then** no recupera acceso a ninguna pantalla protegida y se lo redirige a iniciar sesión.
3. **Given** un usuario que cierra el navegador sin cerrar sesión, **When** lo vuelve a abrir antes
   de que pasen las 8 horas, **Then** el sistema le pide autenticarse de nuevo y no retoma la
   sesión anterior.

---

### Edge Cases

- Usuario dado de baja o bloqueado mientras tiene sesión abierta: la próxima operación que intente
  debe rechazarse y llevarlo a la pantalla de ingreso (cubierto en User Story 2).
- Usuario al que le cambian los roles mientras tiene sesión abierta: los permisos efectivos deben
  reflejar los roles nuevos, no los que tenía al momento de entrar (cubierto en User Story 2).
- Username tipeado con mayúsculas o espacios al costado: se normaliza igual que en el alta (trim,
  comparación sin distinguir mayúsculas) para que pueda entrar de todas formas (cubierto en User
  Story 1).
- Sesión vencida en medio de una carga de datos: el sistema informa que la sesión expiró, en
  lenguaje llano, y lo lleva a la pantalla de ingreso, sin mostrar un error técnico.
- Ingreso con contraseña temporal recién restablecida (módulo 2, GU_03): es válida y permite
  entrar sin exigir un cambio de contraseña en ese primer ingreso, dentro de las 24 horas de
  generada.
- Ingreso con una contraseña temporal de más de 24 horas: se rechaza con el mensaje genérico de
  credenciales no válidas, y el usuario debe pedirle al responsable de sistemas un nuevo
  restablecimiento (cubierto en User Story 3).
- Intentos fallidos repetidos sobre la misma cuenta: no hay bloqueo automático de cuentas en esta
  versión; el bloqueo de una cuenta es siempre manual, a cargo del responsable de sistemas desde el
  módulo 2. Lo que sí actúa automáticamente es el límite temporal por origen y cuenta (FR-021), que
  no cambia el estado de ninguna cuenta.
- Usuario legítimo que se equivoca 5 veces seguidas: queda esperando 1 minuto antes de poder
  reintentar; su cuenta sigue `activa` y no necesita que nadie la destrabe (cubierto en User
  Story 3).
- Varias personas de la oficina se equivocan desde la misma conexión a internet: como el contador
  es por origen **y** cuenta, el error de una no frena a las demás; cada quien arrastra sólo sus
  propios intentos fallidos (cubierto en User Story 3).
- El mismo usuario abre sesión en dos equipos a la vez: se permite; no hay sesión única por
  usuario.
- Usuario que cierra el navegador sin cerrar sesión: la sesión termina igual, aunque no se hayan
  cumplido las 8 horas, porque en la oficina se comparten equipos (cubierto en User Story 5).
- Usuario cuyos roles todavía no habilitan ninguna funcionalidad implementada: igual inicia sesión
  y llega a la pantalla de inicio, con el menú vacío (cubierto en User Story 1).
- Usuario sin sesión que abre un enlace directo a una funcionalidad que sus roles NO autorizan:
  tras autenticarse llega a la pantalla de inicio, no a la funcionalidad pedida ni a un error
  (cubierto en User Story 2).
- Sistema sin ningún usuario activo con rol *Administrador del sistema*: se previene fuera de este
  módulo (módulo 2); este módulo garantiza que el usuario administrador inicial exista desde la
  instalación (FR-019) para que el sistema sea operable desde el primer día.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE permitir autenticarse únicamente a usuarios en estado `activo`; los
  estados `inactivo` y `bloqueado` no pueden iniciar sesión aunque la contraseña sea correcta.
- **FR-002**: El sistema DEBE validar las credenciales contra la contraseña almacenada de forma
  hasheada; nunca debe almacenar, mostrar ni recuperar la contraseña en texto plano. El hasheo DEBE
  usar una función pensada específicamente para contraseñas, con un valor aleatorio distinto para
  cada una y un costo de cómputo deliberadamente alto, y DEBE permitir endurecer esos parámetros más
  adelante sin invalidar las contraseñas ya almacenadas.
- **FR-003**: Ante un username inexistente o una contraseña incorrecta, el sistema DEBE informar
  únicamente que las credenciales no son válidas, sin distinguir cuál de los dos datos falló.
- **FR-004**: Ante una cuenta existente en estado `inactivo` o `bloqueado` cuya contraseña sea
  correcta, el sistema DEBE informar que la cuenta no está habilitada e indicar que debe
  contactarse al responsable de sistemas; este mensaje es distinto al de credenciales no válidas.
- **FR-005**: En cada inicio de sesión exitoso, el sistema DEBE actualizar el campo `ultimoAcceso`
  del usuario con la fecha y hora del momento.
- **FR-006**: El sistema DEBE calcular los permisos efectivos de un usuario como la unión de los
  permisos de todos sus roles vigentes en el momento de cada operación, no en el momento del
  ingreso.
- **FR-007**: El sistema DEBE exigir una sesión activa para toda funcionalidad, con la única
  excepción de la propia pantalla de inicio de sesión.
- **FR-008**: El sistema DEBE verificar la autorización en el servidor en cada operación,
  independientemente de si la opción correspondiente está visible u oculta en el menú del cliente.
- **FR-009**: Si un usuario deja de estar `activo` mientras tiene una sesión abierta, el sistema
  DEBE invalidar esa sesión en la siguiente operación que intente.
- **FR-010**: El sistema DEBE expirar automáticamente una sesión tras 8 horas continuas de
  inactividad, renovando ese plazo con cada operación que el usuario realice mientras está activo.
  NO DEBE existir un tope máximo de duración por encima de eso: la inactividad y el cierre del
  navegador (FR-022) son las únicas causas de vencimiento.
- **FR-011**: El formulario de inicio de sesión DEBE marcar el username y la contraseña como
  obligatorios y NO DEBE enviar la solicitud al servidor si alguno de los dos está vacío.
- **FR-012**: El sistema DEBE normalizar el username ingresado (recorte de espacios al costado,
  comparación sin distinguir mayúsculas de minúsculas) al validar credenciales, de la misma forma
  en que se normaliza al crear la cuenta.
- **FR-013**: El sistema DEBE permitir cerrar sesión desde cualquier pantalla, invalidando la
  sesión de forma que no pueda recuperarse el acceso navegando "atrás" en el navegador.
- **FR-014**: El sistema DEBE permitir que un mismo usuario tenga sesiones simultáneas abiertas en
  más de un equipo; no existe restricción de sesión única.
- **FR-015**: Cuando una sesión expira en medio de una operación, el sistema DEBE informar en
  lenguaje llano que la sesión expiró y llevar al usuario a la pantalla de inicio de sesión, sin
  exponer errores técnicos.
- **FR-016**: El sistema NO DEBE bloquear automáticamente una cuenta por intentos fallidos
  repetidos; el bloqueo de cuentas es siempre una acción manual del responsable de sistemas,
  realizada fuera de este módulo. La única restricción automática ante intentos fallidos es el
  límite temporal por origen y cuenta definido en FR-021, que no cambia el estado de ninguna
  cuenta.
- **FR-017**: El sistema DEBE aceptar como válida una contraseña temporal generada por un
  restablecimiento (módulo 2) para iniciar sesión, sin exigir su cambio en ese primer ingreso,
  siempre que no hayan pasado más de 24 horas desde que se generó. Vencido ese plazo, el ingreso
  DEBE rechazarse con el mismo mensaje genérico de credenciales no válidas.
- **FR-018**: La contraseña NO DEBE mostrarse en pantalla en ningún momento, NO DEBE aparecer en la
  URL de ninguna solicitud, y NO DEBE quedar registrada en ningún log del sistema.
- **FR-019**: El sistema DEBE quedar instalado con un único usuario administrador inicial en estado
  `activo` y con el rol *Administrador del sistema*, junto con el catálogo fijo de roles y permisos
  necesario para autorizar sus operaciones. NO DEBE crear ninguna otra cuenta de ejemplo o de
  prueba: el resto de los usuarios se dan de alta desde el Módulo 2.
- **FR-020**: Tras un inicio de sesión exitoso, el sistema DEBE llevar al usuario a una pantalla de
  inicio que muestre su nombre de usuario, sus roles y el botón de cerrar sesión. El menú DEBE
  listar únicamente las opciones ya implementadas que los roles del usuario autorizan; NO DEBE
  anunciar módulos todavía no implementados. Si ninguna opción está disponible para sus roles, el
  menú se muestra vacío y la pantalla de inicio sigue siendo accesible.
- **FR-021**: Tras 5 intentos de inicio de sesión fallidos contra la misma cuenta desde el mismo
  origen dentro de una ventana de 5 minutos, el sistema DEBE rechazar temporalmente durante 1
  minuto los intentos siguientes de esa combinación de origen y cuenta, informándolo en lenguaje
  llano. Los intentos sobre otras cuentas desde ese mismo origen NO DEBEN verse afectados. El
  origen se identifica por la dirección de red desde la que llega la petición. Esta restricción NO
  DEBE cambiar el estado de ninguna cuenta ni requerir intervención del responsable de sistemas, y
  DEBE levantarse sola al cumplirse el plazo.
- **FR-022**: La sesión DEBE terminar al cerrarse el navegador, aunque no se hayan cumplido las 8
  horas de inactividad: al volver a abrirlo, el usuario DEBE autenticarse de nuevo. El sistema NO
  DEBE ofrecer ninguna opción de "mantener la sesión iniciada".
- **FR-023**: El dato que identifica la sesión de un usuario NO DEBE ser accesible desde el código
  que se ejecuta dentro de la página y NO DEBE acompañar a peticiones originadas desde otros
  sitios. Ambas condiciones rigen durante toda la vida de la sesión, no sólo al iniciarla; su
  transporte cifrado queda cubierto por FR-024.
- **FR-024**: Toda credencial —tanto la contraseña como el dato que identifica la sesión— DEBE
  viajar únicamente por conexiones cifradas, en el ingreso y en cualquier operación posterior. El
  sistema NO DEBE aceptar una credencial que llegue por una conexión sin cifrar.
- **FR-025**: La pantalla de inicio de sesión y la pantalla de inicio DEBEN poder operarse
  completamente con el teclado, sin necesidad de mouse; cada campo DEBE tener una etiqueta visible
  asociada; los mensajes de error DEBEN anunciarse a los lectores de pantalla al aparecer; y los
  textos DEBEN mantener un contraste suficiente para leerse sin dificultad. Estas cuatro
  condiciones son el piso mínimo de accesibilidad del producto y rigen también para los módulos
  siguientes.
- **FR-026**: Cuando un usuario sin sesión intenta abrir una funcionalidad concreta y el sistema lo
  lleva a la pantalla de inicio de sesión, tras autenticarse correctamente DEBE llevarlo a la
  funcionalidad que había pedido. Si sus roles no la autorizan, o si el destino guardado no
  corresponde a una pantalla de la propia aplicación, DEBE llevarlo a la pantalla de inicio. El
  sistema NO DEBE aceptar como destino ninguna dirección externa.

### Key Entities *(include if feature involves data)*

- **Usuario**: persona del personal de G&T Logística con acceso al sistema. Para esta
  funcionalidad se consulta en modo lectura (username, contraseña hasheada, estado
  `activo`/`inactivo`/`bloqueado`, roles asignados) salvo el campo `ultimoAcceso`, que esta
  funcionalidad sí actualiza. El alta y la edición de usuarios son responsabilidad del módulo 2.
- **Rol**: agrupación de permisos asignada a uno o más usuarios. Se consulta en modo lectura para
  determinar qué funcionalidades ve y puede ejecutar cada usuario. El catálogo de roles es fijo y
  queda cargado en la instalación (FR-019); la asignación de roles a usuarios es responsabilidad
  del módulo 2, que además garantiza que todo usuario tenga al menos un rol.
- **Permiso**: autorización concreta sobre una funcionalidad del sistema, otorgada a través de uno
  o más roles. Se consulta en modo lectura; el catálogo de permisos y su asociación a los roles
  queda cargado en la instalación (FR-019) y ningún módulo lo edita en esta versión.
- **Sesión**: representa el período en que un usuario permanece autenticado tras un ingreso
  exitoso, con una expiración por inactividad y un cierre al cerrarse el navegador. No es una
  entidad de negocio persistente del dominio, pero es central al comportamiento de esta
  funcionalidad (inicio, vigencia, expiración y cierre).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los usuarios en estado `activo` que ingresan username y contraseña
  correctos llegan a la pantalla de inicio y ven en el menú exactamente las opciones ya
  implementadas que sus roles autorizan, sin intervención de soporte.
- **SC-002**: El 100% de los inicios de sesión exitosos deja registrada la fecha y hora reales en
  `ultimoAcceso`.
- **SC-003**: Ante credenciales inválidas o cuenta no habilitada, el 100% de los usuarios recibe un
  mensaje que entiende sin ayuda técnica y que le indica claramente qué hacer a continuación
  (reintentar o contactar al responsable de sistemas).
- **SC-004**: El 100% de los intentos de acceder a una funcionalidad no autorizada por URL directa
  —sin sesión, o con sesión pero sin el rol requerido— es rechazado por el servidor.
- **SC-005**: Ningún usuario logra recuperar el acceso a una pantalla protegida usando el botón
  "atrás" del navegador después de haber cerrado sesión.
- **SC-006**: Ninguna contraseña queda expuesta en pantalla, en una URL o en un registro del
  sistema, ni viaja por una conexión sin cifrar; verificable por inspección directa de la interfaz,
  de las direcciones visitadas y del tráfico del navegador.
- **SC-007**: A partir del sexto intento fallido contra una misma cuenta desde un mismo equipo
  dentro de 5 minutos, el 100% de los intentos de esa combinación es rechazado durante 1 minuto,
  el 100% de los ingresos correctos de otras cuentas desde ese mismo equipo sigue funcionando, y
  ninguna cuenta cambia de estado por esos intentos.
- **SC-008**: El recorrido completo de ingreso —recorrer los dos campos, enviar el formulario, leer
  un mensaje de error y volver a intentar— se puede realizar en su totalidad usando únicamente el
  teclado, sin tocar el mouse en ningún momento.

## Assumptions

- El usuario administrador inicial (en estado `activo`, con rol *Administrador del sistema*) queda
  creado por este módulo al instalar el sistema, junto con el catálogo fijo de roles y permisos,
  garantizando que siempre haya alguien capaz de gestionar cuentas desde el módulo 2. No se cargan
  cuentas de ejemplo ni datos de prueba adicionales.
- Como consecuencia de lo anterior, los escenarios de la User Story 4 (cuentas `inactiva` y
  `bloqueada`) sólo pueden verificarse operando la aplicación una vez que el módulo 2 permita
  cambiar el estado de una cuenta; hasta entonces se verifican con datos cargados manualmente.
- La sesión expira tras 8 horas continuas de inactividad y se renueva automáticamente mientras el
  usuario sigue operando, de forma que una jornada laboral normal no se ve interrumpida por la
  expiración. Ese plazo sólo corre mientras el navegador sigue abierto: cerrarlo termina la sesión,
  decisión tomada porque en la oficina se comparten equipos.
- Una contraseña temporal generada por un restablecimiento (módulo 2, GU_03) es válida para
  ingresar durante 24 horas desde su generación, sin exigir su cambio inmediato; vencido ese plazo
  hace falta pedir un nuevo restablecimiento. El cambio de contraseña por el propio usuario queda
  fuera de alcance de este módulo, tal como ya estaba definido.
- No se implementa bloqueo automático de cuentas por intentos fallidos repetidos en esta versión;
  esa decisión de seguridad queda deliberadamente en manos del responsable de sistemas (bloqueo
  manual, módulo 2). La única defensa automática es el límite temporal por origen y cuenta
  (FR-021), que se levanta solo, nunca deja a un usuario legítimo fuera del sistema y nunca frena a
  quien no se equivocó, aunque comparta la conexión con quien sí lo hizo.
- El alta de usuarios, la recuperación de contraseña autoiniciada, el cambio de contraseña por el
  usuario ya autenticado, el doble factor de autenticación, el inicio de sesión externo (Google,
  Active Directory, SSO), la política de expiración/historial de contraseñas, el captcha y la
  auditoría de intentos de ingreso más allá de `ultimoAcceso` quedan fuera de alcance de este
  módulo, tal como indica la especificación de origen.
