# Research: Gestión de flota (Módulo 4)

**Feature**: `004-gestion-flota` | **Fecha**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

Este documento registra las decisiones técnicas del módulo y —sobre todo— las alternativas que se
descartaron y por qué. Cada sección se referencia desde [plan.md](./plan.md).

El Módulo 4 es el primero que **se apoya sobre otro módulo de negocio ya construido** en vez de sobre
la infraestructura común: reutiliza el `Transportista`, el catálogo `DocumentacionTipo`, el almacén de
archivos y la regla de vencimientos del Módulo 3. La mayor parte de la investigación consistió en
decidir **qué se comparte, qué se copia y qué se toca**, porque la spec acota explícitamente los
cambios al Módulo 3 a dos (ver Assumptions de la spec).

---

## 1. Los documentos del vehículo van a una tabla propia, no a `Documentaciones`

**Decisión**: se crea la tabla `DocumentacionesVehiculo` con la entidad
`GT.Domain.Flota.DocumentacionVehiculo`. La tabla `Documentaciones` del Módulo 3 **no se toca**: sigue
siendo la documentación de choferes, con `ChoferId` obligatorio.

**Rationale**: la alternativa natural era una sola tabla con dos dueños posibles —`ChoferId` y
`VehiculoId` anulables más una restricción que exija exactamente uno—. Se descartó por tres razones,
en orden de peso:

1. **La spec acota los cambios al Módulo 3 a exactamente dos** (el ámbito en `DocumentacionTipo` y la
   baja de transportista). Compartir la tabla obligaría a un tercero, un cuarto y un quinto: volver
   `Documentaciones.ChoferId` anulable, mover la entidad `Documentacion` fuera del namespace
   `GT.Domain.Choferes`, y reescribir `CalculadorEstadoChofer` para que ignore las filas ajenas.
2. **Se perdería una garantía real por una hipotética.** Hoy `ChoferId NOT NULL` significa que
   ninguna fila de documentación puede quedar sin dueño, y lo garantiza la base. Con dos columnas
   anulables esa garantía pasa a depender de una restricción `CHECK` escrita a mano en la migración,
   que EF Core no expresa y ningún test de dominio ejercita.
3. **La ganancia era menor de lo que parece.** Lo que se comparte de verdad no es la tabla: es la
   *regla* de vencimientos y el *almacén* de archivos, y las dos se reutilizan tal cual sin compartir
   filas (§2 y §3).

**Lo que se duplica, y cuánto es**: la orquestación de carga, corrección y eliminación de documentos
—cuatro casos de uso en `GT.Application/Flota/Documentacion/`— y la elección del documento vigente de
cada tipo, expresada dos veces: en C# (`CalculadorEstadoVehiculo`) y en SQL (`RepositorioVehiculos`).
Son unas 250 líneas de lógica muy parecida a la del Módulo 3.

**Cómo se controla esa duplicación**, en vez de fingir que no existe:

- El orden entre disco y base ya es una **convención escrita** del proyecto (`CLAUDE.md`, [003]): el
  archivo se escribe antes de confirmar la fila y se borra después. El módulo la sigue, no la
  reinventa.
- La equivalencia entre la regla en C# y la consulta en SQL lleva un test que compara las dos sobre
  el mismo dato, tal como fija la convención [003] de `CLAUDE.md`.

**Alternativas consideradas**:

| Alternativa | Por qué se descartó |
|---|---|
| Tabla única con `ChoferId`/`VehiculoId` anulables + `CHECK` | Tres cambios extra al Módulo 3 que la spec no autoriza, y una garantía `NOT NULL` real cambiada por una restricción escrita a mano |
| Extraer un coordinador de adjuntos genérico y usarlo desde los dos módulos | Es la solución correcta a largo plazo, pero exige reescribir los cuatro casos de uso del Módulo 3. Queda anotado como candidato para una spec futura, no se hace acá |
| Herencia de EF Core (`Documentacion` base, dos derivadas) | Misma tabla o tabla por tipo, con los mismos problemas y además una jerarquía que el dominio no pide. El Módulo 3 ya descartó la herencia por motivos parecidos |

---

## 2. Lo que se reutiliza del Módulo 3 **sin tocar una línea**

Esta es la parte que hace que el módulo sea chico. Todo lo siguiente se consume tal como está:

| Pieza | Dónde vive hoy | Por qué sirve sin cambios |
|---|---|---|
| `CalculadorEstadoDocumento` | `GT.Domain/Choferes/` | Recibe fecha de vencimiento, días de aviso y "hoy": tres primitivos. No sabe de choferes |
| `DocumentacionEstado` | `GT.Domain/Choferes/` | Los tres valores del documento (`vigente`, `proximaAvencer`, `vencida`) son los mismos de FR-019 |
| `FechaHoyArgentina` | `GT.Domain/Choferes/` | FR-020 pide exactamente la misma definición de "hoy" |
| `IAlmacenDeArchivos` + `AlmacenDeArchivos` | `GT.Application/Choferes/Documentacion/` y `GT.Infrastructure/Archivos/` | Guarda un stream y devuelve una ruta generada por el sistema. No sabe a qué entidad pertenece |
| `IValidadorDeArchivo` + `ValidadorArchivo` | ídem | PDF/JPG/PNG y 10 MB, por firma: exactamente FR-025 |
| `Transportista` y su ABM | `GT.Domain/Choferes/`, `GT.Application/Choferes/Transportistas/` | FR-008b prohíbe crear un padrón paralelo |
| `PaginaDe<T>` | `GT.Application/Choferes/` | La forma `{ items, total, pagina, tamanioPagina }` ya es convención del proyecto |
| `PoliticasAutorizacion`, `ErrorResponse` | `GT.Api/` | Infraestructura común desde el Módulo 1 |

**Consecuencia incómoda que conviene decir en voz alta**: el módulo de flota va a escribir
`using GT.Application.Choferes.Documentacion;` para llegar al almacén de archivos, y
`using GT.Domain.Choferes;` para llegar al calculador de estado. Esos tipos ya no son "de choferes":
son de documentación, y su carpeta quedó vieja. **No se mueven en esta feature**: una relocación de
namespace toca una decena de archivos del Módulo 3 sin cambiar una sola conducta, y la spec acotó los
cambios a ese módulo. Queda anotado como candidato para una spec futura.

Lo que **sí** se duplica en el dominio, y es barato:

- `EstadoDocumentacionVehiculo`: enum de cuatro valores, calcado de `EstadoDocumentacionChofer`. Son
  20 líneas y ninguna lógica. Compartirlo exigiría renombrarlo, que es un cambio al Módulo 3.
- `CalculadorEstadoVehiculo.VigentesDeCadaTipo`: diez líneas, misma regla de FR-024 que ya usa el
  chofer (el de vencimiento más lejano; con empate, el de `Id` mayor).

---

## 3. `DocumentacionTipo` gana un ámbito: el único cambio de forma al Módulo 3

**Decisión**: `DocumentacionTipos` suma la columna `Ambito` (`NOT NULL`, enum
`DocumentacionAmbito : byte { Chofer = 1, Vehiculo = 2 }`). La migración da valor `Chofer` a todas las
filas existentes (FR-017c). El enum se declara junto a `DocumentacionTipo`, en `GT.Domain/Choferes/`.

**Rationale**: es lo que decidió la clarificación del 2026-08-08. Un catálogo por módulo duplicaría el
ABM, la regla de días de aviso y la pantalla; y con dos catálogos, "cuántos documentos usan este tipo"
—la pregunta que bloquea la baja— se responde distinto en cada uno.

**Tres consecuencias que hay que aceptar explícitamente**:

1. **El nombre del tipo sigue siendo único en todo el catálogo, no por ámbito.** El índice único sobre
   `Nombre` no lleva filtro hoy y no se le agrega uno: cambiarlo sería un cambio extra al Módulo 3 y
   la spec pide "nombre único" sin calificarlo. Precio concreto: no pueden convivir un "Seguro" de
   chofer y un "Seguro" de vehículo. Con los tipos que la spec enumera —licencia, LiNTI, psicofísico,
   ART de un lado; VTV, seguro, RUTA, cédula verde del otro— no hay colisión. Si aparece, se resuelve
   con el nombre ("Seguro del vehículo"), no con el esquema.
2. **`ContarDocumentosAsync` pasa a sumar las dos tablas** (FR-017b, FR-017d). Es el único método del
   Módulo 3 cuyo resultado cambia, y cambia hacia el lado seguro: bloquea más bajas, nunca menos.
3. **El ABM de tipos sigue viviendo en el Módulo 3**, bajo el permiso `choferes.gestionar`. La
   pantalla suma el selector de ámbito y el filtro de la lista; no se crea una pantalla de tipos en
   flota. Es lo que pide FR-017 ("no duplicarse el ABM").

**Corregir el ámbito (FR-017d)**: se permite mientras el tipo no tenga ningún documento —de ninguno de
los dos lados—. Con documentos asociados se rechaza informando cuántos son. La razón es concreta: si
un tipo con documentos de vehículo pasa a ámbito chofer, esos documentos quedan colgando de un tipo
que su propio módulo ya no ofrece, y su formulario de corrección no podría volver a elegirlo.

---

## 4. El estado operativo se guarda, pero el que manda se deriva

**Decisión**: `Vehiculos.EstadoOperativo` guarda lo que eligió el operador (`Disponible` o
`FueraDeServicio`, FR-012). El estado que el listado muestra y por el que filtra se calcula al leer:

```
estadoOperativoDerivado =
    (estadoDocumentacion es Vencida o SinDocumentacion)
        ? FueraDeServicio
        : estadoOperativoGuardado
```

**Rationale**: es literalmente FR-014, y repite la decisión [003] ya adoptada en el proyecto: los
estados derivables se calculan al leer y nunca se guardan en columna. Sobrescribir la columna al
vencer un seguro exigiría un proceso nocturno que la mantenga al día —y, peor, uno que la *revierta*
al renovar el documento—. Derivándola, la unidad vuelve a estar disponible sola, sin que nadie edite
nada (SC-010, US4 esc. 11).

**Por qué la columna guardada no sobra**, aunque el valor mostrado se derive: distingue "fuera de
servicio porque está en el taller" de "fuera de servicio porque le venció el seguro". Sin ella, al
renovar el seguro de una unidad rota el sistema la marcaría disponible.

**FR-014a no es lo mismo que FR-014, y conviene no confundirlos**:

- **FR-014a es una validación de formulario**: al guardar, se rechaza `disponible` si la documentación
  está `vencida` o falta, y se explica cuál documento lo impide. Es para que el operador entienda por
  qué no puede elegir lo que quiere.
- **FR-014 es la derivación al consultar**: cubre el paso del tiempo, que ningún formulario puede
  atrapar —el seguro que vence de un día para el otro sin que nadie abra la pantalla—.

**Consecuencia en el alta**: un vehículo recién registrado no tiene documentos, así que su estado
general es `sinDocumentacion` y FR-014a rechaza `disponible`. **El formulario de alta sólo admite
`fuera de servicio`**, y lo dice antes de que el operador intente lo otro. Es exactamente US2 esc. 8;
se declara acá porque no es evidente al leer FR-012.

---

## 5. Un solo filtro de estado con tres valores, resuelto en la base

**Decisión**: el filtro `estado` del listado toma `disponible`, `fueraDeServicio` o `dadoDeBaja`
(FR-030a). Se traduce a la consulta así, con `vencidos`/`vigentes` contados como subconsulta
correlacionada igual que en el listado de choferes:

| Valor del filtro | Predicado |
|---|---|
| *omitido* | `Activo` (FR-031) |
| `dadoDeBaja` | `!Activo` |
| `disponible` | `Activo && EstadoOperativo == Disponible && vigentes > 0 && vencidos == 0` |
| `fueraDeServicio` | `Activo && (EstadoOperativo == FueraDeServicio \|\| vigentes == 0 \|\| vencidos > 0)` |

**Rationale**: un solo control con tres valores excluyentes es lo que decidió la clarificación. Las
combinaciones que se pierden —"dados de baja que además estaban disponibles"— no tienen sentido
operativo: un vehículo fuera de la flota no está disponible para nada.

Que las dos últimas filas sean complementarias dentro de `Activo` no es casual: **todo vehículo activo
cae en exactamente una de las dos**, y por eso `disponible` nunca puede devolver una unidad con
documentación vencida o ausente (FR-015, SC-006). El predicado de `disponible` lo garantiza en la
misma consulta, no en un chequeo posterior.

**Alternativa descartada**: dos filtros separados (operativo + activo/inactivo). Deja al operador
armar combinaciones vacías por construcción y suma un quinto control a un listado que ya tiene cuatro.

---

## 6. La patente: normalizar antes de comparar, validar el formato argentino

**Decisión**: dos reglas puras nuevas en `GT.Domain/Flota/`.

- `NormalizadorPatente.Normalizar`: pasa a mayúsculas y descarta todo lo que no sea letra o dígito.
  `ab 123 cd`, `AB-123-CD` y `AB123CD` quedan en `AB123CD` (FR-003).
- `ValidadorPatente.EsValida`: acepta el formato viejo (`^[A-Z]{3}[0-9]{3}$`) y el Mercosur
  (`^[A-Z]{2}[0-9]{3}[A-Z]{2}$`), sobre el valor **ya normalizado** (FR-004).

**Rationale**: el orden importa. Si se validara antes de normalizar, `AB-123-CD` sería rechazada por
formato en vez de aceptada como la patente que es. Y si se comparara antes de normalizar, `ab 123 cd`
y `AB123CD` convivirían como dos unidades distintas, que es justo el caso límite de la spec.

**No se reutiliza `NormalizadorDocumentoNumerico`** del Módulo 3: descarta las letras, que en una
patente son la mitad del dato.

**La unicidad se garantiza con un índice único sobre `Patente`, sin filtro por `Activo`** (FR-002).
Sin índice, dos altas simultáneas de la misma patente pasan las dos: la consulta previa cierra la
ventana normal, el índice cierra la carrera. Es la convención [003] del proyecto y acá se repite.

**FR-008f cae solo de ese índice.** Al detectar la patente ocupada, el alta mira si el vehículo dueño
está activo y responde distinto:

- dueño activo → `patente_duplicada`, "Esa patente ya está registrada en la flota."
- dueño inactivo → `patente_de_vehiculo_dado_de_baja`, con el mensaje que pide reactivar la unidad
  existente en vez de crear una nueva.

Sin esa distinción, quien intenta recargar una unidad que volvió recibe "ya está registrada" y no
encuentra dónde: el vehículo no aparece en el listado por defecto.

---

## 7. Dos permisos, no uno: el catálogo de tipos es sólo del administrador

**Decisión**: el módulo agrega dos permisos.

| Código | Lo otorgan | Cubre |
|---|---|---|
| `flota.gestionar` | Tráfico, Administrador del sistema | Vehículos, su documentación y el panel de vencimientos |
| `flota.tipos.gestionar` | Administrador del sistema | ABM del catálogo de tipos de vehículo |

**Rationale**: FR-039 pide exactamente eso y es la primera vez que la spec distingue niveles de acceso
*dentro* de un módulo. El Módulo 3 tiene un permiso único justamente porque su spec no distinguía.
Resolverlo con un permiso solo obligaría a chequear el rol en el endpoint, y la convención del
Módulo 1 es que la autorización se evalúa **por permiso, nunca por rol** (`PermisoHandler`).

**Consecuencia práctica**: Tráfico ve la entrada "Flota" en el menú pero no "Tipos de vehículo", y el
`CatalogoOpcionesMenu` ya resuelve eso sin código nuevo —cada entrada declara su permiso—.

**Nota sobre los tipos de documentación de ámbito vehículo**: los administra la pantalla del Módulo 3,
bajo `choferes.gestionar` (§3). Tráfico tiene ese permiso, así que puede cargarlos. No se le pone
`flota.tipos.gestionar`: ese permiso es del catálogo de **tipos de vehículo**, que es otra cosa.

---

## 8. La baja de un transportista pasa a mirar también su flota

**Decisión**: `DarDeBajaTransportista` rechaza la baja si el transportista tiene al menos un chofer
activo **o** al menos un vehículo activo, e informa las dos cantidades (FR-008d). El DTO
`TransportistaConChoferesActivos` pasa a `TransportistaConDependenciasActivas`, con los dos números.

**Rationale**: sin esto, un vehículo activo puede quedar apuntando a un transportista inactivo, que es
exactamente el estado que FR-008a prohíbe crear desde el alta. La regla del Módulo 3 no lo contemplaba
porque la flota no existía.

**Lo que técnicamente lo habilita**: `Transportista` suma la navegación inversa
`ICollection<Vehiculo> Vehiculos`. `Vehiculo` vive en `GT.Domain/Flota/` y `Transportista` en
`GT.Domain/Choferes/`, pero los dos están en el mismo ensamblado, así que la navegación compila sin
mover nada. El conteo viaja en la misma consulta que ya cuenta los choferes: una fila con dos números,
no dos colecciones traídas a memoria.

**Asimetría deliberada con el catálogo de tipos**, que conviene no "corregir" por prolijidad:

- **Transportista**: se rechaza por dependientes **activos**. Un transportista cuyos vehículos ya
  están todos de baja puede darse de baja (edge case explícito de la spec).
- **Tipo de vehículo** (FR-010) y **tipo de documentación** (FR-017b): se rechazan por dependientes
  **cualesquiera**, activos o no.

La diferencia tiene sentido: dar de baja un transportista no rompe nada de lo ya guardado, mientras
que un vehículo dado de baja **sigue mostrando su tipo** (FR-011) y un documento histórico sigue
necesitando los días de aviso de su tipo para calcular su estado. Es el mismo criterio que el
Módulo 3 ya aplicó entre `Transportista` y `DocumentacionTipo`.

---

## 9. Paginación y orden total del listado de flota

**Decisión**: 20 filas por página, filtros aplicados antes de paginar, total de coincidencias en la
respuesta, forma `{ items, total, pagina, tamanioPagina }` (FR-032). Orden: `Patente, Id`.

**Rationale**: es la convención [003] del proyecto y `PaginaDe<T>` ya existe. El `Id` final es
redundante acá —la patente es única, así que ordenar por ella ya es un orden total— y se agrega igual
por dos razones: la convención lo pide sin excepciones, y cuesta nada. El caso que la convención
previene (dos homónimos intercambiándose entre páginas) no puede darse con patentes, pero un orden
que depende de una restricción de unicidad para ser total es frágil ante cualquier cambio futuro.

---

## 10. Panel de vencimientos de flota: separado del de choferes

**Decisión**: `GET /api/flota/vencimientos` y la pantalla `/flota/vencimientos`, distintos de los del
Módulo 3. Reglas idénticas (FR-035): sólo vehículos **activos**, sólo el documento vigente de cada
tipo, ordenado por urgencia (`FechaVencimiento`, y `Id` para desempatar).

**Rationale**: unificar los dos paneles en una vista sola sería más lindo y es **alcance fantasma**: la
spec no lo pide, y las dos vistas responden preguntas distintas —quién no puede manejar y qué unidad
no puede salir—. Queda anotado como candidata para una spec futura.

**Los vehículos dados de baja no aparecen**, cualquiera sea el estado de sus papeles: ya no forman
parte de la flota operativa y nadie va a renovarlos. Al reactivar la unidad vuelve a alertar sola, sin
recargar nada, porque el estado se calcula al consultar (FR-008e, edge case de la spec).

---

## 11. Reactivar un vehículo cuyo transportista o tipo se dio de baja

**Decisión**: `POST /api/flota/vehiculos/{id}/reactivacion` acepta un cuerpo **opcional** con
`transportistaId` y `tipoVehiculoId`. Si los que el vehículo ya tiene siguen activos, el cuerpo no
hace falta. Si alguno está inactivo y no vino un reemplazo activo, se rechaza indicando cuál falta
(FR-008e, US6 esc. 11).

**Rationale**: la reactivación tiene que dejar la unidad en un estado que el alta también aceptaría; si
no, quedaría un vehículo activo apuntando a un transportista inactivo, que es lo que FR-008a prohíbe.
Pedir los dos datos siempre sería molesto para el caso normal —que es la unidad que vuelve con todo en
orden—, y por eso el cuerpo es opcional.

**Alternativa descartada**: reactivar igual y dejar que el operador corrija después desde la edición.
Deja el sistema pasar por un estado inválido, aunque sea por un rato, y ese estado es justo el que la
spec declara imposible ("un vehículo nunca queda apuntando a un transportista inactivo").

---

## 12. Lo que se decide acá porque la spec no lo dice, y no la contradice

Cuatro decisiones de diseño, no requisitos nuevos:

1. **Desempate del "documento más reciente"**: con dos documentos del mismo tipo y la misma fecha de
   vencimiento, manda el de `Id` mayor. Sin criterio, la consulta devolvería uno u otro según el plan
   de ejecución y el listado cambiaría solo entre dos consultas idénticas. Es la misma decisión que ya
   tomó el Módulo 3.
2. **Prefijo `/api/flota/` para los documentos de vehículo**: el Módulo 3 ocupó
   `/api/documentacion/{id}` con los del chofer. Sin prefijo, dos entidades distintas compartirían
   espacio de identificadores.
3. **Marca y modelo se guardan con `Trim`**, hasta 50 caracteres cada uno (FR-006). No se normalizan
   más que eso: no son claves y no se comparan.
4. **El listado no busca por texto.** La spec lo deja explícitamente afuera (Assumptions). Los cuatro
   filtros son selección exacta entre lo ya cargado (FR-030).

---

## 13. Riesgos y cómo se cubren

| Riesgo | Cómo se cubre |
|---|---|
| La regla de vencimientos queda escrita en C# y en SQL, y las dos se separan con el tiempo | Test que compara dominio contra consulta sobre el mismo dato, convención [003] de `CLAUDE.md` |
| La migración del ámbito rompe los tipos ya cargados del Módulo 3 | La columna se crea con valor por defecto `Chofer` en la misma sentencia; test de integración que verifica que los documentos de chofer existentes siguen calculando igual (FR-017c) |
| El filtro `disponible` deja pasar una unidad con papeles vencidos | El predicado lo resuelve en la consulta (§5); test de integración sobre SC-006, que exige 0% |
| El archivo se escribe y la fila no, o al revés | Convención [003]: archivo primero, fila después, borrado compensatorio. Test con un almacén que falla a propósito (FR-029) |
| Dos altas simultáneas de la misma patente | Índice único en la base, traducido a error de aplicación por el repositorio (§6) |
