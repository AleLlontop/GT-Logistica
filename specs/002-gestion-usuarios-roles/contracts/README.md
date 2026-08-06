# Contratos: Gestionar usuarios y roles (Módulo 2)

Dos contratos, uno por cada frontera del módulo:

- **[usuarios-api.yaml](./usuarios-api.yaml)** — contrato HTTP entre el frontend y el backend
  (OpenAPI 3.0).
- **Este archivo** — contrato de interfaz: qué pantallas hay, qué se ve en cada una y qué texto
  exacto lee el usuario en cada situación.

Ambos se apoyan en lo que ya fijó el Módulo 1 y no lo redefinen: el cuerpo de error
(`{ codigo, mensaje }`), el comportamiento ante sesión vencida, el menú calculado en el servidor y
el piso de accesibilidad.

---

## Acceso al módulo

Cinco de las seis pantallas exigen sesión activa y el rol *Administrador del sistema* (FR-007). Quien
no lo tenga **no ve las entradas en el menú** —el servidor no se las devuelve— y si llega a la ruta a
mano, recibe el mensaje de falta de permiso.

Se agregan dos entradas al menú, ambas atadas al permiso `usuarios.gestionar`:

| Entrada | Ruta |
|---|---|
| *Gestión de usuarios* | `/usuarios` (ya existía, apuntaba a una pantalla vacía) |
| *Personas* | `/personas` |

**La sexta pantalla es la excepción**: el cambio de contraseña propia (`/mi-cuenta/contrasena`) está
disponible para **cualquier** usuario autenticado, tenga el rol que tenga (FR-029). Por eso su enlace
no sale del menú calculado por permisos, sino que va fijo en el encabezado, junto a *Cerrar sesión*,
visible desde cualquier pantalla del sistema.

---

## Pantallas

### Listado de usuarios — `/usuarios`

| Elemento | Comportamiento |
|---|---|
| Columnas | Username, email, estado, roles asignados, fecha de alta y último acceso (FR-011) |
| Último acceso vacío | Se muestra como `Nunca ingresó`, no como celda en blanco |
| Filtros | *Username* y *email* son campos de texto: traen todo lo que **contenga** lo escrito, sin distinguir mayúsculas. *Rol* y *estado* son listas desplegables de selección exacta. Los cuatro se combinan con "y" |
| Sin resultados | Mensaje explícito en lugar de una tabla vacía (FR-012) |
| Acciones por fila | *Ver*, *Editar*, *Roles* y *Dar de baja* |
| Botón *Nuevo usuario* | Lleva al formulario de alta |

### Detalle de usuario — `/usuarios/{id}`

Se llega desde la acción *Ver* del listado. Es la pantalla que exige FR-013.

| Elemento | Contenido |
|---|---|
| Datos de la cuenta | Username, email, estado, roles asignados, fecha de alta y último acceso |
| Persona asociada | Nombre, apellido, DNI y tipo, si tiene una. Si no, la leyenda `Sin persona asociada` |
| Contraseña | **No aparece de ninguna forma**: ni el valor, ni un campo enmascarado, ni un botón de "ver" (FR-013) |
| Acciones | *Editar*, *Roles*, *Restablecer contraseña* y *Dar de baja* |

### Formulario de usuario — `/usuarios/nuevo` y `/usuarios/{id}/editar`

| Elemento | Alta | Edición |
|---|---|---|
| *Nombre de usuario* | Obligatorio | Obligatorio, precargado |
| *Email* | Obligatorio, con formato válido | Obligatorio, precargado |
| *Contraseña inicial* | Obligatoria, mínimo 8 caracteres, siempre enmascarada | **No aparece** (FR-014) |
| *Estado* | Lista desplegable, precargada en `activo` (FR-005) | Precargado con el estado actual |
| *Persona asociada* | Opcional. Selector con las personas **activas** del padrón, más la opción *Sin persona asociada* | Igual; elegir *Sin persona asociada* la libera |
| *Roles* | Cuatro casillas, al menos una marcada (FR-001) | Se editan desde el panel de roles, no acá |

### Restablecer contraseña — desde el detalle de un usuario

Un botón *Restablecer contraseña* con confirmación previa. **No hay ningún campo de contraseña**: el
responsable de sistemas no la elige ni la ve (FR-009). Al confirmar, el sistema genera una temporal,
se la envía por email al usuario y avisa el resultado del envío.

La contraseña temporal vence a las 24 horas, según la regla que fijó el Módulo 1, y el mensaje de
confirmación lo dice explícitamente para que el responsable pueda avisarlo.

El restablecimiento **corta las sesiones que ese usuario tuviera abiertas** (FR-032): si estaba
trabajando en otra máquina, en su próxima acción vuelve a la pantalla de ingreso con el mensaje de
sesión vencida del Módulo 1. La confirmación se lo advierte al responsable de sistemas.

Reglas comunes a los dos modos:

- La validación de formato se muestra **en el campo**, marcado en rojo y con el motivo puntual, y no
  se llama al servidor hasta que el formulario es válido.
- Los errores de regla de negocio que devuelve el servidor (duplicados, persona ya vinculada, último
  administrador) se muestran sobre el campo que corresponde si el error trae `campo`, y sobre el
  formulario si no.
- **En ningún momento se muestra ni se pide de vuelta la contraseña existente** (FR-013).
- El selector de persona puede venir vacío: el padrón arranca sin nadie cargado. En ese caso muestra
  una leyenda que lleva a la pantalla de personas, en vez de un desplegable vacío sin explicación.

### Panel de roles — `/usuarios/{id}/roles`

| Elemento | Comportamiento |
|---|---|
| Lista de roles | Los cuatro roles del sistema, con los ya asignados marcados (FR-018) |
| Guardar | Los roles quedan exactamente como se dejaron marcados, ni más ni menos |
| Sin ninguno marcado | Se rechaza con mensaje, y no se guarda nada |
| Ver permisos de un rol | Abre los permisos de ese rol **agrupados por módulo**, en modo lectura: sin casillas, sin botones de edición (FR-010) |
| Rol sin permisos todavía | Se muestra la leyenda de que ese rol aún no habilita funcionalidades implementadas, no una lista vacía |

### Cambiar mi contraseña — `/mi-cuenta/contrasena`

La única pantalla del módulo abierta a todos los usuarios autenticados (FR-029).

| Elemento | Comportamiento |
|---|---|
| *Contraseña actual* | Obligatoria, enmascarada, vacía al abrir |
| *Contraseña nueva* | Obligatoria, mínimo 8 caracteres, enmascarada |
| *Repetir contraseña nueva* | Obligatoria. Si no coincide con la anterior, se marca en pantalla y no se llama al servidor |
| Al guardar | La contraseña queda cambiada, **la sesión desde la que se hizo el cambio sigue abierta** y no hay que volver a ingresar. Las otras sesiones que ese usuario tuviera abiertas se cortan (FR-032) |
| Contraseña actual incorrecta | Se informa y no se cambia nada |

Si el usuario había ingresado con una contraseña temporal, al cambiarla la nueva pasa a ser
definitiva y deja de vencer a las 24 horas (FR-031). Es el circuito que cierra el restablecimiento.

### Listado de personas — `/personas`

| Elemento | Comportamiento |
|---|---|
| Columnas | Nombre, apellido, DNI, tipo, teléfono, email, fecha de nacimiento y estado |
| Búsqueda | Un campo de texto que busca por nombre, apellido o DNI, parcial y sin distinguir mayúsculas |
| Padrón vacío | Mensaje explícito de que todavía no hay personas cargadas (FR-025) |
| Acciones por fila | *Editar* y *Dar de baja* |
| Botón *Nueva persona* | Lleva al formulario de alta |

### Formulario de persona — `/personas/nueva` y `/personas/{id}/editar`

Los siete datos de FR-026, todos obligatorios: *Nombre*, *Apellido*, *DNI*, *Tipo* (chofer o
empleado), *Teléfono*, *Email* y *Fecha de nacimiento*. **No se pide ningún otro dato.**

---

## Confirmaciones

Las dos bajas piden confirmación explícita antes de ejecutar cualquier cambio, y cancelar no
modifica nada (FR-017).

| Acción | Texto de la confirmación |
|---|---|
| Dar de baja un usuario | `¿Confirmás la baja de {username}? La cuenta va a quedar inactiva y no va a poder ingresar al sistema.` |
| Dar de baja una persona | `¿Confirmás la baja de {nombre} {apellido}? Va a dejar de estar disponible para asociar a un usuario.` |

---

## Textos de la interfaz

Todos en español rioplatense, con voseo (Principio II). Los devuelve el backend en el campo
`mensaje` y el frontend los muestra tal cual, sin reescribirlos.

### Confirmaciones de operación

| Situación | Texto |
|---|---|
| Usuario creado | `El usuario {username} se creó correctamente.` |
| Usuario modificado | `Los cambios se guardaron correctamente.` |
| Roles guardados | `Los roles de {username} se actualizaron.` |
| Usuario dado de baja | `El usuario {username} quedó inactivo.` |
| Contraseña restablecida y enviada | `Se generó una contraseña temporal y se envió a {email}. Vence en 24 horas. Si tenía una sesión abierta, se cerró.` |
| Persona registrada | `La persona se registró correctamente.` |
| Persona modificada | `Los cambios se guardaron correctamente.` |
| Persona dada de baja | `{nombre} {apellido} quedó dada de baja.` |
| Contraseña propia cambiada | `Tu contraseña se cambió correctamente.` |

### Errores

| Código | Situación | Texto |
|---|---|---|
| `datos_invalidos` | Algún campo con formato inválido | `Revisá los campos marcados en rojo.` |
| `username_duplicado` | El username ya pertenece a otro usuario | `Ese nombre de usuario ya está en uso. Elegí otro.` |
| `email_duplicado` | El email ya pertenece a otro usuario | `Ese email ya está registrado para otro usuario.` |
| `sin_roles` | Ningún rol marcado | `Todo usuario tiene que tener al menos un rol asignado.` |
| `persona_ya_vinculada` | La persona elegida ya pertenece a otro usuario | `Esa persona ya está asociada al usuario {username}. Desvinculala de esa cuenta antes de asignarla acá.` |
| `persona_inexistente` | La persona elegida no existe o está dada de baja | `La persona seleccionada ya no está disponible. Actualizá la lista y volvé a elegir.` |
| `ultimo_administrador` | La operación dejaría al sistema sin administradores activos | `No se puede hacer: tiene que quedar siempre al menos un usuario activo con el rol Administrador del sistema.` |
| `dni_duplicado` | El DNI ya está en el padrón | `Ese DNI ya está registrado en el padrón.` |
| `persona_vinculada` | Se intentó dar de baja una persona asociada | `No se puede dar de baja: está asociada al usuario {username}. Desvinculala primero.` |
| `password_actual_incorrecta` | La contraseña actual escrita no es la correcta | `Tu contraseña actual no es correcta.` |
| `no_encontrado` | El registro ya no existe | `Ese registro ya no existe. Puede que lo hayan eliminado desde otra sesión.` |
| — | El envío del correo falló, pero el restablecimiento quedó hecho | `La contraseña se restableció, pero no pudimos enviar el correo a {email}. Verificá la dirección o volvé a intentar el envío.` |

Los mensajes de sesión vencida, falta de permiso, error inesperado y falta de conexión son los que
ya fijó el Módulo 1 y no cambian.

Ninguno de estos textos expone detalles técnicos, códigos de error ni nombres de campos internos.

### Estados vacíos

| Pantalla | Texto |
|---|---|
| Listado de usuarios sin coincidencias | `No hay usuarios que coincidan con los filtros aplicados.` |
| Padrón sin ninguna persona cargada | `Todavía no hay personas cargadas. Registrá la primera para poder asociarla a un usuario.` |
| Búsqueda de personas sin coincidencias | `No hay personas que coincidan con la búsqueda.` |
| Selector de persona en el formulario de usuario, con el padrón vacío | `No hay personas registradas. Cargá una desde la pantalla Personas.` |
| Permisos de un rol todavía sin funcionalidades | `Este rol todavía no habilita funcionalidades implementadas.` |

---

## Lo que nunca aparece en pantalla

Cuatro cosas, todas exigidas por la spec:

1. **La contraseña de un usuario**, ni en el detalle, ni en la edición, ni enmascarada con opción de
   ver (FR-013, FR-014). En el cambio de contraseña propia los tres campos arrancan vacíos: tampoco
   ahí se precarga ni se recupera nada.
2. **La contraseña temporal generada por un restablecimiento**, en ningún momento y por ningún medio
   (FR-009, SC-004). El responsable de sistemas confirma el envío, no la lee.
3. **Roles o permisos editables**: los permisos se ven en modo lectura y el catálogo de roles no se
   toca desde acá (FR-010).
4. **Opciones de menú de módulos que no existen todavía**: se mantiene la regla del Módulo 1.

---

## Accesibilidad

Rigen las mismas cuatro condiciones que fijó el Módulo 1 y que quedaron como piso de todo el
sistema: operables con teclado, etiquetas asociadas a cada campo, errores anunciados a lectores de
pantalla y contraste suficiente.

Dos puntos propios de este módulo, por el tipo de pantallas que agrega:

- Las tablas de los listados usan encabezados de columna reales, para que un lector de pantalla
  pueda anunciar a qué columna corresponde cada celda.
- Los diálogos de confirmación de baja reciben el foco al abrirse, se cierran con `Escape` —lo que
  equivale a cancelar, sin modificar nada— y devuelven el foco a la fila desde la que se abrieron.
