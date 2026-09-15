# Contrato de interfaz: Gestión de adelantos de sueldo (Módulo 10)

Qué pantallas tiene el módulo, qué muestra cada una y **con qué palabras exactas**. Los textos se
implementan tal cual: español rioplatense con voseo, moneda en pesos argentinos (Principio II). El
contrato HTTP está en [`adelantos-api.yaml`](./adelantos-api.yaml). Todas las pantallas siguen `gt-ui`
(Principio VI); la acción primaria y los estados de campo están en `plan.md` §UI Design Check.

Cinco reglas que atraviesan todo el módulo y no se repiten en cada pantalla:

- **Ningún estado se comunica sólo por color.** Los adelantos `rechazado` y `anulado` van atenuados **y**
  con la palabra de su estado (FR-039, convención [003]).
- **Ningún listado oculta filas en silencio**: si filtra por estado, el control dice cuál.
- **Todo resultado que aparece sin que la pantalla cambie** —el desplegable de personas que se vuelve a
  llenar, un filtro aplicado, un cambio de página, una aprobación, un rechazo, una anulación— se anuncia con
  `role="status"` (FR-040, convención [003]).
- **Los importes con `compartido/moneda` y las fechas con `compartido/fechas`**, incluida la fecha de hoy
  que se propone (research §10b). Nunca `toFixed(2)`, `new Date(iso)` ni `toISOString()`.
- **La persona se nombra `Apellido, Nombre`** con el DNI en mono, siempre leída del padrón vigente
  (FR-020).

---

## Pantallas

| Pantalla | Ruta | Permiso para ver | Permiso para operar |
|---|---|---|---|
| Listado de adelantos | `/adelantos` | `adelantos.consultar` | `adelantos.gestionar` |
| Registrar adelanto | `/adelantos/nuevo` | `adelantos.gestionar` | `adelantos.gestionar` |
| Detalle de adelanto | `/adelantos/:id` | `adelantos.consultar` | `adelantos.gestionar` |

Dos diálogos sobre el detalle: **Rechazar adelanto** y **Anular adelanto**. Aprobar no tiene diálogo.

La ruta literal `/adelantos/nuevo` va antes que `/adelantos/:id` en `App.tsx`.

**Menú**: dos entradas en la sección *Operación*.

| Código | Texto | Ruta | Permiso |
|---|---|---|---|
| `consultar-adelanto` | Consultar adelanto | `/adelantos` | `adelantos.consultar` |
| `registrar-adelanto` | Registrar adelanto | `/adelantos/nuevo` | `adelantos.gestionar` |

**Quien tiene sólo `adelantos.consultar`** —Gerencia— no ve *Registrar adelanto* en el menú ni en el
listado, ni ninguna acción en el detalle. Si invoca una a mano recibe `403` (FR-043). **Sin sesión**,
cualquiera de las tres rutas lleva a `/ingresar` (FR-044).

**Con sesión y sin el permiso de la pantalla** (FR-043, US6 esc. 5) —*Tráfico* en cualquiera de las tres,
*Gerencia* en *Registrar adelanto*—, la pantalla no se dibuja: debajo del título va sólo un aviso con
`role="alert"`, sin filtros, tabla, formulario, datos ni acciones, y sin nada que invite a volver a
intentar:

| Pantalla | Texto |
|---|---|
| Listado | `No tenés permiso para ver los adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.` |
| Detalle | `No tenés permiso para ver este adelanto. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.` |
| Registrar | `No tenés permiso para registrar adelantos. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.` |

El listado y el detalle lo deciden con el **`403` de su carga**: quien dice es el servidor. *Registrar
adelanto* no carga nada al abrir, así que lo decide con `adelantos.gestionar` de la sesión (convención
[005]), y el `403` del guardado sigue siendo la restricción.

**Una acción rechazada con `403`** —registrar, aprobar, rechazar o anular, por ejemplo con el permiso
quitado mientras la sesión seguía abierta— muestra en su aviso `role="alert"`, donde se muestran los demás
rechazos de esa acción: `No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.`

---

## Listado de adelantos

**Título**: `Adelantos de sueldo`. **Acción primaria** (sólo con `gestionar`): `Registrar adelanto` →
`/adelantos/nuevo`.

**Columnas** (FR-013):

| Columna | Contenido |
|---|---|
| Fecha | `14/09/2026` |
| Persona | `Pérez, Juan`, subrayado, enlace al detalle; debajo, el DNI en mono. Nombre accesible del enlace: `Ver adelanto de Pérez, Juan del 14/09/2026` |
| Motivo | `Gastos médicos`, en una línea, cortado con puntos suspensivos si no entra |
| Importe | `$ 150.000,00`, a la derecha, en negrita |
| Estado | pastilla `Pendiente` · `Aprobado` · `Rechazado` · `Anulado` |

La fila `rechazado` o `anulado` va **atenuada y con la palabra** de su estado. La fila entera navega al
detalle; el enlace de la persona es el destino real y la fila no recibe `tabIndex` (convención [008]).

El enlace nombra la fecha además de la persona porque **una misma persona tiene varias filas**: veinte
enlaces que se leen `Pérez, Juan` no se distinguen con lector de pantalla. El nombre accesible contiene el
texto visible.

**Filtros**, combinables (FR-014):

| Filtro | Control | Opciones | Sin elegir |
|---|---|---|---|
| Persona | desplegable | las personas con algún adelanto: `Pérez, Juan — 30123456` | `Todas las personas` |
| Desde | fecha | — | vacío: sin límite |
| Hasta | fecha | — | vacío: sin límite |
| Estado | desplegable | `Pendiente` · `Aprobado` · `Rechazado` · `Anulado` | `Todos los estados` |

**Rango invertido** (FR-015): al cambiar *Desde* o *Hasta* y quedar *Desde* posterior, los dos campos se
marcan, debajo de *Hasta* aparece `La fecha desde es posterior a la hasta. Corregí una de las dos.`, no se
consulta, y en el lugar de la tabla dice `Corregí el rango de fechas para ver los adelantos.`

**El filtro de estado dice qué está mostrando**, con `role="status"`:

- sin filtro: `Mostrando todos los adelantos, incluidos los rechazados y los anulados.`
- con filtro: `Mostrando sólo los adelantos pendientes.` · `…aprobados.` · `…rechazados.` · `…anulados.`

**Resumen**, debajo de los filtros (FR-016):

`{n} adelanto` / `{n} adelantos` · **`Total adelantado $ 200.000,00`** · `Suma sólo los aprobados` ·
`Ordenado por fecha, del más reciente al más antiguo`

**Paginación**: 20 filas. Cambiar cualquier filtro vuelve a la página 1.

| Situación | Texto |
|---|---|
| Cargando | `Cargando adelantos…` |
| Sin adelantos todavía | `Todavía no se registró ningún adelanto.` Con `gestionar`, además: `Registrá el primero eligiendo a la persona y el importe.` |
| Sin coincidencias | `Ningún adelanto coincide con los filtros aplicados. Probá limpiarlos.` |
| Error de carga | `No pudimos traer los adelantos. Volvé a intentar en unos minutos.` |
| Sin permiso (`403` al cargar) | el aviso de §Pantallas, sin filtros, resumen ni tabla |

---

## Registrar adelanto

**Título**: `Registrar adelanto`. **Terciario**: `Volver a adelantos`.

### Sección 1 — Beneficiario

Ayuda de la sección: `Se ofrecen los choferes de G&T Logística y los empleados activos.`

| Campo | Control | Obligatorio | Texto vacío |
|---|---|---|---|
| Tipo de persona | desplegable: `Chofer` · `Empleado` | sí | `Seleccioná el tipo` |
| Persona | desplegable: `Pérez, Juan — 30123456` | sí | sin tipo: `Elegí primero el tipo de persona` · con tipo: `Seleccioná una persona` |

**Sin tipo elegido, *Persona* no ofrece ninguna opción** más que su texto vacío (FR-002, US1 esc. 3). No se
deshabilita: sigue siendo alcanzable con el teclado y puede marcarse con su error.

**Al elegir o cambiar el tipo** (FR-003): la persona se vacía, se buscan las del tipo nuevo, y fecha,
motivo e importe quedan como estaban.

| Situación | Texto | Rol |
|---|---|---|
| Buscando | `Buscando choferes…` · `Buscando empleados…` | `status` |
| Encontradas | `Hay 2 choferes de G&T Logística para elegir.` · `Hay 1 empleado para elegir.` | `status` |
| Ninguna del tipo (FR-004) | `No hay choferes de G&T Logística activos para elegir. Los choferes se cargan desde Choferes.` · `No hay empleados activos para elegir. Las personas se cargan desde Personas.` | `status` |

**Empresa emisora sin configurar, con tipo *Chofer*** (FR-002a) — callout debajo del tipo, sin opciones de
persona:

> **No se pueden elegir choferes todavía.** Falta configurar la empresa emisora: sin su CUIT el sistema no
> distingue a los choferes de G&T Logística de los de transportistas externos. Configurala en *Empresa
> emisora* y volvé. Para un empleado, elegí el tipo *Empleado*.

*Empresa emisora* es enlace a `/facturacion/empresa`. Los dos roles con `adelantos.gestionar` tienen
`facturacion.gestionar`, así que el enlace siempre lleva a una pantalla que pueden abrir.

### Sección 2 — Adelanto

| Campo | Control | Obligatorio | Propuesto | Texto vacío | Ayuda |
|---|---|---|---|---|---|
| Fecha | fecha | sí | hoy | — | `Desde el 01/08/2026 hasta hoy.` |
| Motivo | texto, hasta 200 | sí | — | `Gastos médicos` | — |
| Importe | importe con decimales, el mismo control que la orden de pago del Módulo 9 | sí | — | `150000,00` | — |

El `01/08/2026` de la ayuda es **el primer día del mes anterior a hoy**, calculado en la pantalla y nunca
escrito (FR-005, convención [009]).

### Errores de campo

Al perder el foco habiendo tocado el campo, o al apretar *Guardar adelanto*. Todo lo cargado se conserva
(FR-008).

| Campo | Situación | Texto |
|---|---|---|
| Tipo de persona | vacío | `Elegí si el adelanto es para un chofer o un empleado.` |
| Persona | vacía, sin tipo | `Elegí primero el tipo de persona.` |
| Persona | vacía, con tipo | `Elegí la persona que recibe el adelanto.` |
| Fecha | vacía | `Elegí la fecha en que se otorgó el adelanto.` |
| Fecha | fuera de rango | `La fecha tiene que estar entre el 01/08/2026 y hoy.` |
| Motivo | vacío o sólo espacios | `Escribí el motivo del adelanto. Ejemplo: gastos médicos` |
| Importe | vacío, cero o negativo | `Escribí un importe mayor que cero. Ejemplo: 150000,00` |
| Importe | más de dos decimales o mal escrito | `Escribí el importe con hasta dos decimales. Ejemplo: 150000,50` |

### Barra de acciones

`* Obligatorio` a la izquierda. A la derecha: `Cancelar` (secundario, vuelve al listado sin guardar) y
**`Guardar adelanto`** (primario). No se deshabilita: apretarlo con campos vacíos los marca.

### Resultado

**Guardado**: navega al detalle del adelanto creado, que anuncia
`Se registró el adelanto de $ 150.000,00 para Pérez, Juan. Queda pendiente de aprobación.` El formulario no
queda en pantalla (FR-011, convención [005]).

**Rechazos al guardar** — aviso de nivel formulario, `role="alert"`, sin salir de la pantalla y
conservando lo cargado. Los textos los arma el servidor:

| Código | Motivo | Texto |
|---|---|---|
| `beneficiario_no_elegible` | `inactiva` | `Pérez, Juan se dio de baja y ya no puede recibir adelantos. Elegí otra persona.` |
| `beneficiario_no_elegible` | `tipoDistinto` | `Pérez, Juan ya no figura como chofer. Elegí el tipo de persona otra vez para ver a quiénes se puede elegir.` · `…como empleado.…` |
| `beneficiario_no_elegible` | `choferExterno` | `Pérez, Juan maneja para un transportista externo, y los adelantos son sólo para choferes de G&T Logística. Elegí otra persona.` |
| `beneficiario_no_elegible` | `inexistente` | `La persona elegida ya no está en el padrón. Elegí otra.` |
| `empresa_emisora_no_configurada` | — | el texto del callout de arriba |
| `fecha_fuera_de_rango` | — | marca la fecha con `La fecha tiene que estar entre el {desde} y hoy.`, con el `desde` del servidor |

---

## Detalle de adelanto

### Encabezado

- **Título**: `Adelanto`, con la pastilla del estado en la misma línea.
- **Contexto** debajo: `Pérez, Juan · Chofer · 14/09/2026`.
- **Terciario**: `Volver a adelantos`.

### Acciones (sólo con `adelantos.gestionar`)

| Estado | Primaria | Secundaria | Destructiva |
|---|---|---|---|
| `pendiente` | `Aprobar adelanto` | `Rechazar adelanto` | — |
| `aprobado` | ninguna | — | `Anular adelanto` |
| `rechazado` | ninguna | — | — |
| `anulado` | ninguna | — | — |

**Aprobar** no abre diálogo: ejecuta, se deshabilita mientras espera, relee el detalle y anuncia
`Se aprobó el adelanto de $ 150.000,00 para Pérez, Juan.`

### Callouts de estado

Van debajo del encabezado. Dicen qué pasa **y** qué hacer (principio 4 de `gt-ui`). Quien sólo consulta
los ve **sin** la última oración, que es una instrucción que no puede seguir.

| Estado | Texto |
|---|---|
| `pendiente` | **Pendiente de aprobación.** Todavía no suma en el total adelantado. Aprobalo, o rechazalo con su motivo. |
| `aprobado` | — |
| `rechazado` | **Adelanto rechazado — cerrado.** Motivo: {motivo}. No se corrige ni se vuelve a presentar: si hace falta, registrá un adelanto nuevo. |
| `anulado` | **Adelanto anulado — cerrado.** Motivo: {motivo}. Ya no suma en el total adelantado. Si correspondía a otra persona o a otro importe, registrá un adelanto nuevo. |

### Columna principal

**Tarjeta `Datos del adelanto`** (FR-019):

| Dato | Contenido |
|---|---|
| Persona | `Pérez, Juan`; debajo, el DNI en mono |
| Tipo | `Chofer` · `Empleado` — el elegido al registrar, aunque hoy sea otro (FR-020) |
| Fecha | `14/09/2026` |
| Motivo | `Gastos médicos`, completo |

**Tarjeta `Historial`** — línea de tiempo, de la más vieja a la más nueva (FR-036):

| Operación | Texto |
|---|---|
| `registro` | `Registrado por Gómez, Ramona` |
| `aprobacion` | `Aprobado por Gómez, Ramona` |
| `rechazo` | `Rechazado por Gómez, Ramona` · debajo, `Motivo: {motivo}` |
| `anulacion` | `Anulado por Gómez, Ramona` · debajo, `Motivo: {motivo}` |

Cada entrada con `dd/mm/aaaa hh:mm` en metadata.

### Aside

| Estado | Cifra destacada (32 px) | Debajo |
|---|---|---|
| `pendiente` | `Importe` · `$ 150.000,00` | `Todavía no cuenta como adelantado.` |
| `aprobado` | `Importe` · `$ 150.000,00` | `Cuenta en el total adelantado.` |
| `rechazado`, `anulado` | `Importe` · `$ 150.000,00` | `No cuenta en el total adelantado.` |

| Situación | Texto |
|---|---|
| Cargando | `Cargando adelanto…` |
| No existe | `No encontramos ese adelanto.` + enlace `Volver a adelantos` |
| Sin permiso (`403` al cargar) | el aviso de §Pantallas, sin datos ni acciones |

### Rechazos de las acciones

Aviso `role="alert"` debajo del encabezado; el detalle **se relee** para mostrar el estado actual.

| Código | Texto |
|---|---|
| `adelanto_no_resoluble` | `Este adelanto ya está aprobado: no se puede aprobar ni rechazar.` · `…ya está rechazado: …` · `…ya está anulado: …` |
| `adelanto_no_anulable` | `Este adelanto está pendiente: sólo se anula un adelanto aprobado. Si está mal cargado, rechazalo.` · `Este adelanto está rechazado: no se puede anular.` · `Este adelanto ya está anulado.` |

---

## Rechazar adelanto (diálogo)

**Título**: `Rechazar adelanto`. **Subtítulo**: `Pérez, Juan · $ 150.000,00`.

> El adelanto queda rechazado y no se vuelve a presentar. Si hace falta, se registra uno nuevo. No se
> puede deshacer.

| Campo | Control | Obligatorio | Límite | Texto vacío |
|---|---|---|---|---|
| Motivo | texto de varias líneas | sí | 500 | `Ya tiene un adelanto pendiente del mes anterior.` |

| Error | Texto |
|---|---|
| Motivo vacío o sólo espacios (FR-024) | `Escribí el motivo del rechazo: queda en el historial.` |

Botones: `Volver` (secundario, cierra sin llamar al servidor, FR-025) · **`Rechazar adelanto`** (primario).
El botón no se deshabilita sin motivo: apretarlo marca el campo. **Este diálogo es la confirmación
explícita**: el envío lleva `confirmado: true` (research §4).

**Resultado**: cierra, el detalle se relee y anuncia `Se rechazó el adelanto de $ 150.000,00 para Pérez, Juan.`

Un rechazo del servidor —`adelanto_no_resoluble`— se muestra adentro del diálogo, con `role="alert"`.

---

## Anular adelanto (diálogo)

**Título**: `Anular adelanto`.

> El adelanto de **$ 150.000,00** para **Pérez, Juan** queda anulado y deja de sumar en el total
> adelantado. No se puede deshacer.

| Campo | Control | Obligatorio | Límite | Texto vacío |
|---|---|---|---|---|
| Motivo | texto de varias líneas | sí | 500 | `Cargado sobre la persona equivocada.` |

| Error | Texto |
|---|---|
| Motivo vacío o sólo espacios (FR-029) | `Escribí el motivo de la anulación: queda en el historial.` |

Botones: `Volver` (secundario, cierra sin llamar al servidor, FR-030) · **`Anular adelanto`**
(destructivo). **Este diálogo es la confirmación explícita**: el envío lleva `confirmado: true` (research
§4).

**Resultado**: cierra, el detalle se relee y anuncia `Se anuló el adelanto de $ 150.000,00 para Pérez, Juan.`

Un rechazo del servidor —`adelanto_no_anulable`— se muestra adentro del diálogo, con `role="alert"`.
