# Quickstart: Gestionar usuarios y roles (Módulo 2)

Cómo levantar el sistema con este módulo y comprobar, operando la aplicación, que las 7 historias de
usuario y los 9 criterios de éxito se cumplen. No hace falta leer código, mirar logs técnicos ni
correr consultas SQL (Principio IV).

---

## Requisitos previos

- Podman (desarrollo local) o Docker (CI). El mismo `docker-compose.yml` sirve para los dos.
- `.env` completo. Si venís del Módulo 1 ya lo tenés; si no, copiá `.env.template` a `.env` y
  completá `GT_SQL_PASSWORD` y `GT_ADMIN_PASSWORD_INICIAL`.

Este módulo agrega variables **opcionales** al `.env.template`, para el envío de correo:

```bash
# Servidor de correo saliente. Si se dejan vacías, el sistema registra el envío en el log del
# backend en vez de mandarlo, y todo lo demás funciona igual (research §1).
GT_CORREO_HOST=
GT_CORREO_PUERTO=587
GT_CORREO_USUARIO=
GT_CORREO_PASSWORD=
GT_CORREO_REMITENTE=sistema@gtlogistica.com.ar
```

Dejalas vacías para validar el módulo: alcanza y sobra.

---

## Levantar el sistema

```bash
podman compose up -d          # o: docker compose up -d
```

La migración de este módulo se aplica sola al arrancar el backend. Sobre una base que ya venía del
Módulo 1, le pone al usuario `admin` el email `admin@gtlogistica.local`, que vas a poder corregir
desde la pantalla nueva.

Entrá a `http://localhost:5173` e ingresá con `admin` y la contraseña de `GT_ADMIN_PASSWORD_INICIAL`.
En el menú tenés que ver dos entradas nuevas: **Gestión de usuarios** y **Personas**.

---

## Recorrido de validación

Seguilo en orden: cada paso deja el sistema listo para el siguiente.

### 1. El padrón arranca vacío (User Story 6, FR-024)

Entrá a **Personas**. Tiene que decir que todavía no hay personas cargadas — no una tabla vacía sin
explicación.

### 2. Registrar una persona (User Story 6, SC-008)

*Nueva persona* → completá los siete datos (nombre, apellido, DNI, tipo, teléfono, email y fecha de
nacimiento) y guardá. Aparece en el listado.

Probá ahora a cargar **otra persona con el mismo DNI**: tiene que rechazarla diciendo que ese DNI ya
está registrado (FR-027).

### 3. Crear un usuario (User Story 1, SC-001, SC-002)

**Gestión de usuarios** → *Nuevo usuario*. Comprobá antes de guardar:

- El estado viene precargado en `activo` (FR-005).
- El selector de persona ofrece la que cargaste en el paso 2.

Verificá los rechazos, uno por uno:

| Probá esto | Tiene que pasar |
|---|---|
| Un email mal escrito, o una contraseña de menos de 8 caracteres | El campo se marca en rojo con el motivo y no se envía nada |
| Ningún rol marcado | Avisa que todo usuario necesita al menos un rol |
| El username `admin` | Avisa que ese nombre de usuario ya está en uso |
| El email `admin@gtlogistica.local` | Avisa que ese email ya está registrado |

Ahora guardá uno válido —por ejemplo `jperez`, con el rol *Tráfico* y la persona del paso 2—. En el
listado tiene que aparecer con estado `activo`, el rol elegido, la fecha de alta de hoy y el último
acceso vacío.

### 4. La persona queda ocupada (FR-008)

Volvé a *Nuevo usuario* e intentá asignarle **la misma persona** a otro usuario. Tiene que rechazarlo
diciendo a qué usuario ya está asociada.

Andá a **Personas** e intentá dar de baja esa persona: también se rechaza, porque está vinculada
(FR-028).

### 5. Buscar y filtrar (User Story 2, SC-003)

En el listado de usuarios:

- Escribí `pere` en el filtro de username: tiene que aparecer `jperez`. La búsqueda es parcial y no
  distingue mayúsculas (FR-011).
- Combiná rol y estado: el resultado cumple **todas** las condiciones a la vez.
- Poné un filtro que no coincida con nadie: aparece el mensaje de "sin resultados", no una tabla
  vacía (FR-012).
- Abrí el detalle de `jperez`: se ven sus datos completos y la persona asociada, y **en ningún lugar
  aparece la contraseña** (FR-013).

### 6. Modificar y restablecer contraseña (User Story 3, SC-004)

Abrí *Editar* sobre `jperez`. El formulario viene con los datos cargados y **sin ningún campo de
contraseña** (FR-014). Cambiá el email y guardá.

Ahora, **antes de restablecer**, dejá una sesión de `jperez` abierta en otro navegador (o en una
ventana de incógnito): la vas a necesitar en un momento.

Pedí *Restablecer contraseña*. Tiene que confirmarte que se generó una contraseña temporal y se
envió al email del usuario, **sin mostrarla en pantalla en ningún momento** (SC-004). Vence a las 24
horas.

Volvé ahora al navegador donde `jperez` tenía la sesión abierta y hacé cualquier cosa: navegar,
refrescar, abrir una pantalla. Tiene que sacarlo a la pantalla de ingreso (FR-032, SC-010). Una
contraseña que dejó de ser válida no puede seguir sosteniendo una sesión viva.

> Con las variables de correo vacías, el envío queda registrado en el log del backend
> (`podman compose logs backend`) con el destinatario y el asunto — nunca con la contraseña. Es el
> comportamiento esperado en desarrollo.

**Para ver el fallo de envío** (FR-021): poné `GT_CORREO_HOST=servidor.inexistente` en `.env`,
reiniciá el backend y volvé a pedir el restablecimiento. Tiene que avisarte que el correo no se pudo
enviar **pero que la contraseña sí se restableció**.

### 7. Cambiar la contraseña propia (User Story 7, SC-009)

Este paso cierra el circuito del anterior, y hay que hacerlo **con la cuenta del usuario, no con
`admin`**. Abrí otro navegador (o una ventana de incógnito) e ingresá como `jperez`.

Fijate primero en el encabezado: el enlace *Cambiar contraseña* tiene que estar visible **aunque
`jperez` no sea administrador** y no vea ninguna opción de *Gestión de usuarios* en el menú. Esa es
la excepción de FR-029.

Abrilo y comprobá:

| Probá esto | Tiene que pasar |
|---|---|
| Escribir mal la contraseña actual | Avisa que no es correcta y no cambia nada |
| Una contraseña nueva de menos de 8 caracteres | Se marca el campo en rojo, no se envía nada |
| Repetir la nueva distinta a la primera | Se marca en pantalla, no se llama al servidor |
| Todo correcto | Confirma el cambio y **la sesión desde la que lo hiciste sigue abierta**: no te saca a la pantalla de ingreso |

Cerrá sesión y volvé a ingresar con la contraseña nueva. Como `jperez` había entrado con una
temporal, ahora la suya es definitiva y ya no vence a las 24 horas (FR-031).

Para ver la otra mitad de FR-032: dejá dos sesiones de `jperez` abiertas en navegadores distintos,
cambiá la contraseña desde una, y comprobá que **esa** sigue funcionando y la otra queda cortada en
su próxima acción.

> Probá también entrar a `/mi-cuenta/contrasena` como `admin`: la pantalla es la misma y cambia
> **su** contraseña, no la de otro. No hay forma de indicar un usuario distinto.

### 8. La sesión se corta al desactivar (User Story 3, SC-006, FR-016)

Necesitás dos navegadores (o una ventana de incógnito):

1. En el segundo navegador, ingresá como `jperez`.
2. En el primero, como `admin`, editá a `jperez` y pasalo a `inactivo`.
3. Volvé al segundo navegador y hacé cualquier cosa: navegar, refrescar, abrir una pantalla.

Tiene que sacarlo a la pantalla de ingreso. La sesión abierta **no** sobrevive.

Volvelo a `activo` para seguir.

### 9. Ajustar roles y ver permisos (User Story 4)

Abrí *Roles* sobre `jperez`:

- Se ven los cuatro roles del sistema con los suyos marcados.
- Cambiá la selección y guardá: los roles quedan **exactamente** como los dejaste (FR-018).
- Desmarcá todos e intentá guardar: se rechaza (FR-001).
- Abrí los permisos de *Administrador del sistema*: se ven agrupados por módulo y **en modo lectura**
  — sin casillas ni botones de edición (FR-010).

> Los otros tres roles todavía no habilitan nada implementado y muestran la leyenda correspondiente.
> Es lo esperado, no un error.

### 10. Dar de baja (User Story 5, SC-007)

Sobre `jperez`, *Dar de baja*:

- Pide una confirmación explícita antes de tocar nada (FR-017).
- **Cancelá**: nada cambia.
- Confirmá: queda `inactivo`, **sigue apareciendo en el listado** con ese estado, y ya no puede
  ingresar (FR-006).

### 11. Nunca sin administrador (SC-005, FR-019)

Este es el criterio más importante del módulo. Estando logueado como `admin`, que es el único usuario
activo con ese rol, probá los tres caminos:

| Probá esto | Tiene que pasar |
|---|---|
| Editar `admin` y pasarlo a `inactivo` o `bloqueado` | Se rechaza |
| Abrir sus roles y desmarcar *Administrador del sistema* | Se rechaza |
| Darlo de baja desde el listado | Se rechaza |

Los tres avisan que tiene que quedar siempre al menos un usuario activo con ese rol — incluso siendo
tu propia cuenta.

Para confirmar que la protección no es un bloqueo ciego: creá un segundo usuario con el rol
*Administrador del sistema*, y recién ahí volvé a intentar desactivar a `admin`. Ahora tiene que
dejarte.

### 12. Reactivación (caso límite)

Editá a `jperez`, pasalo a `activo` y guardá. Tiene que poder ingresar de nuevo, y seguir teniendo al
menos un rol asignado.

---

## Tests automatizados

```bash
cd backend && dotnet test    # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test      # Vitest + React Testing Library
```

Los de integración levantan la aplicación contra el SQL Server del compose, así que necesitan el
stack arriba.

Hay dos escenarios que los tests cubren mejor que el recorrido manual, y conviene mirar que estén en
verde:

- **Unicidad bajo concurrencia** (caso límite de la spec): dos altas simultáneas del mismo username;
  la segunda tiene que recibir el error de duplicado, no una excepción técnica.
- **Protección del último administrador** en sus tres variantes, que a mano es tedioso de recorrer
  entero.

---

## Problemas frecuentes

| Síntoma | Causa y solución |
|---|---|
| El backend no arranca y habla de `GT_ADMIN_PASSWORD_INICIAL` | Falta la variable y el usuario `admin` todavía no existe. Completala en `.env` |
| El menú no muestra *Gestión de usuarios* ni *Personas* | Esa cuenta no tiene el rol *Administrador del sistema*. Es el comportamiento correcto (FR-007) |
| El restablecimiento dice que se envió, pero no llega ningún mail | Esperable con las variables de correo vacías: se registra en el log en vez de enviarse |
| El selector de persona aparece vacío en el formulario de usuario | El padrón está vacío o todas las personas están dadas de baja. Cargá una desde **Personas** |
