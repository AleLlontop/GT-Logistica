# Contrato de interfaz: Gestión de liquidación a transportistas (Módulo 9)

Qué pantallas tiene el módulo, qué muestra cada una y **con qué palabras exactas**. Los textos de acá
se implementan tal cual: español rioplatense con voseo, moneda en pesos argentinos (Principio II). El
contrato HTTP está en [`liquidaciones-api.yaml`](./liquidaciones-api.yaml). Todas las pantallas siguen
`gt-ui` (Principio VI); las decisiones de acción primaria y estados de campo están en `plan.md`
§UI Design Check.

Cinco reglas que atraviesan todo el módulo y no se repiten en cada pantalla:

- **Ningún estado se comunica sólo por color**, y toda liquidación atenuada lleva además la palabra de su
  estado (FR-032).
- **Ningún listado oculta filas en silencio**: si filtra por estado, el control dice cuál.
- **Todo resultado que aparece sin que la pantalla cambie** —la lista de viajes encontrada, el total
  recalculado en la edición, un cambio de página, un guardado que vuelve a la ficha— se anuncia con
  `role="status"` (FR-062, convención [003]).
- **Los importes con `compartido/moneda`, las fechas con `compartido/fechas` y los CUIT con
  `compartido/cuit`.** Nunca `toFixed(2)`, `new Date(iso).toLocaleDateString()` ni un formato de CUIT
  escrito a mano (convenciones [003] y [005], research §11b).
- **Los números de liquidación y de orden de pago llegan armados del servidor** —`LQ-12`, `OP-3`— y se
  muestran tal cual, en mono y como token de identificador (research §4). Los viajes se muestran con su
  token de siempre, `#13`.

---

## Pantallas

| Pantalla | Ruta | Permiso para ver | Permiso para operar |
|---|---|---|---|
| Listado de liquidaciones | `/liquidaciones` | `liquidaciones.consultar` | `liquidaciones.gestionar` |
| Generar liquidación | `/liquidaciones/nueva` | `liquidaciones.gestionar` | `liquidaciones.gestionar` |
| Detalle de liquidación | `/liquidaciones/:id` | `liquidaciones.consultar` | `liquidaciones.gestionar` |
| Editar liquidación | `/liquidaciones/:id/editar` | `liquidaciones.gestionar` | `liquidaciones.gestionar` |

Dos diálogos sobre el detalle: **Registrar orden de pago** y **Anular liquidación**.

La ruta literal `/liquidaciones/nueva` va antes que `/liquidaciones/:id` en `App.tsx`.

**Menú**: dos entradas en la sección *Operación*.

| Código | Texto | Ruta | Permiso |
|---|---|---|---|
| `consultar-liquidacion` | Consultar liquidación | `/liquidaciones` | `liquidaciones.consultar` |
| `generar-liquidacion` | Generar liquidación | `/liquidaciones/nueva` | `liquidaciones.gestionar` |

**Quien tiene sólo `liquidaciones.consultar`** —Gerencia— no ve la entrada *Generar liquidación*, ni el
botón de generar en el listado, ni ninguna acción en el detalle. Si invoca una a mano recibe `403`
(FR-065, SC-012). **Sin sesión**, cualquiera de las cuatro rutas lleva a `/ingresar` (FR-066).

---

## Listado de liquidaciones

**Título**: `Liquidaciones`. **Acción primaria** (sólo con `gestionar`): `Generar liquidación` →
`/liquidaciones/nueva`.

**Columnas** (FR-021):

| Columna | Contenido |
|---|---|
| Número | token `LQ-12`, enlace al detalle |
| Período | `07/2026` |
| Transportista | razón social; debajo, CUIT en mono `20-12345678-6`. Dado de baja: la palabra `Inactivo` al lado |
| Importe total | `$ 355.000,00`, a la derecha, en negrita |
| Resta pagar | `$ 155.000,00`, a la derecha, en negrita. `pagada`: `$ 0,00`. `anulada`: `No corresponde`, en `faint` |
| Estado | pastilla `Pendiente` · `Pagada` · `Anulada`. La anulada suma su motivo debajo, en metadata |

La fila `anulada` va **atenuada y con la palabra** de su estado (FR-032). La fila entera navega al
detalle; el token es el destino real (convención [008]).

**Filtros**, combinables (FR-022):

| Filtro | Opciones | Sin elegir |
|---|---|---|
| Transportista | externos activos **y dados de baja**, por razón social | `Todos los transportistas` |
| Mes | `01` … `12` | `Todos los meses` |
| Año | `2025` · `2026` | `Todos los años` |
| Estado | `Pendiente` · `Pagada` · `Anulada` | `Todos los estados` |

**El filtro de estado dice qué está mostrando**, con `role="status"`:

- sin filtro: `Mostrando todas las liquidaciones, incluidas las anuladas.`
- con filtro: `Mostrando sólo las liquidaciones pendientes.` · `…pagadas.` · `…anuladas.`

**Resumen**: `{n} liquidación` / `{n} liquidaciones` · `Ordenado por número, de la más nueva a la más
vieja`.

**Paginación**: 20 filas. Cambiar cualquier filtro vuelve a la página 1.

| Situación | Texto |
|---|---|
| Cargando | `Cargando liquidaciones…` |
| Sin liquidaciones todavía | `Todavía no se generó ninguna liquidación. Generá la primera eligiendo un transportista y un período.` |
| Sin coincidencias | `Ninguna liquidación coincide con los filtros aplicados. Probá limpiarlos.` |
| Error de carga | `No pudimos traer las liquidaciones. Volvé a intentar en unos minutos.` |

---

## Generar liquidación

**Título**: `Generar liquidación`. **Terciario**: `Volver a liquidaciones`.

### Antes del formulario

**Empresa emisora sin configurar** (FR-001a) — callout de bloqueo, sin formulario:

> **No se puede generar todavía.** Falta configurar la empresa emisora: sin su CUIT el sistema no
> distingue a G&T Logística de los transportistas externos. Configurala en *Empresa emisora* y volvé.

*Empresa emisora* es enlace a `/facturacion/empresa`. Los dos roles con `liquidaciones.gestionar`
tienen `facturacion.gestionar`, así que el enlace siempre lleva a una pantalla que pueden abrir.

**Ningún transportista externo activo** — estado vacío, sin formulario:

> **No hay transportistas externos activos.** Cargá uno en *Transportistas* para poder liquidarle.

### Sección 1 — Transportista y período

Ayuda de la sección: `Se liquidan los viajes rendidos de ese transportista con fecha dentro del mes
elegido.`

| Campo | Control | Obligatorio | Texto vacío |
|---|---|---|---|
| Transportista | desplegable: `Transportes Díaz — 20-12345678-6` | sí | `Seleccioná un transportista` |
| Mes | desplegable `01` … `12` | sí | `Mes` |
| Año | desplegable `2025` · `2026` | sí | `Año` |

Acción de la sección, **secundaria**: `Buscar viajes`.

| Error (al perder el foco habiendo tocado el campo, o al apretar *Buscar viajes*) | Texto |
|---|---|
| Transportista vacío | `Elegí el transportista al que le vas a liquidar.` |
| Mes vacío | `Elegí el mes del período.` |
| Año vacío | `Elegí el año del período.` |

Con algún campo vacío, *Buscar viajes* **no busca** (FR-003).

**Cambiar el transportista, el mes o el año después de buscar** vacía la lista y el total, deshabilita
*Guardar liquidación* y anuncia: `Cambiaste la selección. Buscá los viajes de nuevo.` (FR-010).

### Sección 2 — Viajes a liquidar

| Situación | Texto |
|---|---|
| Todavía no se buscó | `Elegí el transportista y el período, y buscá sus viajes.` |
| Buscando | `Buscando viajes rendidos…` |
| Sin viajes (FR-009) | `Transportes Díaz no tiene viajes rendidos para liquidar en 08/2026. Los viajes pendientes, en curso, anulados o ya liquidados no se ofrecen.` |

**Tabla**, una fila por viaje disponible (FR-006):

| Columna | Contenido |
|---|---|
| Viaje | token `#13` |
| Fecha | `12/07/2026` |
| Ruta | `Rosario → Córdoba` |
| Estado | pastilla `Rendido` o `Facturado` |
| Importe | `$ 120.000,00`, a la derecha, en negrita |

**Pie de la tabla**: `3 viajes` a la izquierda · `Importe total` y `$ 355.000,00` a la derecha, en
negrita. No hay casillas: la liquidación agrupa todos los viajes listados (FR-008).

**Anuncio al encontrar**: `Se encontraron 3 viajes por $ 355.000,00.`

**Total en cero** (FR-012a) — callout debajo de la tabla, y *Guardar liquidación* deshabilitado:

> **No hay importe a liquidar.** Los viajes de este período suman $ 0,00, y no se genera una liquidación
> sin importe.

### Barra de acciones

`* Obligatorio` a la izquierda. A la derecha: `Cancelar` (secundario, vuelve al listado) y
**`Guardar liquidación`** (primario), **habilitado sólo con viajes listados y total mayor que cero**.

### Resultado

**Guardado**: navega al detalle de la liquidación creada, que anuncia:
`Se generó la liquidación LQ-12 por $ 355.000,00 con 3 viajes.` El formulario no queda en pantalla
(FR-019, convención [005]).

**Rechazos al guardar** — aviso de nivel formulario, `role="alert"`, sin salir de la pantalla:

| Código | Texto |
|---|---|
| `viaje_ya_liquidado` | `El viaje #102 ya está en la liquidación LQ-5. Buscá los viajes de nuevo para ver los que siguen disponibles.` |
| `viaje_no_liquidable` | `El viaje #102 ya no se puede liquidar: {motivo}. Buscá los viajes de nuevo.` |
| `transportista_no_liquidable` | `Ese transportista ya no se puede liquidar: es G&T Logística o se dio de baja. Elegí otro.` |
| `empresa_emisora_no_configurada` | el texto del callout de arriba |
| `total_en_cero` | `Los viajes elegidos suman $ 0,00. No se genera una liquidación sin importe.` |

Motivos de `viaje_no_liquidable`: `no está rendido ni facturado` · `es de otro transportista` · `es de
otro período`.

---

## Detalle de liquidación

### Encabezado

- **Título**: `Liquidación`, con el token `LQ-12` y la pastilla del estado en la misma línea.
- **Contexto** debajo: `Transportes Díaz · 07/2026 · Generada el 14/09/2026`.
- **Terciario**: `Volver a liquidaciones`.

### Acciones (sólo con `liquidaciones.gestionar`)

| Situación | Primaria | Secundaria | Destructiva |
|---|---|---|---|
| `pendiente` sin órdenes de pago | `Registrar orden de pago` | `Editar liquidación` | `Anular liquidación` |
| `pendiente` con órdenes de pago | `Registrar orden de pago` | — | — |
| `pagada` | ninguna | — | — |
| `anulada` | ninguna | — | — |

### Callouts de estado

Van debajo del encabezado. Dicen qué pasa **y** qué hacer (principio 4 de `gt-ui`):

| Situación | Texto |
|---|---|
| `pendiente` con órdenes de pago | **Tiene pagos registrados — ya no se edita ni se anula.** Registrá el resto del pago para cerrarla. |
| `pagada` | **Liquidación pagada — cerrada.** No se edita, no se anula y no admite más órdenes de pago. |
| `anulada` | **Liquidación anulada.** Motivo: {motivo}. Sus viajes quedaron disponibles para liquidarse de nuevo desde *Generar liquidación*. |

Quien sólo consulta ve los mismos callouts **sin** la segunda oración de la `pendiente` con pagos, que
es una instrucción que no puede seguir.

### Columna principal

**Tarjeta `Viajes liquidados`** —en una `anulada`, `Viajes que agrupaba`— (FR-025, FR-028):

| Columna | Contenido |
|---|---|
| Viaje | token `#13`, enlace a `/viajes/:id` |
| Fecha | `12/07/2026` |
| Ruta | `Rosario → Córdoba` |
| Importe | `$ 120.000,00`, a la derecha, en negrita |

Pie: `3 viajes` · `Importe total` `$ 355.000,00`.

**Tarjeta `Órdenes de pago`**:

| Columna | Contenido |
|---|---|
| Orden | token `OP-3` |
| Fecha de pago | `20/09/2026` |
| Registrada por | `Gómez, Ramona`; debajo, `20/09/2026 11:42` en metadata |
| Importe | `$ 200.000,00`, a la derecha, en negrita |

Sin órdenes: `Todavía no se registró ninguna orden de pago.`

**Tarjeta `Historial`** — línea de tiempo, de la más vieja a la más nueva (FR-033):

| Operación | Texto |
|---|---|
| `generacion` | `Generada por Gómez, Ramona` |
| `edicion` | `Editada por Gómez, Ramona` · debajo, `Quitó #13 · Agregó #21`, omitiendo la parte que no tenga viajes |
| `anulacion` | `Anulada por Gómez, Ramona` · debajo, `Motivo: {motivo}` |

Cada entrada con `dd/mm/aaaa hh:mm` en metadata.

### Aside

| Estado | Cifra destacada (32 px) | Debajo |
|---|---|---|
| `pendiente` | `Resta pagar` · `$ 155.000,00` | `Importe total $ 355.000,00` · `Pagado $ 200.000,00` |
| `pagada` | `Resta pagar` · `$ 0,00` | `Importe total` · `Pagado` |
| `anulada` | `Importe total` · `$ 355.000,00` | `Anulada: no hay saldo por pagar.` |

Después, **Transportista**: razón social y CUIT en mono. Dado de baja: `Inactivo`.

| Situación | Texto |
|---|---|
| Cargando | `Cargando liquidación…` |
| No existe | `No encontramos esa liquidación.` + enlace `Volver a liquidaciones` |

---

## Registrar orden de pago (diálogo)

**Título**: `Registrar orden de pago`. **Subtítulo**: `LQ-12 · Transportes Díaz · Resta pagar
$ 155.000,00`.

### Paso 1 — Datos

| Campo | Control | Obligatorio | Propuesto | Ayuda |
|---|---|---|---|---|
| Fecha de pago | fecha | sí | hoy | `Entre el 14/09/2026 y hoy.` |
| Importe | número con decimales | sí | lo que resta pagar | `Hasta $ 155.000,00.` |

| Error | Texto |
|---|---|
| Fecha vacía | `Elegí la fecha en que se pagó.` |
| Fecha fuera de rango (FR-038) | `La fecha de pago tiene que estar entre el 14/09/2026 y hoy.` |
| Importe vacío, cero o negativo | `Escribí un importe mayor que cero. Ejemplo: 120000,00` |
| Importe que supera lo que resta (FR-037) | `El importe supera lo que resta pagar: $ 155.000,00.` |

Botones: `Cancelar` (secundario) · **`Registrar orden de pago`** (primario). Enviar sin `confirmado`
responde `409 pago_requiere_confirmacion` y pasa al paso 2 **con los importes que devolvió el servidor**
(research §7).

### Paso 2 — Confirmación

> Vas a registrar una orden de pago por **$ 100.000,00**. Después de ella resta pagar **$ 55.000,00**.
> Una orden de pago no se modifica ni se elimina.

Si `quedaPagada` es `true`, la segunda oración pasa a ser: `Con ella la liquidación queda pagada y se
cierra.`

Botones: `Volver` (secundario, regresa al paso 1 con los datos) · **`Confirmar orden de pago`**
(primario).

### Resultado

Cierra el diálogo, el detalle se relee y anuncia:

- `Se registró la orden de pago OP-3 por $ 100.000,00.`
- si quedó pagada: `Se registró la orden de pago OP-4 por $ 55.000,00. La liquidación quedó pagada.`

| Rechazo | Texto |
|---|---|
| `liquidacion_no_pagable` | `La liquidación LQ-12 ya está pagada: no admite más órdenes de pago.` · `…ya está anulada: …` |
| `importe_supera_saldo` (otro pago se adelantó) | `El importe supera lo que resta pagar: $ 55.000,00. Otro pago se registró mientras tanto.` |

---

## Anular liquidación (diálogo)

**Título**: `Anular liquidación LQ-12`.

> La liquidación queda anulada y sus 3 viajes vuelven a estar disponibles para liquidarse. No se puede
> deshacer.

| Campo | Control | Obligatorio | Límite | Texto vacío |
|---|---|---|---|---|
| Motivo | texto de varias líneas | sí | 500 | `El período no correspondía: los viajes eran de junio.` |

| Error | Texto |
|---|---|
| Motivo vacío (FR-055) | `Escribí el motivo de la anulación: queda en el historial.` |

Botones: `Volver` (secundario) · **`Anular liquidación`** (destructivo). **Este diálogo es la
confirmación explícita**: el envío lleva `confirmado: true` (research §7).

**Resultado**: el detalle se relee y anuncia `Se anuló la liquidación LQ-12. Sus 3 viajes quedaron
disponibles.`

| Rechazo | Texto |
|---|---|
| `liquidacion_no_anulable` con órdenes (FR-054) | `La liquidación LQ-12 tiene 1 orden de pago por $ 200.000,00: ya no se puede anular.` |
| `liquidacion_no_anulable` pagada | `La liquidación LQ-12 está pagada: ya no se puede anular.` |
| `liquidacion_no_anulable` anulada | `La liquidación LQ-12 ya está anulada.` |

---

## Editar liquidación

**Título**: `Editar liquidación LQ-12`. **Terciario**: `Volver a la liquidación`.

**Si al abrir ya no es editable** (FR-045): callout de bloqueo con el motivo y sin formulario —
`La liquidación LQ-12 ya tiene órdenes de pago: no se puede editar.` · `…está pagada: …` · `…está
anulada: …`— y el enlace `Volver a la liquidación`.

### Sección 1 — Transportista y período

Sólo lectura: `Transportes Díaz — 20-12345678-6 · 07/2026`.
Ayuda: `No se cambian. Si están mal, anulá la liquidación y generá una nueva.`

### Sección 2 — Viajes incluidos

Misma tabla que en la generación, con una acción por fila al final: `Quitar`, con nombre accesible
`Quitar viaje #13`. Sin filas: `La liquidación se quedó sin viajes. Agregá al menos uno o volvé sin
guardar.`

### Sección 3 — Viajes disponibles del período

Los disponibles de ese transportista y período (FR-046), con `Agregar` por fila —`Agregar viaje #21`—.
Sin filas: `No hay otros viajes rendidos de Transportes Díaz en 07/2026 para agregar.`

**Quitar mueve la fila a disponibles y agregar la mueve a incluidos**, sin llamar al servidor: lo que se
guarda es el conjunto final.

### Total

Debajo de la sección 2: `2 viajes` · `Importe total` `$ 260.000,00`. Cada cambio lo recalcula y anuncia
`Importe total: $ 260.000,00 con 2 viajes.` (FR-047, FR-062).

### Barra de acciones

`Cancelar` (secundario, vuelve al detalle sin guardar) · **`Guardar cambios`** (primario), deshabilitado
sin viajes incluidos, con total en cero o sin cambios respecto de lo guardado.

### Resultado

**Guardado**: navega al detalle, que anuncia `Se guardaron los cambios de la liquidación LQ-12: ahora
suma $ 260.000,00 con 2 viajes.`

| Rechazo | Texto |
|---|---|
| `viaje_ya_liquidado` | `El viaje #21 ya está en la liquidación LQ-14. Volvé a abrir la edición para ver los que siguen disponibles.` |
| `liquidacion_modificada` | `Otro usuario guardó cambios en la liquidación LQ-12 mientras la editabas. Volvé a abrir la edición para ver cómo quedó y rehacer tus cambios.` |
| `liquidacion_no_editable` | el mismo texto del callout de bloqueo |
| `total_en_cero` | `Los viajes incluidos suman $ 0,00. Una liquidación necesita importe a liquidar.` |
