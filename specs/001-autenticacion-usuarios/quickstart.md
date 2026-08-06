# Quickstart: Autenticación de usuarios (Módulo 1)

Cómo levantar el sistema y comprobar, operando la aplicación, que el módulo cumple lo que dice la
spec. Salvo donde se aclara lo contrario, todo se valida haciendo clic (Principio IV).

---

## Requisitos previos

- Podman con `podman compose` (desarrollo local) o Docker (CI). El mismo `docker-compose.yml` sirve
  en ambos.
- .NET SDK 10 y Node 22 LTS, si querés correr los tests fuera de los contenedores.

## Levantar el sistema

La contraseña del administrador inicial **no está en el repositorio** (Principio V). La primera vez
hay que definirla:

```bash
cp .env.ejemplo .env          # y editá GT_ADMIN_PASSWORD_INICIAL
podman compose up -d          # SQL Server + backend + frontend
```

Al arrancar, el backend aplica las migraciones y siembra el catálogo de roles y permisos junto con
el usuario `admin` (FR-019). La siembra es idempotente: reiniciar no pisa la contraseña.

Cuando termina, la aplicación queda en `http://localhost:5173`.

De ahí en más alcanza con `podman compose up -d`. La variable sólo hace falta mientras el usuario
`admin` no exista: una vez sembrado, podés borrarla del `.env` y el sistema sigue funcionando. Si
falta justo cuando la siembra tenía que correr, el backend se detiene con un mensaje explícito
diciendo qué falta — es el comportamiento esperado, no una falla.

Fuera de contenedores, el mismo valor se define con
`dotnet user-secrets set "GT_ADMIN_PASSWORD_INICIAL" "..."`, que lo guarda en el perfil del usuario
y no en el repositorio.

## Correr los tests

```bash
cd backend && dotnet test     # GT.UnitTests + GT.IntegrationTests
cd frontend && npm test       # tests de frontend
```

Los tests de integración usan el SQL Server que levanta el compose, así que tiene que estar
corriendo.

---

## Validación por historia de usuario

### User Story 1 — Iniciar sesión con credenciales válidas (P1)

1. Abrí `http://localhost:5173`. Te lleva a la pantalla de ingreso.
2. Ingresá con `admin` y la contraseña que pusiste en `.env`.
3. **Esperado**: llegás a la pantalla de inicio, que muestra el usuario `admin`, el rol
   *Administrador del sistema* y el botón de cerrar sesión. El menú tiene una sola opción:
   *Gestión de usuarios*.
4. Cerrá sesión y volvé a ingresar escribiendo `  ADMIN  ` (con mayúsculas y espacios).
   **Esperado**: entrás igual (FR-012).

Para comprobar `ultimoAcceso` (FR-005) sin mirar la base: hasta que exista el Módulo 2 no hay
pantalla que lo muestre, así que queda cubierto por el test de integración
`ActualizaUltimoAcceso_TrasIngresoExitoso`.

### User Story 2 — Proteger funcionalidades sin sesión o sin permisos (P1)

1. Sin haber ingresado, escribí `http://localhost:5173/usuarios` en la barra de direcciones.
   **Esperado**: te manda a la pantalla de ingreso (FR-007).
2. Ingresá ahí mismo como `admin`.
   **Esperado**: llegás directamente a *Gestión de usuarios*, no a la pantalla de inicio — el
   sistema recordó a dónde querías ir (FR-026).
3. Cerrá sesión, y ahora ingresá entrando por `http://localhost:5173` sin pedir ninguna ruta.
   **Esperado**: llegás a la pantalla de inicio.

Los escenarios 2 a 4 de esta historia (usuario sin el rol, roles cambiados en caliente, cuenta dada
de baja con sesión abierta) necesitan una segunda cuenta con otros roles, que hasta el Módulo 2 no
se puede crear desde la app. Están cubiertos por tests de integración:
`RechazaOperacionSinPermiso`, `UsaRolesVigentesNoLosDelIngreso` y `CortaSesionSiLaCuentaSeDesactiva`.

### User Story 3 — Rechazo claro con credenciales inválidas (P2)

1. Probá a ingresar con un usuario que no existe.
   **Esperado**: `El usuario o la contraseña no son correctos.`
2. Probá con `admin` y una contraseña equivocada.
   **Esperado**: exactamente el mismo mensaje, sin pistas de que el usuario sí existía (FR-003).
3. Dejá los dos campos vacíos y presioná *Ingresar*.
   **Esperado**: se marcan como obligatorios y no se llama al servidor (FR-011). Comprobable en la
   solapa de red del navegador: no aparece ninguna petición.
4. Equivocá la contraseña de `admin` 5 veces seguidas y probá una sexta.
   **Esperado**: `Hubo demasiados intentos fallidos. Esperá un minuto y volvé a intentar.` Un
   minuto después ingresás normalmente y la cuenta sigue funcionando (FR-021).
   Con la cuenta todavía frenada, probá ingresar desde el mismo equipo con **otra** cuenta y sus
   credenciales correctas. **Esperado**: entra sin demora — el contador es por origen y cuenta, así
   que el error de uno no frena a los demás. Hasta que exista el Módulo 2 no hay una segunda cuenta
   para probarlo a mano; queda cubierto por el test `NoAfectaAOtrasCuentasDelMismoOrigen`.
5. Mirá la barra de direcciones y la solapa de red durante todo el proceso.
   **Esperado**: la contraseña no aparece nunca, ni en la URL ni en pantalla (FR-018).

El escenario de contraseña temporal vencida (FR-017) depende de que el Módulo 2 la genere; está
cubierto por el test de integración `RechazaPasswordTemporalVencida`.

### User Story 4 — Rechazar el ingreso de una cuenta no habilitada (P2)

Esta es la única historia que **no se puede validar operando la app** hasta que exista el Módulo 2:
FR-019 prohíbe sembrar cuentas de ejemplo y este módulo no tiene ninguna pantalla para cambiar el
estado de una cuenta. Es una consecuencia conocida y aceptada de esa decisión, anotada en las
*Assumptions* de la spec.

- **Cobertura automática**: los tres escenarios están en `GT.IntegrationTests`
  (`RechazaCuentaInactiva`, `RechazaCuentaBloqueada`,
  `CuentaInactivaConPasswordIncorrecta_DevuelveMensajeGenerico`).
- **Comprobación manual, si hace falta antes del Módulo 2**: cambiar a mano el `Estado` del usuario
  `admin` a `2` en la tabla `Usuarios` y probar el ingreso. Esperado: `Tu cuenta no está
  habilitada. Contactá al responsable de sistemas.` Después hay que volver a dejarlo en `1`.

### User Story 5 — Cerrar sesión de forma definitiva (P3)

1. Ingresá y después presioná *Cerrar sesión*.
   **Esperado**: volvés a la pantalla de ingreso.
2. Presioná el botón "atrás" del navegador.
   **Esperado**: no recuperás ninguna pantalla protegida; seguís en la pantalla de ingreso
   (FR-013).
3. Ingresá de nuevo, cerrá el navegador entero sin cerrar sesión, y volvé a abrirlo en
   `http://localhost:5173`.
   **Esperado**: te pide autenticarte de nuevo, aunque no hayan pasado 8 horas (FR-022).
4. Ingresá desde dos navegadores distintos con la misma cuenta.
   **Esperado**: las dos sesiones funcionan a la vez (FR-014).

---

## Criterios de éxito y cómo se comprueban

| Criterio | Cómo se comprueba |
|---|---|
| SC-001 — Los usuarios activos ingresan y ven su menú | User Story 1, pasos 1 a 3 |
| SC-002 — Todo ingreso exitoso registra `ultimoAcceso` | Test `ActualizaUltimoAcceso_TrasIngresoExitoso` |
| SC-003 — Los mensajes se entienden sin ayuda técnica | User Story 3, pasos 1 y 2; textos en [contracts/README.md](./contracts/README.md) |
| SC-004 — Todo acceso no autorizado por URL directa se rechaza | User Story 2, paso 1, y tests de integración |
| SC-005 — El botón "atrás" no recupera el acceso | User Story 5, paso 2 |
| SC-006 — Ninguna contraseña queda expuesta | User Story 3, paso 5 |
| SC-008 — El ingreso se puede completar sólo con teclado | Recorrido de accesibilidad, más abajo |
| SC-007 — El límite por origen y cuenta corta a partir del sexto intento, sin afectar a otras cuentas | User Story 3, paso 4, y test `NoAfectaAOtrasCuentasDelMismoOrigen` |

---

## Recorrido de accesibilidad (FR-025, SC-008)

**Apartá el mouse** y hacé todo el recorrido con el teclado:

1. Abrí la pantalla de ingreso. **Esperado**: el foco ya está en el campo de nombre de usuario.
2. Escribí un usuario, pasá al campo siguiente con `Tab`, escribí una contraseña equivocada y
   enviá con `Enter`. **Esperado**: el foco se ve siempre, y el orden de recorrido sigue el orden
   visual de la pantalla.
3. Con el mensaje de error en pantalla, corregí y volvé a enviar, siempre sin mouse.
   **Esperado**: se puede completar el ciclo entero, incluido cerrar sesión después.
4. Mirá que cada campo tenga una etiqueta visible propia, no un texto dentro del campo que
   desaparece al escribir.

Si tenés un lector de pantalla a mano, comprobá además que el mensaje de error se anuncie solo al
aparecer, sin tener que ir a buscarlo.

## El vencimiento por inactividad (FR-010)

Las 8 horas de inactividad no se pueden esperar en una validación manual. Para comprobarlo, el
plazo se lee de configuración: bajándolo a 1 minuto en el entorno de desarrollo se puede verificar
que, tras ese tiempo sin actividad, la operación siguiente muestra `Tu sesión expiró. Ingresá de
nuevo.` y lleva a la pantalla de ingreso (FR-015). En producción el valor queda en 8 horas.

No hay ningún tope máximo por encima de eso: mientras el usuario siga operando y no cierre el
navegador, la sesión se renueva indefinidamente. Es una decisión explícita, no un olvido — FR-022 ya
la acota a una jornada en la práctica.
