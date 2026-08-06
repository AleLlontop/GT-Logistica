# Modelo de datos: Gestionar choferes y su documentación (Módulo 3)

Fase 1 del plan. Parte del esquema que dejaron los Módulos 1 y 2 y agrega cuatro tablas. **No
modifica ninguna columna existente**: `Personas` se referencia, no se altera.

Todo entra en **una sola migración**. Las decisiones de fondo están en [research.md](./research.md)
§1, §2 y §3.

---

## Panorama

```text
Transportista ──1────*── Chofer ──*────1── Persona   (padrón del Módulo 2)
                            │                 │
                            │                 └──0..1── Usuario   (Módulo 2)
                            │
                            1
                            │
                            *
                      Documentacion ──*────1── DocumentacionTipo
```

El chofer **referencia** a la persona, no la especializa en el sentido de la herencia: es una fila
aparte con clave foránea única (research §1). Los datos personales no se duplican, que es lo que
FR-006 exige de fondo.

---

## Transportista (tabla `Transportistas`) — NUEVA

Empresa o persona que aporta choferes, incluida G&T Logística S.A. como una fila más (FR-004).

| Columna | Tipo | Reglas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `Nombre` | `nvarchar(200)`, obligatorio | Nombre o razón social |
| `Cuit` | `nvarchar(11)`, obligatorio, **único** | Sólo dígitos, 11, con dígito verificador válido (FR-003, FR-025) |
| `Tipo` | `tinyint`, obligatorio | `1` física, `2` jurídica (FR-002) |
| `Telefono` | `nvarchar(30)`, obligatorio | |
| `Email` | `nvarchar(254)`, obligatorio | Formato válido, **sin** unicidad |
| `Activo` | `bit`, obligatorio | `true` al crear. La baja lógica lo pone en `false` (FR-001) |

**Índices**: `IX_Transportistas_Cuit` único.

**Reglas**

| Regla | Requisito |
|---|---|
| CUIT único en todo el padrón; en modificación la comparación excluye al propio | FR-003 |
| El tipo de persona es obligatorio y sólo admite `fisica` o `juridica` | FR-002 |
| No se puede dar de baja si tiene **al menos un chofer activo**; el mensaje dice cuántos son | FR-010 |
| La baja procede si todos sus choferes están inactivos, o si no tiene ninguno | FR-010, caso límite |
| G&T Logística S.A. no recibe trato especial en ninguna de estas reglas | FR-004, caso límite |

---

## Chofer (tabla `Choferes`) — NUEVA

| Columna | Tipo | Reglas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `PersonaId` | `int`, obligatorio, **único** | FK a `Personas`. Único: una persona es chofer a lo sumo una vez |
| `Cuil` | `nvarchar(11)`, obligatorio, **único** | Sólo dígitos, 11, con dígito verificador válido (FR-007, FR-025) |
| `TransportistaId` | `int`, **obligatorio** | FK a `Transportistas`. `NOT NULL` real, no sólo por código (FR-008) |
| `Activo` | `bit`, obligatorio | `true` al crear. La baja lógica lo pone en `false` (FR-005) |

**Índices**

| Índice | Columnas | Tipo |
|---|---|---|
| `IX_Choferes_PersonaId` | `PersonaId` | Único |
| `IX_Choferes_Cuil` | `Cuil` | Único |
| `IX_Choferes_TransportistaId` | `TransportistaId` | Común, para el filtro del listado |

> A diferencia del índice de `Usuarios.PersonaId` del Módulo 2, éste **no** lleva filtro: `PersonaId`
> es obligatorio acá, así que no hay `NULL` que SQL Server pueda confundir entre sí.

**De dónde salen los datos personales**

El chofer no guarda nombre, apellido, DNI, teléfono, email ni fecha de nacimiento: los toma de su
`Persona` (FR-006). Editar esos datos desde la ficha del chofer actualiza la fila de `Personas`, que
es la misma que ve el padrón del Módulo 2.

**Reglas**

| Regla | Requisito |
|---|---|
| Todo chofer pertenece a exactamente un transportista **activo** | FR-008 |
| Se puede reasignar a otro transportista activo sin tocar su documentación | FR-009, SC-009 |
| CUIL único en todo el padrón; en modificación excluye al propio chofer | FR-007 |
| El DNI sigue siendo único en todo el padrón, choferes y empleados incluidos | FR-006 |
| Menor de 18 años a la fecha del alta: se rechaza | FR-011 |
| Si el DNI ya existe en el padrón, se reutiliza esa persona en vez de duplicarla | Caso límite, research §6 |
| Si esa persona **ya es chofer**, se rechaza como duplicado | Research §6 |
| La baja es lógica: `Activo = false`. El registro no se borra y se consulta filtrando por inactivo | FR-005, FR-022 |
| La baja no toca la documentación: documentos y archivos quedan intactos | FR-005a |
| Un chofer inactivo se reactiva volviendo `Activo` a `true`; su documentación vuelve a contar sola, porque el estado se calcula al consultarlo | FR-005b |
| El listado sin filtros muestra sólo `Activo = true`; el panel de vencimientos, también | FR-021, FR-022 |

---

## DocumentacionTipo (tabla `DocumentacionTipos`) — NUEVA

Catálogo de los documentos que el sistema controla. **Arranca vacío**: no se siembra por migración.

| Columna | Tipo | Reglas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `Nombre` | `nvarchar(100)`, obligatorio, **único** | Licencia de conducir, LiNTI, psicofísico, ART, … (FR-012) |
| `DiasAvisoVencimiento` | `int`, obligatorio | Entero **mayor o igual a cero** (FR-013) |
| `Activo` | `bit`, obligatorio | `true` al crear. La baja lógica lo pone en `false` (FR-012) |

**Índices**: `IX_DocumentacionTipos_Nombre` único.

**Reglas**

| Regla | Requisito |
|---|---|
| Nombre único en el catálogo | FR-012 |
| Días de aviso: entero ≥ 0. Cero significa sin período de aviso intermedio | FR-013, caso límite |
| No se puede dar de baja si tiene documentos asociados; el mensaje dice cuántos | FR-014 |
| Un tipo inactivo deja de ofrecerse al cargar documentación, y su registro no se borra | FR-012 |
| Cambiar sus días de aviso **recalcula** los documentos de ese tipo, sin actualizar ninguna fila | US6 esc. 4, research §2 |

---

## Documentacion (tabla `Documentaciones`) — NUEVA

| Columna | Tipo | Reglas |
|---|---|---|
| `Id` | `int`, identidad | Clave primaria |
| `ChoferId` | `int`, obligatorio | FK a `Choferes` |
| `DocumentacionTipoId` | `int`, obligatorio | FK a `DocumentacionTipos` |
| `Numero` | `nvarchar(50)`, obligatorio | **Sin índice único**: una licencia conserva su número al renovarse, así que dos documentos del mismo chofer y tipo pueden repetirlo (FR-015) |
| `FechaEmision` | `date`, obligatorio | |
| `FechaVencimiento` | `date`, obligatorio | **Posterior** a la emisión (FR-016) |
| `ArchivoRuta` | `nvarchar(400)`, nulo | Ruta relativa dentro del volumen. `null` = documento sin respaldo (research §3) |
| `ArchivoNombre` | `nvarchar(255)`, nulo | Nombre original, sólo para mostrar y para la descarga |
| `ArchivoTipoContenido` | `nvarchar(100)`, nulo | `application/pdf`, `image/jpeg` o `image/png`. Lo determina el sistema al validar la firma del archivo, no la extensión (FR-015a) |

> **No hay columna de estado.** Es la decisión central del módulo: el estado se calcula (research §2).

> **Tampoco hay columna `Activo`.** El documento es la única entidad del módulo que se borra
> físicamente (FR-015d): eliminarlo quita la fila, no la marca. Es deliberado y va contra la
> convención del resto de las tablas, así que está escrito en los dos lados (research §10).

**Índices**

| Índice | Columnas | Para qué |
|---|---|---|
| `IX_Documentaciones_ChoferId_TipoId_Vencimiento` | `ChoferId`, `DocumentacionTipoId`, `FechaVencimiento DESC` | Traer la ficha de un chofer **y** resolver cuál es el documento vigente de cada tipo (research §8) sin ordenar en memoria |
| `IX_Documentaciones_DocumentacionTipoId` | `DocumentacionTipoId` | Contar los que usan un tipo, al intentar darlo de baja |
| `IX_Documentaciones_FechaVencimiento` | `FechaVencimiento` | El panel de vencimientos y el filtro por estado |

> El primer índice reemplaza al que sólo tenía `ChoferId`: lo contiene como prefijo, así que sigue
> sirviendo para la ficha y además cubre la función de ventana que elige el documento vigente.

**Reglas**

| Regla | Requisito |
|---|---|
| El vencimiento tiene que ser posterior a la emisión | FR-016 |
| Se puede modificar tipo, número, fechas y archivo, con las mismas validaciones del alta | FR-015b |
| Se puede eliminar, con confirmación previa. **El borrado es físico**: la fila desaparece y su archivo también | FR-015c, FR-015d |
| La escritura del archivo y la de la fila son todo o nada: nunca queda una fila que diga tener archivo sin archivo | FR-015e, research §10 |
| Un chofer puede tener varios documentos del mismo tipo; los anteriores quedan como historial | FR-020 |
| De cada tipo, el **vigente** es el de vencimiento más lejano —con `Id` mayor como desempate—; los demás son historial y no alertan | FR-020a, research §8 |
| El archivo adjunto es opcional, pero el sistema distingue el documento con respaldo del que no lo tiene | FR-015, caso límite |
| Si viene archivo: sólo PDF, JPG o PNG, hasta 10 MB, validado por firma y no por extensión | FR-015a |
| Sólo se aceptan tipos de documentación **activos** | FR-012 |

---

## Estado de un documento — calculado, nunca almacenado

`DocumentacionEstado` es un valor derivado. Se calcula con `fechaVencimiento`, los
`diasAvisoVencimiento` de su tipo y la fecha del día:

| Condición | Estado |
|---|---|
| `fechaVencimiento` **<** hoy | `vencida` |
| hoy **≤** `fechaVencimiento` **≤** hoy + `diasAvisoVencimiento` | `proximaAvencer` |
| `fechaVencimiento` **>** hoy + `diasAvisoVencimiento` | `vigente` |

Dos bordes que la spec fija explícitamente y que los tests tienen que cubrir:

- **Vence exactamente hoy** → `proximaAvencer`, no `vencida`. Pasa a `vencida` recién mañana.
- **`diasAvisoVencimiento = 0`** → no hay ventana intermedia: el documento es `vigente` hasta el día
  del vencimiento inclusive, y `vencida` al día siguiente.

La misma expresión se traduce a SQL para poder filtrar por estado sin traer las filas (FR-022).

---

## Documento vigente de cada tipo — el que manda

Un chofer puede tener varios documentos del mismo tipo (FR-020). De cada tipo, **uno solo cuenta**:
el de `FechaVencimiento` mayor, y con empate, el de `Id` mayor (FR-020a, research §8).

| Documento | Rol |
|---|---|
| El de vencimiento más lejano de su tipo | **Vigente**: define el estado general del chofer y puede alertar |
| Cualquier otro del mismo tipo | **Historial**: se ve en la ficha, no define nada y no alerta |

Es la regla que hace que cargar una renovación saque al chofer del panel sin que nadie tenga que
borrar ni editar el documento viejo (SC-010).

Como el vigente se determina en cada consulta y no se guarda, **eliminar o corregir un documento
reacomoda la vigencia sola**: si se elimina el vigente, el siguiente de ese tipo pasa a mandar; si se
corrige una fecha de vencimiento mal tipeada, la vigencia se recalcula con el valor nuevo. No hay
ninguna columna que actualizar (FR-015b, FR-015c).

---

## Estado general de un chofer — calculado, cuatro valores

FR-029 lo define con exactamente cuatro valores. Se calcula en dos pasos:

1. Tomar el documento **vigente** de cada tipo (regla de arriba).
2. Quedarse con el **peor** de esos estados.

| Situación | Estado general |
|---|---|
| El chofer no tiene ningún documento cargado | `sinDocumentacion` |
| Al menos un documento vigente `vencida` | `vencida` |
| Ninguno vencido, al menos uno `proximaAvencer` | `proximaAvencer` |
| Todos los documentos vigentes están al día | `enRegla` |

El orden de precedencia es `vencida` > `proximaAvencer` > `enRegla`.

Dos cosas que los tests tienen que cubrir:

- **`sinDocumentacion` no es `enRegla`.** Un chofer sin papeles no está en regla por ausencia de
  papeles (FR-028): son dos valores distintos y la pantalla los muestra distinto.
- **Ningún tipo del catálogo es obligatorio** (FR-029a). El cálculo mira los documentos cargados y no
  compara contra una lista esperada: un chofer con un solo documento al día es `enRegla`. No hay
  quinto valor para "le falta un documento", y `DocumentacionTipos` no lleva ninguna columna que
  distinga obligatorios de opcionales.
- **El adjunto no entra en el cálculo.** `ArchivoRuta` en `null` es un dato del documento, no del
  chofer: no cambia el estado general (FR-029).
- **Un documento vencido reemplazado por una renovación no ensucia el estado.** El chofer con la
  licencia renovada y la vieja vencida en el historial está `enRegla`, no `vencida` (FR-020a).

> **Nota de terminología**: el estado del chofer usa `enRegla`, mientras que el de un documento usa
> `vigente`. Son escalas distintas a propósito —una describe un papel, la otra a una persona— y
> mantenerlas con nombres distintos evita que un `vigente` en una respuesta signifique dos cosas
> según dónde aparezca.

---

## Orden y paginación del listado

FR-030 pagina el listado de choferes de a 20 filas. Para que la paginación sea correcta hace falta un
**orden total y estable**: `Apellido`, `Nombre`, `Id`. Sin el `Id` final, dos choferes homónimos
podrían intercambiarse entre consultas y aparecer duplicados en una página o desaparecer de otra
(research §9).

El filtro se aplica antes de paginar, y sin filtro de estado se agrega `Activo = 1` (FR-022).

---

## Migración

Un único archivo de migración de EF Core:

1. Crear `Transportistas` con su índice único de `Cuit`.
2. Crear `DocumentacionTipos` con su índice único de `Nombre`.
3. Crear `Choferes` con sus dos índices únicos (`PersonaId`, `Cuil`), el común de
   `TransportistaId`, y las claves foráneas a `Personas` y `Transportistas`, ambas con
   `DeleteBehavior.Restrict`.
4. Crear `Documentaciones` con sus tres índices y sus claves foráneas.
5. Sembrar el permiso `choferes.gestionar` y otorgarlo a los roles *Tráfico* y *Administrador del
   sistema* (FR-027, research §5).

**Sin datos de negocio.** No se siembra ningún transportista —ni siquiera G&T Logística S.A., que se
carga desde la pantalla como cualquier otro (FR-004)— ni ningún tipo de documentación. Lo único que
la migración siembra es el permiso, que es infraestructura de autorización y no dato del negocio.

**Ninguna columna existente cambia.** `Personas`, `Usuarios`, `Roles` y `Permisos` quedan como
estaban; sólo se agregan filas al catálogo de permisos.
