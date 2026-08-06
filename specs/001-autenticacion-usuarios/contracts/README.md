# Contratos: Autenticación de usuarios (Módulo 1)

Dos contratos, uno por cada frontera del módulo:

- **[auth-api.yaml](./auth-api.yaml)** — contrato HTTP entre el frontend y el backend (OpenAPI
  3.0), más el comportamiento de autorización que aplica a todo endpoint protegido del sistema.
- **Este archivo** — contrato de interfaz: qué pantallas hay, qué se ve en cada una y qué texto
  exacto lee el usuario en cada situación.

---

## Pantallas

### Pantalla de inicio de sesión — `/ingresar`

Es la única pantalla pública del sistema (FR-007).

| Elemento | Comportamiento |
|---|---|
| Campo *Nombre de usuario* | Obligatorio. Se envía tal como se escribió; el servidor normaliza (FR-012) |
| Campo *Contraseña* | Obligatorio, siempre enmascarado. Nunca se muestra, ni con un botón de "ver" (FR-018) |
| Botón *Ingresar* | Se deshabilita mientras la petición está en curso, para evitar envíos repetidos |
| Validación en pantalla | Si alguno de los dos campos está vacío, se marca como obligatorio y **no se llama al servidor** (FR-011) |
| Mensaje de error | Aparece sobre el formulario, sin borrar lo escrito, de modo que se pueda reintentar de inmediato |

Al entrar a cualquier otra ruta sin sesión, el sistema redirige acá y **recuerda la ruta pedida**
(FR-026). Tras autenticarse, el usuario llega a esa ruta si sus roles la autorizan; si no, o si la
ruta guardada no es una pantalla de la propia aplicación, llega a la pantalla de inicio. Nunca se
acepta como destino una dirección externa.

### Pantalla de inicio — `/`

Primera pantalla después de un ingreso exitoso (FR-020).

| Elemento | Contenido |
|---|---|
| Nombre de usuario | El `username` que devuelve la sesión |
| Roles | Los nombres de los roles vigentes, separados por coma |
| Menú | Una entrada por cada opción de `opcionesMenu`. Si la lista viene vacía, el menú se muestra vacío y la pantalla sigue siendo accesible |
| Botón *Cerrar sesión* | Visible desde cualquier pantalla del sistema, no sólo desde esta (FR-013) |

El menú **no anuncia módulos que todavía no existen**: no hay entradas deshabilitadas ni leyendas
de "próximamente" (FR-020).

---

## Textos de la interfaz

Todos en español rioplatense, con voseo (Principio II). Son los que devuelve el backend en el campo
`mensaje`; el frontend los muestra tal cual, sin reescribirlos.

| Situación | Texto |
|---|---|
| Campo vacío al enviar | `Completá el nombre de usuario y la contraseña.` |
| Username inexistente, contraseña incorrecta, o contraseña temporal vencida | `El usuario o la contraseña no son correctos.` |
| Cuenta `inactiva` o `bloqueada` con contraseña correcta | `Tu cuenta no está habilitada. Contactá al responsable de sistemas.` |
| Sexto intento fallido desde el mismo origen | `Hubo demasiados intentos fallidos. Esperá un minuto y volvé a intentar.` |
| Sesión vencida en medio de una operación | `Tu sesión expiró. Ingresá de nuevo.` |
| Operación sin el permiso necesario | `No tenés permiso para acceder a esta funcionalidad.` |
| El servidor no responde | `No pudimos conectarnos con el sistema. Revisá tu conexión y volvé a intentar.` |

Ninguno de estos textos expone detalles técnicos, códigos de error ni nombres de campos internos
(FR-015).

---

## Piso mínimo de accesibilidad

Las cuatro condiciones de FR-025 rigen para las dos pantallas de este módulo y quedan como base
para todos los módulos siguientes:

| Condición | Qué significa en concreto |
|---|---|
| Operable con teclado | Se puede recorrer los campos, enviar el formulario, leer el error y cerrar sesión sin tocar el mouse. El foco queda siempre visible y el orden de recorrido sigue el orden visual |
| Enter encadena los campos | En el nombre de usuario, Enter baja a la contraseña en vez de enviar el formulario; en la contraseña, Enter envía. Sin esto, quien completa el usuario y sigue con Enter se choca con el error de campos incompletos antes de haber podido escribir la contraseña. Tab sigue funcionando igual |
| Etiquetas asociadas | Cada campo tiene una etiqueta visible vinculada a él, no un texto de ayuda dentro del campo que desaparece al escribir |
| Errores anunciados | Cuando aparece un mensaje de error, un lector de pantalla lo lee sin que el usuario tenga que buscarlo |
| Contraste suficiente | El texto se lee sin dificultad sobre su fondo, incluido el de los mensajes de error |

Al abrir la pantalla de ingreso, el foco arranca en el campo de nombre de usuario.

## Reglas transversales del frontend

1. **Todas las peticiones envían las credenciales del navegador**, para que la cookie de sesión
   viaje. El frontend nunca guarda tokens ni datos de sesión en `localStorage` ni en
   `sessionStorage`.
2. **Ante un `401` en cualquier petición**, se descarta el estado de sesión en memoria, se muestra
   `Tu sesión expiró. Ingresá de nuevo.` y se redirige a `/ingresar` (FR-015).
3. **Ocultar una opción del menú nunca es la protección.** El backend rechaza igual la operación;
   el menú sólo evita ofrecer lo que no corresponde (FR-008).
4. **Después de cerrar sesión, el botón "atrás" no debe recuperar ninguna pantalla protegida**: las
   respuestas protegidas viajan con `Cache-Control: no-store` y el estado en memoria se limpia al
   cerrar sesión (FR-013).
