# Research: Gestionar choferes y su documentación (Módulo 3)

Fase 0 del plan. Cada decisión resuelve una incógnita técnica del `Technical Context` y se evalúa
contra el Principio I de la constitución (ante dos soluciones, la más simple).

Este módulo se apoya sobre dos módulos ya entregados, así que la primera pregunta de cada punto sigue
siendo **¿esto ya está resuelto?** — y en tres casos la respuesta es que sí (§7).

---

## 1. Chofer sobre Persona: composición, no herencia

**Decisión**: `Choferes` es una tabla propia con una clave foránea **única** a `Personas`. El chofer
no hereda de `Persona` en el modelo de EF Core: la referencia.

**Rationale**: la herencia es el modelo conceptualmente más fiel —un chofer *es* una persona— y con
TPH ni siquiera agregaría una tabla, porque `Personas.Tipo` ya podría servir de discriminador. Pero
choca de frente con un caso límite que la spec ya declara:

> "Se intenta registrar como chofer a alguien que ya está en el padrón **como empleado**: el sistema
> **reutiliza esa persona** en vez de crear un duplicado."

Eso exige **cambiar el tipo de una fila existente**, y EF Core no lo permite: el discriminador de una
entidad seguida no se puede modificar. La única salida sería borrar la `Persona` y reinsertarla como
`Chofer`, y ahí se cae todo lo demás:

- pierde el `Id`, y con él el vínculo con su cuenta de usuario;
- el `OnDelete(DeleteBehavior.Restrict)` de `Usuario → Persona` **bloquea el borrado** si tiene cuenta;
- perdería su documentación ya cargada.

Hay además un segundo efecto: bajo TPH, las columnas de un subtipo son siempre nullable en la base,
así que `TransportistaId` —que FR-008 exige obligatorio— sólo se podría hacer cumplir por código.
Con tabla propia es `NOT NULL` de verdad.

La composición cumple igual lo que FR-006 pide de fondo: *"los datos personales viven en Persona y NO
DEBEN duplicarse"*. El chofer no copia nombre, apellido ni DNI: los referencia.

**Alternativas consideradas**:

- **Herencia TPH o TPT**: descartada por lo anterior. Se podría sostener sólo si se cambiara el caso
  límite, obligando a dar de baja y volver a cargar a quien pasa de empleado a chofer. Es peor para
  quien opera el sistema y se descartó como decisión de producto.
- **Aplanar los campos de chofer sobre `Persona`** (CUIL y transportista nullables en el padrón):
  evita la tabla, pero ensucia una entidad que el Módulo 2 definió con exactamente siete campos
  (FR-026 de aquel módulo) y tampoco permite exigir el transportista. Descartada.

**Consecuencia que hay que decidir, no dejar pasar**: con dos representaciones posibles de "es
chofer" —la fila en `Choferes` y el campo `Persona.Tipo` del Módulo 2— hace falta una regla. **La fila
en `Choferes` es la única fuente de verdad**; `Persona.Tipo` queda como dato informativo del padrón y
este módulo no lo consulta para nada. Como efecto, el Módulo 2 recibe un cambio chico: su
`DarDeBajaPersona` hoy rechaza si la persona tiene usuario, y ahora también debe rechazar si tiene
chofer (§7).

**El desplegable de Tipo del Módulo 2 no se toca** (decisión del 2026-08-06). `FormularioPersona.tsx`
deja elegir entre *chofer* y *empleado*, así que alguien puede marcar como empleado a una persona que
sí es chofer. Se evaluó volverlo de sólo lectura o sacarlo, y se descartaron las dos:

- Este módulo **nunca lee** `Persona.Tipo`, así que un valor equivocado no altera ningún cálculo,
  ninguna alerta ni ninguna baja. El daño máximo es una columna confusa en un listado del Módulo 2.
- La spec del Módulo 3 no pide ningún cambio en esa pantalla, y el Principio III prohíbe construir lo
  que la spec no pide. El FR-026 del Módulo 2 definió ese campo como catálogo fijo y editable.

Queda anotado como candidato para una spec futura, no como deuda de este módulo.

---

## 2. El estado de un documento se calcula, no se guarda

**Decisión**: `Documentaciones` **no** tiene columna de estado. `DocumentacionEstado` se deriva de
`fechaVencimiento`, de los `diasAvisoVencimiento` de su tipo y de la fecha del día, con una regla
pura en `GT.Domain/Choferes/CalculadorEstadoDocumento.cs`. La misma expresión se traduce a SQL para
poder **filtrar** por estado sin traer las filas a memoria.

**Rationale**: FR-019 lo pide sin ambigüedad —*"un documento pase por sí solo a `proximaAvencer` y
luego a `vencida` sin intervención de nadie"*— y SC-005 exige que aparezca en el panel *"el mismo día
en que corresponde"*. Una columna almacenada no cambia sola: haría falta un proceso que la recalcule
todas las noches, con todo lo que eso arrastra (dónde corre, qué pasa si un día no corrió, cómo se
prueba). Una expresión sobre la fecha no tiene ninguno de esos problemas.

Hay un segundo motivo, menos evidente y más fuerte: el escenario 4 de la User Story 6 exige que
cambiar los `diasAvisoVencimiento` de un tipo **recalcule los documentos existentes**. Con estado
almacenado, ese cambio tendría que disparar una actualización masiva de todas las filas de ese tipo.
Calculado, no hay nada que actualizar.

Las tres reglas, con los bordes que la spec fija explícitamente:

| Situación | Estado |
|---|---|
| `fechaVencimiento` < hoy | `vencida` |
| `fechaVencimiento` entre hoy **inclusive** y hoy + `diasAvisoVencimiento` | `proximaAvencer` |
| más lejos que eso | `vigente` |

El "hoy inclusive" no es un detalle: la spec dice que un documento que vence **exactamente hoy** es
`proximaAvencer` y recién pasa a `vencida` al día siguiente. Y con `diasAvisoVencimiento = 0` la
ventana intermedia se reduce al propio día del vencimiento, que es justo lo que el otro caso límite
describe.

**Alternativas consideradas**:

- **Columna calculada persistida en SQL Server**: no sirve, porque la expresión depende de la fecha
  actual y de una tabla relacionada, y SQL Server sólo admite funciones deterministas sobre columnas
  de la propia fila.
- **Columna común más tarea programada nocturna**: es la opción que parece natural y la que más
  cuesta. Descartada por Principio I.
- **Calcular sólo en memoria, sin traducir a SQL**: obligaría a traer todos los documentos de todos
  los choferes para poder filtrar por estado (FR-022). Descartada por costo.

**Costo aceptado**: el filtro por estado no puede apoyarse en un índice sobre el estado. A la escala
del sistema —cientos de documentos— es irrelevante, y sí se indexa `fechaVencimiento`, que es lo que
la expresión recorre.

---

## 3. Archivos adjuntos: `IFormFile` y un volumen, servidos por endpoint autorizado

**Decisión**: el archivo se sube con `multipart/form-data`, se guarda en un volumen del compose bajo
un nombre generado por el sistema, y la fila conserva la ruta relativa. Se descarga por
`GET /api/documentacion/{id}/archivo`, que **exige sesión y el permiso del módulo**. El volumen queda
fuera de la raíz web y fuera del repositorio. Se aceptan **PDF, JPG y PNG de hasta 10 MB**
(FR-015a).

> **Decisión confirmada.** Esta sección se escribió antes de la sesión de clarificación del
> 2026-08-06 y advertía que convenía confirmarla. Se confirmó: la spec ahora la fija en FR-015a
> (subida real, con formatos y tamaño) y en FR-024 (descarga restringida a los roles del módulo).
> Lo que era una inferencia del plan pasó a ser requisito.

**Rationale**: la spec asumía que *"el almacenamiento de archivos adjuntos usa el mismo mecanismo que
el resto del sistema"*. **Ese mecanismo no existía**: era la misma clase de supuesto que el Módulo 2
hizo con el correo saliente y que resultó falso. La clarificación reemplazó ese supuesto y dejó la
construcción del almacén dentro de este módulo.

`IFormFile` viene en ASP.NET Core, así que no agrega dependencias. Un volumen del compose es el
almacenamiento más simple que sobrevive al reinicio del contenedor.

Tres decisiones dentro de ésta, cada una con su motivo:

- **El nombre en disco lo genera el sistema**, no el usuario. Un nombre de archivo cargado por
  alguien puede contener `../` y escaparse del directorio; también puede repetirse y pisar otro
  documento. Se guarda con un identificador propio y el nombre original queda como dato de la fila,
  sólo para mostrarlo.
- **La descarga pasa por un endpoint autorizado**, no por un archivo estático. Un psicofísico o una
  licencia son datos personales sensibles: si el volumen se sirviera como contenido estático,
  cualquiera con la URL los vería sin sesión. Con endpoint, conocer la ruta no alcanza.
- **Se limita el tamaño y los tipos aceptados**: PDF, JPG y PNG, hasta 10 MB. Sin límite, un archivo
  suficientemente grande llena el volumen; sin lista de tipos, el sistema termina almacenando y
  devolviendo cualquier cosa. El límite se valida **por la firma del archivo, no por su extensión**:
  renombrar un `.exe` a `.pdf` no debe alcanzar para que entre.

**Alternativas consideradas**:

- **Guardar el archivo en la base como `varbinary(max)`**: evita el volumen y hace que la copia de
  seguridad de la base incluya los archivos, que es una ventaja real. Descartada porque infla la base
  con contenido que nunca se consulta en una cláusula `WHERE`, y encarece toda restauración.
- **Almacenamiento de objetos (S3, Azure Blob)**: es lo correcto a escala, pero acá agrega una
  cuenta, credenciales y una dependencia de red para un sistema que corre en la oficina de una
  empresa. Descartada por Principio I.
- **Guardar sólo una URL que el operador pega**, sin subir nada: era la lectura literal del supuesto
  de la spec y sería lo más barato. **Descartada explícitamente en la clarificación del 2026-08-06**,
  por el motivo que ya se anticipaba acá: un enlace externo no puede respetar FR-027, así que
  cualquiera con la URL vería un psicofísico o un DNI sin pasar por el sistema.

---

## 4. Validación de CUIT y CUIL: dígito verificador, no sólo largo

**Decisión**: `ValidadorCuit` en `GT.Domain/Choferes/`, que normaliza a sólo dígitos (FR-025),
comprueba que sean 11 y valida el **dígito verificador** con el algoritmo estándar argentino. La misma
regla sirve para CUIT y para CUIL: comparten formato.

**Rationale**: FR-003 pide "formato válido". Comprobar sólo que sean once dígitos deja pasar
cualquier número tipeado de más, y un CUIT mal cargado se descubre recién cuando alguien intenta
facturar. El dígito verificador es una multiplicación y un módulo: cuesta nada y atrapa la enorme
mayoría de los errores de tipeo.

Normalizar antes de validar es lo que hace que `20-12345678-3` y `20123456783` no convivan como
registros distintos, que es exactamente lo que pide el caso límite de la spec.

**Alternativas consideradas**:

- **Sólo longitud**: más simple, pero deja entrar datos inválidos que el sistema no puede detectar
  después.
- **Consultar el padrón de AFIP**: la spec pone explícitamente fuera de alcance la verificación
  contra organismos externos.

---

## 5. Autorización: un permiso nuevo para dos roles

**Decisión**: sembrar el permiso `choferes.gestionar` y otorgarlo a *Tráfico* **y** a *Administrador
del sistema* (FR-027), en la migración de este módulo. Los endpoints lo exigen con la misma
`PoliticasAutorizacion.Para(...)` que ya usan los módulos anteriores.

**Rationale**: es el primer módulo cuyo acceso no es exclusivo del administrador, así que el esquema
que el Módulo 1 construyó —autorizar por **permiso** y no por rol, con los permisos recalculados en
cada petición— se ejercita por primera vez de verdad. No hace falta nada nuevo: alcanza con sembrar
el permiso y asignárselo a los dos roles.

Las opciones de menú salen del `CatalogoOpcionesMenu`, que ya traduce permisos a entradas del lado
del servidor. Un usuario de Tráfico va a ver las entradas de este módulo y ninguna del Módulo 2, sin
que el frontend tenga que decidir nada.

**Alternativas consideradas**:

- **Reusar `usuarios.gestionar`**: dejaría el módulo fuera del alcance de Tráfico, que es justamente
  quien lo usa. Incumple FR-027.
- **Un permiso por operación** (`choferes.crear`, `choferes.editar`…): la spec no distingue niveles de
  acceso dentro del módulo. Sería complejidad anticipada. Descartada por Principio I.

---

## 6. Reutilización de la persona al registrar un chofer

**Decisión**: el alta de un chofer normaliza el DNI y **busca primero en el padrón**. Si existe, se
reutiliza esa `Persona` y sólo se crea la fila de `Choferes`; si no, se crean las dos en la misma
operación. Si la persona encontrada **ya es chofer**, se rechaza como duplicado.

**Rationale**: es el caso límite explícito de la spec, y es también lo que sostiene FR-006: el DNI
sigue siendo único en todo el padrón porque no hay un segundo padrón donde repetirlo.

El orden importa: buscar antes de validar el duplicado permite distinguir dos situaciones que para
quien opera son muy distintas —"esta persona ya está cargada, la reutilizo" y "esta persona ya es
chofer, no la puedo cargar de nuevo"— y darles mensajes distintos.

**Alternativas consideradas**:

- **Pedir que primero se cargue la persona desde el Módulo 2 y después elegirla acá**: es más simple
  de programar y peor de usar, porque obliga a Tráfico a entrar a un módulo al que ni siquiera tiene
  acceso (FR-027 no les da `usuarios.gestionar`). Descartada.
- **Crear siempre una persona nueva**: rompe la unicidad del DNI y el propósito de FR-006.

---

## 7. Lo que ya está resuelto y no hay que volver a construir

| Requisito de este módulo | Lo resuelve | Trabajo pendiente |
|---|---|---|
| FR-027 — acceso restringido por rol | El `PermisoHandler` y las políticas del Módulo 1, que evalúan por permiso y lo recalculan en cada petición | Sembrar `choferes.gestionar` y agregar `.RequireAuthorization(...)`. Ningún mecanismo nuevo |
| FR-006 — los datos personales viven en `Persona` | El padrón del Módulo 2, con su índice único de DNI y su ABM completo | Sólo referenciarlo desde `Choferes` |
| FR-025 — normalizar antes de validar unicidad | El patrón ya establecido en los Módulos 1 y 2 (`NormalizadorUsername`, `NormalizadorEmail`) | Un normalizador más para documentos numéricos, con la misma forma |
| Confirmación previa a una baja (FR-026) | El componente `DialogoConfirmacion` del Módulo 2, con foco, `Escape` y devolución del foco ya resueltos | Reutilizarlo |

**Un cambio al Módulo 2**: `DarDeBajaPersona` hoy rechaza la baja si la persona está vinculada a un
usuario. Con este módulo, una persona también puede estar vinculada a un chofer, y darla de baja
dejaría un chofer activo apuntando a una persona inactiva. Hay que extender esa validación. Es el
único archivo del Módulo 2 con cambio de comportamiento.

---

## 8. El documento vigente de cada tipo: el de vencimiento más lejano

**Decisión**: para cada par chofer–tipo, el documento **vigente** es el de `FechaVencimiento` mayor,
y con empate, el de `Id` mayor. Sólo ese documento alimenta el estado general del chofer (FR-029) y
el panel de vencimientos (FR-021). Los demás son historial: se muestran en la ficha y no alertan.

**Rationale**: lo fija FR-020a, y sin esa regla el módulo no cumpliría su objetivo. FR-020 conserva
las renovaciones como historial; si el historial siguiera contando, un chofer que renovó la licencia
arrastraría para siempre la vencida y el panel se llenaría de alertas que nadie puede resolver. El
escenario 3 de la User Story 5 lo dice al revés: cargar la renovación **saca** al chofer del panel.

**Por qué el vencimiento y no la fecha de carga**: "más reciente" podría leerse como el último
cargado. Se descartó porque el orden de carga no es confiable —nada impide cargar primero la
renovación y después el documento viejo que faltaba— y porque lo que habilita a manejar es la fecha
hasta la que el documento vale, no cuándo se lo tipeó. FR-020a lo define así de forma explícita.

**El empate hay que resolverlo, no dejarlo librado**: dos documentos del mismo tipo con el mismo
vencimiento son un error de carga plausible (alguien carga dos veces el mismo papel). Sin desempate,
la consulta devuelve filas distintas según el plan de ejecución y el listado cambia solo entre dos
consultas. Con `Id` mayor como segundo criterio, el resultado es estable y además es el más
razonable: gana el que se cargó último.

En SQL es una función de ventana sobre `Documentaciones`:

```text
ROW_NUMBER() OVER (
  PARTITION BY ChoferId, DocumentacionTipoId
  ORDER BY FechaVencimiento DESC, Id DESC
) = 1
```

Se resuelve en la base, igual que el cálculo del estado (§2), así que el filtro por estado general
sigue sin traer documentos a memoria. En EF Core sale de un `GroupBy` sobre las dos claves con un
`Max`, o de una subconsulta correlacionada; las dos se traducen sin `AsEnumerable`.

**Alternativas consideradas**:

- **Una columna `Vigente` (bit) que se actualiza al cargar una renovación**: hace la consulta
  trivial, pero introduce un dato derivado que puede quedar desincronizado —dos altas simultáneas,
  una baja, una corrección de fecha— y obliga a mantenerlo en cada operación de escritura. Es
  exactamente el problema que §2 evita con el estado. Descartada por coherencia y por Principio I.
- **Marcar la renovación a mano**, con el operador eligiendo qué documento reemplaza a cuál: es la
  opción C que la clarificación descartó. Agrega un paso a la operación más frecuente del módulo para
  resolver algo que la fecha ya responde.

---

## 9. Paginación del listado de choferes

**Decisión**: `GET /choferes` devuelve una página de **20 filas** con el total de coincidencias
(FR-030). Los filtros se aplican en la base **antes** de paginar. Sin filtro de estado, la consulta
agrega `Activo = 1` por defecto (FR-022). El orden es `Apellido, Nombre, Id`.

**Rationale**: FR-030 lo fija. La parte que la spec no dice y el diseño tiene que resolver es el
**orden**: una paginación sin orden estable no es una paginación, porque SQL Server no garantiza el
mismo orden entre dos consultas y una fila puede aparecer en dos páginas o en ninguna. Se ordena por
apellido y nombre —que es como se busca a una persona— con `Id` como desempate final, que garantiza
un orden total aunque haya dos choferes homónimos.

El total viaja junto con la página: sin él, la pantalla no puede decir cuántos coinciden ni cuántas
páginas hay, y FR-030 lo pide explícitamente. Son dos consultas contra la base —un `COUNT` y un
`SELECT` con `OFFSET/FETCH`— sobre el mismo filtro.

**El default de activos es un filtro, no una vista aparte**: la consulta es la misma; lo único que
cambia es que, si no vino `estado`, se agrega la condición. Así el listado sin filtros muestra la
operación del día y el padrón completo sigue a un clic (FR-022).

**Alternativas consideradas**:

- **Sin paginación, devolviendo todo el padrón**: era lo que este contrato hacía antes de la
  clarificación y lo que hace el Módulo 2. A la escala del sistema —decenas de choferes— funcionaría.
  Se descartó porque la clarificación lo decidió al revés, y porque el listado de choferes crece con
  la operación mientras que el de usuarios no.
- **Paginar en el frontend, trayendo todo**: da la misma pantalla con menos trabajo en el backend,
  pero el total y las páginas dejarían de ser lo que la base sabe, y el costo de la consulta no
  bajaría en absoluto. Descartada.
- **Scroll infinito**: es la opción C de la clarificación. Descartada por decisión del usuario;
  además complica saber "cuántos hay", que es dato de gestión.

> **El Módulo 2 no pagina sus listados.** Este módulo estrena la paginación en el sistema, así que la
> forma de la respuesta (`{ items, total, pagina, tamanioPagina }`) queda como precedente para los
> módulos siguientes. Es la única inconsistencia deliberada con lo ya entregado.

---

## 10. Corrección y eliminación de documentos: la fila manda, el archivo la sigue

**Decisión**: el orden de las operaciones es siempre el mismo — **el archivo se escribe antes de
confirmar la fila y se borra después de confirmarla**:

| Operación | Orden |
|---|---|
| Crear con archivo | Escribir el archivo → abrir transacción → insertar la fila → confirmar. Si algo falla, deshacer la transacción **y borrar el archivo recién escrito** |
| Corregir reemplazando el archivo | Escribir el archivo nuevo → actualizar la fila → confirmar → borrar el archivo viejo. Si la actualización falla, borrar el archivo nuevo y dejar el viejo intacto |
| Eliminar el documento | Borrar la fila → confirmar → borrar el archivo. Nunca al revés |

**Rationale**: FR-015e pide que la carga sea todo o nada, y FR-015c que la eliminación se lleve el
archivo. La base es transaccional; el sistema de archivos no. No existe forma de confirmar los dos a
la vez sin un coordinador de dos fases, que para este sistema es desproporcionado (Principio I).

Como no se puede evitar toda ventana de falla, **se elige cuál de los dos estados rotos es
aceptable**:

| Estado roto posible | ¿Se acepta? |
|---|---|
| Un archivo en el volumen que ninguna fila referencia | **Sí**. Es invisible para quien opera, no ocupa lugar en la base y no miente sobre nada |
| Una fila que dice tener archivo y apunta a un archivo que no existe | **No**. Es exactamente lo que FR-015e evita: el operador cree que subió el psicofísico y no está |

El orden de arriba es el que garantiza que sólo pueda ocurrir el primero. Escribir el archivo
después de confirmar la fila —el orden intuitivo— produce justamente el segundo.

**Consecuencia asumida**: si el proceso se cae entre escribir el archivo y confirmar la transacción,
queda un archivo huérfano. No hay requisito de limpieza y no se construye ninguno: sería alcance
fantasma para un caso que, a este volumen, produce unos pocos kilobytes al año. Queda anotado como
candidato si alguna vez importa.

**Sobre la eliminación física**: el documento es la única entidad del módulo que se borra de verdad
(FR-015d). Va contra la convención de baja lógica del resto, y por eso está escrito como requisito y
repetido acá: no es un descuido. El motivo es que un documento cargado por error no es un hecho
histórico que convenga conservar —es basura que además puede tapar el estado real del chofer, porque
el vigente de cada tipo es el de vencimiento más lejano (§8)—.

**Alternativas consideradas**:

- **Baja lógica del documento, con `Activo`**, como el resto de las entidades: más consistente, y
  descartada porque la spec decidió lo contrario de forma explícita. Habría obligado además a filtrar
  por `Activo` en todas las consultas de §8, incluida la función de ventana.
- **Guardar el archivo dentro de la transacción de la base** (`varbinary(max)`): resolvería la
  atomicidad de una, y es la única alternativa que la resuelve del todo. Ya se descartó en §3 por el
  costo sobre la base y las restauraciones; la atomicidad no alcanza para revertir aquella decisión,
  porque el estado roto que evita es el que acá ya se acepta.
- **Borrar el archivo primero y después la fila**: deja el peor de los dos estados rotos si falla la
  segunda parte. Descartada.
