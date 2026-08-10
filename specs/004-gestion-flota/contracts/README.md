# Contratos: Gestión de flota (Módulo 4)

**Feature**: `004-gestion-flota` | **Fecha**: 2026-08-08

Dos contratos:

- **`flota-api.yaml`** — el contrato HTTP (OpenAPI 3.0): rutas, cuerpos, códigos de error.
- **este archivo** — el contrato de interfaz: qué pantallas hay, qué muestra cada una y **con qué
  palabras exactas**. Los textos de acá son los que la implementación tiene que usar, y son los que
  `quickstart.md` verifica.

Todo en español rioplatense (Principio II de la constitución). Este módulo no maneja montos.

---

## Acceso al módulo

| Permiso | Lo otorgan | Habilita |
|---|---|---|
| `flota.gestionar` | Tráfico, Administrador del sistema | Vehículos, su documentación, el panel de vencimientos y la descarga de adjuntos |
| `flota.tipos.gestionar` | Administrador del sistema | El ABM del catálogo de tipos de vehículo |

Opciones de menú que agrega el módulo:

| Etiqueta | Ruta | Permiso |
|---|---|---|
| Flota | `/flota` | `flota.gestionar` |
| Tipos de vehículo | `/tipos-vehiculo` | `flota.tipos.gestionar` |

Un usuario de Tráfico ve **Flota** y no ve **Tipos de vehículo**. Un usuario sin ninguno de los dos
permisos no ve ninguna de las dos, y si escribe la URL a mano recibe el mismo error de siempre: la
autorización se evalúa en el servidor, no en el menú.

**Los tipos de documentación de ámbito vehículo se administran desde la pantalla que ya existe**
(`/tipos-documentacion`, permiso `choferes.gestionar`). Este módulo no agrega una pantalla de tipos de
documentación: la existente suma el ámbito.

**La descarga de un adjunto exige el mismo permiso que el resto**, aunque sea una lectura de archivo:
conocer la ruta no puede alcanzar para verlo (FR-038, SC-011).

---

## Pantallas

### Listado de flota — `/flota`

Columnas: **Patente**, **Marca**, **Modelo**, **Tipo**, **Transportista**, **Estado**, **Documentación**.

- **Estado** muestra el estado operativo **derivado**, no el guardado: una unidad guardada como
  disponible cuyo seguro venció figura como `Fuera de servicio` (FR-014).
- **Documentación** muestra el estado general con su etiqueta escrita, nunca sólo con un color
  (convención [003]).

Cuatro filtros, los cuatro por selección exacta entre lo ya cargado (FR-030):

| Filtro | Opciones |
|---|---|
| Transportista | Los transportistas activos del padrón, más "Todos" |
| Tipo de vehículo | Los tipos activos del catálogo, más "Todos" |
| Estado del vehículo | "Todos", "Disponible", "Fuera de servicio", "Dado de baja" |
| Estado de documentación | "Todos", "En regla", "Próxima a vencer", "Vencida", "Sin documentación" |

- Sin filtros, el listado muestra **sólo los vehículos activos** —disponibles y fuera de servicio—.
  Los dados de baja aparecen eligiendo "Dado de baja" (FR-031).
- **El control siempre dice qué está filtrando**: ninguna fila queda oculta en silencio (FR-037).
- Paginación de 20 filas, con el total de coincidencias y la forma de avanzar (FR-032).
- Cada fila lleva a la ficha del vehículo.

### Ficha de un vehículo — `/flota/{id}`

Muestra patente, marca, modelo, tipo, transportista, estado operativo **derivado** y el estado general
de documentación (FR-038).

Debajo, la lista completa de documentos **agrupada por tipo** y, dentro de cada tipo, por vencimiento
descendente: el vigente arriba y sus renovaciones anteriores debajo. De cada uno: tipo, número, fecha
de emisión, fecha de vencimiento, estado calculado, si es el vigente de su tipo, y el enlace al
archivo cuando lo tiene.

- Los documentos que no son el vigente de su tipo se muestran atenuados **y con la palabra
  "Histórico"**: un elemento atenuado lleva siempre la palabra que lo explica (convención [003]).
- Un documento sin archivo se muestra con la leyenda **"Sin archivo adjunto"**, no con un enlace roto
  ni con un espacio en blanco (FR-016a).
- Acciones: **Editar**, **Dar de baja** (o **Reactivar**, si está dado de baja), **Agregar documento**,
  y por cada documento **Corregir** y **Eliminar**.
- **Abrir archivo** abre el escaneo **en una pestaña nueva y se ve ahí**, no se descarga para abrirlo
  después. Lo decide el backend con `Content-Disposition: inline`, no el enlace, así que la acción se
  comporta igual desde cualquier pantalla.

### Formulario de vehículo — `/flota/nuevo` y `/flota/{id}/editar`

Campos: patente, marca, modelo, tipo de vehículo, transportista, estado operativo.

- El tipo y el transportista son **selectores de lo ya cargado y activo**, obligatorios los dos.
- La patente se valida en el navegador por formato antes de enviar y **se normaliza al guardar**:
  quien escribe `ab 123 cd` termina con `AB123CD` guardado (FR-003).
- **En el alta, el estado operativo sólo admite "Fuera de servicio"**, y el formulario lo explica:
  *"Una unidad sin documentación cargada no puede quedar disponible. Cargá su documentación desde la
  ficha y después cambiá el estado."* (FR-013, US2 esc. 8).
- En la edición, "Disponible" se puede elegir siempre que la documentación no esté vencida ni falte;
  si lo está, el sistema lo rechaza nombrando el documento que lo impide (FR-014a).

### Cargar y corregir un documento — desde la ficha del vehículo

Campos: tipo de documentación, número, fecha de emisión, fecha de vencimiento, archivo adjunto.

- El selector de tipo ofrece **únicamente los tipos activos de ámbito vehículo**. Los de chofer no
  aparecen (FR-017a).
- **No hay campo de estado, ni editable ni de sólo lectura editable**: el sistema lo calcula y se
  muestra recién después de guardar (FR-021, SC-004).
- **El archivo es opcional** (FR-016a). El texto del campo lo dice: *"Archivo (opcional)"*, con la
  aclaración *"PDF, JPG o PNG, hasta 10 MB"*.
- Al corregir, dejar el campo de archivo vacío **conserva** el archivo que el documento ya tenía; para
  reemplazarlo se elige uno nuevo. **Al reemplazarlo con éxito, el archivo anterior se borra**: un
  escaneo que ya no corresponde deja de existir en vez de quedar guardado por las dudas (CHK023).
- Si el archivo nuevo no se puede guardar, **el documento no queda modificado ni pierde el que tenía**,
  y el formulario conserva lo tipeado para reintentar (FR-029).

### Panel de vencimientos de flota — `/flota/vencimientos`

Una fila por documento `proximaAvencer` o `vencida` de un vehículo **activo**, ordenadas por urgencia:
primero lo vencido hace más tiempo.

De cada fila: patente, transportista, tipo de documento, número, fecha de vencimiento y **"Vence en N
días"** o **"Venció hace N días"** (FR-035).

- Cada fila lleva a la ficha del vehículo con la documentación visible (US5 esc. 2).
- **No aparecen vehículos dados de baja**, ni documentos ya reemplazados por una renovación.
- Todo vehículo excluido del filtro "disponible" por documentación vencida o ausente **figura acá**
  (FR-015, SC-006).

### Listado y formulario de tipos de vehículo — `/tipos-vehiculo`

Campos: nombre y estado. Sólo para el Administrador del sistema.

- Con el catálogo vacío se muestra el mensaje de padrón vacío, no una tabla sin explicación (US1
  esc. 1, FR-036).
- La baja pide confirmación y se rechaza si el tipo tiene vehículos asociados, diciendo cuántos son.
- **Un tipo inactivo se da de alta desde su edición**: la fila no ofrece ninguna acción de estado, y
  al editarlo el formulario dice que está inactivo y suma el botón **Dar de alta** (US1 esc. 6,
  FR-009). Es una acción propia y no un campo: guardar el nombre no cambia el estado. No pide
  confirmación aparte —no destruye nada y se deshace con la baja, que sí la pide—.

### Tipos de documentación — `/tipos-documentacion` (pantalla existente, MODIFICADA)

Suma un campo **Ámbito**, obligatorio, con dos opciones: **"Chofer"** y **"Vehículo"** (FR-017).

- El listado muestra el ámbito de cada tipo y permite filtrar por él.
- El ámbito **se puede corregir mientras el tipo no tenga ningún documento cargado**; con documentos
  asociados el sistema lo rechaza diciendo cuántos son, contando los de chofer y los de vehículo
  (FR-017d).
- Los tipos que ya existían quedaron con ámbito **Chofer** (FR-017c).

---

## Confirmaciones

Tres operaciones piden confirmación explícita antes de ejecutarse, y **cancelar no cambia nada**
(FR-007, FR-008e, FR-027, SC-009):

| Operación | Texto de la confirmación |
|---|---|
| Dar de baja un vehículo | *"¿Dar de baja la unidad {patente}? Va a dejar de figurar en el listado y en el panel de vencimientos. Su documentación se conserva y podés reactivarla más adelante."* |
| Reactivar un vehículo | *"¿Reactivar la unidad {patente}? Vuelve al listado y al panel de vencimientos con toda su documentación."* |
| Eliminar un documento | *"¿Eliminar este documento? Se borra junto con su archivo adjunto y **esta acción no se puede deshacer**."* |

La eliminación de un documento es la única que advierte que no se puede deshacer, porque es la única
que borra de verdad: la baja de un vehículo es lógica y se revierte reactivándolo (FR-028).

---

## Textos de la interfaz

### Confirmaciones de operación

| Situación | Texto |
|---|---|
| Vehículo registrado | "La unidad {patente} quedó registrada en la flota." |
| Vehículo modificado | "Los datos de la unidad {patente} quedaron actualizados." |
| Vehículo reasignado | "La unidad {patente} ahora pertenece a {transportista}." |
| Vehículo dado de baja | "La unidad {patente} quedó dada de baja. Su documentación se conserva." |
| Vehículo reactivado | "La unidad {patente} volvió a la flota." |
| Documento cargado | "El documento quedó cargado." |
| Documento cargado sin archivo | "El documento quedó cargado. Todavía no tiene archivo adjunto; podés agregarlo más adelante." |
| Documento corregido | "El documento quedó actualizado." |
| Documento eliminado | "El documento y su archivo se eliminaron." |
| Tipo de vehículo creado | "El tipo {nombre} quedó disponible para registrar vehículos." |
| Tipo de vehículo dado de baja | "El tipo {nombre} quedó inactivo. Deja de ofrecerse al registrar vehículos." |
| Tipo de vehículo dado de alta | "El tipo {nombre} volvió a estar activo. Se ofrece de nuevo al registrar vehículos." |

### Errores

| Código | Texto | Cuándo |
|---|---|---|
| `patente_duplicada` | "Esa patente ya está registrada en la flota." | FR-002. La comparación es sobre la patente normalizada, así que `ab 123 cd` choca con `AB123CD` |
| `patente_de_vehiculo_dado_de_baja` | "Esa patente pertenece a una unidad dada de baja. Reactivala desde su ficha en vez de registrarla de nuevo." | FR-008f |
| `patente_invalida` | "La patente tiene que tener el formato ABC123 o AB123CD." | FR-004 |
| `tipo_vehiculo_inexistente` | "Elegí un tipo de vehículo activo." | FR-005 |
| `transportista_inexistente` | "Elegí un transportista activo." | FR-008a |
| `sin_tipos_de_vehiculo` | "Todavía no hay ningún tipo de vehículo cargado. Pedile al administrador que cargue al menos uno antes de registrar unidades." | US2 esc. 6 |
| `sin_transportistas` | "Todavía no hay ningún transportista cargado. Registrá al menos uno antes de registrar unidades." | US2 esc. 7 |
| `disponible_con_documentacion_vencida` | "No podés dejar la unidad disponible: {documento} está vencido." | FR-014a |
| `disponible_sin_documentacion` | "No podés dejar la unidad disponible: todavía no tiene documentación cargada." | FR-013, FR-014a |
| `tipo_vehiculo_en_uso` | "No se puede dar de baja: {n} vehículo(s) usan este tipo." | FR-010 |
| `transportista_con_dependencias` | "No se puede dar de baja: {n} chofer(es) y {m} vehículo(s) activos dependen de este transportista." | FR-008d |
| `tipo_documentacion_en_uso` | "No se puede dar de baja: {n} documento(s) usan este tipo." | FR-017b |
| `ambito_no_modificable` | "No se puede cambiar el ámbito: {n} documento(s) ya usan este tipo." | FR-017d |
| `vencimiento_anterior_a_emision` | "La fecha de vencimiento tiene que ser posterior a la de emisión." | FR-018 |
| `archivo_no_admitido` | "El archivo tiene que ser PDF, JPG o PNG y pesar menos de 10 MB." | FR-025 |
| `archivo_no_guardado` | "No se pudo guardar el archivo. El documento no se modificó; volvé a intentar." | FR-029 |
| `transportista_inactivo_al_reactivar` | "El transportista de esta unidad está dado de baja. Elegí uno activo para reactivarla." | FR-008e |
| `tipo_inactivo_al_reactivar` | "El tipo de esta unidad está dado de baja. Elegí uno activo para reactivarla." | FR-008e |
| `nombre_duplicado` | "Ya existe un tipo con ese nombre." | FR-009 |
| `no_encontrado` | "No encontramos lo que buscabas." | — |
| `datos_invalidos` | "Revisá los campos marcados." | Con el campo puntual señalado |

Los mensajes de baja rechazada **dicen el número** porque saber que hay dependencias sin saber cuántas
no ayuda a resolverlo (SC-008).

### Estados vacíos

Nunca una tabla vacía sin explicación (FR-036):

| Pantalla | Texto |
|---|---|
| Flota sin ningún vehículo | "Todavía no hay unidades registradas. Registrá la primera para empezar." |
| Flota con filtros sin coincidencias | "Ningún vehículo coincide con los filtros aplicados." |
| Ficha sin documentos | "Esta unidad todavía no tiene documentación cargada. Mientras no la tenga, no puede quedar disponible." |
| Panel de vencimientos vacío | "No hay vencimientos pendientes." |
| Catálogo de tipos de vehículo vacío | "Todavía no hay tipos de vehículo cargados. Cargá el primero para poder registrar unidades." |

### Estados: etiquetas de pantalla y valores del JSON

**Estado operativo del vehículo** (`VehiculoEstado`, y también el derivado que se muestra):

| JSON | Pantalla |
|---|---|
| `disponible` | Disponible |
| `fueraDeServicio` | Fuera de servicio |

**Filtro de estado del vehículo** (FR-030a) — suma un tercer valor que no es operativo:

| JSON | Pantalla |
|---|---|
| `disponible` | Disponible |
| `fueraDeServicio` | Fuera de servicio |
| `dadoDeBaja` | Dado de baja |

**Estado general de documentación del vehículo** (`EstadoDocumentacionVehiculo`):

| JSON | Pantalla |
|---|---|
| `enRegla` | En regla |
| `proximaAvencer` | Próxima a vencer |
| `vencida` | Vencida |
| `sinDocumentacion` | Sin documentación |

**Estado de un documento** (`DocumentacionEstado`, igual que en el Módulo 3):

| JSON | Pantalla |
|---|---|
| `vigente` | Vigente |
| `proximaAvencer` | Próxima a vencer |
| `vencida` | Vencida |

**Ámbito de un tipo de documentación** (`DocumentacionAmbito`):

| JSON | Pantalla |
|---|---|
| `chofer` | Chofer |
| `vehiculo` | Vehículo |

Dos escalas distintas con nombres distintos a propósito: un **documento** está `vigente`, un
**vehículo** está `enRegla`. Es la misma separación que el Módulo 3 hizo entre el documento y el
chofer, y evita que un mismo término signifique dos cosas según dónde aparezca.

Todas las fechas se muestran con `date-fns` desde `compartido/fechas`, nunca con
`new Date(iso).toLocaleDateString()`: eso interpreta un `yyyy-MM-dd` como medianoche UTC y en UTC−3
muestra el día anterior (convención [003]).

---

## Lo que nunca aparece en pantalla

- **Un campo de estado de documento editable o elegible**, en ningún formulario ni en ninguna pantalla
  (FR-021, SC-004).
- **Un enlace directo al archivo en disco.** El adjunto se sirve por un endpoint que exige sesión y
  permiso; la ruta interna nunca sale al cliente (FR-038, SC-011).
- **Un vehículo con documentación vencida o ausente dentro del resultado del filtro "disponible"**
  (FR-015, SC-006).
- **Un vehículo dado de baja en el panel de vencimientos** (FR-035).
- **Una fila oculta sin que el control lo diga** (FR-037).
- **Un estado comunicado sólo por color**, ni un elemento atenuado sin la palabra que lo explica
  (convención [003]).

---

## Accesibilidad

- Cada estado lleva su texto además de su color, y cada elemento atenuado lleva la palabra que explica
  por qué lo está.
- Los mensajes de error se asocian al campo que los provoca, para que un lector de pantalla los
  anuncie junto con él.
- Los diálogos de confirmación reciben el foco al abrirse y lo devuelven al cerrarse.
