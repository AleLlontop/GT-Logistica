# Research: Gestionar usuarios y roles (Módulo 2)

Fase 0 del plan. Cada decisión resuelve una incógnita técnica del `Technical Context` y se evalúa
contra el Principio I de la constitución (ante dos soluciones, la más simple).

Este módulo arranca sobre un repositorio que ya tiene el Módulo 1 funcionando, así que la primera
pregunta de cada punto es siempre la misma: **¿esto ya está resuelto?** En cuatro casos la respuesta
fue que sí, y quedan documentados en §7 para que nadie los vuelva a implementar.

---

## 1. Envío de correo: interfaz con dos implementaciones, SMTP con MailKit

**Decisión**: definir `IEnviadorCorreo` en `GT.Application/Usuarios/` y dos implementaciones en
`GT.Infrastructure/Correo/`:

- `EnviadorCorreoSmtp` — MailKit, se registra cuando hay un `Correo:Host` configurado.
- `EnviadorCorreoRegistrado` — escribe el destinatario y el asunto al log (**nunca la contraseña
  temporal**) y se registra cuando no hay SMTP configurado.

La selección se hace una sola vez en `Program.cs` mirando la configuración.

**Rationale**: la spec exige enviar la contraseña temporal por email (FR-009) y el repositorio no
tiene hoy ningún mecanismo de correo saliente, pese a que las *Assumptions* de la spec daban por
sentado que existía "el mismo mecanismo que el resto del sistema". Hay que construirlo.

Las dos implementaciones no son un lujo: sin la segunda, `podman compose up` no alcanzaría para
validar el módulo —haría falta un servidor SMTP real o un contenedor extra—, y el Principio IV pide
que los criterios se comprueben operando la aplicación. Con el enviador que registra al log, el
`quickstart` se recorre completo y el escenario de fallo de envío (FR-021) se fuerza configurando un
host inválido.

MailKit sobre `System.Net.Mail.SmtpClient` porque esta última está marcada como obsoleta para
desarrollo nuevo por la propia documentación de .NET, y MailKit es la biblioteca que Microsoft
recomienda en su lugar. Es una única dependencia, sin arrastre.

**Alternativas consideradas**:

- **Un servicio externo (SendGrid, Resend, SES)**: agrega una cuenta, una clave de API y una
  dependencia de red en un sistema que corre en la oficina de una única empresa, para mandar unos
  pocos correos por mes. Descartado por Principio I.
- **Un contenedor de MailHog/Mailpit en el compose**: resuelve la validación local, pero suma un
  cuarto servicio al `docker-compose.yml` para algo que un log resuelve igual de bien.
  Descartado por Principio I.
- **Cola de reintentos para los envíos fallidos**: la spec ya define el comportamiento ante un fallo
  —informar que el envío no se completó, sin revertir el restablecimiento (FR-021)—, así que una cola
  sería alcance no pedido. Descartado por Principio III.

**Costo aceptado**: en desarrollo la contraseña temporal no llega a ningún buzón. Es exactamente lo
que se quiere: quien valida comprueba que el sistema confirma el envío y que la contraseña no aparece
en pantalla (SC-004), que es lo que la spec pide verificar.

---

## 2. Generación de la contraseña temporal

**Decisión**: `GeneradorPasswordTemporal` en `GT.Infrastructure/Seguridad/`, con
`RandomNumberGenerator` sobre un alfabeto sin caracteres ambiguos (sin `l`, `1`, `O`, `0`), de 12
caracteres. Se hashea con el `IHasheadorPassword` que ya existe, se guarda el hash, y se escribe
`PasswordTemporalGeneradaEn = ahora`. El texto plano vive sólo el tiempo de armar el correo: no se
devuelve en la respuesta, no se registra en ningún log y no llega al frontend.

**Rationale**: 12 caracteres superan con holgura el mínimo de 8 de FR-004 y compensan que la
contraseña viaje por correo en texto plano. `RandomNumberGenerator` en vez de `Random` porque es una
credencial: `Random` es predecible a partir de sus salidas. El alfabeto sin ambigüedades es porque
alguien va a tener que tipear esto leyéndolo de un mail.

La marca `PasswordTemporalGeneradaEn` no es un campo nuevo: el Módulo 1 ya la creó y ya la
interpreta (§7).

**Alternativas consideradas**:

- **Un enlace de un solo uso en vez de una contraseña**: es la práctica moderna y sería mejor, pero
  exige una tabla de tokens, un endpoint público y una pantalla de "elegí tu contraseña nueva", todo
  fuera de alcance —la spec pone la recuperación iniciada por el propio usuario explícitamente
  afuera—. Descartado por Principio III.
- **Dejar que el responsable de sistemas elija la contraseña temporal**: contradice FR-009, que
  exige que no la vea en ningún momento.

---

## 3. Unicidad bajo concurrencia: índice único + captura del choque

**Decisión**: para `username`, `email` y `dni`, validar antes de guardar **y** dejar que la base
tenga la última palabra con un índice único. Cuando `SaveChangesAsync` levante una `DbUpdateException`
por violación de índice único, mapearla al mismo error de duplicado que devuelve la validación
previa, identificando cuál de los campos chocó por el nombre del índice.

**Rationale**: es exactamente lo que pide el caso límite de la spec —dos altas simultáneas del mismo
username, quien llega segundo recibe el error de duplicado— y lo que FR-002 exige por escrito
("garantizado con una restricción de unicidad en la base de datos"). La validación previa sola tiene
una ventana entre el `SELECT` y el `INSERT`; el índice solo daría un error técnico feo.

El Módulo 1 ya creó el índice único de `UsernameNormalizado` anticipando esto
(`UsuarioConfiguracion.cs`), así que para username no hay trabajo de esquema.

**Alternativas consideradas**:

- **Transacción serializable**: resuelve lo mismo tomando bloqueos de rango en cada alta, a cambio
  de contención en una tabla que se lee todo el tiempo. Descartado por Principio I.
- **Sólo validación previa**: incumple FR-002 de forma literal.

---

## 4. Búsqueda parcial: filtrar por las columnas normalizadas

**Decisión**: el filtro de username busca `Contains` sobre `UsernameNormalizado` con el término
pasado a mayúsculas; el de email, sobre `EmailNormalizado` con el término en minúsculas. Los filtros
de rol y estado son igualdad exacta.

**Rationale**: la clarificación fijó coincidencia parcial en cualquier posición y sin distinguir
mayúsculas (FR-011). SQL Server suele venir con una *collation* insensible a mayúsculas, así que un
`LIKE` sobre la columna original **parecería** funcionar —pero estaría dependiendo de una
configuración del servidor que nadie declaró y que un despliegue distinto puede cambiar sin aviso.
Filtrar sobre las columnas normalizadas, que ya existen y ya tienen su índice, hace explícito el
comportamiento y lo vuelve independiente de la *collation*.

**Alternativas consideradas**:

- **`LIKE` sobre `Username` confiando en la *collation***: más corto de escribir, pero deja el
  cumplimiento de un requisito atado a una configuración implícita del servidor.
- **Búsqueda de texto completo de SQL Server**: pensada para documentos, no para decenas de filas.
  Descartado por Principio I.

---

## 5. La migración y el usuario `admin` que ya existe

**Decisión**: una única migración que agrega a `Usuarios` las columnas `Email`, `EmailNormalizado`
(índice único), `FechaAlta` y `PersonaId` (índice único **filtrado**), y crea la tabla `Personas`.
Para la fila que ya existe, la migración rellena `Email = 'admin@gtlogistica.local'`,
`EmailNormalizado` en minúsculas y `FechaAlta = SYSUTCDATETIME()`, y recién después vuelve las
columnas obligatorias.

**Rationale**: FR-003 exige email obligatorio y único, pero el Módulo 1 sembró al administrador sin
ninguno (lo dejó explícitamente afuera por Principio III). Alguien tiene que ponerle un valor. Como
FR-019 del Módulo 1 garantiza que **esa es la única cuenta preexistente**, el relleno es una fila
sola y predecible, y el responsable de sistemas puede corregirlo desde la pantalla nueva apenas
entra. El dominio `.local` es deliberado: no es una dirección real y no se le puede mandar correo
por accidente.

El índice único de `PersonaId` tiene que ser **filtrado** (`WHERE PersonaId IS NOT NULL`): en SQL
Server un índice único común considera duplicados a varios `NULL`, con lo cual sólo un usuario del
sistema podría quedarse sin persona asociada — y la spec declara ese caso como válido y habitual.

**Alternativas consideradas**:

- **Pedir el email del administrador por variable de entorno**: obliga a tocar `.env`,
  `docker-compose.yml` y el sembrador para un dato que se edita en diez segundos desde la pantalla
  que este mismo módulo construye. Descartado por Principio I.
- **`Email` nullable**: evitaría el relleno, pero incumple FR-003 y arrastraría comprobaciones de
  `null` a todo el módulo.

---

## 6. Estado de la persona: un `bool`, no un enum

**Decisión**: `Persona.Activa` como `bool`, con la baja lógica poniéndolo en `false`.

**Rationale**: la spec le da a la persona exactamente dos estados —registrada o dada de baja—
mientras que al usuario le da tres (`activo`, `inactivo`, `bloqueado`), que sí justifican el
`EstadoUsuario`. Un enum de dos valores para imitar la simetría sería complejidad anticipada para
una necesidad futura hipotética, que es justo lo que el Principio I prohíbe.

**Alternativas consideradas**:

- **`EstadoPersona` como enum**: se descartó por lo anterior. Si alguna vez aparece un tercer estado
  real, migrar un `bool` a un enum es una migración trivial.
- **`FechaBaja` nullable en vez de `bool`**: guardaría además *cuándo* se dio de baja, pero la spec
  no pide ese dato en ninguna pantalla ni criterio. Descartado por Principio III.

---

## 7. Lo que ya está resuelto y no hay que volver a construir

Cuatro requisitos de esta spec los cumple código que el Módulo 1 ya dejó escrito. Conviene tenerlos
identificados para no duplicarlos:

| Requisito de este módulo | Lo resuelve | Trabajo pendiente |
|---|---|---|
| FR-016 y SC-006 — la sesión se corta cuando la cuenta deja de estar `activa`, incluso con sesión abierta | `RevalidadorSesion` (`GT.Api/Autenticacion/`), que recarga al usuario en cada petición | **Ninguno**. Alcanza con guardar el estado nuevo; el corte sale solo. Sí hay que cubrirlo con un test de integración |
| FR-009 — la contraseña temporal vale 24 horas | `VigenciaPasswordTemporal` (`GT.Domain/Autenticacion/`) y el campo `PasswordTemporalGeneradaEn` | Sólo **escribir** la marca al restablecer; leerla e interpretarla ya está hecho |
| FR-007 — el módulo es sólo para el *Administrador del sistema* | El permiso `usuarios.gestionar`, ya sembrado y ya asignado a ese rol, más las políticas de `PermisoHandler` | Agregar `.RequireAuthorization(...)` en los endpoints nuevos. **No se crea un permiso nuevo para personas**: el padrón es parte de este módulo y comparte su restricción de acceso |
| FR-004 — la contraseña se almacena hasheada | `IHasheadorPassword` (`GT.Infrastructure/Seguridad/`) | Sólo invocarlo |

Además, el `CatalogoOpcionesMenu` ya traduce permisos a opciones de menú del lado del servidor: la
entrada nueva del padrón de personas se agrega ahí, apuntando al mismo permiso, y el frontend la
dibuja sin lógica propia.

---

## 8. Protección del último administrador: una regla pura, verificada en el servidor

**Decisión**: `ProteccionUltimoAdministrador` en `GT.Domain/Usuarios/`, una función pura que recibe
la cantidad de usuarios `activos` con el rol *Administrador del sistema* **excluyendo al afectado** y
la operación que se intenta, y responde si se permite. Los tres casos de FR-019 —cambio de estado,
quita del rol y baja— la consultan antes de guardar, y el conteo se hace en la misma operación, no
antes.

**Rationale**: la regla es idéntica en los tres casos y no depende de la base, así que aislarla la
hace verificable con tests unitarios rápidos en vez de tres tests de integración lentos. Que el
conteo excluya al usuario afectado es el detalle que hace que funcione cuando el afectado **es** el
único administrador, que es justamente el caso que la spec quiere frenar —incluida la variante de que
sea la propia cuenta de quien opera.

**Alternativas consideradas**:

- **Un `CHECK` o un *trigger* en la base**: expresar "al menos una fila que cumpla X" en SQL Server
  exige un *trigger*, que esconde una regla de negocio central fuera del código y complica los tests.
  Descartado por Principio I y IV.
- **Verificarlo sólo en el frontend**: sería incumplir FR-019 ante cualquier petición que no venga
  del formulario.

---

## 9. Cambio de contraseña propia: la excepción de autorización del módulo

**Decisión**: `POST /api/mi-cuenta/contrasena` se protege con `RequireAuthorization()` **sin** política
de permiso, y siempre opera sobre el usuario de la sesión: el identificador sale de los *claims*, no
del cuerpo ni de la URL. El enlace a la pantalla vive fijo en el encabezado, junto a *Cerrar sesión*,
y no pasa por el `CatalogoOpcionesMenu`. La contraseña actual se verifica con el
`IVerificadorPassword` que ya existe.

**Rationale**: es la única pantalla del módulo que no puede exigir el rol *Administrador del sistema*
(FR-029): quien recibe una contraseña temporal puede tener cualquier rol, y si no puede cambiarla,
el vencimiento de 24 horas lo deja afuera del sistema. Por eso FR-007 quedó con una excepción
explícita.

Que el usuario objetivo salga de los *claims* y no de un parámetro es lo que impide que este endpoint
—el único sin política de permiso— se convierta en una forma de cambiarle la contraseña a otro. No es
una validación que se pueda olvidar: sencillamente no hay dónde indicar un usuario distinto.

El enlace no puede salir del `CatalogoOpcionesMenu` porque ese catálogo traduce **permisos** a
opciones, y acá no hay permiso que consultar. Ponerlo en el encabezado es además donde la gente lo
busca.

**Alternativas consideradas**:

- **Crear un permiso `cuenta.cambiar_password` y dárselo a los cuatro roles**: encajaría en el
  mecanismo existente, pero un permiso que tienen todos los roles no discrimina nada: es
  ceremonia sin efecto, y habría que acordarse de otorgárselo a cada rol futuro. Descartado por
  Principio I.
- **Recibir el `id` del usuario en la ruta y verificar que coincide con el de la sesión**: funciona,
  pero deja la puerta abierta a que un descuido en esa comparación exponga las contraseñas de todos.
  Descartado por innecesario.
- **Forzar el cambio en el primer ingreso con contraseña temporal**: sería mejor práctica, pero exige
  tocar el Módulo 1 (redirección obligatoria post-login) y la spec no lo pide. Queda como candidato
  para una spec futura.

---

## 10. Cortar las sesiones abiertas cuando cambia la contraseña

**Decisión**: agregar `PasswordActualizadaEn` a `Usuarios`, escribirla en las tres operaciones que
tocan una contraseña (alta, restablecimiento y cambio propio), **incluirla como *claim* en la cookie**
(`gt:password_v`) y **modificar el `RevalidadorSesion` del Módulo 1** para que rechace toda sesión
cuyo *claim* no coincida exactamente con el valor de la base. Después de un cambio propio, el
endpoint vuelve a emitir la cookie con `SignInAsync`, con lo cual la sesión desde la que se hizo el
cambio sobrevive y las demás no.

> **Corregido durante la implementación.** La primera versión de esta decisión no usaba un *claim*:
> comparaba el `IssuedUtc` de la cookie contra la marca de la base. **No funciona**: `IssuedUtc`
> viaja como texto RFC1123, que no tiene fracciones de segundo, así que todo lo que ocurre dentro de
> un mismo segundo queda indistinguible. El test de FR-032 lo dejó en evidencia de inmediato —en un
> test todo pasa en milisegundos— y ningún ajuste de truncado o tolerancia lo arregla sin romper el
> borde opuesto: o sobrevive una sesión que debía morir, o se expulsa al usuario de la sesión desde
> la que acaba de cambiar su propia contraseña. La comparación por igualdad no tiene bordes.

**Rationale**: FR-032 pide que un restablecimiento corte las sesiones abiertas, y hoy nada lo hace:
el `RevalidadorSesion` sólo mira el estado de la cuenta, así que una sesión abierta sigue viva con una
contraseña que ya no sirve. Es el mismo agujero que el Módulo 1 cerró para las cuentas desactivadas,
sin cerrar para las contraseñas.

Una sola columna, un *claim* y una condición más en el revalidador que ya se ejecuta en cada
petición. Sin tabla de sesiones y sin tocar el mecanismo de la cookie.

Que el cambio propio re-emita la cookie es lo que hace compatibles FR-032 y el escenario 2 de la
User Story 7 —que exige que la sesión siga abierta— sin agregar una excepción a la regla: la regla
sigue siendo "toda sesión anterior al cambio muere", y la sesión que hizo el cambio deja de ser
anterior.

**Alternativas consideradas**:

- **Comparar el `IssuedUtc` de la cookie contra la marca de la base**: era la decisión original,
  descartada al implementarla por el problema de precisión descrito arriba. La solución final es un
  *security stamp* simplificado —el mismo patrón que usa ASP.NET Core Identity, pero con la marca de
  tiempo que ya hacía falta en vez de un GUID aparte—.
- **Una tabla de sesiones activas para poder revocarlas de a una**: es lo que el Módulo 1 ya había
  descartado al elegir cookie sobre JWT. Volver a introducirla acá desharía esa decisión por un
  requisito que se resuelve con una columna.
- **Aceptar que la sesión sobreviva hasta vencer por inactividad**: era el estado implícito antes de
  esta decisión. Descartado por FR-032.

**Costo aceptado**: este módulo modifica un archivo del Módulo 1 (`RevalidadorSesion`). Es una
condición agregada a una revalidación que ya existía, no un rediseño, pero conviene que los tests de
integración de autenticación del Módulo 1 se corran después del cambio.
