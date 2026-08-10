# Contratos: Gestionar choferes y su documentación (Módulo 3)

Dos contratos, uno por cada frontera del módulo:

- **[choferes-api.yaml](./choferes-api.yaml)** — contrato HTTP entre el frontend y el backend
  (OpenAPI 3.0).
- **Este archivo** — contrato de interfaz: qué pantallas hay, qué se ve en cada una y qué texto
  exacto lee el usuario.

Ambos se apoyan en lo que fijaron los Módulos 1 y 2 y no lo redefinen: el cuerpo de error
(`{ codigo, mensaje, campo? }`), el comportamiento ante sesión vencida, el menú calculado en el
servidor, el diálogo de confirmación de baja y el piso de accesibilidad.

---

## Acceso al módulo

Las diez pantallas exigen sesión activa y el permiso `choferes.gestionar`, que otorgan los roles
**Tráfico** y **Administrador del sistema** (FR-027).

Es el primer módulo cuyo acceso no es exclusivo del administrador. Un usuario de Tráfico ve las
entradas de este módulo y **ninguna** del Módulo 2; el administrador ve las de los dos. Eso sale del
`CatalogoOpcionesMenu` del servidor, sin lógica de permisos en el frontend.

Se agregan tres entradas al menú, las tres atadas a `choferes.gestionar`:

| Entrada | Ruta |
|---|---|
| *Choferes* | `/choferes` |
| *Transportistas* | `/transportistas` |
| *Tipos de documentación* | `/tipos-documentacion` |

El panel de vencimientos (`/choferes/vencimientos`) no lleva entrada propia: se llega desde el
listado de choferes, porque es una vista sobre los mismos datos y no un módulo aparte.

---

## Pantallas

### Listado de transportistas — `/transportistas`

| Elemento | Comportamiento |
|---|---|
| Columnas | Nombre o razón social, CUIT, tipo de persona, teléfono, email, estado y cantidad de choferes activos |
| Búsqueda | Un campo de texto que busca por nombre o CUIT, parcial y sin distinguir mayúsculas |
| Padrón vacío | Mensaje explícito de que todavía no hay transportistas cargados (FR-023) |
| Acciones por fila | *Editar* y *Dar de baja* |
| Botón *Nuevo transportista* | Lleva al formulario de alta |

### Formulario de transportista — `/transportistas/nuevo` y `/transportistas/{id}/editar`

*Nombre o razón social*, *CUIT*, *Tipo de persona* (física o jurídica), *Teléfono* y *Email*, todos
obligatorios.

El CUIT se puede escribir con guiones o puntos: el sistema lo normaliza a sólo dígitos antes de
validar, así que `20-12345678-3` y `20123456783` son el mismo transportista (FR-025). Se valida el
dígito verificador, no sólo el largo.

### Listado de choferes — `/choferes`

| Elemento | Comportamiento |
|---|---|
| Columnas | Apellido y nombre, DNI, transportista, estado del chofer y estado general de su documentación (FR-022) |
| Estado de documentación | Cuatro valores: `En regla`, `Próxima a vencer`, `Vencida` y **`Sin documentación`**, que es distinto de estar en regla (FR-028, FR-029). Sale del peor estado entre los documentos vigentes de cada tipo |
| Filtros | *Apellido* y *DNI* son campos de texto con coincidencia parcial. *Transportista*, *Estado* y *Estado de documentación* son listas desplegables de selección exacta. Los cinco se combinan |
| Estado por defecto | Al entrar, el listado muestra **sólo los choferes activos**. El filtro *Estado* arranca en `Activo`, a la vista y modificable: quien quiera ver los dados de baja elige `Inactivo` (FR-022) |
| Paginación | 20 filas por página, con el total de coincidencias y la navegación entre páginas. Cambiar cualquier filtro vuelve a la página 1 (FR-030) |
| Sin resultados | Mensaje explícito en vez de una tabla vacía (FR-023) |
| Acciones por fila | *Ver ficha*, *Editar* y *Dar de baja* |
| Enlace *Ver vencimientos* | Lleva al panel |

El filtro de estado **arranca con un valor puesto, no vacío**. Es deliberado: un listado que oculta
choferes sin decirlo se lee como un error de datos. Con `Activo` visible en el control, quien opera
ve por qué no está el chofer que dio de baja ayer, y lo encuentra cambiando el filtro.

### Ficha de un chofer — `/choferes/{id}`

| Elemento | Contenido |
|---|---|
| Datos personales | Apellido, nombre, DNI, CUIL, fecha de nacimiento, teléfono y email |
| Transportista | Nombre, con enlace a su ficha |
| Documentación | Una fila por documento: tipo, número, emisión, vencimiento, **estado calculado** y el archivo si lo tiene. Agrupada por tipo, con el vigente primero y sus renovaciones anteriores debajo |
| Documento histórico | El que fue reemplazado por una renovación se muestra atenuado y marcado como *Reemplazado*, con el estado en gris: sigue visible, pero no es el que cuenta (FR-020a) |
| Acciones por documento | *Abrir archivo* (si lo tiene), *Corregir* y *Eliminar* (FR-015b, FR-015c). *Abrir archivo* abre el escaneo **en una pestaña nueva y se ve ahí**, no se descarga: lo decide el backend con `Content-Disposition: inline`, no el enlace |
| Chofer sin documentos | Mensaje explícito de que no tiene documentación cargada, no una tabla vacía (caso límite) |
| Documento sin archivo | Se muestra como *Sin respaldo*, distinto de uno con adjunto (caso límite) |
| Acciones | *Cargar documento*, *Editar chofer* y *Dar de baja*. Si el chofer está inactivo, en lugar de *Dar de baja* aparece **Reactivar** (FR-005b) |

**El estado de cada documento no es editable por ninguna vía** (FR-018): no hay lista desplegable, ni
casilla, ni forma de forzarlo. Se muestra y nada más.

### Formulario de chofer — `/choferes/nuevo` y `/choferes/{id}/editar`

*Nombre*, *Apellido*, *DNI*, *CUIL*, *Fecha de nacimiento*, *Teléfono*, *Email* y *Transportista*,
todos obligatorios.

| Situación | Comportamiento |
|---|---|
| El DNI ya está en el padrón | El sistema **reutiliza esa persona** y sólo agrega los datos de chofer. La pantalla avisa que se está reutilizando una persona ya cargada |
| Esa persona ya es chofer | Se rechaza como duplicado |
| Sin transportistas activos | El formulario no se puede completar: se informa que primero hay que registrar un transportista, con enlace a esa pantalla (US2 esc. 4) |
| Menos de 18 años | Se rechaza al guardar (FR-011) |

### Cargar documento — desde la ficha del chofer

*Tipo de documentación* (sólo los activos del catálogo), *Número*, *Fecha de emisión*, *Fecha de
vencimiento* y *Archivo adjunto* **opcional**.

**No hay campo de estado**, a propósito: lo calcula el sistema (FR-018). El formulario tampoco lo
muestra como valor previsto, para no dar a entender que se puede elegir.

El campo de archivo dice **antes de elegir nada** qué se acepta: `PDF, JPG o PNG, hasta 10 MB`
(FR-015a). Si el archivo no cumple, se marca ese campo y **no se guarda el documento**: nada de
guardar el registro y perder el adjunto en silencio.

Si el chofer ya tiene un documento de ese tipo, el formulario avisa que se está cargando una
renovación y que el anterior va a quedar como historial (FR-020a) — no lo impide ni pide confirmar
nada, sólo lo dice.

Si el archivo no llega a guardarse, **el documento no se crea**: el sistema lo informa y el
formulario conserva todo lo tipeado para reintentar sin volver a completarlo (FR-015e).

### Corregir documento — desde la ficha del chofer

El mismo formulario que el alta, con los datos cargados y las mismas validaciones (FR-015b). Dos
diferencias:

| Elemento | Comportamiento |
|---|---|
| Archivo adjunto | Muestra el que ya tiene, con la opción de reemplazarlo. Si no se elige uno nuevo, el actual se conserva |
| Aviso de recálculo | Si se cambia la fecha de vencimiento, el formulario avisa que eso puede cambiar cuál es el documento vigente de ese tipo y el estado del chofer (FR-020a) |

Si el archivo de reemplazo no llega a guardarse, el documento **queda como estaba**, con su adjunto
anterior intacto (FR-015e).

### Eliminar documento — desde la ficha del chofer

Acción por fila en la lista de documentos. Pide confirmación explícita y **advierte que no se puede
deshacer**, porque a diferencia del resto del módulo acá el borrado es definitivo (FR-015c, FR-015d).

Al confirmar, el documento y su archivo desaparecen de la ficha. Si el eliminado era el vigente de su
tipo, el anterior vuelve a mandar y el estado del chofer cambia solo (FR-020a).

Si el catálogo de tipos está vacío, se informa que primero hay que cargar un tipo, con enlace a esa
pantalla.

### Panel de vencimientos — `/choferes/vencimientos`

Una fila por documento en problemas, con el chofer, su transportista, el documento y **cuántos días
faltan o cuántos pasaron**. Ordenado por urgencia: primero lo vencido hace más tiempo.

Sólo entran **choferes activos** y **documentos vigentes de su tipo**: un chofer dado de baja no
alerta aunque tenga todo vencido, y una licencia vieja ya renovada tampoco (FR-021, FR-020a).

Si no hay ninguno, se informa explícitamente que no hay vencimientos pendientes (US5 esc. 4) — no se
muestra una tabla vacía.

Cada fila lleva a la ficha del chofer con su documentación visible (US5 esc. 2).

### Listado y formulario de tipos de documentación — `/tipos-documentacion`

*Nombre* y *Días de aviso de vencimiento* (entero mayor o igual a cero).

El listado muestra cuántos documentos usa cada tipo, que es lo que explica por qué algunos no se
pueden dar de baja (FR-014).

---

## Confirmaciones

Las bajas piden confirmación explícita antes de ejecutar nada, y cancelar no modifica nada (FR-026).
Se usa el mismo diálogo del Módulo 2, con su manejo de foco y `Escape`.

| Acción | Texto de la confirmación |
|---|---|
| Dar de baja un chofer | `¿Confirmás la baja de {apellido}, {nombre}? Va a quedar inactivo y no va a poder asignarse a un viaje. Su documentación se conserva.` |
| Dar de baja un transportista | `¿Confirmás la baja de {nombre}? Va a dejar de ofrecerse al registrar o reasignar choferes.` |
| Dar de baja un tipo de documentación | `¿Confirmás la baja de {nombre}? Va a dejar de ofrecerse al cargar documentación.` |
| Eliminar un documento | `¿Confirmás que querés eliminar el {tipo} N° {numero}? Se borra junto con su archivo adjunto y no se puede deshacer.` |
| Reactivar un chofer | `¿Confirmás la reactivación de {apellido}, {nombre}? Va a volver al listado y su documentación va a contar de nuevo.` |

La confirmación de eliminar un documento es la única que habla de borrar y no de dar de baja: es la
única operación del módulo que no se puede revertir (FR-015d), y el texto tiene que decirlo.

---

## Textos de la interfaz

Todos en español rioplatense, con voseo (Principio II). Los devuelve el backend en `mensaje` y el
frontend los muestra tal cual.

### Confirmaciones de operación

| Situación | Texto |
|---|---|
| Transportista registrado | `El transportista {nombre} se registró correctamente.` |
| Chofer registrado | `El chofer {apellido}, {nombre} se registró correctamente.` |
| Chofer registrado reutilizando una persona del padrón | `El chofer {apellido}, {nombre} se registró correctamente, reutilizando la persona que ya estaba en el padrón.` |
| Documento cargado | `El documento se cargó correctamente.` |
| Cambios guardados | `Los cambios se guardaron correctamente.` |
| Chofer reasignado | `{apellido}, {nombre} ahora pertenece a {transportista}. Su documentación se conservó.` |
| Chofer dado de baja | `{apellido}, {nombre} quedó inactivo.` |
| Transportista dado de baja | `{nombre} quedó inactivo.` |
| Tipo dado de baja | `{nombre} quedó inactivo.` |

### Errores

| Código | Situación | Texto |
|---|---|---|
| `datos_invalidos` | Algún campo con formato inválido | `Revisá los campos marcados en rojo.` |
| `cuit_duplicado` | El CUIT ya está en el padrón | `Ese CUIT ya está registrado para otro transportista.` |
| `cuil_duplicado` | El CUIL ya está en el padrón | `Ese CUIL ya está registrado para otro chofer.` |
| `dni_duplicado` | La persona de ese DNI ya es chofer | `Esa persona ya está registrada como chofer.` |
| `transportista_inexistente` | El transportista elegido no existe o está inactivo | `El transportista seleccionado ya no está disponible. Actualizá la lista y volvé a elegir.` |
| `transportista_con_choferes` | Se intentó dar de baja con choferes activos | `No se puede dar de baja: tiene {cantidad} chofer(es) activo(s). Reasignalos o dalos de baja primero.` |
| `menor_de_edad` | Fecha de nacimiento de menos de 18 años | `Un chofer tiene que ser mayor de 18 años.` |
| `vencimiento_anterior_a_emision` | Fechas incoherentes | `La fecha de vencimiento tiene que ser posterior a la de emisión.` |
| `tipo_duplicado` | El nombre ya está en el catálogo | `Ya existe un tipo de documentación con ese nombre.` |
| `tipo_inexistente` | El tipo elegido no existe o está inactivo | `El tipo de documentación seleccionado ya no está disponible. Actualizá la lista y volvé a elegir.` |
| `tipo_con_documentos` | Se intentó dar de baja un tipo en uso | `No se puede dar de baja: hay {cantidad} documento(s) de ese tipo cargados.` |
| `archivo_no_admitido` | Formato o tamaño no aceptado | `El archivo tiene que ser un PDF, JPG o PNG de hasta 10 MB.` |
| `archivo_no_guardado` | El archivo es válido pero el sistema no pudo guardarlo | `No pudimos guardar el archivo, así que no se guardó nada. Volvé a intentar; los datos que cargaste se conservan.` |
| `no_encontrado` | El registro ya no existe | `Ese registro ya no existe. Puede que lo hayan eliminado desde otra sesión.` |

Los mensajes de sesión vencida, falta de permiso, error inesperado y falta de conexión son los que
ya fijó el Módulo 1 y no cambian.

### Estados vacíos

| Pantalla | Texto |
|---|---|
| Padrón de transportistas sin cargar | `Todavía no hay transportistas cargados. Registrá el primero para poder asignarle choferes.` |
| Búsqueda de transportistas sin coincidencias | `No hay transportistas que coincidan con la búsqueda.` |
| Listado de choferes sin cargar | `Todavía no hay choferes registrados.` |
| Listado de choferes sin coincidencias | `No hay choferes que coincidan con los filtros aplicados.` |
| Chofer sin documentación cargada | `Este chofer todavía no tiene documentación cargada.` |
| Panel de vencimientos sin alertas | `No hay documentación próxima a vencer ni vencida.` |
| Catálogo de tipos sin cargar | `Todavía no hay tipos de documentación. Cargá el primero para poder registrar documentos.` |
| Selector de transportista en el formulario de chofer, sin activos | `No hay transportistas activos. Registrá uno desde la pantalla Transportistas.` |
| Selector de tipo al cargar un documento, sin activos | `No hay tipos de documentación activos. Cargá uno desde la pantalla Tipos de documentación.` |

### Estados de documentación

Son **dos escalas distintas**, y se llaman distinto a propósito: una describe un papel, la otra a una
persona.

**Estado de un documento** (`DocumentacionEstado`):

| Valor | Texto en pantalla |
|---|---|
| `vigente` | `Al día` |
| `proximaAvencer` | `Próxima a vencer` |
| `vencida` | `Vencida` |

**Estado general del chofer** (`EstadoDocumentacionChofer`, FR-029):

| Valor | Texto en pantalla |
|---|---|
| `enRegla` | `En regla` |
| `proximaAvencer` | `Próxima a vencer` |
| `vencida` | `Vencida` |
| `sinDocumentacion` | `Sin documentación` |

En el panel y en la ficha se acompaña con el plazo: `Vence en {n} días` o `Venció hace {n} días`.

Un documento reemplazado por una renovación se muestra con su estado real —una licencia vieja sigue
diciendo `Vencida`— pero atenuado y con la marca *Reemplazado*, para que se entienda por qué el
chofer figura `En regla` con un documento vencido a la vista (FR-020a).

---

## Lo que nunca aparece en pantalla

1. **Un control para elegir o editar el estado de un documento**, por ninguna vía (FR-018, SC-004).
2. **Un chofer sin documentos mostrado como al día**: siempre se distingue *Sin documentación*
   (FR-028).
3. **Un enlace directo al archivo adjunto sin sesión**: los adjuntos se sirven por un endpoint
   autorizado, nunca como contenido estático (FR-024, research §3).
4. **Transportistas o tipos inactivos entre las opciones** al registrar un chofer o cargar un
   documento.
5. **Un chofer alertado por un documento que ya renovó**, ni un chofer inactivo en el panel de
   vencimientos (FR-020a, FR-021).
6. **Un listado sin indicar qué estado está filtrando**: si oculta a los inactivos, el control lo
   muestra (FR-022).

---

## Accesibilidad

Rigen las mismas condiciones que fijó el Módulo 1 y que quedaron como piso de todo el sistema.
Dos puntos propios de este módulo:

- **El estado de documentación no se comunica sólo por color.** Un semáforo rojo/amarillo/verde deja
  afuera a quien no distingue esos colores, y acá el color es la información principal de la
  pantalla: siempre va acompañado del texto (`En regla` / `Al día`, `Próxima a vencer`, `Vencida`,
  `Sin documentación`).
- **Un documento *Reemplazado* tampoco se distingue sólo por estar atenuado**: lleva la palabra, no
  nada más el gris.
- **La carga de archivo** tiene su etiqueta asociada, informa los formatos y el tamaño admitidos
  antes de intentar la subida, y anuncia el resultado con `role="status"`.
- **La paginación** anuncia el cambio de página con `role="status"` (`Página 2 de 4, mostrando 20 de
  73 choferes`), para que no sea sólo un cambio visual de la tabla.
