# Contrato de interfaz: Gestión de viajes (Módulo 5)

Qué pantallas tiene el módulo, qué muestra cada una y **con qué palabras exactas**. Los textos de acá
son los que se implementan tal cual: español rioplatense con voseo, moneda en pesos argentinos
(Principio II). El contrato HTTP está en [`viajes-api.yaml`](./viajes-api.yaml).

Dos reglas que atraviesan todo el módulo y no se repiten en cada pantalla:

- **Ningún estado se comunica sólo por color**, y todo elemento atenuado lleva además la palabra que lo
  explica (FR-049).
- **Ningún listado oculta filas en silencio**: si filtra por estado, el control dice cuál (FR-049).

---

## Pantallas

| Pantalla | Ruta | Permiso para ver | Permiso para operar |
|---|---|---|---|
| Listado de clientes | `/clientes` | `viajes.consultar` | `viajes.gestionar` |
| Formulario de cliente | `/clientes/nuevo`, `/clientes/:id` | `viajes.gestionar` | `viajes.gestionar` |
| Listado de viajes | `/viajes` | `viajes.consultar` | `viajes.gestionar` |
| Formulario de viaje | `/viajes/nuevo`, `/viajes/:id/editar` | `viajes.gestionar` | `viajes.gestionar` |
| Ficha de viaje | `/viajes/:id` | `viajes.consultar` | `viajes.gestionar` |
| Asignación de chofer y vehículo | `/viajes/:id/asignacion` | `viajes.gestionar` | `viajes.gestionar` |
| Totales por período | `/viajes/totales` | `viajes.consultar` | — |

**Menú**: tres entradas, las tres atadas a `viajes.consultar` —*Viajes*, *Clientes* y *Totales*—
porque las tres pantallas se pueden mirar sin poder tocar nada. El servidor las resuelve por permiso,
como siempre: el frontend dibuja lo que recibe (precedente del Módulo 2).

**Quien tiene sólo `viajes.consultar`** no ve el botón de alta, ni el de editar, ni el de asignar, ni
los de cambio de estado, ni el de anular, ni en el listado ni en la ficha. Y si invoca la acción a
mano recibe `403`: la restricción no vive sólo en la pantalla (FR-052, SC-012).

---

## Listado de clientes

**Columnas**: razón social · CUIT · teléfono · email · estado.

**Acciones por fila**: *Editar* · *Dar de baja* (activo) o *Dar de alta* (inactivo).

**Paginación**: 20 filas, con el total de coincidencias.

Un cliente inactivo se muestra atenuado **y** con la palabra `Inactivo` al lado de su razón social.

**Estados vacíos**

| Situación | Texto |
|---|---|
| Padrón vacío | `Todavía no hay clientes cargados. Registrá el primero para poder empezar a cargar viajes.` |
| Sin coincidencias | `Ningún cliente coincide con los filtros aplicados.` |

**Confirmación de baja** (FR-005). Cancelar no modifica nada.

> **¿Dar de baja a {razón social}?**
> Deja de ofrecerse al registrar viajes. Sus viajes históricos se conservan y podés darlo de alta de
> nuevo cuando quieras.
> [Cancelar] [Dar de baja]

**El alta de nuevo no pide confirmación aparte**: no destruye nada y se deshace con la baja, que sí la
pide (FR-007, precedente [004]).

---

## Formulario de cliente

| Campo | Obligatorio | Límite |
|---|---|---|
| Razón social | sí | 100 |
| CUIT | sí | 11 dígitos |
| Teléfono | sí | 30 |
| Email | sí | 254 |
| Dirección | no | 200 |

El CUIT se normaliza a sólo dígitos antes de validar: escribir `30-71234567-8` es válido y se guarda
como `30712345678` (FR-004, misma regla que el Módulo 3).

**Confirmaciones**

- `El cliente {razón social} quedó registrado y ya se puede elegir al cargar un viaje.`
- `Los datos de {razón social} quedaron actualizados.`
- `{razón social} quedó dado de baja. Deja de ofrecerse al registrar viajes.`
- `{razón social} volvió al padrón. Se ofrece de nuevo al registrar viajes.`

---

## Listado de viajes

**Columnas** (FR-040): número · fecha · cliente · origen · destino · chofer · vehículo · transportista
· estado · importe.

**Filtros** (FR-041), combinables entre sí: cliente · rango de fechas · estado · transportista. Los
tres primeros son selección exacta entre las opciones ya cargadas.

**Búsqueda** (FR-042): un solo campo, coincidencia parcial sobre origen, destino y razón social del
cliente, sin distinguir mayúsculas ni acentos. Etiqueta: `Buscar por origen, destino o cliente`.

**Orden**: fecha descendente y, a igual fecha, número descendente. Lo más reciente primero.

**Paginación**: 20 filas, con el total de coincidencias. **Sin fila de total de importes**: los totales
viven en su pantalla (FR-046a).

**Sin filtro de estado no se muestran los anulados**, y el control lo dice: la opción por defecto se
llama `Todos menos anulados` (FR-044, FR-049).

**Señales por fila**, todas con palabra y no sólo con color:

| Señal | Cómo se ve | Cuándo |
|---|---|---|
| Estado | `Pendiente` · `En curso` · `Rendido` · `Anulado` | siempre |
| Demorado | etiqueta `Demorado` junto al estado `En curso` | más de 5 días corridos en curso (FR-039) |
| Retroactivo | etiqueta `Carga retroactiva` junto a la fecha | fecha anterior a hoy (FR-016) |
| Chofer o vehículo dado de baja | `Gómez, Juan (inactivo)` | el chofer o el vehículo está inactivo (FR-030) |
| Cliente dado de baja | `Distribuidora del Litoral (inactivo)` | el cliente está inactivo (FR-008) |

**Estados vacíos**

| Situación | Texto |
|---|---|
| Sin viajes cargados | `Todavía no hay viajes registrados. Registrá el primero para empezar.` |
| Sin coincidencias | `Ningún viaje coincide con los filtros aplicados.` |

**Al filtrar por `Anulado`**, cada fila muestra su motivo (FR-036).

---

## Formulario de viaje

| Campo | Obligatorio | Límite |
|---|---|---|
| Cliente | sí | selección entre clientes **activos** |
| Fecha | sí | sin límite de antigüedad ni de anticipación |
| Origen | sí | 100, texto libre |
| Destino | sí | 100, texto libre |
| Número de remito | no | 50 |
| Detalle de carga | no | 500 |
| Importe | no (por defecto 0) | ≥ 0, dos decimales |

**No hay chofer ni vehículo en este formulario**, ni en el alta ni en la edición: el viaje se registra
primero y se asigna después, desde su propia acción (FR-019a, US3 esc. 14 y 15).

**El número de viaje se muestra y nunca es editable**, en ningún estado (FR-011, FR-017).

**Sin ningún cliente activo cargado**, el formulario no deja completar el alta y muestra:

> `Todavía no hay clientes activos. Cargá al menos un cliente antes de registrar viajes.`
> [Ir a Clientes]

**Confirmaciones y advertencias**

| Situación | Texto |
|---|---|
| Alta | `El viaje {número} quedó registrado como pendiente.` |
| Edición | `Los datos del viaje {número} quedaron actualizados.` |
| Origen = destino (advertencia, no frena) | `El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien.` |
| Fecha pasada | `Estás cargando un viaje con fecha anterior a hoy. Queda registrado como carga retroactiva.` |

Las dos advertencias **llegan con el resultado**: el viaje ya se guardó y no hay ningún paso extra que
dar (FR-015a, reversible).

---

## Ficha de viaje

Muestra todo (FR-045): número · cliente · origen · destino · fecha · remito · detalle de carga ·
importe · estado · chofer · vehículo · transportista · motivo de anulación cuando corresponde · e
**historial completo de cambios de estado**.

**Historial**, una fila por cambio, de la más vieja a la más nueva:

```
Alta            →  Pendiente     jtrafico     10/08/2026 09:14
Pendiente       →  En curso      jtrafico     11/08/2026 06:02
En curso        →  Rendido       jtrafico     13/08/2026 18:40
```

**Acciones**, según el estado. La pantalla ofrece exactamente las que el estado admite y ninguna más:

| Estado | Acciones ofrecidas |
|---|---|
| `pendiente` | Editar · Asignar chofer y vehículo · Poner en curso · Anular |
| `en curso` | Editar · Reasignar chofer y vehículo · Rendir · Anular |
| `rendido` | **ninguna** |
| `anulado` | **ninguna** |

En un viaje `rendido` la ficha lo dice, para que no parezca que faltan botones:

> `Este viaje está rendido. Los viajes rendidos no se editan, no se reasignan y no se anulan.`

*Poner en curso* está deshabilitado —con el motivo a la vista, no en silencio— si falta asignar:

> `Asigná chofer y vehículo antes de poner el viaje en curso.`

---

## Asignación de chofer y vehículo

Pantalla propia, con dos desplegables y un botón. Es la única operación del módulo que devuelve
bloqueos y advertencias por documentación (FR-019a).

**Los dos desplegables son obligatorios**: no hay asignación parcial, así que el botón no se habilita
con uno solo elegido y un viaje nunca queda con chofer y sin vehículo, ni al revés (FR-019b).

**Si el viaje ya está `en curso`**, la reasignación verifica además que la unidad nueva no esté en otro
viaje andando, con el mismo mensaje y el mismo número de viaje que el rechazo de *Poner en curso*
(FR-026a). Reasignar un viaje `pendiente` no verifica nada: un pendiente no ocupa a nadie.

- **Choferes ofrecidos**: activos. Ningún dado de baja, aunque tenga viajes históricos.
- **Vehículos ofrecidos**: activos y con estado operativo guardado `disponible`. Ninguno dado de baja
  ni fuera de servicio.
- **Toda la evaluación corre contra la fecha del viaje**, no contra hoy. La pantalla lo dice arriba:

> `La documentación se valida contra la fecha del viaje: {fecha}.`

**Cuando no hay nada para ofrecer**

| Situación | Texto |
|---|---|
| Sin choferes activos | `Todavía no hay choferes activos. Cargá al menos uno en el módulo de Choferes.` |
| Sin vehículos disponibles | `Todavía no hay vehículos disponibles. Revisá el módulo de Flota.` |

El viaje se puede registrar igual y queda `pendiente` sin asignar; lo que no puede es pasar a
`en curso` (FR-019, FR-025).

**Resultados**

| Situación | Texto | Efecto |
|---|---|---|
| Habilitado | `El viaje {número} quedó asignado a {chofer} con {patente}.` | se guarda |
| Documento por vencer (advertencia) | `Asignación guardada. Atención: {tipo} de {chofer o patente} vence el {fecha}.` | **se guarda** |
| Documento vencido (bloqueo) | `No podés asignar {chofer o patente}: {tipo} N° {número} está vencido al {fecha del viaje}.` | **no se guarda nada** |

La advertencia llega con el resultado porque reasignar es reversible mientras el viaje no esté rendido
ni anulado (FR-015a, FR-023).

---

## Poner en curso, rendir y anular

Tres acciones, cada una con su recurso propio (FR-034).

### Poner en curso

| Situación | Texto |
|---|---|
| Éxito | `El viaje {número} está en curso.` |
| Falta asignación | `Asigná chofer y vehículo antes de poner el viaje en curso.` |
| Unidad dada de baja | `{chofer o patente} está dado de baja. Reasigná el viaje antes de ponerlo en curso.` |
| Chofer ocupado | `{chofer} ya está en el viaje {número}. Cerralo antes de poner este en curso.` |
| Vehículo ocupado | `{patente} ya está en el viaje {número}. Cerralo antes de poner este en curso.` |

El rechazo **nombra el viaje que lo ocupa** (FR-026): saber que está ocupado sin saber por qué no
ayuda a resolverlo.

**Lo que acá no se revisa**: la documentación y el estado operativo del vehículo. Se controlaron al
asignar, y volver a mirarlos dejaría en tierra un viaje planificado con la unidad en regla (FR-025,
US4 esc. 11 y 15). Lo único que se exige del padrón es que las dos unidades sigan **activas**.

### Rendir

Con importe mayor a cero, rinde directo:

> `El viaje {número} quedó rendido.`

**Con importe en cero, no rinde al primer intento** (FR-038). Pide confirmación:

> **¿Rendir el viaje {número} sin importe?**
> El viaje va a quedar cerrado con importe $ 0,00. **Después no se va a poder corregir**: un viaje
> rendido no se edita, no se reasigna y no se anula.
> [Cancelar] [Rendir sin importe]

Cancelar deja el viaje `en curso` con su importe en cero, y se puede completar antes de volver a
rendirlo (US4 esc. 7).

### Anular

> **¿Anular el viaje {número}?**
> Deja de contar como trabajo realizado y su importe no figura en ningún total. El chofer y el
> vehículo quedan libres. Queda registrado con su motivo y no se puede volver atrás.
>
> Motivo *(obligatorio)*: [____________________]  (hasta 500 caracteres)
> [Cancelar] [Anular viaje]

**Sin motivo escrito, el botón de confirmar no se habilita** (FR-036, US6 esc. 2). Cancelar no
modifica nada: ni el estado, ni la asignación, ni el historial.

Éxito: `El viaje {número} quedó anulado.`

---

## Totales por período

Pantalla propia (FR-046). **Rango de fechas obligatorio**: mientras no haya rango elegido, no calcula
ni muestra nada.

> `Elegí un rango de fechas para ver los totales.`

Con rango elegido, **dos cuadros**:

| Por cliente | | |
|---|---|---|
| Cliente | Viajes | Importe |
| Distribuidora del Litoral | 8 | $ 1.240.000,00 |

| Por transportista | | |
|---|---|---|
| Transportista | Viajes | Importe |
| Transporte Sur | 5 | $ 780.000,00 |

- **La fecha de corte es la fecha del viaje** (FR-046a).
- **Los anulados no figuran en ninguna cantidad ni en ningún importe** (FR-047, SC-008).
- **Los viajes sin transportista asignado no aparecen en el segundo cuadro**: todavía no se sabe quién
  los va a hacer.
- Sin filas: `No hay viajes en el período elegido.`

---

## Códigos de error

El frontend decide con el **código** y muestra el **mensaje** tal cual, sin interpretarlo.

**La regla de códigos HTTP**: `400` cuando el problema está en lo que se tipeó; `409` cuando está en el
estado de algo que se comparte o que cambió (research §5).

### Clientes

| Código | HTTP | Mensaje |
|---|---|---|
| `datos_invalidos` | 400 | `Revisá los campos marcados.` |
| `no_encontrado` | 404 | `No encontramos lo que buscabas.` |
| `cuit_invalido` | 400 | `El CUIT tiene que tener once dígitos y un dígito verificador válido.` |
| `cuit_duplicado` | 400 | `Ese CUIT ya pertenece a otro cliente.` |
| `cuit_de_cliente_dado_de_baja` | 400 | `Ese CUIT pertenece a un cliente dado de baja. Dalo de alta de nuevo desde el listado en vez de registrarlo otra vez.` |
| `email_invalido` | 400 | `Escribí un email con formato válido.` |
| `cliente_con_viajes` | 400 | `No se puede dar de baja: {n} viaje(s) pendiente(s) o en curso dependen de este cliente.` |

`cliente_con_viajes` lleva además `cantidadViajes` en el cuerpo, no sólo en el texto (FR-006, SC-009,
precedente [004]). **Los viajes rendidos y anulados no cuentan**: un cliente que dejó de operar tiene
historial por definición, y prohibirle la baja por eso haría imposible justo el caso que la historia
de usuario pide.

### Viajes

| Código | HTTP | Mensaje |
|---|---|---|
| `datos_invalidos` | 400 | `Revisá los campos marcados.` |
| `no_encontrado` | 404 | `No encontramos lo que buscabas.` |
| `cliente_inexistente` | 400 | `Elegí un cliente activo.` |
| `remito_duplicado` | 400 | `Ese número de remito ya está cargado en el viaje {número}.` |
| `importe_negativo` | 400 | `El importe no puede ser negativo.` |
| `viaje_rendido_inmutable` | 409 | `El viaje {número} está rendido y no se puede modificar.` |
| `viaje_anulado_inmutable` | 409 | `El viaje {número} está anulado y no se puede modificar.` |
| `transicion_no_permitida` | 409 | `No se puede pasar el viaje {número} de {estado actual} a {estado pedido}.` |
| `falta_asignacion` | 409 | `Asigná chofer y vehículo antes de poner el viaje en curso.` |
| `unidad_dada_de_baja` | 409 | `{chofer o patente} está dado de baja. Reasigná el viaje antes de ponerlo en curso.` |
| `chofer_ocupado` | 409 | `{chofer} ya está en el viaje {número}. Cerralo antes de poner este en curso.` |
| `vehiculo_ocupado` | 409 | `{patente} ya está en el viaje {número}. Cerralo antes de poner este en curso.` |
| `rendicion_requiere_confirmacion` | 409 | `El viaje va a quedar cerrado sin importe y después no se va a poder corregir. Confirmá para rendirlo igual.` |
| `motivo_requerido` | 400 | `Escribí el motivo de la anulación.` |
| `rango_de_fechas_requerido` | 400 | `Elegí un rango de fechas para ver los totales.` |

### Asignación

| Código | HTTP | Mensaje |
|---|---|---|
| `chofer_inexistente` | 400 | `Elegí un chofer activo.` |
| `vehiculo_inexistente` | 400 | `Elegí un vehículo disponible.` |
| `documentacion_vencida` | 409 | `No podés asignar {unidad}: {tipo} N° {número} está vencido al {fecha del viaje}.` |
| `asignacion_no_permitida` | 409 | `El viaje {número} está {rendido/anulado} y no se puede reasignar.` |
| `fecha_bloquea_asignacion` | 409 | `No se puede mover el viaje al {fecha}: {tipo} de {unidad} está vencido a esa fecha. Cambiá la unidad o elegí otra fecha.` |

`documentacion_vencida` y `fecha_bloquea_asignacion` llevan además, en el cuerpo del error, qué unidad
y qué documento lo impiden, no sólo en el texto (FR-022, FR-022a, SC-004).

### Advertencias que no bloquean

Viajan en `advertencias[]` junto con el resultado, nunca como error (FR-015a).

| Código | Mensaje |
|---|---|
| `origen_igual_a_destino` | `El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien.` |
| `carga_retroactiva` | `Estás cargando un viaje con fecha anterior a hoy. Queda registrado como carga retroactiva.` |
| `documentacion_proxima_a_vencer` | `Asignación guardada. Atención: {tipo} de {unidad} vence el {fecha}.` |

---

## Formatos

| Dato | En el JSON | En pantalla |
|---|---|---|
| Fecha del viaje | `"2026-08-10"` (`yyyy-MM-dd`) | `10/08/2026`, con `formatearFecha` de `compartido/fechas` |
| Instante del historial | `"2026-08-10T12:14:03Z"` | `10/08/2026 09:14` (hora local), con `formatearInstante` |
| Importe | `1240000.00` (número) | `$ 1.240.000,00`, con `formatearPesos` de `compartido/moneda` |
| Estados | `"enCurso"` (camelCase) | `En curso` |

Las fechas **nunca** se formatean con `new Date(iso).toLocaleDateString()`: eso lee un `yyyy-MM-dd`
como medianoche UTC y en UTC−3 muestra el día anterior (convención [003]).

`compartido/moneda.ts` es **nuevo**: es el primer módulo del sistema que maneja dinero (research §11).
