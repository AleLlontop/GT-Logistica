# Research: Autenticación de usuarios (Módulo 1)

Fase 0 del plan. Cada decisión resuelve una incógnita técnica del `Technical Context` y se evalúa
contra el Principio I de la constitución (ante dos soluciones, la más simple).

---

## 1. Mecanismo de sesión: cookie de sesión, no JWT

**Decisión**: autenticación por cookie de ASP.NET Core (`AddAuthentication().AddCookie()`), con la
cookie marcada como **no persistente** (`IsPersistent = false`), `ExpireTimeSpan = 8 horas` y
`SlidingExpiration = true`. En cada petición, el evento `OnValidatePrincipal` recarga al usuario
desde la base y rechaza la sesión si dejó de estar `activo`.

**Rationale**: cuatro requisitos de la spec son incompatibles con un token autocontenido salvo que
se le agregue infraestructura encima:

| Requisito | Por qué la cookie lo resuelve de una |
|---|---|
| FR-006 — permisos efectivos calculados en cada operación | La revalidación lee roles frescos de la base; un JWT los lleva congelados adentro |
| FR-009 — cortar la sesión cuando la cuenta deja de estar activa | La revalidación lo detecta en la petición siguiente; con JWT haría falta una lista de revocación |
| FR-013 — cerrar sesión invalida de inmediato | `SignOutAsync` borra la cookie; un JWT sigue siendo válido hasta que vence |
| FR-022 — la sesión termina al cerrar el navegador | Una cookie sin `Expires` muere con el navegador; guardar un JWT exige elegir entre `localStorage` (sobrevive al cierre) y `sessionStorage` (se pierde entre pestañas) |

Además el navegador maneja la cookie solo: el frontend no guarda ni renueva credenciales, no hay
*refresh token*, y con `HttpOnly` la sesión queda fuera del alcance de cualquier script.

**Alternativas consideradas**:

- **JWT de acceso + *refresh token***: el patrón más difundido, pero para cumplir FR-006 y FR-009
  habría que consultar la base igual en cada petición, con lo cual se pierde la única ventaja real
  del token (no tocar la base) y quedan la lista de revocación, la rotación y el almacenamiento en
  el cliente como complejidad neta. Descartado por Principio I.
- **ASP.NET Core Identity completo**: trae registro, confirmación por email, 2FA, *lockout* y
  bloqueo automático, todo explícitamente fuera de alcance (FR-016 y la sección Assumptions).
  Descartado por Principio III.

**Costo aceptado**: una consulta de usuario y roles por petición. A la escala del sistema (decenas
de usuarios) es irrelevante, y se mitiga con un caché de memoria de vida corta si alguna vez
molesta.

---

## 2. Hasheo de contraseñas: `PasswordHasher<T>` del framework

**Decisión**: usar `Microsoft.AspNetCore.Identity.PasswordHasher<Usuario>` como única pieza tomada
de Identity, sin adoptar el resto del paquete. Verificación con `VerifyHashedPassword`.

**Rationale**: viene en el framework (sin dependencia nueva), usa PBKDF2 con parámetros que
Microsoft mantiene al día, y guarda el algoritmo y el salt dentro del propio hash, así que cambiar
de parámetros más adelante no rompe las contraseñas existentes. Cumple FR-002 sin escribir una
línea de criptografía propia.

**Alternativas consideradas**:

- **BCrypt.Net-Next**: excelente y muy usado, pero agrega una dependencia externa para obtener lo
  mismo que ya viene incluido. Descartado por Principio I.
- **Hasheo propio con `Rfc2898DeriveBytes`**: implica decidir a mano el salt, las iteraciones y el
  formato de almacenamiento. Es exactamente el tipo de código que no conviene escribir uno mismo.
  Descartado.

**Nota para el Módulo 2**: el Módulo 2 crea usuarios y genera contraseñas temporales, así que tiene
que usar este mismo hasher. Queda como contrato compartido en `GT.Infrastructure/Seguridad`.

---

## 3. Verificación en tiempo constante para el mensaje genérico

**Decisión**: cuando el username no existe, igual se ejecuta una verificación de hash contra un
hash ficticio precalculado antes de responder.

**Rationale**: FR-003 exige que un username inexistente y una contraseña incorrecta den el mismo
mensaje. Si no se hace nada, el caso "usuario inexistente" responde en milisegundos y el caso
"contraseña incorrecta" tarda ~100 ms por el hasheo: esa diferencia de tiempo delata qué usernames
existen y anula el propósito del mensaje genérico. Son tres líneas de código.

**Alternativas consideradas**: no hacer nada y aceptar la filtración por tiempo. Descartado porque
contradice el sentido explícito de FR-003 y de la User Story 3.

---

## 4. Límite de intentos por origen y cuenta: contador en memoria

**Decisión**: contador de fallos en `IMemoryCache` con clave compuesta **(IP de origen + username
normalizado)** y ventana de 5 minutos. Al sexto intento de esa combinación se rechaza durante 1
minuto (FR-021). Un inicio de sesión exitoso borra el contador de esa combinación. Se implementa
como middleware aplicado sólo al endpoint de login.

**Rationale**: es la implementación más chica que cumple el requisito. Dos detalles del diseño no
son negociables:

- **La clave incluye la cuenta, no sólo la IP.** G&T Logística sale a internet por una única
  conexión: contando sólo por origen, cinco errores de tipeo de personas distintas dejarían fuera a
  toda la oficina durante un minuto. Con la clave compuesta, cada quien arrastra únicamente sus
  propios fallos.
- **El contador se incrementa sólo con intentos fallidos.** El `RateLimiter` incorporado en ASP.NET
  Core cuenta *todas* las peticiones, con lo cual penalizaría a quien ingresa bien varias veces
  seguidas — no es lo que pide FR-021.

**Alternativas consideradas**:

- **Contar sólo por origen**: era la redacción original de FR-021. Descartada por el daño colateral
  descrito arriba, detectado al revisar el checklist de seguridad (CHK010).
- **Contar sólo por cuenta**: cierra el hueco de las IPs rotativas, pero permite dejar afuera a
  alguien a propósito fallando adrede sobre su username. Reintroduce por la ventana el bloqueo
  automático que FR-016 descartó. Descartada.
- **Agregar un techo alto por origen** (por ejemplo 50 fallos en 5 minutos) como red de contención
  contra el barrido de muchas cuentas: es la opción más completa, pero suma un segundo umbral para
  un ataque que hoy no es una amenaza realista a esta escala. Descartada por Principio I; se puede
  agregar encima de lo actual sin romper nada si alguna vez hace falta.
- **`Microsoft.AspNetCore.RateLimiting` incorporado**: no distingue intento exitoso de fallido.
  Descartado por no cumplir el requisito.
- **Contador en Redis o en la base**: necesario si mañana hay varias instancias del backend, pero
  hoy hay una sola. Descartado por Principio I (nada de generalizar antes del segundo caso real).

**Limitación aceptada y documentada**: al vivir en memoria, el contador se reinicia si se reinicia
el backend y no se comparte entre instancias. Con una sola instancia y bloqueos de 1 minuto, el
impacto es nulo.

**Riesgo residual**: un atacante desde una IP puede probar 5 contraseñas por cada cuenta que
conozca. Con la cantidad de usuarios de G&T son unas decenas de intentos cada 5 minutos, muy lejos
del ataque automatizado que FR-021 busca frenar, y requiere conocer los usernames de antemano.

---

## 5. Contraseña temporal: un solo campo con marca de tiempo

**Decisión**: agregar a `Usuarios` una única columna nullable `PasswordTemporalGeneradaEn`. Si es
`NULL`, la contraseña es definitiva. Si tiene valor, la contraseña es temporal y sólo sirve dentro
de las 24 horas siguientes a esa marca (FR-017).

**Rationale**: un solo campo expresa las dos cosas que hacen falta (si es temporal y desde cuándo).
Un booleano `EsTemporal` acompañando a la fecha sería un estado redundante que puede quedar
inconsistente.

**Alternativas consideradas**: tabla aparte de restablecimientos con su propio ciclo de vida.
Serviría para auditar, pero la auditoría está fuera de alcance. Descartado por Principio III.

**Nota para el Módulo 2**: el Módulo 2 escribe esta columna al restablecer una contraseña; el
Módulo 1 sólo la lee.

---

## 6. Contraseña del administrador inicial: variable de entorno obligatoria

**Decisión**: la siembra lee `GT_ADMIN_PASSWORD_INICIAL`. La variable es obligatoria **sólo cuando
la siembra tiene que correr**, es decir cuando el usuario `admin` todavía no existe: en ese caso, si
falta o está vacía, la aplicación **no arranca** y muestra un mensaje explícito indicando qué falta.
Una vez sembrado el administrador, la variable puede borrarse y el sistema sigue funcionando. El
`docker-compose.yml` la toma del entorno o de un archivo `.env` que está en `.gitignore`.

**Rationale**: el Principio V prohíbe secretos versionados. Fallar al arrancar es mejor que las dos
alternativas habituales: una contraseña por defecto tipo `admin/admin` que nadie cambia, o una
contraseña generada al azar que hay que escribir en el log — justo lo que FR-018 prohíbe.

Atarla a la condición de siembra acota la ventana en la que el secreto tiene que existir. Exigirla
en cada arranque no aportaría ninguna seguridad extra —el administrador ya está creado y su
contraseña vive hasheada en la base— y a cambio dejaría la contraseña en texto plano en el `.env` de
cada máquina durante toda la vida del proyecto.

**Alternativas consideradas**:

- **Exigirla en todo arranque**: más simple de explicar, pero obliga a conservar el secreto para
  siempre sin ninguna ganancia. Descartado.
- **Contraseña por defecto conocida**: queda en producción para siempre. Descartado.
- **Generarla y mostrarla en el log del arranque**: viola FR-018 ("no debe quedar registrada en
  ningún log del sistema"). Descartado.

**Consecuencia operativa**: si alguna vez se recrea la base desde cero, la variable vuelve a hacer
falta. El mensaje de arranque lo dice con todas las letras, así que el caso se diagnostica solo.

---

## 7. Autorización: políticas por permiso, no por rol

**Decisión**: los endpoints protegidos exigen un **permiso** (por ejemplo `usuarios.gestionar`), no
un rol. Los permisos efectivos se calculan como la unión de los permisos de los roles vigentes del
usuario y se cargan en los *claims* durante la revalidación de cada petición.

**Rationale**: es lo que dice FR-006 al pie de la letra ("la unión de los permisos de todos sus
roles vigentes"). Además evita tener que tocar cada endpoint cuando el Módulo 2 cambie qué rol
otorga qué permiso.

**Alternativas consideradas**: `[Authorize(Roles = "Administrador del sistema")]`. Es más corto,
pero clava el rol en el código y contradice el modelo de permisos que la spec describe. Descartado.

---

## 8. El menú lo decide el servidor

**Decisión**: la respuesta de sesión incluye la lista de opciones de menú que el usuario tiene
autorizadas. El frontend dibuja lo que recibe, sin lógica propia de permisos.

**Rationale**: mantiene una única fuente de verdad y hace evidente lo que exige FR-008: el menú es
sólo presentación, y ocultar una opción nunca es la protección. El servidor rechaza igual la
operación aunque el cliente muestre algo que no debería.

**Alternativas consideradas**: que el frontend arme el menú a partir de la lista de permisos.
Duplicaría en TypeScript el mapeo permiso → pantalla que ya existe en el backend. Descartado.

---

## 9. Versiones y entorno

**Decisión**: .NET 10 (LTS), EF Core 10, React 19 con Vite y TypeScript, Node 22 LTS, SQL Server
2022. Un único `docker-compose.yml` levanta SQL Server, backend y frontend.

**Rationale**: son las versiones LTS vigentes al momento de arrancar el proyecto, lo que da el
horizonte de soporte más largo sin sorpresas. El compose usa sintaxis estándar, sin extensiones
propias de Docker ni de Podman, que es lo que permite que el mismo archivo funcione en ambos
entornos tal como pide `CLAUDE.md`.

**Verificación pendiente al implementar**: confirmar que el SDK instalado coincide con la versión
elegida antes de crear la solución. Si el equipo tiene otra LTS, se ajusta sin impacto en el
diseño.

---

## 10. Tests de integración contra el SQL Server del compose

**Decisión**: `GT.IntegrationTests` usa `WebApplicationFactory` apuntando al SQL Server que ya
levanta el compose, con una base de datos separada por corrida. Sin Testcontainers.

**Rationale**: es exactamente el flujo que `CLAUDE.md` documenta (`podman compose up -d` y después
`dotnet test`), y ese mismo compose corre en CI sobre Docker nativo. No hace falta una segunda
forma de levantar la base.

**Alternativas consideradas**:

- **Testcontainers**: aísla mejor, pero agrega una dependencia y una segunda manera de arrancar la
  base para resolver un problema que hoy no existe. Descartado por Principio I.
- **Proveedor en memoria de EF Core**: no valida índices únicos ni el comportamiento real de SQL
  Server, que es justamente lo que hay que probar. Descartado.
