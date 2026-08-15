# Contrato de interfaz: Gestión de facturación (Módulo 6)

Qué pantallas tiene el módulo, qué muestra cada una y **con qué palabras exactas**. Los textos de acá
se implementan tal cual: español rioplatense con voseo, moneda en pesos argentinos (Principio II). El
contrato HTTP está en [`facturacion-api.yaml`](./facturacion-api.yaml).

Cuatro reglas que atraviesan todo el módulo y no se repiten en cada pantalla:

- **Ningún estado se comunica sólo por color**, y todo elemento atenuado lleva además la palabra que
  lo explica (FR-065).
- **Ningún listado oculta filas en silencio**: si filtra por estado, el control dice cuál (FR-064).
- **Todo resultado que aparece sin que la pantalla cambie** —un guardado, una carga de archivo, un
  cambio de página, un cambio de estado— se anuncia con `role="status"` (FR-065, convención [003]).
- **Los importes se muestran con `compartido/moneda` y las fechas con `compartido/fechas`.** Nunca
  `toFixed(2)` ni `new Date(iso).toLocaleDateString()` (convenciones [003] y [005]).

---

## Pantallas

| Pantalla | Ruta | Permiso para ver | Permiso para operar |
|---|---|---|---|
| Empresa emisora | `/facturacion/empresa` | `facturacion.gestionar` | `facturacion.gestionar` |
| Listado de facturas | `/facturas` | `facturacion.consultar` | `facturacion.gestionar` |
| Alta de factura | `/facturas/nueva` | `facturacion.gestionar` | `facturacion.gestionar` |
| Ficha de factura | `/facturas/:id` | `facturacion.consultar` | `facturacion.gestionar` / `facturacion.anular` |
| Corrección de factura | `/facturas/:id/editar` | `facturacion.gestionar` | `facturacion.gestionar` |
| Panel de vencimientos | `/facturas/vencimientos` | `facturacion.consultar` | — |
| Totales facturados | `/facturas/totales` | `facturacion.consultar` | — |

**Menú**: cuatro entradas. *Facturas*, *Vencimientos* y *Totales facturados* van atadas a
`facturacion.consultar`, porque las tres se pueden mirar sin poder tocar nada. *Empresa emisora* va
atada a `facturacion.gestionar`, porque no es una pantalla de lectura para nadie. El servidor las
resuelve por permiso; el frontend dibuja lo que recibe.

La entrada se llama **`Totales facturados`** y no `Totales` a secas, porque el Módulo 5 ya tiene una
entrada `Totales` que apunta a `/viajes/totales`. Dos entradas con el mismo nombre en el mismo menú no
se distinguen.

**Quien tiene sólo `facturacion.consultar`** no ve el botón de emitir, ni el de corregir, ni el de
registrar el cobro, ni el de anular, ni en el listado ni en la ficha. **Quien tiene
`facturacion.gestionar` pero no `facturacion.anular`** ve todo lo anterior menos *Anular*. Y si
cualquiera de los dos invoca la acción a mano, recibe `403`: la restricción no vive sólo en la
pantalla (FR-068, SC-014).

---

## Empresa emisora

Una sola pantalla, un solo formulario, un solo guardado. No hay listado ni alta: la configuración es
única para todo el sistema (FR-001).

**Sin configurar**, arriba del formulario vacío:

> La empresa emisora todavía no está configurada. Completá al menos la razón social, el CUIT, el
> domicilio y la condición de IVA para poder emitir facturas.

| Campo | Obligatorio | Límite |
|---|---|---|
| Razón social | sí | 200 |
| CUIT | sí | 11 dígitos |
| Domicilio | sí | 200 |
| Condición de IVA | sí | 100 |
| Número de ingresos brutos | no | 50 |
| Inicio de actividades | no | fecha |
| Punto de venta | no | 4 dígitos |
| CBU | no | 22 dígitos |
| Teléfono | no | 50 |
| Email | no | 254 |

El CUIT se normaliza a sólo dígitos antes de validar: escribir `30-71234567-1` es válido y se guarda
como `30712345671` (FR-002, misma regla del Módulo 3).

> El ejemplo decía `30-71234567-8` hasta el recorrido del quickstart. **Ese número no pasa la
> validación**: el verificador que cierra para `3071234567` es `1`, no `8`. El texto del contrato no
> cambia —la regla es la misma— pero el ejemplo tenía que ser un CUIT válido de verdad, porque es el que
> alguien va a tipear al seguir el recorrido.

**Guardado exitoso** (no cambia de pantalla, se anuncia con `role="status"`):

> Los datos de la empresa emisora quedaron guardados.

**Mensajes de error**

| Situación | Texto |
|---|---|
| CUIT mal formado | `El CUIT tiene que tener once dígitos y un dígito verificador válido.` |
| Email mal formado | `Escribí un email con formato válido.` |
| Obligatorio vacío | `Completá {campo} para poder guardar.` |

### Logo

Zona propia dentro de la misma pantalla. **Es opcional**: sin logo las facturas se emiten igual
(FR-004).

- Sin logo: `Todavía no hay un logo cargado. Es opcional: las facturas se emiten igual.`
- Con logo: la imagen, su nombre, y los botones *Reemplazar* y *Quitar*.
- Ayuda del campo: `JPG o PNG, hasta 10 MB.`
- Rechazo: `Ese archivo no es una imagen JPG ni PNG. La configuración quedó sin cambios.`

**Quitar el logo no pide confirmación aparte**: no destruye nada que no se pueda volver a subir, y es
el mismo criterio con el que el Módulo 4 trató el alta de un vehículo (precedente [004]).

---

## Listado de facturas

**Columnas**: número · fecha · cliente · tipo de comprobante · período · importe total · estado ·
vencimiento de pago (FR-057).

**Filtros**: cliente · rango de fechas (desde / hasta) · período (mes y año) · estado · tipo de
comprobante. Todos combinables (FR-058).

**Paginación**: 20 filas, con el total de coincidencias. Orden: fecha descendente y, a igual fecha,
número de comprobante descendente (FR-059).

**El filtro de estado siempre dice qué está mostrando** (FR-064):

- sin filtro: `Mostrando todas las facturas, incluidas las anuladas.`
- con filtro: `Mostrando sólo las facturas {estado}.`

Sus cuatro valores son **excluyentes**: una factura impaga y pasada de fecha aparece bajo `Vencida` y
**no** bajo `Pendiente` (FR-058a, US3 esc. 11).

**Cómo se ve cada estado** — nunca sólo por color:

| Estado | Se muestra |
|---|---|
| `pendiente` | `Pendiente` |
| `vencida` | `Vencida` + `Venció hace {n} días` |
| `pagada` | `Pagada` + la fecha de cobro |
| `anulada` | `Anulada`, la fila atenuada **y** el motivo visible |

Un cliente dado de baja después de facturado se muestra con su razón social **congelada** y la palabra
`Inactivo` al lado (FR-011, US3 esc. 9).

**Estados vacíos**

| Situación | Texto |
|---|---|
| Sin facturas todavía | `Todavía no se emitió ninguna factura. Emití la primera para empezar a seguir la cobranza.` |
| Sin coincidencias | `Ninguna factura coincide con los filtros aplicados.` |

---

## Alta de factura

La pantalla central del módulo. Cuatro bloques en orden: **datos del comprobante**, **selección de
viajes**, **importes**, **vista previa**.

### Bloque 1 — Datos del comprobante

| Campo | Control | Obligatorio |
|---|---|---|
| Cliente | desplegable con razón social y CUIT, **sólo activos** | sí |
| Tipo de comprobante | desplegable: `Factura A` · `Factura B` · `Factura C` | sí |
| Tipo de facturación | desplegable: `Original` · `Refacturación` | sí |
| Factura que reemplaza | desplegable, **sólo con `Refacturación`** | sí en ese caso |
| Condición de venta | desplegable: `Contado` · `Cuenta Corriente` · `Tarjeta de Débito / Crédito` · `Cheque` | sí |
| Mes | desplegable: `01` … `12` | sí |
| Año | desplegable: `2025` · `2026` | sí |
| Fecha de facturación | fecha, **propuesta en hoy** | sí |
| Número de comprobante | texto `0000-00000000`, con el punto de venta propuesto | sí |
| CAE | texto | sí |
| Vencimiento del CAE | fecha | sí |
| Vencimiento de pago | fecha, **propuesta en fecha de facturación + 30 días** | sí |
| Detalle | texto largo, hasta 500 | no |

**El desplegable de factura reemplazada ofrece únicamente las anuladas de ese cliente que todavía
nadie refacturó** (FR-049, FR-049a). Con `Original` no aparece.

**Si no hay clientes activos** el alta no se puede completar:

> No hay clientes activos en el padrón. Registrá o reactivá un cliente en el Módulo de viajes para
> poder emitirle una factura.

**Si la empresa emisora está incompleta**, el rechazo llega del servidor al guardar y nombra los
datos:

> Falta configurar la empresa emisora: {razón social, CUIT, domicilio}. Cargalos en *Empresa
> emisora* para poder emitir.

### Bloque 2 — Selección de viajes

Se carga al elegir cliente, mes y año. Muestra de cada viaje: **número · fecha · remito · origen ·
destino · importe**, con una casilla para incluirlo (FR-019).

Un viaje **sin remito** aparece igual, con la casilla deshabilitada y la leyenda al lado (FR-019a):

> Sin remito — no se puede facturar

**Sin viajes facturables**, en lugar de una lista vacía (FR-021):

> No hay viajes facturables de {cliente} en {mes} de {año}. Se ofrecen sólo los viajes rendidos, sin
> facturar, cuya fecha cae en ese período.

### Bloque 3 — Importes

Se actualiza en cada cambio de la selección y en cada cambio del tipo de comprobante (FR-020, FR-025):

> **{n} viajes seleccionados** · Neto `$ 82.644,63` · IVA (21%) `$ 17.355,37` · Total `$ 100.000,00`

**Los tres son de sólo lectura y no hay ningún campo donde escribirlos** (FR-024). Con `Factura C` el
IVA muestra `(0%)` y `$ 0,00`, y el total es igual al neto: **no es un error ni una factura
incompleta** (FR-023).

### Bloque 4 — Vista previa

Botón *Ver vista previa*. Abre el **documento tal como va a quedar**, generado por el servidor, en un
marco dentro de la pantalla (FR-033).

> Así va a salir la factura. Revisala antes de confirmar: una vez emitida, el cliente, los viajes y
> los importes no se pueden cambiar.

**No es una maqueta dibujada en la pantalla**: es el mismo PDF que se va a guardar, pedido a
`POST /api/facturas/vista-previa`, mostrado sobre una URL de `Blob`. Pedirla no crea la factura ni
guarda ningún archivo; abandonar la pantalla no deja rastro (US2 esc. 33).

### Confirmaciones previas del servidor (FR-032)

Las dos llegan como `409` **sin haber creado nada**. Se reintenta con `confirmado: true`.

> **Un viaje incluido no tiene importe**
> El viaje N° {número} tiene importe `$ 0,00` y no suma al neto. Una vez emitida, la factura no
> cambia de importes: sólo se corrige anulándola.
> [Cancelar] [Emitir igual]

> **La fecha de la factura es anterior a la de un viaje**
> El viaje N° {número} es del {fecha}, posterior a la fecha de facturación {fecha}. Suele indicar un
> error de carga de fechas.
> [Cancelar] [Emitir igual]

### Después de emitir (FR-014)

**El formulario no queda en pantalla.** Se navega a la ficha de la factura recién creada, y ahí se
anuncia con `role="status"`:

> Se emitió la factura {número}. Sus {n} viajes quedaron en estado facturado.

### Rechazos del alta

| Código | Texto |
|---|---|
| `numero_duplicado` | `El número {número} ya lo usa la factura del {fecha} de {cliente}. Cargá otro número.` |
| `viaje_ya_facturado` | `El viaje N° {número} ya fue facturado en el comprobante {número}. Actualizá la lista y volvé a intentar.` |
| `viaje_sin_remito` | `El viaje N° {número} no tiene número de remito y el remito sale impreso en el detalle. No se puede facturar.` |
| `cliente_sin_domicilio` | `A {cliente} le falta el domicilio, que sale impreso en la factura. Cargalo en el padrón de clientes del Módulo de viajes y volvé a intentar.` |
| `cliente_inactivo` | `{cliente} está dado de baja en el padrón. Dalo de alta de nuevo para poder facturarle.` |
| `anulada_ya_reemplazada` | `La factura {número} ya fue reemplazada por la Refacturación {número}. Elegí otra.` |
| `refacturacion_sin_reemplazada` | `Elegí qué factura anulada reemplaza esta Refacturación.` |
| `vencimiento_pago_anterior` | `El vencimiento de pago no puede ser anterior a la fecha de facturación.` |
| `cae_vencimiento_anterior` | `El vencimiento del CAE no puede ser anterior a la fecha de facturación.` |
| `sin_viajes_seleccionados` | `Elegí al menos un viaje para facturar.` |
| `numero_invalido` | `El número tiene que tener el formato 0000-00000000.` |

---

## Ficha de factura

**Muestra** (FR-060): datos del emisor **tal como quedaron al emitirla** · datos del cliente
congelados · tipo de comprobante · tipo de facturación · condición de venta · período · fecha ·
detalle · la lista de viajes incluidos con su importe · neto, IVA y total · CAE con su vencimiento ·
vencimiento de pago · fecha de cobro cuando corresponda · motivo de anulación cuando corresponda · la
referencia de refacturación cuando corresponda · el acceso al documento · el historial completo.

**Aviso permanente arriba de los datos del emisor y del cliente** (FR-034, FR-034a):

> Estos datos son los que tenía la factura el día que se emitió. Un cambio posterior en la
> configuración o en el padrón no la modifica.

**Documento**: botón *Ver el documento*. Se abre **en línea**, sin bajarlo y abrirlo a mano
(FR-031a). Debajo:

> Este documento es la representación impresa de la factura, no el comprobante fiscal. La validez la
> da el CAE, que se obtiene en AFIP/ARCA por fuera del sistema.

**Referencias de refacturación** (FR-050), en las dos fichas:

- en la Refacturación: `Reemplaza a la factura {número} del {fecha}, anulada.`
- en la anulada: `Reemplazada por la Refacturación {número} del {fecha}.`

**Historial** (FR-045, FR-037): una fila por entrada, de la más vieja a la más nueva.

| Estado anterior | Estado nuevo | Usuario | Fecha y hora |
|---|---|---|---|
| — | Pendiente | jlopez | 12/08/2026 10:14 |
| Pendiente | Pagada | jlopez | 20/09/2026 09:02 |
| *Corrección de datos* | | mgarcia | 21/09/2026 11:30 |

Una entrada de corrección se lee `Corrección de datos` y no lleva estado anterior ni nuevo: el sistema
registra **quién y cuándo**, y no qué campos cambiaron (FR-037).

**Acciones**, según estado y permiso:

| Estado | Acciones |
|---|---|
| `pendiente` / `vencida` | *Corregir datos* · *Registrar cobro* · *Anular* |
| `pagada` | *Corregir datos* |
| `anulada` | ninguna |

**No existe ninguna acción para revertir un cobro, para devolver una anulada a `pendiente` ni para
editarla** (FR-043, FR-038). No están ocultas: no existen.

---

## Corrección de factura

Sólo cuatro campos editables: **detalle · CAE · vencimiento del CAE · vencimiento de pago** (FR-035).

El resto se muestra **de sólo lectura**, con el aviso:

> El cliente, los viajes y los importes de una factura emitida no se modifican. Si están mal, la
> factura se anula y se emite una Refacturación.

**Guardado exitoso**:

> Se guardaron los cambios y se regeneró el documento de la factura.

| Situación | Texto |
|---|---|
| CAE vacío | `Una factura emitida no puede quedarse sin CAE.` |
| Vencimiento del CAE vacío | `Una factura emitida no puede quedarse sin vencimiento del CAE.` |
| Factura anulada | `Una factura anulada no se puede corregir.` |

**Corregir una factura `pagada` está permitido** y no le toca ni el estado ni la fecha de cobro
(FR-035, US4 esc. 8).

---

## Registrar el cobro

Formulario chico dentro de la ficha, con un solo campo: **fecha de cobro**, propuesta en hoy.

> **Registrar el cobro de la factura {número}**
> La factura queda en estado Pagada. Es un paso que no se revierte: el sistema no ofrece ninguna
> acción para volver atrás un cobro.
> [Cancelar] [Registrar cobro]

| Situación | Texto |
|---|---|
| Fecha anterior a la de facturación | `La fecha de cobro no puede ser anterior a la fecha de facturación.` |

---

## Anular una factura

Requiere el permiso `facturacion.anular` (FR-067). Motivo escrito obligatorio **y** confirmación: sin
motivo, el botón no se habilita (FR-046).

> **¿Anular la factura {número}?**
> Sus {n} viajes vuelven a estado rendido y quedan disponibles para facturar de nuevo. La factura
> queda anulada y su documento se regenera indicando el motivo. No se puede deshacer.
>
> Motivo de la anulación *(obligatorio, hasta 500 caracteres)*
> [Cancelar] [Anular factura]

Cancelar no modifica nada (FR-046, US6 esc. 3).

**Rechazo de anular una factura cobrada** (FR-043a) — sin ofrecer ni sugerir revertir el cobro:

> La factura {número} está cobrada desde el {fecha} y no se puede anular.

---

## Panel de vencimientos

Las facturas `vencida` y las que vencen dentro de los próximos **7 días corridos** (FR-063).

**Columnas**: cliente · número · importe · vencimiento · situación.

La situación va **con la palabra**, no sólo con color:

- `Vencida hace {n} días`
- `Vence en {n} días`
- `Vence hoy`

**Vacío**: `No hay facturas vencidas ni por vencer en los próximos 7 días.`

---

## Totales facturados

**El rango de fechas es obligatorio.** Sin elegirlo, el sistema no calcula ni muestra nada (FR-061):

> Elegí un rango de fechas para ver los totales.

**Columnas**: cliente · cantidad de facturas · facturado · cobrado · pendiente de cobro.

Debajo del cuadro:

> Las facturas anuladas no suman en ninguna columna. La fecha de corte es la fecha de facturación.

**Vacío**: `No hay facturas emitidas entre el {desde} y el {hasta}.`

---

## Cambios en las pantallas del Módulo 5

Los seis cambios de FR-051 a FR-055a, y ninguno más (FR-056). En el frontend son tres.

### Listado y ficha de viajes

- El estado `Facturado` se agrega a la columna de estado y al filtro de estado.
- Un viaje facturado muestra, en la ficha y en la fila, **el número y la fecha de su factura**
  (FR-055): `Facturado en {número}, del {fecha}`.
- Un viaje `facturado` **no ofrece ninguna acción de escritura**: ni editar, ni asignar, ni cambiar de
  estado, ni anular. Igual que `rendido` (FR-052).

### Rendición de un viaje

El número de remito pasa a ser **obligatorio para rendir** (FR-055a). Sigue siendo opcional en
`pendiente` y en `en curso`.

| Situación | Texto |
|---|---|
| Rendir sin remito | `Cargá el número de remito antes de rendir el viaje: sale impreso en el detalle de la factura.` |

**Limitación conocida y aceptada**: los viajes que ya estaban `rendido` sin remito antes de esta regla
no se pueden facturar, y no hay forma de corregirlos, porque un viaje rendido no admite edición en
ninguna versión del sistema (FR-019a).

---

## Endpoints del Módulo 5 que cambian

Tres, y ninguno cambia de forma ni de contrato: cambian de comportamiento o agregan un campo.

| Endpoint | Cambio |
|---|---|
| `POST /api/viajes/{id}/rendicion` | rechaza con `400 remito_requerido` si el viaje no tiene remito (FR-055a) |
| `GET /api/viajes` | la fila incorpora `factura: { numero, fecha } \| null` y el filtro de estado acepta `facturado` (FR-055) |
| `GET /api/viajes/{id}` | la ficha incorpora `factura: { id, numero, fecha } \| null` (FR-055) |

Ningún endpoint del Módulo 5 se borra, se renombra ni cambia de método.
